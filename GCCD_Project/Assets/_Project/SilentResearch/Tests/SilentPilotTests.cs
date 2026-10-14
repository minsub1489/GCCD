using System;
using System.Linq;
using NUnit.Framework;

namespace GCCD.SilentResearch.Tests
{
    public sealed class SilentPilotTests
    {
        static PilotEvidence Calibration()
        {
            var e = new PilotEvidence { Kind = PilotKind.Calibration, Seed = 42, Complete = true, DeviceKey = "fixture-device",
                PatternHash = "fixture-pattern", OutputMode = "BHaptics", ParticipantID = "FIXTURE" };
            e.Trials = SilentPilotEvidence.Channels(e.Seed).Select((channel, i) => new TrialRecord { TrialID = (i + 1).ToString(),
                Status = "Completed", Valid = "true", MotorIndex = channel.ToString(), PerceivedMotor = channel.ToString(), DeliveryAccepted = "true", Comfortable = "true" }).ToArray();
            return e;
        }
        static PilotEvidence Identification(PilotKind kind)
        {
            var e = Calibration(); e.Kind = kind;
            e.Trials = SilentPilotEvidence.IdentificationPlan(e.Seed).Select((t, i) => new TrialRecord { TrialID = (i + 1).ToString(),
                Status = "Completed", Valid = "true", EventType = t.Type.ToString(), TypeResponse = t.Type.ToString(), EventDirection = (t.DirectionIndex * 90).ToString(),
                DirectionResponse = (t.DirectionIndex * 90).ToString(), DeliveryAccepted = "true", Comfortable = "true" }).ToArray();
            return e;
        }
        [Test] public void CalibrationRequiresEveryPhysicalLocationAndComfort()
        {
            var e = Calibration(); Assert.IsTrue(SilentPilotEvidence.Evaluate(e).Passed);
            e.Trials[0].PerceivedMotor = "wrong"; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
            e = Calibration(); e.Trials[0].Comfortable = "false"; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
            e = Calibration(); e.Trials = e.Trials.Take(39).ToArray(); Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
            e = Calibration(); e.Trials[0].DeliveryAccepted = "false"; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
            e = Calibration(); e.Trials[0].TrialID = e.Trials[1].TrialID; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
        }
        [Test] public void SimulationAndInterruptedCalibrationNeverBecomeCertificates()
        {
            var e = Calibration(); e.OutputMode = "Simulation"; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
            e = Calibration(); e.Complete = false; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
            e = Calibration(); e.Trials[0].Status = "Aborted"; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
        }
        [Test] public void CertificatesAreBoundToDevicePatternAndParticipant()
        {
            var e = Identification(PilotKind.TypeIdentification);
            Assert.IsTrue(SilentPilotEvidence.Matches(e, e.Kind, e.DeviceKey, e.PatternHash, e.ParticipantID));
            Assert.IsFalse(SilentPilotEvidence.Matches(e, e.Kind, "other-device", e.PatternHash, e.ParticipantID));
            Assert.IsFalse(SilentPilotEvidence.Matches(e, e.Kind, e.DeviceKey, "changed-pattern", e.ParticipantID));
            Assert.IsFalse(SilentPilotEvidence.Matches(e, e.Kind, e.DeviceKey, e.PatternHash, "other-person"));
        }
        [Test] public void IdentificationChecksEveryClassAndIncludesMissingAnswers()
        {
            var e = Identification(PilotKind.DirectionIdentification);
            Assert.IsTrue(SilentPilotEvidence.Evaluate(e).Passed);
            foreach (var r in e.Trials.Where(r => r.EventType == "Footsteps").Take(9)) r.TypeResponse = "";
            var summary = SilentPilotEvidence.Evaluate(e);
            Assert.Greater(summary.TypeAccuracyPct, 80); Assert.IsFalse(summary.Passed);
            Assert.AreEqual(9, summary.NoResponse); Assert.AreEqual(9, summary.TypeConfusion[3]);
            e = Identification(PilotKind.DirectionIdentification);
            foreach (var r in e.Trials.Where(r => r.EventDirection == "0")) r.DirectionResponse = "";
            summary = SilentPilotEvidence.Evaluate(e); Assert.IsFalse(summary.Passed); Assert.AreEqual(12, summary.DirectionConfusion[4]);
        }
        [Test] public void TestTruthMustMatchTheRecordedSeededPlan()
        {
            var e = Identification(PilotKind.TypeIdentification); e.Seed++; Assert.IsFalse(SilentPilotEvidence.Evaluate(e).Passed);
        }
        [Test] public void MainMetricsExcludeBothKindsOfPilots()
        {
            var row = new TrialRecord { Purpose = "DifficultyPilot", RowKind = "final", Valid = "true", Practice = "false", TrialID = "1", IsThreat = "true", ObjectiveSuccess = "true" };
            Assert.AreEqual(0, SilentMetrics.Calculate(new[] { row }).ValidTrials);
            Assert.AreEqual(1, SilentMetrics.Calculate(new[] { row }, "DifficultyPilot").ValidTrials);
            row.Purpose = "HardwarePilot"; Assert.AreEqual(0, SilentMetrics.Calculate(new[] { row }).ValidTrials);
        }
        [TestCase(0, "floor")] [TestCase(15, "ceiling")] [TestCase(8, "usable")]
        public void DifficultyReviewRequiresFullCoverageAndDetectsExtremeRates(int wins, string decision)
        {
            var rows = Enumerable.Range(0, 15).Select(i => new TrialRecord { Purpose = "DifficultyPilot", RowKind = "final", Valid = "true",
                Practice = "false", TrialID = i.ToString(), Status = "Completed", IsThreat = "true", ObjectiveSuccess = TrialRecord.Bool(i < wins) }).ToArray();
            var review = SilentDifficultyReview.Evaluate(rows, 15); Assert.IsTrue(review.Complete); StringAssert.Contains(decision, review.Decision);
            rows[0].Valid = "false"; review = SilentDifficultyReview.Evaluate(rows, 15); Assert.IsFalse(review.Complete); StringAssert.Contains("Insufficient", review.Decision);
        }
    }
}
