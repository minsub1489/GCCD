using System;
using System.Collections.Generic;
using UnityEngine;

namespace GCCD
{
    public enum CueCondition { AudioOnly, AlertOnly, Direction, DirectionAndDistance }
    [Serializable] public struct Trial
    {
        public CueCondition condition;
        public int hemisphere, height, distance;
        public bool occluded;
        public float azimuth;
        public Vector3 Position => Quaternion.Euler(0, azimuth, 0) * new Vector3(0, 0, Radius) + new Vector3(0, 4.5f + height * 3.5f, 0);
        public float Radius => distance == 0 ? 6 : distance == 1 ? 10 : 14;
    }
    public static class ResearchModel
    {
        // Balanced Latin square: each condition precedes every other once across four groups.
        static readonly int[,] Orders = { {0,1,3,2}, {1,2,0,3}, {2,3,1,0}, {3,0,2,1} };
        public static List<Trial> Schedule(int seed, bool practice)
        {
            var rng = new System.Random(seed);
            var result = new List<Trial>();
            int group = (seed % 4 + 4) % 4;
            for (int block = 0; block < 4; block++)
            {
                var trials = new List<Trial>();
                for (int face=0; face<2; face++)
                for (int height=-1; height<=1; height++)
                for (int distance=0; distance<3; distance++)
                for (int wall=0; wall<2; wall++)
                    trials.Add(new Trial { condition=(CueCondition)Orders[group,block], hemisphere=face, height=height, distance=distance, occluded=wall==1,
                        azimuth=(face==0 ? 35 : 145) * (rng.Next(2)==0 ? -1 : 1) });
                for (int i=trials.Count-1; i>0; i--) { int j=rng.Next(i+1); var t=trials[i]; trials[i]=trials[j]; trials[j]=t; }
                result.AddRange(practice ? trials.GetRange(0,3) : trials);
            }
            return result;
        }
        public static int Face(Vector3 direction, Vector3 forward)
        {
            direction.y=0; forward.y=0;
            return Vector3.Dot(direction,forward)>=0 ? 0 : 1;
        }
        public static int Height(Vector3 delta) => delta.y > 1 ? 1 : delta.y < -1 ? -1 : 0;
        public static int Distance(float metres) => metres < 8 ? 0 : metres < 12 ? 1 : 2;
        // Channel order: front upper, front lower, back upper, back lower.
        public static float[] Motors(CueCondition condition, int face, int height, int distance, float pairedGain=0.70f)
        {
            var m = new float[4];
            if(condition==CueCondition.AudioOnly) return m;
            if(condition==CueCondition.AlertOnly) { for(int i=0;i<4;i++) m[i]=0.22f; return m; }
            float a=condition==CueCondition.DirectionAndDistance ? (distance==0 ? 0.85f : distance==1 ? 0.55f : 0.30f) : 0.55f;
            int offset=face*2;
            if(height>=0) m[offset]=a*(height==0 ? pairedGain : 1);
            if(height<=0) m[offset+1]=a*(height==0 ? pairedGain : 1);
            return m;
        }
        public static void SelfTest()
        {
            for(int seed=0;seed<4;seed++) {
                var s=Schedule(seed,false);
                if(s.Count!=144) throw new Exception("Schedule length");
                var keys=new HashSet<string>();
                foreach(var t in s) keys.Add($"{t.condition}/{t.hemisphere}/{t.height}/{t.distance}/{t.occluded}");
                if(keys.Count!=144) throw new Exception("Factorial balance");
                var again=Schedule(seed,false);
                for(int i=0;i<s.Count;i++) if(s[i].azimuth!=again[i].azimuth || s[i].condition!=again[i].condition) throw new Exception("Seed reproducibility");
            }
            if(Schedule(42,true).Count!=12) throw new Exception("Practice length");
            if(Face(Vector3.back,Vector3.forward)!=1 || Height(new Vector3(0,3,1))!=1) throw new Exception("Spatial classification");
            var both=Motors(CueCondition.Direction,1,0,0);
            if(both[0]!=0 || both[1]!=0 || both[2]<=0 || both[2]!=both[3]) throw new Exception("Same level mapping");
            foreach(CueCondition c in Enum.GetValues(typeof(CueCondition)))
            for(int f=0;f<2;f++) for(int h=-1;h<=1;h++) for(int d=0;d<3;d++) {
                var m=Motors(c,f,h,d);
                foreach(float v in m) if(v<0 || v>1) throw new Exception("Motor bounds");
                if(c==CueCondition.AudioOnly && Array.Exists(m,v=>v!=0)) throw new Exception("Audio only leak");
            }
            if(Distance(7.99f)!=0 || Distance(8)!=1 || Distance(12)!=2) throw new Exception("Distance thresholds");
        }
    }
}
