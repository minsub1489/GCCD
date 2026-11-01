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
        static readonly double[] edges={0,15,30,45,60,90,120};
        public static TrialSpec[] PlanA(int seed,int row,double hfov)
        {
            Angles.IsOffscreen(180,hfov);var order=ConditionScheduler.Order(row);var result=new List<TrialSpec>();
            for(int block=0;block<6;block++)
            {
                var trials=new TrialSpec[18];for(int f=0;f<18;f++)
                {
                    var rng=new StableRandom(unchecked(seed+f*7919));int bin=f/3;
                    double gap=edges[bin]+(edges[bin+1]-edges[bin])*(.15+.7*rng.Unit());
                    double[] relative={0,gap,180+gap/2};
                    double[] distances=Distances(f,rng);
                    // Cycle which angular position receives the nearest/isolated rank.
                    double[] assigned=Enumerable.Range(0,3).Select(i=>distances[(i+f+block)%3]).ToArray();
                    var rotationRng=new StableRandom(unchecked(seed+f*7919+block*104729));
                    trials[f]=new TrialSpec{Theta=order[block],Block=block,Family=f,Seed=seed,Preparation=2+2*rotationRng.Unit(),Threats=RotateOffscreen(relative,assigned,rotationRng,hfov,(block+f)%2==0)};
                }
                new StableRandom(unchecked(seed+block*65537)).Shuffle(trials);
                for(int i=0;i<18;i++){trials[i].Id="A"+(block*18+i+1).ToString("D3");result.Add(trials[i]);}
            }
            return result.ToArray();
        }
        static double[] Distances(int family,StableRandom rng)
        {
            // Equal global distance distributions across theta. Includes 2-3 threats in one bin.
            double[] starts={6.4,12.4,24.4};double[] widths={4.5,10,10};int type=family%6;
            int[] bins=type<3?new[]{type,type,type}:type==3?new[]{0,0,2}:type==4?new[]{1,1,2}:new[]{0,1,2};
            var d=bins.Select((b,i)=>starts[b]+widths[b]*(.05+.85*rng.Unit())+i*.03).OrderBy(x=>x).ToArray();return d;
        }
        static Threat[] RotateOffscreen(double[] relative,double[] distances,StableRandom rng,double hfov,bool mirror)
        {
            for(int attempt=0;attempt<10000;attempt++)
            {
                double rotation=360*rng.Unit();double[] bearings=relative.Select(a=>Angles.Normalize(rotation+(mirror?-a:a))).ToArray();
                // Margin covers target angular extent; actual viewport geometry is checked at event time.
                if(bearings.Any(a=>Angles.Difference(a,0)<=hfov/2+6))continue;
                return bearings.Select((a,i)=>{double rad=a*Math.PI/180;return new Threat("E"+(i+1),true,true,true,Math.Sin(rad)*distances[i],1.7,Math.Cos(rad)*distances[i],distances[i],a);}).ToArray();
            }
            throw new ArgumentException("Unable to construct offscreen scene at requested FOV.");
        }
        public static TrialSpec[] PlanB(int seed,int row,double hfov,int candidate,int eventsPerCondition=12)
        {
            if(!ThreatPolicy.Conditions.Contains(candidate)||candidate==0||row<0||row>1||eventsPerCondition<1)throw new ArgumentException("B needs a nonzero preregistered candidate and two-way order.");Angles.IsOffscreen(180,hfov);
            int[] order=row==0?new[]{0,candidate}:new[]{candidate,0};var result=new List<TrialSpec>();
            for(int b=0;b<2;b++)for(int f=0;f<eventsPerCondition;f++)
            {
                var r=new StableRandom(unchecked(seed+f*19937));double available=360-hfov-20;
                var ts=Enumerable.Range(0,5).Select(i=>{double a=hfov/2+10+available*r.Unit();double d=6.5+28*r.Unit()+i*.01;double rad=a*Math.PI/180;return new Threat("E"+(i+1),true,true,true,Math.Sin(rad)*d,1.7,Math.Cos(rad)*d,d,a);}).ToArray();
                result.Add(new TrialSpec{Id="B"+(result.Count+1).ToString("D3"),Theta=order[b],Block=b,Family=f,Seed=seed,Threats=ts,Preparation=2+2*r.Unit()});
            }
            return result.ToArray();
        }
    }
}
