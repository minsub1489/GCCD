using UnityEngine;

namespace GCCD.ContinuousHaptics
{
    public struct HapticDirection
    {
        public float Azimuth, Elevation, Distance;
        public HapticDirection(float azimuth, float elevation, float distance)
        { Azimuth = azimuth; Elevation = elevation; Distance = distance; }
        public bool IsValid => HapticMath.Finite(Azimuth) && HapticMath.Finite(Elevation)
            && HapticMath.Finite(Distance) && Distance >= 0 && Elevation >= -90 && Elevation <= 90;
    }

    public static class HapticMath
    {
        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        public static bool ValidCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return false;
            foreach (var k in curve.keys)
                // Infinite tangents are Unity's legitimate stepped-curve representation.
                if (!Finite(k.time) || !Finite(k.value) || float.IsNaN(k.inTangent)
                    || float.IsNaN(k.outTangent) || !Finite(k.inWeight) || !Finite(k.outWeight)) return false;
            return true;
        }
        public static float Alpha(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);
    }

    public static class HapticDirectionCalculator
    {
        public static bool TryCalculate(Transform player, Vector3 targetWorld, float previousAzimuth,
            out HapticDirection result)
        {
            result = default;
            if (!player || !HapticMath.Finite(targetWorld) || !HapticMath.Finite(player.position)) return false;
            // Direction uses rotation only; non-uniform player scale must not change world distance.
            return TryFromLocal(player.InverseTransformDirection(targetWorld - player.position),
                previousAzimuth, out result);
        }
        public static bool TryFromLocal(Vector3 relative, float previousAzimuth, out HapticDirection result)
        {
            result = default;
            if (!HapticMath.Finite(relative) || !HapticMath.Finite(previousAzimuth)) return false;
            float distance = relative.magnitude;
            if (!HapticMath.Finite(distance) || distance < 0.00001f) return false; // coincident: undefined direction
            Vector3 unit = relative / distance;
            float horizontal = new Vector2(unit.x, unit.z).magnitude;
            float azimuth = horizontal < 0.00001f ? previousAzimuth : Mathf.Atan2(unit.x, unit.z) * Mathf.Rad2Deg;
            result = new HapticDirection(Mathf.Repeat(azimuth, 360),
                Mathf.Atan2(unit.y, horizontal) * Mathf.Rad2Deg, distance);
            return result.IsValid;
        }
        public static Vector3 ToLocal(float azimuth, float elevation, float distance)
        {
            float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)) * distance;
        }
    }
}
