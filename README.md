# GCCD — 무음 게임의 청각 정보 대체 햅틱 연구 플랫폼

**Design and Evaluation of Haptic Feedback for Auditory Information Substitution in Silent Games**

소리를 제거한 1인칭 실내 게임에서 사건 발생, 유형, 방향을 햅틱으로 전달했을 때의 수행 성능을 측정합니다. 기존 연속 X40 모터 배치·SDK 어댑터, `ResearchPlayerMotion`, 무료 발소리·문소리, Williams 조건 순서를 재사용했습니다. 프로젝트에는 공포게임 연구 장면 하나만 남겼습니다. 이전 맵·과녁 실험은 저장소 밖의 로컬 보관 폴더로 이동했습니다.

| 조건 | 청각 | 햅틱 정보 |
|---|---|---|
| A Audio Only | 공간 음원 | 없음 |
| B Basic Haptic | 무음 | 동일 패턴으로 사건 발생 |
| C Type Haptic | 무음 | 유형별 시간 패턴 |
| D Type + Direction Haptic | 무음 | 유형 + 현재 시점 기준 4방향 위치 |

Unity **6000.3.24f1**에서 `GCCD_Project`를 열고 **GCCD > Silent Study > Create or Open Research Platform**을 실행한 뒤 Play를 누릅니다. 씬은 `Assets/_Project/Scenes/SilentHapticResearch.unity`입니다. 참가자 익명 ID, 시드, 조건, 시나리오를 설정하고 연습 또는 측정을 시작합니다. 기본은 장비 없는 연습용 시뮬레이션입니다. 이 로컬 프로젝트에는 bHaptics SDK2와 macOS 네이티브 라이브러리를 설치하고 활성화했습니다. SDK와 계정 설정은 Git에서 제외합니다. 다른 PC에서는 공식 Haptic Plugin을 설치하고 **GCCD > Silent Study > Enable Installed SDK2**를 실행합니다. 실제 측정은 X40 연결과 저장된 교정·식별 통과 기록을 요구합니다.

WASD 이동, 마우스 시점, Ctrl로 탁자 아래 숨기, Space로 시점 방향 회피, F/G 위협/안전 응답, 1/2/3 발소리/문소리/울음 분류, 화살표로 현재 시점 기준 방향 응답입니다. 발소리는 숨기, 울음은 회피, 문소리는 두 행동 중 하나를 사용합니다. Esc는 현재 시행을 중단하고 저장합니다. 사건 응답 창이 닫히기 전에는 괴물이 보이지 않습니다. 이후 순찰·추격을 피하고 EXIT에 도달합니다.

결과는 `Application.persistentDataPath/SilentStudy/<SessionID>/`에 **trials.csv, journal.jsonl, configuration.json, summary.json**으로 자동 저장됩니다. 무응답·실패·중단도 기록하며, 불가측정 항목은 빈칸입니다. 연습과 중단 시행은 유효 측정에서 제외합니다. 하드웨어와 실제 참가자 연구 결과는 별도 검증 사항입니다.

- [연구 질문·가설·타당성 및 한계](docs/RESEARCH.md)
- [실험 절차·측정 지표·분석·복구](docs/EXPERIMENT.md)
- [구조·데이터 흐름·재사용 범위](docs/ARCHITECTURE.md)
- [프로젝트 정리와 재사용 범위](docs/PROJECT_AUDIT.md)
- [실제 검증 기록](docs/SILENT_VALIDATION.md)


교정·파일럿은 같은 장면의 연구자 화면에서 실행합니다. **Check SDK / Player / X40 connection → 40-channel calibration → labelled training → blind type / type+direction identification → difficulty pilot** 순서입니다. 유형·방향 시험은 각 48회이며, 각 클래스 80% 이상을 요구합니다. 교정 확인 체크박스는 제거했습니다. 시뮬레이션은 교정 증명으로 인정하지 않습니다.

난이도는 easy/base/hard 시나리오에서 블록 시작 전에 선택합니다. 전체 15회 이상 유효 시행을 마쳐야 판정하며 목표 성공률 ≤20%는 바닥, ≥90%는 천장 가능성으로 표시합니다. 파일럿 결과는 본 실험 분석에서 제외하고, 블록 중 설정 변경은 중단합니다.

장비를 현재 사용할 수 없어 **실물 출력·체감 교정·실제 참가자 파일럿 결과는 대기 상태**입니다. [실물 검증 및 파일럿 실행 안내](docs/X40_PILOTS.md)를 따르세요.
