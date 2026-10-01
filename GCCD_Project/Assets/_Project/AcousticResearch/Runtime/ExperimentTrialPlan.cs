using System;
using System.Collections.Generic;
using UnityEngine;

namespace GCCD.AcousticResearch
{
    [Serializable]
    public struct ResearchTrial
    {
        public SoundCategory Category;
        public float Azimuth, Elevation, Distance, Foreperiod;
        public bool CatchTrial, Masked;
    }
    public static class ExperimentTrialPlan
    {
        public static List<ResearchTrial> Create(int seed, int count=24)
        {
            if(count<1 || count>256) throw new ArgumentOutOfRangeException(nameof(count));
            int targetCount=count-count/6;
            var rng=new System.Random(seed); var result=new List<ResearchTrial>();
            var categories=new[]{SoundCategory.Gunshot,SoundCategory.Footsteps,SoundCategory.Vehicle,SoundCategory.Door};
            for(int i=0;i<count;i++)
            {
                float azimuth,elevation;
                // Uniform spherical sampling, excluding the initial 70-degree 16:9 frustum.
                do { azimuth=(float)rng.NextDouble()*360; elevation=Mathf.Asin((float)rng.NextDouble()*2-1)*Mathf.Rad2Deg; }
                while(InInitialView(azimuth,elevation));
                if(i==0) { azimuth=0;elevation=90; }
                if(i==1) { azimuth=0;elevation=-90; }
                result.Add(new ResearchTrial {
                    Category=categories[i%categories.Length], Azimuth=azimuth,Elevation=elevation, Distance=(i/8+i%2)%2==0?6:10,
                    Foreperiod=1.5f+(float)rng.NextDouble()*2, CatchTrial=i>=targetCount, Masked=(i/4+i%2)%2==0 });
            }
            for(int i=result.Count-1;i>0;i--) { int j=rng.Next(i+1); var t=result[i];result[i]=result[j];result[j]=t; }
            return result;
        }
        public static bool InInitialView(float azimuth,float elevation)
        {
            var v=GCCD.ContinuousHaptics.HapticDirectionCalculator.ToLocal(azimuth,elevation,1);
            float tangent=Mathf.Tan(35*Mathf.Deg2Rad);
            return v.z>0 && Mathf.Abs(v.x/v.z)<=tangent*16/9 && Mathf.Abs(v.y/v.z)<=tangent;
        }
        // Balanced Williams orders for four conditions; index is a participant code, not random reroll.
        public static ExperimentCondition[] ConditionOrder(int index)
        {
            int[,] orders={{0,1,3,2},{1,2,0,3},{2,3,1,0},{3,0,2,1}};
            var result=new ExperimentCondition[4]; int row=((index%4)+4)%4;
            for(int i=0;i<4;i++) result[i]=(ExperimentCondition)orders[row,i]; return result;
        }
    }
}
