using System;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed partial class ModernInterface
    {
        VisualElement directionOverlay;Label directionText,directionCount;Button directionConfirm;readonly Button[] directionButtons=new Button[4];string directionError="";
        void BuildDirectionTest()
        {
            directionOverlay=Element(root,"direction-overlay");directionOverlay.name="directionCheck";
            var card=Element(directionOverlay,"direction-card");Label(card,"TACTSUIT X40  /  FRONT 20 + BACK 20","eyebrow accent");Label(card,"짧은 진동 · 방향 확인","direction-title");
            Label(card,"조끼를 바르게 입고 화면 정면을 향해 앉으세요.\n착용한 사람 기준으로 앞·오른쪽·뒤·왼쪽을 확인합니다.","description");
            var row=Element(card,"direction-buttons");for(int i=0;i<4;i++){int direction=i;directionButtons[i]=Button(row,ExperimentPlatform.DirectionName(i),()=>{try{platform.TestDirectionPulse(direction);directionError="";}catch(Exception e){directionError=e.Message;}},"button direction-button");directionButtons[i].name="testDirection"+i;}
            directionText=Label(card,"방향을 누르면 해당 방향으로 0.2초간 진동을 요청합니다.","direction-message");
            directionConfirm=Button(card,"방금 누른 방향에서 느껴졌어요",()=>platform.ConfirmDirectionPulse(),"button primary");directionCount=Label(card,"","small muted");
            var corrections=new Foldout{text="방향이 반대로 느껴지면 보정",value=false};card.Add(corrections);
            var front=new Toggle("앞면 좌우 반전"){value=platform.MirrorX40Front};var back=new Toggle("뒷면 좌우 반전"){value=platform.MirrorX40Back};var swap=new Toggle("앞·뒤 패널 교환"){value=platform.SwapX40Panels};
            corrections.Add(front);corrections.Add(back);corrections.Add(swap);
            System.Action save=()=>{try{platform.SetX40Correction(front.value,back.value,swap.value);directionError="";}catch(Exception e){directionError=e.Message;}};
            front.RegisterValueChangedCallback(e=>save());back.RegisterValueChangedCallback(e=>save());swap.RegisterValueChangedCallback(e=>save());
            Label(corrections,"이 보정은 이 노트북의 미션·방향 검사에 저장됩니다.\n보정 후 네 방향을 다시 확인하세요.","small muted");
            var footer=Element(card,"row");Button(footer,"진동 중지",()=>platform.StopMissionHaptics(),"button secondary");Button(footer,"확인 마치기",()=>directionOverlay.style.display=DisplayStyle.None,"button secondary").name="closeDirectionCheck";directionOverlay.style.display=DisplayStyle.None;
        }
        void OpenDirectionTest(){directionError="";directionOverlay.style.display=DisplayStyle.Flex;}
        void RefreshDirectionTest()
        {
            if(directionOverlay==null||directionOverlay.style.display==DisplayStyle.None)return;
            foreach(var button in directionButtons)button.SetEnabled(platform.ConnectionReady);
            directionText.text=!platform.ConnectionReady?"Player에서 X40를 연결해 주세요. 연결되면 방향 버튼을 누를 수 있습니다.":string.IsNullOrEmpty(directionError)?platform.PulseStatus:directionError;
            directionConfirm.SetEnabled(platform.PulseStatus.Contains("느껴졌나요"));directionCount.text="직접 확인한 방향 "+platform.DirectionCheckCount+" / 4 · X40 4열 × 5행 × 앞뒤";
        }
    }
}
