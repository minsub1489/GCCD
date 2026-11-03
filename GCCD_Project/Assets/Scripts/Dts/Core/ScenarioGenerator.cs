using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace GCCD.Dts
{
    // Explicit xorshift32 gives stable sequences across Mono/.NET implementations.
    public sealed class StableRandom
    {
        uint state;public StableRandom(int seed){state=(uint)seed;if(state==0)state=0x9e3779b9;}
        public uint Next(){state^=state<<13;state^=state>>17;state^=state<<5;return state;}
        public double Unit(){return Next()/4294967296.0;}
        public void Shuffle<T>(T[] a){for(int i=a.Length-1;i>0;i--){int j=(int)(Next()%(uint)(i+1));T temp=a[i];a[i]=a[j];a[j]=temp;}}
    }
    public sealed class TrialSpec
    {
        public string Id;public int Theta,Block,Family,Seed;public Threat[] Threats;public double Preparation;
        public string Signature {get{return Id+":"+Theta+":"+string.Join(";",Threats.Select(t=>t.Bearing.ToString("R",CultureInfo.InvariantCulture)+","+t.Distance.ToString("R",CultureInfo.InvariantCulture)));}}
    }
    public static class ConditionScheduler
    {
        static readonly int[][] orders={new[]{0,15,90,30,60,45},new[]{15,30,0,45,90,60},new[]{30,45,15,60,0,90},new[]{45,60,30,90,15,0},new[]{60,90,45,0,30,15},new[]{90,0,60,15,45,30}};
        public static int[] Order(int row){if(row<0||row>5)throw new ArgumentException("Counterbalance row must be 0..5.");return (int[])orders[row].Clone();}
    }
    public static class ScenarioGenerator
    {
        public static TrialSpec[] PlanA(int seed, int row, double hfov, int threatCount)
        {
            Angles.IsOffscreen(180, hfov);
            if (threatCount < 2 || threatCount > ExperimentConfig.MaximumThreatCount)
                throw new ArgumentOutOfRangeException(nameof(threatCount));

            var order = ConditionScheduler.Order(row);
            var layoutRng = new StableRandom(seed);
            var layouts = new Threat[18][];
            double start = hfov / 2 + 6;
            for (int family = 0; family < layouts.Length; family++)
            {
                var threats = new Threat[threatCount];
                for (int i = 0; i < threats.Length; i++)
                {
                    double bearing = start + (360 - 2 * start) * layoutRng.Unit();
                    double distance = 6.4 + 28 * layoutRng.Unit();
                    double radians = bearing * Math.PI / 180;
                    threats[i] = new Threat("E" + (i + 1), true, true, true,
                        Math.Sin(radians) * distance, 1.7, Math.Cos(radians) * distance,
                        distance, bearing);
                }
                layouts[family] = threats;
            }

            var result = new List<TrialSpec>();
            for (int block = 0; block < order.Length; block++)
            {
                var rng = new StableRandom(unchecked(seed + block * 65537));
                var families = Enumerable.Range(0, layouts.Length).ToArray();
                rng.Shuffle(families);
                foreach (int family in families)
                {
                    result.Add(new TrialSpec
                    {
                        Id = "A" + (result.Count + 1).ToString("D3"),
                        Theta = order[block],
                        Block = block,
                        Family = family,
                        Seed = seed,
                        Preparation = 2 + 2 * rng.Unit(),
                        Threats = (Threat[])layouts[family].Clone()
                    });
                }
            }
            return result.ToArray();
        }
    }
}
