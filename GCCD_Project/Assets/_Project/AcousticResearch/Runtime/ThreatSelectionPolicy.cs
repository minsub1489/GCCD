using System;
using UnityEngine;

namespace GCCD.AcousticResearch
{
    [Serializable]
    public sealed class ThreatSelectionPolicy
    {
        public HapticSelectionMode Mode = HapticSelectionMode.MaskingPriority;
        [Range(0, 1)] public float MinimumRisk = .55f;
        [Range(0, 1)] public float CriticalRisk = .9f;
        [Range(0, 1)] public float CriticalUrgency = .7f;
        [Range(-20, 20)] public float MaskingSnrThresholdDb = 0;
        [Range(1, 4)] public int MaximumSelected = 2;
        [Range(0, .5f)] public float SwitchMargin = .08f;
        public bool Valid => Enum.IsDefined(typeof(HapticSelectionMode), Mode)
            && Unit(MinimumRisk) && Unit(CriticalRisk) && Unit(CriticalUrgency)
            && !float.IsNaN(MaskingSnrThresholdDb) && Mathf.Abs(MaskingSnrThresholdDb) <= 20
            && MaximumSelected >= 1 && MaximumSelected <= 4 && Unit(SwitchMargin);
        static bool Unit(float x) => !float.IsNaN(x) && x >= 0 && x <= 1;
        public static float BandOverlap(SoundBand a, SoundBand b)
            => a == SoundBand.Broadband || b == SoundBand.Broadband || a == b ? 1 : Mathf.Abs((int)a - (int)b) == 1 ? .3f : .05f;
        public static float Snr(float signalPower, float maskerPower)
            => 10 * Mathf.Log10(Mathf.Max(signalPower, .00000001f) / Mathf.Max(maskerPower, .00000001f));
        public bool Eligible(bool offscreen, SoundCueProfile p, bool masked)
        {
            if (Mode == HapticSelectionMode.AllSources) return true;
            if (!offscreen || !p.IsThreat) return false;
            if (Mode == HapticSelectionMode.OffscreenThreats) return true;
            bool critical = p.Risk >= CriticalRisk && p.Urgency >= CriticalUrgency;
            return p.Risk >= MinimumRisk && (masked || critical);
        }
        public static float Score(SoundCueProfile p, float closeness, bool masked)
            => .45f * p.Risk + .25f * p.Urgency + .2f * Mathf.Clamp01(closeness) + (masked ? .1f : 0);
    }
}
