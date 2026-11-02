using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using GCCD.Dts;
class CoreTests
{
    static int passed, failed;
    static void Test(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch(Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    static void Eq<T>(T a,T b) { if(!Equals(a,b)) throw new Exception("expected " + b + ", actual " + a); }
    static void Near(double a,double b) { if(Math.Abs(a-b)>1e-8) throw new Exception(a+" != "+b); }
    static void Throws(Action a) { try { a(); } catch(ArgumentException) { return; } throw new Exception("expected argument rejection"); }
    static Threat T(string id,double bearing,double distance) { return new Threat(id,true,true,true,0,0,0,distance,bearing); }
    static Threat[] ABC() { return new[]{T("A",0,6),T("B",40,12),T("C",80,24)}; }
    static MotorProfile Profile() { return MotorProfile.Virtual(); }
    static void Main()
    {
        Test("0 retains coincident sources",()=>Eq(ThreatPolicy.Apply(new[]{T("A",0,6),T("B",0,9),T("C",0,11)},0).Kept.Length,3));
        foreach(int angle in new[]{15,30,45,60,90}) { int theta=angle; Test(theta+" inclusive boundary",()=>Eq(ThreatPolicy.Apply(new[]{T("A",350,6),T("B",350+theta,7)},theta).Kept.Length,1)); }
        Test("suppressed source never suppresses",()=>Eq(string.Join(",",ThreatPolicy.Apply(ABC(),45).Kept.Select(t=>t.Id)),"A,C"));
        Test("circular wrap",()=>Near(Angles.Difference(350,10),20));
        Test("opposite",()=>Near(Angles.Difference(0,180),180));
        Test("normalized bearing",()=>Near(T("A",-10,6).Bearing,350));
        Test("atan2 right clockwise",()=>Near(Angles.Bearing(1,0,0),90));
        Test("yaw relative",()=>Near(Angles.Bearing(1,0,90),0));
        Test("finite input",()=>Throws(()=>T("A",double.NaN,6)));
        Test("distance input",()=>Throws(()=>T("A",0,-1)));
        Test("unknown theta rejected",()=>Throws(()=>ThreatPolicy.Apply(ABC(),22)));
        Test("tie ordinal ID",()=>Eq(ThreatPolicy.Apply(new[]{T("B",0,6),T("A",40,6)},90).Kept[0].Id,"A"));
        Test("input order independent",()=>Eq(string.Join(",",ThreatPolicy.Apply(ABC().Reverse(),45).Kept.Select(t=>t.Id)),"A,C"));
        Test("no refill",()=>Eq(ThreatPolicy.Apply(ABC().Concat(new[]{T("D",200,30),T("E",270,36)}),90).Kept.Length,1));
        Test("parent nearest retained",()=>Eq(ThreatPolicy.Apply(new[]{T("A",0,6),T("B",80,8),T("C",40,9)},45).Decisions[2].ParentId,"A"));
        Test("all retained",()=>Eq(ThreatPolicy.Apply(new[]{T("A",0,6),T("B",120,12),T("C",240,24)},90).Kept.Length,3));
        Test("filter eligibility",()=>Eq(ThreatPolicy.Apply(new[]{new Threat("A",false,true,true,0,0,0,6,0),new Threat("B",true,false,true,0,0,0,7,0),new Threat("C",true,true,false,0,0,0,8,0)},0).Selected.Length,0));
        Test("duplicate IDs rejected",()=>Throws(()=>ThreatPolicy.Apply(new[]{T("A",0,6),T("A",90,9)},0)));
        Test("distance endpoints",()=>{Near(DistanceEncoding.Intensity(6,.1,.9),.9);Near(DistanceEncoding.Intensity(36,.1,.9),.1);});
        Test("distance clamp",()=>{Near(DistanceEncoding.Intensity(0,.1,.9),.9);Near(DistanceEncoding.Intensity(60,.1,.9),.1);});
        Test("distance boundaries",()=>{Eq(DistanceEncoding.Bin(11.999),DistanceBin.Near);Eq(DistanceEncoding.Bin(12),DistanceBin.Middle);Eq(DistanceEncoding.Bin(24),DistanceBin.Far);Eq(DistanceEncoding.Bin(36),DistanceBin.Far);});
        Test("invalid calibration",()=>Throws(()=>DistanceEncoding.Intensity(20,.8,.2)));
        Test("max composition",()=>{var p=Profile();var a=MotorComposer.Compose(new[]{T("A",0,6)},p,.1,.9);var b=MotorComposer.Compose(new[]{T("A",0,6),T("B",0,36)},p,.1,.9);Eq(string.Join(",",a),string.Join(",",b));});
        Test("quantized endpoint",()=>Eq(MotorComposer.Compose(new[]{T("A",0,6)},Profile(),.1,.9)[0],90));
        Test("profile duplicate channel",()=>{var p=Profile();p.Motors[1].Index=0;Throws(()=>p.Validate(false));});
        Test("unverified physical mapping rejected",()=>Throws(()=>Profile().Validate(true)));
        Test("zero response denominator three",()=>{var s=ResponseScorer.Score(ABC(),new Response[0],10);Near(s.JointAccuracy,0);Eq(s.Precision.HasValue,false);});
        Test("matching permutes responses",()=>{var s=ResponseScorer.Score(ABC(),new[]{new Response(80,DistanceBin.Far),new Response(0,DistanceBin.Near),new Response(40,DistanceBin.Middle)},5);Near(s.JointAccuracy,1);});
        Test("suppressed remains denominator",()=>Near(ResponseScorer.Score(ABC(),new[]{new Response(0,DistanceBin.Near),new Response(80,DistanceBin.Far)},5).JointAccuracy,2.0/3));
        Test("one response cannot match twice",()=>Near(ResponseScorer.Score(new[]{T("A",0,6),T("B",1,7),T("C",180,30)},new[]{new Response(0,DistanceBin.Near)},5).JointAccuracy,1.0/3));
        Test("direction and distance joint required",()=>Near(ResponseScorer.Score(ABC(),new[]{new Response(0,DistanceBin.Far)},5).JointAccuracy,0));
        Test("ambiguous overlap flagged",()=>Eq(ResponseScorer.Score(new[]{T("A",0,6),T("B",1,7),T("C",180,30)},new[]{new Response(.5,DistanceBin.Near)},5).Ambiguous,true));
        Test("independent distance accuracy",()=>Near(ResponseScorer.Score(ABC(),new[]{new Response(180,DistanceBin.Near),new Response(180,DistanceBin.Middle),new Response(180,DistanceBin.Far)},5).DistanceAccuracy,1));
        Test("response cap",()=>Throws(()=>ResponseScorer.Score(ABC(),new[]{new Response(0,DistanceBin.Near),new Response(0,DistanceBin.Near),new Response(0,DistanceBin.Near),new Response(0,DistanceBin.Near)},5)));
        Test("18 families each 6 conditions",()=>{var plan=ScenarioGenerator.PlanA(123,0,90);Eq(plan.Length,108);foreach(var g in plan.GroupBy(t=>t.Theta)) Eq(g.Count(),18);});
        Test("exact provided counterbalance rows",()=>Eq(string.Join(",",ConditionScheduler.Order(0)),"0,15,90,30,60,45"));
        Test("position balance",()=>{for(int pos=0;pos<6;pos++) Eq(Enumerable.Range(0,6).Select(i=>ConditionScheduler.Order(i)[pos]).Distinct().Count(),6);});
        Test("reproducible all snapshots",()=>{var a=ScenarioGenerator.PlanA(73,2,90);var b=ScenarioGenerator.PlanA(73,2,90);Eq(string.Join(";",a.Select(x=>x.Signature)),string.Join(";",b.Select(x=>x.Signature)));});
        Test("all generated threats offscreen and unique distance",()=>{foreach(var t in ScenarioGenerator.PlanA(10,3,100)){Eq(t.Threats.Length,3);Eq(t.Threats.Select(x=>x.Distance).Distinct().Count(),3);foreach(var x in t.Threats) Eq(Angles.IsOffscreen(x.Bearing,100),true);}});
        Test("distance distributions matched across theta",()=>{var plan=ScenarioGenerator.PlanA(1,0,90);var expected=string.Join(",",plan.Where(t=>t.Theta==0).SelectMany(t=>t.Threats).Select(t=>t.Distance).OrderBy(x=>x));foreach(var g in plan.GroupBy(t=>t.Theta)) Eq(string.Join(",",g.SelectMany(t=>t.Threats).Select(t=>t.Distance).OrderBy(x=>x)),expected);});
        Test("same distance categories allowed",()=>Eq(ScenarioGenerator.PlanA(1,0,90).Any(t=>t.Threats.Select(x=>DistanceEncoding.Bin(x.Distance)).Distinct().Count()==1),true));
        Test("invalid FOV rejects",()=>Throws(()=>ScenarioGenerator.PlanA(1,0,360)));
        Test("families reflect minimum pairwise gap",()=>{double[] edges={0,15,30,45,60,90,120};foreach(var t in ScenarioGenerator.PlanA(10,3,90)){double gap=180;for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)gap=Math.Min(gap,Angles.Difference(t.Threats[a].Bearing,t.Threats[b].Bearing));int bin=t.Family/3;Eq(gap>edges[bin]&&gap<edges[bin+1],true);}});
        Test("config rejects non-finite FOV",()=>{var c=new ExperimentConfig{HorizontalFov=double.NaN};Eq(c.Problems(false,false,false).Length>0,true);});
        Test("matching invalid tolerance",()=>Throws(()=>ResponseScorer.Score(ABC(),new Response[0],double.NaN)));
        Test("pending trial survives restart",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try {using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}"))s.Begin("T002","{}");using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}",true)){Eq(s.Pending.Single(),"T002");s.Commit("T002","{}");Eq(s.Pending.Length,0);}}finally{Directory.Delete(dir,true);}});
        Test("resume metadata mismatch refuses",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try {using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}")){}Throws(()=>new SessionStore(dir,"P001","DEVELOPMENT","A","{different}",true));}finally{Directory.Delete(dir,true);}});
        Test("B five active nearest three",()=>{foreach(var t in ScenarioGenerator.PlanB(4,1,90,45)){Eq(t.Threats.Length,5);Eq(ThreatPolicy.Apply(t.Threats,t.Theta).Selected.Length,3);}});
        Test("B refuses zero candidate",()=>Throws(()=>ScenarioGenerator.PlanB(4,1,90,0)));
        Test("game first miss permanent fail",()=>{var g=new FirstActionTracker("A",4);g.Shot(1,"A",false,false);g.Shot(2,"A",true,false);Eq(g.Success,false);Eq(g.Target,"A");});
        Test("front task excluded",()=>{var g=new FirstActionTracker("A",4);g.Shot(1,"FRONT",true,true);Eq(g.HasAction,false);g.Shot(2,"A",true,false);Eq(g.Success,true);Near(g.ResponseSeconds,2);});
        Test("deadline inclusive timeout",()=>{var g=new FirstActionTracker("A",4);g.Shot(4,"A",true,false);Eq(g.Success,false);Eq(g.TimedOut,true);});
        Test("output equivalence uses submitted integers",()=>{var r=StimulusValidator.Compare(ABC(),Profile(),.1,.9,500);Eq(r.Conditions.Length,6);Eq(r.PairCount,15);Eq(r.IdenticalPairs>=0,true);});
        Test("CSV quotes newline and comma",()=>Eq(CsvTable.Escape("a,\"b\"\nc"),"\"a,\"\"b\"\"\nc\""));
        Test("session commits reject duplicates and resume",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try {using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}")){s.Begin("T001","{} ");s.Commit("T001","{}");Throws(()=>s.Begin("T001","{}"));}using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}",true)) Eq(s.Completed.Contains("T001"),true);}finally{Directory.Delete(dir,true);}});
        Test("anonymous path validation",()=>Throws(()=>new SessionStore("/tmp/no-write","../real name","DEVELOPMENT","A","{}")));
        Test("committed journal corruption detected",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try{using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}")){s.Begin("T001","{}");s.Commit("T001","{\"score\":0}");}string f=Path.Combine(dir,"journal","T001.committed.json");File.WriteAllText(f,File.ReadAllText(f).Replace("score","other"));bool rejected=false;try{using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}",true)){} }catch(InvalidDataException){rejected=true;}Eq(rejected,true);}finally{Directory.Delete(dir,true);}});
        Test("mission spawn seed reproducibility",()=>{var a=MissionSpawnPlanner.Plan(77,6,0,-14,0,false,(x,z)=>false);var b=MissionSpawnPlanner.Plan(77,6,0,-14,0,false,(x,z)=>false);Eq(string.Join(";",a.Select(x=>x.X+","+x.Z)),string.Join(";",b.Select(x=>x.X+","+x.Z)));});
        Test("mission different seeds change enemies",()=>{var a=MissionSpawnPlanner.Plan(1,6,0,-14,0,false,(x,z)=>false);var b=MissionSpawnPlanner.Plan(2,6,0,-14,0,false,(x,z)=>false);Eq(a[0].X==b[0].X&&a[0].Z==b[0].Z,false);});
        Test("mission rejects occupied spawn and keeps spacing",()=>{var a=MissionSpawnPlanner.Plan(18,6,0,-14,0,false,(x,z)=>x<0);Eq(a.All(p=>p.X>=0&&p.X<10&&p.Z>=-24&&p.Z<=48),true);for(int i=0;i<a.Length;i++)for(int j=i+1;j<a.Length;j++)Eq(Math.Pow(a[i].X-a[j].X,2)+Math.Pow(a[i].Z-a[j].Z,2)>=9,true);});
        Test("mission surprise sources offscreen",()=>{var a=MissionSpawnPlanner.Plan(41,3,0,0,110,true,(x,z)=>false);foreach(var p in a)Eq(Angles.IsOffscreen(Angles.Bearing(p.X,p.Z,110),100),true);});
        Test("mission unavailable spawns fail bounded",()=>Throws(()=>MissionSpawnPlanner.Plan(1,5,0,0,0,false,(x,z)=>true)));
        Test("terminal map coordinates round trip",()=>{var p=MissionFloorPlan.ToMap(-28,18);var w=MissionFloorPlan.FromMap(p.X,p.Z);Near(w.X,-28);Near(w.Z,18);Throws(()=>MissionFloorPlan.FromMap(-.01,.5));});
        Test("terminal entrances are open and facade is solid",()=>{foreach(int side in new[]{-1,1})foreach(double z in new[]{-10d,8,26,44})Eq(MissionFloorPlan.Walkable(side*12,z,1.8,.35),true);Eq(MissionFloorPlan.Walkable(12,-15,1.8,.35),false);});
        Test("terminal rooms connected through traversable corridors",()=>{var seen=new HashSet<string>();var queue=new Queue<MissionSpawnPoint>();queue.Enqueue(new MissionSpawnPoint{X=0,Z=-14});while(queue.Count>0){var p=queue.Dequeue();string key=p.X+","+p.Z;if(seen.Contains(key)||!MissionFloorPlan.Walkable(p.X,p.Z,1.8,.35))continue;seen.Add(key);foreach(var d in new[]{new[]{1d,0},new[]{-1d,0},new[]{0d,1},new[]{0d,-1}})queue.Enqueue(new MissionSpawnPoint{X=p.X+d[0],Z=p.Z+d[1]});}foreach(var room in MissionFloorPlan.Rooms)Eq(seen.Contains(room.X+","+room.Z),true);});
        Test("terminal service hatch requires crouch",()=>{Eq(MissionFloorPlan.Walkable(29,17,1.8,.3),false);Eq(MissionFloorPlan.Walkable(29,17,1,.3),true);});
        Test("expanded enemies include safe indoor positions",()=>{var p=MissionSpawnPlanner.Plan(124,10,0,-14,0,false,(x,z)=>!MissionFloorPlan.Walkable(x,z,2.2,.7),true);Eq(p.Any(x=>Math.Abs(x.X)>12),true);Eq(p.All(x=>MissionFloorPlan.Walkable(x.X,x.Z,2.2,.7)),true);});
        Test("mission clock freezes and resumes without deadline drift",()=>{var c=new MissionPlayClock();Near(c.Read(10),10);c.SetPaused(true,10);Near(c.Read(80),10);c.SetPaused(true,90);c.SetPaused(false,100);Near(c.Read(102),12);});
        Test("response accepts optional marks and explicit empty completion",()=>{var r=new MissionLocationResponse();r.Begin();Eq(r.Active,true);Eq(r.Markers.Length,0);r.Complete();Eq(r.Active,false);Eq(r.Markers.Length,0);});
        Test("response markers toggle without exposing source count",()=>{var r=new MissionLocationResponse();r.Begin();r.Toggle(-20,8);r.Toggle(21,18);Eq(r.Markers.Length,2);r.Toggle(-20,8);Eq(r.Markers.Length,1);r.Complete();Eq(r.Toggle(0,0),false);r.Begin();Eq(r.Markers.Length,0);});
        Test("response rejects invalid map positions and supports undo",()=>{var r=new MissionLocationResponse();r.Begin();Throws(()=>r.Toggle(double.NaN,0));Throws(()=>r.Toggle(100,0));r.Toggle(0,0);r.Undo();Eq(r.Markers.Length,0);r.Undo();r.Toggle(0,1);r.Clear();Eq(r.Markers.Length,0);});
        Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed");
        Environment.ExitCode=failed==0?0:1;
    }
}
