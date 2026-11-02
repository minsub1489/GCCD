using System;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed partial class ModernInterface
    {
        VisualElement directionOverlay,directionPane,distancePane;Label directionText,directionCount;Button directionConfirm,directionTab,distanceTab;readonly Button[] directionButtons=new Button[4],distanceButtons=new Button[3];string directionError="";bool distanceMode;
        void BuildDirectionTest()
        {
            directionOverlay=Element(root,"direction-overlay");directionOverlay.name="directionCheck";
            var card=Element(directionOverlay,"direction-card");Label(card,"TACTSUIT X40  /  BEFORE YOU START","eyebrow accent");Label(card,"진동 · 방향과 거리 확인","direction-title");
            var tabs=Element(card,"direction-buttons check-tabs");directionTab=Button(tabs,"01  방향",()=>SelectHapticCheck(false),"button direction-button");directionTab.name="showDirectionCheck";distanceTab=Button(tabs,"02  거리 · 세기",()=>SelectHapticCheck(true),"button direction-button");distanceTab.name="showDistanceCheck";
            directionPane=Element(card,"");directionPane.name="directionPane";
            Label(directionPane,"조끼를 바르게 입고 화면 정면을 향해 앉으세요.\n착용한 사람 기준 네 방향을 같은 세기로 확인합니다.","description");
            var row=Element(directionPane,"direction-buttons");for(int i=0;i<4;i++){int direction=i;directionButtons[i]=Button(row,ExperimentPlatform.DirectionName(i),()=>{try{platform.TestDirectionPulse(direction);directionError="";}catch(Exception e){directionError=e.Message;}},"button direction-button");directionButtons[i].name="testDirection"+i;}
            distancePane=Element(card,"");distancePane.name="distancePane";
            Label(distancePane,"같은 앞 방향 · 같은 0.2초, 세기만 달라집니다.\n멀리 → 중간 → 가까움 순서로 눌러 비교하세요.","description");
            var distances=Element(distancePane,"direction-buttons");foreach(int value in new[]{2,1,0}){int level=value;string[] names={"가까움 · 강함\n6m","중간 세기\n21m","멀리 · 약함\n36m"};distanceButtons[level]=Button(distances,names[level],()=>{try{platform.TestDistancePulse(level);directionError="";}catch(Exception e){directionError=e.Message;}},"button direction-button distance-button");distanceButtons[level].name="testDistance"+level;}
            directionText=Label(card,"방향을 누르면 해당 방향으로 0.2초간 진동을 요청합니다.","direction-message");
            directionConfirm=Button(card,"방금 누른 방향에서 느껴졌어요",()=>{if(distanceMode)platform.ConfirmDistancePulse();else platform.ConfirmDirectionPulse();},"button primary");directionConfirm.name="confirmHapticCheck";directionCount=Label(card,"","small muted");
            var corrections=new Foldout{text="방향이 반대로 느껴지면 보정",value=false};card.Add(corrections);
            var front=new Toggle("앞면 좌우 반전"){value=platform.MirrorX40Front};var back=new Toggle("뒷면 좌우 반전"){value=platform.MirrorX40Back};var swap=new Toggle("앞·뒤 패널 교환"){value=platform.SwapX40Panels};
            corrections.Add(front);corrections.Add(back);corrections.Add(swap);
            System.Action save=()=>{try{platform.SetX40Correction(front.value,back.value,swap.value);directionError="";}catch(Exception e){directionError=e.Message;}};
            front.RegisterValueChangedCallback(e=>save());back.RegisterValueChangedCallback(e=>save());swap.RegisterValueChangedCallback(e=>save());
            Label(corrections,"이 보정은 이 노트북의 미션과 진동 검사에 저장됩니다.\n보정 후 방향과 거리 세기를 다시 확인하세요.","small muted");
            var footer=Element(card,"row");Button(footer,"진동 중지",()=>{platform.ResetPendingHapticCheck();directionError="";},"button secondary");Button(footer,"확인 마치기",()=>{platform.ResetPendingHapticCheck();directionOverlay.style.display=DisplayStyle.None;},"button secondary").name="closeDirectionCheck";SelectHapticCheck(false);directionOverlay.style.display=DisplayStyle.None;
        }
        void SelectHapticCheck(bool distance){distanceMode=distance;directionError="";platform.ResetPendingHapticCheck();directionPane.style.display=distance?DisplayStyle.None:DisplayStyle.Flex;distancePane.style.display=distance?DisplayStyle.Flex:DisplayStyle.None;directionTab.EnableInClassList("selected-check",!distance);distanceTab.EnableInClassList("selected-check",distance);}
        void OpenDirectionTest(){SelectHapticCheck(false);directionOverlay.style.display=DisplayStyle.Flex;}
        void RefreshDirectionTest()
        {
            if(directionOverlay==null||directionOverlay.style.display==DisplayStyle.None)return;
            foreach(var button in directionButtons)button.SetEnabled(platform.ConnectionReady);
            foreach(var button in distanceButtons)button.SetEnabled(platform.ConnectionReady);
            directionText.text=!platform.ConnectionReady?"Player에서 X40를 연결해 주세요. 연결되면 진동 버튼을 누를 수 있습니다.":string.IsNullOrEmpty(directionError)?platform.PulseStatus:directionError;
            directionConfirm.text=distanceMode?"방금 누른 세기를 느꼈어요":"방금 누른 방향에서 느껴졌어요";directionConfirm.SetEnabled(platform.ConnectionReady&&(distanceMode?platform.CanConfirmDistance:platform.CanConfirmDirection));directionCount.text=distanceMode?"직접 확인한 거리 세기 "+platform.DistanceCheckCount+" / 3 · 게임과 같은 세기 계산":"직접 확인한 방향 "+platform.DirectionCheckCount+" / 4 · 착용자 기준";
        }
    }
}
