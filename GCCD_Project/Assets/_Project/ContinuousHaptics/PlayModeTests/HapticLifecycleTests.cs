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
        GameObject root;
        [SetUp] public void Setup() { root = new GameObject("Adapter test"); BHapticsVestOutput.BackendFactory = null; }
        [TearDown] public void Teardown() { if (root) Object.DestroyImmediate(root); BHapticsVestOutput.BackendFactory = null; }
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
