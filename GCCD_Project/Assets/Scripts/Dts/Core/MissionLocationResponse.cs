using System;
using System.Collections.Generic;
namespace GCCD.Dts
{
    public sealed class MissionLocationResponse
    {
        readonly List<MissionSpawnPoint> markers=new List<MissionSpawnPoint>();
        public bool Active {get;private set;}
        public MissionSpawnPoint[] Markers {get{return markers.ToArray();}}
        public void Begin(){markers.Clear();Active=true;}
        public bool Toggle(double x,double z)
        {
            Angles.Finite(x);Angles.Finite(z);
            if(x<MissionFloorPlan.MinX||x>MissionFloorPlan.MaxX||z<MissionFloorPlan.MinZ||z>MissionFloorPlan.MaxZ)throw new ArgumentException("지도 안을 선택하세요.");
            if(!Active)return false;
            int index=markers.FindIndex(p=>(p.X-x)*(p.X-x)+(p.Z-z)*(p.Z-z)<2.25);
            if(index>=0)markers.RemoveAt(index);else markers.Add(new MissionSpawnPoint{X=x,Z=z});
            return true;
        }
        public void Undo(){if(Active&&markers.Count>0)markers.RemoveAt(markers.Count-1);}
        public void Clear(){if(Active)markers.Clear();}
        public void Complete(){Active=false;}
    }
    public sealed class MissionPlayClock
    {
        bool paused;double pauseAt,total;
        public double Read(double realtime){Angles.Finite(realtime);return (paused?pauseAt:realtime)-total;}
        public void SetPaused(bool value,double realtime)
        {
            Angles.Finite(realtime);if(value==paused)return;
            if(value)pauseAt=realtime;else total+=realtime-pauseAt;
            paused=value;
        }
    }
}
