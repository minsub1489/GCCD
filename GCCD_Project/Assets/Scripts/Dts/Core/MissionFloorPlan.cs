using System;
using System.Collections.Generic;
namespace GCCD.Dts
{
    public struct MissionWall
    {
        public double X,Z,Width,Depth,Bottom,Height;
        public MissionWall(double x,double z,double width,double depth,double bottom,double height){X=x;Z=z;Width=width;Depth=depth;Bottom=bottom;Height=height;}
    }
    public struct MissionRoom
    {
        public string Name;public int X,Z;
        public MissionRoom(string name,int x,int z){Name=name;X=x;Z=z;}
    }
    public static class MissionFloorPlan
    {
        public const double MinX=-36,MaxX=36,MinZ=-25,MaxZ=53;
        public static readonly MissionWall[] Walls=BuildWalls();
        public static readonly MissionRoom[] Rooms=BuildRooms();
        static MissionRoom[] BuildRooms()
        {
            var rooms=new List<MissionRoom>();
            foreach(int side in new[]{-1,1})for(int i=0;i<4;i++){
                rooms.Add(new MissionRoom((side<0?"A":"B")+" · "+(i+1)+" 작업실",side*16,-10+i*18));
                rooms.Add(new MissionRoom((side<0?"A":"B")+" · "+(i+1)+" 창고",side*29,-10+i*18));
            }
            return rooms.ToArray();
        }
        static MissionWall[] BuildWalls()
        {
            var walls=new List<MissionWall>();
            foreach(int side in new[]{-1,1}){
                Doors(walls,true,side*12,-18,50,new double[]{-10,8,26,44},3.6,3.2);
                Doors(walls,true,side*20,-18,50,new double[]{-10,8,26,44},2.4,2.6);
                Doors(walls,true,side*24,-18,50,new double[]{-10,8,26,44},2.4,2.6);
                walls.Add(new MissionWall(side*34,16,.3,68,0,5.2));
                foreach(double z in new[]{-18d,50})walls.Add(new MissionWall(side*23,z,22,.3,0,5.2));
                foreach(double z in new[]{-1d,17,35}){
                    double lo=side<0?-20:12,hi=side<0?-12:20;
                    Doors(walls,false,z,lo,hi,new[]{side*16d},1.8,2.6);
                    lo=side<0?-34:24;hi=side<0?-24:34;
                    Doors(walls,false,z,lo,hi,new[]{side*29d},1.8,z==17?1.2:2.6);
                }
            }
            walls.Add(new MissionWall(MinX,14,.4,78,0,3));walls.Add(new MissionWall(MaxX,14,.4,78,0,3));
            walls.Add(new MissionWall(0,MinZ,72,.4,0,3));walls.Add(new MissionWall(0,MaxZ,72,.4,0,3));
            return walls.ToArray();
        }
        static void Doors(List<MissionWall> walls,bool alongZ,double axis,double start,double end,double[] centers,double width,double openingHeight)
        {
            Array.Sort(centers);double cursor=start;
            foreach(double center in centers){double a=center-width/2,b=center+width/2;
                Add(walls,alongZ,axis,cursor,a,0,5.2);Add(walls,alongZ,axis,a,b,openingHeight,5.2-openingHeight);cursor=b;
            }
            Add(walls,alongZ,axis,cursor,end,0,5.2);
        }
        static void Add(List<MissionWall> walls,bool alongZ,double axis,double a,double b,double bottom,double height)
        {if(b<=a)return;walls.Add(alongZ?new MissionWall(axis,(a+b)/2,.24,b-a,bottom,height):new MissionWall((a+b)/2,axis,b-a,.24,bottom,height));}
        public static bool Walkable(double x,double z,double height,double radius)
        {
            if(x<MinX+radius||x>MaxX-radius||z<MinZ+radius||z>MaxZ-radius)return false;
            foreach(var w in Walls)if(w.Bottom<height&&w.Bottom+w.Height>.05&&Math.Abs(x-w.X)<w.Width/2+radius&&Math.Abs(z-w.Z)<w.Depth/2+radius)return false;
            return true;
        }
        public static MissionSpawnPoint ToMap(double x,double z)
        {Angles.Finite(x);Angles.Finite(z);return new MissionSpawnPoint{X=(x-MinX)/(MaxX-MinX),Z=(MaxZ-z)/(MaxZ-MinZ)};}
        public static MissionSpawnPoint FromMap(double u,double v)
        {Angles.Finite(u);Angles.Finite(v);if(u<0||u>1||v<0||v>1)throw new ArgumentException("지도 안을 선택하세요.");return new MissionSpawnPoint{X=MinX+u*(MaxX-MinX),Z=MaxZ-v*(MaxZ-MinZ)};}
    }
}
