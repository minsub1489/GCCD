using UnityEngine;
using UnityEngine.InputSystem;

namespace FunIsland
{
    // FunIsland movement preserved; input migrated to the GCCD project's Input System.
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public float moveSpeed = 6, sprintSpeed = 10, jumpSpeed = 7, gravity = 20;
        public float mouseSensitivity = .12f;
        public Transform cam;
        CharacterController controller;
        float verticalVel, pitch;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.minMoveDistance = 0; // Preserve gravity/contact even at very high frame rates.
            if (!cam) { var c = GetComponentInChildren<Camera>(); if (c) cam = c.transform; }
        }
        void Update()
        {
            var k = Keyboard.current; var mouse = Mouse.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) ReleaseCursor();
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool controls = Cursor.lockState == CursorLockMode.Locked;
            if (controls && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -89, 89);
                if (cam) cam.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 axis = Vector2.zero;
            if (controls && k != null)
                axis = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            axis = Vector2.ClampMagnitude(axis, 1);
            float speed = k != null && k.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;
            if (controller.isGrounded)
            {
                verticalVel = -1;
                if (controls && k != null && k.spaceKey.wasPressedThisFrame) verticalVel = jumpSpeed;
            }
            else verticalVel -= gravity * Time.deltaTime;
            controller.Move(((transform.right * axis.x + transform.forward * axis.y) * speed
                + Vector3.up * verticalVel) * Time.deltaTime);
        }
        void OnDisable() => ReleaseCursor();
        void OnApplicationFocus(bool focus) { if (!focus) ReleaseCursor(); }
        static void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
