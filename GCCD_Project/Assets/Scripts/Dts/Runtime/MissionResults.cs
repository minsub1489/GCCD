using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed class MissionEventResult
    {
        public string Id,State,Group,OutputStatus,Utc,MappingVersion,HapticEncoding;public bool Eligible;public double IntensityLow,IntensityHigh,DirectionTolerance,PositionTolerance;
        public double? IntensityMiddle;
        public bool[] Suppressed;public int SuppressionTheta,MinimumThreatCount,MaximumThreatCount;
        public Vector3 Observer;public float Yaw;public Vector3[] Sources,Marks;public MissionLocationScore Score;
    }
    public sealed partial class MissionDemo
    {
        public const double ResultTolerance=3;
        public const double ResultDirectionTolerance=15;
        public double DirectionTolerance { get; internal set; } = ResultDirectionTolerance;
        public double PositionTolerance { get; internal set; } = ResultTolerance;
        readonly List<MissionEventResult> reviewEvents=new List<MissionEventResult>();
        internal static string MissionDataRoot => Path.Combine(Application.persistentDataPath, "ExperimentData", "MISSION_DEMO");
        internal static string LatestCsvFolder()
        {
            var root = MissionDataRoot;
            Directory.CreateDirectory(root);
            return Directory.EnumerateDirectories(root)
                .OrderByDescending(directory => directory, StringComparer.Ordinal)
                .Select(directory => Path.Combine(directory, "csv"))
                .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "events.csv"))) ?? root;
        }

        public string CsvFolder {get{return string.IsNullOrEmpty(folder)?"":Path.Combine(folder,"csv");}}
        public string ExportError {get;private set;}="";
        public bool ExportsReady {get;private set;}
        public MissionEventResult[] ReviewEvents {get{return complete?reviewEvents.ToArray():new MissionEventResult[0];}}
        public bool TryCompleteMission()
        {
            if(complete)return true;if(IsThreatResponse||collected<3||hits<5)return false;
            FinishMission(false);
            return true;
        }
        public void EndExperiment()
        {
            if (!complete && !IsThreatResponse) FinishMission(true);
        }
        public bool EndedEarly { get; private set; }
        void FinishMission(bool endedEarly)
        {
            EndedEarly = endedEarly;
            complete = true;
            SetPaused(true);
            ExportMissionData();
        }
        public bool ExportMissionData()
        {
            try{
                var loaded=new List<MissionEventResult>();
                foreach(string path in Directory.GetFiles(folder,"event-*.json").OrderBy(x=>x,StringComparer.Ordinal)){
                    var record=JsonUtility.FromJson<MissionThreatRecord>(File.ReadAllText(path));
                    if(record==null||record.source_positions==null||record.source_positions.Length==0||record.response_positions==null||record.receipt==null)throw new InvalidDataException("Incomplete event record");
                    string group=record.pulse_interrupted?"INTERRUPTED":record.receipt.simulated?"SIMULATED":record.receipt.submitted?"REQUESTED_UNCONFIRMED":"OUTPUT_FAILED";
                    var item=new MissionEventResult{MappingVersion=record.mapping_version??"unknown-legacy-map",HapticEncoding=record.haptic_encoding??"unknown-legacy-encoding",IntensityLow=record.intensity_low,IntensityHigh=record.intensity_high,IntensityMiddle=record.intensity_middle>0?(double?)record.intensity_middle:null,Utc=record.utc,Id=record.event_id,State=record.state,Group=group,OutputStatus=record.receipt.status,Observer=record.observer_position,Yaw=record.observer_yaw,Sources=record.source_positions,Suppressed=record.source_suppressed,SuppressionTheta=record.suppression_theta,MinimumThreatCount=record.minimum_threat_count,MaximumThreatCount=record.maximum_threat_count,Marks=record.response_positions,Eligible=record.state=="completed"&&(group=="SIMULATED"||group=="REQUESTED_UNCONFIRMED")};
                    item.DirectionTolerance = record.direction_tolerance_deg > 0 ? record.direction_tolerance_deg : ResultDirectionTolerance;
                    item.PositionTolerance = record.position_tolerance_m > 0 ? record.position_tolerance_m : ResultTolerance;
                    item.Score=MissionLocationScorer.Score(Points(item.Sources),Points(item.Marks),item.Observer.x,item.Observer.z,item.Yaw,item.PositionTolerance,item.DirectionTolerance,item.Suppressed,
                        useProximity: record.scoring_rule==MissionLocationScore.ProximityRule || record.scoring_rule==MissionLocationScore.LegacyProximityRule,
                        countDuplicates: record.scoring_rule==MissionLocationScore.ProximityRule);loaded.Add(item);
                }
                // Filenames are random identifiers; replay order uses the original UTC event times.
                loaded=loaded.OrderBy(x=>x.Utc,StringComparer.Ordinal).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
                reviewEvents.Clear();reviewEvents.AddRange(loaded);Directory.CreateDirectory(CsvFolder);
                WriteEventCsv();WritePositionCsv();WriteSummaryCsv();
                SessionStore.AtomicWrite(Path.Combine(folder,"mission_summary.json"),JsonUtility.ToJson(new MissionSummaryRecord{session_id=Path.GetFileName(folder),state=complete?(EndedEarly?"ended_early":"completed"):"aborted",utc=DateTime.UtcNow.ToString("O"),active_seconds=Elapsed,tolerance_m=PositionTolerance,direction_tolerance_deg=DirectionTolerance,intensity_low=Strengths.Far/100.0,intensity_middle=Strengths.Middle/100.0,intensity_high=Strengths.Near/100.0,shots_fired=fired,drone_hits=hits,supplies=collected},true));
                SessionStore.AtomicWrite(Path.Combine(CsvFolder,"읽어주세요.txt"),"UTF-8 BOM CSV / Excel에서 열 수 있습니다.\nsummary.csv: 출력 종류별 종합 비교(각 개수를 합산한 뒤 채점)\n억제율 반영 점수 = (획득 점수 / 유지 위협의 만점) × (1 - 0.5 × 억제율).\nevents.csv: 이벤트별 정확도\npositions.csv: 실제/응답 좌표와 1대1 연결 오차\n새 시행은 방향 허용각 미만인 유지 위협과 일대일로 연결합니다. 정답당 1점과 위치 보너스 1/(1+수평 위치 오차/tolerance_m)점을 줍니다. 거리만으로 오답을 처리하지 않습니다. 새 채점에서는 중복처럼 보이는 표시도 다른 유지 위협과 비교하며, 연결되지 않은 오답당 1점을 차감합니다. 이전 채점은 중복을 제외할 수 있습니다. 이벤트별 최솟값은 0점입니다. 감점 후 점수(adjusted_score)는 감점 후 획득 점수를 전체 위협 만점으로 나눈 값입니다. deducted_correct_count와 adjusted_correct는 방향 정답 수 기준의 별도 지표입니다. 이전 시행은 기록된 채점 규칙을 사용합니다. false_marks는 중복을 포함한 원래 미일치 수이며 far_false_marks는 모든 실제 위협 방향에서 벗어난 표시 수입니다. 새 채점에서는 표시 정확도와 F1에 모든 응답을 포함합니다. 평균 오차는 연결된 쌍만 포함하며 무응답은 빈값입니다.\n중도 종료·출력 실패·출력 중단은 정확도 집계에서 제외됩니다. 화면 체험과 조끼 요청은 별도 행에 저장합니다.\nREQUESTED_UNCONFIRMED는 실제 착용 진동을 확인했다는 뜻이 아닙니다. MISSION_DEMO 체험 자료입니다.\n원본 event JSON에서 CSV를 다시 만들 수 있습니다.\n");
                ExportsReady=true;ExportError="";return true;
            }catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){ExportsReady=false;ExportError="결과 파일 저장 실패: "+e.Message;return false;}
        }
        [Serializable] sealed class MissionSummaryRecord
        {
            public string mode="MISSION_DEMO",haptic_encoding=MissionHapticEncoding.Version,session_id,state,utc;public bool not_research_data=true;public double tolerance_m,direction_tolerance_deg,active_seconds,intensity_low,intensity_middle,intensity_high;public int shots_fired,drone_hits,supplies;
        }
        static MissionSpawnPoint[] Points(Vector3[] positions){return positions.Select(p=>new MissionSpawnPoint{X=p.x,Z=p.z}).ToArray();}
        static string Line(params object[] values){return CsvTable.Line(values.Select(v=>v is string&&((string)v).Length>0&&"=+-@\t\r".IndexOf(((string)v)[0])>=0?(object)("'"+v):v).ToArray());}
        void SaveCsv(string name,StringBuilder text){SessionStore.AtomicWrite(Path.Combine(CsvFolder,name),"\ufeff"+text.ToString());}
        void WriteEventCsv()
        {
            var csv=new StringBuilder();csv.AppendLine("event_index,event_id,state,output_group,output_status,eligible,tolerance_m,source_count,response_count,correct,missed,false_marks,recall,precision,f1,mean_position_error_m,mean_direction_error_deg,mean_range_error_m,observer_x,observer_z,observer_yaw,mode,not_research_data,mapping_version,haptic_encoding,intensity_low,intensity_high,suppression_theta,suppressed_count,retained_count,minimum_threat_count,maximum_threat_count,direction_tolerance_deg,far_false_marks,adjusted_correct,adjusted_recall,retained_recognition,suppression_rate,suppression_lambda,suppression_score,wrong_marks,deducted_correct_count,intensity_middle,scoring_rule,proximity_bonus,earned_points,deducted_points,adjusted_score");
            for(int i=0;i<reviewEvents.Count;i++){var e=reviewEvents[i];var s=e.Score;csv.AppendLine(Line(i+1,e.Id,e.State,e.Group,e.OutputStatus,e.Eligible,e.PositionTolerance,s.SourceCount,s.ResponseCount,e.Eligible?(object)s.Correct:null,e.Eligible?(object)s.Missed:null,e.Eligible?(object)s.FalseMarks:null,e.Eligible?s.Recall:null,e.Eligible?s.Precision:null,e.Eligible?s.F1:null,e.Eligible?s.MeanPositionError:null,e.Eligible?s.MeanDirectionError:null,e.Eligible?s.MeanRangeError:null,e.Observer.x,e.Observer.z,e.Yaw,"MISSION_DEMO",true,e.MappingVersion,e.HapticEncoding,e.IntensityLow,e.IntensityHigh,e.Suppressed==null?null:(object)e.SuppressionTheta,e.Suppressed?.Count(x=>x),e.Suppressed?.Count(x=>!x),e.MinimumThreatCount>0?(object)e.MinimumThreatCount:null,e.MaximumThreatCount>0?(object)e.MaximumThreatCount:null,e.DirectionTolerance,e.Eligible?(object)s.FarFalseMarks:null,e.Eligible?(object)s.AdjustedCorrect:null,e.Eligible?s.AdjustedRecall:null,e.Eligible?s.RetainedRecognition:null,s.SuppressionRate,MissionLocationScore.SuppressionWeight,e.Eligible?s.SuppressionScore:null,e.Eligible?(object)s.WrongMarks:null,e.Eligible?(object)s.AppliedDeduction:null,e.IntensityMiddle,s.ScoringRule,e.Eligible?(object)s.ProximityBonus:null,e.Eligible?(object)s.EarnedPoints:null,e.Eligible?(object)s.AppliedPointDeduction:null,e.Eligible?s.AdjustedScore:null));}
            SaveCsv("events.csv",csv);
        }
        void WritePositionCsv()
        {
            var csv=new StringBuilder();csv.AppendLine("event_index,event_id,output_group,eligible,row_type,source_index,response_index,source_x,source_z,response_x,response_z,position_error_m,direction_error_deg,range_error_m,within_tolerance,nearest_direction_error_deg,far_false_mark,duplicate_mark,wrong_mark");
            for(int index=0;index<reviewEvents.Count;index++){
                var e=reviewEvents[index];var s=e.Score;var used=new HashSet<int>();
                for(int i=0;i<e.Marks.Length;i++){int j=s.ResponseToSource[i];if(j>=0)used.Add(j);Vector3 a=j>=0?e.Sources[j]:Vector3.zero;var b=e.Marks[i];csv.AppendLine(Line(index+1,e.Id,e.Group,e.Eligible,j<0?"extra_response":s.CorrectMarks[i]?"within_tolerance":"outside_tolerance",j<0?null:(object)(j+1),i+1,j<0?null:(object)a.x,j<0?null:(object)a.z,b.x,b.z,s.PositionErrors[i],s.DirectionErrors[i],s.RangeErrors[i],s.CorrectMarks[i],s.NearestDirectionErrors[i],s.FarFalseMark[i],s.DuplicateMarks[i],e.Eligible?(object)s.IsWrongMark(i):null));}
                for(int j=0;j<e.Sources.Length;j++)if(!used.Contains(j))csv.AppendLine(Line(index+1,e.Id,e.Group,e.Eligible,"unmatched_source",j+1,null,e.Sources[j].x,e.Sources[j].z,null,null,null,null,null,false,null,null,null,null));
            }
            SaveCsv("positions.csv",csv);
        }
        void WriteSummaryCsv()
        {
            var csv=new StringBuilder();csv.AppendLine("session_id,mission_state,output_group,event_count,eligible_events,excluded_events,source_count,response_count,correct,missed,false_marks,recall,precision,f1,mean_position_error_m,mean_direction_error_deg,mean_range_error_m,tolerance_m,shots_fired,drone_hits,supplies,active_seconds,mode,not_research_data,direction_tolerance_deg,far_false_marks,adjusted_correct,adjusted_recall,retained_recognition,suppression_rate,suppression_lambda,suppression_score,wrong_marks,deducted_correct_count,proximity_bonus,earned_points,deducted_points,adjusted_score");
            foreach(string group in new[]{"ALL","SIMULATED","REQUESTED_UNCONFIRMED","OUTPUT_FAILED","INTERRUPTED"}){
                var events=reviewEvents.Where(e=>group=="ALL"||e.Group==group).ToArray();var eligible=events.Where(e=>e.Eligible).ToArray();int sources=eligible.Sum(e=>e.Score.SourceCount),marks=eligible.Sum(e=>e.Score.ResponseCount),correct=eligible.Sum(e=>e.Score.Correct);
                int scoredMarks=eligible.Sum(e=>e.Score.ScoredResponseCount), wrong=eligible.Sum(e=>e.Score.WrongMarks);
                int retained=eligible.Sum(e=>e.Score.RetainedCount);
                int adjusted=eligible.Sum(e=>e.Score.AdjustedCorrect), far=eligible.Sum(e=>e.Score.FarFalseMarks);
                csv.AppendLine(Line(Path.GetFileName(folder),complete?(EndedEarly?"ended_early":"completed"):"aborted",group,events.Length,eligible.Length,events.Length-eligible.Length,sources,marks,correct,sources-correct,marks-correct,sources==0?null:(object)(correct/(double)sources),scoredMarks==0?null:(object)(correct/(double)scoredMarks),sources+scoredMarks==0?null:(object)(2d*correct/(sources+scoredMarks)),Mean(eligible.SelectMany(e=>e.Score.PositionErrors)),Mean(eligible.SelectMany(e=>e.Score.DirectionErrors)),Mean(eligible.SelectMany(e=>e.Score.RangeErrors)),PositionTolerance,fired,hits,collected,Elapsed,"MISSION_DEMO",true,DirectionTolerance,far,adjusted,sources==0?null:(object)(adjusted/(double)sources),retained==0?null:(object)(correct/(double)retained),sources==0?null:(object)((sources-retained)/(double)sources),MissionLocationScore.SuppressionWeight,MissionLocationScore.WeightedScore(eligible.Sum(e=>e.Score.ScoredHits),retained,sources),wrong,correct-adjusted,
                    eligible.Sum(e=>e.Score.ProximityBonus),eligible.Sum(e=>e.Score.EarnedPoints),eligible.Sum(e=>e.Score.AppliedPointDeduction),
                    sources==0?null:(object)(eligible.Sum(e=>e.Score.AdjustedScoredHits)/sources)));
            }
            SaveCsv("summary.csv",csv);
        }
        static double? Mean(IEnumerable<double?> numbers){var values=numbers.Where(n=>n.HasValue).Select(n=>n.Value).ToArray();return values.Length==0?(double?)null:values.Average();}
        public void OpenCsvFolder(){if(!Directory.Exists(CsvFolder)&&!ExportMissionData())return;Application.OpenURL(new Uri(CsvFolder+Path.DirectorySeparatorChar).AbsoluteUri);}
    }
}
