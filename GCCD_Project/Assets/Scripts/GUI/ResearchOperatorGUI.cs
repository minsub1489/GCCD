using System;
using UnityEngine;

namespace GCCD.Research
{
    public sealed partial class ResearchController
    {
        private static readonly string[] SurveyLabels =
        {
            "NASA-TLX 1. 정신적 요구 (낮음 0 / 높음 100)",
            "NASA-TLX 2. 신체적 요구 (낮음 0 / 높음 100)",
            "NASA-TLX 3. 시간적 압박 (낮음 0 / 높음 100)",
            "NASA-TLX 4. 수행 성과 (좋음 0 / 나쁨 100)",
            "NASA-TLX 5. 노력 (낮음 0 / 높음 100)",
            "NASA-TLX 6. 좌절 (낮음 0 / 높음 100)",
            "추가 1. 방향 판단 난이도 (쉬움 0 / 어려움 100)",
            "추가 2. 대상 선택 난이도 (쉬움 0 / 어려움 100)",
            "추가 3. 진동 불편감 (없음 0 / 매우 높음 100)"
        };

        private Vector2 panelScroll;
        private GUIStyle headlineStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle directionStyle;
        private GUIStyle dangerStyle;
        private float uiScale;

        private void EnsureStyles()
        {
            if (headlineStyle != null) return;
            headlineStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            directionStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            dangerStyle = new GUIStyle(bodyStyle);
            dangerStyle.normal.textColor = new Color(1f, .45f, .35f);
        }

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width;
            float h = Screen.height;
            uiScale = Mathf.Clamp(Mathf.Min(w / 1280f, h / 720f), .75f, 1.5f);
            if (phase == StudyPhase.Preparation || phase == StudyPhase.Active)
            {
                DrawTrialHUD(w, h);
                return;
            }
            GUI.Box(new Rect(0, 0, w, h), "");
            float left = Mathf.Max(12f, (w - 1020f) / 2f);
            float top = Mathf.Max(10f, h * .04f);
            GUILayout.BeginArea(new Rect(left, top, Mathf.Min(1020f, w - 2 * left), h - top - 10f));
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            Title("GCCD | 몸통 방향성 햅틱 경고 실험");
            Text("동시 위협 수 (1 / 3) × 진동 강도 (무 / 약 / 중 / 강) · 8조건 반복측정", smallStyle);
            GUILayout.Space(10f);
            if (phase == StudyPhase.Setup) DrawSetup();
            else if (phase == StudyPhase.Calibration) DrawCalibration();
            else if (phase == StudyPhase.PracticeReady) DrawPracticeReady();
            else if (phase == StudyPhase.BlockReady) DrawBlockReady();
            else if (phase == StudyPhase.TrialReview) DrawTrialReview();
            else if (phase == StudyPhase.Survey) DrawSurvey();
            else if (phase == StudyPhase.Rest) DrawRest();
            else if (phase == StudyPhase.Finished) DrawFinished();
            else if (phase == StudyPhase.Error) DrawError();
            if (!string.IsNullOrEmpty(protocolError))
            {
                GUILayout.Space(12);
                Text(protocolError, dangerStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Title(string text)
        {
            GUILayout.Label(text, headlineStyle, GUILayout.MinHeight(36));
        }

        private void Text(string value, GUIStyle style = null)
        {
            GUILayout.Label(value, style ?? bodyStyle, GUILayout.MinHeight(24f));
        }

        private static void BigGap() => GUILayout.Space(20f);

        private bool BigButton(string label)
        {
            return GUILayout.Button(label, GUILayout.MinHeight(46f), GUILayout.MaxWidth(760f));
        }

        private void DrawSetup()
        {
            Text("운영자 준비 | 연구 대상자의 실명·학번·연락처를 입력하지 마세요.");
            BigGap();
            GUILayout.Label("세션 종류", bodyStyle);
            mode = (StudyMode)GUILayout.Toolbar((int)mode, new[]
            {
                "Demo · 시뮬레이션", "Pilot · 장치 필수", "Main · 장치 필수"
            }, GUILayout.MinHeight(42f));
            Text(mode == StudyMode.Demo
                ? "데모는 실제 진동을 출력하지 않습니다. 운영 동작과 로그만 시험할 수 있습니다."
                : "Pilot/Main에는 검증된 실제 햅틱 SDK, 4구역 배치 및 참가자별 강도 보정이 필요합니다.",
                smallStyle);
            BigGap();
            GUILayout.Label("익명 참가자 코드", bodyStyle);
            participantCode = GUILayout.TextField(participantCode, 32, GUILayout.MinHeight(38f));
            GUILayout.Label("균형 순서 행 0~7 (비우면 코드 해시로 자동 할당)", bodyStyle);
            sequenceOverride = GUILayout.TextField(sequenceOverride, 2, GUILayout.MinHeight(38f));
            Text("배정 순서: " + BuildOrderText(), smallStyle);
            BigGap();
            Text("프로토콜 (Inspector에서 상세 조정; 모든 값은 세션 스냅샷에 저장)", smallStyle);
            GUILayout.BeginHorizontal();
            Text("조건당 시행 수", smallStyle);
            if (GUILayout.Button("−", GUILayout.Width(50))) trialsPerBlock = Mathf.Max(1, trialsPerBlock - 1);
            Text(trialsPerBlock.ToString(), bodyStyle);
            if (GUILayout.Button("+", GUILayout.Width(50))) trialsPerBlock = Mathf.Min(200, trialsPerBlock + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Text("연습 시행 수", smallStyle);
            if (GUILayout.Button("−", GUILayout.Width(50))) practiceTrials = Mathf.Max(0, practiceTrials - 1);
            Text(practiceTrials.ToString(), bodyStyle);
            if (GUILayout.Button("+", GUILayout.Width(50))) practiceTrials = Mathf.Min(100, practiceTrials + 1);
            GUILayout.EndHorizontal();
            Text("4의 배수 시행 수에서 4구역 단독/3구역 조합이 조건별로 완전히 반복됩니다.", smallStyle);
            Text("고정 시행 " + activeTrialSeconds.ToString("F1") + "초 | 경고 후 마감 " +
                 threatDeadlineSeconds.ToString("F1") + "초 | 준비 지터 " +
                 preparationMinimum.ToString("F1") + "~" + preparationMaximum.ToString("F1") + "초", smallStyle);
            BigGap();
            if (BigButton("1. 강도 보정·장치 확인 화면으로"))
            {
                protocolError = "";
                phase = StudyPhase.Calibration;
                panelScroll = Vector2.zero;
            }
        }

        private void DrawCalibration()
        {
            Text("출력 장치: " + output.Name, bodyStyle);
            Text(output.IsPhysicalHardware && output.IsVerifiedReady ? "연결 검증됨" :
                "실제 장치 출력 없음 · Demo만 시작 가능", dangerStyle);
            Text("구역별 약·중·강 강도를 설정합니다. 보정 수치는 임의 초기값이며 참가자 확인 전 실험값이 아닙니다.",
                smallStyle);
            BigGap();
            for (int i = 0; i < calibration.Length; ++i)
            {
                ResearchCalibration c = calibration[i];
                GUILayout.BeginVertical(GUI.skin.box);
                Text(c.zone, bodyStyle);
                c.low = CalibrationSlider("약", c.low);
                c.medium = CalibrationSlider("중", c.medium);
                c.high = CalibrationSlider("강", c.high);
                GUI.enabled = mode != StudyMode.Demo && output.IsPhysicalHardware && output.IsVerifiedReady;
                c.distinguishable = GUILayout.Toggle(c.distinguishable, "구역 내 약/중/강 구별 가능");
                c.comfortable = GUILayout.Toggle(c.comfortable, "최고 강도도 고통 없고 편안함");
                c.confirmed = GUILayout.Toggle(c.confirmed, "참가자 반응·구역 장치 배치 확인 완료");
                GUI.enabled = true;
                GUILayout.EndVertical();
            }
            BigGap();
            if (BigButton(mode == StudyMode.Demo ? "2. Demo 시작 (Mock, 실제 진동 없음)" : "2. Pilot/Main 시작"))
            {
                protocolError = "";
                StartSession();
                panelScroll = Vector2.zero;
            }
            if (BigButton("운영 설정으로 돌아가기"))
            {
                phase = StudyPhase.Setup;
                protocolError = "";
            }
        }

        private float CalibrationSlider(string name, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, smallStyle, GUILayout.Width(36));
            float changed = GUILayout.HorizontalSlider(value, 0, 1, GUILayout.MaxWidth(550));
            GUILayout.Label(changed.ToString("F2"), smallStyle, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            return Mathf.Round(changed * 100f) / 100f;
        }

        private void DrawPracticeReady()
        {
            Title("연습 · " + (practiceCompleted + 1) + "/" + practiceTrials);
            Text("고정된 위치에서 마우스를 움직여 주변을 확인하고 화면 중앙으로 조준해 왼쪽 클릭으로 표적을 무력화합니다.");
            Text("모든 위협은 경고가 발생한 순간 시야 밖에 나타납니다. 위협은 이동하지 않습니다.");
            Text("대상의 마감시간이 지나면 같은 피해량이 적용됩니다. 사망·게임오버는 없습니다.");
            Text("모든 조건은 동일한 화면 화살표 및 방향성 음향을 사용합니다.", smallStyle);
            BigGap();
            if (BigButton("연습 시행 시작")) StartTrial(true);
        }

        private void DrawBlockReady()
        {
            ResearchCondition c = assignedOrder[blockIndex];
            Title("조건 블록 " + (blockIndex + 1) + "/8 | " + c.code);
            Text("익명 참가자 " + participantCode + " · Williams 균형 순서 " + assignedSequence, smallStyle);
            Text("동시 위협 " + c.threat_count + "개 | 강도 " + c.intensity_level);
            Text("시행 " + (trialIndex + 1) + " / " + trialsPerBlock);
            Text("시행 중에는 자세와 위치를 고정하고 마우스로 시점만 회전하세요.");
            Text("좌클릭: 사격 · ESC: 연구 세션 중단", smallStyle);
            BigGap();
            if (BigButton("시행 준비 시작")) StartBlock();
        }

        private void DrawTrialReview()
        {
            Title(currentIsPractice ? "연습 시행 종료" : "시행 종료");
            Text("명중: " + hitCount + " | 제한시간 미처리: " + failedCount + " | 남은 HP: " + hp.ToString("F0"));
            Text("시행 유효성: " + (currentTrialValid ? "유효" : "무효 - spawn/장치/중단 로그 확인"));
            Text("절대 시행시간은 일정하게 유지되었습니다. 로그: " + (logger != null ? logger.Folder : "없음"), smallStyle);
            BigGap();
            if (BigButton("계속")) AdvanceTrialReview();
        }

        private void DrawSurvey()
        {
            Title("블록 " + (blockIndex + 1) + "/8 종료 설문 | " + assignedOrder[blockIndex].code);
            Text("각 문항은 0~100점 원점수(5점 간격)입니다. NASA-TLX 가중 합산점수는 계산하지 않습니다.", smallStyle);
            BigGap();
            for (int i = 0; i < SurveyLabels.Length; ++i)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                Text(SurveyLabels[i], smallStyle);
                GUILayout.BeginHorizontal();
                surveyRatings[i] = Mathf.RoundToInt(GUILayout.HorizontalSlider(surveyRatings[i], 0, 100,
                    GUILayout.MinWidth(200f), GUILayout.MaxWidth(650f)) / 5f) * 5;
                GUILayout.Label(surveyRatings[i] + " / 100", bodyStyle, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
                surveyAnswered[i] = GUILayout.Toggle(surveyAnswered[i], "이 문항의 응답을 확정합니다.");
                GUILayout.EndVertical();
            }
            BigGap();
            bool complete = true;
            foreach (bool ok in surveyAnswered) if (!ok) complete = false;
            GUI.enabled = complete;
            if (BigButton("설문 저장 후 다음 블록으로"))
            {
                CompleteSurvey();
                panelScroll = Vector2.zero;
            }
            GUI.enabled = true;
            if (!complete) Text("9개 문항 모두 직접 확정해야 제출할 수 있습니다.", smallStyle);
        }

        private void DrawRest()
        {
            Title("조건 사이 휴식");
            Text("다음 블록: " + (blockIndex + 1) + " / 8 | " + assignedOrder[blockIndex].code);
            double left = Math.Max(0, restUntil - Now);
            Text("권장 최소 휴식 잔여 시간: " + left.ToString("F1") + "초");
            BigGap();
            GUI.enabled = left <= 0;
            if (BigButton("휴식 완료, 다음 조건 확인"))
            {
                phase = StudyPhase.BlockReady;
                panelScroll = Vector2.zero;
            }
            GUI.enabled = true;
        }

        private void DrawFinished()
        {
            Title("세션 완료");
            Text("모든 8개 조건 시행과 8회 설문이 저장되었습니다.");
            Text("익명 참가자: " + participantCode + " | 실험 모드: " + mode);
            Text("JSONL 결과: " + (logger != null ? logger.Folder : "경로 없음"), smallStyle);
            Text("장치 상태: " + output.Name, smallStyle);
        }

        private void DrawError()
        {
            Title("세션 오류 / 안전 종료");
            Text(protocolError, dangerStyle);
            Text("데이터가 일부 기록되었을 수 있습니다. 마지막 정상 줄과 trial_end.valid 플래그를 확인하세요.");
            Text("결과 경로: " + (logger != null ? logger.Folder : "로그 생성 실패"), smallStyle);
        }

        private void DrawTrialHUD(float w, float h)
        {
            GUI.Label(new Rect(w / 2 - 18, h / 2 - 22, 36, 44), "+", headlineStyle);
            if (phase == StudyPhase.Preparation)
            {
                GUI.Box(new Rect(w / 2 - 190, h * .17f, 380, 65), "READY · 정면을 유지하세요");
                return;
            }
            double remaining = Math.Max(0, trialEndAt - Now);
            GUI.Box(new Rect(w / 2 - 150, 12, 300, 38),
                "TIME " + remaining.ToString("F1") + "s    HP " + hp.ToString("F0"));
            // Equal brief visual warning for all four intensities; then target search
            // must use remembered cue information and scene perception.
            if (Now - warningTriggeredAt > visualWarningSeconds) return;
            foreach (Threat t in threats)
            {
                if (!t.spawnValid || t.neutralized) continue;
                float x = (t.zone == TorsoZone.FrontLeft || t.zone == TorsoZone.BackLeft)
                    ? w * .07f : w * .77f;
                float y = (t.zone == TorsoZone.FrontLeft || t.zone == TorsoZone.FrontRight)
                    ? h * .14f : h * .72f;
                string label;
                switch (t.zone)
                {
                    case TorsoZone.FrontLeft: label = "↖ FRONT LEFT"; break;
                    case TorsoZone.FrontRight: label = "FRONT RIGHT ↗"; break;
                    case TorsoZone.BackLeft: label = "↙ BACK LEFT"; break;
                    default: label = "BACK RIGHT ↘"; break;
                }
                GUI.Box(new Rect(x, y, w * .16f, 52f * uiScale), label, directionStyle);
            }
        }
    }
}
