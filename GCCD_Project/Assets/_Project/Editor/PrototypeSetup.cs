using System;
using System.IO;
using GCCD.Minimal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PrototypeSetup
{
    const string Root="Assets/_Project";
    public static void Create()
    {
        if(File.Exists(Root+"/Scenes/ThreatDirectionTest.unity")) throw new Exception("Scene already exists; do not overwrite edited scene.");
        foreach(string f in new[]{"Scenes","Prefabs","Materials","Audio","UI","Data","Scripts/Core","Scripts/Experiment"}) Directory.CreateDirectory(Root+"/"+f);
        AssetDatabase.Refresh();
        var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(old.isDirty) throw new Exception("Save the current scene before setup.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var ground=MakeMaterial("Ground",new Color(.3f,.34f,.37f));
        var threatMaterial=MakeMaterial("Threat",new Color(.9f,.25f,.1f));
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Floor"; floor.transform.position=new Vector3(0,-.25f,0); floor.transform.localScale=new Vector3(40,.5f,40); floor.GetComponent<Renderer>().sharedMaterial=ground;
        var light=new GameObject("Directional Light",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.2f; light.transform.rotation=Quaternion.Euler(50,-30,0);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=Color.gray;
        var player=new GameObject("Player",typeof(CharacterController),typeof(FirstPersonController));
        var body=player.GetComponent<CharacterController>(); body.height=1.8f; body.center=new Vector3(0,.9f,0); body.radius=.3f;
        var camera=new GameObject("FirstPersonCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>(); camera.tag="MainCamera"; camera.transform.SetParent(player.transform,false); camera.transform.localPosition=new Vector3(0,1.6f,0); camera.fieldOfView=70; camera.farClipPlane=100; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.16f,.2f);
        Set(player.GetComponent<FirstPersonController>(),"viewCamera",camera);
        var capsule=GameObject.CreatePrimitive(PrimitiveType.Capsule); capsule.name="Threat"; capsule.GetComponent<Renderer>().sharedMaterial=threatMaterial;
        var prefab=PrefabUtility.SaveAsPrefabAsset(capsule,Root+"/Prefabs/Threat.prefab"); UnityEngine.Object.DestroyImmediate(capsule);
        var system=new GameObject("ThreatSystem",typeof(ThreatSpawner),typeof(ThreatSensor));
        var outputObject=new GameObject("Haptics",typeof(DebugHapticOutput),typeof(HapticCueService));
        var spawner=system.GetComponent<ThreatSpawner>(); var sensor=system.GetComponent<ThreatSensor>(); var service=outputObject.GetComponent<HapticCueService>();
        Set(spawner,"player",player.transform); Set(spawner,"threatPrefab",prefab);
        Set(sensor,"viewCamera",camera); Set(sensor,"playerHeading",player.transform); Set(sensor,"spawner",spawner); Set(sensor,"haptics",service);
        Set(service,"output",outputObject.GetComponent<DebugHapticOutput>());
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/ThreatDirectionTest.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
        AssetDatabase.SaveAssets();
    }
    public static void Set(UnityEngine.Object obj,string name,UnityEngine.Object value)
    { var so=new SerializedObject(obj); so.FindProperty(name).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Material MakeMaterial(string name,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color=color; AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat"); return m;
    }
    public static string Verify()
    {
        void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        Check(ThreatSensor.Classify(0,45,45)==ThreatDirection.Front,"front");
        Check(ThreatSensor.Classify(90,45,45)==ThreatDirection.Right,"right");
        Check(ThreatSensor.Classify(-90,45,45)==ThreatDirection.Left,"left");
        Check(ThreatSensor.Classify(180,45,45)==ThreatDirection.Back,"back");
        Check(ThreatSensor.Classify(45,45,45)==ThreatDirection.Front,"front edge");
        Check(ThreatSensor.Classify(46,45,45)==ThreatDirection.Right,"outside front");
        Check(ThreatSensor.Classify(-135,45,45)==ThreatDirection.Back,"back edge");
        Check(ThreatSensor.Classify(35,30,60)==ThreatDirection.Right,"configurable front");
        Check(ThreatSensor.Classify(125,30,60)==ThreatDirection.Back,"configurable back");
        Check(ThreatSensor.InViewport(new Vector3(.5f,.5f,5),.3f,100),"visible");
        foreach(var p in new[]{new Vector3(.5f,.5f,-1),new Vector3(-.1f,.5f,5),new Vector3(1.1f,.5f,5),new Vector3(.5f,1.1f,5),new Vector3(.5f,-.1f,5),new Vector3(.5f,.5f,101)}) Check(!ThreatSensor.InViewport(p,.3f,100),"outside/behind");
        return "PASS: cardinal directions, boundaries, configurable sectors, viewport sides, behind-camera and far plane";
    }
}
