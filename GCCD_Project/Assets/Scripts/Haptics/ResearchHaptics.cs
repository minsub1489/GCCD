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
        HapticDelivery SubmitFrame(float[] orderedByMotorIndex, float frameDurationSeconds, string traceId);
        void StopAll();
    }

    // Stores the most recent frame for preview, without any hardware access.
    public sealed class ResearchMockHaptics : IResearchHapticOutput
    {
        public string Name => "40-motor Mock (virtual output only)";
        public bool IsPhysicalHardware => false;
        public bool IsVerifiedReady => true;
        public readonly float[] Latest = new float[40];
        public int FrameCount { get; private set; }

        public HapticDelivery SubmitFrame(float[] frame, float frameDurationSeconds, string traceId)
        {
            if (frame == null || frame.Length != Latest.Length ||
                frameDurationSeconds <= 0 || frameDurationSeconds > .2f)
                return new HapticDelivery { accepted = false, physicallyDelivered = false,
                    status = "invalid_frame", source = "mock" };
            for (int i = 0; i < Latest.Length; i++)
            {
                if (!SpatialMotorMath.Finite(frame[i]) || frame[i] < 0 || frame[i] > 1)
                    return new HapticDelivery { accepted = false, physicallyDelivered = false,
                        status = "invalid_motor_value", source = "mock" };
            }
            Array.Copy(frame, Latest, 40);
            FrameCount++;
            return new HapticDelivery
            {
                accepted = true, physicallyDelivered = false,
                status = "mock_simulated_not_delivered", source = "mock_40_channel"
            };
        }

        public void StopAll() => Array.Clear(Latest, 0, Latest.Length);
        public void Dispose() => StopAll();

        // Legacy Editor smoke-test compatibility only; production uses SubmitFrame.
        [Obsolete("Use continuous 40-motor SubmitFrame.")]
        public HapticDelivery Request(TorsoZone zone, float normalizedStrength, float seconds, string traceId) =>
            new HapticDelivery
            {
                accepted = normalizedStrength > 0 && normalizedStrength <= 1f,
                physicallyDelivered = false,
                status = normalizedStrength > 0 ? "legacy_mock_only" : "not_requested",
                source = "mock"
            };
    }

    // No known verified API or layout is installed. Deliberately blocked.
    // Official bHaptics SDK motor mapping examples cover other models;
    // do not infer X40 indices from a different device's 32-motor table.
    public sealed class ResearchDisabledX40Haptics : IResearchHapticOutput
    {
        public string Name => "X40 (hardware SDK/mapping not verified; blocked)";
        public bool IsPhysicalHardware => true;
        public bool IsVerifiedReady => false;
        public HapticDelivery SubmitFrame(float[] frame, float frameDurationSeconds, string traceId) =>
            new HapticDelivery
            {
                accepted = false,
                physicallyDelivered = false,
                status = "blocked_sdk_and_40_motor_mapping_unverified",
                source = "disabled_x40_adapter"
            };
        public void StopAll() { }
        public void Dispose() { }

        [Obsolete("Use continuous 40-motor SubmitFrame.")]
        public HapticDelivery Request(TorsoZone zone, float normalizedStrength, float seconds, string traceId) =>
            new HapticDelivery
            {
                accepted = false,
                physicallyDelivered = false,
                status = "blocked_sdk_and_40_motor_mapping_unverified",
                source = "disabled_x40_adapter"
            };
    }
}
