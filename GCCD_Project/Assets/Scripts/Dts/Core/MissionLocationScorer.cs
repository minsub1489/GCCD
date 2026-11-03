using System;
using System.Linq;
using System.Collections.Generic;
namespace GCCD.Dts
{
    public sealed class MissionLocationScore
    {
        public int SourceCount,ResponseCount,Correct;
        public int FarFalseMarks;
        public int AdjustedCorrect => Math.Max(0, Correct - FarFalseMarks);
        public double? AdjustedRecall => SourceCount==0?(double?)null:AdjustedCorrect/(double)SourceCount;
        public bool[] CorrectMarks, FarFalseMark, DuplicateMarks;
        public double?[] NearestDirectionErrors;
        public int Missed {get{return SourceCount-Correct;}}
        public int FalseMarks {get{return ResponseCount-Correct;}}
        public double? Recall {get{return SourceCount==0?(double?)null:Correct/(double)SourceCount;}}
        public double? Precision {get{return ResponseCount==0?(double?)null:Correct/(double)ResponseCount;}}
        public double? F1 {get{return SourceCount+ResponseCount==0?(double?)null:2.0*Correct/(SourceCount+ResponseCount);}}
        public int[] ResponseToSource;
        public double?[] PositionErrors,DirectionErrors,RangeErrors;
        public double? MeanPositionError {get{return Mean(PositionErrors);}}
        public double? MeanDirectionError {get{return Mean(DirectionErrors);}}
        public double? MeanRangeError {get{return Mean(RangeErrors);}}
        static double? Mean(double?[] values){var valid=values.Where(v=>v.HasValue).Select(v=>v.Value).ToArray();return valid.Length==0?(double?)null:valid.Average();}
    }
    public static class MissionLocationScorer
    {
        sealed class Assignment {public int Correct;public double Error;public int[] Map;}
        public static MissionLocationScore Score(MissionSpawnPoint[] sources,MissionSpawnPoint[] marks,double observerX,double observerZ,double yaw,double tolerance,double? directionTolerance=null)
        {
            if(sources==null||sources.Length<1||sources.Length>MissionSpawnPlanner.MaximumThreatCount||marks==null)throw new ArgumentException("Source and response positions required.");
            Angles.Finite(observerX);Angles.Finite(observerZ);Angles.Finite(yaw);Angles.Finite(tolerance);if(tolerance<0)throw new ArgumentException("Nonnegative tolerance required.");
            foreach(var p in sources.Concat(marks)){Angles.Finite(p.X);Angles.Finite(p.Z);}
            if(directionTolerance.HasValue)
            {
                Angles.Finite(directionTolerance.Value);
                if(directionTolerance.Value<=0||directionTolerance.Value>180)
                    throw new ArgumentException("Direction tolerance must be greater than 0 and at most 180 degrees.");
            }
            var bearings=sources.Select(point=>Bearing(point,observerX,observerZ,yaw)).ToArray();
            var position=new double[marks.Length,sources.Length];
            var direction=new double?[marks.Length,sources.Length];
            var correct=new bool[marks.Length,sources.Length];
            var nearest=new double?[marks.Length];
            var far=new bool[marks.Length];
            for(int i=0;i<marks.Length;i++)
            {
                var markBearing=Bearing(marks[i],observerX,observerZ,yaw);
                for(int j=0;j<sources.Length;j++)
                {
                    position[i,j]=Distance(sources[j],marks[i]);
                    if(markBearing.HasValue&&bearings[j].HasValue)
                    {
                        double error=Angles.Difference(markBearing.Value,bearings[j].Value);
                        direction[i,j]=error;
                        if(!nearest[i].HasValue||error<nearest[i].Value)nearest[i]=error;
                    }
                    correct[i,j]=position[i,j]<=tolerance &&
                        (!directionTolerance.HasValue || (direction[i,j].HasValue && direction[i,j].Value<directionTolerance.Value));
                }
                far[i]=directionTolerance.HasValue && (!nearest[i].HasValue || nearest[i].Value>=directionTolerance.Value);
            }

            var states=new Dictionary<int,Assignment>{{0,new Assignment{Map=Enumerable.Repeat(-1,marks.Length).ToArray()}}};
            for(int i=0;i<marks.Length;i++){
                var next=new Dictionary<int,Assignment>(states);
                foreach(var item in states.OrderBy(x=>x.Key))for(int j=0;j<sources.Length;j++)if((item.Key&(1<<j))==0){
                    double error=position[i,j];int mask=item.Key|(1<<j);
                    var candidate=new Assignment{Correct=item.Value.Correct+(correct[i,j]?1:0),Error=item.Value.Error+error,Map=(int[])item.Value.Map.Clone()};candidate.Map[i]=j;
                    Assignment old;if(!next.TryGetValue(mask,out old)||candidate.Correct>old.Correct||(candidate.Correct==old.Correct&&candidate.Error<old.Error))next[mask]=candidate;
                }
                states=next;
            }
            var best=states.OrderByDescending(x=>x.Value.Correct).ThenByDescending(x=>Bits(x.Key)).ThenBy(x=>x.Value.Error).ThenBy(x=>x.Key).First().Value;
            var score=new MissionLocationScore{SourceCount=sources.Length,ResponseCount=marks.Length,Correct=best.Correct,FarFalseMarks=far.Count(value=>value),FarFalseMark=far,NearestDirectionErrors=nearest,CorrectMarks=new bool[marks.Length],DuplicateMarks=new bool[marks.Length],ResponseToSource=best.Map,PositionErrors=new double?[marks.Length],DirectionErrors=new double?[marks.Length],RangeErrors=new double?[marks.Length]};
            for(int i=0;i<marks.Length;i++)if(best.Map[i]>=0){
                var a=sources[best.Map[i]];var b=marks[i];double ax=a.X-observerX,az=a.Z-observerZ,bx=b.X-observerX,bz=b.Z-observerZ;
                score.PositionErrors[i]=position[i,best.Map[i]];score.CorrectMarks[i]=correct[i,best.Map[i]];score.RangeErrors[i]=Math.Abs(Math.Sqrt(ax*ax+az*az)-Math.Sqrt(bx*bx+bz*bz));
                score.DirectionErrors[i]=direction[i,best.Map[i]];
            }
            var matchedCorrectly = new bool[sources.Length];
            for (int i = 0; i < marks.Length; i++)
                if (score.CorrectMarks[i]) matchedCorrectly[best.Map[i]] = true;
            for (int i = 0; i < marks.Length; i++)
            {
                if (score.CorrectMarks[i]) continue;
                for (int j = 0; j < sources.Length; j++)
                {
                    if (!matchedCorrectly[j] || !correct[i,j]) continue;
                    score.DuplicateMarks[i] = true;
                    break;
                }
            }
            return score;
        }
        static double? Bearing(MissionSpawnPoint point,double observerX,double observerZ,double yaw)
        {
            double x=point.X-observerX,z=point.Z-observerZ;
            return x*x+z*z>1e-12 ? (double?)Angles.Bearing(x,z,yaw) : null;
        }
        static int Bits(int value){int count=0;while(value>0){count+=value&1;value>>=1;}return count;}
        static double Distance(MissionSpawnPoint a,MissionSpawnPoint b){double dx=a.X-b.X,dz=a.Z-b.Z;double distance=Math.Sqrt(dx*dx+dz*dz);Angles.Finite(distance);return distance;}
    }
}
