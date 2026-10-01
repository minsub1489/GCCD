using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.AcousticResearch
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ResearchPlayerMotion : MonoBehaviour
    {
        public Camera View;
        public bool MeasurementMode;
        public bool CenterLocked=true;
        public bool LookEnabled=true;
        public float MoveSpeed=4, Sensitivity=.12f;
        CharacterController body;
        float vertical, pitch;
        void Awake() { body=GetComponent<CharacterController>(); body.minMoveDistance=0; }
        public void ResetPose(Vector3 position, Quaternion rotation)
        { body.enabled=false; transform.SetPositionAndRotation(position,rotation); body.enabled=true; vertical=0; pitch=0; if(View) View.transform.localRotation=Quaternion.identity; }
        public void CaptureCursor(bool capture) { Cursor.lockState=capture?CursorLockMode.Locked:CursorLockMode.None; Cursor.visible=!capture; }
        void Update()
        {
            var k=Keyboard.current; var m=Mouse.current;
            if(k!=null && k.escapeKey.wasPressedThisFrame) CaptureCursor(false);
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                if(m!=null && LookEnabled) { var delta=m.delta.ReadValue()*Sensitivity; transform.Rotate(0,delta.x,0); pitch=Mathf.Clamp(pitch-delta.y,-89.9f,89.9f); if(View) View.transform.localRotation=Quaternion.Euler(pitch,0,0); }
                if(CenterLocked || MeasurementMode) { vertical=0; return; }
                Vector2 axis=Vector2.zero;
                if(!MeasurementMode && k!=null) axis=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                axis=Vector2.ClampMagnitude(axis,1);
                vertical=body.isGrounded?-2:vertical-20*Time.deltaTime;
                body.Move(((transform.right*axis.x+transform.forward*axis.y)*MoveSpeed+Vector3.up*vertical)*Time.deltaTime);
            }
        }
        void OnDisable() => CaptureCursor(false);
        void OnApplicationFocus(bool focus) { if(!focus) CaptureCursor(false); }
    }
}
