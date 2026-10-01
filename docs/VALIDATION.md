# FunIsland / 연속 3D 햅틱 검증 기록

검증 환경: Unity **6000.3.24f1**, macOS Apple Silicon, Input System 1.20.0, URP 17.3.0. SDK 포함 로컬 버전은 bHaptics Haptic Plugin **2.8.1**을 사용했다. GitHub 버전에는 SDK와 인증 설정이 없다.

## 자동 검사

2026-10-01 GitHub용 SDK 없는 프로젝트에서:

| 검사 | 결과 | 범위 |
|---|---:|---|
| EditMode | **249 / 249 통과** | 16개 수평 각도 × 5개 elevation × 3가지 보간 방식. 각 경우에 5개 거리 검사. 앞뒤·착용자 좌우 인덱스, 360° 경계, 극점, 회전·scale, 곡선·수치 검증, 세기 제한, dead zone, 각도 smoothing, 타깃 유실. |
| PlayMode | **9 / 9 통과** | 컴포넌트·오브젝트 비활성화/삭제, 포커스·pause·quit 콜백, 5Hz 전송 간격, SDK 없는 출력, 모의 SDK의 요청 중지·연결 해제·중지 실패 후 펄스 만료 대기. FunIsland 씬 로딩·접지·움직이는 위협·플레이어 상대 방향·타깃 삭제. |
| 씬 검사 | 통과 | 110개 Renderer의 URP Lit 재질, 활성 카메라/AudioListener 각 1개, 플레이어/타깃/출력 연결, missing script 없음. |

같은 로컬 구현에서도 계산 249개와 실행 9개를 통과했다. 모의 SDK 검사는 실제 장치 통신의 증거가 아니다. 포커스/pause/quit 검사는 Unity 콜백을 호출하여 정지 동작을 확인한 것이며 운영체제의 모든 상태 전환을 재현하지 않는다.

## 빌드

- 2026-09-29 **SDK 포함 macOS Development Build 성공**. BuildReport errors=0, warnings=347.
- 경고는 주로 기존 AI inference/Sentis 셰이더와 Pipeline 관련이다. Cloud Diagnostics 심볼 업로드는 별도 인증이 없어 수행되지 않았으며 플레이어 빌드는 성공했다.
- 2026-10-01 **SDK 없는 GitHub 버전의 macOS Development Build 성공**. BuildReport errors=0, warnings=350. C# 컴파일 오류 없음. SDK 포함/미포함 두 경로 모두 빌드됐다. 에디터 로그의 기존 AI 기능 entitlement/license 오류는 플레이어 빌드 성공을 막지 않았다.

## 맵 통합과 보존

- 팀원 FunIsland 원본은 별도 Unity 6000.3.25f1 프로젝트다. 에셋을 기존 GCCD 6000.3.24f1 프로젝트에 가져오고 통합 씬을 새로 만들었다.
- 제공 씬에 활성 플레이어·카메라가 2세트였으므로 통합 씬에서 1세트만 활성화했다. 기존 지형·구조물·collider는 유지했다.
- 가져온 이동 코드를 Input System에 맞게 변경하고 CharacterController.minMoveDistance=0으로 설정하여 높은 프레임률에서 중력/접지 움직임이 생략되지 않게 했다.
- 원본 FunIsland 프로젝트의 작업 전후 해시가 일치했다. 기존 GCCD 코드·SDK·기존 씬도 일치했다. 로컬 ProjectSettings는 SDK 정의 및 Unity 저장에 따라 변경되고 EditorBuildSettings에는 통합 씬이 추가됐다.
- GitHub checkout에서 Unity가 자동 갱신한 URP 직렬화 값은 제출 대상에서 제외한다. 패키지 버전 변경은 없다.

## 실제 장치에서 남은 검증

실제 X40 착용 검증은 수행하지 않았다. 확인이 필요한 항목은 40모터 실제 위치/인덱스, 연속 방향과 높이의 체감, 거리 강도 곡선, Bluetooth 지연, 연결 해제 시 체감 중지, macOS 앱의 실제 키보드·마우스 조작이다. 현재 로컬 SDK 설정의 배포 버전은 -1이며 이벤트는 0개였다. Designer 방향 이벤트를 사용하지 않는 직접 모터 출력 경로이지만 공식 SDK 초기화/계정 설정과 실제 장치 재생은 별도 확인해야 한다.

원통 각도와 균일 행 간격은 보간용 수학 모델이다. Gaussian/극점 출력의 분산이 체감 강도를 낮출 수 있다. 정수 출력 및 5–30Hz 갱신에 따른 단계·지연이 남는다. 화면 밖 판정은 타깃 중심점의 viewport 검사이며 가림 판정은 포함하지 않는다.

[사용 설명서](CONTINUOUS_HAPTICS.md)의 교정 및 실험 절차와 [SDK 조사](SDK_AUDIT.md)를 함께 참고한다. 이전 4방향 프로토타입의 기록은 [LEGACY_VALIDATION.md](LEGACY_VALIDATION.md)에 보존했다.
