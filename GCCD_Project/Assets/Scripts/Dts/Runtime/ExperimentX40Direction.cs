using System;
using System.IO;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class ExperimentPlatform
    {
        [Serializable] sealed class X40Options {public bool mirrorFront,mirrorBack,swapPanels;}
        X40Options x40Options;int directionChecks,lastDirection=-1;double nextDirectionPulse;
        public bool MirrorX40Front {get{return Options.mirrorFront;}}
        public bool MirrorX40Back {get{return Options.mirrorBack;}}
        public bool SwapX40Panels {get{return Options.swapPanels;}}
        public int DirectionCheckCount {get{int n=0;for(int i=0;i<4;i++)if((directionChecks&(1<<i))!=0)n++;return n;}}
        public string X40MappingVersion {get{return MissionX40Profile().Version;}}
        X40Options Options {get{
            if(x40Options!=null)return x40Options;x40Options=new X40Options();
            string path=Path.Combine(Application.persistentDataPath,"DtsSettings","x40_layout.json");
            if(File.Exists(path))try{x40Options=JsonUtility.FromJson<X40Options>(File.ReadAllText(path))??new X40Options();}catch(Exception e) when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){notice="X40 보정 설정을 읽지 못해 기본 배열을 사용합니다.";}
            return x40Options;
        }}
        public MotorProfile MissionX40Profile(){return X40Layout.Profile(Options.mirrorFront,Options.mirrorBack,Options.swapPanels);}
        public void SetX40Correction(bool mirrorFront,bool mirrorBack,bool swapPanels)
        {
            StopMissionHaptics();Options.mirrorFront=mirrorFront;Options.mirrorBack=mirrorBack;Options.swapPanels=swapPanels;directionChecks=0;lastDirection=-1;
            string path=Path.Combine(Application.persistentDataPath,"DtsSettings","x40_layout.json");Directory.CreateDirectory(Path.GetDirectoryName(path));SessionStore.AtomicWrite(path,JsonUtility.ToJson(Options,true));testText="배열을 보정했습니다. 네 방향을 다시 확인하세요.";
        }
        public OutputReceipt TestDirectionPulse(int direction)
        {
            if(direction<0||direction>3)throw new ArgumentException("방향을 선택하세요.");
            double now=Time.realtimeSinceStartupAsDouble;if(now<nextDirectionPulse)throw new InvalidOperationException("짧은 진동이 끝난 뒤 다시 눌러 주세요.");
            ConnectEasy();if(!connectionReady)throw new InvalidOperationException(connectionText);
            haptics.Stop();physicalProbe=true;config.AllowRaw40CalibrationProbe=true;
            var threat=new Threat("DIRECTION_TEST",true,true,true,0,0,0,6,direction*90);
            var frame=MotorComposer.Compose(new[]{threat},MissionX40Profile(),.20,.35);var receipt=haptics.Submit(frame,200,config,false,true);
            nextDirectionPulse=now+.5;lastDirection=receipt.submitted?direction:-1;pulseSubmitted=receipt.submitted;feltPulse=false;
            testText=receipt.submitted?DirectionName(direction)+" 방향으로 0.2초 요청했습니다. 그 방향에서 느껴졌나요?":"출력 실패 · "+receipt.error;
            string directory=Path.Combine(Root,"CALIBRATION");Directory.CreateDirectory(directory);
            SessionStore.AtomicWrite(Path.Combine(directory,"direction-"+Guid.NewGuid().ToString("N")+".json"),"{\"kind\":\"X40_DIRECTION_CHECK\",\"bearing\":"+(direction*90)+",\"mapping\":\""+X40MappingVersion+"\",\"motors\":["+string.Join(",",frame)+"],\"receipt\":"+JsonUtility.ToJson(receipt)+"}");
            return receipt;
        }
        public void ConfirmDirectionPulse(){if(lastDirection<0||!pulseSubmitted)return;directionChecks|=1<<lastDirection;ConfirmPulse();testText=DirectionName(lastDirection)+" 방향 확인됨 · "+DirectionCheckCount+"/4";}
        public static string DirectionName(int direction){return new[]{"앞","오른쪽","뒤","왼쪽"}[direction];}
    }
}
