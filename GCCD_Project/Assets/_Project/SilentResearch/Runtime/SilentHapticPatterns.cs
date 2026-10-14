using System;
using GCCD.ContinuousHaptics;
using UnityEngine;

namespace GCCD.SilentResearch
{
    [Serializable]
    public struct HapticPulse
    {
        public int StartMs, DurationMs;
        [Range(1, 100)] public int Intensity;
        public HapticPulse(int start, int duration, int intensity) { StartMs = start; DurationMs = duration; Intensity = intensity; }
    }

    [CreateAssetMenu(menuName = "GCCD/Silent Study/Haptic Patterns")]
    public sealed class SilentHapticPatterns : ScriptableObject
    {
        public string Version = "temporal-v1";
        // Equal default active time (200ms), active motor count (8), and per-motor intensity.
        // Equal commanded energy is NOT evidence of equal perceived intensity or discriminability.
        public HapticPulse[] Basic = { new HapticPulse(0, 200, 35) };
        public HapticPulse[] Footsteps = { new HapticPulse(0, 100, 35), new HapticPulse(200, 100, 35) };
        public HapticPulse[] Door = { new HapticPulse(0, 200, 35) };
        public HapticPulse[] Growl = { new HapticPulse(0, 50, 35), new HapticPulse(100, 50, 35),
            new HapticPulse(200, 50, 35), new HapticPulse(300, 50, 35) };

        public HapticPulse[] Select(FeedbackCondition condition, EventType type)
        {
            if (condition == FeedbackCondition.AudioOnly || type == EventType.None) return Array.Empty<HapticPulse>();
            if (condition == FeedbackCondition.BasicHaptic) return Basic;
            switch (type) { case EventType.Footsteps: return Footsteps; case EventType.Door: return Door; default: return Growl; }
        }
        public bool Valid => !string.IsNullOrWhiteSpace(Version) && ValidPulses(Basic) && ValidPulses(Footsteps)
            && ValidPulses(Door) && ValidPulses(Growl);
        static bool ValidPulses(HapticPulse[] pulses)
        {
            if (pulses == null || pulses.Length == 0 || pulses.Length > 32) return false;
            int end = 0;
            foreach (var p in pulses)
            {
                if (p.StartMs < end || p.DurationMs < 20 || p.DurationMs > 250 || p.Intensity < 1 || p.Intensity > 100) return false;
                end = p.StartMs + p.DurationMs;
                if (end > 3000) return false;
            }
            return true;
        }

        public static void Map(int intensity, bool directional, float azimuth, int[] motors)
        {
            if (motors == null || motors.Length != X40MotorLayout.Count) throw new ArgumentException("X40 requires 40 channels");
            Array.Clear(motors, 0, motors.Length);
            float sectorAngle = SilentTrialPlan.Sector(azimuth) * 90;
            for (int i = 0; i < motors.Length; i++)
            {
                var motor = X40MotorLayout.Get(i);
                bool selected = directional ? motor.Row < 4 && SilentTrialPlan.CircularError(motor.Azimuth, sectorAngle) < 45
                    : motor.Row == 2;
                if (selected) motors[i] = Mathf.Clamp(intensity, 0, 100);
            }
        }
    }
}
