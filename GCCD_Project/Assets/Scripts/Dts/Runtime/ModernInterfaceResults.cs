using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed partial class ModernInterface
    {
        VisualElement resultsOverlay;MissionMapView resultMap;Label resultTitle,resultTotals,resultDetails,resultComparison,resultPath,resultSaveStatus;
        Toggle resultSectorToggle;
        ScrollView resultEventList;MissionDemo resultOwner;int selectedEvent=-1;bool resultsBuilt;
        void BuildResults()
        {
            resultsOverlay=Element(root,"results-overlay");resultsOverlay.name="missionResults";
            var shell=Element(resultsOverlay,"results-shell");Label(shell,"OPERATION COMPLETE  /  RECALL REVIEW","eyebrow accent");resultTitle=Label(shell,"작전 완료 · 위치 정확도 비교","results-title");
            resultTotals=Label(shell,"","results-totals");
            var body=Element(shell,"results-body");
            var left=Element(body,"results-map-column");Label(left,"초록 고리  유지 위협    ·    주황 ×  억제 위협    ·    노란 점  내가 표시한 위치    ·    선  비교 연결","small muted");resultSectorToggle = new Toggle("억제각 보기") { name = "resultSuppressionToggle" };
            resultSectorToggle.AddToClassList("small");
            resultSectorToggle.RegisterValueChangedCallback(evt =>
            {
                resultMap.ShowSuppressionSectors = evt.newValue;
                resultMap.Refresh();
            });
            left.Add(resultSectorToggle);
            resultMap=new MissionMapView{Detailed=true};resultMap.name="resultMap";left.Add(resultMap);
            var side=Element(body,"results-side");resultComparison=Label(side,"","small muted");resultEventList=new ScrollView();resultEventList.AddToClassList("results-event-list");side.Add(resultEventList);resultDetails=Label(side,"","results-details");
            Label(shell,"일치 기준: 수평거리 3m 이내 + 방향 오차 15° 미만 · 1대1 비교 · 평균 오차는 연결된 쌍 기준","results-note");
            var save=Element(shell,"results-save-row");resultPath=Label(save,"","results-path");resultPath.name="csvPath";resultPath.selection.isSelectable=true;
            Button(save,"저장 폴더 열기",()=>GetComponent<MissionDemo>().OpenCsvFolder(),"button outline").name="openCsvFolder";
            Button(save,"CSV 다시 저장",()=>{var m=GetComponent<MissionDemo>();m.ExportMissionData();resultsBuilt=false;},"button outline");
            Button(save,"로비로 돌아가기",()=>GetComponent<MissionDemo>().Exit(),"button primary");resultSaveStatus=Label(shell,"","small accent");resultsOverlay.style.display=DisplayStyle.None;
        }
        void RefreshResults(MissionDemo mission,bool game)
        {
            bool show=game&&mission.IsComplete;resultsOverlay.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
            if(!show){resultsBuilt=false;resultOwner=null;return;}
            hud.style.display=DisplayStyle.None;responseOverlay.style.display=DisplayStyle.None;
            if(resultOwner!=mission){resultOwner=mission;resultsBuilt=false;selectedEvent=-1;resultSectorToggle.value=false;}
            resultTitle.text=mission.EndedEarly?"실험 종료 · 위치 정확도 비교":"작전 완료 · 위치 정확도 비교";
            var events=mission.ReviewEvents;
            if(!resultsBuilt){
                resultEventList.Clear();var eligible=events.Where(e=>e.Eligible).ToArray();int correct=eligible.Sum(e=>e.Score.Correct),sources=eligible.Sum(e=>e.Score.SourceCount),marks=eligible.Sum(e=>e.Score.ResponseCount);
                int adjusted=eligible.Sum(e=>e.Score.AdjustedCorrect);
                resultTotals.text="위협 찾기  "+Percent(sources==0?(double?)null:correct/(double)sources)+"     감점 반영  "+Percent(sources==0?(double?)null:adjusted/(double)sources)+"     표시 정확도  "+Percent(marks==0?(double?)null:correct/(double)marks)+"     일치  "+correct+" / "+sources+"     누락  "+(sources-correct)+"     추가 표시  "+(marks-correct);
                resultComparison.text=GroupText(events,"SIMULATED","화면 체험")+"\n"+GroupText(events,"REQUESTED_UNCONFIRMED","조끼 요청 · 착용 미확인")+"\n집계 제외  "+events.Count(e=>!e.Eligible)+"회";
                int withSuppression = Array.FindIndex(events, e => e.Suppressed != null && e.Suppressed.Any(value => value));
                resultComparison.text += withSuppression >= 0
                    ? "\n억제된 위협이 있는 이벤트는 목록에 개수가 표시됩니다."
                    : events.Any(e => e.Suppressed != null)
                        ? "\n이번 실행에서 실제로 억제된 위협이 없습니다."
                        : "\n이 실행에는 위협별 억제 기록이 없습니다.";
                for (int i = 0; i < events.Length; i++)
                {
                    int index = i;
                    var e = events[i];
                    int? suppressed = e.Suppressed?.Count(value => value);
                    string status = suppressed.HasValue ? $"억제 {suppressed.Value}개" : "억제 기록 없음";
                    string accuracy = e.Eligible ? $"{e.Score.Correct}/{e.Score.SourceCount} 일치" : "집계 제외";
                    var button = Button(resultEventList, $"이벤트 {i + 1:D2} · {status} · {accuracy}", () =>
                    {
                        selectedEvent = index;
                        SelectResult(events[index]);
                    }, "button result-event");
                    if (suppressed > 0) button.style.color = new Color(1f, .68f, .38f);
                }
                if(events.Length>0)
                {
                    if (selectedEvent < 0) selectedEvent = Mathf.Max(0, withSuppression);
                    selectedEvent = Mathf.Clamp(selectedEvent, 0, events.Length - 1);
                    SelectResult(events[selectedEvent]);
                }
                else {resultMap.ActualSources=new Vector3[0];resultMap.SuppressedSources=null;resultMap.SuppressionTheta=0;resultSectorToggle.SetEnabled(false);resultMap.Markers=new MissionSpawnPoint[0];resultMap.MatchedSourceIndices=new int[0];resultMap.PlayerPosition=mission.transform.position;resultDetails.text="완료된 위협 응답이 없습니다.\n정확도와 평균 오차는 —로 표시합니다.";}
                resultPath.text=mission.CsvFolder;resultsBuilt=true;
            }
            resultMap.Refresh();resultSaveStatus.text=mission.ExportsReady?"CSV 자동 저장 완료 · summary.csv / events.csv / positions.csv":mission.ExportError;
        }
        void SelectResult(MissionEventResult e)
        {
            resultMap.PlayerPosition=e.Observer;resultMap.PlayerYaw=e.Yaw;resultMap.ActualSources=e.Sources;resultMap.SuppressedSources=e.Suppressed;
            resultMap.SuppressionTheta=e.SuppressionTheta;
            resultSectorToggle.SetEnabled(e.Suppressed != null && e.SuppressionTheta > 0);
            resultSectorToggle.label = e.SuppressionTheta > 0 && e.Suppressed != null
                ? $"억제각 보기 · {e.SuppressionTheta}° (좌우 ±{e.SuppressionTheta / 2.0:0.#}°)"
                : "억제각 보기 · 적용 범위 없음";
            resultMap.Markers=e.Marks.Select(p=>new MissionSpawnPoint{X=p.x,Z=p.z}).ToArray();resultMap.MatchedSourceIndices=e.Score.ResponseToSource;
            resultDetails.text=GroupName(e.Group)+"\n"+(e.Suppressed==null?"억제 기록 없음":$"억제각 {e.SuppressionTheta}° · 전체 {e.Sources.Length}개 = 유지 {e.Suppressed.Count(value=>!value)}개 + 억제 {e.Suppressed.Count(value=>value)}개" + (e.Suppressed.Any(value=>value) ? "" : " · 이 이벤트에는 주황 ×가 없습니다."))+"\n"+(e.Eligible?"위협 찾기 "+Percent(e.Score.Recall)+" · 표시 정확도 "+Percent(e.Score.Precision):"집계 제외 · "+e.State)+"\n누락 "+e.Score.Missed+" · 추가 표시 "+e.Score.FalseMarks+(e.Eligible?"\n방향 감점 "+e.Score.FarFalseMarks+"개 · 감점 반영 "+e.Score.AdjustedCorrect+"/"+e.Score.SourceCount+" ("+Percent(e.Score.AdjustedRecall)+")":"")+"\n평균 위치 오차 "+Number(e.Score.MeanPositionError,"m")+"\n평균 방향 오차 "+Number(e.Score.MeanDirectionError,"°")+"\n평균 거리 오차 "+Number(e.Score.MeanRangeError,"m");
        }
        static string GroupName(string group){return group=="SIMULATED"?"화면 체험":group=="REQUESTED_UNCONFIRMED"?"조끼 요청 · 착용 미확인":group=="INTERRUPTED"?"출력 중단":"출력 실패";}
        static string GroupText(MissionEventResult[] events,string group,string label){var valid=events.Where(e=>e.Eligible&&e.Group==group).ToArray();int sources=valid.Sum(e=>e.Score.SourceCount),correct=valid.Sum(e=>e.Score.Correct);return label+"  "+Percent(sources==0?(double?)null:correct/(double)sources)+"  ("+valid.Length+"회)";}
        static string Percent(double? value){return value.HasValue?(value.Value*100).ToString("F1")+"%":"—";}
        static string Number(double? value,string unit){return value.HasValue?value.Value.ToString("F2")+unit:"—";}
    }
}
