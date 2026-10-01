using System;
using System.Collections.Generic;
using GCCD.ContinuousHaptics;
using UnityEngine;

namespace GCCD.AcousticResearch
{
    [DisallowMultipleComponent]
    public sealed class AcousticHapticsRouter : MonoBehaviour
    {
        public Transform Player;
        public Camera ViewCamera;
        public HapticVestOutput Output;
        [SerializeField] ExperimentCondition condition;
        public bool HardwareArmed;
        public DistanceIntensityMapper Distance = new DistanceIntensityMapper();
        public HapticSpatialMapper Spatial = new HapticSpatialMapper();
        public ThreatSelectionPolicy Selection = new ThreatSelectionPolicy();
        public StoTorsoOptimizer Sto = new StoTorsoOptimizer();
        [Range(5,30)] public float UpdateRate = 10;
        [Min(.01f)] public float SmoothingSpeed = 12;
        readonly List<ResearchSoundEmitter> emitters = new List<ResearchSoundEmitter>();
        readonly List<CueDecision> decisions = new List<CueDecision>();
        readonly float[] mix = new float[40], mapped = new float[40], smooth = new float[40];
        readonly int[] motors = new int[40];
        double nextTick, lastTick;
        bool suspended, unfocused;
        ResearchSoundEmitter previousBest;
        public ExperimentCondition Condition => condition;
        public IReadOnlyList<CueDecision> Decisions => decisions;
        public int ActiveCount => emitters.Count;
        public int SelectedCount { get; private set; }
        public string Status { get; private set; } = "Ready / hardware disarmed";
        public bool LastSent { get; private set; }
        public int Motor(int index) => motors[index];
        public event Action<AcousticHapticsRouter> FrameComputed;
        public sealed class CueDecision
        {
            public ResearchSoundEmitter Emitter;
            public HapticDirection Direction;
            public bool Offscreen, Masked, Eligible, Selected;
            public float SnrDb, Score, Intensity, Power;
            public string Reason;
        }
        public void Register(ResearchSoundEmitter e) { if (e && !emitters.Contains(e)) emitters.Add(e); }
        public void Unregister(ResearchSoundEmitter e) { emitters.Remove(e); if (emitters.Count == 0) StopOutput("No active cues"); }
        public void SetCondition(ExperimentCondition value)
        {
            if (!Enum.IsDefined(typeof(ExperimentCondition), value)) throw new ArgumentOutOfRangeException(nameof(value));
            StopOutput("Condition changed"); condition = value;
            foreach (var e in emitters) if (e) e.SetAudible(ConditionChannels.Audio(value));
            nextTick = 0;
        }
        void Update()
        {
            for (int i = emitters.Count - 1; i >= 0; i--)
            { var e = emitters[i]; if (!e || !e.CueActive) emitters.RemoveAt(i); else e.Expire(Time.unscaledTimeAsDouble); }
            if (!HardwareArmed || !ConditionChannels.Haptics(condition)) Output?.Stop();
            if (suspended || unfocused) { StopOutput("Paused / unfocused"); return; }
            if (Time.unscaledTimeAsDouble >= nextTick) Tick(Time.unscaledTimeAsDouble);
        }
        public void Tick(double now)
        {
            if (!isActiveAndEnabled || suspended || unfocused || !Player || !ViewCamera || !ViewCamera.isActiveAndEnabled
                || Distance == null || Spatial == null || Selection == null || !Distance.IsValid || !Spatial.IsValid || !Selection.Valid
                || !HapticMath.Finite(UpdateRate) || UpdateRate < 5 || UpdateRate > 30
                || !HapticMath.Finite(SmoothingSpeed) || SmoothingSpeed <= 0 || double.IsNaN(now) || double.IsInfinity(now))
            { StopOutput("Invalid or inactive configuration"); return; }
            float dt = lastTick > 0 ? Mathf.Clamp((float)(now-lastTick),0,1) : 1/UpdateRate;
            lastTick = now; nextTick = now + 1/UpdateRate;
            decisions.Clear(); Array.Clear(mix,0,40); SelectedCount = 0;
            foreach (var e in emitters)
            {
                if (!e || !e.CueActive || !HapticDirectionCalculator.TryCalculate(Player,e.transform.position,0,out var direction)) continue;
                if (!Distance.TryEvaluate(direction.Distance,out float close,out float intensity)) continue;
                Vector3 vp = ViewCamera.WorldToViewportPoint(e.transform.position);
                bool offscreen = !HapticMath.Finite(vp) || vp.z < ViewCamera.nearClipPlane || vp.z > ViewCamera.farClipPlane
                    || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1;
                decisions.Add(new CueDecision { Emitter=e, Direction=direction, Offscreen=offscreen,
                    Intensity=intensity, Power=e.Profile.RelativePower*e.Profile.Gain*e.Profile.Gain*close*close });
            }
            foreach (var d in decisions)
            {
                float masking = 0;
                foreach (var other in decisions) if (other != d)
                    masking += other.Power * ThreatSelectionPolicy.BandOverlap(d.Emitter.Profile.Band,other.Emitter.Profile.Band);
                d.SnrDb = ThreatSelectionPolicy.Snr(d.Power,masking);
                d.Masked = masking > .00000001f && d.SnrDb < Selection.MaskingSnrThresholdDb;
                d.Eligible = d.Intensity > Distance.MinimumPerceptibleIntensity
                    && (!d.Emitter.TreatAsMasker || Selection.Mode == HapticSelectionMode.AllSources)
                    && Selection.Eligible(d.Offscreen,d.Emitter.Profile,d.Masked);
                float close = 1-Mathf.InverseLerp(Distance.NearDistance,Distance.FarDistance,d.Direction.Distance);
                d.Score = ThreatSelectionPolicy.Score(d.Emitter.Profile,close,d.Masked);
                // Small hysteresis avoids rapidly swapping equally important overlapping threats.
                if (d.Emitter == previousBest) d.Score += Selection.SwitchMargin;
                d.Reason = d.Eligible ? "Eligible" : !d.Offscreen && Selection.Mode != HapticSelectionMode.AllSources ? "On screen"
                    : (!d.Emitter.Profile.IsThreat || d.Emitter.TreatAsMasker) && Selection.Mode != HapticSelectionMode.AllSources ? "Non-threat"
                    : "Below risk / not masked / outside range";
            }
            decisions.Sort((a,b) => { int n=b.Score.CompareTo(a.Score); return n!=0?n:a.Emitter.GetInstanceID().CompareTo(b.Emitter.GetInstanceID()); });
            int limit = Selection.Mode == HapticSelectionMode.MaskingPriority ? Selection.MaximumSelected : int.MaxValue;
            previousBest = null;
            foreach (var d in decisions)
            {
                if (!d.Eligible || SelectedCount >= limit) { if (d.Eligible) d.Reason="Lower priority"; continue; }
                if (!Spatial.TryMap(d.Direction.Azimuth,d.Direction.Elevation,mapped,out _)) continue;
                for(int i=0;i<40;i++) mix[i] += mapped[i]*d.Intensity;
                d.Selected=true; d.Reason="Selected"; SelectedCount++; if (!previousBest) previousBest=d.Emitter;
            }
            float total=0; for(int i=0;i<40;i++) total+=mix[i];
            if (Sto != null && Sto.Enabled && Sto.TryOptimize(decisions,Spatial,Distance.MaximumIntensity,mapped))
            { Array.Copy(mapped,mix,40); total=0;for(int i=0;i<40;i++) total+=mix[i]; }
            float scale=total > Distance.MaximumIntensity ? Distance.MaximumIntensity/total : 1;
            if (SelectedCount == 0) { StopOutput("No eligible offscreen threat"); FrameComputed?.Invoke(this); return; }
            float alpha = HapticMath.Alpha(SmoothingSpeed,dt), budget=0;
            for(int i=0;i<40;i++) { smooth[i]=Mathf.Lerp(smooth[i],mix[i]*scale,alpha); budget+=smooth[i]; }
            budget=Mathf.Min(budget,Distance.MaximumIntensity);
            if (budget <= Distance.MinimumPerceptibleIntensity) Array.Clear(motors,0,40);
            else HapticSpatialMapper.TryQuantize(smooth,budget,motors);
            LastSent=false;
            if (!ConditionChannels.Haptics(condition)) { Output?.Stop(); Status="Condition: no haptics"; }
            else if (!HardwareArmed) { Output?.Stop(); Status="Haptic preview / hardware disarmed"; }
            else if (!Output || !Output.isActiveAndEnabled || budget <= Distance.MinimumPerceptibleIntensity)
            { Output?.Stop(); Status="Output unavailable / dead zone"; }
            else { LastSent=Output.TrySend(motors,Mathf.Clamp(Mathf.CeilToInt(1000/UpdateRate)+20,100,220)); Status=Output.Status; }
            FrameComputed?.Invoke(this);
        }
        public void StopAllCues()
        { for(int i=emitters.Count-1;i>=0;i--) if(emitters[i]) emitters[i].StopCue(); emitters.Clear(); decisions.Clear(); StopOutput("Stopped"); }
        public void StopOutput(string reason="Stopped")
        { Output?.Stop(); Array.Clear(motors,0,40); Array.Clear(smooth,0,40); LastSent=false; SelectedCount=0; previousBest=null; Status=reason; }
        void OnDisable() => StopAllCues();
        void OnDestroy() => StopAllCues();
        void OnApplicationQuit() => StopAllCues();
        void OnApplicationPause(bool v) { suspended=v; if(v) StopAllCues(); }
        void OnApplicationFocus(bool v) { unfocused=!v; if(!v) StopAllCues(); }
    }
}
