using System;
using System.IO;
using GCCD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class LabBuilder
{
    const string Root="Assets/GCCD";
    static Material floor,wall,metal,glow,orange;
    [MenuItem("GCCD/Create Research Lab")]
    public static void Create()
    {
        Directory.CreateDirectory(Root+"/Scenes"); Directory.CreateDirectory(Root+"/Materials");
        // Preserve any unsaved changes in the user's active scene before opening the new one.
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(previous.isDirty && !string.IsNullOrEmpty(previous.path)) EditorSceneManager.SaveScene(previous);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        floor=Mat("Floor",new Color(.045f,.075f,.095f)); wall=Mat("Wall",new Color(.10f,.17f,.21f));
        metal=Mat("Drone",new Color(.42f,.55f,.60f)); glow=Mat("Mint",new Color(.25f,.85f,.71f),true); orange=Mat("Amber",new Color(.95f,.44f,.16f),true);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.32f,.43f,.51f);
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(.025f,.065f,.09f); RenderSettings.fogDensity=.013f;
        var environment=new GameObject("Environment").transform;
        Cube("Foundation",new Vector3(0,-.5f,0),new Vector3(38,1,38),floor,environment);
        // Open central observation shaft, with lower and upper peripheral walkways.
        for(int side=0;side<4;side++) {
            var wing=new GameObject("Sector "+side).transform; wing.SetParent(environment); wing.rotation=Quaternion.Euler(0,side*90,0);
            LocalCube("Outer wall",new Vector3(0,5,18),new Vector3(36,12,.5f),wall,wing);
            for(int level=0;level<3;level++) {
                LocalCube("Tier walkway",new Vector3(0,level*3.5f,15.8f),new Vector3(34,.25f,3.5f),metal,wing);
                LocalCube("Tier light",new Vector3(0,level*3.5f+.18f,14.1f),new Vector3(34,.055f,.07f),glow,wing);
            }
            for(int x=-16;x<=16;x+=4) {
                LocalCube("Structural rib",new Vector3(x,5,17.7f),new Vector3(.3f,12,.4f),metal,wing);
                LocalCube("Status strip",new Vector3(x,7,17.4f),new Vector3(.065f,2,.06f),glow,wing);
            }
        }
        for(int grid=-16;grid<=16;grid+=2) {
            Cube("Grid X",new Vector3(grid,.01f,0),new Vector3(.025f,.02f,36),wall,environment);
            Cube("Grid Z",new Vector3(0,.01f,grid),new Vector3(36,.02f,.025f),wall,environment);
        }
        var platform=GameObject.CreatePrimitive(PrimitiveType.Cylinder); platform.name="Observer platform"; platform.transform.SetParent(environment); platform.transform.position=new Vector3(0,1.5f,0); platform.transform.localScale=new Vector3(4.5f,1.5f,4.5f); platform.GetComponent<Renderer>().sharedMaterial=wall;
        Cube("Orientation beacon",new Vector3(0,4.5f,17),new Vector3(.16f,2.4f,.16f),orange,environment);
        var light=new GameObject("Key light",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.6f; light.color=new Color(.65f,.85f,1); light.transform.rotation=Quaternion.Euler(45,-35,0);
        var cam=new GameObject("Observer",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>(); cam.tag="MainCamera"; cam.transform.position=new Vector3(0,4.5f,0); cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=RenderSettings.fogColor; cam.fieldOfView=75; cam.nearClipPlane=.1f; cam.farClipPlane=90;
        var targets=new GameObject("Selectable drones").transform;
        for(int level=-1;level<=1;level++) for(int bearing=0;bearing<6;bearing++) {
            float angle=new float[]{-145,-90,-35,35,90,145}[bearing];
            Vector3 pos=Quaternion.Euler(0,angle,0)*new Vector3(0,0,10)+new Vector3(0,4.5f+level*3.5f,0);
            var node=GameObject.CreatePrimitive(PrimitiveType.Sphere); node.name="Drone "+(level+1)+"-"+bearing; node.transform.SetParent(targets); node.transform.position=pos; node.transform.localScale=Vector3.one*.65f; node.GetComponent<Renderer>().sharedMaterial=metal;
            LocalCube("Core",new Vector3(0,0,-.5f),new Vector3(.65f,.15f,.18f),glow,node.transform);
            LocalCube("Wing",Vector3.zero,new Vector3(2.1f,.12f,.4f),metal,node.transform);
            node.transform.LookAt(new Vector3(0,pos.y,0));
        }
        var baffles=new GameObject("Occlusion baffles").transform;
        for(int i=0;i<12;i++) {
            var segment=new GameObject("Baffle "+i).transform; segment.SetParent(baffles); segment.rotation=Quaternion.Euler(0,15+i*30,0);
            LocalCube("Acoustic panel",new Vector3(0,4.5f,3.5f),new Vector3(.35f,1.5f,.22f),wall,segment);
            LocalCube("Panel edge",new Vector3(0,5.26f,3.5f),new Vector3(.4f,.04f,.24f),orange,segment);
        }
        baffles.gameObject.SetActive(false);
        var emitter=new GameObject("Hidden audio source",typeof(AudioSource),typeof(AudioLowPassFilter)); var audio=emitter.GetComponent<AudioSource>(); audio.playOnAwake=false; audio.spatialBlend=1; audio.rolloffMode=AudioRolloffMode.Linear; audio.minDistance=1; audio.maxDistance=23; audio.dopplerLevel=0;
        var controller=new GameObject("Research session",typeof(HapticOutput),typeof(ThreatLab)); var lab=controller.GetComponent<ThreatLab>(); lab.observer=cam; lab.targetRoot=targets; lab.blocker=baffles; lab.signal=audio; lab.lowPass=emitter.GetComponent<AudioLowPassFilter>();
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/ThreatLab.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/ThreatLab.unity",true)};
        PlayerSettings.productName="GCCD Threat Lab"; PlayerSettings.companyName="GCCD"; PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900; PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=false;
        AssetDatabase.SaveAssets();
        ResearchModel.SelfTest(); Debug.Log("GCCD scene created; model checks passed.");
    }
    static Material Mat(string name,Color c,bool emission=false)
    {
        string path=Root+"/Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.color=c; m.SetFloat("_Smoothness",.4f); if(emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",c*1.3f); } return m;
    }
    static GameObject Cube(string name,Vector3 pos,Vector3 size,Material m,Transform parent)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent); g.transform.position=pos; g.transform.localScale=size; g.GetComponent<Renderer>().sharedMaterial=m; return g;
    }
    static void LocalCube(string name,Vector3 pos,Vector3 size,Material m,Transform parent)
    {
        var g=Cube(name,Vector3.zero,size,m,parent); g.transform.localPosition=pos; g.transform.localRotation=Quaternion.identity;
    }
    [MenuItem("GCCD/Run Model Checks")]
    public static void Check() { ResearchModel.SelfTest(); Debug.Log("GCCD model checks passed"); }
    public static void BuildMac()
    {
        Directory.CreateDirectory("Builds");
        var result=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/GCCD Threat Lab.app",BuildTarget.StandaloneOSX,BuildOptions.None);
        File.WriteAllText("Builds/build-result.txt",result.summary.result+" errors="+result.summary.totalErrors);
    }
}
