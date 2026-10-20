// Enable this only after importing the official bHaptics SDK2 Unity package.
// A compile symbol alone NEVER enables X40 output in ResearchController.
// Verified reference: https://docs.bhaptics.com/sdk/unity/references/library
#if GCCD_BHAPTICS_SDK2
using System;
using System.Collections.Generic;
using Bhaptics.SDK2;

namespace GCCD.Research
{
    /// <summary>
    /// SDK2 integration reference restricted to the DOCUMENTED TactSuit Pro 32 motors.
    /// X40 has 40 motors and is NOT compatible with this call's documented array shape.
    /// Not created by the research runtime. Never reports X40 verified readiness.
    /// The SDK reports a request ID, not confirmed skin-level delivery.
    /// </summary>
    internal static class ResearchSdk2Pro32Reference
    {
        private static int previousRequest = -1;

        // A separate operator-led Pro32 verification workflow would have to pass
        // an explicit device-model authorization AND an actual 32-channel map.
        // Never call this with X40 frames, indices or estimated motor geometry.
        internal static int SubmitDocumentedPro32(
            int[] verifiedPro32MotorLevels, int durationMilliseconds,
            bool physicalPro32DeviceVerified)
        {
            if (!physicalPro32DeviceVerified ||
                verifiedPro32MotorLevels == null || verifiedPro32MotorLevels.Length != 32 ||
                durationMilliseconds < 100)
                return -1;
            for (int i = 0; i < verifiedPro32MotorLevels.Length; i++)
                if (verifiedPro32MotorLevels[i] < 0 || verifiedPro32MotorLevels[i] > 100)
                    return -1;

            // Official position 0 corresponds to TactSuit Pro's 32-motor vest.
            // 100ms minimum follows the SDK's recommended haptic duration.
            previousRequest = BhapticsLibrary.PlayMotors(0, verifiedPro32MotorLevels, durationMilliseconds);
            return previousRequest;
        }

        internal static void StopOwnedPro32Request()
        {
            if (previousRequest < 0) return;
            BhapticsLibrary.StopInt(previousRequest);
            previousRequest = -1;
        }
    }

    /// <summary>
    /// Optional SDK2 directional event bridge that lets the SDK itself decide
    /// physical motor routing. This is NOT an X40 40-motor index mapping.
    /// ResearchController does not construct or enable it: physical mapping,
    /// installed bHaptics Player, registered event, and skin-level delivery
    /// must be verified in a separate manual integration/acceptance process.
    /// </summary>
    internal sealed class ResearchSdk2RotatedEventReference : IDisposable
    {
        private sealed class ActiveEvent
        {
            public int requestId;
            public double nextDispatchAt;
        }

        private readonly Dictionary<string, ActiveEvent> active = new Dictionary<string, ActiveEvent>();
        private readonly string registeredEventId;
        private readonly bool approved;
        private readonly int requestIdBase;
        private int assignedCount;

        /// <param name="authoredEventAtLeast100Milliseconds">
        /// PlayParam duration is a MULTIPLIER, not milliseconds. The approved
        /// Designer/Portal event must independently be checked for >=100ms length.
        /// </param>
        internal ResearchSdk2RotatedEventReference(
            string registeredEventId, bool sdkAppAuthorized, bool connectedWearableVerified,
            bool sourceDirectionMappingVerified, bool authoredEventAtLeast100Milliseconds)
        {
            this.registeredEventId = registeredEventId;
            approved = !string.IsNullOrWhiteSpace(registeredEventId) && sdkAppAuthorized &&
                connectedWearableVerified && sourceDirectionMappingVerified &&
                authoredEventAtLeast100Milliseconds;
            requestIdBase = new Random(Guid.NewGuid().GetHashCode()).Next(100000, 8000000);
        }

        internal bool IsManuallyApproved => approved;

        // Input is listener-relative azimuth: right is positive, forward is zero.
        // SDK's angleX rotates COUNTERCLOCKWISE around Y. Negate this clockwise
        // source angle to get SDK's documented rotation direction.
        internal static float SdkCounterclockwiseAngle(float viewerRightAzimuth)
        {
            return SpatialMotorMath.ToSdk2CounterclockwiseAngle(viewerRightAzimuth);
        }

        internal bool UpdateSource(
            string sourceId, float listenerRelativeAzimuth, float normalizedIntensity,
            double monotonicNowSeconds)
        {
            if (!approved || string.IsNullOrWhiteSpace(sourceId) ||
                !SpatialMotorMath.Finite(listenerRelativeAzimuth) ||
                !SpatialMotorMath.Finite(normalizedIntensity) ||
                double.IsNaN(monotonicNowSeconds) || double.IsInfinity(monotonicNowSeconds))
                return false;
            if (normalizedIntensity <= 0f)
            {
                StopSource(sourceId);
                return true;
            }
            if (!active.TryGetValue(sourceId, out ActiveEvent state))
            {
                if (active.Count >= 3) return false; // Study max: three simultaneous threats.
                state = new ActiveEvent
                {
                    requestId = requestIdBase + assignedCount++,
                    nextDispatchAt = monotonicNowSeconds
                };
                active.Add(sourceId, state);
            }
            if (monotonicNowSeconds < state.nextDispatchAt) return true;
            state.nextDispatchAt = monotonicNowSeconds + .1; // Max 10Hz per source.
            int response = BhapticsLibrary.PlayParam(
                registeredEventId, SpatialMotorMath.Clamp01(normalizedIntensity), 1.0f,
                SdkCounterclockwiseAngle(listenerRelativeAzimuth), 0f, state.requestId);
            // The Hub may accept without end-to-end delivery confirmation.
            return response >= 0;
        }

        internal void StopSource(string sourceId)
        {
            if (!active.TryGetValue(sourceId, out ActiveEvent state)) return;
            BhapticsLibrary.StopInt(state.requestId);
            active.Remove(sourceId);
        }

        internal void StopAllOwned()
        {
            foreach (ActiveEvent state in active.Values)
                BhapticsLibrary.StopInt(state.requestId);
            active.Clear();
        }

        public void Dispose() => StopAllOwned();
    }
}
#endif
