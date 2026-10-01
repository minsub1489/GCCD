using UnityEngine;

namespace GCCD.AcousticResearch
{
    public enum SoundCategory { Gunshot, Footsteps, Vehicle, Healing, Nature, Door }
    public enum SoundBand { Low, Mid, High, Broadband }
    public enum ExperimentCondition { None, AudioOnly, HapticsOnly, AudioAndHaptics }
    public enum HapticSelectionMode { AllSources, OffscreenThreats, MaskingPriority }

    [CreateAssetMenu(menuName = "GCCD/Research Sound Cue")]
    public sealed class SoundCueProfile : ScriptableObject
    {
        public SoundCategory Category;
        public AudioClip Clip;
        [Range(0, 1)] public float Gain = .7f;
        [Min(.1f)] public float Duration = 2;
        public bool Loop;
        public bool IsThreat;
        [Range(0, 1)] public float Risk = .5f, Urgency = .5f;
        public SoundBand Band = SoundBand.Mid;
        [Tooltip("Estimated pre-distance relative power; calibrate after assigning real audio. Not SPL.")]
        [Range(.001f, 1)] public float RelativePower = .3f;
        public bool Valid => !float.IsNaN(Gain) && Gain >= 0 && Gain <= 1 && !float.IsNaN(Duration)
            && !float.IsInfinity(Duration) && Duration >= .1f && Duration <= 120
            && !float.IsNaN(Risk) && Risk >= 0 && Risk <= 1 && !float.IsNaN(Urgency)
            && Urgency >= 0 && Urgency <= 1 && !float.IsNaN(RelativePower)
            && RelativePower >= .001f && RelativePower <= 1;
    }

    public static class ConditionChannels
    {
        public static bool Audio(ExperimentCondition c) => c == ExperimentCondition.AudioOnly || c == ExperimentCondition.AudioAndHaptics;
        public static bool Haptics(ExperimentCondition c) => c == ExperimentCondition.HapticsOnly || c == ExperimentCondition.AudioAndHaptics;
    }
}
