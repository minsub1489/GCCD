using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.Minimal
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField, Min(0)] float moveSpeed = 3f;
        [SerializeField, Min(0)] float mouseSensitivity = .12f;
        [SerializeField, Range(1,89)] float pitchLimit = 80f;
        [SerializeField] float gravity = -9.81f;
        CharacterController body;
        float pitch, verticalSpeed;
        public Camera ViewCamera => viewCamera;
        void Awake() { body=GetComponent<CharacterController>(); }
        void Update()
        {
            var keyboard=Keyboard.current;
            var mouse=Mouse.current;
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
            else if(mouse!=null && mouse.leftButton.wasPressedThisFrame) { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
            if(Cursor.lockState!=CursorLockMode.Locked || !viewCamera) return;
            if(mouse!=null) {
                Vector2 delta=mouse.delta.ReadValue()*mouseSensitivity;
                transform.Rotate(0,delta.x,0);
                pitch=Mathf.Clamp(pitch-delta.y,-pitchLimit,pitchLimit);
                viewCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            Vector2 axis=Vector2.zero;
            if(keyboard!=null) {
                axis.x=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0);
                axis.y=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0);
            }
            axis=Vector2.ClampMagnitude(axis,1);
            if(body.isGrounded && verticalSpeed<0) verticalSpeed=-2;
            verticalSpeed+=gravity*Time.deltaTime;
            body.Move((transform.right*axis.x*moveSpeed+transform.forward*axis.y*moveSpeed+Vector3.up*verticalSpeed)*Time.deltaTime);
        }
        void OnApplicationFocus(bool focus) { if(!focus) ReleaseCursor(); }
        void OnDisable() { ReleaseCursor(); }
        static void ReleaseCursor() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    }
}
