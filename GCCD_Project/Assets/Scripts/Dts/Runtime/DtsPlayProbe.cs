#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEditor;
namespace GCCD.Dts
{
    public sealed class DtsPlayProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            var p=GetComponent<ExperimentPlatform>();int initialFrame=Time.frameCount;double clock=10000;
            p.Clock=()=>clock;p.DataRootOverride=Path.GetFullPath("../.test-build/playmode-"+Guid.NewGuid().ToString("N"));p.Participant="PLAYMODE_TEST";p.Mode="DEVELOPMENT";
            p.StartSession();string folder=p.Folder;
            for(int i=0;i<108;i++)
            {
                if(p.Phase==TrialPhase.Rest)p.Advance();clock+=5;p.Tick();Require(p.Phase==TrialPhase.Pulse,"A pulse "+i+" "+p.Error);
                yield return null;clock+=1;p.Tick();Require(p.Phase==TrialPhase.Response,"A response "+i);
                if(i%3==0)p.AddMarker(180,DistanceBin.Far);
                yield return null;p.CompleteResponse();p.SaveRating(3);Require(p.Phase!=TrialPhase.Fault,"A save "+p.Error);yield return null;
            }
            Require(p.Phase==TrialPhase.Finished,"A finish");Require(DataExporter.Records(folder).Length==108,"A committed");
            p.BackToSetup();p.Experiment="B";p.config.CandidateTheta=45;p.config.CandidateSelectionRule="SOFTWARE TEST ONLY";p.config.CandidateEvidence="synthetic test; no A candidate selected";p.config.FirstActionDeadline=4;p.config.FrontTaskSectorDegrees=30;
            p.StartSession();Require(p.Phase==TrialPhase.Prepare,"B start "+p.Error);string bfolder=p.Folder;
            for(int i=0;i<24;i++)
            {
                if(p.Phase==TrialPhase.Rest)p.Advance();clock+=5;p.Tick();Require(p.Phase==TrialPhase.Pulse,"B pulse "+i+" "+p.Error);yield return null;Physics.SyncTransforms();
                string started=Directory.GetFiles(Path.Combine(bfolder,"journal"),"*.started.json").OrderBy(f=>f).Last();var record=JsonUtility.FromJson<TrialRecord>(File.ReadAllText(started));
                var nearest=record.threats.OrderBy(t=>t.distance_m).First();
                // Exercise the same Raycast path as user input: frontal task first is excluded.
                p.ShootRay(Vector3.forward);clock+=.1;
                if(i%3==0){Vector3 position=new Vector3((float)nearest.world_x,(float)nearest.world_y,(float)nearest.world_z);p.ShootRay((position-transform.position).normalized);}
                if(i%3==1)p.ShootRay(Vector3.back+Vector3.up*3); // external miss permanently fails
                clock+=5;p.Tick();Require(p.Phase!=TrialPhase.Fault,"B save "+p.Error);yield return null;
            }
            Require(p.Phase==TrialPhase.Finished,"B finish");var b=DataExporter.Records(bfolder);Require(b.Length==24,"B 24 events");
            Require(b.Where((x,i)=>i%3==0).All(x=>x.game_success),"B nearest first hits");Require(b.Where((x,i)=>i%3==1).All(x=>!x.game_success&&!x.game_timeout),"B first misses");Require(b.Where((x,i)=>i%3==2).All(x=>x.game_timeout),"B nonresponses timeout");
            Require(b.All(x=>x.threats.Length==5&&x.selected_count==3&&x.game_actions[0].front_task),"B five threats and front task exclusion");
            Require(Time.frameCount>initialFrame+100,"game frames advance");
            File.WriteAllText("../docs/validation/unity-playmode.txt","PASS actual Unity Play Mode: 108 A trials and 24 B events.\nFrames advanced: "+(Time.frameCount-initialFrame)+"\nB raycast: nearest first hit / external miss / no response timeout; frontal target excluded; all 5 sources logged and nearest 3 selected.\nSynthetic SOFTWARE TEST ONLY. No human responses or physical haptics.\nA: "+folder+"\nB: "+bfolder+"\n");
            p.BackToSetup();Debug.Log("DTS_PLAYMODE_VALIDATION_PASS");EditorApplication.Exit(0);
        }
        static void Require(bool ok,string message){if(ok)return;Debug.LogError("DTS_PLAYMODE_FAILED "+message);EditorApplication.Exit(2);throw new Exception(message);}
    }
}
#endif
