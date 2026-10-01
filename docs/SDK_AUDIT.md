# bHaptics / Unity 조사 기록

조사일: 2026-09-28. 대상: `GCCD_Project`.

## 설치된 코드가 기준

- Unity: **6000.3.24f1**, revision `4e7b9b5b6244` (`ProjectSettings/ProjectVersion.txt`). 같은 버전의 Apple Silicon 에디터가 설치되어 있음.
- bHaptics: **Haptic Plugin 2.8.1**. 로컬 Asset Store `.unitypackage` 헤더의 version `2.8.1`, upload `982196`, version ID `1459870` 확인. 설치된 `BhapticsLibrary.cs`, `BhapticsHelpers.cs`, `BhapticsSDK2.cs`가 이 패키지의 파일과 바이트 단위로 일치함.
- UPM 패키지가 아니라 `Assets/Bhaptics/SDK2`에 설치됨. namespace/assembly: `Bhaptics.SDK2`.
- Input System 1.20.0, Test Framework 1.6.0, URP 17.3.0, Pipeline 0.7.0-exp.1.
- 기존 기능: `GCCD.Minimal.ThreatSensor`는 수평 투영 후 Front/Right/Back/Left로 분류하고, `BhapticsHapticOutput`은 Designer 이벤트를 `PlayParam`으로 재생함. 새 모듈은 이 경로와 별도이다.

## 실제 확인한 API

`Assets/Bhaptics/SDK2/Scripts/Core/Plugins/BhapticsLibrary.cs`:

| API | 사용 / 판단 |
|---|---|
| `int PlayMotors(int position, int[] motors, int durationMillis)` | 선택. 이벤트 등록 없이 채널별 세기를 직접 전달. 네이티브 `playDot`에 배열 길이도 전달함. |
| `int PlayPath(int position, float[] xValues, float[] yValues, int[] intensityValues, int duration)` | 조사함. 주변 모터 보간을 SDK 내부에서 수행하므로 연구용 보간 방식·가중치 합·극점 처리를 직접 통제하기 어려워 채택하지 않음. X40에서 작동하지 않는다고 단정하지 않음. |
| `bool IsBhapticsAvailableForce(bool isAutoRunPlayer)` | 재연결 가능 여부 갱신. false를 전달하여 Player를 자동 실행하지 않음. |
| `bool IsConnect(PositionType type)` | 조끼 연결 검사. |
| `List<HapticDevice> GetDevices()` | 연결·페어링·장치 이름·Position 확인. |
| `bool StopInt(int requestId)` | 이 모듈이 소유한 요청만 중지. |
| `StopAll()` | 다른 기능의 햅틱을 중지할 수 있으므로 사용하지 않음. |
| `PlaySingleMotor(...)` | 설치 버전은 Vest 배열을 32개로 생성하므로 X40 경로에서 사용하지 않음. |

`BhapticsHelpers.cs`의 `PositionType.Vest`는 **0**. X40 전용 enum이나 신뢰할 수 있는 모델 ID/모터 수 필드는 없다. 이름에 X40이 있거나, 사용자가 실제 X40임을 확인한 이름 변경 장치만 허용한다. 이 이름 확인은 하드웨어 모델 인증 기능이 아니다. 해당 overload는 장치 주소를 지정할 수 없으므로 연결된 조끼가 정확히 하나일 때만 출력한다.

패턴 파일 / 이벤트 ID는 불필요하지만 설치 SDK의 `BhapticsSDK2.Awake()`는 AppId 설정과 기존 SDK 초기화를 요구한다. 따라서 프로젝트의 bHaptics 설정은 계속 사용한다. 선택적 BhapticsX40SdkBackend가 기존 공식 초기화 컴포넌트가 없을 때 생성하므로, 새 씬에는 SDK 프리팹 참조가 필요 없다. 코어 어셈블리는 SDK를 참조하지 않으며 GCCD_BHAPTICS_SDK2 정의가 켜진 로컬 프로젝트에서만 직접 SDK 호출을 컴파일한다. 인증정보를 새 코드나 보고서에 복사하지 않았다.

## X40 인덱스 근거와 좌표 모델

공식 자료:

- [X40 공식 매뉴얼](https://www.bhaptics.com/docs/manuals/tactsuit-x-manual-en.pdf): 앞 20개 + 뒤 20개.
- [공식 구형 Unity 시각화 프리팹](https://github.com/bhaptics/haptic-library/blob/ab77d37b35d66314073905e00f40e14dfb1806b6/samples/unity-plugin/Assets/Bhaptics/SDK/Prefabs/%5BbHapticsVisualizer%5D.prefab) 및 [VisualFeedback.cs](https://github.com/bhaptics/haptic-library/blob/ab77d37b35d66314073905e00f40e14dfb1806b6/samples/unity-plugin/Assets/Bhaptics/SDK/Scripts/VisualFeedback.cs): 바이너리 프리팹의 실제 child 순서와 RectTransform 좌표를 읽었다. VisualFeedback는 child 순서를 모터 인덱스로 사용한다.
- [공식 X40 OSC 프리팹](https://github.com/bhaptics/VRChatOSC/blob/main/Unity/Assets/bHapticsOSC/VRChat/Prefabs/Without%20Mesh/Vest_old.prefab): `bOSC_v1_VestFront_N` / `VestBack_N`의 위치와 교차 확인.
- [공식 direct API 40채널 테스트](https://github.com/bhaptics/tact-python/blob/main/python_test.py): Vest position 0에 길이 40 배열을 전달하는 예제.
- [현행 API 문서](https://docs.bhaptics.com/sdk/unity/references/library), [현행 모터 문서](https://docs.bhaptics.com/sdk/further/motor): 신형 Pro의 32개 배치를 X40의 40개 배치와 혼동하지 말 것.

구형 공식 시각화에서 front 로컬 인덱스 0,1,2,3의 화면 x는 약 163,132,88,57로 감소하고, back 0,1,2,3은 64.5,95.5,126.5,157.5로 증가한다. 각 면은 위에서 아래로 5행이다. 즉 정면을 외부에서 볼 때는 좌우가 반전되어 보인다. 구현의 `Position.x`는 **착용자 자신의 왼쪽=0, 오른쪽=1**이다.

X40 출력 배열은 front 0–19 / back 20–39로 관리한다. 각 면의 row-major 인덱스와 몸 둘레 모델은 `X40MotorLayout` 한 파일에만 정의했다. 외부 자료로 확인한 배치와 별개로, 현재 Player/펌웨어 조합의 실제 40채널 재생 및 촉각 위치는 장치 교정 테스트로 확인해야 한다.

| 위에서부터 행 | 앞, 착용자 왼쪽→오른쪽 | 뒤, 착용자 왼쪽→오른쪽 | normalized Y |
|---|---|---|---|
| 1 | 0 1 2 3 | 20 21 22 23 | 1 |
| 2 | 4 5 6 7 | 24 25 26 27 | .75 |
| 3 | 8 9 10 11 | 28 29 30 31 | .5 |
| 4 | 12 13 14 15 | 32 33 34 35 | .25 |
| 5 | 16 17 18 19 | 36 37 38 39 | 0 |

각 행을 둘러싸는 **수학적** 원통 모델은 22.5°, 67.5°, 112.5°, 157.5°, 202.5°, 247.5°, 292.5°, 337.5°에 8열을 배치한다. 위쪽 행의 순서는 `[2,3,23,22,21,20,0,1]`. 이 각도는 공식 인체 실측값이 아니라 연속 보간을 위한 모델이다. 사용자의 8방향 분류와 달리 입력을 이 각도로 반올림하지 않는다. Nearest 비교 조건에서만 가장 가까운 모터를 선택한다.

## SDK 자체의 주의점

- SDK의 에디터 실행 경로는 Universal(TactHub)와 네이티브 Player에 모두 요청할 수 있다. 본 구현은 로컬 Player와 하나의 X40을 기준으로 하며, 별도 TactHub 중복 연결은 검증하지 않았다.
- SDK singleton 코드의 초기화/종료 구현은 원본 그대로 보존했다.
- 원격 연결이 끊긴 순간의 물리적 중지를 소프트웨어가 보장할 수는 없다. 출력은 100–220ms 유효기간을 갖고, 정상 연결 시 소유한 요청을 즉시 StopInt한다. 중지 실패 시 이전 펄스 유효기간까지 다음 출력을 대기시킨다.
