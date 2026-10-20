using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using GCCD.Research;

namespace GCCD.Editor
{
    /// <summary>Reproducible minimal scene: no prior experiment assets or paid packages.</summary>
    public static class StudySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ThreatIntensityStudy.unity";

        [MenuItem("GCCD / Generate Threat Intensity Study Scene")]
        public static void Generate()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGO = new GameObject("Participant Camera + Experiment Orchestrator");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.position = new Vector3(0f, 1.7f, 0f);
            cameraGO.transform.rotation = Quaternion.identity;
            var cam = cameraGO.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.backgroundColor = new Color(0.035f, 0.045f, 0.06f);
            cameraGO.AddComponent<AudioListener>();
            cameraGO.AddComponent<ResearchController>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Neutral Unobstructed Floor";
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            floor.transform.position = Vector3.zero;
            var floorMaterial = new Material(Shader.Find("Standard"));
            floorMaterial.color = new Color(0.16f, 0.17f, 0.18f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var origin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            origin.name = "World Forward Origin Marker";
            origin.transform.position = new Vector3(0f, 0.06f, 8f);
            origin.transform.localScale = new Vector3(0.35f, 0.06f, 0.35f);
            var markerMaterial = new Material(Shader.Find("Standard"));
            markerMaterial.color = new Color(0.85f, 0.76f, 0.22f);
            origin.GetComponent<Renderer>().sharedMaterial = markerMaterial;

            var sun = new GameObject("Neutral Directional Lighting");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = Color.gray;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "GCCD Research";
            PlayerSettings.productName = "Threat Count and Haptic Intensity Study";
            PlayerSettings.bundleVersion = "0.1.0-demo";
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
    }
}
