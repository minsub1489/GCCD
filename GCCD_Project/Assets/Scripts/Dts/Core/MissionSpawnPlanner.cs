using System;
using System.Collections.Generic;
namespace GCCD.Dts
{
    public struct MissionSpawnPoint {public double X,Z;}
    public static class MissionSpawnPlanner
    {
        public const int MaximumThreatCount = 12;
        public static MissionSpawnPoint[] Plan(int seed,int count,double playerX,double playerZ,double yaw,bool offscreen,Func<double,double,bool> occupied,bool expanded=false,bool excludeSideGaps=false)
        {
            if(count<1||count>MaximumThreatCount||occupied==null)throw new ArgumentException("Invalid spawn request.");
            Angles.Finite(playerX);Angles.Finite(playerZ);Angles.Finite(yaw);
            var random=new StableRandom(seed);var points=new List<MissionSpawnPoint>();
            for(int attempt=0;attempt<4096&&points.Count<count;attempt++)
            {
                double x=expanded?-35+70*random.Unit():-9.2+18.4*random.Unit(),z=-23+(expanded?74:69)*random.Unit();
                double dx=x-playerX,dz=z-playerZ,d2=dx*dx+dz*dz;
                if(d2<36||d2>36*36||occupied(x,z))continue;
                if (offscreen || excludeSideGaps)
                {
                    double bearing = Angles.Bearing(dx, dz, yaw);
                    if (offscreen && !Angles.IsOffscreen(bearing, 100)) continue;
                    if (excludeSideGaps && X40Layout.SideSector(bearing) != 0) continue;
                }
                bool separated=true;foreach(var p in points)if((p.X-x)*(p.X-x)+(p.Z-z)*(p.Z-z)<9){separated=false;break;}
                if(separated)points.Add(new MissionSpawnPoint{X=x,Z=z});
            }
            if(points.Count!=count)throw new ArgumentException("No safe enemy positions. Retry from an open area.");return points.ToArray();
        }
    }
}
