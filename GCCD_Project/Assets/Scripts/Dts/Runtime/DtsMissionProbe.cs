#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed class DtsMissionProbe:MonoBehaviour
    {
        IEnumerator Start()
        {
            var p=GetComponent<ExperimentPlatform>();p.Clock=()=>100;
            var camera=GetComponent<Camera>();float originalFar=camera.farClipPlane;bool originalHdr=camera.allowHDR;Color originalSky=RenderSettings.ambientSkyColor;Color originalFog=RenderSettings.fogColor;
            if(!GetComponent<ModernInterface>()||!GetComponent<ModernInterface>().HasLobby)Fail("Modern lobby initialization");
            yield return null;yield return null;
            var button=GetComponent<UIDocument>().rootVisualElement.Q<Button>(className:"primary");button.Focus();yield return null;Debug.Log("DTS_UI_BUTTON attached="+(button.panel!=null)+" enabled="+button.enabledInHierarchy);using(var submit=NavigationSubmitEvent.GetPooled()){button.SendEvent(submit);}yield return null;yield return null;
            var m=GetComponent<MissionDemo>();if(!m||!m.HasWorld)Fail("World initialization");
            m.SetPaused(false);ScreenCapture.CaptureScreenshot("mission-hud.png");yield return null;yield return null;
            double time=1000;m.Clock=()=>time;
            int meshes=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;if(meshes<100)Fail("Detailed environment not placed");
            if(!m.HasGunAudio)Fail("Recorded shot audio missing");
            Physics.SyncTransforms();if(m.EnemyPositions.Length!=5)Fail("Random enemy count");
            m.StepWeapon(.1f);float hip=GetComponent<Camera>().fieldOfView;Capture("mission-preview.png");var gun=transform.Find("akm");gun.gameObject.SetActive(false);Capture("lobby-background.png");gun.gameObject.SetActive(true);
            m.SetAiming(true);m.StepWeapon(.5f);if(GetComponent<Camera>().fieldOfView>=hip-10)Fail("ADS FOV transition");Capture("mission-ads.png");
            m.SetAiming(false);m.StepWeapon(.5f);
            var supplies=FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Supply ")).ToArray();
            foreach(var supply in supplies){transform.position=supply.position+Vector3.up;if(!m.CollectNearby())Fail("E collection");}
            if(m.SuppliesCollected!=3)Fail("Three supplies");
            var targets=FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.gameObject.name=="Security drone").ToArray();
            foreach(var target in targets){transform.position=target.bounds.center+Vector3.back*2;Physics.SyncTransforms();time+=.2;if(!m.Fire(Vector3.forward))Fail("Enemy raycast");int fired=m.ShotsFired,ammo=m.Ammo;m.Fire(Vector3.forward);if(m.ShotsFired!=fired||m.Ammo!=ammo)Fail("Fire rate enforcement");}
            if(m.ShotsHit!=5)Fail("Five enemy hits");
            if(!m.Reload()||!m.IsReloading)Fail("Reload begin");int before=m.ShotsFired;m.Fire(Vector3.up);if(m.ShotsFired!=before)Fail("Fire blocked while reloading");time+=2;m.StepWeapon(.1f);if(m.Ammo!=30||m.IsReloading)Fail("Reload completion");
            m.ThreatEvent();yield return null;if(m.EnemyPositions.Length!=3)Fail("Random offscreen threat count");
            m.Exit();yield return null;if(!p.enabled||GetComponent<CharacterController>()||GetComponent<MissionImageEffect>())Fail("Mission cleanup");
            if(camera.farClipPlane!=originalFar||camera.allowHDR!=originalHdr||RenderSettings.ambientSkyColor!=originalSky||RenderSettings.fogColor!=originalFog)Fail("Research camera and render settings restored");
            File.WriteAllText("mission-result.txt","PASS realistic environment meshes="+meshes+"; native UI Toolkit lobby; randomized 5 enemies; ADS/FOV; recorded shot AudioClip; 3 supply collection; 5 raycast kills; shot cooldown; reload/fire blocking/refill; random 3 offscreen threats; simulation-only pulse; cleanup/menu return. Physical sound audibility and physical haptics not verified.");Debug.Log("DTS_MISSION_PROBE_PASS");EditorApplication.Exit(0);
        }
        void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=GetComponent<Camera>();var rt=new RenderTexture(1440,900,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(name,image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=old;Destroy(rt);Destroy(image);
        }
        static void Fail(string s){Debug.LogError("DTS_MISSION_PROBE_FAILED "+s);EditorApplication.Exit(2);throw new Exception(s);}
    }
}
#endif
