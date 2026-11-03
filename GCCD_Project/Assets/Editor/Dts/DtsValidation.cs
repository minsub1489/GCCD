using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GCCD.Dts;
namespace GCCD.Editor
{
    public static class DtsValidation
    {
        static void Check(bool value,string message){if(!value)throw new Exception("DTS validation: "+message);}
        [MenuItem("GCCD/Distance Priority/Run Software Validation")]
        public static void Run()
        {
            DtsProjectBuilder.Generate();var go=GameObject.Find("GCCD Controlled FPS Experiment");var p=go.GetComponent<ExperimentPlatform>();
            go.SendMessage("Awake");double time=100;p.Clock=()=>time;p.DataRootOverride=Path.GetFullPath("../.test-build/validation-"+Guid.NewGuid().ToString("N"));p.Mode="DEVELOPMENT";p.Participant="SOFTWARE_TEST";
            p.StartSession();Check(p.Phase==TrialPhase.Prepare,"session start: "+p.Error);string folder=p.Folder;
            for(int i=0;i<108;i++)
            {
                if(p.Phase==TrialPhase.Rest)p.Advance();time+=5;p.Tick();Check(p.Phase==TrialPhase.Pulse,"pulse at "+i+": "+p.Error);
                time+=1;p.Tick();Check(p.Phase==TrialPhase.Response,"response opens after pulse");
                if(i==0){Check(p.AddMarker(0,DistanceBin.Near),"first marker");Check(p.AddMarker(90,DistanceBin.Middle),"second marker");Check(p.AddMarker(180,DistanceBin.Far),"third marker");for(int j=3;j<6;j++)Check(p.AddMarker(45*j,DistanceBin.Far),"extra marker");Check(!p.AddMarker(270,DistanceBin.Far),"seventh rejected");p.Markers.RemoveAt(1);p.Markers[0].response_bearing=45;}
                if(i==1){p.AddMarker(270,DistanceBin.Middle);time+=13;p.Tick();Check(p.Phase==TrialPhase.Questionnaire,"timeout transition");}
                else p.CompleteResponse();
                Check(p.Phase==TrialPhase.Questionnaire,"questionnaire at "+i);p.SaveRating(4);Check(p.Phase!=TrialPhase.Fault,"save: "+p.Error);
            }
            Check(p.Phase==TrialPhase.Finished,"108-trial finish");var records=DataExporter.Records(folder);Check(records.Length==108,"108 committed records");Check(records.All(r=>r.total_threat_count==6&&r.selected_count==6),"six eligible threats throughout");Check(records.All(r=>r.mode=="DEVELOPMENT"&&r.receipt.simulated),"simulation separation");Check(records[1].timeout&&records[1].responses.Length==1,"timeout preserves current markers");Check(records.All(r=>DataExporter.Validate(r).Length==0),"all invariants");Check(!File.Exists(Path.Combine(folder,"trials.csv")),"no automatic CSV");Check(p.SaveFinishedCsv(),"optional CSV save");Check(File.ReadAllLines(Path.Combine(folder,"trials.csv")).Length==109,"CSV headers+108");
            p.BackToSetup();p.ResumeSession(folder);Check(p.Phase==TrialPhase.Finished,"completed resume no duplication");Check(DataExporter.Records(folder).Length==108,"resume count stable");
            p.BackToSetup();p.StartSession();time+=5;p.Tick();p.AddMarker(0,DistanceBin.Near);p.Pause("test interruption");string interrupted=p.Folder;Check(DataExporter.Records(interrupted).Single().technical_failure,"pause marks technical failure");
            p.BackToSetup();p.Mode="MAIN";p.StartSession();Check(p.Phase==TrialPhase.Setup&&!string.IsNullOrEmpty(p.Error),"unverified main blocked");
            p.Mode="DEVELOPMENT";string stimuli=p.ValidateStimuli();Check(File.ReadAllLines(Path.Combine(stimuli,"outputs.csv")).Length==109,"18 layouts x 6 policies");
            Directory.CreateDirectory("../docs/validation");File.WriteAllText("../docs/validation/unity-integration.txt","PASS: 108 A trials, pulse/response/questionnaire, 0-6 markers, delete/edit, timeout preservation, separate simulation tag, invariant checks, CSV export, duplicate-free complete-session resume, technical interruption, unverified MAIN gate, 108 stimulus condition outputs.\nData: "+folder+"\nNot a hardware or human participant test.\n");
            p.BackToSetup();UnityEngine.Object.DestroyImmediate(go);DtsProjectBuilder.Generate();
            Debug.Log("DTS_SOFTWARE_VALIDATION_PASS");
        }
    }
}
