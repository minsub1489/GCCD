using UnityEngine;

namespace GCCD.Visuals
{
    /// <summary>
    /// Purely visual procedural weapon sway and dry-fire muzzle glow. No extra bullets,
    /// no audio triggers and no physics, preserving the research controller's own hits.
    /// </summary>
    public sealed class StealthViewmodelEffects : MonoBehaviour
    {
        public Transform cameraTransform;
        public Light muzzleLight;
        [Range(0f, .06f)] public float swayDistance = .018f;
        [Range(0f, 1.2f)] public float recoilAngle = .6f;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 targetPosition;
        private float lastCameraYaw;
        private float recoil;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            if (cameraTransform != null) lastCameraYaw = cameraTransform.eulerAngles.y;
        }

        private void LateUpdate()
        {
            float deltaYaw = 0;
            if (cameraTransform)
            {
                deltaYaw = Mathf.DeltaAngle(lastCameraYaw, cameraTransform.eulerAngles.y);
                lastCameraYaw = cameraTransform.eulerAngles.y;
            }

            float sway = Mathf.Clamp(deltaYaw * .5f, -1, 1);
            float idle = Mathf.Sin(Time.time * 1.6f) * .003f;
            targetPosition = restPosition + new Vector3(-sway * swayDistance, idle - recoil * .026f, -recoil * .03f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * 10f);
            Quaternion rotation = restRotation * Quaternion.Euler(-recoil * recoilAngle, -sway * 1.5f, sway * -1.2f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, rotation, Time.deltaTime * 9f);

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
                recoil = 1f;
            if (muzzleLight) muzzleLight.intensity = recoil > .65f ? 1.15f : 0f;
        }
    }
}
