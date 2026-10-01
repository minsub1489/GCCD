using UnityEngine;

namespace GCCD.AcousticResearch
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class ResearchSoundEmitter : MonoBehaviour
    {
        public SoundCueProfile Profile;
        public AcousticHapticsRouter Router;
        public bool TreatAsMasker;
        AudioSource source;
        double endsAt;
        bool active;
        public bool CueActive => active && isActiveAndEnabled && Profile && Profile.Valid;
        public bool AudioPlaying => source && source.isPlaying && !source.mute;
        public AudioSource Source => source;
        public double StartedAt { get; private set; }
        public int Sequence { get; private set; }
        void Awake()
        {
            source = GetComponent<AudioSource>(); source.playOnAwake = false; source.Stop();
            source.spatialBlend = 1; source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f; source.maxDistance = 15; source.dopplerLevel = 0;
        }
        public bool Trigger(double now, float duration = -1)
        {
            if (!Router || !Profile || !Profile.Valid || !isActiveAndEnabled || double.IsNaN(now) || double.IsInfinity(now)) return false;
            StopCue(); StartedAt = now; endsAt = now + (duration > 0 ? Mathf.Clamp(duration, .1f, 120) : Profile.Duration);
            active = true; Sequence++; Router.Register(this);
            if (source) { source.clip = Profile.Clip; source.loop = Profile.Loop; source.volume = Profile.Gain; }
            SetAudible(ConditionChannels.Audio(Router.Condition));
            return true;
        }
        public void Expire(double now) { if (active && now >= endsAt) StopCue(); }
        public void SetAudible(bool audible)
        {
            if (!source) return;
            source.mute = !audible;
            if (!CueActive || !Profile.Clip || Profile.Clip.length <= 0) { source.Stop(); return; }
            if (!audible) { source.Stop(); return; }
            if (source.isPlaying) return;
            // Resume the same event time, rather than restarting a sound after a condition change.
            double elapsed = System.Math.Max(0, Time.unscaledTimeAsDouble - StartedAt);
            if (!Profile.Loop && elapsed >= Profile.Clip.length) return;
            source.time = Profile.Loop ? (float)(elapsed % Profile.Clip.length) : (float)elapsed;
            source.Play();
        }
        public void StopCue()
        {
            active = false; if (source) source.Stop();
            if (Router) Router.Unregister(this);
        }
        void OnDisable() => StopCue();
        void OnDestroy() => StopCue();
    }
}
