using UnityEngine;

namespace GCCD.Minimal
{
    public sealed class DebugHapticOutput : HapticOutputBase
    {
        [SerializeField] bool logCues=true;
        public ThreatDirection LastDirection { get; private set; }
        public float LastIntensity { get; private set; }
        public int CueCount { get; private set; }
        public override bool IsReady => isActiveAndEnabled;
        public override bool PlayThreatCue(ThreatDirection direction,float intensity)
        {
            LastDirection=direction; LastIntensity=Mathf.Clamp01(intensity); CueCount++;
            if(logCues) Debug.Log($"[Haptic DEBUG ONLY] {direction} | Intensity {LastIntensity:F2} | No hardware output",this);
            return true;
        }
        public override void Stop() { LastIntensity=0; }
    }
}
