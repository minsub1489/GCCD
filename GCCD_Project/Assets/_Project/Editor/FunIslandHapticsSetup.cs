using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GCCD.ContinuousHaptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FunIslandHapticsSetup
{
    public const string ScenePath = "Assets/_Project/Scenes/FunIslandHaptics.unity";
    const string MaterialRoot = "Assets/FunIsland/Materials/URP";
    [MenuItem("GCCD/FunIsland/Open Haptic Research Map")]
    public static void Create()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); Validate(); return; }
        var scene = EditorSceneManager.OpenScene("Assets/FunIsland/Scenes/FunIsland.unity");
        EditorSceneManager.SaveScene(scene, ScenePath);
        var players = UnityEngine.Object.FindObjectsByType<FunIsland.FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(p => p.transform.GetSiblingIndex()).ToArray();
        if (players.Length == 0) throw new Exception("No FunIsland player found");
        var player = players.FirstOrDefault(p => p.gameObject.activeInHierarchy) ?? players[0];
        player.gameObject.SetActive(true); player.mouseSensitivity = .12f;
        // The supplied scene contains two active Player/Camera/AudioListener sets.
        // Keep one rig active in the research copy; preserve all map geometry and the source scene.
        foreach (var other in players) if (other != player) other.gameObject.SetActive(false);
        Camera camera = player.GetComponentInChildren<Camera>();
        if (!camera) throw new Exception("No camera on selected player");
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = c == camera;
        foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            listener.enabled = listener.gameObject == camera.gameObject;
        ConvertMaterials();

        var center = new GameObject("HapticTorsoOrigin").transform;
        center.SetParent(player.transform, false); center.localPosition = new Vector3(0, 1, 0);
        var system = new GameObject("ContinuousHaptics");
        var controller = system.AddComponent<HapticThreatController>();
        controller.Player = center; controller.ViewCamera = camera; controller.OffScreenOnly = true;
        controller.EnableHaptics = false; // clone-safe default; enable locally after SDK/device setup
        controller.Output = system.AddComponent<BHapticsVestOutput>();
        controller.EnableKeyboardTest = true;
        system.AddComponent<HapticCsvRecorder>();

        var threat = GameObject.CreatePrimitive(PrimitiveType.Sphere); threat.name = "HapticThreat (T pauses motion)";
        threat.transform.localScale = Vector3.one * .8f;
        threat.transform.position = center.position + HapticDirectionCalculator.ToLocal(135, 0, 8);
        UnityEngine.Object.DestroyImmediate(threat.GetComponent<Collider>());
        var threatMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        threatMat.SetColor("_BaseColor", new Color(1, .15f, .08f));
        threatMat.EnableKeyword("_EMISSION"); threatMat.SetColor("_EmissionColor", new Color(.5f, .015f, 0));
        AssetDatabase.CreateAsset(threatMat, MaterialRoot + "/HapticThreat.mat");
        threat.GetComponent<Renderer>().sharedMaterial = threatMat;
        var orbit = threat.AddComponent<HapticThreatOrbitDemo>(); orbit.InitialCenter = center;
        controller.Target = threat.transform;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        existing.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = existing.ToArray();
        AssetDatabase.SaveAssets(); Selection.activeGameObject = system;
        Validate(); Capture();
        Debug.Log("FunIsland haptics integration complete; original map preserved.");
    }
    static void ConvertMaterials()
    {
        Directory.CreateDirectory(MaterialRoot); AssetDatabase.Refresh();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new Exception("URP Lit missing");
        var converted = new Dictionary<string, Material>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i]; if (!original) continue;
                Color color = original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                float gloss = original.HasProperty("_Glossiness") ? original.GetFloat("_Glossiness") : .3f;
                float metal = original.HasProperty("_Metallic") ? original.GetFloat("_Metallic") : 0;
                string key = ColorUtility.ToHtmlStringRGBA(color) + "_" + Mathf.RoundToInt(gloss * 100) + "_" + Mathf.RoundToInt(metal * 100);
                if (!converted.TryGetValue(key, out var replacement))
                {
                    replacement = new Material(shader) { name = "Island_" + key, enableInstancing = true };
                    replacement.SetColor("_BaseColor", color); replacement.SetFloat("_Smoothness", gloss); replacement.SetFloat("_Metallic", metal);
                    AssetDatabase.CreateAsset(replacement, MaterialRoot + "/Island_" + key + ".mat"); converted[key] = replacement;
                }
                materials[i] = replacement;
            }
            renderer.sharedMaterials = materials;
        }
    }
    public static void Validate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var c = UnityEngine.Object.FindFirstObjectByType<HapticThreatController>();
        if (!c || !c.Player || !c.Target || !c.ViewCamera || !c.Output) throw new Exception("Haptic wiring incomplete");
        if (UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.enabled) != 1)
            throw new Exception("Expected exactly one active camera");
        if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled) != 1)
            throw new Exception("Expected exactly one active audio listener");
        int renderers = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script: " + t.name);
                foreach (var renderer in t.GetComponents<Renderer>())
                {
                    renderers++;
                    foreach (var m in renderer.sharedMaterials)
                        if (!m || !m.shader || m.shader.name != "Universal Render Pipeline/Lit") throw new Exception("Unconverted material: " + t.name);
                }
            }
        if (renderers < 100) throw new Exception("Unexpectedly missing map geometry");
        Debug.Log($"FunIsland validation passed: {renderers} renderers; one active camera/listener; valid haptic links.");
    }
    [MenuItem("GCCD/FunIsland/Capture Research Map Preview")]
    public static void Capture()
    {
        var cameraObject = new GameObject("Temporary overview camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(2, 58, -68); camera.transform.rotation = Quaternion.Euler(34, 0, 0); camera.fieldOfView = 50;
        var rt = new RenderTexture(1280, 720, 24); var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            string path = Environment.GetEnvironmentVariable("GCCD_PREVIEW_PATH");
            if (string.IsNullOrEmpty(path)) path = "Assets/FunIsland/Preview_Haptics.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject); }
    }
    [MenuItem("GCCD/Continuous Haptics/Enable Installed bHaptics SDK")]
    public static void EnableSdk()
    {
        if (!File.Exists("Assets/Bhaptics/SDK2/Scripts/Core/Plugins/BhapticsLibrary.cs"))
            throw new Exception("Install bHaptics Haptic Plugin SDK2 first.");
        var target = UnityEditor.Build.NamedBuildTarget.Standalone;
        var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s => s.Length > 0).ToList();
        if (!defines.Contains("GCCD_BHAPTICS_SDK2")) defines.Add("GCCD_BHAPTICS_SDK2");
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
    }
    public static void Build()
    {
        Validate();
        string path = Environment.GetEnvironmentVariable("GCCD_HAPTICS_BUILD_PATH");
        if (string.IsNullOrEmpty(path)) throw new Exception("Set GCCD_HAPTICS_BUILD_PATH");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = path,
            target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("FunIsland build failed: " + report.summary.result);
        Debug.Log($"FUNISLAND_BUILD_SUCCEEDED errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}");
    }
}
