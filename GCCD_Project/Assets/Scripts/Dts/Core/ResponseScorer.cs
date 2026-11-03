using System;
using System.Collections.Generic;
using System.Linq;
namespace GCCD.Dts
{
    public sealed class Response
    {
        public double Bearing {get;private set;}public DistanceBin Bin {get;private set;}
        public Response(double bearing,DistanceBin bin){if(!Enum.IsDefined(typeof(DistanceBin),bin))throw new ArgumentException("Unknown distance bin.");Bearing=Angles.Normalize(bearing);Bin=bin;}
    }
    public sealed class Score
    {
        public double JointAccuracy,DirectionAccuracy,DistanceAccuracy,FalseResponseRate,NoResponseRate;
        public double? Precision,DirectionError;
        public bool Ambiguous;public int[] MatchedThreatIndices;public int[,] Confusion=new int[3,3];
    }
    public static class ResponseScorer
    {
        private sealed class Candidate {public int[] Match;public int Joint,Direction,Distance;public double Error;}
        public static Score Score(Threat[] original,Response[] responses,double tolerance)
        {
            Angles.Finite(tolerance);if(original==null||original.Length<1||original.Length>ExperimentConfig.MaximumThreatCount||responses==null||responses.Length>original.Length||tolerance<0||tolerance>180)throw new ArgumentException("Valid original threat count, responses up to that count and fixed tolerance required.");
            var candidates=new List<Candidate>();Enumerate(original,responses,tolerance,0,new bool[original.Length],new int[responses.Length],candidates);
            // Joint objective, then total angular error over maximal-cardinality assignments.
            // Final lexicographic key resolves numerical ties deterministically.
            int bestJoint=candidates.Max(c=>c.Joint);
            var best=candidates.Where(c=>c.Joint==bestJoint).OrderBy(c=>c.Error).ThenBy(c=>string.Join(",",c.Match)).First();
            var score=new Score{JointAccuracy=best.Joint/(double)original.Length,DirectionAccuracy=candidates.Max(c=>c.Direction)/(double)original.Length,DistanceAccuracy=candidates.Max(c=>c.Distance)/(double)original.Length,Precision=responses.Length==0?(double?)null:best.Joint/(double)responses.Length,FalseResponseRate=responses.Length==0?0:(responses.Length-best.Joint)/(double)responses.Length,NoResponseRate=responses.Length==0?1:0,DirectionError=responses.Length==0?(double?)null:best.Error/responses.Length,MatchedThreatIndices=best.Match};
            // Ambiguity means multiple maximum-joint assignments involving overlapping eligible pairs,
            // even when the angular-error tie breaker chooses a unique assignment.
            score.Ambiguous=bestJoint>0&&candidates.Where(c=>c.Joint==bestJoint).Select(c=>string.Join(";",Enumerable.Range(0,responses.Length).Where(i=>Angles.Difference(original[c.Match[i]].Bearing,responses[i].Bearing)<=tolerance&&DistanceEncoding.Bin(original[c.Match[i]].Distance)==responses[i].Bin).Select(i=>i+":"+c.Match[i]))).Distinct().Count()>1;
            for(int i=0;i<responses.Length;i++)score.Confusion[(int)DistanceEncoding.Bin(original[best.Match[i]].Distance),(int)responses[i].Bin]++;
            return score;
        }
        static void Enumerate(Threat[] t,Response[] r,double tolerance,int i,bool[] used,int[] match,List<Candidate> output)
        {
            if(i==r.Length){var c=new Candidate{Match=(int[])match.Clone()};for(int k=0;k<r.Length;k++){double e=Angles.Difference(t[match[k]].Bearing,r[k].Bearing);bool direction=e<=tolerance,dist=DistanceEncoding.Bin(t[match[k]].Distance)==r[k].Bin;c.Error+=e;if(direction)c.Direction++;if(dist)c.Distance++;if(direction&&dist)c.Joint++;}output.Add(c);return;}
            for(int j=0;j<t.Length;j++)if(!used[j]){used[j]=true;match[i]=j;Enumerate(t,r,tolerance,i+1,used,match,output);used[j]=false;}
        }
    }
}
