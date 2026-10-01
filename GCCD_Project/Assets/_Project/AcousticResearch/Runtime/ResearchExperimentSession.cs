using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GCCD.ContinuousHaptics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.AcousticResearch
{
    public sealed class ResearchExperimentSession : MonoBehaviour
    {
        public AcousticHapticsRouter Router;
        public ResearchPlayerMotion Player;
        public ResearchSoundEmitter[] Sources;
        public ResearchSoundEmitter[] Maskers;
        public GameObject ThreatVisual;
        public string ParticipantCode="P001";
        public int Seed=1489, TrialCount=24, CounterbalanceIndex;
        public float ResponseWindow=8, DirectionTolerance=30;
        public bool Record=true;
        public bool Running { get; private set; }
        public bool Practice { get; private set; }
        public int TrialIndex { get; private set; }=-1;
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int FalseAlarms { get; private set; }
        public string Message { get; private set; }="Choose condition, then start practice.";
        public string CurrentFile { get; private set; }
        public bool HasPendingLogs => !Running && pendingFile!=null && File.Exists(pendingFile);
        string pendingFile;
        public IReadOnlyList<ResearchTrial> Plan => plan;
        List<ResearchTrial> plan;
        ResearchTrial trial;
        ResearchSoundEmitter current;
        double onset, deadline, nextTrialAt, firstVisibleAt, shootAllowedAt;
        string fixedConfiguration;
        bool emitted, preparing;
        int shotCount;
        double firstShotAt;
        ExperimentCondition fixedCondition;
        Vector3 home;
        Quaternion homeRotation;
        StreamWriter writer;
        void Awake() { if(Player) { home=Player.transform.position; homeRotation=Player.transform.rotation; } if(ThreatVisual) ThreatVisual.SetActive(false); }
        void OnEnable() { if(Router) Router.FrameComputed+=Frame; }
        void OnDisable() { if(Router) Router.FrameComputed-=Frame; StopSession("Component disabled"); }
        public bool ReadyForMeasurement(out string reason)
        {
            reason="";
            if(!Router || !Player || !Player.View || !ThreatVisual || Sources==null || Sources.Length!=6 || Maskers==null || Maskers.Length<2)
            { reason="Scene wiring incomplete"; return false; }
            foreach(var s in Sources) if(!s || !s.Profile || !s.Profile.Valid) { reason="Invalid sound profile"; return false; }
            if(ConditionChannels.Audio(Router.Condition))
            {
                foreach(var s in Sources) if(!s.Profile.Clip) { reason="Add all six audio clips before an auditory measurement block"; return false; }
                foreach(var s in Maskers) if(!s || !s.Profile || !s.Profile.Clip) { reason="Assign masker clips before measurement"; return false; }
            }
            if(ConditionChannels.Haptics(Router.Condition))
            {
                var output=Router.Output as BHapticsVestOutput;
                if(!Router.HardwareArmed || !output || !output.CheckConnection())
                { reason="Arm hardware and connect the calibrated X40 before a haptic measurement block"; return false; }
            }
            if(ConditionChannels.Haptics(Router.Condition) && Router.Sto != null && Router.Sto.Enabled && !Router.Sto.X40CalibrationVerified)
            { reason="STO uses an uncalibrated X40 model. Disable STO for the primary study or complete calibration first.";return false; }
            return true;
        }
        public bool StartSession(bool practice)
        {
            if(HasPendingLogs) { Message="Choose Save Logs or Discard Logs before starting another block."; return false; }
            StopSession("New block");
            if(!practice && !ReadyForMeasurement(out string reason)) { Message=reason; return false; }
            if(!Router || !Player || TrialCount<1 || TrialCount>256 || ResponseWindow<.5f || !HapticMath.Finite(ResponseWindow)) return false;
            fixedCondition=Router.Condition; Practice=practice; plan=ExperimentTrialPlan.Create(Seed,TrialCount);
            Hits=Misses=FalseAlarms=0; TrialIndex=-1; Running=true;
            try { if(Record) OpenRecord(); }
            catch(Exception e) { Running=false; Message="Recording unavailable: "+e.GetType().Name; return false; }
            fixedConfiguration=Configuration(); shootAllowedAt=Time.unscaledTimeAsDouble+.2;
            Player.MeasurementMode=true; Player.CaptureCursor(true); Message=practice?"PRACTICE (not measurement)":"Measurement block";
            BeginTrial(Time.unscaledTimeAsDouble); return true;
        }
        void BeginTrial(double now)
        {
            Router.StopAllCues(); if(ThreatVisual) ThreatVisual.SetActive(false);
            if(++TrialIndex>=plan.Count) { StopSession("Block complete"); return; }
            Player.ResetPose(home,homeRotation); Player.LookEnabled=false; current=null; trial=plan[TrialIndex]; emitted=false; preparing=true;
            onset=now+trial.Foreperiod; deadline=onset+ResponseWindow; firstVisibleAt=-1;firstShotAt=-1;shotCount=0;
            foreach(var s in Sources) if(s.Profile.Category==trial.Category) { current=s; break; }
            if(!current) { StopSession("Missing target source"); return; }
            current.transform.position=Router.Player.position+Player.transform.TransformDirection(
                HapticDirectionCalculator.ToLocal(trial.Azimuth,trial.Elevation,trial.Distance));
            var initialView=Player.View.WorldToViewportPoint(current.transform.position);
            if(initialView.z>0 && initialView.x>=0 && initialView.x<=1 && initialView.y>=0 && initialView.y<=1)
            {
                trial.Azimuth=Mathf.Repeat(trial.Azimuth+180,360);
                current.transform.position=Router.Player.position+Player.transform.TransformDirection(
                    HapticDirectionCalculator.ToLocal(trial.Azimuth,trial.Elevation,trial.Distance));
            }
            if(ThreatVisual) ThreatVisual.transform.position=current.transform.position;
            Write("trial_prepared",now,0,0,"pending");
        }
        void Update()
        {
            if(!Running) return;
            double now=Time.unscaledTimeAsDouble;
            if(Router.Condition!=fixedCondition) { StopSession("Condition changed during block; aborted"); return; }
            if(Configuration()!=fixedConfiguration) { StopSession("Settings changed during block; aborted");return; }
            if(!Practice && ConditionChannels.Haptics(fixedCondition) && !(Router.Output as BHapticsVestOutput).CheckConnection())
            { StopSession("X40 disconnected; block aborted");return; }
            var k=Keyboard.current;
            if(k!=null && k.escapeKey.wasPressedThisFrame) { StopSession("Participant paused; block aborted"); return; }
            if(!preparing) { if(now>=nextTrialAt) BeginTrial(now); return; }
            if(!emitted && now>=onset)
            {
                // Timestamp the actual main-thread trigger, including scheduling jitter.
                onset=now; deadline=now+ResponseWindow; emitted=true; Player.LookEnabled=true;
                if(!trial.CatchTrial) { current.Trigger(now,ResponseWindow); if(ThreatVisual) ThreatVisual.SetActive(true); }
                if(trial.Masked) foreach(var m in Maskers) if(m) m.Trigger(now,ResponseWindow);
                Write("cue_onset",now,0,0,trial.CatchTrial?"catch":"threat");
            }
            if(emitted && !trial.CatchTrial && firstVisibleAt<0)
            {
                var vp=Player.View.WorldToViewportPoint(current.transform.position);
                if(vp.z>Player.View.nearClipPlane && vp.x>=0 && vp.x<=1 && vp.y>=0 && vp.y<=1) firstVisibleAt=now;
            }
            if(k!=null && k.spaceKey.wasPressedThisFrame || Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame)
                if(now>=shootAllowedAt && Cursor.lockState==CursorLockMode.Locked) Shoot(now);
            if(preparing && now>=deadline) { if(!trial.CatchTrial) Misses++; FinishTrial(now,trial.CatchTrial?"correct_rejection":"miss",0,0); }
        }
        public bool Shoot(double now)
        {
            if(!Running || !preparing) return false;
            shotCount++;if(firstShotAt<0)firstShotAt=now;
            if(!emitted || trial.CatchTrial) { FalseAlarms++; FinishTrial(now,"false_alarm",0,0); return false; }
            Vector3 delta=current.transform.position-Router.Player.position;
            float error=Vector3.Angle(Player.View.transform.forward,delta);
            if(Physics.Raycast(Player.View.transform.position,Player.View.transform.forward,out var hit,100,~(1<<2),QueryTriggerInteraction.Ignore))
            {
                var target=hit.collider.GetComponentInParent<ResearchShootTarget>();
                if(target && target.Session==this && target.gameObject==ThreatVisual) { target.Hit(now);return true; }
            }
            Write("missed_shot",now,(float)(now-onset),error,"shot_missed",JsonUtility.ToJson(new ShotRecord {shots=shotCount,firstShotSeconds=(float)(firstShotAt-onset)}));
            return false;
        }
        [Serializable] sealed class ShotRecord { public int shots; public float firstShotSeconds; }
        public void TargetHit(ResearchShootTarget target,double now)
        {
            if(!Running || !preparing || !emitted || trial.CatchTrial || target.gameObject!=ThreatVisual) return;
            Hits++;
            float error=Vector3.Angle(Player.View.transform.forward,current.transform.position-Router.Player.position);
            FinishTrial(now,"hit",(float)(now-onset),error);
        }
        void FinishTrial(double now,string outcome,float reaction,float error)
        { Write("response",now,reaction,error,outcome,JsonUtility.ToJson(new ShotRecord {shots=shotCount,firstShotSeconds=firstShotAt<0?-1:(float)(firstShotAt-onset)})); Router.StopAllCues(); if(ThreatVisual) ThreatVisual.SetActive(false); preparing=false; nextTrialAt=now+1; }
        public void StopSession(string reason="Stopped")
        {
            if(Running) Write("block_end",Time.unscaledTimeAsDouble,0,0,reason);
            Running=false; preparing=false; Router?.StopAllCues(); if(ThreatVisual) ThreatVisual.SetActive(false);
            if(Player) { Player.MeasurementMode=false; Player.LookEnabled=true; Player.CaptureCursor(false); }
            Message=reason; CloseRecord();
        }
        public bool PreviewSource(int index)
        { if(Running || Sources==null || index<0 || index>=Sources.Length) return false; return Sources[index].Trigger(Time.unscaledTimeAsDouble); }
        void OpenRecord()
        {
            string folder=Path.Combine(Application.persistentDataPath,"AcousticResearch","Pending"); Directory.CreateDirectory(folder);
            CurrentFile=Path.Combine(folder,fixedCondition+"_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
            pendingFile=CurrentFile;
            writer=new StreamWriter(CurrentFile,false,Encoding.UTF8){AutoFlush=true};
            writer.WriteLine("entry,utc,time,participant,practice,condition,selection,trial,category,azimuth,elevation,distance,catch,planned_masking,reaction_seconds,aim_error_degrees,first_visible_seconds,outcome,audio_clip_present,audio_playing,hardware_armed,sdk_sent,detail_json");
            File.WriteAllText(Path.ChangeExtension(CurrentFile,"settings.json"),Configuration());
        }
        public bool SaveLogs()
        {
            if(!HasPendingLogs) return false;
            try
            {
                string folder=Path.Combine(Application.persistentDataPath,"AcousticResearch","Saved");Directory.CreateDirectory(folder);
                string destination=Path.Combine(folder,Path.GetFileName(pendingFile));
                // Copy both files before retiring temporary originals, preserving recovery on failure.
                File.Copy(pendingFile,destination,false);
                try {File.Copy(Path.ChangeExtension(pendingFile,"settings.json"),Path.ChangeExtension(destination,"settings.json"),false);}
                catch {File.Delete(destination);throw;}
                File.Delete(pendingFile);File.Delete(Path.ChangeExtension(pendingFile,"settings.json"));
                pendingFile=null;CurrentFile=destination;Message="Logs saved.";return true;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
            {Message="Save failed; temporary logs retained: "+e.GetType().Name;return false;}
        }
        public bool DiscardLogs()
        {
            if(!HasPendingLogs) return false;
            try
            {File.Delete(pendingFile);File.Delete(Path.ChangeExtension(pendingFile,"settings.json"));pendingFile=null;CurrentFile=null;Message="Logs discarded.";return true;}
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
            {Message="Discard failed: "+e.GetType().Name;return false;}
        }
        [Serializable] sealed class SettingsRecord { public string session,router; public string[] profiles; }
        string Configuration()
        {
            var profiles=new List<string>();
            if(Sources!=null) foreach(var s in Sources) if(s && s.Profile) profiles.Add(JsonUtility.ToJson(s.Profile));
            return JsonUtility.ToJson(new SettingsRecord {session=JsonUtility.ToJson(this),router=JsonUtility.ToJson(Router),profiles=profiles.ToArray()},true);
        }
        void Write(string entry,double now,float reaction,float error,string outcome,string details="")
        {
            if(writer==null) return;
            string visible=firstVisibleAt<0?"":(firstVisibleAt-onset).ToString("R",CultureInfo.InvariantCulture);
            var fields=new[]{entry,DateTime.UtcNow.ToString("O"),now.ToString("R",CultureInfo.InvariantCulture),ParticipantCode,
                Practice?"1":"0",fixedCondition.ToString(),Router.Selection.Mode.ToString(),TrialIndex.ToString(),trial.Category.ToString(),
                trial.Azimuth.ToString(CultureInfo.InvariantCulture),trial.Elevation.ToString(CultureInfo.InvariantCulture),trial.Distance.ToString(CultureInfo.InvariantCulture),
                trial.CatchTrial?"1":"0",trial.Masked?"1":"0",reaction.ToString("R",CultureInfo.InvariantCulture),error.ToString("R",CultureInfo.InvariantCulture),
                visible,outcome,current&&current.Profile.Clip?"1":"0",current&&current.AudioPlaying?"1":"0",Router.HardwareArmed?"1":"0",Router.LastSent?"1":"0",details};
            for(int i=0;i<fields.Length;i++) fields[i]="\""+(fields[i]??"").Replace("\"","\"\"")+"\"";
            try { writer.WriteLine(string.Join(",",fields)); }
            catch(IOException) { CloseRecord(); Running=false; Router.StopAllCues(); if(ThreatVisual) ThreatVisual.SetActive(false); Player.CaptureCursor(false); Message="Recording failed; block aborted"; }
        }
        [Serializable] sealed class DecisionRecord { public string source,reason; public float snr,score,distance,azimuth,elevation; public bool masked,selected,offscreen; public int[] motors; public string stoStatus; public float stoMilliseconds; public float[] renderedAngles; }
        void Frame(AcousticHapticsRouter r)
        {
            if(!Running || !emitted) return;
            foreach(var d in r.Decisions)
            {
                var values=new int[40]; for(int i=0;i<40;i++) values[i]=r.Motor(i);
                Write("haptic_decision",Time.unscaledTimeAsDouble,0,0,d.Reason,JsonUtility.ToJson(new DecisionRecord {
                    source=d.Emitter.Profile.Category.ToString(),reason=d.Reason,snr=d.SnrDb,score=d.Score,distance=d.Direction.Distance,
                    azimuth=d.Direction.Azimuth,elevation=d.Direction.Elevation,masked=d.Masked,selected=d.Selected,offscreen=d.Offscreen,motors=values,stoStatus=r.Sto.Status,stoMilliseconds=r.Sto.SolveMilliseconds,renderedAngles=r.Sto.RenderedAngles }));
            }
        }
        void CloseRecord() { if(writer==null)return; try { writer.Dispose(); } catch(IOException){} writer=null; }
        void OnApplicationFocus(bool focus) { if(!focus && Running) StopSession("Focus lost; block aborted"); }
        void OnApplicationPause(bool pause) { if(pause && Running) StopSession("Application paused; block aborted"); }
        void OnApplicationQuit() => StopSession("Application quit");
    }
}
