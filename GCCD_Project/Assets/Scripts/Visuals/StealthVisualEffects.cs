using UnityEngine;

namespace GCCD.Visuals
{
    /// <summary>Ambient-only visuals; not linked to a trial condition or target location.</summary>
    public sealed class StealthVisualEffects : MonoBehaviour
    {
        public Light ambientLightA;
        public Light ambientLightB;
        [Range(0f, 1f)] public float pulseAmount = .12f;
        private float baseA;
        private float baseB;

        private void Awake()
        {
            if (ambientLightA) baseA = ambientLightA.intensity;
            if (ambientLightB) baseB = ambientLightB.intensity;
        }

        private void Update()
        {
            // Gentle, scene-wide electronic ambience. Low modulation to avoid blinding flashes.
            float pulse = Mathf.Sin(Time.time * .9f) * pulseAmount;
            if (ambientLightA) ambientLightA.intensity = baseA * (1 + pulse);
            if (ambientLightB) ambientLightB.intensity = baseB * (1 - pulse);
        }
    }
}
