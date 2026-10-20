using System;
using System.Collections.Generic;

namespace GCCD.Research
{
    // Pure C# math; no Unity or SDK dependency. Positive azimuth is viewer-right:
    // 0° = forward (+Z), +90° = right (+X), ±180° = behind.
    [Serializable]
    public sealed class SpatialMotor
    {
        public int index;
        public float azimuthDegrees;
        public float gain = 1f;
        public float low = .20f;
        public float medium = .45f;
        public float high = .70f;
        public bool placementConfirmed;
        public bool strengthConfirmed;
    }

    [Serializable]
    public sealed class SpatialMotorProfile
    {
        public string profileId = "synthetic_40_equal_angles_unverified";
        public string provenance = "DEMO ONLY: 40 evenly spaced virtual angles, not the X40 manufacturer's motor indices";
        public bool physicalLayoutVerified;
        public SpatialMotor[] motors;

        public static SpatialMotorProfile CreateVirtualPreview()
        {
            var profile = new SpatialMotorProfile { motors = new SpatialMotor[40] };
            for (int i = 0; i < 40; ++i)
                profile.motors[i] = new SpatialMotor
                {
                    index = i,
                    azimuthDegrees = i * 9f,
                    placementConfirmed = false,
                    strengthConfirmed = false
                };
            return profile;
        }

        public bool Validate(out string error, bool requirePhysicalVerification = false)
        {
            if (motors == null || motors.Length != 40)
            {
                error = "Profile must explicitly enumerate exactly 40 motor indices.";
                return false;
            }
            var uniqueIndices = new HashSet<int>();
            for (int i = 0; i < motors.Length; ++i)
            {
                SpatialMotor m = motors[i];
                if (m == null || m.index < 0 || m.index >= 40 || !uniqueIndices.Add(m.index))
                {
                    error = "Null, duplicated, or out-of-range motor index at element " + i;
                    return false;
                }
                if (!SpatialMotorMath.Finite(m.azimuthDegrees) ||
                    !SpatialMotorMath.Finite(m.gain) || m.gain < 0 || m.gain > 1 ||
                    !SpatialMotorMath.Finite(m.low) || !SpatialMotorMath.Finite(m.medium) ||
                    !SpatialMotorMath.Finite(m.high) ||
                    m.low <= 0 || m.low >= m.medium || m.medium >= m.high || m.high > 1)
                {
                    error = "Invalid motor angle/gain/ordered comfortable levels at index " + m.index;
                    return false;
                }
                if (requirePhysicalVerification && (!m.placementConfirmed || !m.strengthConfirmed))
                {
                    error = "Unverified physical placement/intensity at motor " + m.index;
                    return false;
                }
            }
            if (requirePhysicalVerification && (!physicalLayoutVerified ||
                string.IsNullOrWhiteSpace(profileId) ||
                profileId.StartsWith("synthetic_", StringComparison.OrdinalIgnoreCase)))
            {
                error = "A separately tested, named physical X40 calibration profile is required.";
                return false;
            }
            error = "";
            return true;
        }
    }

    public struct SpatialEmitter
    {
        public float worldX;
        public float worldZ;
        public float gain;
        public bool active;
        public string id;
    }

    public static class SpatialMotorMath
    {
        private const double Radians = Math.PI / 180.0;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float Wrap180(float degrees)
        {
            if (!Finite(degrees)) return 0;
            float wrapped = (degrees + 180f) % 360f;
            if (wrapped < 0) wrapped += 360f;
            return wrapped - 180f;
        }

        public static float AngularDistance(float a, float b) => Math.Abs(Wrap180(a - b));

        // SDK2 PlayParam angleX is counterclockwise. Our azimuth is viewer-right
        // positive (clockwise). This conversion remains subject to on-body
        // verification of each authored event's native orientation.
        public static float ToSdk2CounterclockwiseAngle(float viewerRightAzimuth)
        {
            float ccw = (-Wrap180(viewerRightAzimuth)) % 360f;
            if (ccw < 0f) ccw += 360f;
            return ccw;
        }

        public static float ListenerRelativeAzimuth(
            float listenerX, float listenerZ, float listenerYawDegrees, float sourceX, float sourceZ)
        {
            float dx = sourceX - listenerX;
            float dz = sourceZ - listenerZ;
            if (!Finite(dx) || !Finite(dz) || (dx == 0f && dz == 0f)) return 0f;
            float absolute = (float)(Math.Atan2(dx, dz) / Radians);
            return Wrap180(absolute - listenerYawDegrees);
        }

        private static float Intensity(SpatialMotor motor, string level)
        {
            switch (level)
            {
                case "low": return motor.low;
                case "medium": return motor.medium;
                case "high": return motor.high;
                default: return 0f;
            }
        }

        // Smooth circular Gaussian contributions, saturating accumulation:
        // 1−∏(1−individual) so overlapping simultaneous threats never overflow.
        // maxMotorOutput bounds all resulting 40 channel amplitudes.
        public static int Mix(
            SpatialMotorProfile profile, IList<SpatialEmitter> emitters, string intensity,
            float listenerX, float listenerZ, float listenerYawDegrees,
            float spreadDegrees, float maxMotorOutput, float[] destination)
        {
            if (profile == null || profile.motors == null || profile.motors.Length != 40 ||
                destination == null || destination.Length != 40)
                throw new ArgumentException("A 40-entry profile and a 40-channel output are required.");
            Array.Clear(destination, 0, 40);
            if (intensity == "none" || emitters == null || emitters.Count == 0) return 0;
            if (!Finite(spreadDegrees) || spreadDegrees <= 0f ||
                !Finite(maxMotorOutput) || maxMotorOutput < 0f || maxMotorOutput > 1f)
                throw new ArgumentOutOfRangeException("spread / maxMotorOutput");
            int contributing = 0;
            for (int j = 0; j < emitters.Count; ++j)
            {
                SpatialEmitter emitter = emitters[j];
                if (!emitter.active || !Finite(emitter.gain) || emitter.gain <= 0f ||
                    !Finite(emitter.worldX) || !Finite(emitter.worldZ)) continue;
                float azimuth = ListenerRelativeAzimuth(listenerX, listenerZ, listenerYawDegrees,
                    emitter.worldX, emitter.worldZ);
                ++contributing;
                for (int k = 0; k < 40; ++k)
                {
                    SpatialMotor motor = profile.motors[k];
                    float radians = AngularDistance(azimuth, motor.azimuthDegrees) / spreadDegrees;
                    float spatialWeight = (float)Math.Exp(-.5 * radians * radians);
                    float value = Clamp01(emitter.gain) * motor.gain * Intensity(motor, intensity) * spatialWeight;
                    value = Clamp01(value);
                    // Indices can be stored in an arbitrary calibration order.
                    int channel = motor.index;
                    destination[channel] = 1f - (1f - destination[channel]) * (1f - value);
                }
            }
            for (int k = 0; k < 40; ++k)
                destination[k] = Math.Min(maxMotorOutput, Clamp01(destination[k]));
            return contributing;
        }

        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    public sealed class SpatialMotorSmoother
    {
        private readonly float[] current = new float[40];
        public float[] Current => current;

        public void Step(float[] target, float deltaSeconds, float attackSeconds, float releaseSeconds)
        {
            if (target == null || target.Length != 40) throw new ArgumentException("Expected 40-channel target.");
            float dt = Math.Max(0, deltaSeconds);
            for (int i = 0; i < 40; i++)
            {
                // Exact zeros have priority so neutralized sources never leave a residual trail.
                if (target[i] <= 0f) { current[i] = 0f; continue; }
                float tau = target[i] > current[i] ? attackSeconds : releaseSeconds;
                float alpha = tau <= 0 ? 1f : 1f - (float)Math.Exp(-dt / tau);
                current[i] = SpatialMotorMath.Clamp01(current[i] + (target[i] - current[i]) * alpha);
            }
        }

        public void Reset() => Array.Clear(current, 0, current.Length);
    }
}
