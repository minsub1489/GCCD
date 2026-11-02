using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if GCCD_BHAPTICS_SDK2
using Bhaptics.SDK2;
#endif
namespace GCCD.Dts
{
    [Serializable] public sealed class OutputReceipt
    {
        public string status="NOT_ATTEMPTED",error="",device="",utc="";
        public int request_id=-1;public bool submitted,simulated;
        public double submit_started,submit_returned;
    }
    public sealed class BhapticsAdapter
    {
        readonly LocalPlayerClient local=new LocalPlayerClient();
        bool useLocal;public string Transport {get{return useLocal?"local-player-v1":"not-connected";}}
        public Func<double> Clock;
        double Now {get{return Clock!=null?Clock():Time.realtimeSinceStartupAsDouble;}}
        public string[] ConnectedVestNames {get;private set;}=new string[0];
        readonly HashSet<int> owned=new HashSet<int>();
        public string DeviceInventory{get;private set;}="Official SDK not imported/enabled.";
        public void Initialize()
        {
#if GCCD_BHAPTICS_SDK2
            var initializers=UnityEngine.Object.FindObjectsByType<BhapticsSDK2>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if(initializers.Length==0)throw new InvalidOperationException("Official initializer prefab missing.");
            var settings=BhapticsSettings.Instance;
            if(settings==null||string.IsNullOrWhiteSpace(settings.AppId)||string.IsNullOrWhiteSpace(settings.ApiKey)||settings.LastDeployVersion<0)throw new InvalidOperationException("Link/deploy this app in the official Developer Window first.");
            var chosen=initializers.FirstOrDefault(x=>x.isActiveAndEnabled)??initializers[0];
            foreach(var sdk in initializers)if(sdk!=chosen)sdk.enabled=false;
            chosen.enabled=true;chosen.gameObject.SetActive(true);
#else
            throw new InvalidOperationException("Official SDK not imported/enabled.");
#endif
        }
        public bool Ready(ExperimentConfig config,out string reason)
        {
            try
            {
                if(useLocal){reason=local.VestConnected?"Player 연결됨":"Player 조끼 연결을 확인하세요.";return local.VestConnected;}
#if GCCD_BHAPTICS_SDK2
                if(UnityEngine.Object.FindFirstObjectByType<BhapticsSDK2>()==null){reason="Official [bhaptics] scene initializer missing.";return false;}
                var settings=BhapticsSettings.Instance;
                if(settings==null||string.IsNullOrWhiteSpace(settings.AppId)||string.IsNullOrWhiteSpace(settings.ApiKey)||settings.LastDeployVersion<0){reason="Link/deploy the existing app in bHaptics Developer Window.";return false;}
                if(!BhapticsLibrary.IsBhapticsAvailableForce(false)){reason="bHaptics Player unavailable.";return false;}
                var devices=BhapticsLibrary.GetDevices();var names=new List<string>();var vests=new List<string>();bool matched=false;
                foreach(var d in devices){if(d.IsConnected&&d.Position==PositionType.Vest)vests.Add(d.DeviceName);names.Add(d.DeviceName+" ["+d.Position+"] connected="+d.IsConnected);if(d.IsConnected&&d.Position==PositionType.Vest&&string.Equals(d.DeviceName,config.VerifiedDeviceName,StringComparison.Ordinal))matched=true;}
                ConnectedVestNames=vests.Distinct(StringComparer.Ordinal).ToArray();
                DeviceInventory=string.Join("\n",names);
                if(!matched){reason="Connected vest name does not match the operator-verified X40 device.\n"+DeviceInventory;return false;}
                reason="Verified device name connected; physical delivery is not acknowledged by this API.";return true;
#else
                reason=DeviceInventory;return false;
#endif
            }
            catch(Exception e){reason="SDK connection error: "+e.GetBaseException().Message;return false;}
        }
        public bool AutoConnect(ExperimentConfig config,out string reason)
        {
            local.BeginConnect();
            if(local.VestConnected){useLocal=true;ConnectedVestNames=new[]{"Player의 조끼"};if(string.IsNullOrEmpty(config.LockedHash))config.VerifiedDeviceName=ConnectedVestNames[0];reason="Player 연결됨 · 로그인 불필요";return true;}
            reason="Player에서 연결된 조끼를 기다리는 중입니다.";return false;
        }

        public OutputReceipt Submit(int[] frame,int milliseconds,ExperimentConfig config,bool simulation,bool calibrationProbe=false)
        {
            var r=new OutputReceipt{simulated=simulation,utc=DateTime.UtcNow.ToString("O"),submit_started=Now,device=simulation?"VIRTUAL":config.VerifiedDeviceName};
            try
            {
                if(frame==null||frame.Length!=40||milliseconds<100)throw new ArgumentException("40 channels and >=100 ms required.");
                foreach(int v in frame)if(v<0||v>100)throw new ArgumentException("Motor range is 0..100.");
                if(simulation){r.status="SIMULATED_NO_PHYSICAL_OUTPUT";r.submitted=true;return r;}
                if(calibrationProbe){if(!config.AllowRaw40CalibrationProbe)throw new InvalidOperationException("Explicit raw-40 calibration opt-in required; SDK docs do not guarantee X40 shape.");}
                else{config.Mapping.Validate(true);if(!config.Raw40TransportConfirmed||!config.PhysicalConcurrencyConfirmed)throw new InvalidOperationException("X40 transport/concurrency unverified.");}
                string reason;if(!Ready(config,out reason))throw new InvalidOperationException(reason);
if(useLocal){if(!calibrationProbe&&!config.TransportEvidence.Contains("local-player-v1"))throw new InvalidOperationException("새 Player 출력 경로의 교정을 먼저 확인하세요.");local.Submit(frame,milliseconds);r.submitted=true;r.status="PLAYER_REQUEST_SENT_PHYSICAL_UNCONFIRMED";return r;}
                throw new InvalidOperationException("X40는 Player 앞20/뒤20 직접 연결을 사용해야 합니다. 연결을 다시 확인하세요.");
            }
            catch(Exception e){r.status="TECHNICAL_FAILURE";r.error=e.GetBaseException().Message;Stop();}
            finally{r.submit_returned=Now;}
            return r;
        }
        public void Dispose(){Stop();local.Dispose();}
        public string Stop()
        {
            string error="";
            try{local.Stop();}catch(Exception e){error=e.GetBaseException().Message;}
#if GCCD_BHAPTICS_SDK2
            foreach(int id in owned)try{if(!BhapticsLibrary.StopInt(id))error="SDK did not confirm stopping request "+id;}catch(Exception e){error=e.GetBaseException().Message;}
#endif
            owned.Clear();return error;
        }
    }
}
