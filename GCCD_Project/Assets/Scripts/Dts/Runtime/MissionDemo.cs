using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class ExperimentPlatform
    {
        public void StartMission(int suppressionTheta = 45){if(Phase!=TrialPhase.Setup)return;var m=gameObject.AddComponent<MissionDemo>();m.Owner=this;m.SuppressionTheta=suppressionTheta;m.Physical=connectionReady;enabled=false;}
        public OutputReceipt MissionPulse(Threat[] threats,bool physical){var motors=MissionHapticEncoding.Compose(threats,MissionX40Profile());config.AllowRaw40CalibrationProbe=true;return haptics.Submit(motors,MissionHapticEncoding.DurationMilliseconds,config,!physical,true);}
        public void EndMission(){haptics.Stop();enabled=true;transform.SetPositionAndRotation(new Vector3(0,1.7f,0),Quaternion.identity);}
        public void StopMissionHaptics(){haptics.Stop();}
        public bool AdvancedVisible {get{return advanced;}}
        public void ConfirmPulse(){if(pulseSubmitted){feltPulse=true;testText="연결 테스트 확인됨";}}
        public string PulseStatus {get{return testText;}}
        public bool LobbySimulation => string.IsNullOrEmpty(config.LobbyMode) || config.LobbyMode == "DEVELOPMENT";
        public void StartLobbyExperiment(bool training = false, int? theta = null, int minimum = 0, int maximum = 0)
        {
            Mode = LobbySimulation ? "DEVELOPMENT" : config.LobbyMode;
            StartSession(training, theta, minimum, maximum);
        }
    }
    public sealed partial class MissionDemo:MonoBehaviour
    {
        public const double MissionLow=MissionHapticEncoding.Low,MissionHigh=MissionHapticEncoding.High;
        public ExperimentPlatform Owner;public bool Physical;public Func<double> Clock;
        GameObject world,weapon,muzzle;CharacterController body;Camera cam;Color oldBackground;bool oldFog;
        readonly Dictionary<Collider,GameObject> shootable=new Dictionary<Collider,GameObject>();readonly List<GameObject> supplies=new List<GameObject>();
        int originalLayer;int hits,collected,ammo=30,fired,spawnSeed;float yaw,pitch,vertical,aimBlend,recoil,walkAmount,weaponTop;double nextThreat,lastThreat,started,nextShot,reloadUntil,lastHit,flashUntil;string output="",folder;bool paused,complete,aiming,exited;
        System.Random rng;AudioSource gunAudio;AudioClip shotClip,reloadClip;Light muzzleLight;
        double Now {get{return playClock.Read(RealNow);}}
        public int ShotsHit{get{return hits;}}public int SuppliesCollected{get{return collected;}}public bool HasWorld{get{return world!=null;}}
        public int Ammo{get{return ammo;}}public int ShotsFired{get{return fired;}}public bool IsReloading{get{return reloadUntil>Now;}}public bool IsPaused{get{return paused;}}public bool IsComplete{get{return complete;}}public bool IsAiming{get{return aiming;}}public bool HitMarker{get{return Now-lastHit<.15&&lastHit>0;}}
        public bool HasGunAudio{get{return shotClip&&gunAudio;}}public int SpawnSeed{get{return spawnSeed;}}public string OutputText{get{return Now-lastThreat<2.5?output:"";}}
        public Vector3[] SupplyPositions{get{return supplies.Where(s=>s).Select(s=>s.transform.position+Vector3.up*.9f).ToArray();}}
        public double Elapsed{get{return Now-started;}}public Vector3[] EnemyPositions{get{return shootable.Values.Where(x=>x).Select(x=>x.transform.position).ToArray();}}
        void Start()
        {
            originalLayer=gameObject.layer;gameObject.layer=30;cam=GetComponent<Camera>();oldBackground=cam.backgroundColor;oldFog=RenderSettings.fog;
            transform.SetPositionAndRotation(new Vector3(-22,1.77f,-12),Quaternion.identity);yaw=0;
            body=gameObject.AddComponent<CharacterController>();body.height=1.8f;body.center=new Vector3(0,-.8f,0);body.radius=.3f;body.stepOffset=.25f;
            world=new GameObject("MISSION ONLY · Photographed freight terminal");BuildEnvironment();Physics.SyncTransforms();
            spawnSeed=UnityEngine.Random.Range(1,int.MaxValue);rng=new System.Random(spawnSeed);
            var points=MissionSpawnPlanner.Plan(spawnSeed,5,transform.position.x,transform.position.z,0,false,Occupied,true);foreach(var p in points)SpawnEnemy(new Vector3((float)p.X,1.9f,(float)p.Z));
            weapon=RealModel("akm",Vector3.zero,.88f,Pbr("akm"),false,Quaternion.AngleAxis(90,Vector3.forward)*Quaternion.Euler(0,90,180));weaponTop=weapon.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y);foreach(var r in weapon.GetComponentsInChildren<Renderer>())r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;weapon.transform.SetParent(transform,false);weapon.transform.localPosition=new Vector3(.22f,-weaponTop-.08f,.56f);weapon.transform.localRotation=Quaternion.identity;
            foreach(var c in weapon.GetComponentsInChildren<Collider>())Destroy(c);
            muzzle=GameObject.CreatePrimitive(PrimitiveType.Sphere);muzzle.name="Muzzle flash";Destroy(muzzle.GetComponent<Collider>());muzzle.transform.SetParent(transform);muzzle.transform.localPosition=new Vector3(.17f,-.08f,1.0f);muzzle.transform.localScale=new Vector3(.07f,.07f,.16f);var glow=new Material(Shader.Find("Standard"));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(6,3,.5f));muzzle.GetComponent<Renderer>().sharedMaterial=glow;ownedVisuals.Add(glow);muzzle.SetActive(false);
            muzzleLight=new GameObject("Shot light").AddComponent<Light>();muzzleLight.transform.SetParent(transform);muzzleLight.transform.localPosition=new Vector3(0,0,.8f);muzzleLight.color=new Color(1,.65f,.25f);muzzleLight.range=7;muzzleLight.intensity=0;
            gunAudio=gameObject.AddComponent<AudioSource>();gunAudio.spatialBlend=0;gunAudio.playOnAwake=false;gunAudio.volume=.65f;gunAudio.priority=32;shotClip=Resources.Load<AudioClip>("Realism/Audio/rifle-shot");reloadClip=Resources.Load<AudioClip>("Realism/Audio/reload");if(!shotClip)throw new InvalidOperationException("Rifle audio missing.");
            var finish=gameObject.AddComponent<MissionImageEffect>();finish.enabled=true;
            started=Now;nextThreat=started+8;
            folder=Path.Combine(Application.persistentDataPath,"ExperimentData","MISSION_DEMO",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
            SessionStore.AtomicWrite(Path.Combine(folder,"layout.json"),JsonUtility.ToJson(new MissionLayoutRecord{seed=spawnSeed,enemy_positions=EnemyPositions},true));
            SetPaused(false);
        }
        [Serializable] class MissionLayoutRecord{public int seed;public Vector3[] enemy_positions;}
        bool Occupied(double x,double z){return Physics.CheckBox(new Vector3((float)x,1.8f,(float)z),new Vector3(.7f,.65f,.7f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);}
        void SpawnEnemy(Vector3 p)
        {
            var enemy=new GameObject("Security drone");enemy.transform.SetParent(world.transform);enemy.transform.position=p;
            AddDronePart(enemy,PrimitiveType.Sphere,Vector3.zero,new Vector3(.7f,.45f,.6f),trim);
            AddDronePart(enemy,PrimitiveType.Cylinder,new Vector3(0,0,-.3f),new Vector3(.18f,.1f,.18f),orange,Quaternion.Euler(90,0,0));
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1}){AddDronePart(enemy,PrimitiveType.Cube,new Vector3(x*.35f,.04f,z*.25f),new Vector3(.75f,.06f,.08f),steel,Quaternion.Euler(0,x*z*35,0));AddDronePart(enemy,PrimitiveType.Cylinder,new Vector3(x*.56f,.1f,z*.42f),new Vector3(.4f,.025f,.4f),trim);}
            var c=enemy.AddComponent<BoxCollider>();c.size=new Vector3(1.3f,.65f,1.1f);shootable.Add(c,enemy);
        }
        void AddDronePart(GameObject parent,PrimitiveType shape,Vector3 at,Vector3 scale,Material material,Quaternion rotation=default(Quaternion))
        {var p=GameObject.CreatePrimitive(shape);p.transform.SetParent(parent.transform,false);p.transform.localPosition=at;p.transform.localScale=scale;p.transform.localRotation=rotation==default(Quaternion)?Quaternion.identity:rotation;p.GetComponent<Renderer>().sharedMaterial=material;Destroy(p.GetComponent<Collider>());}
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) || (MapVisible && Input.GetKeyDown(KeyCode.Escape)))
            {
                ToggleMap();
                return;
            }
            if(Input.GetKeyDown(KeyCode.Escape)&&!IsThreatResponse)SetPaused(!paused);
            if(paused||complete)return;
            float sensitivity=aiming?1.0f:1.8f;yaw+=Input.GetAxis("Mouse X")*sensitivity;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*sensitivity,-75,75);transform.rotation=Quaternion.Euler(pitch-recoil*.35f,yaw,0);
            if(Input.GetKeyDown(KeyCode.C))SetCrouched(!crouched);
            StepMovement(new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")),Input.GetKeyDown(KeyCode.Space),Input.GetKey(KeyCode.LeftShift),Time.deltaTime);
            SetAiming(Input.GetMouseButton(1));if(Input.GetMouseButton(0))Fire(transform.forward);if(Input.GetKeyDown(KeyCode.R))Reload();if(Input.GetKeyDown(KeyCode.E))CollectNearby();
            StepWeapon(Time.deltaTime);
            if(TryCompleteMission())return;
            if(Now>=nextThreat&&Now-lastThreat>3)ThreatEvent();
        }
        public void SetAiming(bool value){aiming=value&&!IsReloading&&!paused;}
        public void StepWeapon(float dt)
        {
            if(!weapon||!cam)return;if(reloadUntil>0&&Now>=reloadUntil){ammo=30;reloadUntil=0;}
            aimBlend=Mathf.Lerp(aimBlend,aiming?1:0,1-Mathf.Exp(-dt*15));recoil=Mathf.MoveTowards(recoil,0,dt*12);
            cam.fieldOfView=Mathf.Lerp(previousFov,42,aimBlend);
            Vector3 hip=new Vector3(.22f,-weaponTop-.08f,.56f),ads=new Vector3(0,-weaponTop-.012f,.47f);float bob=Mathf.Sin(Time.time*10)*.006f*walkAmount*(1-aimBlend);
            weapon.transform.localPosition=Vector3.Lerp(hip,ads,aimBlend)+new Vector3(0,bob,-recoil*.018f);weapon.transform.localRotation=Quaternion.Euler(-recoil+(IsReloading?24:0),0,IsReloading?-22:0);
            muzzle.transform.localPosition=Vector3.Lerp(new Vector3(.22f,-.1f,1),new Vector3(0,-.01f,.95f),aimBlend);
            bool flash=Now<flashUntil;muzzle.SetActive(flash);muzzleLight.intensity=flash?2.5f:0;
        }
        public bool Reload(){if(paused||complete||IsReloading||ammo==30)return false;reloadUntil=Now+1.7;aiming=false;if(reloadClip)gunAudio.PlayOneShot(reloadClip,.65f);return true;}
        public bool CollectNearby(){if(paused||complete)return false;var item=supplies.FirstOrDefault(s=>s&&Vector3.Distance(s.transform.position,transform.position)<3);if(!item)return false;supplies.Remove(item);Destroy(item);collected++;nextThreat=Math.Min(nextThreat,Now+.8);return true;}
        public bool Fire(Vector3 direction)
        {
            if(paused||complete||IsReloading||Now<nextShot||ammo==0)return false;
            nextShot=Now+.13;ammo--;fired++;recoil=Mathf.Min(5,recoil+1.4f);flashUntil=Now+.045;gunAudio.pitch=UnityEngine.Random.Range(.96f,1.04f);gunAudio.PlayOneShot(shotClip);
            RaycastHit hit;bool didHit=Physics.Raycast(transform.position,direction,out hit,90,~(1<<30),QueryTriggerInteraction.Ignore);
            bool success=didHit&&shootable.ContainsKey(hit.collider);
            if(success){hits++;lastHit=Now;var target=shootable[hit.collider];shootable.Remove(hit.collider);Destroy(target);}
            if(didHit)Impact(hit.point,hit.normal);
            return success;
        }
        void Impact(Vector3 p,Vector3 n){var spark=GameObject.CreatePrimitive(PrimitiveType.Sphere);spark.name="Impact";Destroy(spark.GetComponent<Collider>());spark.transform.position=p+n*.03f;spark.transform.localScale=Vector3.one*.05f;spark.GetComponent<Renderer>().sharedMaterial=orange;Destroy(spark,.2f);}
        public void ThreatEvent()
        {
            if(paused||complete||IsThreatResponse)return;
            lastThreat=Now;nextThreat=Now+9+rng.NextDouble()*5;
            try{
                int count=rng.Next(1,4);
                var locations=MissionSpawnPlanner.Plan(rng.Next(),count,transform.position.x,transform.position.z,yaw,true,Occupied,true);
                var threats=locations.Select((p,i)=>{var at=new Vector3((float)p.X,1.9f,(float)p.Z);var delta=at-transform.position;return new Threat("M"+i,true,true,true,at.x,at.y,at.z,delta.magnitude,Angles.Bearing(delta.x,delta.z,yaw));}).ToArray();
                BeginMissionThreatEvent(threats);
            }catch(ArgumentException){output="생성할 공간이 부족합니다. 개방된 구역으로 이동하세요.";}
        }
        public int SuppressionTheta { get; internal set; } = 45;
        internal void BeginMissionThreatEvent(Threat[] threats)
        {
            var policy = ThreatPolicy.Apply(threats, SuppressionTheta);
            var retained = new HashSet<string>(policy.Kept.Select(t => t.Id), StringComparer.Ordinal);
            var receipt = Owner.MissionPulse(policy.Kept, Physical);
            output = receipt.simulated ? "화면 체험 · 위치를 표시해 주세요" : receipt.submitted ? "위치 응답 대기" : "조끼 출력 실패 · 체험 응답";
            BeginThreatResponse(threats, receipt, threats.Select(t => !retained.Contains(t.Id)).ToArray());
        }
        public void SetPaused(bool value)
        {
            if(!value&&(IsThreatResponse||complete))return;
            if(value&&IsThreatResponse&&RealNow<responseReadyAt)responseRecord.pulse_interrupted=true;
            ApplyPause(value,value);
        }
        void OnApplicationFocus(bool focused){if(!focused)SetPaused(true);}
        public void Exit(){if(exited)return;exited=true;Cleanup();Owner.EndMission();Destroy(this);}
        void Cleanup(){gameObject.layer=originalLayer;AbortThreatResponse();if(!string.IsNullOrEmpty(folder))ExportMissionData();if(Owner)Owner.StopMissionHaptics();if(cam){cam.backgroundColor=oldBackground;RenderSettings.fog=oldFog;RestoreEnvironment();}if(gunAudio){gunAudio.Stop();Destroy(gunAudio);}if(muzzle)Destroy(muzzle);if(muzzleLight)Destroy(muzzleLight.gameObject);var effect=GetComponent<MissionImageEffect>();if(effect)Destroy(effect);if(world)Destroy(world);if(weapon)Destroy(weapon);if(body)Destroy(body);}
        void OnDestroy(){if(!exited)Cleanup();}
    }
}
