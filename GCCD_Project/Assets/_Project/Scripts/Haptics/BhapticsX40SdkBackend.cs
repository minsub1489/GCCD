#if GCCD_BHAPTICS_SDK2
using System;
using Bhaptics.SDK2;
using GCCD.ContinuousHaptics;
using UnityEngine;

namespace GCCD.Minimal
{
    // API signatures were verified against the installed SDK2 2.8.1 source.
    // Keep the licensed SDK and credentials outside Git. No reflection or guessed API calls.
    public sealed class BhapticsX40SdkBackend : IX40SdkBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { BHapticsVestOutput.BackendFactory = () => new BhapticsX40SdkBackend(); }

        public bool CheckConnection(bool confirmRenamedX40, out string status)
        {
            if (!UnityEngine.Object.FindFirstObjectByType<BhapticsSDK2>())
            {
                var settings = BhapticsSettings.Instance;
                if (settings == null || string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.ApiKey))
                { status = "Configure the installed SDK in bHaptics > Developer Window"; return false; }
                // Same official initializer used by [bhaptics], created only when hardware is requested.
                // No SDK-specific scene reference: a clean checkout remains usable without the SDK.
                new GameObject("[bhaptics] (X40 runtime initializer)").AddComponent<BhapticsSDK2>();
            }
            if (!BhapticsLibrary.IsBhapticsAvailableForce(false))
            { status = "bHaptics Player unavailable"; return false; }
            if (!BhapticsLibrary.IsConnect(PositionType.Vest))
            { status = "Vest disconnected"; return false; }
            var devices = BhapticsLibrary.GetDevices();
            int count = 0; bool x40 = false;
            foreach (var device in devices)
            {
                if (device == null || !device.IsConnected || !device.IsPaired || device.Position != PositionType.Vest) continue;
                count++;
                x40 = !string.IsNullOrEmpty(device.DeviceName)
                    && device.DeviceName.IndexOf("x40", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            if (count != 1) { status = "Connect exactly one X40 vest"; return false; }
            if (!x40 && !confirmRenamedX40)
            { status = "Verify X40 model; device name does not contain X40"; return false; }
            status = "X40 connected (identity by name / manual confirmation)";
            return true;
        }
        public int PlayMotors(int[] motors, int durationMillis)
            => BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, durationMillis);
        public bool StopRequest(int requestId) => BhapticsLibrary.StopInt(requestId);
    }
}
#endif
