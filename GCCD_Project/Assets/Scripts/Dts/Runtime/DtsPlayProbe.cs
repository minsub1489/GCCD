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
            var ui = GetComponent<UIDocument>().rootVisualElement;
            ui.Q<DropdownField>("studyAngle").index = 4;
            ui.Q<DropdownField>("studyThreatRange").index = 1;
            button.Focus();
            using (var submit = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            Require(p.Phase == TrialPhase.Prepare, "direct lobby start: " + p.Error);
            string folder=p.Folder;
            for (int i = 0; i < 7; i++)
            {
                clock += 5; p.Tick(); Require(p.Phase == TrialPhase.Pulse, p.Error);
                yield return null;
                clock += 1; p.Tick(); Require(p.Phase == TrialPhase.Response, p.Error);
                string id = "A" + (i+1).ToString("D3");
                var trial = JsonUtility.FromJson<TrialRecord>(File.ReadAllText(Path.Combine(folder,"journal",id+".started.json")));
                var source = trial.threats[0];
                var bin = DistanceEncoding.Bin(source.distance_m);
                p.AddMarker(source.bearing_deg, bin);
                p.AddMarker(source.bearing_deg, bin);
                double far = Enumerable.Range(0, 360).First(a => trial.threats.All(t => Angles.Difference(t.bearing_deg,a) >= 15));
                if(i%2==0) p.AddMarker(far, bin);
                p.CompleteResponse(); p.SaveRating(3);
                Require(p.Phase != TrialPhase.Fault, p.Error);
                yield return null;
            }
            var trials = DataExporter.Records(folder);
            Require(p.Phase == TrialPhase.Finished && trials.Length == 7, "selected condition finishes");
            Require(trials.All(t => t.condition_theta == 45 && t.total_threat_count >= 4 && t.total_threat_count <= 9 &&
                t.minimum_threat_count == 4 && t.maximum_threat_count == 9), "selected angle and range logged");
            Require(trials.Select(t=>t.total_threat_count).Distinct().Count() > 1, "range varies between trials");
            for(int i=0;i<trials.Length;i++)
            {
                var t=trials[i];
                Require(t.far_false_mark_count == (i%2==0 ? 1 : 0), "only far mark penalized");
                Require(t.adjusted_joint_correct_count == Math.Max(0,t.joint_correct_count-t.far_false_mark_count), "adjusted correct count");
            }
            string csv = Path.Combine(folder,"trials.csv");
            Require(!Directory.GetFiles(folder,"*.csv").Any(), "CSV waits for choice");
            p.BackToSetup();
            Require(!Directory.GetFiles(folder,"*.csv").Any(), "skip leaves CSV absent");
            p.ResumeSession(folder);
            Require(p.SaveFinishedCsv() && File.Exists(csv), "explicit export after resume");
            p.BackToSetup();Debug.Log("DTS_PLAYMODE_VALIDATION_PASS");EditorApplication.Exit(0);
        }
        static void Require(bool ok,string message){if(ok)return;Debug.LogError("DTS_PLAYMODE_FAILED "+message);EditorApplication.Exit(2);throw new Exception(message);}
    }
}
#endif
