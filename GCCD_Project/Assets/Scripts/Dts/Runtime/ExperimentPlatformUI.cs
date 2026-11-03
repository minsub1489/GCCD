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
        readonly string[] modes={"장비 확인","교정","자극 검증","데이터 검토"};
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
            var modern=GetComponent<ModernInterface>();if(modern&&modern.Showing)return;
            InitStyles();
            ApplyComfortableSkin();
            if(Phase==TrialPhase.Setup){DrawSetup();return;}
            if(operatorVisible){DrawOperator();return;}
            float w=Math.Min(880,Screen.width-40);GUILayout.BeginArea(new Rect((Screen.width-w)/2,30,w,Screen.height-60),GUI.skin.box);
            if(Mode=="DEVELOPMENT")GUILayout.Label("개발용 시뮬레이션 · 실제 진동 없음 · 연구 측정 자료 아님",bodyStyle);
            if(training)GUILayout.Label("훈련 · 정답 피드백 제공",bodyStyle);
            if(Phase==TrialPhase.Prepare||Phase==TrialPhase.Pulse)
            {GUILayout.FlexibleSpace();GUILayout.Label("+",new GUIStyle(titleStyle){alignment=TextAnchor.MiddleCenter,fontSize=48});GUILayout.Label("정면을 바라보고 자극을 기다려 주세요.",new GUIStyle(bodyStyle){alignment=TextAnchor.MiddleCenter});GUILayout.FlexibleSpace();}
            else if(Phase==TrialPhase.Response)DrawResponse();
            else if(Phase==TrialPhase.Questionnaire)
            {GUILayout.Label("방금 자극을 구별하는 데 얼마나 혼란스러웠나요?",titleStyle);GUILayout.Label("1 전혀 혼란스럽지 않음 — 7 매우 혼란스러움",bodyStyle);GUILayout.BeginHorizontal();for(int i=1;i<=7;i++){int rating=i;if(GUILayout.Button(i.ToString(),GUILayout.Height(60)))SaveRating(rating);}GUILayout.EndHorizontal();}
            else if(Phase==TrialPhase.Rest||Phase==TrialPhase.Paused)
            {GUILayout.Label(Phase==TrialPhase.Rest?"잠시 쉬어 주세요":"일시정지",titleStyle);GUILayout.Label(training?feedback:"준비되면 계속 버튼을 눌러 주세요.",bodyStyle);if(Phase==TrialPhase.Paused)GUILayout.Label("진행 중이던 자극은 기술 실패로 기록되며 정상 시행으로 채점하지 않습니다.",bodyStyle);if(GUILayout.Button("계속",GUILayout.Height(48)))Advance();}
            else if(Phase==TrialPhase.Finished)
            {GUILayout.Label(training?"훈련 종료":"참여가 끝났습니다",titleStyle);if(!training)
                {
                    var scored = finishedScores;
                    if(scored != null && scored.Length > 0)
                    {
                        GUILayout.Label("방향 허용오차: "+frozen.DirectionTolerance+"° 미만 (정확히 일치 포함)",bodyStyle);
                        GUILayout.Label("방향·거리 공동 정답률 "+scored.Average(t=>t.joint_accuracy).ToString("P1")+
                            "   ·   감점 반영 "+scored.Average(t=>t.adjusted_joint_accuracy).ToString("P1"),bodyStyle);
                        GUILayout.Label("멀리 빗나간 표시 "+scored.Sum(t=>t.far_false_mark_count)+"개 · 시행마다 (공동 정답 수 − 빗나간 표시 수, 최소 0) ÷ 전체 위협 수",bodyStyle);
                        DrawThreatReview(scored);

                    }
                }
                if(training)GUILayout.Label(feedback+"\n설정된 훈련 기준 통과: "+trainingPassed,bodyStyle);if(!training)
                {
                    if(GUILayout.Button("CSV 저장",GUILayout.Height(48)))SaveFinishedCsv();
                    if(!string.IsNullOrEmpty(resultExportStatus))GUILayout.Label(resultExportStatus,bodyStyle);
                }
                if(GUILayout.Button(training||resultExportStatus.StartsWith("CSV 저장 완료")?"메인으로":"저장 없이 메인으로",GUILayout.Height(48)))BackToSetup();}
            else if(Phase==TrialPhase.Fault)
            {GUILayout.Label("진행 중단 · 연구자 확인 필요",titleStyle);GUILayout.Label(Error,bodyStyle);GUILayout.Label("확인된 원본 기록은 저장 폴더에 보존됩니다.",bodyStyle);if(GUILayout.Button("연구자 메뉴"))BackToSetup();}
            GUILayout.EndArea();
        }
        int reviewedTrialIndex;
        Vector2 reviewedThreatScroll;

        void DrawThreatReview(TrialRecord[] trials)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = reviewedTrialIndex > 0;
            if (GUILayout.Button("이전 시행")) { reviewedTrialIndex--; reviewedThreatScroll = Vector2.zero; }
            GUI.enabled = reviewedTrialIndex < trials.Length - 1;
            if (GUILayout.Button("다음 시행")) { reviewedTrialIndex++; reviewedThreatScroll = Vector2.zero; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var trial = trials[reviewedTrialIndex];
            GUILayout.Label($"{trial.trial_id} ({reviewedTrialIndex + 1}/{trials.Length}) · 억제각 {trial.condition_theta}° · 유지 {trial.retained_count}개 · 억제 {trial.suppressed_count}개", bodyStyle);
            GUILayout.BeginHorizontal();
            float size = Mathf.Clamp(Screen.height * .30f, 140, 280);
            Rect map = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(map, ringTexture);
            GUI.Label(new Rect(map.center.x - 23, map.y, 62, 22), "0° 정면");
            foreach (var threat in trial.threats)
            {
                float angle = (float)threat.bearing_deg * Mathf.Deg2Rad;
                float radius = size * .39f * Mathf.Clamp01((float)threat.distance_m / 36);
                Vector2 point = map.center + radius * new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                Color previous = GUI.color;
                GUI.color = threat.suppressed ? new Color(1f, .53f, .40f) : new Color(.53f, 1f, .72f);
                GUI.Label(new Rect(point.x - 13, point.y - 11, 46, 24),
                    (threat.suppressed ? "× " : "● ") + threat.threat_id);
                GUI.color = previous;
            }

            reviewedThreatScroll = GUILayout.BeginScrollView(reviewedThreatScroll, GUILayout.Height(size));
            GUILayout.Label("● 유지   × 억제", bodyStyle);
            foreach (var threat in trial.threats)
            {
                string state = threat.suppressed ? $"× 억제 · 기준 {threat.suppression_parent_id}" : "● 유지";
                GUILayout.Label($"{threat.threat_id}   {threat.bearing_deg:F1}°   {threat.distance_m:F1}m   {state}", bodyStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
        }

        void Guard(Action action){try{action();}catch(Exception e){notice=e.GetBaseException().Message;}}
        void DrawAdvancedSetup()
        {
            GUILayout.BeginArea(new Rect(20,20,Screen.width-40,Screen.height-40),GUI.skin.box);
            GUILayout.Label("GCCD · 거리 우선 방향 억제각 실험",titleStyle);
            menu=GUILayout.SelectionGrid(menu,modes,4,GUILayout.Height(76));
            setupScroll=GUILayout.BeginScrollView(setupScroll);
            if(menu==0)DrawDevice();
            else if(menu==1)DrawCalibration();
            else if(menu==2)DrawValidation();
            else DrawDataReview();
            GUILayout.Space(14);GUILayout.BeginHorizontal();if(GUILayout.Button("설정 저장"))Guard(SaveConfig);if(GUILayout.Button("설정 불러오기"))Guard(LoadConfig);if(GUILayout.Button("긴급 진동 중지"))notice=haptics.Stop();GUILayout.EndHorizontal();
            GUILayout.Label("설정 파일: "+ConfigPath,bodyStyle);Text("재개할 세션 폴더",ref resumeFolder);
            if(GUILayout.Button("세션 재개 · 미완료 시행은 기술 실패로 보존"))ResumeSession(resumeFolder);
            if(!string.IsNullOrEmpty(Folder))GUILayout.Label("최근 데이터: "+Folder,bodyStyle);
            if(!string.IsNullOrEmpty(Error))GUILayout.Label(Error,bodyStyle);if(!string.IsNullOrEmpty(notice))GUILayout.Label(notice,bodyStyle);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
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
            var p=ScenarioGenerator.PlanA(config.Seed,config.CounterbalanceRow,config.HorizontalFov,config.ThreatCountFor(LobbySimulation),config.TrialsPerConditionFor(LobbySimulation));string folder=Path.Combine(Root,"STIMULUS_VALIDATION",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
            var rows=new List<string>{"scene_family,source_block,source_theta,theta,selected_ids,retained_ids,suppressed_ids,retained_count,requested_motor_array,output_duration"};var pairs=new int[6,6];
            foreach(var t in p.Where(t=>t.Block==0))
            {
                var r=StimulusValidator.Compare(t.Threats,config.Mapping,config.Low,config.High,config.PulseMilliseconds);
                foreach(var o in r.Conditions)rows.Add(CsvTable.Line(t.Family,t.Block,t.Theta,o.Theta,string.Join(";",o.SelectedIds),string.Join(";",o.RetainedIds),string.Join(";",o.SuppressedIds),o.RetainedIds.Length,string.Join(";",o.Motors),o.Duration));
                for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)if(r.Conditions[i].Motors.SequenceEqual(r.Conditions[j].Motors))pairs[i,j]++;
            }
            SessionStore.AtomicWrite(Path.Combine(folder,"outputs.csv"),string.Join("\n",rows)+"\n");
            var pairRows=new List<string>{"theta_a,theta_b,identical_trials,total_layouts,identical_ratio"};for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)pairRows.Add(CsvTable.Line(ThreatPolicy.Conditions[i],ThreatPolicy.Conditions[j],pairs[i,j],p.Length/6,pairs[i,j]/(p.Length/6.0)));
            SessionStore.AtomicWrite(Path.Combine(folder,"pair_equivalence.csv"),string.Join("\n",pairRows)+"\n");SessionStore.AtomicWrite(Path.Combine(folder,"config_snapshot.json"),JsonUtility.ToJson(config,true));
            comparisonSummary=(p.Length/6)+"개 배치 × 6조건 비교 저장.\n"+folder+"\n각 조건 쌍의 동일 배열 비율은 pair_equivalence.csv에 있습니다.\n명령 배열 차이는 피부 자극 차이를 증명하지 않습니다. 매핑: "+config.Mapping.Version;
            return folder;
        }
        void DrawValidation(){GUILayout.Label("전체 배치에 여섯 억제각을 적용해 양자화된 제출 배열을 비교합니다.",bodyStyle);if(GUILayout.Button("출력 동일성 분석"))Guard(()=>ValidateStimuli());GUILayout.Label(comparisonSummary,bodyStyle);if(GUILayout.Button("검증된 본 실험 설정 잠금"))Guard(FreezeConfig);}
        void DrawDataReview()
        {
            Text("검토할 세션 폴더",ref resumeFolder);
            if(GUILayout.Button("원본 저널에서 CSV 재생성·무결성 검사"))Guard(()=>{DataExporter.Export(resumeFolder);var r=DataExporter.Records(resumeFolder);notice="완료 기록 "+r.Length+", 기술 실패 "+r.Count(t=>t.technical_failure)+", 무결성 오류 "+r.Sum(t=>DataExporter.Validate(t).Length)+"\n"+resumeFolder;});
        }
        void DrawOperator()
        {
            GUILayout.BeginArea(new Rect(25,25,Screen.width-50,Screen.height-50),GUI.skin.box);GUILayout.Label("연구자 대시보드 · 참가자에게 보이지 않도록 운영",titleStyle);
            GUILayout.Label("ID "+Participant+" / "+Mode+" / 방향 억제각 실험 / "+Phase,bodyStyle);
            GUILayout.Label("완료 "+Completed+" / "+Total+" · 기술 실패 "+TechnicalFailures,bodyStyle);
            if(index<plan.Length)GUILayout.Label("현재 블록 "+(plan[index].Block+1)+(Mode=="DEVELOPMENT"?" · θ "+plan[index].Theta+"°":""),bodyStyle);
            if(Mode=="DEVELOPMENT")GUILayout.Label("조건 순서 "+string.Join(" → ",metadata.block_order),bodyStyle);else GUILayout.Label("조건 수치는 진행 화면에 표시하지 않습니다. 연구자는 세션 메타데이터에서 확인하세요.",bodyStyle);GUILayout.Label("저장 폴더 "+Folder,bodyStyle);GUILayout.Label(Error,bodyStyle);
            string reason;GUILayout.Label("장비: "+(Mode=="DEVELOPMENT"?"가상 출력":haptics.Ready(frozen,out reason)?"연결됨":reason),bodyStyle);
            if(GUILayout.Button("긴급 진동 중지 / 시행 중단")){haptics.Stop();Pause("Emergency stop.");}
            if(GUILayout.Button("진행 재개"))Advance();if(GUILayout.Button("세션 종료 / 메뉴"))BackToSetup();GUILayout.EndArea();
        }
        Vector2 markerScroll;
        void DrawResponse()
        {
            GUILayout.Label("느낀 위협의 방향과 거리를 표시해 주세요",titleStyle);GUILayout.Label("지도를 클릭해 방향을 추가하고, 각 마커의 거리 범주를 고르세요.",bodyStyle);
            GUILayout.Label("남은 시간 "+Math.Max(0,deadline-Now).ToString("F1")+"초",bodyStyle);
            float mapSize = Mathf.Clamp(Screen.height * .40f, 190, 360);
            Rect area=GUILayoutUtility.GetRect(mapSize,mapSize,GUILayout.ExpandWidth(false));GUI.DrawTexture(area,ringTexture);
            Vector2 center=area.center;GUI.Label(new Rect(center.x-32,area.y-3,80,26),"0° 정면");GUI.Label(new Rect(area.xMax-50,center.y-13,50,26),"90°");GUI.Label(new Rect(center.x-23,area.yMax-28,70,26),"180°");GUI.Label(new Rect(area.x+5,center.y-13,60,26),"270°");
            for(int i=0;i<Markers.Count;i++)
            {
                var marker=Markers[i];float rad=(float)(marker.response_bearing*Math.PI/180);Vector2 position=center+new Vector2(Mathf.Sin(rad),-Mathf.Cos(rad))*(mapSize*.39f);
                if(GUI.Button(new Rect(position.x-15,position.y-15,30,30),(i+1).ToString()))selectedMarker=i;
            }
            Event ev=Event.current;
            if(ev.type==EventType.MouseDown&&ev.button==0&&area.Contains(ev.mousePosition)&&Vector2.Distance(ev.mousePosition,center)>12&&Now<deadline)
            {
                Vector2 d=ev.mousePosition-center;double angle=Angles.Normalize(Math.Atan2(d.x,-d.y)*180/Math.PI);
                if(selectedMarker>=0&&selectedMarker<Markers.Count){Markers[selectedMarker].response_bearing=angle;Markers[selectedMarker].response_time=Now-responseOpened;selectedMarker=-1;}
                else AddMarker(angle,(DistanceBin)calibrationBin);ev.Use();
            }
            markerScroll = GUILayout.BeginScrollView(markerScroll, GUILayout.MaxHeight(Mathf.Max(48, Screen.height - mapSize - 270)));
            for(int i=0;i<Markers.Count;i++)
            {
                GUILayout.BeginHorizontal();GUILayout.Label((i+1)+": "+Markers[i].response_bearing.ToString("F1")+"°",GUILayout.Width(100));
                int old=(int)Enum.Parse(typeof(DistanceBin),Markers[i].response_distance_bin);int bin=GUILayout.SelectionGrid(old,new[]{"가까움","중간","멀음"},3);
                if(bin!=old&&Now<deadline){Markers[i].response_distance_bin=((DistanceBin)bin).ToString();Markers[i].response_time=Now-responseOpened;}
                if(GUILayout.Button("방향 수정",GUILayout.Width(95)))selectedMarker=i;
                if(GUILayout.Button("삭제",GUILayout.Width(60))&&Now<deadline){Markers.RemoveAt(i);selectedMarker=-1;GUILayout.EndHorizontal();break;}GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
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
