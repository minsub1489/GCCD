using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed partial class ModernInterface
    {
        VisualElement mapPanel,responseOverlay;MissionMapView miniMap,responseMap;Label responseHelp,responseStatus,responseMarkCount,responseEventCount;Button responseSubmit,responseUndo,responseClear;
        void BuildMaps()
        {
            mapPanel=Element(hud,"mini-map-panel");Label(mapPanel,"시설 지도  /  N ↑","eyebrow");miniMap=new MissionMapView{ShowDistanceScale=true};mapPanel.Add(miniMap);Label(mapPanel,"▲ 현재 위치   ◆ 보급품   TAB 닫기","small muted");
            mapPanel.style.height=480;
            Button(mapPanel,"실험 종료",()=>GetComponent<MissionDemo>().EndExperiment(),"button primary").name="mapEndExperiment";
            Button(mapPanel,"로비로 돌아가기",()=>GetComponent<MissionDemo>().Exit(),"button secondary").name="mapReturnLobby";
            responseOverlay=Element(root,"response-overlay");responseOverlay.name="threatResponse";
            var shell=Element(responseOverlay,"response-shell");
            var heading=Element(shell,"response-heading");Label(heading,"HAPTIC RECALL  /  N ↑","eyebrow accent");Label(heading,"위협이 어디에 있었나요?","response-title");
            responseHelp=Label(heading,"","response-description");
            responseEventCount=Label(heading,"","small accent");
            responseEventCount.name="responseEventCount";
            var row=Element(shell,"response-body");responseMap=new MissionMapView{Detailed=true,ShowDistanceScale=true};responseMap.name="responseMap";responseMap.AddToClassList("response-map");row.Add(responseMap);
            responseMap.Picked=(x,z)=>{var m=GetComponent<MissionDemo>();if(m)m.ToggleResponseMarker(x,z);};
            var tools=Element(row,"response-tools");Label(tools,"기억나는 위치를 표시하세요","objective-title");
            Label(tools,"지도 클릭  위치 표시\n표시한 점 클릭  삭제\n\n▲ 진동 당시 내 위치와 방향\n● 내가 표시한 위치\n주황색 통로  앉아서 통과","response-description");
            Label(tools,"기억나지 않으면 비워 두고 완료할 수 있습니다.","small muted");
            responseUndo=Button(tools,"마지막 표시 취소",()=>GetComponent<MissionDemo>().UndoResponseMarker(),"button outline");
            responseClear=Button(tools,"모두 지우기",()=>GetComponent<MissionDemo>().ClearResponseMarkers(),"button outline");
            responseMarkCount=Label(tools,"","small accent");
            responseMarkCount.name="responseMarkCount";
            responseStatus=Label(tools,"","small accent");
            responseSubmit=Button(tools,"표시 완료 · 게임 계속  →",()=>GetComponent<MissionDemo>().CompleteThreatResponse(),"button primary");responseSubmit.name="responseSubmit";
            Button(tools,"로비로 돌아가기",()=>GetComponent<MissionDemo>().Exit(),"text-button muted");
            responseOverlay.style.display=DisplayStyle.None;
        }
        void RefreshMaps(MissionDemo mission,bool game)
        {
            bool answering=game&&mission.IsThreatResponse;responseOverlay.style.display=answering?DisplayStyle.Flex:DisplayStyle.None;
            mapPanel.style.display=game&&mission.MapVisible&&!mission.IsComplete?DisplayStyle.Flex:DisplayStyle.None;
            if(game&&mission.MapVisible){miniMap.PlayerPosition=mission.transform.position;miniMap.PlayerYaw=mission.transform.eulerAngles.y;miniMap.Supplies=mission.SupplyPositions;miniMap.Refresh();}
            if(!answering)return;
            hud.style.display=DisplayStyle.None;responseMap.PlayerPosition=mission.ResponseObserver;responseMap.PlayerYaw=mission.ResponseYaw;responseMap.Markers=mission.ResponseMarkers;responseMap.Interactive=mission.ResponseReady;responseMap.Refresh();
            responseHelp.text=mission.ResponseReady?"당시 내 위치와 방향을 기준으로, 느껴진 위협의 위치를 지도에 찍어 주세요.":"진동이 끝나면 위치를 표시할 수 있습니다.";
            responseEventCount.text=$"위협 발생 {mission.ThreatEventsOccurred}회";
            responseSubmit.SetEnabled(mission.ResponseReady);responseUndo.SetEnabled(mission.ResponseReady);responseClear.SetEnabled(mission.ResponseReady);
            int markCount = responseMap.Markers.Length;
            responseMarkCount.text = $"표시 {markCount} / {mission.MaximumThreatCount}개";
            if (markCount >= mission.MaximumThreatCount)
                responseMarkCount.text += "\n한도에 도달했습니다. 기존 표시를 지우면 다시 찍을 수 있습니다.";
            responseStatus.text=string.IsNullOrEmpty(mission.ResponseError)?mission.ResponseNotice:mission.ResponseError;
        }
    }
}
