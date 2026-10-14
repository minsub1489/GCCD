# 실험 운영과 데이터 정의

## 준비와 진행

1. Unity 6000.3.24f1에서 `GCCD > Silent Study > Create or Open Research Platform`을 실행하고 Play합니다. 새 씬 생성은 기존 씬을 삭제하지 않습니다.
2. 익명 참가자 ID(영문·숫자·_·-, 1–24자), 참가자 순서 인덱스, 블록 인덱스, 시드, Indoor01/Indoor02 시나리오를 설정합니다. 참가자 번호 인덱스 % 4가 Williams 순서를 결정합니다. 순서 안내대로 연구자가 각 블록 조건을 선택합니다.
3. 실제 연구 전 Headphones의 공간 음원과 bHaptics X40의 각 인덱스·세기를 교정합니다. 로컬 SDK2 설치 및 define는 [SDK_AUDIT.md](SDK_AUDIT.md), [X40_PILOTS.md](X40_PILOTS.md) 경로를 재사용합니다. 계정 키는 소스에 넣지 않습니다.
4. 연습에서 F1 시뮬레이터로 사건/40모터를 확인할 수 있습니다. 실제 조끼로 패턴을 충분히 학습시키고 유형·방향 식별 기준을 확인합니다. 시뮬레이터 성공을 촉각 구별 가능성으로 간주하지 않습니다.
5. A는 AudioOnly, B는 BasicHaptic, C는 TypeHaptic, D는 TypeDirectionHaptic입니다. B/C/D 측정은 실물 출력·교정 확인·연결이 없으면 시작을 거부합니다. 연구자 UI는 측정 내내 숨겨집니다.
6. WASD·마우스 이동/시점, Ctrl 은폐, Space 회피. F/G는 위협/안전, 1/2/3은 발소리/문소리/울음, 화살표는 **응답하는 순간의 시점 기준** 전/우/후/좌입니다. 각 차원 첫 응답만 기록합니다. 무음 catch에서는 시행 reset 후 공통 최대 foreperiod + 0.1초(기본 3.1초)부터 G 응답을 받습니다. 이 고정 안내는 실제 onset이나 정답을 표시하지 않습니다. 위협 보고와 방어 행동은 별개의 측정입니다.
7. 같은 시나리오/시드를 모든 조건에 적용하고 조건 사이 휴식을 둡니다. Esc, 앱 초점 상실, 종료, 설정 변경, 장치 단절, 출력 실패는 현재 시행을 무효 중단으로 저장합니다. 재시작은 새 SessionID로 시작하며 기존 결과를 덮어쓰지 않습니다.

기본 시행: foreperiod 1.5–3초 → 사건 기준 시각 → 3초 응답 창 → 5초 추격 평가 → EXIT 완료 또는 출발 후 20초 제한. 무위협 시행도 같은 시간 기준을 갖지만 아무 출력/추격도 없습니다. 실제 사건 순서는 `SilentTrialPlan`의 시드 셔플이며 12 threat + 3 catch입니다. 연습은 입력 시드 XOR 0x5A17C0DE를 사용하며 InputSeed는 입력값, RandomSeed는 실제 실행값을 기록합니다. 같은 시나리오는 조건에 따른 난이도 분기를 갖지 않습니다.

## 측정 시작·종료·분모

| 지표 | 정의와 코드 기록 |
|---|---|
| 목표 달성률 % | Valid=true, Practice=false인 최종 시행 전체 중 Status=Success 비율. Caught/Timeout은 실패로 포함, Aborted는 무효로 따로 보고 |
| 위협 탐지 정확도 % | 전체 유효 threat 중 onset 이후 응답 창 내 첫 F 응답 비율. G·무응답·선행 F는 실패. `DetectionSuccess` |
| 사건 유형 정확도 % | 전체 유효 threat 중 첫 1/2/3이 실제 유형과 같은 비율. 오답·무응답 포함. `IsCorrect`는 **유형 분류 정확성** |
| 방향 인지 오차 ° | 응답 순간 실제 사건의 카메라 yaw 기준 방위와 첫 화살표 방향의 `abs(DeltaAngle)` (0–180°). `DirectionTruthAtResponseDeg`/`DirectionResponse`/`DirectionErrorDeg`. `EventDirection`은 onset 시점 방위로 따로 기록. 무응답은 빈칸이며 방향 응답률 동반 보고 |
| 위협 대응시간 ms | 실제 논리 onset부터 **유형에 맞는 첫 실제 방어 행동**까지. Hide는 지정 볼륨 안에서 새로 숨기 시작, Evade는 사건 반대 방향 dot≥.25이고 실제 0.3m 이상 움직임. 발소리 Hide, 울음 Evade, 문 둘 중 하나. 3초 창 안에서만 유효. `ReactionTimeMs` |
| 회피 성공률 % | 전체 유효 threat 중 5초 추격 창 끝까지 잡히지 않은 비율. 초기 방어 응답 정답과 별개. Timeout이어도 추격 창을 살아남으면 true 가능. catch는 측정불가 빈칸 |
| 오경보율 % | 전체 유효 catch 중 첫 위협 보고 F 비율. foreperiod 선행 F도 catch에서는 오경보. catch 무응답 수와 명시적 Safe 응답률을 함께 보고 |
| 완료시간 s | trial 시작부터 실제 EXIT 성공까지. 성공 시행만 값 존재. 실패의 경과시간은 `OutcomeTimeSec`, 실패/중단 CompletionTimeSec은 빈칸 |

DetectionTimeMs, TypeTimeMs, DirectionTimeMs는 각 차원 첫 응답의 지연이며 ReactionTimeMs와 다릅니다. 평균 올바른 대응시간만 낮아져도 성공률·정확도·응답률이 낮아졌으면 성능 향상으로 판단하지 않습니다. 응답 창 이후 시각 기반 행동의 RT를 섞지 않습니다. 차원별 응답은 다른 측정을 대신하지 않습니다.

시간은 Stopwatch 고해상도 단조 시계입니다. Timestamp만 UTC의 벽시계입니다. 실제 Unity 메인 스레드의 발생 시각을 onset으로 쓰고 예정 시각과 차이를 OnsetJitterMs에 기록합니다. 이 값은 전기/촉각 출력 onset이나 헤드폰 지연을 측정하지 않습니다. MaxHapticDispatchJitterMs는 소프트웨어 펄스 요청의 예정 시각 차이입니다. 놓친 펄스 또는 실제 측정 중 250ms 초과 프레임 간격은 중단합니다.

## 파일과 결측

`Application.persistentDataPath/SilentStudy/<SessionID>/`(Researcher Inspector의 ResultRoot로 변경 가능):

- `configuration.json`: 입력 변수·시나리오·패턴·음원 이름/길이·정적 충돌체·출력모드 스냅샷.
- `journal.jsonl`: trial 생성, onset, 첫 응답, 최종행을 즉시 append/Flush(true). 강제 종료 시 미완료 상태 복구용.
- `trials.csv`: 시행별 최종행 정확히 한 번. 필수 18개 항목과 유효성·연습·출력모드·실제 onset·방향 응답·시각 노출·구성 해시 등을 포함.
- `summary.json`: 유효 실제 측정만 집계. 백분율 분모와 응답 수를 함께 저장. 유효 자료가 없으면 빈 문자열이며 0이 아님.

CSV/JSONL의 수치·boolean은 명시적인 문자열입니다. 값 없음은 `""`, 실제 0ms/0°는 `"0"`, 실패는 `"false"`입니다. catch 유형은 None, 방향·유형 정확성·회피·RT는 해당하지 않으면 빈칸입니다. 중단 시 결정되지 않은 성과도 빈칸입니다. Practice=true 기록과 Valid=false 기록은 주 분석에서 제외하되 누락하지 않습니다. TrialID는 SessionID 안에서 유일합니다. crash 시 전체 과정에서 exactly-once 트랜잭션은 보장하지 않으며 세션/시행 키로 복구·중복 제거합니다.

## 분석과 복구

```bash
python3 Tools/silent_results.py analyze /path/to/trials.csv --output /path/to/analysis.json
python3 Tools/silent_results.py recover /path/to/journal.jsonl --output /path/to/recovered.csv
```

분석기는 기존 결과를 덮어쓰지 않으며 연습/무효 기록을 별도 계수합니다. 참가자×조건×시나리오로 집계하며 no-response 포함 정확도, catch 응답 수, 방향/방어 응답 수, 성공 수를 함께 제공합니다. 두 파일에 같은 세션/시행이 있으면 같은 행만 중복 제거하고 다른 행이면 오류를 냅니다. 조건 간 ControlHash가 같아야 통제 설정/시드가 같은지 확인할 수 있습니다. ConfigHash는 참가자·조건·출력모드도 포함하므로 조건마다 달라지는 것이 정상입니다.

복구는 각 시행의 마지막 저널 상태를 사용합니다. 미완료 상태는 Aborted/Valid=false로 바꾸며 알 수 없는 결과와 시간을 만들어 넣지 않습니다. 마지막 잘린 한 줄만 무시하고 중간 손상은 오류 처리합니다. 원본은 보존합니다. RT/완료시간은 응답/성공 시행에 조건화되어 있으므로 정확도·실패·응답률 및 가능한 검열 분석과 함께 읽습니다. 실제 연구의 paired 검정·신뢰구간·효과 크기는 참가자별 집계에 적용합니다. 자동 요약은 가설 검증이나 비열등성 판정을 대신하지 않습니다.

## 구현된 교정·파일럿 게이트

물리 교정 완료 체크박스는 없습니다. 40채널의 SDK 전달 승인·정확한 체감 위치·편안함 응답 전부를 저장해야 통과합니다. B/C/D 시작은 같은 장치와 패턴의 통과 기록을 요구합니다. C는 해당 참가자의 48회 유형 식별 통과, D는 유형 및 48회 방향 식별 통과도 요구합니다. 각 유형/각 방향 클래스 정확도 80% 이상을 사전에 고정했습니다. 이 기준은 연구 효과의 증명이 아닌 파일럿 운영 기준입니다.

DifficultyPilot은 연습과 본 실험 모두와 별도 목적/시드를 사용합니다. 시나리오 easy/base/hard를 블록 전에 선택하고 A–D 조건을 동일 시나리오로 비교합니다. 완전한 유효 블록만 바닥/천장 가능성을 판정합니다. 자동으로 패턴이나 난이도를 바꾸지 않습니다. 교정·식별 파일은 `SilentPilots`, 난이도 파일은 `SilentStudy`에 저장되며 분석 도구는 본 실험 목적만 포함합니다.
