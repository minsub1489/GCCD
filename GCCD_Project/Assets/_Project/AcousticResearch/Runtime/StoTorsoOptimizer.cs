using System;
using System.Collections.Generic;
using System.Diagnostics;
using GCCD.ContinuousHaptics;
using UnityEngine;

namespace GCCD.AcousticResearch
{
    [Serializable]
    public sealed class StoTorsoOptimizer
    {
        public bool Enabled;
        [Tooltip("Only mark after measuring this X40 model with the study's participants.")]
        public bool X40CalibrationVerified;
        [Range(.1f,2)] public float StevensExponent=.8f;
        [Range(5,50)] public float GaussianSpreadDegrees=18;
        [Range(0,30)] public float CandidateRadiusDegrees=30;
        [Range(10,120)] public float ClosePairDegrees=90;
        public float AngleWeight=.5f, IntensityWeight=.5f, SeparationWeight=10, EnergyWeight=.05f;
        [Range(1,30)] public float TimeBudgetMilliseconds=8;
        // Optional measured primitive parameters. Empty arrays use a clearly provisional analytic model.
        public float[] PrimitiveMeanOffsets=new float[0], PrimitiveSpreads=new float[0];
        public string Status { get; private set; }="STO X40 adaptation / uncalibrated";
        public float SolveMilliseconds { get; private set; }
        public int FeasibleAssignments { get; private set; }
        public float[] RenderedAngles { get; private set; }=new float[0];
        const int PrimitiveCount=16, PointCount=60;
        public bool TryOptimize(List<AcousticHapticsRouter.CueDecision> cues,HapticSpatialMapper spatial,float budget,float[] result)
        {
            SolveMilliseconds=0; FeasibleAssignments=0; RenderedAngles=new float[0];
            if(result==null || cues==null || spatial==null || !spatial.IsValid || !HapticMath.Finite(budget) || budget<=0) return false;
            Array.Clear(result,0,result.Length); var targets=cues.FindAll(d=>d.Selected);
            if(!Enabled || targets.Count<2 || targets.Count>3) { Status="Single target / direct 3D mapping"; return false; }
            if(result.Length!=40 || StevensExponent<=0 || !HapticMath.Finite(StevensExponent) || !HapticMath.Finite(GaussianSpreadDegrees)
                || GaussianSpreadDegrees<5 || GaussianSpreadDegrees>50 || !HapticMath.Finite(CandidateRadiusDegrees)
                || CandidateRadiusDegrees<0 || CandidateRadiusDegrees>30 || !HapticMath.Finite(TimeBudgetMilliseconds)
                || TimeBudgetMilliseconds<1 || TimeBudgetMilliseconds>30 || !HapticMath.Finite(ClosePairDegrees) || ClosePairDegrees<10 || ClosePairDegrees>120 || !Positive(AngleWeight) || !Positive(IntensityWeight) || !Positive(SeparationWeight) || !Positive(EnergyWeight))
            { Status="Invalid STO settings; direct mapping";return false; }
            foreach(var d in targets) if(Mathf.Abs(d.Direction.Elevation)>80) { Status="Pole: direct 3D mapping";return false; }
            int k=targets.Count,n=PrimitiveCount*k;
            var watch=Stopwatch.StartNew();FeasibleAssignments=0;
            var p=new double[PointCount,n]; var a=new double[40,n]; var columnEnergy=new double[n];
            float[] temp=new float[40];
            for(int target=0;target<k;target++) for(int primitive=0;primitive<PrimitiveCount;primitive++)
            {
                int col=target*PrimitiveCount+primitive; float center=primitive*22.5f;
                float offset=PrimitiveMeanOffsets!=null && PrimitiveMeanOffsets.Length==16?PrimitiveMeanOffsets[primitive]:0;
                float sigma=PrimitiveSpreads!=null && PrimitiveSpreads.Length==16?PrimitiveSpreads[primitive]:GaussianSpreadDegrees;
                if(!HapticMath.Finite(offset) || !HapticMath.Finite(sigma) || sigma<1) { Status="Invalid calibration";return false; }
                for(int point=0;point<PointCount;point++) { float d=Mathf.DeltaAngle(center+offset,point*6);p[point,col]=Math.Exp(-d*d/(2*sigma*sigma)); }
                spatial.TryMap(center,targets[target].Direction.Elevation,temp,out _);
                for(int motor=0;motor<40;motor++) { a[motor,col]=temp[motor];columnEnergy[col]+=temp[motor]; }
            }
            var choices=new List<int>[k];
            for(int i=0;i<k;i++)
            {
                choices[i]=new List<int>();
                for(int point=0;point<PointCount;point++) if(Mathf.Abs(Mathf.DeltaAngle(targets[i].Direction.Azimuth,point*6))<=CandidateRadiusDegrees+.001f) choices[i].Add(point);
                int target=i; choices[i].Sort((x,y)=>Mathf.Abs(Mathf.DeltaAngle(targets[target].Direction.Azimuth,x*6)).CompareTo(Mathf.Abs(Mathf.DeltaAngle(targets[target].Direction.Azimuth,y*6))));
            }
            double best=double.PositiveInfinity; double[] bestS=null; int[] bestPeaks=null; bool timedOut=false;
            int[] peaks=new int[k];
            void Search(int depth)
            {
                if(watch.Elapsed.TotalMilliseconds>TimeBudgetMilliseconds) { timedOut=true;return; }
                if(depth<k)
                { foreach(int point in choices[depth]) { peaks[depth]=point;Search(depth+1);if(timedOut)return; } return; }
                double fixedCost=0; float separation=180;bool close=false;
                for(int i=0;i<k;i++)
                {
                    fixedCost+=AngleWeight*Math.Abs(Mathf.DeltaAngle(targets[i].Direction.Azimuth,peaks[i]*6))/k;
                    for(int j=0;j<i;j++)
                    {
                        if(peaks[i]==peaks[j]) return;
                        if(Mathf.Abs(Mathf.DeltaAngle(targets[i].Direction.Azimuth,targets[j].Direction.Azimuth))<=ClosePairDegrees)
                        { close=true; separation=Mathf.Min(separation,Mathf.Abs(Mathf.DeltaAngle(peaks[i]*6,peaks[j]*6))); }
                    }
                }
                if(close) fixedCost-=SeparationWeight*separation;
                if(fixedCost>=best) return; // Continuous costs are nonnegative: valid lower bound.
                int variableCount=n+k; var constraints=new List<double[]>();var rhs=new List<double>();
                void Add(double[] coefficients,double b) { constraints.Add(coefficients);rhs.Add(b); }
                for(int col=0;col<n;col++) { var r=new double[variableCount];r[col]=1;Add(r,1); }
                var energy=new double[variableCount];for(int col=0;col<n;col++) energy[col]=columnEnergy[col];Add(energy,budget);
                for(int target=0;target<k;target++)
                {
                    double desired=Math.Pow(targets[target].Intensity,1/StevensExponent);
                    var positive=new double[variableCount];var negative=new double[variableCount];
                    for(int col=0;col<n;col++) { positive[col]=p[peaks[target],col];negative[col]=-positive[col]; }
                    positive[n+target]=negative[n+target]=-1;Add(positive,desired);Add(negative,-desired);
                    // Assigned locations must be strict local maxima in P*s.
                    foreach(int neighbor in new[]{(peaks[target]+PointCount-1)%PointCount,(peaks[target]+1)%PointCount})
                    { var r=new double[variableCount];for(int col=0;col<n;col++) r[col]=p[neighbor,col]-p[peaks[target],col];Add(r,-.00001); }
                }
                var matrix=new double[constraints.Count,variableCount];
                for(int i=0;i<constraints.Count;i++) for(int j=0;j<variableCount;j++) matrix[i,j]=constraints[i][j];
                var objective=new double[variableCount];for(int col=0;col<n;col++) objective[col]=-EnergyWeight*columnEnergy[col]/40;
                for(int i=0;i<k;i++) objective[n+i]=-IntensityWeight/k;
                if(!new StoLinearProgram(matrix,rhs.ToArray(),objective).Solve(out var s,out double value)) return;
                FeasibleAssignments++; double loss=fixedCost-value;
                if(loss<best) { best=loss;bestS=s;bestPeaks=(int[])peaks.Clone(); }
            }
            Search(0);SolveMilliseconds=(float)watch.Elapsed.TotalMilliseconds;
            if(bestS==null) { Status=timedOut?"STO time budget exhausted; direct mapping":"STO infeasible; direct mapping";return false; }
            for(int motor=0;motor<40;motor++) for(int col=0;col<n;col++) result[motor]+=(float)(a[motor,col]*bestS[col]);
            RenderedAngles=new float[k];for(int i=0;i<k;i++) RenderedAngles[i]=bestPeaks[i]*6;
            Status=(X40CalibrationVerified?"STO X40 calibrated":"STO X40 analytic / uncalibrated")+(timedOut?"; time-limited feasible solution":"; enumerated optimum");
            return true;
        }
        static bool Positive(float x)=>HapticMath.Finite(x)&&x>=0;
    }
}
