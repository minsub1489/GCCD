using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class MissionDemo
    {
        const float StandingHeight=1.8f,StandingEye=1.7f,CrouchHeight=1f,CrouchEye=.92f;
        bool crouched;readonly MissionPlayClock playClock=new MissionPlayClock();
        public bool IsCrouched {get{return crouched;}}
        public bool IsGrounded {get{return body&&body.isGrounded;}}
        public bool MapVisible {get;private set;}
        double RealNow {get{return Clock!=null?Clock():Time.realtimeSinceStartupAsDouble;}}
        public void ToggleMap()
        {
            if (complete || IsThreatResponse || (paused && !MapVisible)) return;
            bool opening = !MapVisible;
            ApplyPause(opening, false);
            MapVisible = opening;
        }
        public bool SetCrouched(bool value)
        {
            if(paused||complete||!body)return false;if(value==crouched)return true;
            Vector3 feet=transform.position-Vector3.up*(crouched?CrouchEye:StandingEye);
            if(!value){foreach(var c in Physics.OverlapCapsule(feet+Vector3.up*.34f,feet+Vector3.up*(StandingHeight-.34f),.30f,~0,QueryTriggerInteraction.Ignore))if(c!=body)return false;}
            body.enabled=false;crouched=value;body.height=value?CrouchHeight:StandingHeight;body.center=Vector3.up*(body.height/2-(value?CrouchEye:StandingEye));transform.position=feet+Vector3.up*(value?CrouchEye:StandingEye);body.enabled=true;
            return true;
        }
        public void StepMovement(Vector2 input,bool jump,bool sprint,float dt)
        {
            if(paused||complete||!body||dt<=0)return;dt=Mathf.Min(dt,.05f);
            bool grounded=body.isGrounded;if(grounded&&vertical<0)vertical=-2;
            if(jump&&grounded&&!crouched)vertical=Mathf.Sqrt(2*9.81f*1.05f);
            vertical-=9.81f*dt;
            Vector3 walk=Quaternion.Euler(0,yaw,0)*new Vector3(input.x,0,input.y);walkAmount=Mathf.Min(1,walk.magnitude);
            float speed=crouched?1.7f:sprint&&!aiming?5.7f:3.5f;
            walk=Vector3.ClampMagnitude(walk,1)*speed;walk.y=vertical;
            CollisionFlags flags=body.Move(walk*dt);if((flags&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
        }
        void ApplyPause(bool value,bool stopOutput)
        {
            playClock.SetPaused(value,RealNow);paused=value;
            if(value){aiming=false;MapVisible=false;if(gunAudio)gunAudio.Stop();if(muzzle)muzzle.SetActive(false);if(muzzleLight)muzzleLight.intensity=0;if(stopOutput&&Owner)Owner.StopMissionHaptics();}
            else nextShot=System.Math.Max(nextShot,Now+.15);
            Cursor.lockState=value?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=value;
        }
    }
}
