using UnityEngine;

namespace GCCD.Minimal
{
    public sealed class HapticCueService : MonoBehaviour
    {
        [SerializeField] HapticOutputBase output;
        [SerializeField] bool cuesEnabled=true;
        [SerializeField, Min(.05f)] float minimumInterval=.5f;
        float nextAllowed;
        public bool PlayThreatCue(ThreatDirection direction,float intensity)
        {
            if(!cuesEnabled || !output || !output.IsReady || !isActiveAndEnabled || Time.unscaledTime<nextAllowed) return false;
            intensity=Mathf.Clamp01(intensity);
            if(intensity<=0) return false;
            bool accepted=output.PlayThreatCue(direction,intensity);
            nextAllowed=Time.unscaledTime+minimumInterval;
            return accepted;
        }
        public void Stop() { if(output) output.Stop(); }
        void OnDisable() { Stop(); }
        void OnApplicationFocus(bool focus) { if(!focus) Stop(); }
    }
}
