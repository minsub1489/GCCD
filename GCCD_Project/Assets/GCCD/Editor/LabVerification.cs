using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GCCD;
using UnityEngine;

public static class LabVerification
{
    public static string Run()
    {
        if(!Application.isPlaying) throw new Exception("Enter Play Mode first.");
        ResearchModel.SelfTest();
        var lab=UnityEngine.Object.FindFirstObjectByType<ThreatLab>();
        if(lab==null) throw new Exception("ThreatLab missing");
        lab.haptics.udpEnabled=false;
        int oldSeed=lab.seed; lab.seed=1234;
        MethodInfo begin=typeof(ThreatLab).GetMethod("BeginCue",BindingFlags.Instance|BindingFlags.NonPublic);
        MethodInfo finish=typeof(ThreatLab).GetMethod("FinishTrial",BindingFlags.Instance|BindingFlags.NonPublic);
        lab.StartSession(false);
        string fullPath=lab.CsvPath;
        for(int i=0;i<144;i++) {
            if(lab.State!="READY") throw new Exception("Trial not ready "+i);
            begin.Invoke(lab,null);
            if(lab.State!="ACTIVE") throw new Exception("Cue missing");
            var t=lab.Current;
            int target=-1;
            for(int n=0;n<lab.targetRoot.childCount;n++) if(Vector3.Distance(lab.targetRoot.GetChild(n).position,t.Position)<.1f) target=n;
            if(target<0) throw new Exception("Source has no selectable target");
            lab.Replay(); lab.SelectTarget(target); lab.AnswerDistance(t.distance); lab.Next();
        }
        if(lab.State!="COMPLETE" || lab.Completed!=144) throw new Exception("Completion");
        var lines=File.ReadAllLines(fullPath);
        if(lines.Length!=145) throw new Exception("CSV rows");
        foreach(var line in lines.Skip(1)) {
            var cells=line.Split(',');
            if(cells.Length!=28 || cells[18]!="True" || cells[19]!="True" || cells[20]!="True" || cells[21]!="True" || cells[24]!="1") throw new Exception("CSV correctness / replay fields");
        }
        lab.StartSession(true); begin.Invoke(lab,null);
        // Pick a different hemisphere and height, then deliberately misreport distance.
        var current=lab.Current;
        int wrong=Enumerable.Range(0,lab.targetRoot.childCount).First(i=>ResearchModel.Face(lab.targetRoot.GetChild(i).position-lab.observer.transform.position,Vector3.forward)!=current.hemisphere && ResearchModel.Height(lab.targetRoot.GetChild(i).position-lab.observer.transform.position)!=current.height);
        lab.SelectTarget(wrong); lab.AnswerDistance((current.distance+1)%3); lab.Next(); begin.Invoke(lab,null); finish.Invoke(lab,new object[]{"timeout"});
        lab.Next(); begin.Invoke(lab,null); lab.Abort();
        var failure=File.ReadAllLines(lab.CsvPath);
        if(failure.Length!=4 || failure[1].Split(',')[18]!="False" || failure[1].Split(',')[19]!="False" || failure[1].Split(',')[20]!="False" || failure[1].Split(',')[21]!="False" || !failure[2].Contains(",timeout,") || !failure[3].Contains(",aborted,")) throw new Exception("Error / timeout / abort logging");
        if(lab.haptics.levels.Any(x=>x!=0)) throw new Exception("Haptic stop");
        if(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length!=1) throw new Exception("EventSystem count");
        lab.seed=oldSeed;
        string report="PASS: factorial balance and reproducibility for four groups; motor mapping and bounds; 144 correct trial lifecycle + CSV rows; wrong target and distance; timeout; abort; replay count; motor stop; single EventSystem. Automated synthetic responses, not participant data.";
        Directory.CreateDirectory("Verification"); File.WriteAllText("Verification/playmode-checks.txt",report);
        return report;
    }
}
