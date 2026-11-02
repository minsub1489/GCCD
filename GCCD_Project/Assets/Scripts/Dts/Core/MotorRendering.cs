using System;
using System.Collections.Generic;
using System.Linq;
namespace GCCD.Dts
{
    [Serializable] public sealed class Motor { public int Index;public double Bearing;public double Gain=1;public bool PlacementConfirmed; }
    [Serializable] public sealed class MotorProfile
    {
        public string Version="virtual-preview-v1";public string Provenance="Synthetic circular 40-channel preview; NOT an X40 index map";
        public bool PhysicalLayoutVerified;public Motor[] Motors;
        // Not a physical layout: operator must replace every position using actual device tests.
        public static MotorProfile Virtual() {return new MotorProfile{Motors=Enumerable.Range(0,40).Select(i=>new Motor{Index=i,Bearing=i*9}).ToArray()};}
        public void Validate(bool physical)
        {
            if(Motors==null||Motors.Length!=40||Motors.Any(m=>m==null)||Motors.Select(m=>m.Index).Distinct().Count()!=40)throw new ArgumentException("Exactly 40 unique motor entries required.");
            foreach(var m in Motors){Angles.Finite(m.Bearing);Angles.Finite(m.Gain);if(m.Index<0||m.Index>39||m.Gain<=0||m.Gain>4)throw new ArgumentException("Invalid motor index/gain.");}
            if(physical&&(!PhysicalLayoutVerified||Version.StartsWith("virtual",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(Provenance)||Motors.Any(m=>!m.PlacementConfirmed)))throw new ArgumentException("Physical X40 profile is unverified.");
        }
    }
    public static class MotorComposer
    {
        // Circular linear interpolation between adjacent unique angular columns.
        // Motors sharing a bearing share the same weight; actual vertical rows are operator-defined.
        // Apply regional gain, clamp once to the SDK range, then round AwayFromZero to 0..100.
        public static int[] Compose(IEnumerable<Threat> threats,MotorProfile profile,double low,double high)
        {
            if(profile==null||threats==null)throw new ArgumentException("Profile and threats required.");profile.Validate(false);
            DistanceEncoding.Intensity(6,low,high);
            var angles=profile.Motors.Select(m=>Angles.Normalize(m.Bearing)).Distinct().OrderBy(x=>x).ToArray();
            if(angles.Length<2)throw new ArgumentException("At least two angular columns required.");
            var values=new double[40];
            foreach(var t in threats)
            {
                double bearing=t.Bearing;int next=Array.FindIndex(angles,a=>a>=bearing);if(next<0)next=0;
                int previous=(next+angles.Length-1)%angles.Length;double start=angles[previous],span=Angles.Normalize(angles[next]-start),offset=Angles.Normalize(bearing-start);
                double wNext=offset/span;
                foreach(var m in profile.Motors)
                {
                    double a=Angles.Normalize(m.Bearing);double w=a==angles[next]?wNext:a==start?1-wNext:0;
                    double value=DistanceEncoding.Intensity(t.Distance,low,high)*w*m.Gain;
                    values[m.Index]=Math.Max(values[m.Index],Math.Max(0,Math.Min(1,value)));
                }
            }
            return values.Select(v=>(int)Math.Round(Math.Round(100*v,10),MidpointRounding.AwayFromZero)).ToArray();
        }
    }
    public sealed class ConditionOutput {public int Theta;public string[] SelectedIds,RetainedIds,SuppressedIds;public int[] Motors;public int Duration;}
    public sealed class OutputComparison {public ConditionOutput[] Conditions;public int IdenticalPairs;public int PairCount;}
    public static class StimulusValidator
    {
        public static double MaximumPairIdentity(TrialSpec[] plan,MotorProfile profile,double low,double high,int duration)
        {
            if(plan==null||plan.Length==0)throw new ArgumentException("Nonempty stimulus plan required.");var pairs=new int[6,6];
            foreach(var t in plan){var r=Compare(t.Threats,profile,low,high,duration);for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)if(r.Conditions[i].Motors.SequenceEqual(r.Conditions[j].Motors))pairs[i,j]++;}
            int max=0;foreach(int count in pairs)max=Math.Max(max,count);return max/(double)plan.Length;
        }
        public static OutputComparison Compare(Threat[] threats,MotorProfile profile,double low,double high,int duration)
        {
            if(duration<100)throw new ArgumentException("Pulse must be at least 100 ms.");
            var outputs=new List<ConditionOutput>();
            foreach(int theta in ThreatPolicy.Conditions){var r=ThreatPolicy.Apply(threats,theta);outputs.Add(new ConditionOutput{Theta=theta,SelectedIds=r.Selected.Select(t=>t.Id).ToArray(),RetainedIds=r.Kept.Select(t=>t.Id).ToArray(),SuppressedIds=r.Decisions.Where(d=>!d.Retained).Select(d=>d.Threat.Id).ToArray(),Motors=MotorComposer.Compose(r.Kept,profile,low,high),Duration=duration});}
            var result=new OutputComparison{Conditions=outputs.ToArray()};
            for(int i=0;i<6;i++)for(int j=i+1;j<6;j++){result.PairCount++;if(outputs[i].Motors.SequenceEqual(outputs[j].Motors))result.IdenticalPairs++;}
            return result;
        }
    }
}
