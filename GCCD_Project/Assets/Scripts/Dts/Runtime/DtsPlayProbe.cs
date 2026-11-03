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
            p.config.TotalThreatCount = 12;
            p.config.TrialsPerCondition = 7;
            var button = GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.Q<UnityEngine.UIElements.Button>("startExperiment");
            Require(button != null, "lobby start button");
            button.Focus();
            using (var submit = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            Require(p.Phase == TrialPhase.Prepare, "direct lobby start: " + p.Error);
            string folder=p.Folder;
            for(int i=0;i<42;i++)
            {
                if(p.Phase==TrialPhase.Rest)p.Advance();clock+=5;p.Tick();Require(p.Phase==TrialPhase.Pulse,"A pulse "+i+" "+p.Error);
                yield return null;clock+=1;p.Tick();Require(p.Phase==TrialPhase.Response,"A response "+i);
                if(i == 0)
                {
                    for (int j = 0; j < 12; j++) Require(p.AddMarker(30 * j, DistanceBin.Middle), "twelve response slots");
                    Require(!p.AddMarker(300, DistanceBin.Far), "thirteenth marker blocked");
                }
                else if(i%3==0)p.AddMarker(180,DistanceBin.Far);
                yield return null;p.CompleteResponse();p.SaveRating(3);Require(p.Phase!=TrialPhase.Fault,"A save "+p.Error);yield return null;
            }
            Require(p.Phase==TrialPhase.Finished,"A finish");Require(DataExporter.Records(folder).Length==42,"A committed");
            var trials = DataExporter.Records(folder);
            Require(trials.All(t => t.total_threat_count == 12 && t.selected_count == 12), "all twelve threats reach suppression");
            Require(trials.Where(t => t.condition_theta == 0).All(t => t.retained_count == 12), "baseline retains all twelve");
            Require(trials.Where(t => t.condition_theta == 45).Select(t => t.retained_count).Distinct().Count() > 1, "random layout varies retained count");
            Require(trials.Any(t => t.threats.Any(x => !x.is_offscreen && x.selected)), "front sources participate");
            Require(trials.All(t => t.retained_count + t.suppressed_count == 12), "complete partitions");
            Require(trials[0].responses.Length == 12 && trials[0].retained_recognition_rate >= 0, "scoring and retention logged");
            Require(File.ReadAllText(Path.Combine(folder, "responses.csv")).Contains("joint_correct"), "joint correctness CSV");
            Require(!File.ReadAllText(Path.Combine(folder, "threats.csv")).Contains("top3_selected"), "obsolete CSV column removed");
            var baseline = trials.First(t => t.condition_theta == 0);
            for (int channel = 0; channel < 40; channel++)
                Require(baseline.requested_motor_array[channel] == baseline.threats.Max(t => t.individual_motor_array[channel]), "all retained patterns enter the composite");
            var events = File.ReadAllLines(Path.Combine(folder, "runtime_events.csv"));
            Require(events.Count(line => line.Contains(",OUTPUT_REQUEST,")) == 42, "one combined request per trial");
            Require(Time.frameCount>initialFrame+100,"game frames advance");
            p.BackToSetup();Debug.Log("DTS_PLAYMODE_VALIDATION_PASS");EditorApplication.Exit(0);
        }
        static void Require(bool ok,string message){if(ok)return;Debug.LogError("DTS_PLAYMODE_FAILED "+message);EditorApplication.Exit(2);throw new Exception(message);}
    }
}
#endif
