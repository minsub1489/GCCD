using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GCCD.SilentResearch.Tests
{
    public sealed class SilentRuntimeTests
    {
        SilentExperimentSession session;
        string root;
        [UnitySetUp] public IEnumerator Setup()
        {
            SceneManager.LoadScene("SilentHapticResearch"); yield return null;
            session = UnityEngine.Object.FindFirstObjectByType<SilentExperimentSession>();
            Assert.IsNotNull(session, "Generate the research scene before running PlayMode tests");
            root = Path.Combine(Path.GetTempPath(), "GCCD-SilentTest-" + Guid.NewGuid().ToString("N"));
            session.ResultRoot = root; session.AutomaticTicks = false;
            session.Stimulus.Mode = OutputMode.Simulation;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            session.StopSession("Test ended");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            yield return null;
        }
        [UnityTest] public IEnumerator AllFourConditionsUseSameScenarioAndCorrectOutputChannels()
        {
            SilentTrial[] firstPlan = null;
            while (!FirstPracticeTrial().Threat) session.RandomSeed++;
            foreach (FeedbackCondition condition in Enum.GetValues(typeof(FeedbackCondition)))
            {
                session.Condition = condition; Assert.IsTrue(session.StartSession(true), session.Message);
                var plan = session.Plan.ToArray(); if (firstPlan == null) firstPlan = plan; else CollectionAssert.AreEqual(firstPlan, plan);
                // Same seeded first threat in all four conditions.
                double now = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
                session.Tick(now, .016f);
                if (plan[0].Threat)
                {
                    Assert.AreEqual(condition == FeedbackCondition.AudioOnly, session.Stimulus.AudioAudible);
                    Assert.AreEqual(condition != FeedbackCondition.AudioOnly, Enumerable.Range(0, 40).Any(i => session.Stimulus.Motor(i) > 0));
                }
                Assert.IsFalse(session.World.ThreatVisible);
                session.StopSession();
                Assert.AreEqual("false", session.Records[0].Valid);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator FirstResponsesAreRecordedOnceAndCircularDirectionIsCorrect()
        {
            session.Condition = FeedbackCondition.TypeDirectionHaptic;
            // Choose a seeded plan whose first episode is a threat; participant data are never fabricated.
            while (!FirstPracticeTrial().Threat) session.RandomSeed++;
            Assert.IsTrue(session.StartSession(true), session.Message);
            double onset = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
            session.Tick(onset, .016f);
            var type = session.Plan[0].Type;
            session.Detect(true, onset + .1); session.Detect(false, onset + .2);
            session.Classify(type, onset + .2); session.Classify((EventType)(((int)type + 1) % 3), onset + .3);
            Assert.IsTrue(SilentTrialPlan.TryBearing(session.Stimulus.View, session.Scenario.EventAnchors[session.Plan[0].DirectionIndex], out var direction));
            session.Direction(direction.Azimuth + 359, onset + .3); session.Direction(180, onset + .4);
            Assert.AreEqual("Threat", session.Current.DetectionResponse); Assert.AreEqual(type.ToString(), session.Current.TypeResponse);
            Assert.AreEqual("true", session.Current.IsCorrect); Assert.That(double.Parse(session.Current.DirectionErrorDeg), Is.EqualTo(1).Within(.001));
            session.StopSession("Interrupted");
            Assert.AreEqual(1, session.Records.Count);
            Assert.AreEqual("Aborted", session.Records[0].Status); Assert.AreEqual("false", session.Records[0].Valid);
            Assert.AreEqual(2, File.ReadAllLines(Path.Combine(session.ResultDirectory, "trials.csv")).Length);
            yield return null;
        }
        [UnityTest] public IEnumerator NoResponseTimeoutAndCatchAreRetainedWithMissingTimes()
        {
            session.Condition = FeedbackCondition.AudioOnly;
            while (FirstPracticeTrial().Threat) session.RandomSeed++;
            Assert.IsTrue(session.StartSession(true), session.Message);
            double onset = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
            session.Tick(onset, .016f);
            session.Tick(onset + session.Scenario.TrialLimitSec, .016f);
            Assert.AreEqual(1, session.Records.Count);
            var row = session.Records[0]; Assert.AreEqual("Timeout", row.Status); Assert.AreEqual("NoResponse", row.PlayerResponse);
            Assert.AreEqual("", row.ReactionTimeMs); Assert.AreEqual("", row.CompletionTimeSec); Assert.AreEqual("", row.AvoidanceSuccess);
            session.StopSession();
            Assert.AreEqual(1, session.Records.Count);
            yield return null;
        }
        [UnityTest] public IEnumerator MeasurementRejectsSimulationAndSettingsChangeAborts()
        {
            session.Condition = FeedbackCondition.BasicHaptic; Assert.IsFalse(session.StartSession(false));
            StringAssert.Contains("calibrated X40", session.Message);
            Assert.IsTrue(session.StartSession(true));
            double time = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture);
            session.RandomSeed++; session.Tick(time, .016f);
            Assert.IsFalse(session.Running); Assert.AreEqual("false", session.Records[0].Valid);
            StringAssert.Contains("Configuration changed", session.Records[0].AbortReason);
            yield return null;
        }
        SilentTrial FirstPracticeTrial() => SilentTrialPlan.Create(SilentTrialPlan.EffectiveSeed(session.RandomSeed, true), 1, 1.5f, 3)[0];
        [UnityTest] public IEnumerator ToolkitControlsAreBoundAndHiddenThroughoutMeasurement()
        {
            var root = session.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsNotNull(session.GetComponent<SilentResearcherUI>().UIFont);
            Assert.IsNotNull(root.style.unityFontDefinition.value.fontAsset);
            Assert.IsNotNull(root.Q<Button>("practice")); Assert.IsNotNull(root.Q<DropdownField>("condition"));
            session.Condition = FeedbackCondition.AudioOnly;
            Assert.IsTrue(session.StartSession(false), session.Message);
            session.GetComponent<SilentResearcherUI>().ShowPracticeSimulator = true;
            yield return null;
            Assert.IsFalse(session.Practice); Assert.IsTrue(root.Q("researcherRoot").ClassListContains("running"));
            session.StopSession("UI test ended"); yield return null;
            Assert.IsFalse(root.Q("researcherRoot").ClassListContains("running"));
            Assert.AreEqual("false", session.Records[0].Valid);
        }
        [UnityTest] public IEnumerator CatchFalseAlarmKeepsInapplicableFieldsBlank()
        {
            session.Condition = FeedbackCondition.AudioOnly;
            while (FirstPracticeTrial().Threat) session.RandomSeed++;
            Assert.IsTrue(session.StartSession(true), session.Message);
            double onset = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
            session.Tick(onset, .016f); session.Detect(true, onset + .1); session.Detect(false, onset + .2);
            session.Tick(onset + session.Scenario.TrialLimitSec, .016f);
            var row = session.Records[0]; Assert.AreEqual("Threat", row.DetectionResponse); Assert.AreEqual("false", row.DetectionSuccess);
            Assert.AreEqual("", row.EventDirection); Assert.AreEqual("", row.IsCorrect); Assert.AreEqual("", row.AvoidanceSuccess);
            yield return null;
        }
        [UnityTest] public IEnumerator GrowlPursuitActuallyCatchesAPlayerUnderCover()
        {
            session.Condition = FeedbackCondition.AudioOnly;
            while (!FirstPracticeTrial().Threat || FirstPracticeTrial().Type != EventType.Growl) session.RandomSeed++;
            Assert.IsTrue(session.StartSession(true), session.Message);
            double onset = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
            session.Tick(onset, .016f);
            session.World.Player.ResetPose(session.World.HidingVolumes[0].transform.position, Quaternion.identity);
            session.World.SetCrouch(true); Assert.IsTrue(session.World.Hidden);
            for (int i = 1; i <= 490 && session.Records.Count == 0; i++) session.Tick(onset + i / 60.0, 1f / 60);
            Assert.IsNotEmpty(session.Records); Assert.AreEqual("Caught", session.Records[0].Status);
            Assert.AreEqual("false", session.Records[0].AvoidanceSuccess); Assert.AreEqual("false", session.Records[0].ObjectiveSuccess);
            yield return null;
        }
        [UnityTest] public IEnumerator GoalCompletionAndCorrectDefenseUseActualWorldState()
        {
            session.Condition = FeedbackCondition.AudioOnly;
            while (!FirstPracticeTrial().Threat ||
                FirstPracticeTrial().Type == EventType.Growl) session.RandomSeed++;
            Assert.IsTrue(session.StartSession(true));
            double onset = double.Parse(session.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
            session.Tick(onset, .016f);
            session.World.Player.ResetPose(session.World.HidingVolumes[0].transform.position, Quaternion.identity);
            session.World.SetCrouch(true); Assert.IsTrue(session.World.Hidden);
            session.Defense("Hide", onset + .4); Assert.AreEqual("400", session.Current.ReactionTimeMs);
            // Reaching EXIT early cannot bypass the common pursuit duration.
            session.World.Player.ResetPose(session.Scenario.Goal, Quaternion.identity);
            session.Tick(onset + 1, .016f); Assert.IsEmpty(session.Records);
            session.Tick(onset + session.Scenario.ResponseWindowSec + session.Scenario.PursuitWindowSec + .1, .016f);
            Assert.AreEqual("Success", session.Records[0].Status); Assert.AreEqual("true", session.Records[0].ObjectiveSuccess);
            Assert.AreEqual("true", session.Records[0].AvoidanceSuccess);
            Assert.AreNotEqual("", session.Records[0].CompletionTimeSec);
            yield return null;
        }
    }
}
