using System;
using GCCD.ContinuousHaptics;
using UnityEngine;

namespace GCCD.SilentResearch
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SilentStimulusOutput : MonoBehaviour
    {
        public SilentHapticPatterns Patterns;
        public HapticVestOutput Vest;
        public Transform View;
        public AudioClip Footsteps, Door, Growl;
        [Range(0, 1)] public float AudioGain = .7f;
        [Range(.1f, 3)] public float ExposureSec = 1;
        public OutputMode Mode;
        public SilentPilotController Pilot;
        public bool X40CalibrationVerified => Pilot && Pilot.CalibrationMatches();
        readonly int[] motors = new int[40];
        HapticPulse[] pulses;
        AudioSource audioSource;
        SoundEvent current;
        FeedbackCondition condition;
        int nextPulse;
        double pulseEnd;
        bool active;
        public int Motor(int i) => motors[i];
        public bool Active => active;
        public string Status { get; private set; } = "Idle";
        public double LastDispatch { get; private set; }
        public bool AudioAudible => audioSource && audioSource.isPlaying && !audioSource.mute;
        public bool HardwareFailed { get; private set; }
        public double MaxDispatchJitterMs { get; private set; }
        public event Action<SoundEvent> Emitted;
        void Awake()
        {
            audioSource = GetComponent<AudioSource>(); audioSource.Stop(); audioSource.playOnAwake = false;
            audioSource.loop = false; audioSource.spatialBlend = 1; audioSource.dopplerLevel = 0;
            audioSource.rolloffMode = AudioRolloffMode.Linear; audioSource.minDistance = 1; audioSource.maxDistance = 25;
        }
        public AudioClip Clip(EventType type) => type == EventType.Footsteps ? Footsteps : type == EventType.Door ? Door : Growl;
        public bool Ready(FeedbackCondition value, bool practice, out string reason)
        {
            reason = "";
            if (!View || !Patterns || !Patterns.Valid || !StudyClock.Finite(AudioGain) || AudioGain <= 0 || AudioGain > 1)
            { reason = "Invalid stimulus configuration"; return false; }
            if (!StudyClock.Finite(ExposureSec) || ExposureSec < .1f || ExposureSec > 3)
            { reason = "Invalid sensory exposure window"; return false; }
            foreach (var pattern in new[] { Patterns.Basic, Patterns.Footsteps, Patterns.Door, Patterns.Growl })
                foreach (var pulse in pattern) if (pulse.StartMs + pulse.DurationMs > ExposureSec * 1000)
                { reason = "Haptic pattern exceeds the common sensory exposure window"; return false; }
            if (value == FeedbackCondition.AudioOnly && (!Footsteps || !Door || !Growl))
            { reason = "Assign all three non-silent audio clips"; return false; }
            if (value != FeedbackCondition.AudioOnly && !practice)
            {
                if (Mode != OutputMode.BHaptics || !(Vest is BHapticsVestOutput real) || !real.CheckConnection() || !X40CalibrationVerified)
                { reason = "Measurement requires the calibrated X40, SDK2, and a live connection"; return false; }
            }
            return true;
        }
        public void Begin(SoundEvent value, FeedbackCondition feedback)
        {
            Stop(); current = value; condition = feedback; HardwareFailed = false; MaxDispatchJitterMs = 0;
            // The same event object drives every condition. Catches emit no sensory cue.
            if (!value.IsThreat || value.Type == EventType.None) { Status = "Silent catch"; return; }
            transform.position = value.Position;
            pulses = Patterns.Select(feedback, value.Type); nextPulse = 0; active = true;
            Status = feedback == FeedbackCondition.AudioOnly ? "Audio-only event" : "Haptic event";
            if (audioSource)
            {
                audioSource.mute = feedback != FeedbackCondition.AudioOnly;
                audioSource.volume = AudioGain;
                if (feedback == FeedbackCondition.AudioOnly) { audioSource.clip = Clip(value.Type); audioSource.Play(); }
            }
            Tick(value.Onset); Emitted?.Invoke(value);
        }
        void Update() { if (active) Tick(StudyClock.Now); }
        public void Tick(double now)
        {
            if (!active) return;
            if (now >= current.Onset + ExposureSec) { Stop(); return; }
            if (pulseEnd > 0 && now >= pulseEnd) { Array.Clear(motors, 0, 40); Vest?.Stop(); pulseEnd = 0; }
            if (pulses != null && nextPulse < pulses.Length && now >= current.Onset + pulses[nextPulse].StartMs / 1000.0)
            {
                var p = pulses[nextPulse++];
                MaxDispatchJitterMs = Math.Max(MaxDispatchJitterMs, (now - current.Onset - p.StartMs / 1000.0) * 1000);
                double scheduledEnd = current.Onset + (p.StartMs + p.DurationMs) / 1000.0;
                // Never replay overdue pulses after a frame stall.
                if (now >= scheduledEnd) { HardwareFailed = true; Status = "Missed pulse deadline"; return; }
                float azimuth = 0;
                if (!SilentTrialPlan.TryBearing(View, current.Position, out var direction))
                { HardwareFailed = true; Status = "Undefined event direction"; return; }
                azimuth = direction.Azimuth;
                SilentHapticPatterns.Map(p.Intensity, condition == FeedbackCondition.TypeDirectionHaptic, azimuth, motors);
                int duration = Math.Min(p.DurationMs, Math.Max(1, (int)Math.Ceiling((scheduledEnd - now) * 1000)));
                LastDispatch = now; pulseEnd = scheduledEnd;
                if (Mode == OutputMode.BHaptics)
                {
                    bool sent = Vest && Vest.TrySend(motors, duration);
                    HardwareFailed |= !sent; Status = Vest ? Vest.Status : "No vest adapter";
                }
                else { Vest?.Stop(); Status = "Simulation (not participant measurement)"; }
            }
            if (nextPulse == (pulses?.Length ?? 0) && pulseEnd == 0 && (!audioSource || !audioSource.isPlaying)) active = false;
        }
        public void Stop()
        { active = false; pulseEnd = 0; pulses = null; Array.Clear(motors, 0, 40); Vest?.Stop(); if (audioSource) { audioSource.Stop(); audioSource.mute = true; } Status = "Stopped"; }
        void OnDisable() => Stop();
        void OnApplicationPause(bool pause) { if (pause) Stop(); }
        void OnApplicationFocus(bool focus) { if (!focus) Stop(); }
    }
}
