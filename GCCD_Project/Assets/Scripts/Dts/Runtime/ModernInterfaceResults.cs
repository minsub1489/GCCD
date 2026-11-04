using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    public sealed partial class ModernInterface
    {
        VisualElement resultsOverlay;MissionMapView resultMap;Label resultNote,resultTitle,resultTotals,resultDetails,resultComparison,resultPath,resultSaveStatus;
        Toggle resultSectorToggle,resultPenaltyToggle,resultDuplicateToggle;
        ScrollView resultEventList;MissionDemo resultOwner;int selectedEvent=-1;bool resultsBuilt;
        void BuildResults()
        {
            resultsOverlay=Element(root,"results-overlay");resultsOverlay.name="missionResults";
            var shell=Element(resultsOverlay,"results-shell");Label(shell,"OPERATION COMPLETE  /  RECALL REVIEW","eyebrow accent");resultTitle=Label(shell,"작전 완료 · 위치 정확도 비교","results-title");
            resultTotals=Label(shell,"","results-totals");
            var body=Element(shell,"results-body");
            var left=Element(body,"results-map-column");Label(left,"초록 고리  유지 위협    ·    주황 ×  억제 위협    ·    노란 점  내가 표시한 위치    ·    선  정답 연결","small muted");resultSectorToggle = new Toggle("억제각 보기") { name = "resultSuppressionToggle" };
            resultSectorToggle.AddToClassList("small");
            resultSectorToggle.RegisterValueChangedCallback(evt =>
            {
                resultMap.ShowSuppressionSectors = evt.newValue;
                resultMap.Refresh();
            });
            var overlays = Element(left, "row");
            overlays.Add(resultSectorToggle);
            resultPenaltyToggle = new Toggle("오답 보기 (빨간 고리)") { name = "resultPenaltyToggle" };
            resultDuplicateToggle = new Toggle("중복 보기 (하늘색 고리)") { name = "resultDuplicateToggle" };
            foreach (var toggle in new[] { resultPenaltyToggle, resultDuplicateToggle })
            {
                toggle.AddToClassList("small");
                toggle.style.marginLeft = 18;
                overlays.Add(toggle);
            }
            resultPenaltyToggle.RegisterValueChangedCallback(evt => { resultMap.ShowPenaltyMarks = evt.newValue; resultMap.Refresh(); });
            resultDuplicateToggle.RegisterValueChangedCallback(evt => { resultMap.ShowDuplicateMarks = evt.newValue; resultMap.Refresh(); });
            resultMap=new MissionMapView{Detailed=true,ShowDistanceScale=true};resultMap.name="resultMap";left.Add(resultMap);
            var side=Element(body,"results-side");resultComparison=Label(side,"","small muted");resultEventList=new ScrollView();resultEventList.AddToClassList("results-event-list");side.Add(resultEventList);resultDetails=Label(side,"","results-details");
            resultNote=Label(shell,"","results-note");
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
            if(resultOwner!=mission){resultOwner=mission;resultsBuilt=false;selectedEvent=-1;resultSectorToggle.value=false;resultPenaltyToggle.value=false;resultDuplicateToggle.value=false;}
            resultTitle.text=mission.EndedEarly?"실험 종료 · 위치 정확도 비교":"작전 완료 · 위치 정확도 비교";
            var events=mission.ReviewEvents;
            if(selectedEvent<0) SetResultCriteria(mission.PositionTolerance, mission.DirectionTolerance);
            if(!resultsBuilt){
                resultEventList.Clear();var eligible=events.Where(e=>e.Eligible).ToArray();int correct=eligible.Sum(e=>e.Score.Correct),sources=eligible.Sum(e=>e.Score.SourceCount),marks=eligible.Sum(e=>e.Score.ScoredResponseCount);
                double locationPoints=eligible.Sum(e=>e.Score.ScoredHits);
                int retained=eligible.Sum(e=>e.Score.RetainedCount);
                double? weighted=MissionLocationScore.WeightedScore(eligible.Sum(e=>e.Score.ScoredHits),retained,sources);
                resultTotals.text="억제율 반영 점수  "+Percent(weighted)
                    +"    유지 위협 인지율  "+Percent(retained==0?(double?)null:correct/(double)retained)
                    +"    억제율  "+Percent(sources==0?(double?)null:(sources-retained)/(double)sources)
                    +"    위치 점수  "+Percent(sources==0?(double?)null:locationPoints/sources)
                    +"    표시 정확도  "+Percent(marks==0?(double?)null:correct/(double)marks);
                string wrongTotal = eligible.Length == 0 ? "—" : $"{eligible.Sum(e => e.Score.WrongMarks)}개";
                resultComparison.text=GroupText(events,"SIMULATED","화면 체험")+"\n"+GroupText(events,"REQUESTED_UNCONFIRMED","조끼 요청 · 착용 미확인")
                    +$"\n오답 합계 {wrongTotal}"
                    +$"\n집계 제외 {events.Count(e=>!e.Eligible)}회";
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
                    string accuracy = e.Eligible ? $"{e.Score.Correct}/{e.Score.RetainedCount} 유지 위협 일치" : "집계 제외";
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
                else {resultMap.ActualSources=new Vector3[0];resultMap.SuppressedSources=null;SetResponseOverlays(null);resultMap.SuppressionTheta=0;resultSectorToggle.SetEnabled(false);resultMap.Markers=new MissionSpawnPoint[0];resultMap.MatchedSourceIndices=new int[0];resultMap.PlayerPosition=mission.transform.position;resultDetails.text="완료된 위협 응답이 없습니다.\n정확도와 평균 오차는 —로 표시합니다.";}
                resultPath.text=mission.CsvFolder;resultsBuilt=true;
            }
            resultMap.Refresh();resultSaveStatus.text=mission.ExportsReady?"CSV 자동 저장 완료 · summary.csv / events.csv / positions.csv":mission.ExportError;
        }
        void SetResultCriteria(double position, double direction, bool useProximity = true, bool countDuplicates = true, bool deductWrong = false)
        {
            resultNote.text = useProximity
                ? $"정답 · 유지 위협과 방향 오차 {direction:G}° 미만이면 1점 · 위치 오차가 {position:G}m일 때 보너스 0.5점"
                    + "\n위치 보너스 = 1 ÷ (1 + 수평 위치 오차 ÷ 기준 거리)"
                    + (countDuplicates ? "\n가까운 표시도 다른 유지 위협과 비교하며, 연결되지 않으면 오답입니다." : "\n이전 채점 · 중복 표시는 채점에서 제외합니다.")
                    + (deductWrong ? "\n이전 채점 · 오답당 1점 차감" : "\n정답에 연결되지 않은 표시는 0점입니다.")
                    + "\n억제율 반영 점수 = (획득 점수 ÷ 유지 위협 만점) × (1 − 0.5 × 억제율)"
                : $"이전 채점 · 수평거리 {position:G}m 이내와 방향 오차 {direction:G}° 미만이면 정답";
        }
        void SelectResult(MissionEventResult e)
        {
            SetResultCriteria(e.PositionTolerance, e.DirectionTolerance, e.Score.UsesProximity, e.Score.CountsDuplicates, e.Score.DeductWrong);
            resultMap.PlayerPosition=e.Observer;resultMap.PlayerYaw=e.Yaw;resultMap.ActualSources=e.Sources;resultMap.SuppressedSources=e.Suppressed;
            resultMap.SuppressionTheta=e.SuppressionTheta;
            resultSectorToggle.SetEnabled(e.Suppressed != null && e.SuppressionTheta > 0);
            resultSectorToggle.label = e.SuppressionTheta > 0 && e.Suppressed != null
                ? $"억제각 보기 · {e.SuppressionTheta}° (좌우 ±{e.SuppressionTheta / 2.0:0.#}°)"
                : "억제각 보기 · 적용 범위 없음";
            SetResponseOverlays(e.Eligible ? e.Score : null);
            resultMap.Markers=e.Marks.Select(p=>new MissionSpawnPoint{X=p.x,Z=p.z}).ToArray();resultMap.MatchedSourceIndices=e.Score.ResponseToSource.Select((source,index)=>e.Score.CorrectMarks[index]?source:-1).ToArray();
            var score = e.Score;
            string suppression = e.Suppressed == null
                ? "억제 기록 없음"
                : $"억제각 {e.SuppressionTheta}° · 전체 {e.Sources.Length}개 = 유지 {e.Suppressed.Count(value => !value)}개 + 억제 {e.Suppressed.Count(value => value)}개"
                    + (e.Suppressed.Any(value => value) ? "" : " · 이 이벤트에는 주황 ×가 없습니다.");
            string accuracy = e.Eligible
                ? $"억제율 반영 점수 {Percent(score.SuppressionScore)} · 유지 인지율 {Percent(score.RetainedRecognition)} · 억제율 {Percent(score.SuppressionRate)}"
                    + $"\n유지 위협 누락 {score.RetainedCount - score.Correct}개 · 오답 {score.WrongMarks}개 · 중복 {score.DuplicateCount}개"
                    + $"\n획득 {score.EarnedPoints:0.##}점 · 위치 보너스 +{score.ProximityBonus:0.##}점"
                    + $"\n위치 점수 {Percent(score.SourceCount==0?(double?)null:score.ScoredHits/score.SourceCount)}"
                : $"집계 제외 · {e.State}";
            resultDetails.text = $"{GroupName(e.Group)}\n{suppression}\n{accuracy}"
                + $"\n평균 위치 오차 {Number(score.MeanPositionError, "m")}"
                + $"\n평균 방향 오차 {Number(score.MeanDirectionError, "°")}"
                + $"\n평균 거리 오차 {Number(score.MeanRangeError, "m")}";
        }
        void SetResponseOverlays(MissionLocationScore score)
        {
            resultMap.ResponseScore = score;
            resultPenaltyToggle.SetEnabled(score != null && score.ResponseCount > 0);
            resultDuplicateToggle.SetEnabled(score != null && score.ResponseCount > 0);
        }
        static string GroupName(string group){return group=="SIMULATED"?"화면 체험":group=="REQUESTED_UNCONFIRMED"?"조끼 요청 · 착용 미확인":group=="INTERRUPTED"?"출력 중단":"출력 실패";}
        static string GroupText(MissionEventResult[] events,string group,string label)
        {
            var valid=events.Where(e=>e.Eligible&&e.Group==group).ToArray();
            var score=MissionLocationScore.WeightedScore(valid.Sum(e=>e.Score.ScoredHits),
                valid.Sum(e=>e.Score.RetainedCount),valid.Sum(e=>e.Score.SourceCount));
            return label+"  "+Percent(score)+"  ("+valid.Length+"회)";
        }
        static string Percent(double? value){return value.HasValue?(value.Value*100).ToString("F1")+"%":"—";}
        static string Number(double? value,string unit){return value.HasValue?value.Value.ToString("F2")+unit:"—";}
    }
}
