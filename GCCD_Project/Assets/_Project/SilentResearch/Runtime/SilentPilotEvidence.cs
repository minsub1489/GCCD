using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GCCD.SilentResearch
{
    public enum PilotKind { Calibration, TypeIdentification, DirectionIdentification }
    [Serializable] public sealed class PilotEvidence
    {
        public string Version = "1", ParticipantID, DeviceKey, PatternHash, OutputMode, CreatedUtc;
        public PilotKind Kind;
        public int Seed;
        public bool Complete;
        public TrialRecord[] Trials;
    }
    [Serializable] public sealed class PilotSummary
    {
        public int Expected, Completed, Valid, NoResponse;
        public double TypeAccuracyPct, DirectionAccuracyPct;
        // Truth rows, response columns; final column means missing response.
        public int[] TypeConfusion = new int[12], DirectionConfusion = new int[20];
        public double[] TypeClassAccuracy = new double[3], DirectionClassAccuracy = new double[4];
        public bool Passed;
        public string Decision;
    }
    public static class SilentPilotEvidence
    {
        public const int IdentificationTrials = 48;
        public const string LayoutVersion = "X40-wearer-40-v1";
        public static string Hash(string text)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        public static string Fingerprint(SilentHapticPatterns patterns) => Hash(LayoutVersion + JsonUtility.ToJson(patterns));
        public static int[] Channels(int seed)
        {
            var result = Enumerable.Range(0, 40).ToArray(); var random = new System.Random(seed);
            for (int i = 39; i > 0; i--) { int j = random.Next(i + 1); int old = result[i]; result[i] = result[j]; result[j] = old; }
            return result;
        }
        public static SilentTrial[] IdentificationPlan(int seed) => SilentTrialPlan.Create(seed, 4, 1, 2).Where(t => t.Threat).ToArray();
        public static bool Matches(PilotEvidence evidence, PilotKind kind, string device, string pattern, string participant = null)
            => evidence != null && evidence.Version == "1" && evidence.Kind == kind && evidence.DeviceKey == device
                && !string.IsNullOrEmpty(device) && evidence.PatternHash == pattern
                && (participant == null || participant == evidence.ParticipantID) && Evaluate(evidence).Passed;
        public static PilotSummary Evaluate(PilotEvidence evidence)
        {
            var summary = new PilotSummary { Expected = evidence.Kind == PilotKind.Calibration ? 40 : IdentificationTrials };
            var rows = evidence.Trials ?? Array.Empty<TrialRecord>();
            summary.Completed = rows.Count(r => r.Status == "Completed");
            summary.Valid = rows.Count(r => r.Valid == "true" && r.Status == "Completed");
            bool physical = evidence.OutputMode == OutputMode.BHaptics.ToString() && !string.IsNullOrEmpty(evidence.DeviceKey);
            bool coverage = evidence.Complete && rows.Length == summary.Expected && rows.Select(r => r.TrialID).Distinct().Count() == summary.Expected;
            if (evidence.Kind == PilotKind.Calibration)
            {
                int[] expected = Channels(evidence.Seed);
                bool correct = coverage && rows.Select((r, i) => r.MotorIndex == expected[i].ToString(CultureInfo.InvariantCulture)
                    && r.PerceivedMotor == r.MotorIndex && r.DeliveryAccepted == "true" && r.Comfortable == "true"
                    && r.Valid == "true" && r.Status == "Completed").All(v => v);
                summary.NoResponse = rows.Count(r => r.PerceivedMotor == "");
                summary.Passed = physical && correct;
                summary.Decision = summary.Passed ? "40/40 locations felt correctly and confirmed comfortable; physical calibration passed"
                    : "Calibration pending / failed: requires all 40 physical locations, matching responses and comfort confirmation";
                return summary;
            }
            var plan = IdentificationPlan(evidence.Seed);
            int typeCorrect = 0, directionCorrect = 0;
            var typeCounts = new int[3]; var directionCounts = new int[4]; var typeHits = new int[3]; var directionHits = new int[4];
            bool planMatches = coverage;
            for (int i = 0; i < rows.Length && i < plan.Length; i++)
            {
                var r = rows[i]; var t = plan[i]; int type = (int)t.Type, direction = t.DirectionIndex;
                planMatches &= r.EventType == t.Type.ToString() && r.EventDirection == (direction * 90).ToString(CultureInfo.InvariantCulture);
                int typeResponse = Enum.TryParse(r.TypeResponse, out EventType parsed) && (int)parsed >= 0 && (int)parsed < 3 ? (int)parsed : 3;
                int directionResponse = int.TryParse(r.DirectionResponse, out int bearing) && bearing >= 0 && bearing <= 270 && bearing % 90 == 0 ? bearing / 90 : 4;
                summary.TypeConfusion[type * 4 + typeResponse]++; summary.DirectionConfusion[direction * 5 + directionResponse]++;
                typeCounts[type]++; directionCounts[direction]++;
                if (typeResponse == type) { typeCorrect++; typeHits[type]++; }
                if (directionResponse == direction) { directionCorrect++; directionHits[direction]++; }
                if (typeResponse == 3 || evidence.Kind == PilotKind.DirectionIdentification && directionResponse == 4) summary.NoResponse++;
            }
            summary.TypeAccuracyPct = 100.0 * typeCorrect / summary.Expected;
            summary.DirectionAccuracyPct = 100.0 * directionCorrect / summary.Expected;
            for (int i = 0; i < 3; i++) summary.TypeClassAccuracy[i] = typeCounts[i] == 0 ? 0 : 100.0 * typeHits[i] / typeCounts[i];
            for (int i = 0; i < 4; i++) summary.DirectionClassAccuracy[i] = directionCounts[i] == 0 ? 0 : 100.0 * directionHits[i] / directionCounts[i];
            summary.Passed = physical && planMatches && summary.Valid == summary.Expected && rows.All(r => r.DeliveryAccepted == "true" && r.Comfortable == "true")
                && summary.TypeClassAccuracy.All(a => a >= 80) && (evidence.Kind != PilotKind.DirectionIdentification || summary.DirectionClassAccuracy.All(a => a >= 80));
            summary.Decision = summary.Passed ? "Identification passed: every type / required direction >=80% with full physical trial coverage"
                : "Pending / retraining needed: each class must reach 80%, all 48 trials must be valid physical trials; omissions count as errors";
            return summary;
        }
    }
}
