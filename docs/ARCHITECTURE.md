# 구조와 데이터 흐름

새 플랫폼은 `Assets/_Project/SilentResearch`의 독립 어셈블리입니다. 공유 모터 계산·출력 어댑터와 Input System을 참조합니다. 이동 컴포넌트·오디오·UI 자산은 GUID를 유지해 새 연구 폴더로 이동했습니다. SDK는 로컬의 기본 어셈블리 어댑터로 연결합니다.

```text
SilentScenario + seed → SilentTrialPlan (type × direction / catch)
                     → SilentExperimentSession (monotonic onset)
                         ├─ SoundEvent → SilentStimulusOutput
                         │                 ├─ A: spatial AudioSource
                         │                 └─ B/C/D: SilentHapticPatterns
                         │                     → X40MotorLayout → HapticVestOutput
                         │                         ├─ existing BHapticsVestOutput / SDK backend
                         │                         └─ simulation motor frame
                         ├─ SilentGameWorld → existing ResearchPlayerMotion + silent monster
                         └─ input / world outcomes → TrialRecord
                             → durable journal + unique final CSV → SilentMetrics / Python analysis
```

| 클래스 | 역할 |
|---|---|
| SilentScenario | ScriptableObject: 시나리오 ID, 위치·속도·탐지·제한·노출 이후 게임 시간 |
| SilentTrialPlan / SoundEvent | 시드 재현 계획, Williams 순서 재사용, 순환 오차/시점 방위. Unity 내부 사건을 출력으로 변환 |
| SilentHapticPatterns | 조절 가능한 펄스 배열. B 동일 시간패턴, C 유형별 패턴, D C와 같은 시간패턴+4방향 모터 위치 |
| SilentStimulusOutput | 조건의 유일한 출력 소유자. 실제 SDK와 시뮬레이션 분리, 1초 노출 상한, 예정 펄스 만료/실패 확인 |
| SilentGameWorld | foreperiod 이동 통제, 실내 은폐 볼륨, 실제 이동 회피 판정, 정해진 경로 순찰·추격, 목표/잡힘 결과. 조건 enum 참조 없음 |
| SilentExperimentSession | 참가자/블록 관리, 설정 고정, 시점별 첫 응답, 독립 방어 RT, 중단/종료 정리 |
| SilentDataRecorder / SilentMetrics | 생성 시 CreateNew, 각 checkpoint fsync, final 키 중복 방지, 결측 빈칸, 분모 포함 요약 |
| SilentParticipantUI / SilentResearcherUI | 참가자 중립 조작 UI, UI Toolkit 연구자 설정/결과 UI. 기존 ResearchPanel·TextCore 폰트와 InputSystemUIInputModule을 재사용. 측정 중 연구자 UI/시뮬레이터 강제 비표시 |
| SilentResearchSetup | Unity 에셋·씬 API로 실내 씬/설정/음원 연결 및 검증. 공포게임 씬만 유지 |

X40 마스크는 기존 `X40MotorLayout.Get`의 행과 방위 좌표만 사용합니다. B/C는 중앙 행 8개 모터, D는 선택 구역의 두 둘레 열 × 4행으로 8개 모터를 사용합니다. 하드웨어 채널 순서를 재정의하지 않습니다. 좌/우는 착용자 기준이고, 방향은 카메라 yaw를 따라 각 펄스 시작 시 다시 계산합니다. 위/아래 시점 변화는 수평 방향을 반전시키지 않습니다. Sector helper는 4–8 구역에 대응하며, 실제 8방향 조건/마스크 검증은 후속 확장입니다.

SDK 백엔드는 `.cs.txt` 템플릿에서 로컬 SDK에 설치한 사본만 `GCCD_BHAPTICS_SDK2` 정의 아래 컴파일되며 `PlayMotors`와 소유 요청 `StopInt` 경로를 사용합니다. 250ms 이하 bounded pulse를 분리해 보내고 다른 소비자의 StopAll은 호출하지 않습니다. 장비 부재는 simulation으로 자동 위장하지 않습니다. 측정 시작 차단/연결 실패 중단으로 데이터 유효성을 구분합니다.

설정은 시작 시 복제하여 게임에 적용하고 원본 구성 변화는 0.5초마다 검사해 블록을 중단합니다. 실물 출력 실패는 즉시 중단합니다. 모든 참가자 응답은 첫 차원별 응답만 기록하고, 최종 CSV는 세션/시행당 한 번 기록합니다. 앱 중단/초점 상실/quit도 같은 경로를 통과합니다. 프로세스 강제 종료는 OnQuit 보장을 할 수 없으므로 JSONL 복구를 사용합니다. 물리적 장치 해제 순간의 중지를 소프트웨어로 보장할 수는 없으며 기존 어댑터의 bounded pulse가 남습니다.

`SilentPilotController`는 같은 장면의 교정과 식별 시험을 독점 실행합니다. `SilentPilotEvidence`는 40채널/48회 전체 기록에서 통과를 다시 계산하며 장치 해시·패턴 해시·참가자에 묶습니다. 소프트웨어 인증/암호학적 장치 증명이 아닌, SDK 전달 확인과 사람의 응답 기록입니다. `SilentDifficultyReview`는 별도 Purpose의 블록 자료만 평가합니다. 본 실험 분석은 Measurement 목적만 포함합니다.
