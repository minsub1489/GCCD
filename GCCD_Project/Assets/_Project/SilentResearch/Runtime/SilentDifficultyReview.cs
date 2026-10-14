using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GCCD.SilentResearch
{
    [Serializable] public sealed class DifficultyReview
    {
        public string Purpose = "DifficultyPilot", Decision, Scenario, Condition;
        public int Expected, ValidTrials, AbortedTrials, MissingDefenseResponses;
        public string ObjectiveRatePct, AvoidanceRatePct, DetectionAccuracyPct;
        public bool Complete;
    }
    public static class SilentDifficultyReview
    {
        // Provisional pilot guardrails, declared before collection; never adapt a measurement block.
        public static DifficultyReview Evaluate(IEnumerable<TrialRecord> records, int expected)
        {
            var rows = records.Where(r => r.Purpose == "DifficultyPilot" && r.RowKind == "final")
                .GroupBy(r => r.SessionID + "/" + r.TrialID).Select(g => g.Last()).ToArray();
            var metrics = SilentMetrics.Calculate(rows, "DifficultyPilot");
            var result = new DifficultyReview { Expected = expected, ValidTrials = metrics.ValidTrials,
                AbortedTrials = rows.Count(r => r.Status == "Aborted"), Complete = expected >= 15 && metrics.ValidTrials == expected,
                MissingDefenseResponses = rows.Count(r => r.Valid == "true" && r.IsThreat == "true" && r.ReactionTimeMs == ""),
                Scenario = rows.FirstOrDefault()?.ScenarioID ?? "", Condition = rows.FirstOrDefault()?.Condition ?? "",
                ObjectiveRatePct = metrics.ObjectiveRatePct, AvoidanceRatePct = metrics.AvoidanceRatePct, DetectionAccuracyPct = metrics.DetectionAccuracyPct };
            if (!result.Complete) result.Decision = "Insufficient valid trials; finish the whole pilot block";
            else
            {
                double rate = double.Parse(metrics.ObjectiveRatePct, CultureInfo.InvariantCulture);
                result.Decision = rate >= 90 ? "Possible ceiling: test the harder preset in a new pilot block"
                    : rate <= 20 ? "Possible floor: test the easier preset in a new pilot block"
                    : "Provisional usable range (20–90% objective success); compare all A–D blocks and participants before freezing the scenario";
            }
            return result;
        }
    }
}
