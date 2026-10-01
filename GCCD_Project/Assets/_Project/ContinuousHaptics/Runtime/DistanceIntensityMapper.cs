using System;
using UnityEngine;

namespace GCCD.ContinuousHaptics
{
    public enum DistanceMode { Continuous, Discrete3Level }

    [Serializable]
    public sealed class DistanceIntensityMapper
    {
        [Min(0)] public float NearDistance = 1.5f;
        [Min(0)] public float FarDistance = 15f;
        [Range(0, 1)] public float MinimumPerceptibleIntensity = 0.05f;
        [Range(0, 1)] public float MaximumIntensity = 0.5f;
        public DistanceMode Mode = DistanceMode.Continuous;
        public AnimationCurve IntensityCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Closeness thresholds: Far -> Mid -> Near. Outside FarDistance always stops.")]
        [Range(0, 1)] public float MidThreshold = 1f / 3f;
        [Range(0, 1)] public float NearThreshold = 2f / 3f;
        [Range(0, 1)] public float FarLevel = 0.25f;
        [Range(0, 1)] public float MidLevel = 0.5f;
        [Range(0, 1)] public float NearLevel = 1f;

        public bool IsValid => HapticMath.Finite(NearDistance) && HapticMath.Finite(FarDistance)
            && NearDistance >= 0 && FarDistance > NearDistance && Unit(MinimumPerceptibleIntensity)
            && Unit(MaximumIntensity) && HapticMath.ValidCurve(IntensityCurve)
            && Unit(MidThreshold) && Unit(NearThreshold) && MidThreshold < NearThreshold
            && Unit(FarLevel) && Unit(MidLevel) && Unit(NearLevel)
            && (Mode == DistanceMode.Continuous || Mode == DistanceMode.Discrete3Level);
        static bool Unit(float x) => HapticMath.Finite(x) && x >= 0 && x <= 1;

        public bool TryEvaluate(float distance, out float closeness, out float intensity)
        {
            closeness = intensity = 0;
            if (!IsValid || !HapticMath.Finite(distance) || distance < 0) return false;
            if (distance >= FarDistance) return true;
            closeness = 1f - Mathf.InverseLerp(NearDistance, FarDistance, distance);
            float value = Mode == DistanceMode.Continuous ? IntensityCurve.Evaluate(closeness)
                : closeness >= NearThreshold ? NearLevel : closeness >= MidThreshold ? MidLevel : FarLevel;
            if (!HapticMath.Finite(value)) return false;
            intensity = Mathf.Clamp01(value) * MaximumIntensity;
            return true; // dead zone is applied AFTER smoothing, without a minimum-intensity floor
        }
    }
}
