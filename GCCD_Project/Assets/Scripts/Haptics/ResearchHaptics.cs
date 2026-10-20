using System;

namespace GCCD.Research
{
    public struct HapticDelivery
    {
        public bool accepted;
        public bool physicallyDelivered;
        public string status;
        public string source;
    }

    public interface IResearchHapticOutput : IDisposable
    {
        string Name { get; }
        bool IsPhysicalHardware { get; }
        bool IsVerifiedReady { get; }
        HapticDelivery Request(TorsoZone zone, float normalizedStrength, float seconds, string traceId);
        void StopAll();
    }

    // This deliberately does not pretend to drive a vest. It only traces requests.
    public sealed class ResearchMockHaptics : IResearchHapticOutput
    {
        public string Name => "Mock (no physical haptics)";
        public bool IsPhysicalHardware => false;
        public bool IsVerifiedReady => true;

        public HapticDelivery Request(TorsoZone zone, float normalizedStrength, float seconds, string traceId)
        {
            return new HapticDelivery
            {
                accepted = normalizedStrength > 0,
                physicallyDelivered = false,
                status = normalizedStrength > 0 ? "mock_simulated_not_delivered" : "not_requested",
                source = "mock"
            };
        }
        public void StopAll() { }
        public void Dispose() { }
    }

    // A separate verified adapter must be written against the installed bHaptics SDK,
    // deployed AppID and independently checked 4-zone hardware mapping.
    public sealed class ResearchDisabledX40Haptics : IResearchHapticOutput
    {
        public string Name => "X40 (SDK not integrated / disabled)";
        public bool IsPhysicalHardware => true;
        public bool IsVerifiedReady => false;

        public HapticDelivery Request(TorsoZone zone, float normalizedStrength, float seconds, string traceId)
        {
            return new HapticDelivery
            {
                accepted = false,
                physicallyDelivered = false,
                status = "blocked_sdk_and_mapping_unverified",
                source = "disabled_x40_adapter"
            };
        }
        public void StopAll() { }
        public void Dispose() { }
    }
}
