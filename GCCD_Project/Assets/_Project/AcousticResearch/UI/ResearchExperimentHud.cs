using System;
using System.Linq;
using System.IO;
using GCCD.ContinuousHaptics;
using UnityEngine;
using UnityEngine.UIElements;

namespace GCCD.AcousticResearch
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ResearchExperimentHud : MonoBehaviour
    {
        public ResearchExperimentSession Session;
        VisualElement root;
        void OnEnable()
        {
            root=GetComponent<UIDocument>().rootVisualElement.Q("root");
            if(root==null || !Session) return;
            for(int i=0;i<4;i++) { int n=i;root.Q<Button>("condition"+i).clicked+=()=>Session.Router.SetCondition((ExperimentCondition)n); }
            for(int i=0;i<3;i++) { int n=i;root.Q<Button>("policy"+i).clicked+=()=>Session.Router.Selection.Mode=(HapticSelectionMode)n; }
            for(int i=0;i<6;i++) { int n=i;root.Q<Button>("preview"+i).clicked+=()=>Session.PreviewSource(n); }
            var participant=root.Q<TextField>("participant"); participant.SetValueWithoutNotify(Session.ParticipantCode);
            participant.RegisterValueChangedCallback(e=>Session.ParticipantCode=e.newValue);
            BindInt("seed",Session.Seed,v=>Session.Seed=v);
            BindInt("count",Session.TrialCount,v=>Session.TrialCount=Mathf.Clamp(v,1,256));
            BindInt("order",Session.CounterbalanceIndex,v=>Session.CounterbalanceIndex=v);
            var hardware=root.Q<Toggle>("hardware");hardware.SetValueWithoutNotify(Session.Router.HardwareArmed);
            hardware.RegisterValueChangedCallback(e=> {Session.Router.StopAllCues();Session.Router.HardwareArmed=e.newValue;});
            var sto=root.Q<Toggle>("sto");sto.SetValueWithoutNotify(Session.Router.Sto.Enabled);
            sto.RegisterValueChangedCallback(e=> {Session.Router.StopAllCues();Session.Router.Sto.Enabled=e.newValue;});
            root.Q<Button>("practice").clicked+=()=>Session.StartSession(true);
            root.Q<Button>("measure").clicked+=()=>Session.StartSession(false);
            root.Q<Button>("stop").clicked+=()=>Session.StopSession();
            root.Q<Button>("multi").clicked+=PreviewOverlapping;
            root.Q<Button>("saveLogs").clicked+=()=>Session.SaveLogs();
            root.Q<Button>("discardLogs").clicked+=()=>Session.DiscardLogs();
        }
        static string ConditionLabel(ExperimentCondition c) => new[]{"No Feedback","Audio Only","Haptics Only","Audio + Haptics"}[(int)c];
        void BindInt(string name,int value,Action<int> apply)
        { var f=root.Q<IntegerField>(name);f.SetValueWithoutNotify(value);f.RegisterValueChangedCallback(e=>apply(e.newValue)); }
        void PreviewOverlapping()
        {
            if(Session.Running) return;
            Session.Router.StopAllCues();
            var azimuths=new[]{135f,157.5f};
            for(int i=0;i<2;i++)
            { var s=Session.Sources[i];s.transform.position=Session.Router.Player.position+Session.Player.transform.TransformDirection(HapticDirectionCalculator.ToLocal(azimuths[i],0,8));s.Trigger(Time.unscaledTimeAsDouble,8); }
            foreach(var s in Session.Maskers) s.Trigger(Time.unscaledTimeAsDouble,8);
        }
        void Update()
        {
            if(root==null || !Session) return;
            root.EnableInClassList("running",Session.Running);
            if(Session.Running) return;
            root.EnableInClassList("pending",Session.HasPendingLogs);
            root.Q<Label>("notice").text=Session.Message;
            root.Q<Label>("logResult").text=$"{ConditionLabel(Session.Router.Condition)} · {(Session.Practice?"Practice":"Measurement")}\nHits {Session.Hits} · Misses {Session.Misses} · False alarms {Session.FalseAlarms}\n{Session.Message}\nTemporary CSV: {Path.GetFileName(Session.CurrentFile)}";
            root.Q("logDecision").EnableInClassList("hidden",!Session.HasPendingLogs);
            root.Q<Button>("practice").SetEnabled(!Session.HasPendingLogs);root.Q<Button>("measure").SetEnabled(!Session.HasPendingLogs);
            for(int i=0;i<4;i++) root.Q<Button>("condition"+i).EnableInClassList("selected",i==(int)Session.Router.Condition);
            for(int i=0;i<3;i++) root.Q<Button>("policy"+i).EnableInClassList("selected",i==(int)Session.Router.Selection.Mode);
            root.Q<Label>("orderLabel").text="Recommended order: "+string.Join(" → ",ExperimentTrialPlan.ConditionOrder(Session.CounterbalanceIndex).Select(ConditionLabel));
            int clips=Session.Sources.Count(s=>s && s.Profile && s.Profile.Clip);
            root.Q<Label>("clips").text=$"Audio clips: {clips}/6 assigned. Events without clips are silent practice placeholders.";
            root.Q<Label>("status").text=Session.Message;
            root.Q<Label>("result").text=$"Hits {Session.Hits} · Misses {Session.Misses} · False alarms {Session.FalseAlarms}\nCSV: {Session.CurrentFile}";
            root.Q<Label>("diagnostics").text=$"Active cues {Session.Router.ActiveCount} · Selected {Session.Router.SelectedCount}\n{Session.Router.Status}\n{(Session.Router.Sto.Enabled?Session.Router.Sto.Status:"STO disabled / direct 3D mapping")} ({Session.Router.Sto.SolveMilliseconds:F1} ms)";
        }
    }
}
