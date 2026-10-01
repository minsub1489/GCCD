#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GCCD.ContinuousHaptics.PlayModeTests
{
    public class FunIslandIntegrationTests
    {
        [UnityTest]
        public IEnumerator MapLoadsWalkableWithMovingThreatAndPlayerRelativeDirection()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/FunIslandHaptics.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var c = Object.FindFirstObjectByType<HapticThreatController>();
            Assert.IsNotNull(c); Assert.IsNotNull(c.Player); Assert.IsNotNull(c.Target); Assert.IsNotNull(c.ViewCamera);
            Assert.IsFalse(c.EnableHaptics, "Committed map must start in device-free preview mode");
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.enabled), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled), Is.EqualTo(1));
            Vector3 oldTarget = c.Target.position;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(Vector3.Distance(oldTarget, c.Target.position), Is.GreaterThan(.1f));
            var body = c.Player.GetComponentInParent<CharacterController>();
            Assert.IsNotNull(body);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!body.isGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(body.isGrounded, "Player must land on the preserved map colliders; position=" + body.transform.position);
            var orbit = c.Target.GetComponent<HapticThreatOrbitDemo>(); orbit.Animate = false;
            c.OffScreenOnly = false; c.EnableSmoothing = false;
            c.Tick(Time.unscaledTimeAsDouble); float before = c.CurrentAzimuth;
            body.transform.Rotate(0, 90, 0); c.Tick(Time.unscaledTimeAsDouble);
            Assert.That(Mathf.DeltaAngle(before, c.CurrentAzimuth), Is.EqualTo(-90).Within(.05f));
            Object.Destroy(c.Target.gameObject); yield return null;
            Assert.That(c.SmoothedIntensity, Is.Zero);
            var empty = SceneManager.CreateScene("AfterFunIslandTest"); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync("FunIslandHaptics");
        }
    }
}
#endif
