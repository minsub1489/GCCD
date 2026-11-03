#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed class DtsPlayProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            var p=GetComponent<ExperimentPlatform>();int initialFrame=Time.frameCount;double clock=10000;
            p.Clock=()=>clock;p.DataRootOverride=Path.GetFullPath("../.test-build/playmode-"+Guid.NewGuid().ToString("N"));p.Participant="PLAYMODE_TEST";p.Mode="DEVELOPMENT";
            p.config.TotalThreatCount = 5;
            var button = GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.Q<UnityEngine.UIElements.Button>("startExperiment");
            Require(button != null, "lobby start button");
            button.Focus();
            using (var submit = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            Require(p.Phase == TrialPhase.Prepare, "direct lobby start: " + p.Error);
            string folder=p.Folder;
            for(int i=0;i<108;i++)
            {
                if(p.Phase==TrialPhase.Rest)p.Advance();clock+=5;p.Tick();Require(p.Phase==TrialPhase.Pulse,"A pulse "+i+" "+p.Error);
                yield return null;clock+=1;p.Tick();Require(p.Phase==TrialPhase.Response,"A response "+i);
                if(i == 0)
                {
                    for (int j = 0; j < 5; j++) Require(p.AddMarker(60 * j, DistanceBin.Middle), "five response slots");
                    Require(!p.AddMarker(300, DistanceBin.Far), "sixth marker blocked");
                }
                else if(i%3==0)p.AddMarker(180,DistanceBin.Far);
                yield return null;p.CompleteResponse();p.SaveRating(3);Require(p.Phase!=TrialPhase.Fault,"A save "+p.Error);yield return null;
            }
            Require(p.Phase==TrialPhase.Finished,"A finish");Require(DataExporter.Records(folder).Length==108,"A committed");
            var trials = DataExporter.Records(folder);
            Require(trials.All(t => t.total_threat_count == 5 && t.selected_count == 5), "all five threats reach suppression");
            Require(trials.Where(t => t.condition_theta == 0).All(t => t.retained_count == 5), "baseline retains all five");
            Require(trials.Where(t => t.condition_theta == 45).Select(t => t.retained_count).Distinct().Count() > 1, "random layout varies retained count");
            Require(Time.frameCount>initialFrame+100,"game frames advance");
            p.BackToSetup();Debug.Log("DTS_PLAYMODE_VALIDATION_PASS");EditorApplication.Exit(0);
        }
        static void Require(bool ok,string message){if(ok)return;Debug.LogError("DTS_PLAYMODE_FAILED "+message);EditorApplication.Exit(2);throw new Exception(message);}
    }
}
#endif
