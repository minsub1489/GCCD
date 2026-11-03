using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class MissionDemo
    {
        readonly MissionLocationResponse locationResponse=new MissionLocationResponse();
        MissionThreatRecord responseRecord;string responsePath,responseError="";double responseStarted,responseReadyAt;
        Vector3[] pendingSources=new Vector3[0];
        public bool IsThreatResponse {get{return locationResponse.Active;}}
        public bool ResponseReady {get{return IsThreatResponse&&RealNow>=responseReadyAt;}}
        public string ResponseNotice {get{
            if(responseRecord==null||responseRecord.receipt.simulated)return "조끼 없이 화면 체험 중 · 게임 일시정지";
            if(!responseRecord.receipt.submitted)return "조끼 출력 실패 · 체험 응답으로 기록됩니다";
            if(responseRecord.pulse_interrupted)return "진동 출력 중지됨 · 기억나는 위치를 표시하세요";
            return "진동 요청 후 위치 응답 중 · 게임 일시정지";
        }}
        public string ResponseError {get{return responseError;}}
        public Vector3 ResponseObserver {get;private set;}
        public float ResponseYaw {get;private set;}
        public MissionSpawnPoint[] ResponseMarkers {get{return locationResponse.Markers;}}
        [Serializable] sealed class MissionThreatRecord
        {
            public string mode="MISSION_DEMO",event_id,state,utc,mapping_version,haptic_encoding;public bool not_research_data=true;public int seed;
            public Vector3 observer_position;public float observer_yaw;public Vector3[] source_positions,response_positions;
            public OutputReceipt receipt;public double response_seconds;public double intensity_low=MissionLow,intensity_high=MissionHigh;public bool pulse_interrupted;
            public int suppression_theta;public bool[] source_suppressed;
        }
        void BeginThreatResponse(Threat[] threats,OutputReceipt receipt,bool[] suppressed)
        {
            ResponseObserver=transform.position;ResponseYaw=yaw;responseStarted=RealNow;responseReadyAt=RealNow+.2;
            responseError="";pendingSources=threats.Select(t=>new Vector3((float)t.X,(float)t.Y,(float)t.Z)).ToArray();
            string id=Guid.NewGuid().ToString("N");responsePath=Path.Combine(folder,"event-"+id+".json");
            responseRecord=new MissionThreatRecord{haptic_encoding=MissionHapticEncoding.Version,mapping_version=Owner.X40MappingVersion,event_id=id,state="awaiting_response",utc=DateTime.UtcNow.ToString("O"),seed=spawnSeed,observer_position=ResponseObserver,observer_yaw=yaw,source_positions=pendingSources,source_suppressed=suppressed,suppression_theta=SuppressionTheta,receipt=receipt,response_positions=new Vector3[0]};
            locationResponse.Begin();ApplyPause(true,false);WriteResponse();
        }
        bool WriteResponse()
        {
            try{SessionStore.AtomicWrite(responsePath,JsonUtility.ToJson(responseRecord,true));responseError="";return true;}
            catch(IOException){responseError="응답을 저장하지 못했습니다. 저장 공간을 확인한 뒤 다시 눌러 주세요.";return false;}
            catch(UnauthorizedAccessException){responseError="저장 권한을 확인한 뒤 다시 눌러 주세요.";return false;}
        }
        public bool ToggleResponseMarker(double x,double z){return ResponseReady&&locationResponse.Toggle(x,z);}
        public void UndoResponseMarker(){if(ResponseReady)locationResponse.Undo();}
        public void ClearResponseMarkers(){if(ResponseReady)locationResponse.Clear();}
        public bool CompleteThreatResponse()
        {
            if(!ResponseReady)return false;
            responseRecord.state="completed";responseRecord.response_seconds=RealNow-responseStarted;
            responseRecord.response_positions=locationResponse.Markers.Select(p=>new Vector3((float)p.X,0,(float)p.Z)).ToArray();
            if(!WriteResponse())return false;
            Owner.StopMissionHaptics();locationResponse.Complete();
            // Sources stay hidden during the answer, including in the rendered world.
            foreach(var p in pendingSources)if(shootable.Count<14)SpawnEnemy(p);
            pendingSources=new Vector3[0];nextThreat=Now+9+rng.NextDouble()*5;ApplyPause(false,false);return true;
        }
        void AbortThreatResponse()
        {
            if(!IsThreatResponse)return;responseRecord.state="aborted";responseRecord.response_seconds=RealNow-responseStarted;
            responseRecord.response_positions=locationResponse.Markers.Select(p=>new Vector3((float)p.X,0,(float)p.Z)).ToArray();WriteResponse();locationResponse.Complete();
        }
    }
}
