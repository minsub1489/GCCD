using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed class MissionMapView:VisualElement
    {
        public Vector3 PlayerPosition;public float PlayerYaw;public Vector3[] Supplies=new Vector3[0];
        public MissionSpawnPoint[] Markers=new MissionSpawnPoint[0];public Action<double,double> Picked;
        public bool Interactive,Detailed;
        public bool ShowSuppressionSectors;
        public int SuppressionTheta;
        public Vector3[] ActualSources=new Vector3[0];public bool[] SuppressedSources;public int[] MatchedSourceIndices=new int[0];
        readonly Label[] roomLabels=new Label[MissionFloorPlan.Rooms.Length];
        Rect Plot {get{float scale=Mathf.Min((contentRect.width-28)/72,(contentRect.height-28)/78);return new Rect(contentRect.x+(contentRect.width-72*scale)/2,contentRect.y+(contentRect.height-78*scale)/2,72*scale,78*scale);}}
        public MissionMapView()
        {
            AddToClassList("mission-map");generateVisualContent+=Draw;
            for(int i=0;i<roomLabels.Length;i++){roomLabels[i]=new Label(MissionFloorPlan.Rooms[i].Name);roomLabels[i].AddToClassList("map-room-label");roomLabels[i].pickingMode=PickingMode.Ignore;Add(roomLabels[i]);}
            RegisterCallback<PointerDownEvent>(e=>{if(e.button==0&&SelectAt(e.localPosition))e.StopPropagation();});
        }
        public bool SelectAt(Vector2 local)
        {
            Rect r=Plot;if(!Interactive||r.width<=0||!r.Contains(local))return false;
            var p=MissionFloorPlan.FromMap((local.x-r.x)/r.width,(local.y-r.y)/r.height);if(Picked!=null)Picked(p.X,p.Z);return true;
        }
        Vector2 Point(double x,double z){var p=MissionFloorPlan.ToMap(x,z);var r=Plot;return new Vector2(r.x+(float)p.X*r.width,r.y+(float)p.Z*r.height);}
        public void Refresh()
        {
            for(int i=0;i<roomLabels.Length;i++){
                var p=Point(MissionFloorPlan.Rooms[i].X,MissionFloorPlan.Rooms[i].Z);roomLabels[i].style.display=Detailed?DisplayStyle.Flex:DisplayStyle.None;roomLabels[i].style.left=p.x-40;roomLabels[i].style.top=p.y-7;
            }
            MarkDirtyRepaint();
        }
        void Draw(MeshGenerationContext ctx)
        {
            if(contentRect.width<30||contentRect.height<30)return;var p=ctx.painter2D;Rect r=Plot;
            RectShape(p,r,new Color(.08f,.12f,.14f));
            foreach(int side in new[]{-1,1}){
                WorldRect(p,side<0?-34:12,-18,22,68,new Color(.16f,.21f,.23f));
                WorldRect(p,side<0?-24:20,-18,4,68,new Color(.21f,.28f,.29f));
            }
            p.lineWidth=1;p.strokeColor=new Color(.23f,.29f,.30f);
            for(int x=-30;x<=30;x+=10)Line(p,Point(x,-25),Point(x,53));for(int z=-20;z<=50;z+=10)Line(p,Point(-36,z),Point(36,z));
            foreach(var w in MissionFloorPlan.Walls){if(w.Bottom>1.25)continue;WorldRect(p,w.X-w.Width/2,w.Z-w.Depth/2,w.Width,w.Depth,w.Bottom>0?new Color(.85f,.65f,.31f):new Color(.58f,.65f,.64f));}
            foreach(var supply in Supplies)Circle(p,Point(supply.x,supply.z),3.5f,new Color(.47f,.78f,.61f));
            Vector2 at=Point(PlayerPosition.x,PlayerPosition.z);float yaw=PlayerYaw*Mathf.Deg2Rad;Vector2 forward=new Vector2(Mathf.Sin(yaw),-Mathf.Cos(yaw)),right=new Vector2(-forward.y,forward.x);
            if (ShowSuppressionSectors && SuppressionTheta > 0 && SuppressedSources?.Length == ActualSources.Length)
                DrawSuppressionSectors(p, r, at);
            p.fillColor=new Color(.55f,.85f,.89f);p.BeginPath();p.MoveTo(at+forward*10);p.LineTo(at-forward*6+right*6);p.LineTo(at-forward*3);p.LineTo(at-forward*6-right*6);p.ClosePath();p.Fill();
            p.lineWidth=1.5f;p.strokeColor=new Color(.65f,.72f,.70f,.7f);
            for(int i=0;i<Markers.Length&&i<MatchedSourceIndices.Length;i++){int j=MatchedSourceIndices[i];if(j>=0&&j<ActualSources.Length)Line(p,Point(Markers[i].X,Markers[i].Z),Point(ActualSources[j].x,ActualSources[j].z));}
            for(int i=0;i<ActualSources.Length;i++){if(IsSuppressed(i))continue;var source=ActualSources[i];Vector2 pos=Point(source.x,source.z);Circle(p,pos,9,new Color(.3f,.83f,.66f));Circle(p,pos,5,new Color(.08f,.12f,.14f));}
            foreach(var marker in Markers){Vector2 pos=Point(marker.X,marker.Z);Circle(p,pos,7,new Color(.91f,.68f,.29f));Circle(p,pos,2,new Color(.12f,.13f,.12f));}
            for(int i=0;i<ActualSources.Length;i++)
            {
                if(!IsSuppressed(i))continue;
                Vector2 pos=Point(ActualSources[i].x,ActualSources[i].z);
                Circle(p,pos,13,Color.white);
                Circle(p,pos,11,new Color(1f,.46f,.16f));
                p.strokeColor=new Color(.12f,.06f,.02f);p.lineWidth=3;
                Line(p,pos+new Vector2(-5,-5),pos+new Vector2(5,5));
                Line(p,pos+new Vector2(-5,5),pos+new Vector2(5,-5));
            }
            p.strokeColor=new Color(.45f,.62f,.65f);p.lineWidth=1;p.BeginPath();p.MoveTo(r.min);p.LineTo(new Vector2(r.xMax,r.yMin));p.LineTo(r.max);p.LineTo(new Vector2(r.xMin,r.yMax));p.ClosePath();p.Stroke();
        }
        void DrawSuppressionSectors(Painter2D painter, Rect bounds, Vector2 origin)
        {
            if (!bounds.Contains(origin)) return;
            float radius = bounds.width * .5f; // Map width is 72 m and event sources are within 36 m.
            float halfAngle = SuppressionTheta * .5f;
            for (int i = 0; i < ActualSources.Length; i++)
            {
                if (IsSuppressed(i)) continue;
                var source = ActualSources[i];
                Vector2 direction = Point(source.x, source.z) - origin;
                if (direction.sqrMagnitude < .001f) continue;
                float center = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                painter.fillColor = new Color(1f, .54f, .18f, .19f);
                painter.strokeColor = new Color(1f, .64f, .30f, .78f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(origin);
                for (int step = 0; step <= 24; step++)
                {
                    float angle = (center - halfAngle + SuppressionTheta * step / 24f) * Mathf.Deg2Rad;
                    Vector2 ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float edgeX = ((ray.x > 0 ? bounds.xMax : bounds.xMin) - origin.x) / ray.x;
                    float edgeY = ((ray.y > 0 ? bounds.yMax : bounds.yMin) - origin.y) / ray.y;
                    float length = radius;
                    if (Mathf.Abs(ray.x) > .0001f) length = Mathf.Min(length, edgeX);
                    if (Mathf.Abs(ray.y) > .0001f) length = Mathf.Min(length, edgeY);
                    painter.LineTo(origin + ray * length);
                }
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }
        }

        bool IsSuppressed(int index) => SuppressedSources != null && index < SuppressedSources.Length && SuppressedSources[index];
        void WorldRect(Painter2D p,double x,double z,double width,double depth,Color color){Vector2 a=Point(x,z+depth),b=Point(x+width,z);RectShape(p,new Rect(a.x,a.y,Mathf.Max(1,b.x-a.x),Mathf.Max(1,b.y-a.y)),color);}
        static void RectShape(Painter2D p,Rect r,Color color){p.fillColor=color;p.BeginPath();p.MoveTo(r.min);p.LineTo(new Vector2(r.xMax,r.yMin));p.LineTo(r.max);p.LineTo(new Vector2(r.xMin,r.yMax));p.ClosePath();p.Fill();}
        static void Circle(Painter2D p,Vector2 at,float radius,Color color){p.fillColor=color;p.BeginPath();p.Arc(at,radius,Angle.Degrees(0),Angle.Degrees(360));p.ClosePath();p.Fill();}
        static void Line(Painter2D p,Vector2 a,Vector2 b){p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();}
    }
}
