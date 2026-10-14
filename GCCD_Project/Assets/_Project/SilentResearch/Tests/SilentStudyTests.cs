using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GCCD.ContinuousHaptics;
using NUnit.Framework;
using UnityEngine;

namespace GCCD.SilentResearch.Tests
{
    public sealed class SilentStudyTests
    {
        [TestCase(1489)] [TestCase(-12)] [TestCase(0)]
        public void PlansAreReproducibleBalancedAndHaveExplicitCatches(int seed)
        {
            var a = SilentTrialPlan.Create(seed, 2, 1, 3); var b = SilentTrialPlan.Create(seed, 2, 1, 3);
            CollectionAssert.AreEqual(a, b); Assert.AreEqual(30, a.Count); Assert.AreEqual(6, a.Count(t => !t.Threat));
            foreach (var group in a.Where(t => t.Threat).GroupBy(t => t.Type + "/" + t.DirectionIndex)) Assert.AreEqual(2, group.Count());
            Assert.AreEqual(12, a.Where(t => t.Threat).Select(t => t.Type + "/" + t.DirectionIndex).Distinct().Count());
            Assert.That(a.All(t => t.Foreperiod >= 1 && t.Foreperiod <= 3));
        }
        [Test] public void WilliamsOrdersBalancePositionAndDirectedCarryover()
        {
            var orders = Enumerable.Range(0, 4).Select(SilentTrialPlan.ConditionOrder).ToArray();
            for (int i = 0; i < 4; i++) Assert.AreEqual(4, orders.Select(o => o[i]).Distinct().Count());
            var pairs = new HashSet<string>();
            foreach (var order in orders) for (int i = 1; i < 4; i++) pairs.Add(order[i - 1] + "/" + order[i]);
            Assert.AreEqual(12, pairs.Count);
        }
        [Test] public void PracticeUsesSeparateRecordedSeed()
        {
            Assert.AreEqual(1489, SilentTrialPlan.EffectiveSeed(1489, false));
            Assert.AreNotEqual(1489, SilentTrialPlan.EffectiveSeed(1489, true));
        }
        [TestCase(359, 1, 2)] [TestCase(5, 355, 10)] [TestCase(90, 270, 180)]
        public void DirectionErrorWraps(float actual, float response, float expected)
        { Assert.AreEqual(expected, SilentTrialPlan.CircularError(actual, response)); }
        [TestCase(0, 0)] [TestCase(44.9f, 0)] [TestCase(45, 1)] [TestCase(315, 0)] [TestCase(-90, 3)]
        public void SectorsWrapCorrectly(float angle, int sector) { Assert.AreEqual(sector, SilentTrialPlan.Sector(angle)); }
        [Test] public void X40SectorsAreDisjointAndEqualToNeutralCommandBudget()
        {
            var masks = new List<int[]>();
            for (int i = 0; i < 4; i++)
            {
                var motors = new int[40]; SilentHapticPatterns.Map(35, true, i * 90, motors); masks.Add(motors);
                Assert.AreEqual(8, motors.Count(x => x > 0)); Assert.AreEqual(280, motors.Sum());
                foreach (int index in Enumerable.Range(0, 40).Where(k => motors[k] > 0))
                    Assert.Less(SilentTrialPlan.CircularError(X40MotorLayout.Get(index).Azimuth, i * 90), 45);
            }
            for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++)
                Assert.IsFalse(Enumerable.Range(0, 40).Any(i => masks[a][i] > 0 && masks[b][i] > 0));
            var neutral = new int[40]; SilentHapticPatterns.Map(35, false, 19, neutral);
            Assert.AreEqual(280, neutral.Sum()); Assert.AreEqual(8, neutral.Count(x => x > 0));
        }
        [Test] public void BasicDoesNotEncodeTypeAndTypesUseEqualDefaultOnTime()
        {
            var patterns = ScriptableObject.CreateInstance<SilentHapticPatterns>();
            try
            {
                Assert.IsTrue(patterns.Valid);
                Assert.AreSame(patterns.Select(FeedbackCondition.BasicHaptic, EventType.Footsteps), patterns.Select(FeedbackCondition.BasicHaptic, EventType.Growl));
                foreach (var type in new[] { EventType.Footsteps, EventType.Door, EventType.Growl })
                {
                    var c = patterns.Select(FeedbackCondition.TypeHaptic, type); var d = patterns.Select(FeedbackCondition.TypeDirectionHaptic, type);
                    CollectionAssert.AreEqual(c, d); Assert.AreEqual(200, c.Sum(p => p.DurationMs));
                }
                Assert.IsEmpty(patterns.Select(FeedbackCondition.AudioOnly, EventType.Growl));
                patterns.Door[0].DurationMs = 500; Assert.IsFalse(patterns.Valid);
            }
            finally { UnityEngine.Object.DestroyImmediate(patterns); }
        }
        [Test] public void MetricsIncludeMissesWrongAnswersAndResponseCoverage()
        {
            var rows = new[] {
                Row("1", true, "true", "true", "true", "true", "500", "2", "10"),
                Row("2", true, "false", "false", "false", "false", "", "", ""),
                Row("3", false, "false", "", "", "false", "", "", ""),
                Row("4", false, "true", "", "", "false", "", "", "") };
            rows[0].TypeResponse = "Door"; rows[0].DirectionResponse = "1";
            rows[2].DetectionResponse = "Threat"; rows[3].DetectionResponse = "Safe";
            var m = SilentMetrics.Calculate(rows.Concat(new[] { rows[0] }));
            Assert.AreEqual(4, m.ValidTrials); Assert.AreEqual("50", m.DetectionAccuracyPct); Assert.AreEqual("50", m.TypeAccuracyPct);
            Assert.AreEqual("50", m.FalseAlarmRatePct); Assert.AreEqual("500", m.MeanCorrectDefenseTimeMs);
            Assert.AreEqual(1, m.CorrectDefenseResponses); Assert.AreEqual(1, m.DirectionResponses); Assert.AreEqual("2", m.MeanDirectionErrorDeg);
            rows[0].Practice = "true"; rows[1].Valid = "false";
            Assert.AreEqual(2, SilentMetrics.Calculate(rows).ValidTrials);
        }
        static TrialRecord Row(string id, bool threat, string detected, string classified, string avoided, string objective, string reaction, string error, string completed)
            => new TrialRecord { SessionID = "test", TrialID = id, RowKind = "final", Practice = "false", Valid = "true", IsThreat = TrialRecord.Bool(threat),
                DetectionSuccess = detected, IsCorrect = classified, AvoidanceSuccess = avoided, ObjectiveSuccess = objective,
                ReactionTimeMs = reaction, DirectionErrorDeg = error, CompletionTimeSec = completed };
        [Test] public void JournalAndCsvAreDurableUniqueAndKeepMissingValuesBlank()
        {
            string root = Path.Combine(Path.GetTempPath(), "GCCD-Recorder-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var recorder = new SilentDataRecorder(root, "session", "{}"))
                {
                    var row = new TrialRecord { TrialID = "1", ParticipantID = "P001", AbortReason = "reason,with\"quotes" };
                    recorder.Checkpoint(row); Assert.IsTrue(recorder.Complete(row)); Assert.IsFalse(recorder.Complete(row));
                    Assert.AreEqual(2, File.ReadAllLines(recorder.CsvPath).Length);
                    string csv = File.ReadAllText(recorder.CsvPath); StringAssert.Contains("\"reason,with\"\"quotes\"", csv);
                    Assert.AreEqual(2, File.ReadAllLines(Path.Combine(recorder.DirectoryPath, "journal.jsonl")).Length);
                    var recovered = JsonUtility.FromJson<TrialRecord>(File.ReadAllLines(Path.Combine(recorder.DirectoryPath, "journal.jsonl"))[1]);
                    Assert.AreEqual("", recovered.ReactionTimeMs); Assert.AreEqual("final", recovered.RowKind);
                }
                Assert.Throws<IOException>(() => new SilentDataRecorder(root, "session", "{}"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [Test] public void BearingUsesCameraYawAndIgnoresPitch()
        {
            var view = new GameObject("view");
            try
            {
                view.transform.rotation = Quaternion.Euler(80, 90, 0);
                Assert.IsTrue(SilentTrialPlan.TryBearing(view.transform, new Vector3(4, 0, 0), out var d));
                Assert.That(d.Azimuth, Is.EqualTo(0).Within(.01f));
            }
            finally { UnityEngine.Object.DestroyImmediate(view); }
        }
    }
}
