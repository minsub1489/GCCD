using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class ExperimentPlatform
    {
        readonly string[] modes={"Device Check","Calibration","Stimulus Validation","Training","Experiment A","Experiment B","Data Review"};
        int menu=4,selectedMarker=-1,motorIndex,calibrationBin=1;Vector2 setupScroll;string resumeFolder="",notice="";
        double calibrationAngle=0,calibrationDistance=12;bool physicalProbe;string comparisonSummary="";
        GUIStyle titleStyle,bodyStyle;Texture2D ringTexture;Font appFont;
        void InitStyles()
        {
            if(titleStyle!=null)return;
            var installed=Font.GetOSInstalledFontNames();
            string family=new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","AppleGothic","Arial Unicode MS","Arial"}.FirstOrDefault(name=>installed.Contains(name));
            appFont=family==null?Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"):Font.CreateDynamicFontFromOSFont(family,18);
            Debug.Log("DTS_UI_FONT "+(family??"LegacyRuntime"));
            GUI.skin.font=appFont;GUI.skin.label.fontSize=16;GUI.skin.button.fontSize=16;GUI.skin.textField.fontSize=16;GUI.skin.toggle.fontSize=16;
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold,wordWrap=true};
            bodyStyle=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true};
            ringTexture=new Texture2D(360,360,TextureFormat.RGBA32,false);var pixels=new Color[360*360];
            for(int y=0;y<360;y++)for(int x=0;x<360;x++){double d=Math.Sqrt((x-179.5)*(x-179.5)+(y-179.5)*(y-179.5));bool edge=Math.Abs(d-169)<1.5||Math.Abs(d-112)<.7||Math.Abs(d-56)<.7;bool axis=(Math.Abs(x-179.5)<.7||Math.Abs(y-179.5)<.7)&&d<170;pixels[y*360+x]=edge?new Color(.55f,.7f,.75f):axis?new Color(.3f,.4f,.45f):new Color(.08f,.11f,.14f,.95f);}
            ringTexture.SetPixels(pixels);ringTexture.Apply();
        }
        void OnGUI()
        {
            InitStyles();
            if(Phase==TrialPhase.Setup){DrawSetup();return;}
            if(operatorVisible){DrawOperator();return;}
            float w=Math.Min(880,Screen.width-40);GUILayout.BeginArea(new Rect((Screen.width-w)/2,30,w,Screen.height-60),GUI.skin.box);
            if(Mode=="DEVELOPMENT")GUILayout.Label("개발용 시뮬레이션 · 실제 진동 없음 · 연구 측정 자료 아님",bodyStyle);
            if(training)GUILayout.Label("훈련 · 정답 피드백 제공",bodyStyle);
            if(Phase==TrialPhase.Prepare||Phase==TrialPhase.Pulse)
            {GUILayout.FlexibleSpace();GUILayout.Label("+",new GUIStyle(titleStyle){alignment=TextAnchor.MiddleCenter,fontSize=48});GUILayout.Label("정면을 바라보고 자극을 기다려 주세요.",new GUIStyle(bodyStyle){alignment=TextAnchor.MiddleCenter});GUILayout.FlexibleSpace();}
            else if(Phase==TrialPhase.Response&&metadata.experiment_type=="A")DrawResponse();
            else if(Phase==TrialPhase.Response&&metadata.experiment_type=="B"){GUILayout.Label("정면 표적을 사격하며 외부 위협에 대응하세요.",bodyStyle);GUILayout.FlexibleSpace();GUILayout.Label("+",new GUIStyle(titleStyle){alignment=TextAnchor.MiddleCenter});GUILayout.FlexibleSpace();}
            else if(Phase==TrialPhase.Questionnaire)
            {GUILayout.Label("방금 자극을 구별하는 데 얼마나 혼란스러웠나요?",titleStyle);GUILayout.Label("1 전혀 혼란스럽지 않음 — 7 매우 혼란스러움",bodyStyle);GUILayout.BeginHorizontal();for(int i=1;i<=7;i++){int rating=i;if(GUILayout.Button(i.ToString(),GUILayout.Height(60)))SaveRating(rating);}GUILayout.EndHorizontal();}
            else if(Phase==TrialPhase.Rest||Phase==TrialPhase.Paused)
            {GUILayout.Label(Phase==TrialPhase.Rest?"잠시 쉬어 주세요":"일시정지",titleStyle);GUILayout.Label(training?feedback:"준비되면 계속 버튼을 눌러 주세요.",bodyStyle);if(Phase==TrialPhase.Paused)GUILayout.Label("진행 중이던 자극은 기술 실패로 기록되며 정상 시행으로 채점하지 않습니다.",bodyStyle);if(GUILayout.Button("계속",GUILayout.Height(48)))Advance();}
            else if(Phase==TrialPhase.Finished)
            {GUILayout.Label(training?"훈련 종료":"참여가 끝났습니다",titleStyle);if(training)GUILayout.Label(feedback+"\n설정된 훈련 기준 통과: "+trainingPassed,bodyStyle);if(GUILayout.Button("연구자 메뉴",GUILayout.Height(48)))BackToSetup();}
            else if(Phase==TrialPhase.Fault)
            {GUILayout.Label("진행 중단 · 연구자 확인 필요",titleStyle);GUILayout.Label(Error,bodyStyle);GUILayout.Label("확인된 원본 기록은 저장 폴더에 보존됩니다.",bodyStyle);if(GUILayout.Button("연구자 메뉴"))BackToSetup();}
            GUILayout.EndArea();
        }
        void Guard(Action action){try{action();}catch(Exception e){notice=e.GetBaseException().Message;}}
        void DrawSetup()
        {
            GUILayout.BeginArea(new Rect(20,20,Screen.width-40,Screen.height-40),GUI.skin.box);
            GUILayout.Label("GCCD · 거리 우선 방향 억제각 실험",titleStyle);
            menu=GUILayout.SelectionGrid(menu,modes,4,GUILayout.Height(76));
            setupScroll=GUILayout.BeginScrollView(setupScroll);
            GUILayout.BeginHorizontal();GUILayout.Label("익명 참가자 ID",GUILayout.Width(150));Participant=GUILayout.TextField(Participant,32,GUILayout.Width(200));GUILayout.Label("자료 구분",GUILayout.Width(100));int m=Array.IndexOf(new[]{"DEVELOPMENT","PILOT","MAIN"},Mode);m=GUILayout.SelectionGrid(Math.Max(0,m),new[]{"개발","파일럿","본 실험"},3);Mode=new[]{"DEVELOPMENT","PILOT","MAIN"}[m];GUILayout.EndHorizontal();
            if(Mode=="DEVELOPMENT")GUILayout.Label("가상 모터 맵과 임시 허용오차를 사용하는 소프트웨어 확인 모드입니다.",bodyStyle);
            if(menu==0)DrawDevice();
            else if(menu==1)DrawCalibration();
            else if(menu==2)DrawValidation();
            else if(menu==6)DrawDataReview();
            else
            {
                Experiment=menu==5?"B":"A";
                GUILayout.Label(menu==3?"단일 방향 → 단일 거리 → 복합 자극 → 세 위협/원형 응답을 연습합니다.":menu==4?"실험 A · 6블록 × 18시행 = 108시행":"실험 B · 후보 억제각과 0°의 독립 확인",bodyStyle);
                Number("난수 시드",ref config.Seed);Number("균형 순서 행 (0~5)",ref config.CounterbalanceRow);
                GUILayout.Label("연구자 전용 조건 순서: "+SafeOrder(),bodyStyle);
                Number("수평 FOV",ref config.HorizontalFov);Number("펄스 지속시간 ms (500은 초기값)",ref config.PulseMilliseconds);
                Number("방향 허용오차 ° (-1 미확정)",ref config.DirectionTolerance);
                Number("개인 최소 강도 L",ref config.Low);Number("개인 최대 강도 H",ref config.High);
                if(menu==5){Number("A에서 선정한 후보 억제각",ref config.CandidateTheta);Number("첫 대응 제한시간 초",ref config.FirstActionDeadline);Number("정면 과제 사격 구역 반각 ° (미확정 -1)",ref config.FrontTaskSectorDegrees);IndependentBParticipantConfirmed=GUILayout.Toggle(IndependentBParticipantConfirmed,"B 참가자는 A에 참여하지 않았음을 확인함");Number("조건당 이벤트 수",ref config.BEventsPerCondition);Text("후보 선정 규칙",ref config.CandidateSelectionRule);Text("후보 선정 근거",ref config.CandidateEvidence);}
                if(menu==3){Number("훈련 공동 정답률 기준",ref config.TrainingMinimumJointAccuracy);Number("세 위협 훈련 시행 수",ref config.TrainingTripleTrials);Number("최소 유효 세 위협 훈련 시행 수",ref config.TrainingMinimumTrials);Number("최대 재훈련 횟수",ref config.TrainingMaximumRetries);Text("훈련 판정 기준 설명",ref config.TrainingCriterion);Text("훈련 종료 규칙",ref config.TrainingExitRule);}
                if(Mode=="MAIN")DrawMainSettings();
                GUILayout.Space(12);if(GUILayout.Button(menu==3?"훈련 시작":"실험 시작",GUILayout.Height(48)))StartSession(menu==3);
            }
            GUILayout.Space(14);GUILayout.BeginHorizontal();if(GUILayout.Button("설정 저장"))Guard(SaveConfig);if(GUILayout.Button("설정 불러오기"))Guard(LoadConfig);if(GUILayout.Button("긴급 진동 중지"))notice=haptics.Stop();GUILayout.EndHorizontal();
            GUILayout.Label("설정 파일: "+ConfigPath,bodyStyle);Text("재개할 세션 폴더",ref resumeFolder);
            if(GUILayout.Button("세션 재개 · 미완료 시행은 기술 실패로 보존"))ResumeSession(resumeFolder);
            if(!string.IsNullOrEmpty(Folder))GUILayout.Label("최근 데이터: "+Folder,bodyStyle);
            if(!string.IsNullOrEmpty(Error))GUILayout.Label(Error,bodyStyle);if(!string.IsNullOrEmpty(notice))GUILayout.Label(notice,bodyStyle);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        string SafeOrder(){try{return string.Join(" → ",ConditionScheduler.Order(config.CounterbalanceRow));}catch(ArgumentException){return "순서 행을 0~5로 설정하세요.";}}
        void DrawDevice()
        {
            Text("확인한 X40 장비 이름 (Player에 표시된 이름)",ref config.VerifiedDeviceName);
            if(GUILayout.Button("공식 SDK 초기화 및 연결 확인"))Guard(()=>{haptics.Initialize();string reason;bool ready=haptics.Ready(config,out reason);notice=(ready?"연결 조건 충족: ":"미준비: ")+reason;});
            GUILayout.Label(haptics.DeviceInventory,bodyStyle);
            GUILayout.Label("명령 전송 성공, SDK 반환값, 실제 피부 진동은 서로 다른 검증 단계입니다.",bodyStyle);
            GUILayout.Label("공식 SDK 초기화는 GCCD → bHaptics SDK2 메뉴에서 확인합니다. 페어링 설정은 유지합니다.",bodyStyle);
        }
        void DrawCalibration()
        {
            DrawDevice();physicalProbe=GUILayout.Toggle(physicalProbe,"실제 장비로 테스트 (해제 시 가상 출력)");
            config.AllowRaw40CalibrationProbe=GUILayout.Toggle(config.AllowRaw40CalibrationProbe,"연구자: X40 40채널 API 미검증 상태의 개별 교정 시험 허용");
            Number("각도 °",ref calibrationAngle);Number("거리 m",ref calibrationDistance);Number("지속시간 ms",ref config.PulseMilliseconds);Number("개인 L",ref config.Low);Number("개인 H",ref config.High);
            if(GUILayout.Button("각도·거리 패턴 테스트"))Guard(()=>{var frame=MotorComposer.Compose(new[]{new Threat("CAL",true,true,true,0,0,0,calibrationDistance,calibrationAngle)},config.Mapping,config.Low,config.High);CalibrateFrame(frame);});
            Number("모터 인덱스 0~39",ref motorIndex);
            if(motorIndex>=0&&motorIndex<40)
            {
                var motor=config.Mapping.Motors.First(x=>x.Index==motorIndex);Number("해당 모터의 실제 몸통 방위각 °",ref motor.Bearing);Number("위치별 보정 배율",ref motor.Gain);motor.PlacementConfirmed=GUILayout.Toggle(motor.PlacementConfirmed,"이 모터의 물리 위치를 확인함");
                if(GUILayout.Button("선택 모터만 테스트"))Guard(()=>{var frame=new int[40];frame[motorIndex]=(int)Math.Round(DistanceEncoding.Intensity(calibrationDistance,config.Low,config.High)*100);CalibrateFrame(frame);});
            }
            Text("매핑 버전 (실측 맵은 virtual 접두사 교체)",ref config.Mapping.Version);Text("매핑 출처·확인 근거",ref config.Mapping.Provenance);
            config.Mapping.PhysicalLayoutVerified=GUILayout.Toggle(config.Mapping.PhysicalLayoutVerified,"40개 인덱스·전후면 좌우를 실제 확인함");
            config.Raw40TransportConfirmed=GUILayout.Toggle(config.Raw40TransportConfirmed,"이 SDK/Player의 X40 40채널 요청을 실제 검증함");
            config.PhysicalConcurrencyConfirmed=GUILayout.Toggle(config.PhysicalConcurrencyConfirmed,"복합 요청의 물리 동시성을 실제 확인함");
            config.IntensityConfirmed=GUILayout.Toggle(config.IntensityConfirmed,"개인·위치 보정 후 거리 강도 순서를 확인함");
            config.DurationConfirmed=GUILayout.Toggle(config.DurationConfirmed,"최종 지속시간을 파일럿에서 확인함");
            config.BodyCameraAlignmentConfirmed=GUILayout.Toggle(config.BodyCameraAlignmentConfirmed,"몸통·카메라 정면 정렬 확인");
            Text("장비/API 검증 근거",ref config.TransportEvidence);Text("설치 SDK 패키지 버전",ref config.SdkPackageVersion);
            GUILayout.Label("보정으로 상한 포화가 생기면 가까운 거리의 출력 차이가 사라질 수 있습니다. 제출 배열과 파일럿 자료를 함께 확인하세요.",bodyStyle);
        }
        void CalibrateFrame(int[] frame)
        {
            var r=haptics.Submit(frame,config.PulseMilliseconds,config,!physicalProbe,true);
            string folder=Path.Combine(Root,"CALIBRATION");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"_"+Guid.NewGuid().ToString("N")+".json");
            SessionStore.AtomicWrite(path,"{\"frame\":["+string.Join(",",frame)+"],\"receipt\":"+JsonUtility.ToJson(r)+",\"config\":"+JsonUtility.ToJson(config)+"}");
            notice=r.status+"\n"+r.error+"\n요청 배열: "+string.Join(";",frame)+"\n"+path;
        }
        public string ValidateStimuli()
        {
            var p=ScenarioGenerator.PlanA(config.Seed,config.CounterbalanceRow,config.HorizontalFov);string folder=Path.Combine(Root,"STIMULUS_VALIDATION",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
            var rows=new List<string>{"scene_family,source_block,source_theta,theta,selected_ids,retained_ids,suppressed_ids,retained_count,requested_motor_array,output_duration"};var pairs=new int[6,6];int[] duplicates=new int[18];
            foreach(var t in p)
            {
                var r=StimulusValidator.Compare(t.Threats,config.Mapping,config.Low,config.High,config.PulseMilliseconds);
                foreach(var o in r.Conditions)rows.Add(CsvTable.Line(t.Family,t.Block,t.Theta,o.Theta,string.Join(";",o.SelectedIds),string.Join(";",o.RetainedIds),string.Join(";",o.SuppressedIds),o.RetainedIds.Length,string.Join(";",o.Motors),o.Duration));
                duplicates[t.Family]+=r.IdenticalPairs;
                for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)if(r.Conditions[i].Motors.SequenceEqual(r.Conditions[j].Motors))pairs[i,j]++;
            }
            SessionStore.AtomicWrite(Path.Combine(folder,"outputs.csv"),string.Join("\n",rows)+"\n");
            var pairRows=new List<string>{"theta_a,theta_b,identical_trials,total_layouts,identical_ratio"};for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)pairRows.Add(CsvTable.Line(ThreatPolicy.Conditions[i],ThreatPolicy.Conditions[j],pairs[i,j],p.Length,pairs[i,j]/(double)p.Length));
            SessionStore.AtomicWrite(Path.Combine(folder,"pair_equivalence.csv"),string.Join("\n",pairRows)+"\n");SessionStore.AtomicWrite(Path.Combine(folder,"config_snapshot.json"),JsonUtility.ToJson(config,true));
            comparisonSummary="108개 배치 × 6조건 비교 저장.\n"+folder+"\n각 조건 쌍의 동일 배열 비율은 pair_equivalence.csv에 있습니다.\n명령 배열 차이는 피부 자극 차이를 증명하지 않습니다. 매핑: "+config.Mapping.Version;
            return folder;
        }
        void DrawValidation(){GUILayout.Label("전체 배치에 여섯 억제각을 적용해 양자화된 제출 배열을 비교합니다.",bodyStyle);if(GUILayout.Button("출력 동일성 분석"))Guard(()=>ValidateStimuli());GUILayout.Label(comparisonSummary,bodyStyle);}
        void DrawMainSettings()
        {
            Text("설정 버전",ref config.Version);Number("확정 표본 수 (-1 미확정)",ref config.FinalSampleSize);Text("표본 수 근거",ref config.SampleSizeRationale);Text("최소 관심 효과",ref config.MinimumEffect);Text("후보 선정 동률 기준",ref config.CandidateTieRule);Text("기술 실패·제외 규칙",ref config.ExclusionRule);Text("주 분석 통계 모형",ref config.StatisticalModel);
            Number("파일럿 최소 방향 정답률",ref config.PilotMinimumDirectionAccuracy);Number("파일럿 최소 거리 정답률",ref config.PilotMinimumDistanceAccuracy);Number("파일럿 최대 동일 배열 비율",ref config.PilotMaximumIdenticalPairRate);
            Text("파일럿 기준 사전등록 시각/기록",ref config.PilotCriteriaRegisteredAt);Text("실제 파일럿 근거 파일 경로",ref config.PilotEvidencePath);
            if(GUILayout.Button("파일럿 근거 파일 해시 확인"))Guard(()=>{config.PilotEvidenceSha256=SessionStore.Hash(File.ReadAllText(config.PilotEvidencePath));notice="근거 해시: "+config.PilotEvidenceSha256;});
            config.PilotCriteriaPassed=GUILayout.Toggle(config.PilotCriteriaPassed,"연구자: 실제 파일럿 결과가 사전 기준을 충족함");
            if(GUILayout.Button("검증된 본 실험 설정 잠금"))Guard(FreezeConfig);GUILayout.Label("설정 잠금: "+(string.IsNullOrEmpty(config.LockedHash)?"미잠금":config.LockedHash),bodyStyle);
        }
        void DrawDataReview()
        {
            Text("검토할 세션 폴더",ref resumeFolder);
            if(GUILayout.Button("원본 저널에서 CSV 재생성·무결성 검사"))Guard(()=>{DataExporter.Export(resumeFolder);var r=DataExporter.Records(resumeFolder);notice="완료 기록 "+r.Length+", 기술 실패 "+r.Count(t=>t.technical_failure)+", 무결성 오류 "+r.Sum(t=>DataExporter.Validate(t).Length)+"\n"+resumeFolder;});
        }
        void DrawOperator()
        {
            GUILayout.BeginArea(new Rect(25,25,Screen.width-50,Screen.height-50),GUI.skin.box);GUILayout.Label("연구자 대시보드 · 참가자에게 보이지 않도록 운영",titleStyle);
            GUILayout.Label("ID "+Participant+" / "+Mode+" / 실험 "+Experiment+" / "+Phase,bodyStyle);
            GUILayout.Label("완료 "+Completed+" / "+Total+" · 기술 실패 "+TechnicalFailures,bodyStyle);
            if(index<plan.Length)GUILayout.Label("현재 블록 "+(plan[index].Block+1)+(Mode=="DEVELOPMENT"?" · θ "+plan[index].Theta+"°":""),bodyStyle);
            if(Mode=="DEVELOPMENT")GUILayout.Label("조건 순서 "+string.Join(" → ",metadata.block_order),bodyStyle);else GUILayout.Label("조건 수치는 진행 화면에 표시하지 않습니다. 연구자는 세션 메타데이터에서 확인하세요.",bodyStyle);GUILayout.Label("저장 폴더 "+Folder,bodyStyle);GUILayout.Label(Error,bodyStyle);
            string reason;GUILayout.Label("장비: "+(Mode=="DEVELOPMENT"?"가상 출력":haptics.Ready(frozen,out reason)?"연결됨":reason),bodyStyle);
            if(GUILayout.Button("긴급 진동 중지 / 시행 중단")){haptics.Stop();Pause("Emergency stop.");}
            if(GUILayout.Button("진행 재개"))Advance();if(GUILayout.Button("세션 종료 / 메뉴"))BackToSetup();GUILayout.EndArea();
        }
        void DrawResponse()
        {
            GUILayout.Label("느낀 위협을 0~3개 표시해 주세요",titleStyle);GUILayout.Label("지도를 클릭해 방향을 추가하고, 각 마커의 거리 범주를 고르세요.",bodyStyle);
            GUILayout.Label("남은 시간 "+Math.Max(0,deadline-Now).ToString("F1")+"초",bodyStyle);
            Rect area=GUILayoutUtility.GetRect(360,360,GUILayout.ExpandWidth(false));GUI.DrawTexture(area,ringTexture);
            Vector2 center=area.center;GUI.Label(new Rect(center.x-32,area.y-3,80,26),"0° 정면");GUI.Label(new Rect(area.xMax-50,center.y-13,50,26),"90°");GUI.Label(new Rect(center.x-23,area.yMax-28,70,26),"180°");GUI.Label(new Rect(area.x+5,center.y-13,60,26),"270°");
            for(int i=0;i<Markers.Count;i++)
            {
                var marker=Markers[i];float rad=(float)(marker.response_bearing*Math.PI/180);Vector2 position=center+new Vector2(Mathf.Sin(rad),-Mathf.Cos(rad))*140;
                if(GUI.Button(new Rect(position.x-15,position.y-15,30,30),(i+1).ToString()))selectedMarker=i;
            }
            Event ev=Event.current;
            if(ev.type==EventType.MouseDown&&ev.button==0&&area.Contains(ev.mousePosition)&&Vector2.Distance(ev.mousePosition,center)>12&&Now<deadline)
            {
                Vector2 d=ev.mousePosition-center;double angle=Angles.Normalize(Math.Atan2(d.x,-d.y)*180/Math.PI);
                if(selectedMarker>=0&&selectedMarker<Markers.Count){Markers[selectedMarker].response_bearing=angle;Markers[selectedMarker].response_time=Now-responseOpened;selectedMarker=-1;}
                else AddMarker(angle,(DistanceBin)calibrationBin);ev.Use();
            }
            for(int i=0;i<Markers.Count;i++)
            {
                GUILayout.BeginHorizontal();GUILayout.Label((i+1)+": "+Markers[i].response_bearing.ToString("F1")+"°",GUILayout.Width(100));
                int old=(int)Enum.Parse(typeof(DistanceBin),Markers[i].response_distance_bin);int bin=GUILayout.SelectionGrid(old,new[]{"가까움","중간","멀음"},3);
                if(bin!=old&&Now<deadline){Markers[i].response_distance_bin=((DistanceBin)bin).ToString();Markers[i].response_time=Now-responseOpened;}
                if(GUILayout.Button("방향 수정",GUILayout.Width(95)))selectedMarker=i;
                if(GUILayout.Button("삭제",GUILayout.Width(60))&&Now<deadline){Markers.RemoveAt(i);selectedMarker=-1;GUILayout.EndHorizontal();break;}GUILayout.EndHorizontal();
            }
            if(selectedMarker>=0)GUILayout.Label("수정할 방향을 지도에서 다시 클릭하세요.",bodyStyle);
            if(GUILayout.Button("응답 완료 · 0개도 제출 가능",GUILayout.Height(44)))CompleteResponse();
        }
        static void Text(string label,ref string value){GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(320));value=GUILayout.TextField(value??"");GUILayout.EndHorizontal();}
        readonly Dictionary<string,string> numericEdits=new Dictionary<string,string>();
        string EditNumber(string label,string formatted)
        {
            string control="number:"+label;string buffer;
            if(!numericEdits.TryGetValue(control,out buffer)||GUI.GetNameOfFocusedControl()!=control)buffer=formatted;
            GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(320));GUI.SetNextControlName(control);
            buffer=GUILayout.TextField(buffer);numericEdits[control]=buffer;GUILayout.EndHorizontal();return buffer;
        }
        void Number(string label,ref double value){double next;if(double.TryParse(EditNumber(label,value.ToString("G",CultureInfo.InvariantCulture)),NumberStyles.Float,CultureInfo.InvariantCulture,out next)&&!double.IsNaN(next)&&!double.IsInfinity(next))value=next;}
        void Number(string label,ref int value){int next;if(int.TryParse(EditNumber(label,value.ToString(CultureInfo.InvariantCulture)),out next))value=next;}
    }
}
