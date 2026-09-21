using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD
{
    public class ThreatLab : MonoBehaviour
    {
        public Camera observer;
        public Transform targetRoot;
        public Transform blocker;
        public HapticOutput haptics;
        public AudioSource signal;
        public AudioLowPassFilter lowPass;
        public int seed=42;
        public float trialLimit=20;
        public bool practice=true;
        public string State => state;
        public int TrialIndex => index;
        public string CsvPath => csvPath;
        public int Completed => completed;
        public Trial Current => schedule[index];
        readonly List<Transform> nodes=new List<Transform>();
        List<Trial> schedule;
        string state="MENU",csvPath="",notice="",sessionId;
        int index,completed,correct,replays,selected=-1,answerDistance=-1;
        float yaw,pitch,onset,waitUntil,rotationTravel,lastYaw,selectedTime;
        Vector3 onsetForward,sourcePosition;
        int sourceFace,sourceHeight,sourceDistance;
        bool suspended, outputAtOnset;
        StreamWriter writer;
        LabUI ui;
        Material normalMaterial,selectedMaterial;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;

        void Awake()
        {
            haptics=GetComponent<HapticOutput>();
            foreach(Transform t in targetRoot) nodes.Add(t);
            normalMaterial=nodes[0].GetComponent<Renderer>().sharedMaterial;
            selectedMaterial=new Material(normalMaterial); selectedMaterial.color=new Color(0.25f,1,0.75f);
            selectedMaterial.EnableKeyword("_EMISSION"); selectedMaterial.SetColor("_EmissionColor",new Color(0.1f,0.8f,0.5f));
            signal.clip=CreateSignal();
            ui=gameObject.AddComponent<LabUI>(); ui.lab=this;
        }
        AudioClip CreateSignal()
        {
            const int rate=48000; float[] samples=new float[(int)(rate*0.65f)];
            var random=new System.Random(1701);
            for(int i=0;i<samples.Length;i++) {
                float t=(float)i/rate;
                float envelope=Mathf.Min(1,t/0.012f)*Mathf.Min(1,(0.65f-t)/0.08f);
                float pulse=0.35f+0.65f*Mathf.Pow(Mathf.Sin(t*26),2);
                samples[i]=envelope*pulse*(0.18f*Mathf.Sin(2*Mathf.PI*(700*t+400*t*t))+0.12f*(float)(random.NextDouble()*2-1));
            }
            var clip=AudioClip.Create("Threat charge • generated",samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }
        public void StartSession(bool isPractice)
        {
            CloseLog(); practice=isPractice; schedule=ResearchModel.Schedule(seed,practice);
            index=completed=correct=0; sessionId=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff",Inv);
            try {
                string folder=Path.Combine(Application.persistentDataPath,"Sessions"); Directory.CreateDirectory(folder);
                csvPath=Path.Combine(folder,"gccd_"+sessionId+".csv");
                writer=new StreamWriter(csvPath,false,new UTF8Encoding(false)) { AutoFlush=true };
                writer.WriteLine("session,utc,seed,practice,visual_motor_preview,udp_enabled_at_onset,trial,condition,occluded,source_face,source_height,source_distance,source_x,source_y,source_z,response_face,response_height,response_distance,target_correct,face_correct,height_correct,distance_correct,selection_ms,total_response_ms,replays,yaw_travel_deg,outcome,audio_backend");
            } catch(Exception e) { notice="Cannot save results: "+e.Message; state="MENU"; CloseLog(); return; }
            Prepare();
        }
        void Prepare()
        {
            state="READY"; selected=answerDistance=-1; replays=0; rotationTravel=0; yaw=pitch=lastYaw=0;
            observer.transform.rotation=Quaternion.identity;
            signal.Stop(); haptics.Stop(); blocker.gameObject.SetActive(false);
            foreach(var n in nodes) n.GetComponent<Renderer>().sharedMaterial=normalMaterial;
            var trial=Current;
            for(int i=0;i<nodes.Count;i++) {
                int level=i/6-1, bearing=i%6;
                float angle=new float[]{-145,-90,-35,35,90,145}[bearing];
                nodes[i].position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,trial.Radius)+new Vector3(0,4.5f+level*3.5f,0);
            }
            sourcePosition=trial.Position;
            signal.transform.position=sourcePosition;
            // Identical full ring of baffles: no wall placement identifies the active source.
            blocker.gameObject.SetActive(trial.occluded);
            waitUntil=Time.unscaledTime+1.3f;
            Unlock(); notice="Face the central beacon. Signal begins shortly.";
        }
        void BeginCue()
        {
            state="ACTIVE"; onset=Time.unscaledTime;
            onsetForward=observer.transform.forward; onsetForward.y=0; onsetForward.Normalize();
            var delta=sourcePosition-observer.transform.position;
            sourceFace=ResearchModel.Face(delta,onsetForward); sourceHeight=ResearchModel.Height(delta); sourceDistance=ResearchModel.Distance(delta.magnitude);
            outputAtOnset=haptics.udpEnabled;
            PlayCue(); notice="Locate the charging drone. Hold right mouse to look.";
        }
        void PlayCue()
        {
            // Prototype occlusion: documented gain + low-pass, not Steam Audio or measured HRTF.
            signal.volume=Current.occluded ? 0.28f : 0.75f;
            lowPass.cutoffFrequency=Current.occluded ? 1300 : 22000;
            signal.Play();
            haptics.Pulse(ResearchModel.Motors(Current.condition,sourceFace,sourceHeight,sourceDistance,haptics.pairedGain));
        }
        public void Replay()
        {
            if(state!="ACTIVE" || suspended) return;
            replays++; PlayCue();
        }
        void Update()
        {
            if(ui==null) return;
            if(state=="READY" && !suspended && Time.unscaledTime>=waitUntil) BeginCue();
            if(state=="ACTIVE" && !suspended) {
                var mouse=Mouse.current; var keyboard=Keyboard.current;
                if(mouse!=null && mouse.rightButton.isPressed) {
                    Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
                    Vector2 d=mouse.delta.ReadValue(); yaw+=d.x*0.13f; pitch=Mathf.Clamp(pitch-d.y*0.13f,-65,65);
                } else Unlock();
                if(keyboard!=null) {
                    float turn=(keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0);
                    yaw+=turn*85*Time.unscaledDeltaTime;
                    pitch=Mathf.Clamp(pitch+((keyboard.downArrowKey.isPressed?1:0)-(keyboard.upArrowKey.isPressed?1:0))*65*Time.unscaledDeltaTime,-65,65);
                    if(keyboard.rKey.wasPressedThisFrame) Replay();
                    if(keyboard.spaceKey.wasPressedThisFrame) SelectAimedTarget();
                    if(keyboard.escapeKey.wasPressedThisFrame) Abort();
                }
                observer.transform.rotation=Quaternion.Euler(pitch,yaw,0);
                rotationTravel+=Mathf.Abs(Mathf.DeltaAngle(lastYaw,yaw)); lastYaw=yaw;
                if(Time.unscaledTime-onset>=trialLimit) FinishTrial("timeout");
            }
            ui.Refresh();
        }
        public void SelectAimedTarget()
        {
            if(state!="ACTIVE" || suspended) return;
            int best=-1; float angle=8;
            for(int i=0;i<nodes.Count;i++) {
                float a=Vector3.Angle(observer.transform.forward,nodes[i].position-observer.transform.position);
                if(a<angle) { angle=a; best=i; }
            }
            if(best<0) { notice="Aim closer to a drone, then press SPACE."; return; }
            SelectTarget(best);
        }
        public void SelectTarget(int nodeIndex)
        {
            if(state!="ACTIVE" || nodeIndex<0 || nodeIndex>=nodes.Count) return;
            selected=nodeIndex; selectedTime=Time.unscaledTime-onset;
            nodes[selected].GetComponent<Renderer>().sharedMaterial=selectedMaterial;
            state="DISTANCE"; signal.Stop(); haptics.Stop(); Unlock();
            notice="How far away was the signal? Choose your estimate.";
        }
        public void AnswerDistance(int d)
        {
            if(state!="DISTANCE" || d<0 || d>2) return;
            answerDistance=d; FinishTrial("answered");
        }
        void FinishTrial(string outcome)
        {
            signal.Stop(); haptics.Stop(); Unlock();
            int face=-1,height=-9; bool hit=false;
            if(selected>=0) {
                var delta=nodes[selected].position-observer.transform.position;
                face=ResearchModel.Face(delta,onsetForward); height=ResearchModel.Height(delta);
                hit=Vector3.Distance(nodes[selected].position,sourcePosition)<0.1f;
            }
            float elapsed=Time.unscaledTime-onset;
            string F(float n)=>n.ToString("0.###",Inv);
            writer?.WriteLine(string.Join(",",sessionId,DateTime.UtcNow.ToString("O",Inv),seed,practice,practice,outputAtOnset,index+1,Current.condition,Current.occluded,
                sourceFace,sourceHeight,sourceDistance,F(sourcePosition.x),F(sourcePosition.y),F(sourcePosition.z),face,height,answerDistance,hit,face==sourceFace,height==sourceHeight,answerDistance==sourceDistance,
                selected>=0?F(selectedTime*1000):"",F(elapsed*1000),replays,F(rotationTravel),outcome,"Unity3D_Pan_LowPass_Prototype"));
            if(outcome=="aborted" || outcome=="focus_lost") { state="MENU"; CloseLog(); notice="Session stopped. Partial results saved."; return; }
            completed++; if(hit) correct++;
            state="FEEDBACK";
            notice=practice ? (hit?"TARGET CONFIRMED":"TARGET MISSED")+"  •  "+(sourceFace==0?"FRONT":"BACK")+" / "+HeightName(sourceHeight)+" / "+DistanceName(sourceDistance) : "Response saved. Continue when ready.";
            if(practice) foreach(var n in nodes) if(Vector3.Distance(n.position,sourcePosition)<0.1f) n.GetComponent<Renderer>().sharedMaterial=selectedMaterial;
        }
        public void Next()
        {
            if(state!="FEEDBACK") return;
            index++;
            if(index>=schedule.Count) { state="COMPLETE"; CloseLog(); notice="Session complete. CSV saved locally."; }
            else Prepare();
        }
        public void Abort()
        {
            if(state=="ACTIVE" || state=="DISTANCE") FinishTrial("aborted");
            else { state="MENU"; signal.Stop(); haptics.Stop(); CloseLog(); Unlock(); }
        }
        void OnApplicationFocus(bool focus)
        {
            suspended=!focus;
            if(!focus && (state=="ACTIVE" || state=="DISTANCE")) FinishTrial("focus_lost");
            if(!focus) { signal?.Stop(); haptics?.Stop(); Unlock(); }
            if(focus && state=="READY") waitUntil=Time.unscaledTime+1.3f;
        }
        public string Heading => state=="MENU" ? "OFFSCREEN / THREAT LAB" : state=="COMPLETE" ? "SESSION COMPLETE" : "TRIAL "+(index+1).ToString("00")+" / "+schedule.Count;
        public string ConditionLabel => schedule==null || state=="MENU" || state=="COMPLETE" ? "SPATIAL AUDIO + FOUR-POINT HAPTICS" : Current.condition.ToString().ToUpperInvariant();
        public string Notice => notice;
        public string Progress => completed+" responses  /  "+correct+" targets confirmed";
        public float Remaining => state=="ACTIVE" ? Mathf.Max(0,trialLimit-(Time.unscaledTime-onset)) : 0;
        public float Progress01 => schedule==null ? 0 : (float)completed/schedule.Count;
        public static string HeightName(int h)=>h>0?"UPPER":h<0?"LOWER":"SAME LEVEL";
        public static string DistanceName(int d)=>d==0?"NEAR":d==1?"MID":"FAR";
        public void OpenResults() { if(!string.IsNullOrEmpty(csvPath)) Application.OpenURL(new Uri(Path.GetDirectoryName(csvPath)).AbsoluteUri); }
        void Unlock() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        void CloseLog() { writer?.Dispose(); writer=null; }
        void OnDestroy() { CloseLog(); Unlock(); if(selectedMaterial) Destroy(selectedMaterial); if(signal && signal.clip) Destroy(signal.clip); }
    }
}
