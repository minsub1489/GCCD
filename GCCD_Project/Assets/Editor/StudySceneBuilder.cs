using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GCCD.Research;

namespace GCCD.Editor
{
    /// <summary>Rebuilds the controlled first-person study inside an industrial stealth arena.</summary>
    public static class StudySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ThreatIntensityStudy.unity";

        [MenuItem("GCCD/Generate Threat Intensity Study Scene")]
        public static void Generate()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("First-Person Operative / Research Controller");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, 0f), Quaternion.identity);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 88f;
            camera.allowHDR = true;
            camera.backgroundColor = new Color(0.035f, 0.052f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ResearchController>();

            StealthStudyEnvironment.Generate(cameraObject.transform);
            // Keep the committed demo scene self-contained when SDK2 isn't installed.
            // The official [bhaptics] prefab can be placed via GCCD/bHaptics SDK2
            // after a legitimate Asset Store import and per-developer setup.
            StealthSceneValidation.Verify();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "GCCD Research";
            PlayerSettings.productName = "Threat Count and Haptic Intensity Study";
            PlayerSettings.bundleVersion = "0.2.0-stealth-demo";
            AssetDatabase.SaveAssets();
            Debug.Log("GCCD_STUDY_SCENE_CREATED " + ScenePath);
        }

        public static void GenerateAndBuildMac()
        {
            Generate();
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/ThreatIntensityStudy.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new Exception("GCCD study build failed: " + result.summary.result);
            Debug.Log("GCCD_STUDY_MAC_BUILD_SUCCESS " + result.summary.totalSize);
        }

        [MenuItem("GCCD/Capture Stealth Scene Preview")]
        public static void GenerateAndCapturePreview()
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Graphics preview rendering requires an interactive Unity Editor; batchmode has no supported rendering device.");
            Generate();
            Camera cam = Camera.main;
            const int width = 1440;
            const int height = 810;
            RenderTexture render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                render.Create();
                cam.targetTexture = render;
                cam.Render();
                RenderTexture.active = render;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Assets/Art/Previews");
                const string path = "Assets/Art/Previews/StealthResearchPreview.png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                Debug.Log("GCCD_STEALTH_PREVIEW_CAPTURED " + path);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = previous;
                if (image) UnityEngine.Object.DestroyImmediate(image);
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
            }
        }
    }
}
