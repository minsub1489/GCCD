using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace GCCD.Dts
{
    public enum TrialPhase { Setup,Prepare,Pulse,Response,Questionnaire,Rest,Finished,Paused,Fault }
    [RequireComponent(typeof(Camera))]
    public sealed partial class ExperimentPlatform : MonoBehaviour
    {
        public ExperimentConfig config=new ExperimentConfig();
        public TrialPhase Phase{get;private set;}=TrialPhase.Setup;
        public string Participant="PTEST",Mode="DEVELOPMENT";
        public string Error{get;private set;}="";
        public string Folder{get;private set;}="";
        public Func<double> Clock;
        public string DataRootOverride="";
        public int ResponseCapacity => snapshot?.Length ?? 0;
        public int CurrentIndex{get{return index;}}public int Total{get{return plan==null?0:plan.Length;}}
        public int TechnicalFailures{get;private set;}
        public int Completed{get{return store==null?0:store.Completed.Count;}}
        public TrialRecord LastRecord{get;private set;}
        public List<ResponseRecord> Markers{get;private set;}=new List<ResponseRecord>();
        readonly BhapticsAdapter haptics=new BhapticsAdapter();
        SessionStore store;SessionMetadata metadata;ExperimentConfig frozen;
        TrialSpec[] plan;int index,markerSequence;double deadline,responseOpened;
        TrialRecord record;Threat[] snapshot;
        bool operatorVisible,training,trainingPassed;string trainedParticipant="",trainedHash="";int trainingAttempts;
        string feedback="";double trainingSum;int trainingScored;
        Camera view;
        double Now{get{return Clock!=null?Clock():Time.realtimeSinceStartupAsDouble;}}
        public string ConfigPath{get{return Path.Combine(Application.persistentDataPath,"DtsSettings","experiment_config.json");}}
        string Root{get{return string.IsNullOrEmpty(DataRootOverride)?Path.Combine(Application.persistentDataPath,"ExperimentData"):DataRootOverride;}}
        public string FrozenHash(ExperimentConfig c){string old=c.LockedHash;c.LockedHash="";string result=SessionStore.Hash(JsonUtility.ToJson(c));c.LockedHash=old;return result;}
        void Awake(){if(Application.isPlaying&&!GetComponent<ModernInterface>())gameObject.AddComponent<ModernInterface>();haptics.Clock=()=>Now;view=GetComponent<Camera>();view.nearClipPlane=.05f;view.farClipPlane=100;transform.SetPositionAndRotation(new Vector3(0,1.7f,0),Quaternion.identity);SetHorizontalFov(config.HorizontalFov);}
        void SetHorizontalFov(double hfov){view.fieldOfView=(float)(2*Math.Atan(Math.Tan(hfov*Math.PI/360)/Math.Max(.1,view.aspect))*180/Math.PI);}
        public void SaveConfig(){Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));SessionStore.AtomicWrite(ConfigPath,JsonUtility.ToJson(config,true));}
        public void LoadConfig(){config=JsonUtility.FromJson<ExperimentConfig>(File.ReadAllText(ConfigPath));if(config==null||config.Mapping==null)throw new ArgumentException("Invalid config file.");}
        public void FreezeConfig()
        {
            config.LockedHash="PENDING";var problems=config.Problems(true,true).Where(p=>!p.Contains("Freeze configuration")).ToArray();config.LockedHash="";
            if(problems.Length>0)throw new InvalidOperationException(string.Join("\n",problems));
            if(!File.Exists(config.PilotEvidencePath)||SessionStore.Hash(File.ReadAllText(config.PilotEvidencePath))!=config.PilotEvidenceSha256)throw new InvalidOperationException("Pilot evidence hash mismatch.");
            config.LockedHash=FrozenHash(config);SaveConfig();
        }
        void RefreshTrainingHistory()
        {
            string parent=Path.Combine(Root,"TRAINING_"+Mode,"A",Participant);trainingAttempts=0;trainingPassed=false;
            if(!Directory.Exists(parent))return;string hash=FrozenHash(frozen);
            foreach(string folder in Directory.GetDirectories(parent))
            {
                string path=Path.Combine(folder,"session_metadata.json");if(!File.Exists(path))continue;
                string json=File.ReadAllText(path);if(!File.Exists(path+".sha256")||SessionStore.Hash(json)!=File.ReadAllText(path+".sha256"))throw new InvalidDataException("Training metadata checksum mismatch.");
                var m=JsonUtility.FromJson<SessionMetadata>(json);if(m.config_hash!=hash||m.participant_id!=Participant)continue;trainingAttempts++;
                var trials=DataExporter.Records(folder);var triples=trials.Where(t=>t.total_threat_count==frozen.ThreatCountFor(Mode=="DEVELOPMENT")&&!t.technical_failure).ToArray();
                if(trials.Length==3+frozen.TrainingTripleTrials&&trials.All(t=>!t.technical_failure)&&triples.Length>=frozen.TrainingMinimumTrials&&triples.Length>0&&frozen.TrainingMinimumJointAccuracy>=0&&triples.Average(t=>t.joint_accuracy)>=frozen.TrainingMinimumJointAccuracy)
                {trainingPassed=true;trainedParticipant=Participant;trainedHash=hash;}
            }
        }
        public void StartSession(bool asTraining=false)
        {
            if(Phase!=TrialPhase.Setup&&Phase!=TrialPhase.Finished){Error="Return to setup before starting.";return;}
            try
            {
                SessionStore.ValidateId(Participant);training=asTraining;
                frozen=JsonUtility.FromJson<ExperimentConfig>(JsonUtility.ToJson(config));
                bool development=Mode=="DEVELOPMENT",main=Mode=="MAIN"&&!training;
                if(!new[]{"DEVELOPMENT","PILOT","MAIN"}.Contains(Mode))throw new ArgumentException("Invalid mode.");
                if(development&&frozen.DirectionTolerance<0)frozen.DirectionTolerance=15; // explicitly development-only provisional tolerance
                RefreshTrainingHistory();
                if(training){if(trainingAttempts>=frozen.TrainingMaximumRetries+1&&frozen.TrainingMaximumRetries>=0)throw new InvalidOperationException("Training retry limit reached. Apply the configured exit rule.");if(frozen.TrainingTripleTrials<1||frozen.TrainingTripleTrials>108)throw new ArgumentException("Training triple trials must be 1..108.");}
                var problems=frozen.Problems(main,!development);
                if(problems.Length>0)throw new InvalidOperationException(string.Join("\n",problems));
                if(!development){string reason;if(!haptics.Ready(frozen,out reason))throw new InvalidOperationException(reason);}
                if(main)
                {
                    if(frozen.LockedHash!=FrozenHash(frozen))throw new InvalidOperationException("Locked configuration changed.");
                    if(!File.Exists(frozen.PilotEvidencePath)||SessionStore.Hash(File.ReadAllText(frozen.PilotEvidencePath))!=frozen.PilotEvidenceSha256)throw new InvalidOperationException("Pilot evidence changed/missing.");
                    if(StimulusValidator.MaximumPairIdentity(ScenarioGenerator.PlanA(frozen.Seed,frozen.CounterbalanceRow,frozen.HorizontalFov,frozen.ThreatCountFor(Mode=="DEVELOPMENT")),frozen.Mapping,frozen.Low,frozen.High,frozen.PulseMilliseconds)>frozen.PilotMaximumIdenticalPairRate)throw new InvalidOperationException("Stimulus identity exceeds the preregistered maximum pair rate.");
                    if(!trainingPassed||trainedParticipant!=Participant||trainedHash!=FrozenHash(frozen))throw new InvalidOperationException("This participant must pass training with the locked configuration.");
                }
                if(store!=null)store.Dispose();
                string session=DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
                plan=CreatePlan();
                string dataMode=training?"TRAINING_"+Mode:Mode;
                Folder=Path.Combine(Root,dataMode,"A",Participant,session);
                metadata=new SessionMetadata{participant_id=Participant,session_id=session,experiment_type="A",mode=dataMode,created_utc=DateTime.UtcNow.ToString("O"),unity_version=Application.unityVersion,sdk_version=frozen.SdkPackageVersion,config_hash=FrozenHash(frozen),mapping_version=frozen.Mapping.Version,config=frozen,block_order=ConditionScheduler.Order(frozen.CounterbalanceRow),training_passed=trainingPassed};
                store=new SessionStore(Folder,Participant,training?"TRAINING":Mode,metadata.experiment_type,JsonUtility.ToJson(metadata,true));
                if(training){trainingAttempts++;trainingPassed=false;}
                index=0;TechnicalFailures=0;trainingSum=0;trainingScored=0;Error="";operatorVisible=false;Prepare();
            }
            catch(Exception e){Error=e.GetBaseException().Message;Phase=TrialPhase.Setup;}
        }
        TrialSpec[] CreatePlan()
        {
            var a=ScenarioGenerator.PlanA(frozen.Seed,frozen.CounterbalanceRow,frozen.HorizontalFov,frozen.ThreatCountFor(Mode=="DEVELOPMENT"));
            if(training)
            {
                var t=new TrialSpec[3+frozen.TrainingTripleTrials];for(int i=0;i<t.Length;i++){var source=a[i%a.Length];t[i]=new TrialSpec{Id="TR"+(i+1).ToString("D3"),Theta=0,Block=0,Family=source.Family,Seed=source.Seed,Preparation=source.Preparation,Threats=i<3?source.Threats.Take(1).ToArray():source.Threats};}return t;
            }
            return a;
        }
        public void ResumeSession(string folder)
        {
            if(Phase!=TrialPhase.Setup&&Phase!=TrialPhase.Finished){Error="Resume only from setup.";return;}
            try
            {
                string json=File.ReadAllText(Path.Combine(folder,"session_metadata.json"));var m=JsonUtility.FromJson<SessionMetadata>(json);
                if(m!=null&&(m.experiment_type!="A"||m.software_version!=SessionMetadata.CurrentSoftwareVersion))
                    throw new InvalidOperationException("이 세션은 이전 실험 방식으로 저장됐습니다. 데이터 검토에서 CSV를 열 수 있습니다.");
                if(m==null||m.config==null||m.config_hash!=FrozenHash(m.config))throw new InvalidOperationException("Metadata/config hash mismatch.");
                Participant=m.participant_id;training=m.mode.StartsWith("TRAINING_");Mode=training?m.mode.Substring(9):m.mode;
                if(Mode!="DEVELOPMENT"){var errors=m.config.Problems(Mode=="MAIN"&&!training,true);if(errors.Length>0)throw new InvalidOperationException(string.Join(";",errors));string reason;if(!haptics.Ready(m.config,out reason))throw new InvalidOperationException(reason);}
                if(Mode=="MAIN"&&!training&&(m.config.LockedHash!=m.config_hash||!m.training_passed||!File.Exists(m.config.PilotEvidencePath)||SessionStore.Hash(File.ReadAllText(m.config.PilotEvidencePath))!=m.config.PilotEvidenceSha256))throw new InvalidOperationException("MAIN resume prerequisites changed.");
                if(store!=null)store.Dispose();Folder=folder;metadata=m;frozen=m.config;
                store=new SessionStore(folder,Participant,training?"TRAINING":Mode,"A",json,true);
                foreach(string id in store.Pending)
                {
                    var pending=JsonUtility.FromJson<TrialRecord>(File.ReadAllText(Path.Combine(folder,"journal",id+".started.json")));
                    pending.technical_failure=true;pending.interrupted=true;pending.error="Interrupted before durable commit; response/output timing is unknown.";pending.output_status="INTERRUPTED_UNCONFIRMED";
                    store.Commit(id,JsonUtility.ToJson(pending,true));
                }
                DataExporter.Export(folder);var records=DataExporter.Records(folder);TechnicalFailures=records.Count(t=>t.technical_failure);
                trainingSum=records.Where(t=>t.is_training&&!t.technical_failure&&t.total_threat_count==frozen.ThreatCountFor(Mode=="DEVELOPMENT")).Sum(t=>t.joint_accuracy);trainingScored=records.Count(t=>t.is_training&&!t.technical_failure&&t.total_threat_count==frozen.ThreatCountFor(Mode=="DEVELOPMENT"));
                plan=CreatePlan();index=0;while(index<plan.Length&&store.Completed.Contains(plan[index].Id))index++;operatorVisible=false;Error="";if(index>=plan.Length)Finish();else Phase=TrialPhase.Rest;
            }
            catch(Exception e){Error=e.GetBaseException().Message;Phase=TrialPhase.Setup;}
        }
        void Prepare()
        {
            if(index>=plan.Length){Finish();return;}
            transform.SetPositionAndRotation(new Vector3(0,1.7f,0),Quaternion.identity);SetHorizontalFov(frozen.HorizontalFov);
            Markers.Clear();markerScroll=Vector2.zero;selectedMarker=-1;markerSequence=0;feedback="";record=null;LastRecord=null;
            deadline=Now+plan[index].Preparation;Phase=TrialPhase.Prepare;
            SetCursor(false);
        }
        public void Tick()
        {
            if(frozen==null)return;
            try
            {
                SetHorizontalFov(frozen.HorizontalFov);
                if(Phase==TrialPhase.Prepare&&Now>=deadline)Emit();
                if(Phase==TrialPhase.Pulse&&Now>=deadline)
                {
                    record.actual_pulse_wait_seconds=Now-record.receipt.submit_returned;
                    store.AppendEvent(record.trial_id,"OUTPUT_STOP",haptics.Stop());
                    responseOpened=Now;record.response_open=Now;deadline=Now+12;SetCursor(false);
                    Phase=TrialPhase.Response;store.AppendEvent(record.trial_id,"RESPONSE_OPEN",Now.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                }
                if(Phase==TrialPhase.Response&&Now>=deadline)CompleteResponse(true);
                if(Mode!="DEVELOPMENT"&&(Phase==TrialPhase.Pulse||Phase==TrialPhase.Response)){string reason;if(!haptics.Ready(frozen,out reason))Fail("Device disconnected: "+reason);}
            }
            catch(Exception e){Fail(e.GetBaseException().Message);}
        }
        void Update()
        {
            UpdateEasyConnection();
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (Phase == TrialPhase.Setup) advanced = !advanced;
                else
                {
                    if (IsRunning()) Pause("Researcher dashboard opened during trial.");
                    operatorVisible = !operatorVisible;
                }
            }
            if(Input.GetKeyDown(KeyCode.Escape)&&IsRunning())Pause("Operator/participant pause.");
            Tick();
        }
        bool IsRunning(){return Phase==TrialPhase.Prepare||Phase==TrialPhase.Pulse||Phase==TrialPhase.Response||Phase==TrialPhase.Questionnaire;}
        void Emit()
        {
            var spec=plan[index];double yaw=transform.eulerAngles.y;
            // Shared firing event. All positions and policy are frozen here, never updated during the pulse.
            snapshot=spec.Threats.Select(t=>
            {
                Vector3 p=transform.position+Quaternion.Euler(0,(float)yaw,0)*new Vector3((float)t.X,0,(float)t.Z);
                Vector3 local=Quaternion.Inverse(Quaternion.Euler(0,(float)yaw,0))*(p-transform.position);
                double bearing=Angles.Bearing(local.x,local.z,0),distance=Vector3.Distance(p,transform.position);
                bool offscreen=Angles.IsOffscreen(bearing,frozen.HorizontalFov+2); // extra conservative target-radius margin
                if(!offscreen)throw new InvalidOperationException("Actual camera viewport offscreen validation failed.");
                return new Threat(t.Id,true,offscreen,true,p.x,p.y,p.z,distance,bearing);
            }).ToArray();
            var policy=ThreatPolicy.Apply(snapshot,spec.Theta);int[] motors=MotorComposer.Compose(policy.Kept,frozen.Mapping,frozen.Low,frozen.High);
            double eventAt=Now;
            record=new TrialRecord{participant_id=Participant,session_id=metadata.session_id,experiment_type=metadata.experiment_type,mode=metadata.mode,trial_id=spec.Id,utc=DateTime.UtcNow.ToString("O"),event_utc=DateTime.UtcNow.ToString("O"),condition_theta=spec.Theta,block_index=spec.Block,condition_order=string.Join(";",metadata.block_order),scene_family=spec.Family,random_seed=spec.Seed,event_time=eventAt,camera_heading=yaw,total_threat_count=snapshot.Length,selected_count=policy.Selected.Length,retained_count=policy.Kept.Length,requested_motor_array=motors,output_duration=frozen.PulseMilliseconds,config_hash=metadata.config_hash,is_training=training};
            var ranked=snapshot.OrderBy(t=>t.Distance).ThenBy(t=>t.Id,StringComparer.Ordinal).ToArray();
            record.threats=snapshot.Select(t=>
            {
                var d=policy.Decisions.FirstOrDefault(x=>x.Threat.Id==t.Id);
                return new ThreatRecord{threat_id=t.Id,is_active=t.Active,is_firing=t.Firing,is_offscreen=t.Offscreen,world_x=t.X,world_y=t.Y,world_z=t.Z,distance_m=t.Distance,bearing_deg=t.Bearing,distance_rank=Array.IndexOf(ranked,t)+1,selected=d!=null,top3_selected=d!=null&&d.Rank<=3,retained=d!=null&&d.Retained,suppression_parent_id=d==null?"":d.ParentId,reason=d==null?"ineligible":d.Reason,comparisons=d==null?"":string.Join(";",d.Comparisons.Select(c=>c.ComparedId+":"+c.Difference.ToString("R",System.Globalization.CultureInfo.InvariantCulture))),individual_motor_array=MotorComposer.Compose(new[]{t},frozen.Mapping,frozen.Low,frozen.High)};
            }).ToArray();
            store.Begin(record.trial_id,JsonUtility.ToJson(record,true));
            store.AppendEvent(record.trial_id,"THREAT_EVENT",JsonUtility.ToJson(record));
            store.AppendEvent(record.trial_id,"OUTPUT_REQUEST",string.Join(";",motors));
            record.receipt=haptics.Submit(motors,frozen.PulseMilliseconds,frozen,Mode=="DEVELOPMENT");record.output_status=record.receipt.status;
            store.AppendEvent(record.trial_id,"OUTPUT_RESULT",JsonUtility.ToJson(record.receipt));
            if(!record.receipt.submitted){Fail(record.receipt.error);return;}
            Phase=TrialPhase.Pulse;deadline=Now+frozen.PulseMilliseconds/1000.0;
        }
        public bool AddMarker(double bearing,DistanceBin bin)
        {
            if(Phase!=TrialPhase.Response||Now>=deadline||Markers.Count>=snapshot.Length)return false;
            Markers.Add(new ResponseRecord{marker_id=++markerSequence,response_bearing=Angles.Normalize(bearing),response_distance_bin=bin.ToString(),response_time=Now-responseOpened});return true;
        }
        public void CompleteResponse(bool timedOut=false)
        {
            if(Phase!=TrialPhase.Response)return;
            record.timeout=timedOut||Now>=deadline;record.response_time=Math.Min(12,Math.Max(0,Now-responseOpened));record.responses=Markers.ToArray();ScoreCurrent();Phase=TrialPhase.Questionnaire;
        }
        void ScoreCurrent()
        {
            var original = ThreatPolicy.Apply(snapshot, 0).Selected;
            var responses = record.responses.Select(r => new Response(r.response_bearing,
                (DistanceBin)Enum.Parse(typeof(DistanceBin), r.response_distance_bin))).ToArray();
            var score = ResponseScorer.Score(original, responses, frozen.DirectionTolerance);
            record.joint_accuracy = score.JointAccuracy;
            record.direction_accuracy = score.DirectionAccuracy;
            record.distance_accuracy = score.DistanceAccuracy;
            record.direction_error = score.DirectionError ?? -1;
            record.response_precision = score.Precision ?? -1;
            record.false_response_rate = score.FalseResponseRate;
            record.no_response_rate = score.NoResponseRate;
            record.ambiguous_matching = score.Ambiguous;
            record.matched_threat_indices = score.MatchedThreatIndices;
            record.matched_threat_ids = score.MatchedThreatIndices.Select(i => original[i].Id).ToArray();
            record.distance_confusion = new int[9];
            for (int i = 0; i < 9; i++) record.distance_confusion[i] = score.Confusion[i / 3, i % 3];
        }
        public void SaveRating(int rating){if(Phase!=TrialPhase.Questionnaire||rating<1||rating>7)return;record.confusion_rating=rating;Commit();}
        void Commit()
        {
            try
            {
                if(record==null)throw new InvalidOperationException("No trial snapshot to commit.");
                if(!record.technical_failure){var errors=DataExporter.Validate(record);if(errors.Length>0)throw new InvalidOperationException(string.Join(";",errors));}
                store.Commit(record.trial_id,JsonUtility.ToJson(record,true));DataExporter.Export(Folder);LastRecord=record;
                if(record.technical_failure)TechnicalFailures++;
                if(training&&!record.technical_failure&&snapshot.Length==frozen.ThreatCountFor(Mode=="DEVELOPMENT")){trainingSum+=record.joint_accuracy;trainingScored++;}
                if(training)feedback=record.technical_failure?"기술 실패: 채점 제외":string.Join(" / ",snapshot.Select(t=>t.Bearing.ToString("F1")+"° "+DistanceEncoding.Bin(t.Distance)))+"\n공동 정답률: "+record.joint_accuracy.ToString("P0");
                haptics.Stop();SetCursor(false);index++;record=null;
                if(index>=plan.Length){Finish();return;}
                Phase=TrialPhase.Rest;
                if(!training&&plan[index].Block==plan[index-1].Block)Prepare();
            }
            catch(Exception e){haptics.Stop();Error="Data save/integrity failure: "+e.GetBaseException().Message;Phase=TrialPhase.Fault;SetCursor(false);}
        }
        void Finish()
        {
            haptics.Stop();SetCursor(false);Phase=TrialPhase.Finished;
            if(training){trainingPassed=frozen.TrainingMinimumJointAccuracy>=0&&trainingScored>=frozen.TrainingMinimumTrials&&trainingScored>0&&trainingSum/trainingScored>=frozen.TrainingMinimumJointAccuracy&&TechnicalFailures==0;if(trainingPassed){trainedParticipant=Participant;trainedHash=metadata.config_hash;}SessionStore.AtomicWrite(Path.Combine(Folder,"training_result.json"),"{\"passed\":"+trainingPassed.ToString().ToLowerInvariant()+",\"scored_full_layout_trials\":"+trainingScored+",\"mean_joint_accuracy\":"+(trainingScored==0?"null":(trainingSum/trainingScored).ToString("R",System.Globalization.CultureInfo.InvariantCulture))+"}");}
        }
        public void Advance(){if(Phase==TrialPhase.Rest||Phase==TrialPhase.Paused){operatorVisible=false;Prepare();}}
        public void Pause(string reason)
        {
            haptics.Stop();if(record!=null){record.responses=Markers.ToArray();record.technical_failure=true;record.interrupted=true;record.error=reason;record.output_status="INTERRUPTED";Commit();}
            if(Phase!=TrialPhase.Fault&&Phase!=TrialPhase.Finished)Phase=TrialPhase.Paused;SetCursor(false);
        }
        void Fail(string reason)
        {
            haptics.Stop();Error=reason;
            if(record!=null&&store!=null&&!store.Completed.Contains(record.trial_id)){record.technical_failure=true;record.error=reason;record.responses=Markers.ToArray();try{Commit();}catch(Exception e){Error+=" | "+e.Message;}}
            Phase=TrialPhase.Fault;SetCursor(false);
        }
        public void BackToSetup(){if(IsRunning())Pause("Session stopped at operator request.");haptics.Stop();if(store!=null){store.Dispose();store=null;}Phase=TrialPhase.Setup;operatorVisible=false;}
        static void SetCursor(bool locked){Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;}
        void OnApplicationFocus(bool focus){if(!focus&&IsRunning())Pause("Application lost focus.");}
        void OnApplicationPause(bool paused){if(paused&&IsRunning())Pause("Application paused.");}
        void OnDestroy(){haptics.Dispose();if(store!=null){if(record!=null&&!store.Completed.Contains(record.trial_id)){record.technical_failure=true;record.interrupted=true;record.error="Application terminated.";try{store.Commit(record.trial_id,JsonUtility.ToJson(record,true));DataExporter.Export(Folder);}catch(Exception){/* started journal remains recoverable */}}store.Dispose();}}
    }
}
