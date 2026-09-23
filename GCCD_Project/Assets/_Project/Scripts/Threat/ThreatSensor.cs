using UnityEngine;

namespace GCCD.Minimal
{
    public sealed class ThreatSensor : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] Transform playerHeading;
        [SerializeField] ThreatSpawner spawner;
        [SerializeField] HapticCueService haptics;
        [SerializeField, Range(1,89)] float frontHalfAngle=45;
        [SerializeField, Range(1,89)] float backHalfAngle=45;
        [SerializeField, Min(0)] float stableSeconds=.1f;
        [SerializeField, Range(0,1)] float fixedIntensity=.5f;
        [SerializeField] bool logDebug=true;
        [Header("Live diagnostic values (Play Mode)")]
        [SerializeField] bool offScreen;
        [SerializeField] float signedAngle;
        [SerializeField] ThreatDirection direction;
        public bool OffScreen => offScreen;
        public float SignedAngle => signedAngle;
        public ThreatDirection Direction => direction;
        Transform observed;
        bool wasOffscreen, pending;
        ThreatDirection candidate;
        float candidateSince;
        public static bool InViewport(Vector3 viewport,float near,float far)
            => viewport.z>=near && viewport.z<=far && viewport.x>=0 && viewport.x<=1 && viewport.y>=0 && viewport.y<=1;
        public static ThreatDirection Classify(float angle,float front,float back)
        {
            angle=Mathf.DeltaAngle(0,angle);
            if(Mathf.Abs(angle)<=front) return ThreatDirection.Front;
            if(Mathf.Abs(angle)>=180-back) return ThreatDirection.Back;
            return angle>0?ThreatDirection.Right:ThreatDirection.Left;
        }
        void Update() { Evaluate(); }
        public void Evaluate()
        {
            Transform target=spawner?spawner.CurrentThreat:null;
            if(!target || !viewCamera || !playerHeading || !viewCamera.isActiveAndEnabled) {
                if(observed || wasOffscreen) haptics?.Stop();
                observed=null; wasOffscreen=offScreen=pending=false; return;
            }
            Vector3 delta=Vector3.ProjectOnPlane(target.position-playerHeading.position,Vector3.up);
            signedAngle=Vector3.SignedAngle(Vector3.ProjectOnPlane(playerHeading.forward,Vector3.up),delta,Vector3.up);
            direction=Classify(signedAngle,frontHalfAngle,backHalfAngle);
            offScreen=!InViewport(viewCamera.WorldToViewportPoint(target.position),viewCamera.nearClipPlane,viewCamera.farClipPlane);
            bool changedTarget=observed!=target;
            if(changedTarget) haptics?.Stop();
            if(!offScreen) {
                if(wasOffscreen) haptics?.Stop();
                pending=false;
            } else {
                if(changedTarget || !wasOffscreen || candidate!=direction) {
                    candidate=direction; candidateSince=Time.unscaledTime; pending=true;
                }
                if(pending && Time.unscaledTime-candidateSince>=stableSeconds && haptics && haptics.PlayThreatCue(direction,fixedIntensity)) {
                    pending=false;
                    if(logDebug) Debug.Log($"Threat Direction: {direction} | Threat Angle: {signedAngle:F1}° | Off Screen: TRUE | Haptic Intensity: {fixedIntensity:F2}",this);
                }
            }
            observed=target; wasOffscreen=offScreen;
        }
        void OnDisable() { haptics?.Stop(); observed=null; pending=wasOffscreen=offScreen=false; }
    }
}
