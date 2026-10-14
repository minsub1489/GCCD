using System;
using UnityEngine;

namespace GCCD.ContinuousHaptics
{
    public enum SpatialMappingMode { Nearest, LinearInterpolation, Gaussian }

    [Serializable]
    public sealed class HapticSpatialMapper
    {
        public SpatialMappingMode Mode = SpatialMappingMode.LinearInterpolation;
        public AnimationCurve ElevationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Range(0.01f, 0.5f)] public float GaussianSigma = 0.12f;
        [Range(0, 89)] public float PoleBlendStart = 80;
        public bool IsValid => HapticMath.ValidCurve(ElevationCurve) && HapticMath.Finite(GaussianSigma)
            && GaussianSigma >= 0.01f && GaussianSigma <= 0.5f && HapticMath.Finite(PoleBlendStart)
            && PoleBlendStart >= 0 && PoleBlendStart < 90
            && Enum.IsDefined(typeof(SpatialMappingMode), Mode);

        public bool TryMap(float azimuth, float elevation, float[] weights, out Vector2 vest)
        {
            vest = default;
            if (weights == null || weights.Length != X40MotorLayout.Count) return false;
            Array.Clear(weights, 0, weights.Length);
            if (!IsValid || !HapticMath.Finite(azimuth) || !HapticMath.Finite(elevation)
                || elevation < -90 || elevation > 90) return false;
            float y = ElevationCurve.Evaluate((elevation + 90) / 180f);
            if (!HapticMath.Finite(y)) return false;
            vest = new Vector2(Mathf.Repeat(azimuth, 360) / 360f, Mathf.Clamp01(y));
            float column = Mathf.Repeat(azimuth - 22.5f, 360) / 45f;
            float row = (1 - vest.y) * 4;
            if (Mode == SpatialMappingMode.Nearest)
                weights[X40MotorLayout.At(Mathf.RoundToInt(column), Mathf.RoundToInt(row))] = 1;
            else if (Mode == SpatialMappingMode.LinearInterpolation)
            {
                int c = Mathf.FloorToInt(column), r = Mathf.FloorToInt(row), nextRow = Mathf.Min(r + 1, 4);
                float u = column - c, v = row - r;
                weights[X40MotorLayout.At(c, r)] += (1 - u) * (1 - v);
                weights[X40MotorLayout.At(c + 1, r)] += u * (1 - v);
                weights[X40MotorLayout.At(c, nextRow)] += (1 - u) * v;
                weights[X40MotorLayout.At(c + 1, nextRow)] += u * v;
            }
            else
            {
                float minSquared = float.MaxValue;
                for (int i = 0; i < weights.Length; i++)
                {
                    var m = X40MotorLayout.Get(i);
                    float dx = Mathf.DeltaAngle(azimuth, m.Azimuth) / 360f;
                    float dy = vest.y - m.Position.y;
                    weights[i] = dx * dx + dy * dy;
                    minSquared = Mathf.Min(minSquared, weights[i]);
                }
                float sum = 0;
                for (int i = 0; i < weights.Length; i++)
                    sum += weights[i] = Mathf.Exp(-(weights[i] - minSquared) / (2 * GaussianSigma * GaussianSigma));
                for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
            }
            // At the poles azimuth is undefined. Fade to an azimuth-independent top/bottom ring;
            // an abrupt +/-80 degree switch would introduce a spurious tactile boundary.
            float pole = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(PoleBlendStart, 90, Mathf.Abs(elevation)));
            int poleRow = elevation >= 0 ? 0 : 4;
            for (int i = 0; i < weights.Length; i++)
                weights[i] = (1 - pole) * weights[i] + (X40MotorLayout.Get(i).Row == poleRow ? pole / 8 : 0);
            return true;
        }

        // Largest-remainder quantization keeps sum(motors) <= the selected intensity budget.
        // SDK integers impose 1% steps; continuous math cannot remove hardware quantization.
        public static bool TryQuantize(float[] weights, float intensity, int[] motors)
        {
            if (motors == null || motors.Length != 40) return false;
            Array.Clear(motors, 0, motors.Length);
            if (weights == null || weights.Length != 40 || !HapticMath.Finite(intensity) || intensity < 0 || intensity > 1) return false;
            float sum = 0;
            for (int i = 0; i < 40; i++)
            { if (!HapticMath.Finite(weights[i]) || weights[i] < 0) return false; sum += weights[i]; }
            if (!HapticMath.Finite(sum) || sum <= 0) return false;
            int budget = Mathf.FloorToInt(intensity * 100), used = 0;
            for (int i = 0; i < 40; i++) used += motors[i] = Mathf.FloorToInt(weights[i] / sum * budget);
            while (used < budget)
            {
                int best = 0; float remainder = float.NegativeInfinity;
                for (int i = 0; i < 40; i++)
                { float r = weights[i] / sum * budget - motors[i]; if (r > remainder) { remainder = r; best = i; } }
                motors[best]++; used++;
            }
            return true;
        }
    }
}
