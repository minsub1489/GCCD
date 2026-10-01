using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GCCD.ContinuousHaptics.PlayModeTests
{
    public sealed class RecordingOutput : HapticVestOutput
    {
        public int Sends, Stops;
        public bool Playing;
        public override string Status => "Test output";
        public override bool TrySend(int[] motors, int durationMillis) { Sends++; Playing = true; return true; }
        public override void Stop() { Stops++; Playing = false; }
    }
    public sealed class HapticLifecycleTests
    {
        GameObject root, outputObject;
        HapticThreatController controller;
        RecordingOutput output;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            outputObject = new GameObject("Fake vest"); output = outputObject.AddComponent<RecordingOutput>();
            root = new GameObject("Controller"); controller = root.AddComponent<HapticThreatController>();
            controller.TestMode = true; controller.TestDistance = 2; controller.Output = output;
            controller.EnableSmoothing = false;
            yield return null;
            controller.SendMessage("OnApplicationFocus", true);
            controller.Tick(Time.unscaledTimeAsDouble);
            Assert.IsTrue(output.Playing);
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        { BHapticsVestOutput.BackendFactory = null; if (root) Object.Destroy(root); if (outputObject) Object.Destroy(outputObject); yield return null; }

        [UnityTest]
        public IEnumerator ComponentDisableStopsImmediately()
        { controller.enabled = false; Assert.IsFalse(output.Playing); yield return null; Assert.IsFalse(output.Playing); }
        [UnityTest]
        public IEnumerator GameObjectDisableStopsImmediately()
        { root.SetActive(false); Assert.IsFalse(output.Playing); yield return null; Assert.IsFalse(output.Playing); }
        [UnityTest]
        public IEnumerator DestroyStopsOutputOnSeparateObject()
        { Object.Destroy(root); yield return null; Assert.IsFalse(output.Playing); }
        [UnityTest]
        public IEnumerator FocusPauseQuitCallbacksStop()
        {
            controller.SendMessage("OnApplicationFocus", false); Assert.IsFalse(output.Playing);
            yield return null; Assert.IsFalse(output.Playing);
            controller.SendMessage("OnApplicationFocus", true); controller.Tick(Time.unscaledTimeAsDouble); Assert.IsTrue(output.Playing);
            controller.SendMessage("OnApplicationPause", true); Assert.IsFalse(output.Playing);
            controller.SendMessage("OnApplicationPause", false); controller.Tick(Time.unscaledTimeAsDouble); Assert.IsTrue(output.Playing);
            controller.SendMessage("OnApplicationQuit"); Assert.IsFalse(output.Playing);
        }
        [UnityTest]
        public IEnumerator UpdateRateDoesNotSendEveryFrame()
        {
            controller.HapticUpdateRate = 5;
            controller.Tick(Time.unscaledTimeAsDouble);
            int start = output.Sends;
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(output.Sends - start, Is.InRange(1, 3));
        }
        [UnityTest]
        public IEnumerator ActualOutputWithoutSdkOrDeviceDoesNotThrow()
        {
            controller.EnableHaptics = false;
            var real = root.AddComponent<BHapticsVestOutput>(); var values = new int[40]; values[0] = 10;
            Assert.IsFalse(real.TrySend(values, 100));
            Assert.DoesNotThrow(real.Stop);
            yield return null;
        }
        sealed class SimulatedSdk : IX40SdkBackend
        {
            public bool Connected = true, StopSucceeds = true;
            public int Sends, Stops;
            public bool CheckConnection(bool confirmed, out string status) { status = "Simulated connection"; return Connected; }
            public int PlayMotors(int[] values, int duration) { return ++Sends; }
            public bool StopRequest(int id) { Stops++; return StopSucceeds; }
        }
        [UnityTest]
        public IEnumerator OutputCancelsOwnedFramesAndStopsOnDisconnect()
        {
            controller.enabled = false;
            var sdk = new SimulatedSdk(); BHapticsVestOutput.BackendFactory = () => sdk;
            var real = root.AddComponent<BHapticsVestOutput>(); var values = new int[40]; values[0] = 10;
            Assert.IsTrue(real.TrySend(values, 200)); Assert.IsTrue(real.TrySend(values, 200));
            Assert.That(sdk.Sends, Is.EqualTo(2)); Assert.That(sdk.Stops, Is.EqualTo(1));
            sdk.Connected = false; Assert.IsFalse(real.TrySend(values, 200));
            Assert.That(sdk.Stops, Is.EqualTo(2)); Assert.That(real.LastRequestId, Is.EqualTo(-1));
            yield return null;
        }
        [UnityTest]
        public IEnumerator FailedStopWaitsForExpiryBeforeSendingAgain()
        {
            controller.enabled = false;
            var sdk = new SimulatedSdk { StopSucceeds = false }; BHapticsVestOutput.BackendFactory = () => sdk;
            var real = root.AddComponent<BHapticsVestOutput>(); var values = new int[40]; values[0] = 10;
            Assert.IsTrue(real.TrySend(values, 100)); Assert.IsFalse(real.TrySend(values, 100));
            Assert.IsFalse(real.TrySend(values, 100)); Assert.That(sdk.Sends, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.12f);
            Assert.IsTrue(real.TrySend(values, 100)); Assert.That(sdk.Sends, Is.EqualTo(2));
            sdk.StopSucceeds = true;
        }
    }
}
