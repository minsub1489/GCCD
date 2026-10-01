using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace GCCD.ContinuousHaptics.Tests
{
    public sealed class FakeVestOutput : HapticVestOutput
    {
        public int Sends, Stops, Duration;
        public readonly int[] Last = new int[40];
        public override string Status => "Fake output";
        public override bool TrySend(int[] motors, int durationMillis)
        { Sends++; Duration = durationMillis; Array.Copy(motors, Last, 40); return true; }
        public override void Stop() { Stops++; Array.Clear(Last, 0, 40); }
    }
    public class HapticMappingTests
    {
        public static IEnumerable DirectionCases()
        {
            foreach (SpatialMappingMode mode in Enum.GetValues(typeof(SpatialMappingMode)))
                for (int a = 0; a < 16; a++)
                    foreach (float e in new[] { -90f, -45f, 0f, 45f, 90f })
                        yield return new TestCaseData(a * 22.5f, e, mode);
        }
        [TestCaseSource(nameof(DirectionCases))]
        public void AllRequestedDirectionsAndDistances(float azimuth, float elevation, SpatialMappingMode mode)
        {
            var mapper = new HapticSpatialMapper { Mode = mode };
            var distance = new DistanceIntensityMapper { MaximumIntensity = 1 };
            var w = new float[40]; var m = new int[40];
            foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                float d = Mathf.Lerp(distance.NearDistance, distance.FarDistance, fraction);
                Assert.That(HapticDirectionCalculator.TryFromLocal(HapticDirectionCalculator.ToLocal(azimuth, elevation, d), azimuth, out var sample));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(sample.Azimuth, azimuth)), Is.LessThan(.001));
                Assert.That(sample.Elevation, Is.EqualTo(elevation).Within(.001));
                Assert.That(sample.Distance, Is.EqualTo(d).Within(.0001));
                Assert.That(mapper.TryMap(sample.Azimuth, sample.Elevation, w, out var uv));
                Assert.That(uv.y, Is.EqualTo((elevation + 90) / 180).Within(.0001));
                float sum = 0; foreach (float weight in w) { Assert.That(weight, Is.InRange(0f, 1f)); sum += weight; }
                Assert.That(sum, Is.EqualTo(1).Within(.00001));
                Assert.That(distance.TryEvaluate(d, out _, out float intensity));
                Assert.That(intensity, Is.EqualTo(1 - fraction).Within(.00001));
                Assert.That(HapticSpatialMapper.TryQuantize(w, intensity, m));
                int total = 0; foreach (int value in m) { Assert.That(value, Is.InRange(0, 100)); total += value; }
                Assert.That(total, Is.EqualTo(Mathf.FloorToInt(intensity * 100)));
            }
        }
        [Test]
        public void CardinalLocationsUseCorrectPanelsAndWearerSides()
        {
            var s = new HapticSpatialMapper(); var w = new float[40];
            s.TryMap(0, 0, w, out _); Assert.That(w[9], Is.EqualTo(.5)); Assert.That(w[10], Is.EqualTo(.5));
            s.TryMap(90, 0, w, out _); Assert.That(w[11], Is.EqualTo(.5)); Assert.That(w[31], Is.EqualTo(.5));
            s.TryMap(180, 0, w, out _); Assert.That(w[29], Is.EqualTo(.5)); Assert.That(w[30], Is.EqualTo(.5));
            s.TryMap(270, 0, w, out _); Assert.That(w[8], Is.EqualTo(.5)); Assert.That(w[28], Is.EqualTo(.5));
        }
        [TestCase(SpatialMappingMode.LinearInterpolation)]
        [TestCase(SpatialMappingMode.Gaussian)]
        public void SeamsAndPolesAreContinuous(SpatialMappingMode mode)
        {
            var s = new HapticSpatialMapper { Mode = mode }; var a = new float[40]; var b = new float[40];
            foreach (float angle in new[] { 0f, 22.5f, 90f, 180f, 270f, 359.999f })
            {
                s.TryMap(angle - .001f, 17, a, out _); s.TryMap(angle + .001f, 17, b, out _);
                AssertDifference(a, b, .001f);
            }
            foreach (float e in new[] { -90f, 90f })
            {
                s.TryMap(0, e, a, out _); s.TryMap(173, e, b, out _); AssertDifference(a, b, .00001f);
                s.TryMap(17, e * .99999f, a, out _); AssertDifference(a, b, .0001f);
            }
        }
        static void AssertDifference(float[] a, float[] b, float limit)
        { for (int i = 0; i < a.Length; i++) Assert.That(Mathf.Abs(a[i] - b[i]), Is.LessThan(limit), "motor " + i); }
        [Test]
        public void PlayerRotationAndScaleUseLocalDirectionAndWorldDistance()
        {
            var go = new GameObject();
            try
            {
                go.transform.localScale = new Vector3(3, 2, 8);
                go.transform.rotation = Quaternion.Euler(0, 90, 0);
                Assert.That(HapticDirectionCalculator.TryCalculate(go.transform, Vector3.forward * 7, 0, out var d));
                Assert.That(d.Azimuth, Is.EqualTo(270).Within(.001)); Assert.That(d.Distance, Is.EqualTo(7).Within(.001));
                go.transform.rotation = Quaternion.Euler(30, 60, 20);
                Vector3 world = go.transform.TransformDirection(HapticDirectionCalculator.ToLocal(37, 28, 9));
                Assert.That(HapticDirectionCalculator.TryCalculate(go.transform, world, 0, out d));
                Assert.That(d.Azimuth, Is.EqualTo(37).Within(.001)); Assert.That(d.Elevation, Is.EqualTo(28).Within(.001));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void InvalidNumbersAndCoincidentTargetAreRejected()
        {
            Assert.IsFalse(HapticDirectionCalculator.TryFromLocal(Vector3.zero, 0, out _));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(HapticDirectionCalculator.TryFromLocal(new Vector3(invalid, 1, 1), 0, out _));
                Assert.IsFalse(new HapticSpatialMapper().TryMap(invalid, 0, new float[40], out _));
                Assert.IsFalse(new DistanceIntensityMapper().TryEvaluate(invalid, out _, out _));
                Assert.IsFalse(HapticSpatialMapper.TryQuantize(new float[40], invalid, new int[40]));
            }
            var d = new DistanceIntensityMapper { NearDistance = 3, FarDistance = 3 };
            Assert.IsFalse(d.TryEvaluate(3, out _, out _));
        }
        [Test]
        public void DistanceModesAndCustomCurveRespectLimits()
        {
            var d = new DistanceIntensityMapper { MaximumIntensity = .4f };
            d.IntensityCurve = AnimationCurve.Linear(0, 2, 1, 2);
            d.TryEvaluate(2, out _, out float i); Assert.That(i, Is.EqualTo(.4f));
            d.TryEvaluate(15, out _, out i); Assert.That(i, Is.Zero);
            d.Mode = DistanceMode.Discrete3Level;
            d.TryEvaluate(2, out _, out i); Assert.That(i, Is.EqualTo(.4f));
            d.TryEvaluate(8, out _, out i); Assert.That(i, Is.EqualTo(.2f));
            d.TryEvaluate(14, out _, out i); Assert.That(i, Is.EqualTo(.1f));
            d.TryEvaluate(16, out _, out i); Assert.That(i, Is.Zero);
        }
        [Test]
        public void ControllerStopsOnLostTargetDisableInvalidDataAndFarCutoff()
        {
            var go = new GameObject(); var player = new GameObject(); var enemy = new GameObject();
            try
            {
                var output = go.AddComponent<FakeVestOutput>(); var c = go.AddComponent<HapticThreatController>();
                c.Output = output; c.Player = player.transform; c.Target = enemy.transform; c.EnableSmoothing = false;
                enemy.transform.position = Vector3.forward * 3;
                c.Tick(0); Assert.That(output.Sends, Is.EqualTo(1)); Assert.That(output.Duration, Is.EqualTo(120));
                c.Target = null; c.Tick(.1); Assert.That(c.SmoothedIntensity, Is.Zero); Assert.That(output.Last[9], Is.Zero);
                c.Target = enemy.transform; c.Tick(.2); Assert.That(output.Sends, Is.EqualTo(2));
                enemy.SetActive(false); c.Tick(.3); Assert.That(output.Sends, Is.EqualTo(2));
                c.TestMode = true; c.TestDistance = 3; c.Tick(.4); Assert.That(output.Sends, Is.EqualTo(3));
                c.TestAzimuth = float.NaN; c.Tick(.5); Assert.That(c.SmoothedIntensity, Is.Zero);
                c.TestAzimuth = 0; c.TestDistance = 20; c.Tick(.6); Assert.That(output.Sends, Is.EqualTo(3));
                c.TestDistance = 3; c.Tick(.7); int before = output.Stops;
                c.StopNow(); Assert.That(output.Stops, Is.GreaterThan(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(enemy); }
        }
        [Test]
        public void SmoothingCrossesZeroByShortestArcAndDeadZoneStaysSilent()
        {
            var go = new GameObject();
            try
            {
                var output = go.AddComponent<FakeVestOutput>(); var c = go.AddComponent<HapticThreatController>();
                c.Output = output; c.TestMode = true; c.TestAzimuth = 359; c.TestDistance = 2;
                c.Tick(0); c.TestAzimuth = 1; c.Tick(.1);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, c.SmoothedAzimuth)), Is.LessThanOrEqualTo(1));
                c.EnableSmoothing = false; c.TestDistance = 14.5f; int n = output.Sends; c.Tick(.2);
                Assert.That(output.Sends, Is.EqualTo(n));
                c.EnableHaptics = false; c.TestDistance = 2; c.Tick(.3); Assert.That(output.Sends, Is.EqualTo(n));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void NoDeviceOutputInEditModeIsSafe()
        {
            var go = new GameObject();
            try
            {
                var output = go.AddComponent<BHapticsVestOutput>(); var m = new int[40]; m[0] = 1;
                Assert.IsFalse(output.TrySend(m, 100)); Assert.DoesNotThrow(output.Stop);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
