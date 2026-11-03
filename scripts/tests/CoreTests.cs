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
        foreach(int angle in new[]{15,30,45,60,90}) { int theta=angle; Test(theta+" inclusive boundary",()=>Eq(ThreatPolicy.Apply(new[]{T("A",350,6),T("B",350+theta/2.0,7)},theta).Kept.Length,1)); }
        Test("suppressed source never suppresses",()=>Eq(string.Join(",",ThreatPolicy.Apply(ABC(),90).Kept.Select(t=>t.Id)),"A,C"));
        Test("circular wrap",()=>Near(Angles.Difference(350,10),20));
        Test("opposite",()=>Near(Angles.Difference(0,180),180));
        Test("normalized bearing",()=>Near(T("A",-10,6).Bearing,350));
        Test("atan2 right clockwise",()=>Near(Angles.Bearing(1,0,0),90));
        Test("yaw relative",()=>Near(Angles.Bearing(1,0,90),0));
        Test("finite input",()=>Throws(()=>T("A",double.NaN,6)));
        Test("distance input",()=>Throws(()=>T("A",0,-1)));
        Test("unknown theta rejected",()=>Throws(()=>ThreatPolicy.Apply(ABC(),22)));
        Test("tie ordinal ID",()=>Eq(ThreatPolicy.Apply(new[]{T("B",0,6),T("A",40,6)},90).Kept[0].Id,"A"));
        Test("input order independent",()=>Eq(string.Join(",",ThreatPolicy.Apply(ABC().Reverse(),90).Kept.Select(t=>t.Id)),"A,C"));
        Test("60-degree example retains five and composes all five",()=>{
            double[] bearings={20,40,100,125,175,200,265,315};
            double[] distances={7,9,12,15,18,21,25,29};
            var input=bearings.Select((bearing,i)=>T(((char)('A'+i)).ToString(),bearing,distances[i])).Reverse().ToArray();
            var result=ThreatPolicy.Apply(input,60);
            Eq(result.Selected.Length,8);
            Eq(string.Join(",",result.Kept.Select(t=>t.Id)),"A,C,E,G,H");
            Eq(string.Join(",",result.Decisions.Where(d=>!d.Retained).Select(d=>d.ParentId)),"A,C,E");
            Eq(result.Decisions.Count(d=>!d.Retained)+result.Kept.Length,input.Length);
            var profile=Profile();
            var frame=MotorComposer.Compose(result.Kept,profile,.15,.75);
            var separate=result.Kept.Select(t=>MotorComposer.Compose(new[]{t},profile,.15,.75)).ToArray();
            for(int channel=0;channel<40;channel++) Eq(frame[channel],separate.Max(f=>f[channel]));
            for(int i=0;i<separate.Length;i++) Eq(Enumerable.Range(0,40).Any(c=>separate[i][c]>0&&separate.Where((_,j)=>j!=i).All(f=>f[c]<separate[i][c])),true);
            var comparison=StimulusValidator.Compare(input,profile,.15,.75,500).Conditions.Single(c=>c.Theta==60);
            Eq(comparison.RetainedIds.SequenceEqual(result.Kept.Select(t=>t.Id)),true);
            Eq(comparison.Motors.SequenceEqual(frame),true);
        });
        Test("60 and 90 are full sector widths on either side of center",()=>{
            foreach(int theta in new[]{60,90})
            foreach(int side in new[]{-1,1})
            {
                var input=new[]{T("near",100,7),T("boundary",100+side*theta/2.0,10),T("outside",100+side*(theta/2.0+.01),12)};
                Eq(string.Join(",",ThreatPolicy.Apply(input,theta).Kept.Select(t=>t.Id)),"near,outside");
            }
        });
        Test("random layouts are paired and suppression varies",()=>{
            var plan=ScenarioGenerator.PlanA(17,0,90,5);
            foreach(var family in plan.GroupBy(t=>t.Family))
                Eq(family.Select(t=>string.Join(";",t.Threats.Select(x=>x.Bearing+","+x.Distance))).Distinct().Count(),1);
            Eq(plan.Where(t=>t.Theta==45).Select(t=>ThreatPolicy.Apply(t.Threats,45).Kept.Length).Distinct().Count()>1,true);
            Eq(plan.Where(t=>t.Theta==0).All(t=>ThreatPolicy.Apply(t.Threats,0).Kept.Length==5),true);
        });
        Test("unset count only has a development fallback",()=>{
            var config=new ExperimentConfig();
            Eq(config.ThreatCountFor(true),6);
            Throws(()=>config.ThreatCountFor(false));
            Throws(()=>config.TrialsPerConditionFor(false));
            config.TotalThreatCount=12;
            Eq(config.ThreatCountFor(false),12);
        });
        Test("twelve threats can be scored with twelve markers",()=>{
            var threats=Enumerable.Range(0,12).Select(i=>T("E"+i,i*30,7)).ToArray();
            var responses=threats.Reverse().Select(t=>new Response(t.Bearing,DistanceBin.Near)).ToArray();
            Near(ResponseScorer.Score(threats,responses,5).JointAccuracy,1);
            Near(ResponseScorer.Score(threats,responses.Take(5).ToArray(),5).JointAccuracy,5.0/12);
        });
        Test("parent nearest retained",()=>Eq(ThreatPolicy.Apply(new[]{T("A",0,6),T("B",80,8),T("C",40,9)},90).Decisions[2].ParentId,"A"));
        Test("all retained",()=>Eq(ThreatPolicy.Apply(new[]{T("A",0,6),T("B",120,12),T("C",240,24)},90).Kept.Length,3));
        Test("supplied roster participates independently of visibility flags",()=>Eq(ThreatPolicy.Apply(new[]{new Threat("A",false,true,true,0,0,0,6,0),new Threat("B",true,false,true,0,0,0,7,0),new Threat("C",true,true,false,0,0,0,8,0)},0).Selected.Length,3));
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
        Test("assignment maximizes correct pairs before minimizing angular error",()=>{
            var original=new[]{T("A",0,7),T("B",12,8)};
            var responses=new[]{new Response(4,DistanceBin.Near),new Response(356,DistanceBin.Near)};
            var score=ResponseScorer.Score(original,responses,8);
            Eq(string.Join(",",score.MatchedThreatIndices),"1,0");
            Near(score.JointAccuracy,1);
        });
        Test("assignment breaks correct-count ties by total angle error",()=>{
            var original=new[]{T("A",359,7),T("B",12,8)};
            var score=ResponseScorer.Score(original,new[]{new Response(0,DistanceBin.Near)},15);
            Eq(score.MatchedThreatIndices[0],0);
            Near(score.DirectionError.Value,1);
        });
        Test("one response cannot match twice",()=>Near(ResponseScorer.Score(new[]{T("A",0,6),T("B",1,7),T("C",180,30)},new[]{new Response(0,DistanceBin.Near)},5).JointAccuracy,1.0/3));
        Test("direction and distance joint required",()=>Near(ResponseScorer.Score(ABC(),new[]{new Response(0,DistanceBin.Far)},5).JointAccuracy,0));
        Test("ambiguous overlap flagged",()=>Eq(ResponseScorer.Score(new[]{T("A",0,6),T("B",1,7),T("C",180,30)},new[]{new Response(.5,DistanceBin.Near)},5).Ambiguous,true));
        Test("independent distance accuracy",()=>Near(ResponseScorer.Score(ABC(),new[]{new Response(180,DistanceBin.Near),new Response(180,DistanceBin.Middle),new Response(180,DistanceBin.Far)},5).DistanceAccuracy,1));
        Test("response cap",()=>Throws(()=>ResponseScorer.Score(ABC(),new[]{new Response(0,DistanceBin.Near),new Response(0,DistanceBin.Near),new Response(0,DistanceBin.Near),new Response(0,DistanceBin.Near)},5)));
        Test("18 families each 6 conditions",()=>{var plan=ScenarioGenerator.PlanA(123,0,90,5);Eq(plan.Length,108);foreach(var g in plan.GroupBy(t=>t.Theta)) Eq(g.Count(),18);});
        Test("exact provided counterbalance rows",()=>Eq(string.Join(",",ConditionScheduler.Order(0)),"0,15,90,30,60,45"));
        Test("position balance",()=>{for(int pos=0;pos<6;pos++) Eq(Enumerable.Range(0,6).Select(i=>ConditionScheduler.Order(i)[pos]).Distinct().Count(),6);});
        Test("reproducible all snapshots",()=>{var a=ScenarioGenerator.PlanA(73,2,90,5);var b=ScenarioGenerator.PlanA(73,2,90,5);Eq(string.Join(";",a.Select(x=>x.Signature)),string.Join(";",b.Select(x=>x.Signature)));});
        Test("360-degree layouts include front and all quadrants",()=>{
            var plan=ScenarioGenerator.PlanA(10,3,100,12,7);
            Eq(plan.Length,42);
            foreach(var t in plan)
            {
                Eq(t.Threats.Length,12);
                Eq(ThreatPolicy.Apply(t.Threats,0).Kept.Length,12);
            }
            var threats=plan.SelectMany(t=>t.Threats).ToArray();
            Eq(threats.Select(t=>(int)(t.Bearing/90)).Distinct().Count(),4);
            Eq(threats.Any(t=>!t.Offscreen),true);
        });
        Test("distance distributions matched across theta",()=>{var plan=ScenarioGenerator.PlanA(1,0,90,5);var expected=string.Join(",",plan.Where(t=>t.Theta==0).SelectMany(t=>t.Threats).Select(t=>t.Distance).OrderBy(x=>x));foreach(var g in plan.GroupBy(t=>t.Theta)) Eq(string.Join(",",g.SelectMany(t=>t.Threats).Select(t=>t.Distance).OrderBy(x=>x)),expected);});
        Test("invalid FOV rejects",()=>Throws(()=>ScenarioGenerator.PlanA(1,0,360,5)));
        Test("config rejects non-finite FOV",()=>{var c=new ExperimentConfig{HorizontalFov=double.NaN};Eq(c.Problems(false,false).Length>0,true);});
        Test("matching invalid tolerance",()=>Throws(()=>ResponseScorer.Score(ABC(),new Response[0],double.NaN)));
        Test("pending trial survives restart",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try {using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}"))s.Begin("T002","{}");using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}",true)){Eq(s.Pending.Single(),"T002");s.Commit("T002","{}");Eq(s.Pending.Length,0);}}finally{Directory.Delete(dir,true);}});
        Test("resume metadata mismatch refuses",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try {using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}")){}Throws(()=>new SessionStore(dir,"P001","DEVELOPMENT","A","{different}",true));}finally{Directory.Delete(dir,true);}});
        Test("output equivalence uses submitted integers",()=>{var r=StimulusValidator.Compare(ABC(),Profile(),.1,.9,500);Eq(r.Conditions.Length,6);Eq(r.PairCount,15);Eq(r.IdenticalPairs>=0,true);});
        Test("CSV quotes newline and comma",()=>Eq(CsvTable.Escape("a,\"b\"\nc"),"\"a,\"\"b\"\"\nc\""));
        Test("session commits reject duplicates and resume",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try {using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}")){s.Begin("T001","{} ");s.Commit("T001","{}");Throws(()=>s.Begin("T001","{}"));}using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}",true)) Eq(s.Completed.Contains("T001"),true);}finally{Directory.Delete(dir,true);}});
        Test("anonymous path validation",()=>Throws(()=>new SessionStore("/tmp/no-write","../real name","DEVELOPMENT","A","{}")));
        Test("committed journal corruption detected",()=>{var dir=Path.Combine(Path.GetTempPath(),"dts-test-"+Guid.NewGuid());try{using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}")){s.Begin("T001","{}");s.Commit("T001","{\"score\":0}");}string f=Path.Combine(dir,"journal","T001.committed.json");File.WriteAllText(f,File.ReadAllText(f).Replace("score","other"));bool rejected=false;try{using(var s=new SessionStore(dir,"P001","DEVELOPMENT","A","{}",true)){} }catch(InvalidDataException){rejected=true;}Eq(rejected,true);}finally{Directory.Delete(dir,true);}});
        Test("FPS requires both position and strict angular agreement",()=>{
            var sources=new[]{new MissionSpawnPoint{Z=6}};
            double angle=20*Math.PI/180;
            var mark=new MissionSpawnPoint{X=6*Math.Sin(angle),Z=6*Math.Cos(angle)};
            Eq(MissionLocationScorer.Score(sources,new[]{mark},0,0,0,3).Correct,1);
            var score=MissionLocationScorer.Score(sources,new[]{mark},0,0,0,3,15);
            Eq(score.Correct,0);Eq(score.FarFalseMarks,1);
            var boundary=MissionLocationScorer.Score(sources,new[]{new MissionSpawnPoint{X=6}},0,0,0,10,90);
            Eq(boundary.Correct,0);Eq(boundary.FarFalseMarks,1);
        });
        Test("FPS ignores nearby duplicates for direction penalty and deducts far marks",()=>{
            var sources=new[]{new MissionSpawnPoint{Z=8}};
            var near=new[]{sources[0],new MissionSpawnPoint{X=.5,Z=8}};
            var score=MissionLocationScorer.Score(sources,near,0,0,0,3,15);
            Eq(score.Correct,1);Eq(score.FalseMarks,1);Eq(score.AdjustedCorrect,1);
            score=MissionLocationScorer.Score(sources,near.Concat(new[]{new MissionSpawnPoint{X=8}}).ToArray(),0,0,0,3,15);
            Eq(score.Correct,1);Eq(score.FarFalseMarks,1);Eq(score.AdjustedCorrect,0);
            Eq(score.FarFalseMark[2],true);
            Eq(string.Join(",",score.DuplicateMarks),"False,True,False");
        });
        Test("FPS overlapping suppressed match keeps credit and displays duplicate",()=>{
            var sources=new[]{new MissionSpawnPoint{Z=10},new MissionSpawnPoint{X=2,Z=10}};
            var score=MissionLocationScorer.Score(sources,sources,0,0,0,3,15,new[]{false,true});
            Eq(score.Correct,2);Eq(score.AdjustedCorrect,2);
            Eq(string.Join(",",score.DuplicateMarks),"False,True");
            score=MissionLocationScorer.Score(sources,new[]{sources[0],new MissionSpawnPoint{Z=14}},0,0,0,3,15,new[]{false,true});
            Eq(score.Correct,1);Eq(score.FarFalseMarks,0);
            Eq(string.Join(",",score.DuplicateMarks),"False,True");
        });
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
        Test("response accepts optional marks and explicit empty completion",()=>{var r=new MissionLocationResponse();r.Begin(3);Eq(r.Active,true);Eq(r.Markers.Length,0);r.Complete();Eq(r.Active,false);Eq(r.Markers.Length,0);});
        Test("response markers toggle without exposing source count",()=>{var r=new MissionLocationResponse();r.Begin(3);r.Toggle(-20,8);r.Toggle(21,18);Eq(r.Markers.Length,2);r.Toggle(-20,8);Eq(r.Markers.Length,1);r.Complete();Eq(r.Toggle(0,0),false);r.Begin(3);Eq(r.Markers.Length,0);});
        Test("FPS mark limit blocks additions but allows replacement",()=>{
            var r=new MissionLocationResponse();r.Begin(2);
            Eq(r.Toggle(-20,8),true);Eq(r.Toggle(21,18),true);
            Eq(r.Toggle(0,0),false);Eq(r.Markers.Length,2);
            Eq(r.Toggle(-20,8),true);Eq(r.Toggle(0,0),true);Eq(r.Markers.Length,2);
        });
        Test("response rejects invalid map positions and supports undo",()=>{var r=new MissionLocationResponse();r.Begin(3);Throws(()=>r.Toggle(double.NaN,0));Throws(()=>r.Toggle(100,0));r.Toggle(0,0);r.Undo();Eq(r.Markers.Length,0);r.Undo();r.Toggle(0,1);r.Clear();Eq(r.Markers.Length,0);});
        Test("mission scoring ignores response order and uses unique sources",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=0,Z=10},new MissionSpawnPoint{X=10,Z=0}},new[]{new MissionSpawnPoint{X=10,Z=0},new MissionSpawnPoint{X=0,Z=10}},0,0,0,3);Eq(s.Correct,2);Near(s.Recall.Value,1);Near(s.MeanPositionError.Value,0);});
        Test("mission duplicate guesses cannot claim two hits",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=0,Z=10},new MissionSpawnPoint{X=10,Z=0}},new[]{new MissionSpawnPoint{X=0,Z=10},new MissionSpawnPoint{X=0,Z=10}},0,0,0,3);Eq(s.Correct,1);Eq(s.Missed,1);Eq(s.FalseMarks,1);});
        Test("mission empty response counts every missed source",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=0,Z=10}},new MissionSpawnPoint[0],0,0,0,3);Near(s.Recall.Value,0);Eq(s.Precision.HasValue,false);Eq(s.MeanPositionError.HasValue,false);Eq(s.Missed,1);});
        Test("mission extra marks reduce precision",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=0,Z=10}},new[]{new MissionSpawnPoint{X=0,Z=10},new MissionSpawnPoint{X=20,Z=20}},0,0,0,3);Near(s.Precision.Value,.5);Eq(s.ResponseToSource.Count(x=>x<0),1);});
        Test("mission threshold is inclusive and position error is horizontal",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=0,Z=10}},new[]{new MissionSpawnPoint{X=3,Z=10}},0,0,0,3);Eq(s.Correct,1);Near(s.MeanPositionError.Value,3);});
        Test("mission matching maximizes hits before nearest pair error",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=0,Z=10},new MissionSpawnPoint{X=3,Z=10}},new[]{new MissionSpawnPoint{X=1,Z=10},new MissionSpawnPoint{X=-2,Z=10}},0,0,0,2);Eq(s.Correct,2);});
        Test("mission scorer handles wrapped bearings and observer marker",()=>{var s=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=1,Z=10}},new[]{new MissionSpawnPoint{X=-1,Z=10}},0,0,180,3);Eq(s.MeanDirectionError.Value<12,true);var zero=MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=1,Z=10}},new[]{new MissionSpawnPoint{X=0,Z=0}},0,0,0,3);Eq(zero.MeanDirectionError.HasValue,false);});
        Test("mission scorer rejects invalid numeric data",()=>{Throws(()=>MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=double.NaN}},new MissionSpawnPoint[0],0,0,0,3));Throws(()=>MissionLocationScorer.Score(new[]{new MissionSpawnPoint{X=1}},new MissionSpawnPoint[0],0,0,0,-1));});
        Test("X40 profile has front and back five rows of four",()=>{var p=X40Layout.Profile();Eq(p.Motors.Length,40);Eq(p.Motors.Select(m=>m.Bearing).Distinct().Count(),8);foreach(int col in Enumerable.Range(0,4))for(int row=1;row<5;row++){Near(p.Motors[row*4+col].Bearing,p.Motors[col].Bearing);Near(p.Motors[20+row*4+col].Bearing,p.Motors[20+col].Bearing);}Eq(p.PhysicalLayoutVerified,false);});
        Test("X40 forward lights only front middle columns",()=>{var f=MotorComposer.Compose(new[]{T("F",0,6)},X40Layout.Profile(),.2,.45);Eq(Enumerable.Range(0,40).Where(i=>f[i]>0).SequenceEqual(Enumerable.Range(0,20).Where(i=>i%4==1||i%4==2)),true);});
        Test("X40 backward lights only back middle columns",()=>{var f=MotorComposer.Compose(new[]{T("B",180,6)},X40Layout.Profile(),.2,.45);Eq(Enumerable.Range(0,40).Where(i=>f[i]>0).SequenceEqual(Enumerable.Range(20,20).Where(i=>i%4==1||i%4==2)),true);});
        Test("X40 right uses wearer right edges on both panels",()=>{var f=MotorComposer.Compose(new[]{T("R",90,6)},X40Layout.Profile(),.2,.45);Eq(Enumerable.Range(0,40).Where(i=>f[i]>0).All(i=>i%4==3),true);Eq(f.Count(i=>i>0),10);});
        Test("X40 left uses wearer left edges on both panels",()=>{var f=MotorComposer.Compose(new[]{T("L",270,6)},X40Layout.Profile(),.2,.45);Eq(Enumerable.Range(0,40).Where(i=>f[i]>0).All(i=>i%4==0),true);Eq(f.Count(i=>i>0),10);});
        Test("X40 panel correction and mirror are independent",()=>{var standard=X40Layout.Profile();var mirrored=X40Layout.Profile(true,false,false);Near(mirrored.Motors[0].Bearing,standard.Motors[3].Bearing);Near(mirrored.Motors[20].Bearing,standard.Motors[20].Bearing);var swapped=X40Layout.Profile(false,false,true);Near(swapped.Motors[0].Bearing,standard.Motors[20].Bearing);});
        Test("X40 frame split preserves channel 19 20 and 39",()=>{var v=new int[40];v[19]=19;v[20]=20;v[39]=39;var panels=X40Layout.Split(v);Eq(panels.Length,2);Eq(panels[0].Length,20);Eq(panels[1].Length,20);Eq(panels[0][19],19);Eq(panels[1][0],20);Eq(panels[1][19],39);Throws(()=>X40Layout.Split(new int[32]));v[0]=101;Throws(()=>X40Layout.Split(v));});
        Test("X40 keeps distance falloff and circular wrap",()=>{var p=X40Layout.Profile();var a=MotorComposer.Compose(new[]{T("A",359,6)},p,.2,.45);var b=MotorComposer.Compose(new[]{T("B",1,6)},p,.2,.45);Eq(a.Sum(),b.Sum());var far=MotorComposer.Compose(new[]{T("A",0,36)},p,.2,.45);Eq(far.Sum()<a.Sum(),true);});
        Test("distance check near middle far have clear separated X40 levels",()=>{var p=X40Layout.Profile();var near=MissionHapticEncoding.CheckFrame(0,0,p);var mid=MissionHapticEncoding.CheckFrame(0,1,p);var far=MissionHapticEncoding.CheckFrame(0,2,p);Eq(near.Max(),40);Eq(mid.Max(),25);Eq(far.Max(),10);Eq(near.Sum(),far.Sum()*4);Eq(Enumerable.Range(0,40).All(i=>(near[i]>0)==(far[i]>0)),true);});
        Test("distance check matches mission composition at same distance",()=>{var p=X40Layout.Profile();for(int level=0;level<3;level++){double d=MissionHapticEncoding.CheckDistance(level);var expected=MissionHapticEncoding.Compose(new[]{T("real",90,d)},p);Eq(expected.SequenceEqual(MissionHapticEncoding.CheckFrame(1,level,p)),true);}});
        Test("distance strength is monotonic clamped and bounded at every angle",()=>{var p=X40Layout.Profile();for(int angle=0;angle<360;angle+=15){int previous=100000;for(int distance=0;distance<=45;distance++){var frame=MissionHapticEncoding.Compose(new[]{T("A",angle,distance)},p);Eq(frame.All(v=>v>=0&&v<=80),true);Eq(frame.Sum()<=previous,true);previous=frame.Sum();}}});
        Test("distance checks reject invalid selection",()=>{Throws(()=>MissionHapticEncoding.CheckFrame(-1,0,X40Layout.Profile()));Throws(()=>MissionHapticEncoding.CheckFrame(0,3,X40Layout.Profile()));});
        Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed");
        Environment.ExitCode=failed==0?0:1;
    }
}
