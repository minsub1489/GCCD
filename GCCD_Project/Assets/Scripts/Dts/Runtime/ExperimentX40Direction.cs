using System;
using System.IO;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class ExperimentPlatform
    {
        [Serializable] sealed class X40Options {public bool mirrorFront,mirrorBack,swapPanels;}
        [Serializable] sealed class HapticCheckRecord
        {
            public string kind,mapping,haptic_encoding;public int bearing,duration_ms;public double distance_m,intensity_low,intensity_high;
            public int[] motors;public OutputReceipt receipt;
        }
        X40Options x40Options;int directionChecks,distanceChecks,lastDirection=-1,lastDistance=-1;double nextCheckPulse;
        public bool MirrorX40Front {get{return Options.mirrorFront;}}
        public bool MirrorX40Back {get{return Options.mirrorBack;}}
        public bool SwapX40Panels {get{return Options.swapPanels;}}
        public int DirectionCheckCount {get{return CountChecks(directionChecks,4);}}
        public int DistanceCheckCount {get{return CountChecks(distanceChecks,3);}}
        public bool CanConfirmDirection {get{return lastDirection>=0&&pulseSubmitted&&!feltPulse;}}
        public bool CanConfirmDistance {get{return lastDistance>=0&&pulseSubmitted&&!feltPulse;}}
        public string X40MappingVersion {get{return MissionX40Profile().Version;}}
        static int CountChecks(int mask,int length){int count=0;for(int i=0;i<length;i++)if((mask&(1<<i))!=0)count++;return count;}
        X40Options Options {get{
            if(x40Options!=null)return x40Options;x40Options=new X40Options();
            string path=Path.Combine(Application.persistentDataPath,"DtsSettings","x40_layout.json");
            if(File.Exists(path))try{x40Options=JsonUtility.FromJson<X40Options>(File.ReadAllText(path))??new X40Options();}catch(Exception e) when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){notice="X40 보정 설정을 읽지 못해 기본 배열을 사용합니다.";}
            return x40Options;
        }}
        public MotorProfile MissionX40Profile(){return X40Layout.Profile(Options.mirrorFront,Options.mirrorBack,Options.swapPanels);}
        public void ResetPendingHapticCheck(){StopMissionHaptics();lastDirection=lastDistance=-1;pulseSubmitted=false;testText="단계를 눌러 짧은 진동을 확인하세요.";}
        public void SetX40Correction(bool mirrorFront,bool mirrorBack,bool swapPanels)
        {
            ResetPendingHapticCheck();Options.mirrorFront=mirrorFront;Options.mirrorBack=mirrorBack;Options.swapPanels=swapPanels;directionChecks=distanceChecks=0;
            string path=Path.Combine(Application.persistentDataPath,"DtsSettings","x40_layout.json");Directory.CreateDirectory(Path.GetDirectoryName(path));SessionStore.AtomicWrite(path,JsonUtility.ToJson(Options,true));testText="배열을 보정했습니다. 방향과 거리 세기를 다시 확인하세요.";
        }
        public int[] DistanceCheckPreview(int level){return MissionHapticEncoding.CheckFrame(0,level,MissionX40Profile());}
        public OutputReceipt TestDirectionPulse(int direction){return PlayHapticCheck(direction,1,false);}
        public OutputReceipt TestDistancePulse(int level){return PlayHapticCheck(0,level,true);}
        OutputReceipt PlayHapticCheck(int direction,int level,bool distanceCheck)
        {
            // Both checks use the exact mission renderer; distance checks keep front and duration fixed.
            var frame=MissionHapticEncoding.CheckFrame(direction,level,MissionX40Profile());
            double now=Time.realtimeSinceStartupAsDouble;if(now<nextCheckPulse)throw new InvalidOperationException("짧은 진동이 끝난 뒤 다시 눌러 주세요.");
            lastDirection=lastDistance=-1;pulseSubmitted=false;
            ConnectEasy();if(!connectionReady)throw new InvalidOperationException(connectionText);
            haptics.Stop();physicalProbe=true;config.AllowRaw40CalibrationProbe=true;
            var receipt=haptics.Submit(frame,MissionHapticEncoding.DurationMilliseconds,config,false,true);
            nextCheckPulse=now+.5;lastDirection=lastDistance=-1;pulseSubmitted=receipt.submitted;feltPulse=false;
            if(receipt.submitted){if(distanceCheck)lastDistance=level;else lastDirection=direction;}
            testText=receipt.submitted?(distanceCheck?DistanceName(level)+" · 앞 방향":DirectionName(direction)+" 방향")+"으로 0.2초 요청했습니다. 느껴졌나요?":"출력 실패 · "+receipt.error;
            string directory=Path.Combine(Root,"CALIBRATION");Directory.CreateDirectory(directory);
            var record=new HapticCheckRecord{kind=distanceCheck?"X40_DISTANCE_CHECK":"X40_DIRECTION_CHECK",mapping=X40MappingVersion,haptic_encoding=MissionHapticEncoding.Version,bearing=direction*90,distance_m=MissionHapticEncoding.CheckDistance(level),intensity_low=MissionHapticEncoding.Low,intensity_high=MissionHapticEncoding.High,duration_ms=MissionHapticEncoding.DurationMilliseconds,motors=frame,receipt=receipt};
            SessionStore.AtomicWrite(Path.Combine(directory,(distanceCheck?"distance-":"direction-")+Guid.NewGuid().ToString("N")+".json"),JsonUtility.ToJson(record,true));
            return receipt;
        }
        public void ConfirmDirectionPulse(){if(!CanConfirmDirection)return;directionChecks|=1<<lastDirection;ConfirmPulse();testText=DirectionName(lastDirection)+" 방향 확인됨 · "+DirectionCheckCount+"/4";}
        public void ConfirmDistancePulse(){if(!CanConfirmDistance)return;distanceChecks|=1<<lastDistance;ConfirmPulse();testText=DistanceName(lastDistance)+" 확인됨 · "+DistanceCheckCount+"/3";}
        public static string DirectionName(int direction){return new[]{"앞","오른쪽","뒤","왼쪽"}[direction];}
        public static string DistanceName(int level){return new[]{"가까움 6m · 강함","중간 21m · 중간 세기","멀리 36m · 약함"}[level];}
    }
}
