using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace GCCD.SilentResearch
{
    // The four feedback conditions in the horror study.
    public enum FeedbackCondition { AudioOnly, BasicHaptic, TypeHaptic, TypeDirectionHaptic }
    public enum EventType { Footsteps, Door, Growl, None }
    public enum OutputMode { Simulation, BHaptics }
    public enum TrialOutcome { Pending, Success, Caught, Timeout, Aborted }

    public static class StudyClock
    {
        public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        public static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }

    [Serializable]
    public struct SoundEvent
    {
        public EventType Type;
        public bool IsThreat;
        public Vector3 Position;
        public double Onset;
    }

    [Serializable]
    public struct SilentTrial
    {
        public EventType Type;
        public int DirectionIndex;
        public bool Threat;
        public float Foreperiod;
    }

    public static class SilentTrialPlan
    {
        public static int EffectiveSeed(int inputSeed, bool practice) => practice ? inputSeed ^ 0x5A17C0DE : inputSeed;
        // Each repetition contains every type x cardinal direction and three explicit silent catches.
        public static List<SilentTrial> Create(int seed, int repetitions, float minimum, float maximum)
        {
            if (repetitions < 1 || repetitions > 8 || !StudyClock.Finite(minimum) ||
                !StudyClock.Finite(maximum) || minimum < .1f || maximum < minimum || maximum > 10)
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            var rng = new System.Random(seed);
            var trials = new List<SilentTrial>();
            for (int r = 0; r < repetitions; r++)
            {
                for (int type = 0; type < 3; type++)
                    for (int direction = 0; direction < 4; direction++)
                        trials.Add(new SilentTrial { Type = (EventType)type, DirectionIndex = direction, Threat = true });
                for (int i = 0; i < 3; i++) trials.Add(new SilentTrial { Type = EventType.None, Threat = false, DirectionIndex = -1 });
            }
            for (int i = trials.Count - 1; i > 0; i--)
            { int j = rng.Next(i + 1); var t = trials[i]; trials[i] = trials[j]; trials[j] = t; }
            for (int i = 0; i < trials.Count; i++)
            { var t = trials[i]; t.Foreperiod = minimum + (float)rng.NextDouble() * (maximum - minimum); trials[i] = t; }
            return trials;
        }

        public static FeedbackCondition[] ConditionOrder(int participantIndex)
        {
            int[,] square = { { 0, 1, 3, 2 }, { 1, 2, 0, 3 }, { 2, 3, 1, 0 }, { 3, 0, 2, 1 } };
            int row = (participantIndex % 4 + 4) % 4;
            var result = new FeedbackCondition[4];
            for (int i = 0; i < result.Length; i++) result[i] = (FeedbackCondition)square[row, i];
            return result;
        }
        public static int Sector(float azimuth, int sectors = 4)
        {
            if (sectors < 4 || sectors > 8 || !StudyClock.Finite(azimuth)) throw new ArgumentOutOfRangeException();
            return Mathf.FloorToInt((Mathf.Repeat(azimuth, 360) + 180f / sectors) / (360f / sectors)) % sectors;
        }
        public static float CircularError(float actual, float response) => Mathf.Abs(Mathf.DeltaAngle(actual, response));
        public static bool TryBearing(Transform view, Vector3 position, out GCCD.ContinuousHaptics.HapticDirection result)
        {
            // Four horizontal directions follow current camera yaw; looking up/down must not flip a sector.
            result = default;
            if (!view) return false;
            return GCCD.ContinuousHaptics.HapticDirectionCalculator.TryFromLocal(
                Quaternion.Inverse(Quaternion.Euler(0, view.eulerAngles.y, 0)) * (position - view.position), 0, out result);
        }
    }

    [CreateAssetMenu(menuName = "GCCD/Silent Study/Scenario")]
    public sealed class SilentScenario : ScriptableObject
    {
        public string ScenarioID = "Indoor-01-v1";
        [Range(1, 8)] public int Repetitions = 1;
        public float MinimumForeperiod = 1.5f, MaximumForeperiod = 3;
        public float ResponseWindowSec = 3, PursuitWindowSec = 5, TrialLimitSec = 20;
        public float PlayerSpeed = 3, MonsterPatrolSpeed = 1.2f, MonsterChaseSpeed = 2.3f;
        public float MonsterDetectionRange = 12, CatchRadius = .65f;
        public Vector3 PlayerStart = new Vector3(0, 1, 0), Goal = new Vector3(0, 1, 7);
        public Vector3[] EventAnchors = { new Vector3(0, 1.4f, 4), new Vector3(4, 1.4f, 0),
            new Vector3(0, 1.4f, -4), new Vector3(-4, 1.4f, 0) };
        public bool Valid => !string.IsNullOrWhiteSpace(ScenarioID) && Repetitions >= 1 && Repetitions <= 8
            && StudyClock.Finite(MinimumForeperiod) && MinimumForeperiod >= .1f
            && StudyClock.Finite(MaximumForeperiod) && MaximumForeperiod >= MinimumForeperiod && MaximumForeperiod <= 10
            && StudyClock.Finite(ResponseWindowSec) && ResponseWindowSec >= .5f && ResponseWindowSec <= 10
            && MinimumForeperiod + ResponseWindowSec > MaximumForeperiod + .2f
            && StudyClock.Finite(PursuitWindowSec) && PursuitWindowSec >= 1 && PursuitWindowSec <= 20
            && StudyClock.Finite(TrialLimitSec) && TrialLimitSec > MaximumForeperiod + ResponseWindowSec + PursuitWindowSec
            && TrialLimitSec <= 120 && Positive(PlayerSpeed) && Positive(MonsterPatrolSpeed)
            && Positive(MonsterChaseSpeed) && Positive(MonsterDetectionRange) && Positive(CatchRadius)
            && GCCD.ContinuousHaptics.HapticMath.Finite(PlayerStart) && GCCD.ContinuousHaptics.HapticMath.Finite(Goal)
            && EventAnchors != null && EventAnchors.Length == 4 && Array.TrueForAll(EventAnchors,
                p => GCCD.ContinuousHaptics.HapticMath.Finite(p) && Vector3.Distance(p, PlayerStart) > 1);
        static bool Positive(float v) => StudyClock.Finite(v) && v > 0 && v <= 30;
    }
}
