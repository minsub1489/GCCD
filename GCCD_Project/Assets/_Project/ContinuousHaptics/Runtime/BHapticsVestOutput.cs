using System;
using UnityEngine;

namespace GCCD.ContinuousHaptics
{
    public abstract class HapticVestOutput : MonoBehaviour
    {
        public abstract string Status { get; }
        public abstract bool TrySend(int[] motors, int durationMillis);
        public abstract void Stop();
    }

    [DisallowMultipleComponent]
    public sealed class BHapticsVestOutput : HapticVestOutput
    {
        [Tooltip("Only for a renamed X40 whose identity you verified in Player. SDK exposes no model ID.")]
        public bool ConfirmRenamedDeviceIsX40;
        [SerializeField] string status = "Not checked";
        // Registered by the optional, compile-time SDK adapter in the default assembly.
        // Core math, preview, and tests compile when the licensed SDK is absent.
        public static Func<IX40SdkBackend> BackendFactory;
        IX40SdkBackend backend;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBackend() { BackendFactory = null; }
        int requestId = -1;
        double expiresAt, blockedUntil;
        string lastError;
        public override string Status => status;
        public int LastRequestId => requestId;

        public bool CheckConnection()
        {
            if (!isActiveAndEnabled || !Application.isPlaying) { status = "Output inactive"; return false; }
            try
            {
                if (backend == null) backend = BackendFactory?.Invoke();
                if (backend == null)
                { status = "Preview available. Install SDK2 and enable GCCD_BHAPTICS_SDK2 for hardware."; return false; }
                return backend.CheckConnection(ConfirmRenamedDeviceIsX40, out status);
            }
            catch (Exception e) { Fail(e); return false; }
        }
        public override bool TrySend(int[] motors, int durationMillis)
        {
            if (motors == null || motors.Length != X40MotorLayout.Count || durationMillis < 1 || durationMillis > 250)
            { Stop(); status = "Invalid output frame"; return false; }
            bool any = false;
            foreach (int value in motors)
            {
                if (value < 0 || value > 100) { Stop(); status = "Invalid motor intensity"; return false; }
                any |= value > 0;
            }
            if (!any) { Stop(); status = "Silent frame"; return true; }
            if (Time.unscaledTimeAsDouble < blockedUntil)
            { status = "Waiting for previous pulse expiry"; return false; }
            if (!CheckConnection()) { Stop(); return false; }
            try
            {
                // Retire our previous request so overlapping frames cannot add intensity.
                // Never StopAll: other SDK consumers may own unrelated feedback.
                if (!TryStopOwned()) return false;
                requestId = backend.PlayMotors(motors, durationMillis);
                expiresAt = Time.unscaledTimeAsDouble + durationMillis / 1000.0;
                if (requestId < 0) { status = "PlayMotors rejected the frame"; return false; }
                lastError = null;
                return true;
            }
            catch (Exception e) { Fail(e); Stop(); return false; }
        }
        bool TryStopOwned()
        {
            if (requestId < 0) return true;
            int old = requestId; requestId = -1;
            if (Time.unscaledTimeAsDouble >= expiresAt) return true;
            try
            {
                if (!backend.StopRequest(old))
                { blockedUntil = expiresAt; status = "Stop request failed; waiting for bounded pulse expiry"; return false; }
                return true;
            }
            catch (Exception e) { blockedUntil = expiresAt; Fail(e); return false; }
        }
        public override void Stop() { TryStopOwned(); }
        void Fail(Exception e)
        {
            status = "SDK unavailable: " + e.GetType().Name;
            if (lastError != status) { lastError = status; Debug.LogWarning(status, this); }
        }
        void OnDisable() => Stop();
        void OnDestroy() => Stop();
        void OnApplicationQuit() => Stop();
        void OnApplicationPause(bool paused) { if (paused) Stop(); }
        void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
    }
}
