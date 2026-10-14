using System;
using System.IO;
using System.Linq;
using GCCD.ContinuousHaptics;
using GCCD.SilentResearch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using EventType = GCCD.SilentResearch.EventType;

public static class SilentResearchSetup
{
    public const string ScenePath = "Assets/_Project/Scenes/SilentHapticResearch.unity";
    const string Root = "Assets/_Project/SilentResearch";
    [MenuItem("GCCD/Silent Study/Create or Open Research Platform")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (File.Exists(ScenePath))
        {
            var existing = EditorSceneManager.OpenScene(ScenePath); EnsurePilots(); EnsureInterface();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(existing); Validate(); return;
        }
        Directory.CreateDirectory(Root + "/Settings"); AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null; RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(.015f, .019f, .025f); RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .018f;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.18f, .20f, .24f);
        var scenario = Asset<SilentScenario>("Indoor01");
        var alternate = Asset<SilentScenario>("Indoor02");
        alternate.ScenarioID = "Indoor-02-v1"; alternate.MinimumForeperiod = 2; alternate.MaximumForeperiod = 3.5f;
        EditorUtility.SetDirty(alternate);
        var patterns = Asset<SilentHapticPatterns>("TemporalPatterns");
        var wall = Material("Concrete", new Color(.19f, .22f, .25f));
        var floor = Material("Floor", new Color(.12f, .14f, .16f));
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/SilentResearch/Settings/Cover.mat") ?? Material("Cover", new Color(.18f, .11f, .07f));
        Cube("Floor", new Vector3(0, -.15f, 0), new Vector3(18, .3f, 18), floor);
        Cube("Ceiling", new Vector3(0, 3.2f, 0), new Vector3(18, .25f, 18), wall);
        Cube("North wall", new Vector3(0, 1.5f, 9), new Vector3(18, 3, .3f), wall);
        Cube("South wall", new Vector3(0, 1.5f, -9), new Vector3(18, 3, .3f), wall);
        Cube("West wall", new Vector3(-9, 1.5f, 0), new Vector3(.3f, 3, 18), wall);
        Cube("East wall", new Vector3(9, 1.5f, 0), new Vector3(.3f, 3, 18), wall);
        Cube("Opaque north partition", new Vector3(0, 1.5f, 3), new Vector3(4, 3, .25f), wall);
        Cube("Opaque south partition", new Vector3(0, 1.5f, -3), new Vector3(4, 3, .25f), wall);
        Cube("Opaque east partition", new Vector3(3, 1.5f, 0), new Vector3(.25f, 3, 4), wall);
        Cube("Opaque west partition", new Vector3(-3, 1.5f, 0), new Vector3(.25f, 3, 4), wall);
        var hiding = new BoxCollider[2];
        for (int i = 0; i < hiding.Length; i++)
        {
            Vector3 center = new Vector3(i == 0 ? -1.3f : 1.3f, 0, -1.3f);
            Cube("Shelter table " + i, center + Vector3.up * 1.25f, new Vector3(1.7f, .2f, 1.6f), wood);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Cube("Table leg " + i + x + z, center + new Vector3(x * .75f, .6f, z * .7f), new Vector3(.12f, 1.2f, .12f), wood);
            var hide = new GameObject("Hiding volume " + i); hide.transform.position = center + Vector3.up;
            hiding[i] = hide.AddComponent<BoxCollider>(); hiding[i].size = new Vector3(1.6f, 2, 1.5f); hiding[i].isTrigger = true;
        }
        for (int i = 0; i < 4; i++)
        {
            var light = new GameObject("Steady room light " + i).AddComponent<Light>(); light.type = LightType.Point;
            light.transform.position = Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 2.6f, 5);
            light.range = 8; light.intensity = 1.1f; light.color = new Color(.72f, .8f, 1);
            light.shadows = LightShadows.None;
        }
        var mainLight = new GameObject("Steady central light").AddComponent<Light>(); mainLight.type = LightType.Point;
        mainLight.transform.position = new Vector3(0, 2.7f, 0); mainLight.range = 8; mainLight.intensity = 1.2f; mainLight.shadows = LightShadows.None;
        var exit = new GameObject("Objective EXIT"); exit.transform.position = scenario.Goal;
        var sign = new GameObject("Static EXIT sign").AddComponent<TextMesh>(); sign.text = "EXIT";
        sign.transform.position = new Vector3(0, 2, 8.7f); sign.characterSize = .22f; sign.fontSize = 48;
        sign.anchor = TextAnchor.MiddleCenter; sign.color = new Color(.4f, 1, .7f);
        var player = new GameObject("Player (reused ResearchPlayerMotion)"); player.layer = 2; player.transform.position = scenario.PlayerStart;
        var body = player.AddComponent<CharacterController>(); body.height = 1.8f; body.radius = .28f; body.skinWidth = .03f;
        var motion = player.AddComponent<ResearchPlayerMotion>(); motion.CenterLocked = false; motion.enabled = false;
        var view = new GameObject("Main Camera"); view.tag = "MainCamera"; view.transform.SetParent(player.transform, false);
        view.transform.localPosition = new Vector3(0, .65f, 0);
        motion.View = view.AddComponent<Camera>(); motion.View.fieldOfView = 70; motion.View.nearClipPlane = .05f; motion.View.farClipPlane = 35;
        motion.View.clearFlags = CameraClearFlags.SolidColor; motion.View.backgroundColor = new Color(.01f, .015f, .02f);
        view.AddComponent<AudioListener>();
        var monster = GameObject.CreatePrimitive(PrimitiveType.Capsule); monster.name = "Monster (silent)";
        UnityEngine.Object.DestroyImmediate(monster.GetComponent<CapsuleCollider>());
        var monsterBody = monster.AddComponent<CharacterController>(); monsterBody.height = 1.8f; monsterBody.radius = .27f; monsterBody.skinWidth = .03f;
        var monsterRenderer = monster.GetComponent<Renderer>(); monsterRenderer.sharedMaterial = Material("Monster", new Color(.035f, .028f, .032f));
        monsterRenderer.enabled = false;
        var system = new GameObject("Silent Study System");
        var output = system.AddComponent<BHapticsVestOutput>();
        var world = system.AddComponent<SilentGameWorld>(); world.Player = motion; world.Monster = monsterBody;
        world.MonsterRenderer = monsterRenderer; world.HidingVolumes = hiding; world.Exit = exit.transform;
        var source = new GameObject("Unified event output (invisible)"); source.AddComponent<AudioSource>().playOnAwake = false;
        var stimulus = source.AddComponent<SilentStimulusOutput>(); stimulus.Patterns = patterns;
        stimulus.Vest = output; stimulus.View = motion.View.transform; stimulus.Mode = OutputMode.Simulation;
        stimulus.Footsteps = Audio("Assets/_Project/SilentResearch/Audio/Footsteps.wav");
        stimulus.Door = Audio("Assets/_Project/SilentResearch/Audio/Door.wav");
        stimulus.Growl = Audio(Root + "/Audio/Growl.wav");
        var session = system.AddComponent<SilentExperimentSession>(); session.Scenario = scenario; session.World = world; session.Stimulus = stimulus;
        system.AddComponent<SilentParticipantUI>().Session = session;
        var controls = system.AddComponent<SilentResearcherUI>(); controls.Session = session; controls.Scenarios = new[] { scenario, alternate };
        EnsurePilots(); EnsureInterface();
        EditorSceneManager.SaveScene(scene, ScenePath);
        // Only the horror research scene is included.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
             .ToArray();
        AssetDatabase.SaveAssets(); Validate(); Selection.activeGameObject = system;
    }
    static T Asset<T>(string name) where T : ScriptableObject
    {
        string path = Root + "/Settings/" + name + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!result) { result = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(result, path); }
        return result;
    }
    static void EnsurePilots()
    {
        var session = UnityEngine.Object.FindFirstObjectByType<SilentExperimentSession>();
        var pilot = session.GetComponent<SilentPilotController>(); if (!pilot) pilot = session.gameObject.AddComponent<SilentPilotController>();
        pilot.Session = session; session.Stimulus.Pilot = pilot;
        var low = DifficultyAsset("IndoorEasy", "Indoor-easy-pilot-v1", session.Scenario, 1.6f, 24);
        var high = DifficultyAsset("IndoorHard", "Indoor-hard-pilot-v1", session.Scenario, 2.8f, 17);
        session.GetComponent<SilentResearcherUI>().Scenarios = new[] { low, session.Scenario, high, Asset<SilentScenario>("Indoor02") };
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
    static SilentScenario DifficultyAsset(string name, string id, SilentScenario baseline, float chase, float limit)
    {
        string path = Root + "/Settings/" + name + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<SilentScenario>(path);
        if (!result)
        { result = UnityEngine.Object.Instantiate(baseline); result.name = name; result.ScenarioID = id; result.MonsterChaseSpeed = chase; result.TrialLimitSec = limit; AssetDatabase.CreateAsset(result, path); }
        return result;
    }
    static void EnsureInterface()
    {
        // Relative USS font/import URLs must be rebuilt after moving the shared UI assets.
        foreach (string file in new[] { "ResearchFont.asset", "ResearchHud.uss", "ResearchHud.tss", "SilentControls.uss", "SilentControls.uxml" })
            AssetDatabase.ImportAsset(Root + "/UI/" + file, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var ui = UnityEngine.Object.FindFirstObjectByType<SilentResearcherUI>();
        ui.UIFont = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(Root + "/UI/ResearchFont.asset");
        if (!ui.UIFont) throw new Exception("Missing runtime font");
        var document = ui.GetComponent<UIDocument>(); if (!document) document = ui.gameObject.AddComponent<UIDocument>();
        document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/SilentResearch/UI/ResearchPanel.asset");
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/SilentControls.uxml");
        if (!document.panelSettings || !document.visualTreeAsset) throw new Exception("Missing UI Toolkit panel / controls");
        var events = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (!events) events = new GameObject("Silent study UI input").AddComponent<EventSystem>();
        var module = events.GetComponent<InputSystemUIInputModule>(); if (!module) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        if (!module.actionsAsset) module.AssignDefaultActions();
    }
    static Material Material(string name, Color color)
    {
        string path = Root + "/Settings/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!result) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); result.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(result, path); }
        return result;
    }
    static GameObject Cube(string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    static AudioClip Audio(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (!importer) throw new Exception("Missing audio: " + path);
        if (path.StartsWith(Root + "/", StringComparison.Ordinal))
        {
            importer.forceToMono = true;
            var samples = importer.defaultSampleSettings; samples.preloadAudioData = true;
            importer.defaultSampleSettings = samples; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("GCCD/Silent Study/Validate Research Scene")]
    public static void Validate()
    {
        var session = UnityEngine.Object.FindFirstObjectByType<SilentExperimentSession>();
        if (!session || !session.Scenario || !session.Scenario.Valid || !session.Stimulus || !session.Stimulus.Patterns || !session.Stimulus.Patterns.Valid)
            throw new Exception("Invalid silent study wiring");
        if (UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length != 1) throw new Exception("Unexpected audio leakage source");
        if (!session.Stimulus.Footsteps || !session.Stimulus.Door || !session.Stimulus.Growl) throw new Exception("Missing auditory reference clips");
        var pilot = session.GetComponent<SilentPilotController>();
        if (!pilot || pilot.Session != session || session.Stimulus.Pilot != pilot || !session.GetComponent<SilentResearcherUI>().UIFont)
            throw new Exception("Missing pilot / runtime font wiring");
        var document = session.GetComponent<UIDocument>();
        if (!document || !document.panelSettings || !document.visualTreeAsset || !UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>())
            throw new Exception("Missing researcher UI / Input System event bridge");
        foreach (EventType type in new[] { EventType.Footsteps, EventType.Door, EventType.Growl })
        {
            var clip = session.Stimulus.Clip(type); clip.LoadAudioData(); var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0) || !data.Any(x => Math.Abs(x) > .001f)) throw new Exception("Silent reference clip: " + type);
        }
        Debug.Log("Silent study validated: 4 feedback conditions, 3 types x 4 directions + explicit catches; horror research scene only.");
    }
    public static void BuildRelease()
    {
        Create();
        string path = Path.GetFullPath("Builds/GCCD-SilentResearch.app");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path,
            target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Silent study build failed");
    }
}
