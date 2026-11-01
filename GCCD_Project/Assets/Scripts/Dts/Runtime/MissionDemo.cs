using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class ExperimentPlatform
    {
        public void StartMission(){if(Phase!=TrialPhase.Setup)return;var mission=gameObject.AddComponent<MissionDemo>();mission.Owner=this;mission.Physical=connectionReady;enabled=false;}
        public OutputReceipt MissionPulse(Threat[] threats,bool physical)
        {
            var policy=ThreatPolicy.Apply(threats,0);var motors=MotorComposer.Compose(policy.Kept,config.Mapping,.08,.20);
            config.AllowRaw40CalibrationProbe=true;return haptics.Submit(motors,200,config,!physical,true);
        }
        public void EndMission(){haptics.Stop();enabled=true;transform.SetPositionAndRotation(new Vector3(0,1.7f,0),Quaternion.identity);}
    }
    public sealed class MissionDemo:MonoBehaviour
    {
        public ExperimentPlatform Owner;public bool Physical;
        GameObject world,weapon;CharacterController body;Camera cam;Color oldBackground;bool oldFog;
        readonly Dictionary<Collider,GameObject> shootable=new Dictionary<Collider,GameObject>();readonly List<GameObject> supplies=new List<GameObject>();
        readonly List<GameObject> spawned=new List<GameObject>();
        int hits,collected;float yaw,pitch,vertical;double nextThreat,lastThreat,started;string output="위협 대기 중",folder;bool paused,complete;
        GUIStyle text,big;Font font;System.Random rng=new System.Random(1102);
        public int ShotsHit{get{return hits;}}public int SuppliesCollected{get{return collected;}}public bool HasWorld{get{return world!=null;}}
        void Start()
        {
            cam=GetComponent<Camera>();oldBackground=cam.backgroundColor;oldFog=RenderSettings.fog;cam.backgroundColor=new Color(.25f,.35f,.44f);RenderSettings.fog=true;RenderSettings.fogColor=cam.backgroundColor;RenderSettings.fogDensity=.012f;transform.SetPositionAndRotation(new Vector3(0,1.7f,-14),Quaternion.identity);
            body=gameObject.AddComponent<CharacterController>();body.height=1.7f;body.center=new Vector3(0,-.8f,0);body.radius=.3f;
            world=new GameObject("MISSION ONLY · CC0 industrial outpost");
            Primitive("Ground",new Vector3(0,-.2f,10),new Vector3(90,.4f,100),new Color(.19f,.23f,.25f));
            for(int i=0;i<8;i++){
                float x=i%2==0?-18:18,z=-8+(i/2)*17;
                Model("city-kit-industrial/building-"+(char)('a'+i),new Vector3(x,0,z),new Vector3(0,i%2==0?90:-90,0),12,true);
            }
            for(int i=0;i<6;i++)Model("city-kit-industrial/shipping-container-"+(char)('a'+i%3),new Vector3(i%2==0?-9:9,0,i*8-4),Vector3.zero,5,true);
            for(int i=0;i<16;i++)Model("survival-kit/"+(i%2==0?"barrel":"box-large"),new Vector3((i%2==0?-1:1)*(6+i%3),0,-4+i*3),Vector3.zero,1.1f,true);
            for(int i=0;i<3;i++){var obj=Model("blaster-kit/crate-medium",new Vector3(i%2==0?-4:4,.15f,i*13-1),Vector3.zero,1.1f,false);obj.name="Supply "+i;supplies.Add(obj);}
            for(int i=0;i<5;i++)SpawnTarget(new Vector3(i%2==0?-5:5,1.6f,8+i*7));
            weapon=Model("blaster-kit/blaster-e",Vector3.zero,Vector3.zero,.65f,false);weapon.transform.SetParent(transform,false);weapon.transform.localPosition=new Vector3(.3f,-.3f,.6f);weapon.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var c in weapon.GetComponentsInChildren<Collider>())Destroy(c);
            started=Time.realtimeSinceStartupAsDouble;nextThreat=started+8;
            folder=Path.Combine(Application.persistentDataPath,"ExperimentData","MISSION_DEMO",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        GameObject Primitive(string name,Vector3 position,Vector3 scale,Color color){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(world.transform);o.transform.position=position;o.transform.localScale=scale;o.GetComponent<Renderer>().material.color=color;return o;}
        GameObject Model(string path,Vector3 position,Vector3 rotation,float size,bool collider)
        {
            var prefab=Resources.Load<GameObject>("FreeAssets/"+path);if(!prefab)throw new InvalidOperationException("Missing licensed asset "+path);
            var o=Instantiate(prefab,world.transform);o.name=path.Split('/').Last();o.transform.rotation=Quaternion.Euler(rotation);
            var renderers=o.GetComponentsInChildren<Renderer>();var b=new Bounds(o.transform.position,Vector3.zero);foreach(var r in renderers)b.Encapsulate(r.bounds);
            float s=size/Mathf.Max(.01f,Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)));o.transform.localScale*=s;
            b=new Bounds(o.transform.position,Vector3.zero);foreach(var r in renderers)b.Encapsulate(r.bounds);o.transform.position=position-new Vector3(b.center.x,b.min.y,b.center.z);
            b=new Bounds(o.transform.position,Vector3.zero);foreach(var r in renderers)b.Encapsulate(r.bounds);
            if(collider){var c=o.AddComponent<BoxCollider>();c.center=o.transform.InverseTransformPoint(b.center);c.size=b.size/s;}
            return o;
        }
        void SpawnTarget(Vector3 p){var t=Model("blaster-kit/target-large",p,new Vector3(0,180,0),1.4f,false);var r=t.GetComponentsInChildren<Renderer>();var b=new Bounds(p,Vector3.zero);foreach(var x in r)b.Encapsulate(x.bounds);var c=t.AddComponent<BoxCollider>();c.size=new Vector3(1,1.5f,.5f)/t.transform.localScale.x;c.center=t.transform.InverseTransformPoint(b.center);shootable[c]=t;}
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused;}
            if(paused||complete)return;
            yaw+=Input.GetAxis("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-75,75);transform.rotation=Quaternion.Euler(pitch,yaw,0);
            Vector3 walk=Quaternion.Euler(0,yaw,0)*new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));walk=Vector3.ClampMagnitude(walk,1)*4;
            if(body.isGrounded)vertical=-2;else vertical-=9.81f*Time.deltaTime;walk.y=vertical;body.Move(walk*Time.deltaTime);
            if(Input.GetMouseButtonDown(0))Fire(transform.forward);
            weapon.transform.localPosition=Vector3.Lerp(weapon.transform.localPosition,new Vector3(.3f,-.3f,.6f),Time.deltaTime*14);
            if(Input.GetKeyDown(KeyCode.E))CollectNearby();
            if(Time.realtimeSinceStartupAsDouble>=nextThreat&&Time.realtimeSinceStartupAsDouble-lastThreat>3)ThreatEvent();
            if(collected>=3&&hits>=5){complete=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        public bool CollectNearby(){var item=supplies.FirstOrDefault(s=>s&&Vector3.Distance(s.transform.position,transform.position)<3);if(!item)return false;supplies.Remove(item);Destroy(item);collected++;nextThreat=Math.Min(nextThreat,Time.realtimeSinceStartupAsDouble+.8);return true;}
        public bool Fire(Vector3 direction){RaycastHit hit;bool success=Physics.Raycast(transform.position,direction,out hit,70)&&shootable.ContainsKey(hit.collider);if(success){hits++;var target=shootable[hit.collider];shootable.Remove(hit.collider);Destroy(target);}weapon.transform.localPosition=new Vector3(.3f,-.28f,.52f);return success;}
        public void ThreatEvent()
        {
            lastThreat=Time.realtimeSinceStartupAsDouble;nextThreat=lastThreat+9+rng.NextDouble()*5;
            var angles=new[]{115.0,180.0,250.0};var distances=new[]{8.0,16.0,26.0};
            var threats=angles.Select((angle,i)=>{Vector3 p=transform.position+Quaternion.Euler(0,yaw+(float)angle,0)*Vector3.forward*(float)distances[i];return new Threat("M"+i,true,true,true,p.x,p.y,p.z,distances[i],angle);}).ToArray();
            var receipt=Owner.MissionPulse(threats,Physical);output=receipt.simulated?"화면 밖 위협 발생 · 조끼 없음":receipt.submitted?"화면 밖 위협 발생 · 진동 요청 전송":"진동 연결 확인 필요";
            string json="{\"mode\":\"MISSION_DEMO\",\"not_research_data\":true,\"elapsed\":"+(lastThreat-started).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"camera_yaw\":"+yaw.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"receipt\":"+JsonUtility.ToJson(receipt)+"}";
            SessionStore.AtomicWrite(Path.Combine(folder,"event-"+Guid.NewGuid().ToString("N")+".json"),json);
        }
        void OnGUI()
        {
            if(text==null){font=Font.CreateDynamicFontFromOSFont(Application.platform==RuntimePlatform.WindowsPlayer?"Malgun Gothic":"Apple SD Gothic Neo",20);text=new GUIStyle(GUI.skin.label){font=font,fontSize=20,wordWrap=true};big=new GUIStyle(text){fontSize=30,fontStyle=FontStyle.Bold};}
            GUI.Label(new Rect(28,25,600,42),"산업 기지 · 보급품 회수",big);GUI.Label(new Rect(28,72,700,90),"보급품 "+collected+" / 3     표적 "+hits+" / 5\nWASD 이동  ·  마우스 조준/사격  ·  E 수집  ·  ESC 메뉴",text);
            GUI.Label(new Rect(Screen.width/2-12,Screen.height/2-20,30,40),"+",big);GUI.Label(new Rect(28,Screen.height-85,900,70),output+"\n체험 모드 · 교정 전 방향 정확도를 보장하지 않으며 본 실험 자료에 포함하지 않습니다.",text);
            foreach(var supply in supplies)if(supply){var p=cam.WorldToScreenPoint(supply.transform.position+Vector3.up*1.5f);if(p.z>0)GUI.Label(new Rect(p.x-45,Screen.height-p.y,150,36),"보급품 [E]",text);}
            if(paused||complete){GUI.Box(new Rect(Screen.width/2-230,Screen.height/2-130,460,260),"");GUI.Label(new Rect(Screen.width/2-180,Screen.height/2-110,400,50),complete?"미션 완료":"일시정지",big);if(!complete&&GUI.Button(new Rect(Screen.width/2-170,Screen.height/2-40,340,55),"계속")){paused=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}if(GUI.Button(new Rect(Screen.width/2-170,Screen.height/2+30,340,55),"시작 화면으로"))Exit();}
        }
        void OnApplicationFocus(bool focused){if(!focused){paused=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
        public void Exit(){cam.backgroundColor=oldBackground;RenderSettings.fog=oldFog;if(world)Destroy(world);if(body)Destroy(body);Owner.EndMission();Destroy(this);}
        void OnDestroy(){if(world)Destroy(world);if(body)Destroy(body);}
    }
}
