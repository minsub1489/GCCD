using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using GCCD.Dts;
namespace GCCD.Editor
{
    public static class DtsProjectBuilder
    {
        public const string ScenePath="Assets/Scenes/DistancePriorityStudy.unity";
        [MenuItem("GCCD/Distance Priority/Generate Experiment Scene")]
        public static void Generate()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("GCCD Controlled FPS Experiment");go.tag="MainCamera";
            var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.04f,.055f,.075f);
            go.transform.position=new Vector3(0,1.7f,0);go.AddComponent<AudioListener>();go.AddComponent<ExperimentPlatform>();
            var light=new GameObject("Uniform task light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(50,0,0);
            RenderSettings.ambientLight=new Color(.6f,.6f,.6f);RenderSettings.fog=false;
            BhapticsSdk2EditorSetup.AddInitializerToCurrentScene();
            var sdk=GameObject.Find("[bhaptics]");if(sdk)sdk.SetActive(false); // physical initialization is explicit; development never starts the SDK
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="GCCD";PlayerSettings.productName="Distance Priority Haptic Study";PlayerSettings.bundleVersion="0.1.0-dts";
            PlayerSettings.runInBackground=true;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();Debug.Log("DTS_SCENE_GENERATED "+ScenePath);
        }
        [MenuItem("GCCD/Distance Priority/Build Windows")]
        public static void BuildWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/Windows/GCCD.exe");}
        [MenuItem("GCCD/Distance Priority/Build macOS Demo")]
        public static void BuildMac(){Build(BuildTarget.StandaloneOSX,"Builds/macOS/GCCD.app");}
        static void Build(BuildTarget target,string path)
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target))throw new InvalidOperationException("Build support module unavailable: "+target);
            Generate();var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=target,locationPathName=path,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("DTS_BUILD_SUCCESS "+target+" "+report.summary.totalSize);
        }
        public static void ValidateAndBuildBoth(){DtsValidation.Run();BuildWindows();BuildMac();}
        public static void EnableSdk(){BhapticsSdk2EditorSetup.EnableSdkAdapter();}
    }
}
