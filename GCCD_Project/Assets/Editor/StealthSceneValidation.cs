using System;
using UnityEditor;
using UnityEngine;

namespace GCCD.Editor
{
    /// <summary>Verify visual upgrades preserve the spatial threat experiment's geometry.</summary>
    public static class StealthSceneValidation
    {
        [MenuItem("GCCD/Validate Industrial Scene Clearance")]
        public static void Verify()
        {
            Camera camera = Camera.main;
            if (camera == null) throw new Exception("Missing tagged participant camera");
            Transform cameraTransform = camera.transform;
            Quaternion previous = cameraTransform.rotation;
            Vector3 position = cameraTransform.position;
            const float targetDistance = 7f;
            const float targetDiameter = .9f;
            int cases = 0;
            try
            {
                foreach (float yaw in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
                {
                    cameraTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    Physics.SyncTransforms();
                    Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
                    Vector3 forward = cameraTransform.forward;
                    foreach (float bearing in new[] { -130f, -70f, 70f, 130f })
                    {
                        Vector3 direction = Quaternion.AngleAxis(bearing, Vector3.up) * forward;
                        Vector3 target = position + direction * targetDistance + Vector3.down * .35f;
                        if (GeometryUtility.TestPlanesAABB(frustum, new Bounds(target, Vector3.one * targetDiameter)))
                            throw new Exception("Threat visible before warning at yaw " + yaw + ", bearing " + bearing);
                        Vector3 ray = target - position;
                        if (Physics.Raycast(position, ray.normalized, ray.magnitude - targetDiameter * .5f))
                            throw new Exception("Environmental collider occludes target at yaw " + yaw + ", bearing " + bearing);
                        if (Physics.CheckSphere(target, targetDiameter * .5f))
                            throw new Exception("Environmental object overlaps target at yaw " + yaw + ", bearing " + bearing);
                        ++cases;
                    }
                }
            }
            finally
            {
                cameraTransform.rotation = previous;
            }

            if (cases != 32) throw new Exception("Incomplete sightline validation");
            if (GameObject.Find("SECTOR 07  /  INDUSTRIAL INFILTRATION TEST SITE") == null)
                throw new Exception("Missing procedural stealth FPS world");
            Debug.Log("GCCD_STEALTH_CLEARANCE_VALIDATION_PASSED checked_angles=" + cases);
        }
    }
}
