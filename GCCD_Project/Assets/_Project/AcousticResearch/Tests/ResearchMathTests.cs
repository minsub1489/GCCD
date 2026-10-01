using System;
using System.Collections.Generic;
using System.Linq;
using GCCD.ContinuousHaptics;
using NUnit.Framework;
using UnityEngine;

namespace GCCD.AcousticResearch.Tests
{
    public sealed class ResearchMathTests
    {
        [TestCase(ExperimentCondition.None,false,false)]
        [TestCase(ExperimentCondition.AudioOnly,true,false)]
        [TestCase(ExperimentCondition.HapticsOnly,false,true)]
        [TestCase(ExperimentCondition.AudioAndHaptics,true,true)]
        public void ChannelsAreIndependent(ExperimentCondition c,bool audio,bool haptics)
        { Assert.AreEqual(audio,ConditionChannels.Audio(c));Assert.AreEqual(haptics,ConditionChannels.Haptics(c)); }
        [TestCase(0)] [TestCase(1489)] [TestCase(-9)] [TestCase(77)]
        public void PlansReproduceAndCoverBothPoles(int seed)
        {
            var a=ExperimentTrialPlan.Create(seed);var b=ExperimentTrialPlan.Create(seed);
            CollectionAssert.AreEqual(a,b);Assert.IsTrue(a.Any(x=>x.Elevation==90 && !x.CatchTrial));Assert.IsTrue(a.Any(x=>x.Elevation==-90 && !x.CatchTrial));
            foreach(var t in a) { Assert.IsFalse(ExperimentTrialPlan.InInitialView(t.Azimuth,t.Elevation));Assert.That(t.Foreperiod,Is.InRange(1.5,3.5));Assert.IsTrue(t.Distance==6f || t.Distance==10f); }
            Assert.AreEqual(4,a.Count(x=>x.CatchTrial));
            foreach(var group in a.Where(x=>!x.CatchTrial).GroupBy(x=>x.Category))
            { Assert.AreEqual(5,group.Count());Assert.IsTrue(group.Any(x=>x.Masked));Assert.IsTrue(group.Any(x=>!x.Masked));Assert.IsTrue(group.Any(x=>x.Distance==6));Assert.IsTrue(group.Any(x=>x.Distance==10)); }
        }
        [Test] public void OrdersBalanceEachPositionAndAllDirectedCarryoverPairs()
        {
            var orders=Enumerable.Range(0,4).Select(ExperimentTrialPlan.ConditionOrder).ToArray();
            for(int j=0;j<4;j++) Assert.AreEqual(4,orders.Select(x=>x[j]).Distinct().Count());
            var pairs=new HashSet<string>();foreach(var order in orders)for(int j=0;j<3;j++)pairs.Add(order[j]+":"+order[j+1]);Assert.AreEqual(12,pairs.Count);
        }
        [Test] public void MaskingPolicyRejectsNonThreatsAndVisibleSources()
        {
            var p=ScriptableObject.CreateInstance<SoundCueProfile>();p.IsThreat=true;p.Risk=.8f;p.Urgency=.8f;
            var policy=new ThreatSelectionPolicy();
            Assert.IsFalse(policy.Eligible(false,p,true));Assert.IsFalse(policy.Eligible(true,p,false));Assert.IsTrue(policy.Eligible(true,p,true));
            p.IsThreat=false;Assert.IsFalse(policy.Eligible(true,p,true));p.IsThreat=true;p.Risk=1;Assert.IsTrue(policy.Eligible(true,p,false));
            UnityEngine.Object.DestroyImmediate(p);
        }
        [Test] public void MaskingEstimateWeightsSpectralOverlap()
        { Assert.Greater(ThreatSelectionPolicy.BandOverlap(SoundBand.Low,SoundBand.Low),ThreatSelectionPolicy.BandOverlap(SoundBand.Low,SoundBand.High));Assert.Less(ThreatSelectionPolicy.Snr(.1f,1),0); }
        [Test] public void SimplexMatchesKnownOptimum()
        {
            var solver=new StoLinearProgram(new double[,]{{1,0},{0,1},{1,1}},new double[]{2,3,4},new double[]{3,2});
            Assert.IsTrue(solver.Solve(out var s,out var v));Assert.That(v,Is.EqualTo(10).Within(1e-6));Assert.That(s[0],Is.EqualTo(2).Within(1e-6));Assert.That(s[1],Is.EqualTo(2).Within(1e-6));
        }
        [Test] public void SimplexHandlesNegativeRhsAndInfeasibility()
        {
            Assert.IsTrue(new StoLinearProgram(new double[,]{{-1},{1}},new double[]{-2,3},new double[]{-1}).Solve(out var s,out var v));Assert.That(s[0],Is.EqualTo(2).Within(1e-6));
            Assert.IsFalse(new StoLinearProgram(new double[,]{{-1},{1}},new double[]{-2,1},new double[]{1}).Solve(out _,out _));
        }
        [Test] public void SimplexRejectsUnboundedProblem()
        { Assert.IsFalse(new StoLinearProgram(new double[,]{{-1}},new double[]{0},new double[]{1}).Solve(out _,out _)); }
        [TestCase(135f,157.5f)] [TestCase(350f,12f)]
        public void StoReturnsFeasibleSeparatePeaksOrExplicitTimeFallback(float first,float second)
        {
            var optimizer=new StoTorsoOptimizer {Enabled=true,TimeBudgetMilliseconds=30};
            var cues=new List<AcousticHapticsRouter.CueDecision>{new AcousticHapticsRouter.CueDecision {Selected=true,Direction=new HapticDirection(first,0,8),Intensity=.2f},new AcousticHapticsRouter.CueDecision {Selected=true,Direction=new HapticDirection(second,0,8),Intensity=.2f}};
            var result=new float[40];bool solved=optimizer.TryOptimize(cues,new HapticSpatialMapper(),.5f,result);
            if(!solved) { StringAssert.Contains("direct mapping",optimizer.Status);return; }
            Assert.Greater(optimizer.FeasibleAssignments,0);Assert.AreEqual(2,optimizer.RenderedAngles.Length);
            Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(first,optimizer.RenderedAngles[0])),30.001);
            Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(second,optimizer.RenderedAngles[1])),30.001);
            Assert.AreNotEqual(optimizer.RenderedAngles[0],optimizer.RenderedAngles[1]);Assert.LessOrEqual(result.Sum(),.50001);Assert.IsTrue(result.All(x=>x>=0 && HapticMath.Finite(x)));
        }
        [Test] public void StoKnownSeparatedPairMustSolve()
        {
            var optimizer=new StoTorsoOptimizer {Enabled=true,TimeBudgetMilliseconds=30,CandidateRadiusDegrees=0};
            var cues=new List<AcousticHapticsRouter.CueDecision>{new AcousticHapticsRouter.CueDecision {Selected=true,Direction=new HapticDirection(90,0,8),Intensity=.2f},new AcousticHapticsRouter.CueDecision {Selected=true,Direction=new HapticDirection(270,0,8),Intensity=.2f}};
            var result=new float[40];Assert.IsTrue(optimizer.TryOptimize(cues,new HapticSpatialMapper(),.5f,result),optimizer.Status);
            CollectionAssert.AreEqual(new[]{90f,270f},optimizer.RenderedAngles);Assert.Greater(result.Sum(),0);Assert.LessOrEqual(result.Sum(),.50001);
        }
        [Test] public void StoPolesRemainDirect3DMapping()
        {
            var optimizer=new StoTorsoOptimizer {Enabled=true};var cues=new List<AcousticHapticsRouter.CueDecision>{new AcousticHapticsRouter.CueDecision {Selected=true,Direction=new HapticDirection(0,90,6)},new AcousticHapticsRouter.CueDecision {Selected=true,Direction=new HapticDirection(90,0,6)}};
            Assert.IsFalse(optimizer.TryOptimize(cues,new HapticSpatialMapper(),.5f,new float[40]));StringAssert.Contains("Pole",optimizer.Status);
        }
    }
}
