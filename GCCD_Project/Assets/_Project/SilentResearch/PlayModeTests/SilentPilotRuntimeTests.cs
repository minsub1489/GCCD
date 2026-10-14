using System;
using System.Collections;
using System.IO;
using System.Linq;
using GCCD.ContinuousHaptics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace GCCD.SilentResearch.Tests
{
    public sealed class SilentPilotRuntimeTests
    {
        SilentPilotController pilot;
        SilentExperimentSession session;
        string root;
        [UnitySetUp] public IEnumerator Setup()
        {
            SceneManager.LoadScene("SilentHapticResearch"); yield return null;
            session = UnityEngine.Object.FindFirstObjectByType<SilentExperimentSession>();
            pilot = session.GetComponent<SilentPilotController>(); Assert.IsNotNull(pilot);
            root = Path.Combine(Path.GetTempPath(), "GCCD-PilotFixture-" + Guid.NewGuid().ToString("N"));
            pilot.ResultRoot = root; session.ResultRoot = root; session.AutomaticTicks = false;
            session.Stimulus.Mode = OutputMode.Simulation;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { pilot.End("Fixture cleanup"); session.StopSession("Fixture cleanup"); if (Directory.Exists(root)) Directory.Delete(root, true); yield return null; }
        [UnityTest] public IEnumerator SimulationCannotCertifyAndAbortWritesExactlyOneFinalRow()
        {
            Assert.IsTrue(pilot.Begin(PilotKind.Calibration));
            Assert.IsFalse(session.StartSession(true));
            yield return null;
            var ui = session.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsTrue(ui.Q("researcherRoot").ClassListContains("running"));
            Assert.AreEqual(DisplayStyle.Flex, ui.Q("pilotRoot").resolvedStyle.display);
            Assert.AreEqual(40, ui.Q("motorChoices").Query<Button>().ToList().Count);
            pilot.End("Fixture interruption"); pilot.End("Duplicate abort");
            Assert.AreEqual(1, pilot.Records.Count); Assert.AreEqual("Aborted", pilot.Records[0].Status);
            Assert.AreEqual("false", pilot.Records[0].Valid); Assert.IsFalse(pilot.CalibrationMatches());
            Assert.AreEqual(2, File.ReadAllLines(Path.Combine(pilot.ResultDirectory, "trials.csv")).Length);
            Assert.IsFalse(pilot.Summary.Passed);
        }
        [UnityTest] public IEnumerator BlindTypeTimeoutKeepsMissingResponseAndDoesNotShowTruth()
        {
            Assert.IsTrue(pilot.Begin(PilotKind.TypeIdentification)); pilot.AutomaticTicks = false;
            double onset = double.Parse(pilot.Current.ScheduledOnsetSec, System.Globalization.CultureInfo.InvariantCulture) + .001;
            pilot.Tick(onset); yield return null;
            var ui = session.GetComponent<UIDocument>().rootVisualElement;
            Assert.AreEqual(DisplayStyle.None, ui.Q("motorChoices").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, ui.Q("directionChoices").resolvedStyle.display);
            Assert.IsFalse(ui.Q<Label>("pilotProgress").text.Contains(pilot.Current.EventType));
            pilot.Tick(onset + 4.1);
            Assert.AreEqual(1, pilot.Records.Count); Assert.AreEqual("NoResponse", pilot.Records[0].PlayerResponse);
            Assert.AreEqual("", pilot.Records[0].TypeTimeMs); Assert.AreEqual("false", pilot.Records[0].DeliveryAccepted);
            pilot.End();
        }
        [UnityTest] public IEnumerator InputSystemMouseClickReachesTheHardwareCheckButton()
        {
            var document = session.GetComponent<UIDocument>();
            var ui = document.rootVisualElement;
            var button = ui.Q<Button>("checkHardware");
            ui.Q("researcherRoot").Q<ScrollView>().ScrollTo(button);
            yield return null; yield return null;
            var module = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            Assert.IsTrue(module.point.action.enabled, "UI pointer action must be enabled");
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(ui.panel, Vector2.zero);
            Vector2 far = RuntimePanelUtils.ScreenToPanel(ui.panel, new Vector2(Screen.width, Screen.height));
            Vector2 target = button.worldBound.center;
            var picked = ui.panel.Pick(target);
            Assert.IsTrue(picked == button || picked?.GetFirstAncestorOfType<Button>() == button,
                "Scrolled button must be the picked element at " + target + "; picked " + picked?.name);
            var screen = new Vector2((target.x - origin.x) * Screen.width / (far.x - origin.x),
                Screen.height - (target.y - origin.y) * Screen.height / (far.y - origin.y));
            var originalBackground = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.EnableDevice(mouse);
            var originalDevices = module.actionsAsset.devices;
            // Isolate the synthetic mouse from the host's native pointer during the headless test.
            module.actionsAsset.devices = new InputDevice[] { mouse };
            Assert.IsTrue(module.point.action.actionMap.asset == module.actionsAsset, "Pointer reference must belong to the UI input asset");
            string before = pilot.Message;
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); InputSystem.Update();
                Assert.That(Vector2.Distance(mouse.position.ReadValue(), screen), Is.LessThan(1), "Synthetic mouse state must be processed: " + mouse.position.ReadValue() + " target " + screen);
                yield return null;
                Assert.That(Vector2.Distance(module.point.action.ReadValue<Vector2>(), screen), Is.LessThan(1), "UI module must read the injected pointer");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left)); InputSystem.Update(); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); InputSystem.Update(); yield return null;
                Assert.AreNotEqual(before, pilot.Message, "Mouse click must invoke the live UI callback");
            }
            finally
            {
                module.actionsAsset.devices = originalDevices; InputSystem.RemoveDevice(mouse); InputSystem.settings.backgroundBehavior = originalBackground;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
#endif
            }
        }
        [UnityTest] public IEnumerator DifficultyPilotIsStoredSeparatelyAndIncompleteBlockCannotPass()
        {
            session.Condition = FeedbackCondition.AudioOnly;
            Assert.IsTrue(session.StartDifficultyPilot(), session.Message); Assert.IsFalse(session.Practice);
            Assert.AreEqual("DifficultyPilot", session.Purpose); Assert.AreEqual("DifficultyPilot", session.Current.Purpose);
            Assert.AreNotEqual(session.RandomSeed.ToString(), session.Current.RandomSeed);
            session.StopSession("Fixture ended");
            Assert.IsTrue(File.Exists(Path.Combine(session.ResultDirectory, "difficulty-review.json")));
            var review = JsonUtility.FromJson<DifficultyReview>(File.ReadAllText(Path.Combine(session.ResultDirectory, "difficulty-review.json")));
            Assert.IsFalse(review.Complete); Assert.AreEqual(1, review.AbortedTrials);
            yield return null;
        }
    }
}
