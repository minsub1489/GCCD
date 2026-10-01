using UnityEngine;
namespace GCCD.AcousticResearch
{
    public sealed class ResearchShootTarget : MonoBehaviour
    {
        public ResearchExperimentSession Session;
        public void Hit(double now) { if(Session) Session.TargetHit(this,now); }
    }
}
