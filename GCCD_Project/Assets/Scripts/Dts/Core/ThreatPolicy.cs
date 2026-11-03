using System;
using System.Collections.Generic;
using System.Linq;
namespace GCCD.Dts
{
    public static class Angles
    {
        public static void Finite(double x) { if(double.IsNaN(x)||double.IsInfinity(x)) throw new ArgumentException("Non-finite number."); }
        public static double Normalize(double x) { Finite(x); return ((x%360)+360)%360; }
        public static double Difference(double a,double b) { double d=Math.Abs(Normalize(a)-Normalize(b)); return Math.Min(d,360-d); }
        // Horizontal world displacement and camera yaw; positive = clockwise/right.
        public static double Bearing(double dx,double dz,double yaw) { Finite(dx);Finite(dz);Finite(yaw); if(dx==0&&dz==0) throw new ArgumentException("Undefined coincident bearing.");return Normalize(Math.Atan2(dx,dz)*180/Math.PI-yaw); }
        public static bool IsOffscreen(double bearing,double horizontalFov) { Finite(horizontalFov);if(horizontalFov<=0||horizontalFov>=180) throw new ArgumentException("FOV must be between 0 and 180 degrees.");return Difference(bearing,0)>horizontalFov/2; }
    }
    public sealed class Threat
    {
        public string Id { get; private set; }
        public bool Active { get; private set; } public bool Offscreen { get; private set; } public bool Firing { get; private set; }
        public double X { get; private set; } public double Y { get; private set; } public double Z { get; private set; }
        public double Distance { get; private set; } public double Bearing { get; private set; }
        public Threat(string id,bool active,bool offscreen,bool firing,double x,double y,double z,double distance,double bearing)
        { if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Threat ID required.");Angles.Finite(distance);Angles.Finite(x);Angles.Finite(y);Angles.Finite(z);if(distance<0)throw new ArgumentException("Negative distance.");Id=id;Active=active;Offscreen=offscreen;Firing=firing;X=x;Y=y;Z=z;Distance=distance;Bearing=Angles.Normalize(bearing); }
    }
    public sealed class Comparison { public string ComparedId; public double Difference; }
    public sealed class Decision
    { public Threat Threat; public int Rank; public bool Retained; public string ParentId="";public string Reason;public Comparison[] Comparisons; }
    public sealed class PolicyResult
    { public Threat[] Selected;public Threat[] Kept;public Decision[] Decisions; }
    public static class ThreatPolicy
    {
        public static readonly int[] Conditions={0,15,30,45,60,90};
        public static PolicyResult Apply(IEnumerable<Threat> input,int theta)
        {
            if(input==null||!Conditions.Contains(theta)) throw new ArgumentException("Expected six-condition theta and a snapshot.");
            var all=input.ToArray();if(all.Any(t=>t==null)||all.Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=all.Length)throw new ArgumentException("Null/duplicate threat ID.");
            var selected=all.Where(t=>t.Active&&t.Offscreen&&t.Firing).OrderBy(t=>t.Distance).ThenBy(t=>t.Id,StringComparer.Ordinal).ToArray();
            var kept=new List<Threat>();var decisions=new List<Decision>();
            foreach(var threat in selected)
            {
                var d=new Decision{Threat=threat,Rank=decisions.Count+1,Retained=true,Reason=theta==0?"baseline_all_retained":"outside_retained_angles"};
                var comparisons=new List<Comparison>();
                if(theta>0) foreach(var k in kept) { double delta=Angles.Difference(threat.Bearing,k.Bearing);comparisons.Add(new Comparison{ComparedId=k.Id,Difference=delta});if(delta<=theta){d.Retained=false;d.ParentId=k.Id;d.Reason="inclusive_angular_suppression";break;} }
                d.Comparisons=comparisons.ToArray();decisions.Add(d);if(d.Retained)kept.Add(threat);
            }
            return new PolicyResult{Selected=selected,Kept=kept.ToArray(),Decisions=decisions.ToArray()};
        }
    }
    public enum DistanceBin { Near,Middle,Far }
    public static class DistanceEncoding
    {
        public static double Intensity(double distance,double low,double high)
        {Angles.Finite(distance);Angles.Finite(low);Angles.Finite(high);if(low<=0||high>1||low>=high)throw new ArgumentException("Require 0 < L < H <= 1.");return low+(high-low)*(36-Math.Max(6,Math.Min(36,distance)))/30;}
        public static DistanceBin Bin(double distance) {Angles.Finite(distance);if(distance<6||distance>36)throw new ArgumentException("Distance outside study range.");return distance<12?DistanceBin.Near:distance<24?DistanceBin.Middle:DistanceBin.Far;}
    }
}
