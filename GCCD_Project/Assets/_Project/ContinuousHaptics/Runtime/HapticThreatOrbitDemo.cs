using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.ContinuousHaptics
{
    // A moving world-space threat for exercising the map, not an enemy-selection policy.
    public sealed class HapticThreatOrbitDemo : MonoBehaviour
    {
        public Transform InitialCenter;
        public bool Animate = true;
        public float BaseDistance = 8, DistanceAmplitude = 4, ElevationAmplitude = 45;
        public float DegreesPerSecond = 25;
        Vector3 center;
        float elapsed;
        void Start() { center = InitialCenter ? InitialCenter.position : transform.position; }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) Animate = !Animate;
            if (!Animate) return;
            elapsed += Time.deltaTime;
            float radius = Mathf.Max(.1f, BaseDistance + DistanceAmplitude * Mathf.Sin(elapsed * .35f));
            float elevation = Mathf.Clamp(ElevationAmplitude, 0, 90) * Mathf.Sin(elapsed * .25f);
            transform.position = center + HapticDirectionCalculator.ToLocal(135 + elapsed * DegreesPerSecond, elevation, radius);
        }
    }
}
