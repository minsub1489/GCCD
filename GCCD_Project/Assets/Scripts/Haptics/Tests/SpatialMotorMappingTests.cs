using System;
using System.Collections.Generic;

namespace GCCD.Research
{
    // Framework-free deterministic harness. Can be called from Unity Editor reflection
    // or built and executed against .NET without Unity or bHaptics SDK installed.
    public static class SpatialMotorMappingTests
    {
        private static void Assert(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("Spatial motor test failed: " + reason);
        }

        private static void Near(float actual, float expected, float maxDelta, string reason)
        {
            Assert(Math.Abs(actual - expected) <= maxDelta,
                reason + ": expected " + expected + ", got " + actual);
        }

        public static string RunAll()
        {
            SpatialMotorProfile profile = SpatialMotorProfile.CreateVirtualPreview();
            Assert(profile.Validate(out string error), error);
            Assert(!profile.Validate(out error, true), "Virtual profile falsely verified physical");
            Assert(!profile.physicalLayoutVerified, "Synthetic motor ring claimed hardware verification");

            Near(SpatialMotorMath.Wrap180(180), -180, .0001f, "seam 180");
            Near(SpatialMotorMath.Wrap180(-181), 179, .0001f, "negative wrap");
            Near(SpatialMotorMath.AngularDistance(179, -179), 2, .0001f, "short seam distance");
            Near(SpatialMotorMath.ListenerRelativeAzimuth(0, 0, 0, 1, 0),
                90, .0001f, "world right at heading zero");
            Near(SpatialMotorMath.ListenerRelativeAzimuth(0, 0, 90, 1, 0),
                0, .0001f, "rotating viewer changes world source angle");
            Near(SpatialMotorMath.ListenerRelativeAzimuth(0, 0, 359, 0, 1),
                1, .0001f, "positive yaw seam");
            Near(SpatialMotorMath.ListenerRelativeAzimuth(0, 0, 1, 0, 1),
                -1, .0001f, "negative yaw seam");
            Near(SpatialMotorMath.ToSdk2CounterclockwiseAngle(90), 270, .0001f,
                "SDK2 clockwise user right maps to counterclockwise");
            Near(SpatialMotorMath.ToSdk2CounterclockwiseAngle(-90), 90, .0001f,
                "SDK2 clockwise user left maps to counterclockwise");
            Near(SpatialMotorMath.ToSdk2CounterclockwiseAngle(179), 181, .0001f,
                "SDK2 mapping across ±180 seam");

            var one = new List<SpatialEmitter>
            {
                new SpatialEmitter { active = true, gain = 1, worldX = 0, worldZ = 1 }
            };
            var two = new List<SpatialEmitter>(one) { one[0] };
            float[] single = new float[40];
            float[] combined = new float[40];
            float[] low = new float[40];
            float[] high = new float[40];
            const float cap = .8f;
            Assert(SpatialMotorMath.Mix(profile, one, "medium", 0, 0, 0, 22, cap, single) == 1,
                "Single active source count");
            Near(single[0], .45f, .0001f, "front motor center");
            Assert(single[10] < single[0], "Spatial kernel must favor source-facing motors");
            Assert(SpatialMotorMath.Mix(profile, two, "medium", 0, 0, 0, 22, cap, combined) == 2,
                "Two simultaneous sources count");
            Assert(combined[0] > single[0] && combined[0] <= cap,
                "Overlapping emitters saturate without overflow");
            var separated = new List<SpatialEmitter>
            {
                new SpatialEmitter { active = true, gain = 1, worldX = -1, worldZ = 0 },
                new SpatialEmitter { active = true, gain = 1, worldX = 0, worldZ = -1 },
                new SpatialEmitter { active = true, gain = 1, worldX = 1, worldZ = 0 }
            };
            Assert(SpatialMotorMath.Mix(profile, separated, "high", 0, 0, 0, 22, cap, combined) == 3,
                "Three simultaneous different source bearings counted");
            Assert(combined[10] > .5 && combined[20] > .5 && combined[30] > .5,
                "Three unique bearings activate three different angular sectors");
            foreach (float motor in combined) Assert(motor >= 0 && motor <= cap,
                "Every motor must obey safety ceiling for three sources");
            SpatialMotorMath.Mix(profile, one, "low", 0, 0, 0, 22, cap, low);
            SpatialMotorMath.Mix(profile, one, "high", 0, 0, 0, 22, cap, high);
            Assert(low[0] < single[0] && single[0] < high[0], "Distinct low/medium/high");

            SpatialMotorMath.Mix(profile, two, "none", 0, 0, 0, 22, cap, combined);
            foreach (float motor in combined) Near(motor, 0, 0, "None condition drives zero");

            // Track a stationary world source as the head rotates through the yaw seam.
            var nearSeam = new List<SpatialEmitter>
            {
                new SpatialEmitter
                {
                    active = true, gain = 1f,
                    worldX = (float)Math.Sin(179 * Math.PI / 180),
                    worldZ = (float)Math.Cos(179 * Math.PI / 180)
                }
            };
            float[] a = new float[40];
            float[] b = new float[40];
            SpatialMotorMath.Mix(profile, nearSeam, "medium", 0, 0, 0, 22, cap, a);
            nearSeam[0] = new SpatialEmitter
            {
                active = true, gain = 1f,
                worldX = (float)Math.Sin(-179 * Math.PI / 180),
                worldZ = (float)Math.Cos(-179 * Math.PI / 180)
            };
            SpatialMotorMath.Mix(profile, nearSeam, "medium", 0, 0, 0, 22, cap, b);
            Near(a[20], b[20], .0005f, "180 seam motor continuity");

            // Reorder physical profile entries but keep explicit channel indices.
            SpatialMotor temp = profile.motors[0];
            profile.motors[0] = profile.motors[20];
            profile.motors[20] = temp;
            Assert(profile.Validate(out error), "Reordered index mapping should validate");
            SpatialMotorMath.Mix(profile, one, "medium", 0, 0, 0, 22, cap, combined);
            Near(combined[0], single[0], .0001f, "Indexed output independent of array order");
            profile.motors[20].index = 20;
            Assert(!profile.Validate(out error), "Duplicate hardware index must be rejected");

            var smoothing = new SpatialMotorSmoother();
            smoothing.Step(single, .05f, .04f, .08f);
            Assert(smoothing.Current[0] > 0 && smoothing.Current[0] <= single[0],
                "Attack smoothing bounds");
            smoothing.Reset();
            foreach (float value in smoothing.Current)
                Near(value, 0, 0, "Immediate hard-stop after source removal");

            var mock = new ResearchMockHaptics();
            Assert(mock.SubmitFrame(single, .05f, "test").accepted,
                "20 Hz 40 motor simulation frame accepted");
            Assert(!mock.IsPhysicalHardware, "Mock must not claim hardware");
            Near(mock.Latest[0], single[0], .0001f, "Mock latest frame");
            mock.StopAll();
            foreach (float value in mock.Latest)
                Near(value, 0, 0, "StopAll zeroes every channel");
            Assert(!new ResearchDisabledX40Haptics().SubmitFrame(single, .05f, "test").accepted,
                "Unverified X40 must remain disabled");

            return "PASS: azimuth/yaw seam, SDK2 angle sign, listener rotation, 40-index map, low/med/high, 3-source saturation, none zero, motor continuity, reset, mock safety";
        }
    }
}
