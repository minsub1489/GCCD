using UnityEngine;

namespace GCCD.Minimal
{
    // Only hardware adapters reference SDK types. Gameplay never references an SDK.
    public abstract class HapticOutputBase : MonoBehaviour
    {
        public abstract bool IsReady { get; }
        public abstract bool PlayThreatCue(ThreatDirection direction, float intensity);
        public abstract void Stop();
    }
}
