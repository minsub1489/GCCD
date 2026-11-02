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
            var ui=GetComponent<UIDocument>().rootVisualElement;Submit(ui.Q<Button>(className:"primary"));yield return null;yield return null;
            var m=GetComponent<MissionDemo>();if(!m||!m.HasWorld)Fail("World initialization");m.Physical=false;m.SetPaused(false);
            double time=Time.realtimeSinceStartupAsDouble;m.Clock=()=>time;
            int meshes=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;if(meshes<100)Fail("Detailed environment not placed");
            if(!m.HasGunAudio)Fail("Recorded shot audio missing");Physics.SyncTransforms();if(m.EnemyPositions.Length!=5)Fail("Random indoor enemy count");
            m.StepWeapon(.1f);float hip=camera.fieldOfView;Capture("mission-preview.png");var gun=transform.Find("akm");gun.gameObject.SetActive(false);Capture("lobby-background.png");gun.gameObject.SetActive(true);
            m.SetAiming(true);m.StepWeapon(.5f);if(camera.fieldOfView>=hip-10)Fail("ADS FOV transition");Capture("mission-ads.png");m.SetAiming(false);m.StepWeapon(.5f);
            // Traverse an actual facade doorway and inner doorway, without teleporting across walls.
            Place(new Vector3(-10,1.77f,-10));for(int i=0;i<190;i++)m.StepMovement(Vector2.left,false,false,.02f);
            if(transform.position.x>-21.5f)Fail("Interior entrance traversal");
            Place(new Vector3(-22,1.77f,-12));for(int i=0;i<20;i++)m.StepMovement(Vector2.zero,false,false,.02f);
            float beforeY=transform.position.y;m.StepMovement(Vector2.zero,true,false,.02f);for(int i=0;i<12;i++)m.StepMovement(Vector2.zero,false,false,.02f);
            if(transform.position.y<beforeY+.5f)Fail("Space jump height");for(int i=0;i<120;i++)m.StepMovement(Vector2.zero,false,false,.02f);if(Mathf.Abs(transform.position.y-beforeY)>.15f)Fail("Jump landing");
            if(!m.SetCrouched(true)||!m.IsCrouched)Fail("C crouch toggle");if(Mathf.Abs(GetComponent<CharacterController>().height-1)>0.01)Fail("Crouch collider height");
            Place(new Vector3(-29,1.00f,17));if(m.SetCrouched(false)||!m.IsCrouched)Fail("Headroom blocks standing");
            Place(new Vector3(-22,1.0f,-12));if(!m.SetCrouched(false))Fail("Standing after leaving hatch");
            m.ToggleMap();yield return null;if(!m.MapVisible)Fail("Tab minimap show");var minimaps=ui.Query<MissionMapView>().ToList();if(minimaps.Count!=3)Fail("Map UI initialization");m.ToggleMap();if(m.MapVisible)Fail("Tab minimap hide");
            var supplies=FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Supply ")).ToArray();foreach(var supply in supplies){Place(supply.position+Vector3.up);if(!m.CollectNearby())Fail("E collection");}if(m.SuppliesCollected!=3)Fail("Three indoor supplies");
            var targets=FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.gameObject.name=="Security drone").ToArray();foreach(var target in targets){Place(target.bounds.center+Vector3.up*.5f);time+=.2;if(!m.Fire(Vector3.down)){RaycastHit detail;Physics.Raycast(transform.position,Vector3.down,out detail,90);Fail("Enemy raycast paused="+m.IsPaused+" rounds="+m.Ammo+" hits="+m.ShotsHit+" collider="+(detail.collider?detail.collider.name:"none"));}int fired=m.ShotsFired,rounds=m.Ammo;m.Fire(Vector3.down);if(m.ShotsFired!=fired||m.Ammo!=rounds)Fail("Fire rate enforcement");}if(m.ShotsHit!=5)Fail("Five enemy hits");
            if(!m.Reload()||!m.IsReloading)Fail("Reload begin");int before=m.ShotsFired;m.Fire(Vector3.up);if(m.ShotsFired!=before)Fail("Fire blocked while reloading");time+=2;m.StepWeapon(.1f);if(m.Ammo!=30||m.IsReloading)Fail("Reload completion");
            Place(new Vector3(-22,1.77f,-12));m.ThreatEvent();if(m.ReviewEvents.Length!=0)Fail("No answer leak before completion");if(!m.IsThreatResponse||!m.IsPaused)Fail("Haptic freezes game for response");
            if(m.EnemyPositions.Length!=0)Fail("Sources hidden before submission");if(m.CompleteThreatResponse())Fail("Pulse completes before response is allowed");
            var frozen=transform.position;int oldAmmo=m.Ammo;double oldElapsed=m.Elapsed;time+=60;m.StepMovement(Vector2.one,true,true,.02f);m.Fire(Vector3.forward);m.Reload();m.SetPaused(false);m.ToggleMap();
            if(transform.position!=frozen||m.Ammo!=oldAmmo||m.Elapsed!=oldElapsed||!m.IsPaused||m.MapVisible)Fail("Response blocks gameplay and timers");
            yield return null;var response=ui.Q<MissionMapView>("responseMap");if(!response.Interactive||ui.Q("threatResponse").resolvedStyle.display==DisplayStyle.None)Fail("Response map visible");
            response.ClearClassList();response.AddToClassList("mission-map");response.AddToClassList("response-map");
            ClickMap(response,.25f,.75f);if(m.ResponseMarkers.Length!=1||Math.Abs(m.ResponseMarkers[0].X+18)>.001||Math.Abs(m.ResponseMarkers[0].Z+5.5)>.001)Fail("Pointer click world coordinate mismatch "+string.Join(";",m.ResponseMarkers.Select(v=>v.X+","+v.Z)));
            m.ClearResponseMarkers();
            response.SelectAt(new Vector2(response.contentRect.width/2,response.contentRect.height/2));if(m.ResponseMarkers.Length!=1)Fail("Map click creates response");m.UndoResponseMarker();if(m.ResponseMarkers.Length!=0)Fail("Undo response marker");m.ToggleResponseMarker(-28,8);
            Submit(ui.Q<Button>("responseSubmit"));if(m.IsThreatResponse||m.IsPaused)Fail("Submit resumes game");if(m.EnemyPositions.Length<1||m.EnemyPositions.Length>3)Fail("Deferred random threats appear after answer");
            // An empty answer is valid and must not disclose a required marker count.
            m.ThreatEvent();time+=1;if(!m.CompleteThreatResponse()||m.IsPaused)Fail("Empty response resumes");
            if(!m.TryCompleteMission()||!m.IsComplete||!m.ExportsReady)Fail("Mission result and CSV completion");
            if(m.ReviewEvents.Length!=2)Fail("Result event count");
            yield return null;if(ui.Q("missionResults").resolvedStyle.display==DisplayStyle.None)Fail("Result UI visible");
            var resultMap=ui.Q<MissionMapView>("resultMap");if(resultMap.ActualSources.Length==0)Fail("Completed result reveals actual sources");
            var reviewed=m.ReviewEvents[0];if(resultMap.Markers.Length!=reviewed.Marks.Length||resultMap.Markers.Where((mark,i)=>Math.Abs(mark.X-reviewed.Marks[i].x)>.0001||Math.Abs(mark.Z-reviewed.Marks[i].z)>.0001).Any())Fail("Result display changes reported coordinates");
            if(!resultMap.ActualSources.SequenceEqual(reviewed.Sources))Fail("Result actual and reported positions swapped");
            foreach(var name in new[]{"summary.csv","events.csv","positions.csv"})if(!File.Exists(Path.Combine(m.CsvFolder,name)))Fail("Missing CSV "+name);
            string summary=File.ReadAllText(Path.Combine(m.CsvFolder,"summary.csv"));if(!summary.Contains("SIMULATED")||!summary.Contains("MISSION_DEMO"))Fail("CSV simulation grouping");
            string beforeExport=File.ReadAllText(Path.Combine(m.CsvFolder,"events.csv"));m.ExportMissionData();if(File.ReadAllText(Path.Combine(m.CsvFolder,"events.csv"))!=beforeExport)Fail("Duplicate-free re-export");
            var settings=GetComponent<UIDocument>().panelSettings;var resultTexture=new RenderTexture(1600,1000,24);settings.targetTexture=resultTexture;
            yield return null;yield return null;yield return null;
            var previous=RenderTexture.active;RenderTexture.active=resultTexture;var screenshot=new Texture2D(1600,1000,TextureFormat.RGB24,false);screenshot.ReadPixels(new Rect(0,0,1600,1000),0,0);screenshot.Apply();File.WriteAllBytes("mission-results-ui.png",screenshot.EncodeToPNG());RenderTexture.active=previous;settings.targetTexture=null;Destroy(resultTexture);Destroy(screenshot);
            File.WriteAllText("results-folder.txt",m.CsvFolder);Debug.Log("DTS_RESULT_FLOW_PASS");
            m.Exit();yield return null;if(!p.enabled||GetComponent<CharacterController>()||GetComponent<MissionImageEffect>())Fail("Mission cleanup");
            if(camera.farClipPlane!=originalFar||camera.allowHDR!=originalHdr||RenderSettings.ambientSkyColor!=originalSky||RenderSettings.fogColor!=originalFog)Fail("Research render settings restored");
            File.WriteAllText("mission-result.txt","PASS meshes="+meshes+"; indoor door traversal; jump/landing; crouch/ceiling clearance; Tab map; ADS/audio; supplies/hits/reload; haptic response freezes gameplay/timers; sources hidden; map click/undo; UI submit resumes; empty answer; cleanup. Physical sound and haptics unverified.");Debug.Log("DTS_MISSION_PROBE_PASS");EditorApplication.Exit(0);
        }
        void ClickMap(MissionMapView map,float u,float v)
        {
            Rect c=map.contentRect;float scale=Mathf.Min((c.width-28)/72,(c.height-28)/78);
            var local=new Vector2(c.x+(c.width-72*scale)/2+72*scale*u,c.y+(c.height-78*scale)/2+78*scale*v);var panel=map.LocalToWorld(local);
            using(var click=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,mousePosition=panel,button=0}))map.SendEvent(click);
        }
        void Place(Vector3 position){var body=GetComponent<CharacterController>();body.enabled=false;transform.position=position;transform.rotation=Quaternion.identity;body.enabled=true;Physics.SyncTransforms();}
        void Submit(Button button){if(button==null)Fail("Missing UI button");button.Focus();using(var submit=NavigationSubmitEvent.GetPooled())button.SendEvent(submit);}
        void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=GetComponent<Camera>();var rt=new RenderTexture(1440,900,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(name,image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;Destroy(rt);Destroy(image);
        }
        static void Fail(string s){Debug.LogError("DTS_MISSION_PROBE_FAILED "+s);EditorApplication.Exit(2);throw new Exception(s);}
    }
}
#endif
