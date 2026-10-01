using System;
using System.IO;
using System.Linq;
using GCCD.AcousticResearch;
using GCCD.ContinuousHaptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class AcousticResearchSetup
{
    public const string ScenePath="Assets/_Project/Scenes/OffscreenThreatResearch.unity";
    const string Root="Assets/_Project/AcousticResearch";
    [MenuItem("GCCD/Acoustic Research/Create or Open Empty 3D Experiment")]
    public static void Create()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath);EnsureFont();EnsureInput();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Validate();return; }
        Directory.CreateDirectory(Root+"/Profiles"); AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.skybox=null;RenderSettings.fog=false;
        var player=new GameObject("Player - fixed at center");player.layer=2;
        var body=player.AddComponent<CharacterController>();body.height=1.7f;body.radius=.3f;
        var motion=player.AddComponent<ResearchPlayerMotion>();motion.CenterLocked=true;
        var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(player.transform,false);
        var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.035f,.055f,.09f);camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.farClipPlane=100;
        cameraObject.AddComponent<AudioListener>();motion.View=camera;
        var torso=new GameObject("Torso origin");torso.transform.SetParent(player.transform,false);
        var system=new GameObject("Acoustic Research System");
        var router=system.AddComponent<AcousticHapticsRouter>();router.Player=torso.transform;router.ViewCamera=camera;
        router.Output=system.AddComponent<BHapticsVestOutput>();router.HardwareArmed=false;
        router.Selection.Mode=HapticSelectionMode.OffscreenThreats;router.Sto.Enabled=false;
        var session=system.AddComponent<ResearchExperimentSession>();session.Router=router;session.Player=motion;
        session.Sources=new ResearchSoundEmitter[6];session.Maskers=new ResearchSoundEmitter[2];
        float[] risk={1,.8f,.75f,.2f,0,.65f},urgency={.95f,.7f,.7f,.1f,0,.6f};
        SoundBand[] bands={SoundBand.High,SoundBand.Mid,SoundBand.Low,SoundBand.Mid,SoundBand.Broadband,SoundBand.Mid};
        for(int i=0;i<6;i++)
        {
            var category=(SoundCategory)i;string path=Root+"/Profiles/"+category+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<SoundCueProfile>(path);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<SoundCueProfile>();profile.Category=category;profile.IsThreat=i!=3&&i!=4;
                profile.Risk=risk[i];profile.Urgency=urgency[i];profile.Band=bands[i];profile.RelativePower=i==4?.8f:.12f;
                profile.Loop=i==1||i==2||i==4;AssetDatabase.CreateAsset(profile,path);
            }
            session.Sources[i]=Emitter(category.ToString(),profile,router,HapticDirectionCalculator.ToLocal(i*60,0,8));
        }
        session.Maskers[0]=Emitter("Nature masker",session.Sources[4].Profile,router,new Vector3(-3,1,-2));
        session.Maskers[1]=Emitter("Vehicle masker",session.Sources[2].Profile,router,new Vector3(3,-1,-2));
        foreach(var m in session.Maskers)m.TreatAsMasker=true;
        var target=GameObject.CreatePrimitive(PrimitiveType.Sphere);target.name="Shoot target";target.transform.localScale=Vector3.one*1.2f;
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Target.mat");
        if(!material)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)throw new Exception("URP Unlit missing");
            material=new Material(shader);material.SetColor("_BaseColor",new Color(.98f,.39f,.18f));AssetDatabase.CreateAsset(material,Root+"/Target.mat");
        }
        target.GetComponent<Renderer>().sharedMaterial=material;target.AddComponent<ResearchShootTarget>().Session=session;
        session.ThreatVisual=target;target.SetActive(false);
        var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/UI/ResearchPanel.asset");
        if(!panel)
        {
            panel=ScriptableObject.CreateInstance<PanelSettings>();panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution=new Vector2Int(1280,720);panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root+"/UI/ResearchHud.tss");
            AssetDatabase.CreateAsset(panel,Root+"/UI/ResearchPanel.asset");
        }
        var ui=new GameObject("Research controls");var document=ui.AddComponent<UIDocument>();document.panelSettings=panel;
        document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"/UI/ResearchHud.uxml");
        ui.AddComponent<ResearchExperimentHud>().Session=session;
        EnsureFont();EnsureInput();
        EditorSceneManager.SaveScene(scene,ScenePath);
        var old=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Select(s=>new EditorBuildSettingsScene(s.path,false));
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(old).ToArray();
        AssetDatabase.SaveAssets();Validate();Selection.activeGameObject=system;
    }
    static void EnsureInput()
    {
        var events=UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if(!events)events=new GameObject("Research UI input").AddComponent<EventSystem>();
        var module=events.GetComponent<InputSystemUIInputModule>();
        if(!module)module=events.gameObject.AddComponent<InputSystemUIInputModule>();
        if(!module.actionsAsset)module.AssignDefaultActions();
    }
    static void EnsureFont()
    {
        string path=Root+"/UI/ResearchFont.asset";
        var font=AssetDatabase.LoadAssetAtPath<FontAsset>(path);
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if(!source)throw new Exception("Research UI requires the project's LiberationSans font");
        if(!font || font.sourceFontFile!=source || !font.material || font.atlasTextures.Any(t=>!t))
        {
            if(font)AssetDatabase.DeleteAsset(path);
            font=FontAsset.CreateFontAsset(source);
            font.name="Research Font";AssetDatabase.CreateAsset(font,path);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
            EditorUtility.SetDirty(font);
        }
        var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/UI/ResearchPanel.asset");
        string settingsPath=Root+"/UI/ResearchTextSettings.asset";
        var settings=AssetDatabase.LoadAssetAtPath<PanelTextSettings>(settingsPath);
        if(!settings) {settings=ScriptableObject.CreateInstance<PanelTextSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
        settings.defaultFontAsset=font;panel.textSettings=settings;EditorUtility.SetDirty(panel);EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(Root+"/UI/ResearchHud.uss",ImportAssetOptions.ForceUpdate);
    }
    static ResearchSoundEmitter Emitter(string name,SoundCueProfile profile,AcousticHapticsRouter router,Vector3 position)
    {
        var go=new GameObject(name+" - invisible sound source");go.transform.position=position;
        var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
        source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.5f;source.maxDistance=15;source.dopplerLevel=0;
        var emitter=go.AddComponent<ResearchSoundEmitter>();emitter.Profile=profile;emitter.Router=router;return emitter;
    }
    [MenuItem("GCCD/Acoustic Research/Validate Empty 3D Experiment")]
    public static void Validate()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<ResearchExperimentSession>();
        if(!s || !s.Player || !s.Player.CenterLocked || s.Player.transform.position!=Vector3.zero || !s.Router || !s.Router.Output
            || s.Sources.Length!=6 || s.Maskers.Length!=2 || !s.ThreatVisual || !s.ThreatVisual.GetComponent<Collider>()
            || !s.ThreatVisual.GetComponent<ResearchShootTarget>())throw new Exception("Incomplete research scene wiring");
        if(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length!=1)throw new Exception("Expected one AudioListener");
        foreach(var root in s.gameObject.scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
        if(!GraphicsSettings.currentRenderPipeline)throw new Exception("URP pipeline required");
        var doc=UnityEngine.Object.FindFirstObjectByType<UIDocument>();
        if(!doc || !doc.panelSettings || !doc.visualTreeAsset || !doc.panelSettings.themeStyleSheet
            || !doc.panelSettings.textSettings || !doc.panelSettings.textSettings.defaultFontAsset
            || !doc.panelSettings.textSettings.defaultFontAsset.sourceFontFile)throw new Exception("Incomplete runtime UI or embedded source font");
        if(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length!=1
            || !UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>())throw new Exception("Research UI input module required");
        var renderer=s.ThreatVisual.GetComponent<Renderer>();if(!renderer.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/"))throw new Exception("Unsupported target shader");
        Debug.Log("Empty 3D offscreen-shooting experiment validated: "+ScenePath);
    }
    [MenuItem("GCCD/Acoustic Research/Capture Experiment UI")]
    public static void Capture()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode to capture the UI");
        var s=UnityEngine.Object.FindFirstObjectByType<ResearchExperimentSession>();
        string name=s.HasPendingLogs?"ResearchLogDecision":s.Running?"ResearchGameplay":"ResearchSetup";
        Directory.CreateDirectory("Logs");ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/"+name+".png"));
    }
    [MenuItem("GCCD/Acoustic Research/Build macOS Research Player")]
    public static void Build()
    {
        Create();string path=Environment.GetEnvironmentVariable("GCCD_RESEARCH_BUILD_PATH");
        if(string.IsNullOrEmpty(path))path=Path.GetFullPath("Builds/OffscreenThreatResearch.app");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ResearchBuild.txt",$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Research player build failed");
    }
}
