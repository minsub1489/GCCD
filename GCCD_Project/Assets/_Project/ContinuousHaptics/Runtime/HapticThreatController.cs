using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.ContinuousHaptics
{
    [DisallowMultipleComponent]
    public sealed class HapticThreatController : MonoBehaviour
    {
        public Transform Player;
        public Transform Target;
        public HapticVestOutput Output;
        public bool EnableHaptics = true;
        public DistanceIntensityMapper Distance = new DistanceIntensityMapper();
        public HapticSpatialMapper Spatial = new HapticSpatialMapper();
        [Range(5, 30)] public float HapticUpdateRate = 10;
        public bool EnableSmoothing = true;
        [Min(0.01f)] public float DirectionSmoothingSpeed = 15;
        [Min(0.01f)] public float IntensitySmoothingSpeed = 10;
        [Header("Optional off-screen filter (ignored in test mode)")]
        public bool OffScreenOnly;
        public Camera ViewCamera;
        [Header("Inspector / keyboard test")]
        public bool TestMode;
        public bool EnableKeyboardTest;
        [Range(0, 360)] public float TestAzimuth;
        [Range(-90, 90)] public float TestElevation;
        [Min(0)] public float TestDistance = 5;
        [Min(0)] public float TestAngularSpeed = 60;
        [Min(0)] public float TestDistanceSpeed = 3;
        public bool DebugMode = true;
        [SerializeField, HideInInspector] float currentDistance, currentAzimuth, currentElevation;
        [SerializeField, HideInInspector] float targetIntensity, smoothedIntensity, smoothedAzimuth, smoothedElevation;
        [SerializeField, HideInInspector] Vector2 vestPosition;
        [SerializeField, HideInInspector] string status = "Idle";
        readonly float[] weights = new float[40];
        readonly int[] motors = new int[40];
        bool hasSample, paused, unfocused, previousTestMode;
        Transform previousTarget;
        double nextTick, lastTick;
        public float CurrentDistance => currentDistance;
        public float CurrentAzimuth => currentAzimuth;
        public float CurrentElevation => currentElevation;
        public float TargetIntensity => targetIntensity;
        public float SmoothedIntensity => smoothedIntensity;
        public float SmoothedAzimuth => smoothedAzimuth;
        public float SmoothedElevation => smoothedElevation;
        public Vector2 VestPosition => vestPosition;
        public string Status => status;
        public int MotorIntensity(int index) => motors[index];
        public float MotorWeight(int index) => weights[index];
        // Subscribers must read buffers synchronously; the arrays are reused next tick.
        public event Action<HapticThreatController, bool> FrameComputed;

        void OnEnable() { nextTick = 0; hasSample = false; lastTick = Time.unscaledTimeAsDouble; }
        void Update()
        {
            if (paused || unfocused) { StopNow("Application paused / unfocused"); return; }
            if (TestMode && EnableKeyboardTest && Distance != null) ReadKeys();
            // Validate every render frame for immediate target-loss and invalid-input stops.
            if (!TryInput(out _)) { StopNow("Missing / invalid input or on-screen target"); return; }
            if (!EnableHaptics) Output?.Stop();
            if (Time.unscaledTimeAsDouble >= nextTick) Tick(Time.unscaledTimeAsDouble);
        }
        bool TryInput(out HapticDirection direction)
        {
            direction = default;
            if (Distance == null || Spatial == null || !Distance.IsValid || !Spatial.IsValid
                || !HapticMath.Finite(HapticUpdateRate) || HapticUpdateRate < 5 || HapticUpdateRate > 30
                || !HapticMath.Finite(DirectionSmoothingSpeed) || DirectionSmoothingSpeed <= 0
                || !HapticMath.Finite(IntensitySmoothingSpeed) || IntensitySmoothingSpeed <= 0) return false;
            if (TestMode)
            {
                direction = new HapticDirection(TestAzimuth, TestElevation, TestDistance);
                return direction.IsValid;
            }
            if (!Player || !Target || !Target.gameObject.activeInHierarchy || !Player.gameObject.activeInHierarchy) return false;
            if (OffScreenOnly)
            {
                if (!ViewCamera || !ViewCamera.isActiveAndEnabled) return false;
                Vector3 v = ViewCamera.WorldToViewportPoint(Target.position);
                if (!HapticMath.Finite(v)) return false;
                if (v.z >= ViewCamera.nearClipPlane && v.z <= ViewCamera.farClipPlane
                    && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1) return false;
            }
            return HapticDirectionCalculator.TryCalculate(Player, Target.position, currentAzimuth, out direction);
        }
        public void Tick(double now)
        {
            if (!isActiveAndEnabled || paused || unfocused || double.IsNaN(now) || double.IsInfinity(now))
            { StopNow("Inactive / invalid time"); return; }
            if (!TryInput(out var sample)) { StopNow("Missing / invalid input or on-screen target"); return; }
            if (Target != previousTarget || TestMode != previousTestMode)
            { Output?.Stop(); hasSample = false; previousTarget = Target; previousTestMode = TestMode; }
            float dt = hasSample ? Mathf.Clamp((float)(now - lastTick), 0, 1) : 1f / HapticUpdateRate;
            lastTick = now; nextTick = now + 1.0 / HapticUpdateRate; // no catch-up burst after a stall
            currentAzimuth = Mathf.Repeat(sample.Azimuth, 360);
            currentElevation = sample.Elevation; currentDistance = sample.Distance;
            if (!Distance.TryEvaluate(currentDistance, out _, out targetIntensity)) { StopNow("Invalid intensity curve"); return; }
            if (!hasSample || !EnableSmoothing)
            { smoothedAzimuth = currentAzimuth; smoothedElevation = currentElevation; }
            else
            {
                float t = HapticMath.Alpha(DirectionSmoothingSpeed, dt);
                smoothedAzimuth = Mathf.Repeat(Mathf.LerpAngle(smoothedAzimuth, currentAzimuth, t), 360);
                smoothedElevation = Mathf.Lerp(smoothedElevation, currentElevation, t);
            }
            smoothedIntensity = EnableSmoothing
                ? Mathf.Lerp(smoothedIntensity, targetIntensity, HapticMath.Alpha(IntensitySmoothingSpeed, dt)) : targetIntensity;
            smoothedIntensity = Mathf.Min(smoothedIntensity, Distance.MaximumIntensity);
            // Far cutoff must stop even if a custom curve is nonzero at closeness zero.
            if (currentDistance >= Distance.FarDistance) smoothedIntensity = 0;
            hasSample = true;
            float outputIntensity = smoothedIntensity <= Distance.MinimumPerceptibleIntensity ? 0 : smoothedIntensity;
            if (!Spatial.TryMap(smoothedAzimuth, smoothedElevation, weights, out vestPosition)
                || !HapticSpatialMapper.TryQuantize(weights, outputIntensity, motors))
            { StopNow("Invalid spatial frame"); return; }
            bool sent = false;
            if (!EnableHaptics) { Output?.Stop(); status = "Preview only"; }
            else if (!Output || !Output.isActiveAndEnabled) { Output?.Stop(); status = "Output unavailable"; }
            else if (outputIntensity <= 0) { Output.Stop(); status = "Silent: dead zone / far cutoff"; }
            else
            {
                int duration = Mathf.Clamp(Mathf.CeilToInt(1000f / HapticUpdateRate) + 20, 100, 250);
                sent = Output.TrySend(motors, duration);
                status = Output.Status;
            }
            FrameComputed?.Invoke(this, sent);
        }
        public void StopNow(string reason = "Stopped")
        {
            bool hadFrame = hasSample;
            Output?.Stop(); hasSample = false; smoothedIntensity = targetIntensity = 0;
            Array.Clear(motors, 0, motors.Length); Array.Clear(weights, 0, weights.Length);
            status = reason;
            if (hadFrame) FrameComputed?.Invoke(this, false);
        }
        void ReadKeys()
        {
            var k = Keyboard.current;
            if (k == null) return;
            float dt = Time.unscaledDeltaTime;
            TestAzimuth = Mathf.Repeat(TestAzimuth + ((k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0)) * TestAngularSpeed * dt, 360);
            TestElevation = Mathf.Clamp(TestElevation + ((k.upArrowKey.isPressed ? 1 : 0) - (k.downArrowKey.isPressed ? 1 : 0)) * TestAngularSpeed * dt, -90, 90);
            TestDistance = Mathf.Clamp(TestDistance + ((k.pageUpKey.isPressed ? 1 : 0) - (k.pageDownKey.isPressed ? 1 : 0)) * TestDistanceSpeed * dt, 0, Distance.FarDistance);
            if (k.escapeKey.wasPressedThisFrame) { EnableHaptics = false; StopNow("Keyboard stop"); }
        }
        void OnDisable() => StopNow();
        void OnDestroy() => StopNow();
        void OnApplicationQuit() => StopNow();
        void OnApplicationPause(bool value) { paused = value; if (value) StopNow(); else nextTick = 0; }
        void OnApplicationFocus(bool value) { unfocused = !value; if (!value) StopNow(); else nextTick = 0; }
        void OnDrawGizmos()
        {
            if (!DebugMode || !Player) return;
            Vector3 origin = Player.position;
            Vector3 local = TestMode ? HapticDirectionCalculator.ToLocal(TestAzimuth, TestElevation, TestDistance)
                : Target ? Player.InverseTransformDirection(Target.position - origin) : Vector3.zero;
            if (!HapticMath.Finite(local)) return;
            Gizmos.color = Color.cyan; Gizmos.DrawLine(origin, origin + Player.TransformDirection(local));
            Gizmos.color = Color.blue; Gizmos.DrawRay(origin, Player.forward * 2);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, Player.TransformDirection(HapticDirectionCalculator.ToLocal(currentAzimuth, 0, 2)));
        }
    }
}
