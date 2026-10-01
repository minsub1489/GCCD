# 화면 밖 위협 반응 실험장

Unity에서 `GCCD > Acoustic Research > Create or Open Empty 3D Experiment`를 선택하고 Play를 누릅니다. 기본 시작 씬은 `Assets/_Project/Scenes/OffscreenThreatResearch.unity`입니다. 기존 FunIsland 씬은 보관되어 있습니다.

플레이어는 빈 공간의 중심에 고정됩니다. 장애물·바닥·배경 오브젝트 없이 위, 아래, 좌우, 앞뒤의 구면 공간에서 과녁이 나타납니다. 처음 보이는 카메라 영역을 제외한 연속 방향을 무작위로 샘플링하며, 기본 24회에는 정수직 위/아래 과녁도 포함됩니다. 준비 구간에는 시점을 고정하고, 실제 이벤트가 시작되면 자유롭게 돌아볼 수 있습니다. 마우스로 조준하고 왼쪽 클릭 또는 Space로 발사합니다. 과녁의 충돌체를 맞히면 과녁이 사라지고 반응시간이 기록됩니다. 이동·중력 낙하는 없습니다.

## 네 조건

| 영어 UI | CSV condition | 소리 | X40 진동 |
|---|---|---:|---:|
| No Feedback | None | 끔 | 끔 |
| Audio Only | AudioOnly | 켬 | 끔 |
| Haptics Only | HapticsOnly | 끔 | 켬 |
| Audio + Haptics | AudioAndHaptics | 켬 | 켬 |

각 조건을 독립된 블록으로 실행합니다. 같은 참가자의 네 블록에 같은 Trial seed, Trial count, 진동 정책, 거리 설정을 사용하면 같은 과녁 순서를 재사용합니다. Order group 0–3은 네 조건의 Williams 순서를 표시합니다. 진행자는 해당 순서대로 조건을 선택합니다. 조건·설정을 블록 중 변경하면 해당 블록은 중단됩니다.

`Start practice`는 음원·장비 없이 동작을 확인할 수 있으며 로그에 `practice=1`로 구분됩니다. `Start measurement`는 본 측정입니다. 청각이 포함되는 본 측정은 6개 효과음과 마스킹 음원이 모두 연결되어야 시작됩니다. 진동이 포함되는 본 측정은 X40 연결 및 Enable connected X40가 필요합니다. 장치가 끊어지거나 창 포커스가 사라지거나 Esc로 중단하면 출력이 멈추고 중단 사유가 기록됩니다.

측정 중에는 설정·소리 위치·진동 진단 화면을 숨깁니다. 조준점과 일정한 조작 안내만 남습니다. No Feedback에서도 같은 시각 과녁이 존재하며, 참가자가 주변을 탐색하여 발견할 수 있습니다.

## 효과음 연결

효과음은 아직 넣지 않았습니다. 팀원이 제공한 파일을 프로젝트에 가져온 뒤 아래 6개 프로필의 `Clip`에 연결합니다. Unity의 3D AudioSource가 위치와 거리 감쇠를 처리합니다. 클립이 없어도 동일한 게임 이벤트는 연습 및 진동 계산용으로 발생하지만 실제 사운드는 재생되지 않습니다.

`Assets/_Project/AcousticResearch/Profiles/`:

- Gunshot: 총소리, 높은 위험도
- Footsteps: 발소리, 접근 위협
- Vehicle: 차량소리, 접근 위협
- Healing: 회복 소리, 비위협 이벤트
- Nature: 자연물·환경 소리, 비위협 이벤트/마스킹 배경
- Door: 문 여는 소리, 위협 가능 이벤트

기본 본 실험 과녁은 Gunshot / Footsteps / Vehicle / Door입니다. Healing / Nature는 이벤트 미리보기로 확인할 수 있고 Nature와 Vehicle은 별도 마스킹 배경 소스로도 재사용합니다. 배경 Vehicle 인스턴스는 `TreatAsMasker`로 지정되어 위험 정책에서는 과녁 위협으로 선택되지 않습니다. 표적용 Vehicle의 위험도는 유지됩니다. 음원 교체 후 Gain, Band, RelativePower를 실제 음원의 크기·대역에 맞춰 고정하고 본 실험을 실행합니다. RelativePower는 현재 게임 메타데이터의 추정 값이며 실제 SPL 측정값이 아닙니다.

## 진동 정책과 STO

- **All events**: 가까운 모든 등록 이벤트를 방향·거리 진동으로 합성합니다. 일반 소리 매핑 확인용입니다.
- **Offscreen threats**: 화면 밖 위협을 진동시킵니다. 네 조건을 비교하는 주 실험의 기본값입니다.
- **Masked / critical threats**: 화면 밖 위험 이벤트 중, 예상 마스킹이 있거나 긴급성이 높은 이벤트를 선택합니다. 기본 최대 2개를 중요도 순서로 선택합니다.

마스킹 추정은 동시 이벤트의 상대 파워, 거리 감쇠, 대역 겹침으로 계산합니다. 소리 출력의 켜짐과 무관하게 같은 가상 이벤트로 계산하므로 Haptics Only와 Audio + Haptics의 진동 선택을 일치시킵니다. 이 정책은 프로젝트의 게임 정보 필터이며 인간 청각 마스킹 검증 모델 또는 원시 파형에서 총소리를 자동 분류하는 모델이 아닙니다. 게임의 새 사운드 이벤트에는 ResearchSoundEmitter를 붙이고 같은 Router에 등록해야 합니다.

`Experimental STO adaptation`은 서로 가까운 복수 촉각 신호의 분리를 개선하는 논문 기반 X40 실험 구현입니다. 위험 소리 선택 정책과 별개입니다. 상세한 수식·차이는 [STO_IMPLEMENTATION.md](STO_IMPLEMENTATION.md)에 있습니다. 주 실험에서는 기본적으로 끕니다. 켠 상태의 본 진동 측정은 X40 모델 검증을 완료하기 전 차단합니다. 현재 기본 과녁은 한 번에 1개이므로 단일 신호는 기존 3D 방향 매핑으로 처리합니다. 복수 신호 최적화는 `Preview overlapping threats`에서 확인할 수 있습니다.

## 기록과 Save / Discard

블록마다 조건 이름·UTC 시작 시각·고유번호가 붙은 별도 CSV와 설정 JSON을 만듭니다. 진행 중의 기록은 `Application.persistentDataPath/AcousticResearch/Pending/`에 임시 보관합니다. 블록 종료 또는 중단 후:

- **Save Logs**: 해당 블록의 CSV와 설정 JSON을 `AcousticResearch/Saved/`에 보관합니다.
- **Discard Logs**: 해당 블록의 임시 CSV와 설정 JSON을 지웁니다.

선택을 마치기 전에는 다음 블록을 시작할 수 없습니다. 저장된 이전 블록은 Discard의 대상이 아닙니다. 저장 실패 시 임시 원본을 유지하며 UI에 실패를 표시합니다. 앱을 닫거나 Unity Play Mode를 종료하면 미결정 임시 파일은 Pending 폴더에 남습니다. 현재 UI 버튼은 현재 실행의 블록을 처리하므로 재시작 후 남은 임시 파일은 해당 폴더에서 확인할 수 있습니다. 로그 파일은 GitHub에 올라가지 않습니다.

CSV의 주요 항목:

| 항목 | 의미 |
|---|---|
| entry | trial_prepared / cue_onset / missed_shot / response / haptic_decision / block_end |
| participant, practice, condition, selection | 참가자 코드·연습 여부·조건·진동 정책 |
| trial, category, azimuth, elevation, distance | 과녁 번호·이벤트 종류·방향·거리 |
| catch, planned_masking | 과녁 없는 시험·동시 배경 이벤트 계획 |
| reaction_seconds | cue_onset의 실제 호출부터 과녁 명중까지의 시간(초), hit 응답에서 사용 |
| first_visible_seconds | 이벤트 시작 후 과녁이 처음 카메라 안에 들어온 시간 |
| outcome | hit / miss / false_alarm / correct_rejection / 중단 사유 |
| detail_json | 첫 발사 시간·발사 횟수, 또는 각 소스 선택 이유·SNR 추정·40모터 값·STO 결과 |
| audio_clip_present, audio_playing, hardware_armed, sdk_sent | 실제 음원·재생·장비 허용·SDK 호출 성공 상태 |

반응시간은 예약된 예정 시각이 아니라 실제 메인 스레드 이벤트 시작 시각에서 계산합니다. 오디오·무선 장치의 실제 지각 지연을 측정한 값은 아닙니다. 빗나간 발사는 기록되고 다음 발사를 허용합니다. 기본 반응 제한은 8초입니다. 제한 시간 안에 과녁을 제거하지 못하면 miss로 기록됩니다. 준비 구간 또는 과녁 없는 catch trial에서 발사하면 false_alarm입니다. 기본 24회 중 20회가 과녁, 4회가 catch이며, 표적 종류·거리·마스킹 여부가 서로 한 조건에만 묶이지 않도록 배치합니다.

분석에서는 `practice=0`, `entry=response`, `outcome=hit`의 reaction_seconds로 참가자별 조건 반응시간을 비교하고, miss·false_alarm·명중률도 함께 비교합니다. 빠른 성공 응답만 비교하면 실패가 많은 조건이 유리해 보일 수 있습니다. 네 조건 구현 자체가 가설을 입증하지는 않으며 실제 참가자 결과가 필요합니다. 주 실험의 정책을 고정한 뒤, 마스킹 필터와 STO의 효과는 별도 실험으로 비교할 수 있습니다.

## 팀 실행과 검증

Unity 6000.3.24f1 / URP 프로젝트입니다. GitHub에는 SDK 자격증명과 라이선스 SDK를 포함하지 않습니다. 팀 PC에 SDK2를 설치하고 `GCCD > Continuous Haptics > Enable Installed bHaptics SDK`를 실행하면 기존 선택적 X40 연결 어댑터를 사용합니다. 원래 프로젝트의 로컬 SDK는 유지됩니다. 실제 X40의 모터 위치와 위/아래 감각, 적정 강도는 착용한 상태에서 확인해야 합니다.

`GCCD > Acoustic Research > Run EditMode Validation / Run PlayMode Validation`로 검사할 수 있습니다. 결과는 프로젝트 `Logs/ResearchEditMode.xml`, `Logs/ResearchPlayMode.xml`입니다. `Build macOS Research Player`는 `Builds/OffscreenThreatResearch.app`을 생성합니다. GitHub 저장소에는 빌드와 로그가 제외됩니다.
