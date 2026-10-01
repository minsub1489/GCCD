using System;
using System.IO;
using GCCD.ContinuousHaptics;
using GCCD.Minimal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ContinuousHapticsSetup
{
    public const string ScenePath = "Assets/_Project/Scenes/ContinuousHapticsTest.unity";
    [MenuItem("GCCD/Continuous Haptics/Create or Open Research Scene")]
    public static void Create()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
        // Work on a COPY: preserve the user's existing experiment scene and all its references.
        const string source = "Assets/_Project/Scenes/ThreatDirectionTest.unity";
        var scene = EditorSceneManager.OpenScene(source);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save research scene");
        foreach (var sensor in UnityEngine.Object.FindObjectsByType<ThreatSensor>(FindObjectsSortMode.None)) sensor.enabled = false;
        foreach (var service in UnityEngine.Object.FindObjectsByType<HapticCueService>(FindObjectsSortMode.None)) service.enabled = false;
        foreach (var output in UnityEngine.Object.FindObjectsByType<HapticOutputBase>(FindObjectsSortMode.None)) output.enabled = false;
        var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
        var spawner = UnityEngine.Object.FindFirstObjectByType<ThreatSpawner>();
        if (!player || !spawner) throw new InvalidOperationException("Expected prototype Player and ThreatSpawner");
        var root = new GameObject("ContinuousHaptics");
        var controller = root.AddComponent<HapticThreatController>();
        controller.Player = player.transform;
        controller.Output = root.AddComponent<BHapticsVestOutput>();
        controller.ViewCamera = Camera.main;
        controller.OffScreenOnly = true;
        root.AddComponent<HapticCsvRecorder>();
        var bridge = root.AddComponent<ContinuousThreatSpawnerBridge>();
        bridge.Controller = controller; bridge.Spawner = spawner;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        AssetDatabase.SaveAssets();
        Debug.Log("Continuous haptics research scene created: " + ScenePath);
    }
    public static void ValidateScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var c = UnityEngine.Object.FindFirstObjectByType<HapticThreatController>();
        if (!c || !c.Player || !c.Output || !c.ViewCamera || !c.GetComponent<ContinuousThreatSpawnerBridge>()
            || !c.GetComponent<HapticCsvRecorder>()) throw new Exception("Incomplete continuous scene wiring");
        foreach (var root in scene.GetRootGameObjects())
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) > 0)
                    throw new Exception("Missing script: " + tr.name);
        foreach (var sensor in UnityEngine.Object.FindObjectsByType<ThreatSensor>(FindObjectsSortMode.None))
            if (sensor.enabled) throw new Exception("Legacy sensor still enabled");
        Debug.Log("Continuous research scene wiring validated");
    }
    public static void BuildResearchPlayer()
    {
        ValidateScene();
        string output = Environment.GetEnvironmentVariable("GCCD_HAPTICS_BUILD_PATH");
        if (string.IsNullOrEmpty(output)) throw new Exception("Set GCCD_HAPTICS_BUILD_PATH");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = output,
            target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Research player build failed: " + report.summary.result);
    }
}
