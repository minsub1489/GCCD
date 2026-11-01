#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace GCCD.Dts
{
    public sealed class DtsMissionProbe:MonoBehaviour
    {
        IEnumerator Start()
        {
            var p=GetComponent<ExperimentPlatform>();p.Clock=()=>100;p.StartMission();yield return null;yield return null;
            var m=GetComponent<MissionDemo>();if(!m||!m.HasWorld)Fail("World initialization");
            int meshes=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;if(meshes<30)Fail("Imported meshes not placed");
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var camera=GetComponent<Camera>();var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("mission-preview.png",image.EncodeToPNG());
                camera.targetTexture=null;RenderTexture.active=old;Destroy(rt);Destroy(image);
            }
            var supplies=GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Supply ")).ToArray();
            foreach(var supply in supplies){transform.position=supply.position+Vector3.up;if(!m.CollectNearby())Fail("E collection");}
            if(m.SuppliesCollected!=3)Fail("Three supplies");
            var targets=GameObject.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.gameObject.name=="target-large").ToArray();
            foreach(var target in targets){transform.position=target.bounds.center+Vector3.back*2;Physics.SyncTransforms();if(!m.Fire(Vector3.forward))Fail("Target raycast");}
            if(m.ShotsHit!=5)Fail("Five hits");m.ThreatEvent();yield return null;
            m.Exit();yield return null;if(!p.enabled||GetComponent<CharacterController>())Fail("Return to setup cleanup");
            File.WriteAllText("mission-result.txt","PASS imported meshes="+meshes+"; collect 3 supplies; raycast 5 targets; simulated surprise pulse; cleanup and menu return. No human/physical validation.");Debug.Log("DTS_MISSION_PROBE_PASS");EditorApplication.Exit(0);
        }
        static void Fail(string s){Debug.LogError("DTS_MISSION_PROBE_FAILED "+s);EditorApplication.Exit(2);throw new Exception(s);}
    }
}
#endif
