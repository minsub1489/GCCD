using UnityEngine;
#if GCCD_BHAPTICS_SDK2
using Bhaptics.SDK2;
#endif

namespace GCCD.Minimal
{
    // SDK2 2.8.1: verified against imported BhapticsLibrary.cs.
    // SDK is installed separately; the default checkout compiles without it.
    public sealed class BhapticsHapticOutput : HapticOutputBase
    {
        [SerializeField] string frontEvent;
        [SerializeField] string rightEvent;
        [SerializeField] string backEvent;
        [SerializeField] string leftEvent;
        [SerializeField, Range(.1f,1f)] float maximumIntensity=.5f;
        [SerializeField] bool logRequests=true;
        int requestId=-1;
        public override bool IsReady {
            get {
#if GCCD_BHAPTICS_SDK2
                if(!isActiveAndEnabled || !Application.isPlaying) return false;
                var settings=BhapticsSettings.Instance;
                if(settings==null || string.IsNullOrWhiteSpace(settings.AppId)) return false;
                return BhapticsLibrary.IsBhapticsAvailableForce(false) && BhapticsLibrary.IsConnect(PositionType.Vest);
#else
                return false;
#endif
            }
        }
        public override bool PlayThreatCue(ThreatDirection direction,float intensity)
        {
#if GCCD_BHAPTICS_SDK2
            if(!IsReady) return false;
            string eventId=direction switch {
                ThreatDirection.Front=>frontEvent, ThreatDirection.Right=>rightEvent,
                ThreatDirection.Back=>backEvent, _=>leftEvent
            };
            if(string.IsNullOrWhiteSpace(eventId)) { Debug.LogWarning($"Assign deployed {direction} event in BhapticsHapticOutput Inspector.",this); return false; }
            Stop();
            requestId=BhapticsLibrary.PlayParam(eventId,Mathf.Clamp(intensity,0,maximumIntensity));
            if(requestId<0) Debug.LogWarning("bHaptics rejected the cue request. Check deployed event ID and Player connection.",this);
            else if(logRequests) Debug.Log($"[bHaptics] {direction} requested (id {requestId}). Physical sensation must be verified on X40.",this);
            return requestId>=0;
#else
            Debug.LogWarning("Install SDK2, link Haptic App, then enable GCCD_BHAPTICS_SDK2 in Player Settings.",this);
            return false;
#endif
        }
        public override void Stop()
        {
#if GCCD_BHAPTICS_SDK2
            if(requestId>=0) BhapticsLibrary.StopInt(requestId);
#endif
            requestId=-1;
        }
        void OnDisable() { Stop(); }
    }
}
