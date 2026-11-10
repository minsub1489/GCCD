#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed class VisualUpgradeProbe:MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            yield return CaptureUi("visual-lobby-1600.png",1600,1000);
            yield return CaptureUi("visual-lobby-1280.png",1280,720);
            var menu=GetComponent<UIDocument>().rootVisualElement;
            Check(menu.Q("participantName").layout.height>25&&menu.Q("studyAngle").layout.height>25,"Essential menu fields visible");
            var panelRoot=menu.panel.visualTree;
            Check(panelRoot.styleSheets.Contains(Resources.Load<StyleSheet>("Realism/UI/OperatorPopup")),"Popup theme reaches panel root");
            var owner=GetComponent<ExperimentPlatform>();owner.StartMission();
            yield return null;yield return null;
            var mission=GetComponent<MissionDemo>();mission.Physical=false;
            double clock=Time.realtimeSinceStartupAsDouble;mission.Clock=()=>clock;
            var world=GameObject.Find("MISSION ONLY · Photographed freight terminal");
            var scenery=world.transform.Find("Terminal detail · CC0 industrial dressing");
            Check(scenery&&scenery.GetComponentsInChildren<Collider>().Length==0,"Scenery changes collision");
            Check(scenery.GetComponentsInChildren<Transform>().Count(t=>t.name=="CC0 industrial_wall_lamp")==12,"Twelve downloaded caged lamps");
            Check(scenery.GetComponentsInChildren<Transform>().Count(t=>t.name=="CC0 power_box_01")==4,"Four downloaded power boxes");
            Check(world.GetComponentsInChildren<Transform>().Count(t=>t.name=="Authored security drone")==5,"Five original drone targets use authored mesh");
            foreach(var rotor in world.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Rotor_")))
                Check(Mathf.Abs(Vector3.Dot(rotor.TransformDirection(Vector3.up).normalized,Vector3.up))>.98f,"Rotor rotates around vertical axis");
            foreach(var target in world.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Security drone"))
                Check(target.size==new Vector3(1.3f,.65f,1.1f),"Original target hitbox preserved");
            var systems=world.GetComponentsInChildren<ParticleSystem>();
            Check(systems.Length==11&&systems.Sum(p=>p.main.maxParticles)<=800,"Bounded particle budget");
            CaptureWorld("visual-interior.png");
            Place(new Vector3(0,1.77f,-20),Quaternion.identity);
            CaptureWorld("visual-exterior.png");
            var enemy=world.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Security drone");
            var focus=enemy.transform.position;
            Place(focus+new Vector3(0,.35f,2.7f),Quaternion.LookRotation(new Vector3(0,-.35f,-2.7f)));
            CaptureWorld("visual-drone.png");
            Place(new Vector3(-22,1.77f,-12),Quaternion.identity);
            clock+=.2;
            int ammo=mission.Ammo;mission.Fire(Vector3.down);
            Check(mission.Ammo==ammo-1,"Shot behavior preserved");
            Check(world.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="Pooled shot tracer"&&l.enabled),"Shot tracer emitted");
            mission.SetPaused(true);yield return null;
            Check(systems.All(p=>p.isPaused),"Particles pause with game");
            mission.SetPaused(false);yield return null;
            mission.ThreatEvent();yield return null;
            Check(mission.IsThreatResponse,"Original recall response");
            var ui=GetComponent<UIDocument>().rootVisualElement;
            Check(ui.Q("missionPresentation").resolvedStyle.display==DisplayStyle.None,"No motion overlay during recall");
            clock+=1;mission.CompleteThreatResponse();yield return null;
            mission.Exit();yield return null;yield return null;
            Check(!GameObject.Find("Terminal detail · CC0 industrial dressing")&&!GetComponent<MissionImageEffect>(),"World and finish cleanup");
            Debug.Log("VISUAL_UPGRADE_PLAY_PASS · models, collision, particles, fire, recall overlay, cleanup");
            EditorApplication.Exit(0);
        }
        void Place(Vector3 at,Quaternion rotation)
        {
            var body=GetComponent<CharacterController>();body.enabled=false;transform.SetPositionAndRotation(at,rotation);body.enabled=true;Physics.SyncTransforms();
        }
        static void Check(bool condition,string message)
        {
            if(condition)return;
            Debug.LogError("VISUAL_UPGRADE_FAILED "+message);EditorApplication.Exit(2);throw new System.InvalidOperationException(message);
        }
        IEnumerator CaptureUi(string path,int width,int height)
        {
            var settings=GetComponent<UIDocument>().panelSettings;var rt=new RenderTexture(width,height,24);settings.targetTexture=rt;
            yield return null;yield return null;yield return new WaitForSecondsRealtime(.15f);
            Save(rt,path);settings.targetTexture=null;Destroy(rt);
        }
        void CaptureWorld(string path)
        {
            var camera=GetComponent<Camera>();var rt=new RenderTexture(1600,1000,24);
            camera.targetTexture=rt;camera.Render();Save(rt,path);camera.targetTexture=null;Destroy(rt);
        }
        void Save(RenderTexture texture,string path)
        {
            var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;Destroy(image);
        }
    }
}
#endif
