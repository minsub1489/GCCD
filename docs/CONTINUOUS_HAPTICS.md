# TactSuit X40 연속 3D 방향·거리 햅틱 시스템

대상 프로젝트: `GCCD_Project` / Unity 6000.3.24f1 / bHaptics SDK 2.8.1.

## 1. 생성한 파일

프로젝트 내 공통 경로 `Assets/_Project/ContinuousHaptics/`:

- `Runtime/HapticDirectionCalculator.cs`
- `Runtime/DistanceIntensityMapper.cs`
- `Runtime/X40MotorLayout.cs`
- `Runtime/HapticSpatialMapper.cs`
- `Runtime/BHapticsVestOutput.cs`
- `Runtime/IX40SdkBackend.cs`
- `Runtime/HapticThreatOrbitDemo.cs`
- `Runtime/HapticThreatController.cs`
- `Runtime/HapticCsvRecorder.cs`
- `Editor/HapticThreatControllerEditor.cs`
- `Editor/BHapticsVestOutputEditor.cs`
- `Tests/HapticMappingTests.cs`
- `PlayModeTests/HapticLifecycleTests.cs`
- `PlayModeTests/FunIslandIntegrationTests.cs`
- 위 네 폴더의 assembly definition 4개와 Unity 생성 `.meta` 파일.

연결 및 씬:

- `Assets/_Project/Scripts/Haptics/ContinuousThreatSpawnerBridge.cs`
- `Assets/_Project/Editor/ContinuousHapticsSetup.cs`
- `Assets/_Project/Scenes/ContinuousHapticsTest.unity`
- `Assets/_Project/Scripts/Haptics/BhapticsX40SdkBackend.cs`
- `Assets/_Project/Editor/FunIslandHapticsSetup.cs`
- `Assets/_Project/Scenes/FunIslandHaptics.unity`
- `Assets/FunIsland/`: 팀원 맵, URP 변환 재질, Input System 이동 코드 및 미리보기

## 2. 수정한 기존 파일

기존 소스, SDK, `ThreatDirectionTest.unity`는 보존했다. 기존 씬을 복제한 **새 씬**에서만 기존 ThreatSensor, HapticCueService, HapticOutputBase 계열의 활성화를 해제하여 중복 출력을 방지했다. 패키지 추가·업그레이드는 하지 않았다. 팀원의 별도 FunIsland 프로젝트는 수정하지 않았다. 가져온 FirstPersonController는 Input System 입력으로 교체하고 접지 최소 이동 거리를 0으로 설정했다. 새 통합 씬에서 중복 플레이어 한 세트를 비활성화하고 재질을 URP로 변환했다. EditorBuildSettings에 통합 씬을 추가했다.

## 3. 각 스크립트 역할

| 스크립트 | 역할 |
|---|---|
| HapticDirectionCalculator | 플레이어 회전 기준 local direction, azimuth/elevation, 실제 거리. 원점 중첩·비정상 수치 거부. |
| DistanceIntensityMapper | 연속 거리 곡선과 비교용 Discrete3Level 조건. |
| X40MotorLayout | 앞뒤 40개 인덱스, 정규화 패널 좌표, 원형 열 배치의 단일 정의. |
| HapticSpatialMapper | Nearest / 원형 bilinear / Gaussian, 극점 분산, 합이 1인 가중치, 세기 예산 내 정수화. |
| HapticVestOutput / BHapticsVestOutput | 출력 교체 가능한 추상 경계 / SDK 선택적 연결, 소유 요청 중지. |
| IX40SdkBackend / BhapticsX40SdkBackend | SDK 없는 코어와 설치된 SDK2의 직접 API 호출을 연결. |
| HapticThreatOrbitDemo / FunIslandHapticsSetup | 맵 위협 이동, URP 통합 씬 생성·검증·빌드. |
| HapticThreatController | 타깃·수동 입력, 주기 갱신, 스무딩, 화면 밖 필터, 상태·가중치 공개. |
| HapticCsvRecorder | CSV + 시작 설정 JSON, 각 프레임 설정·계산값·40개 출력·가중치 기록. |
| ControllerEditor / OutputEditor | 테스트 슬라이더, 곡선 프리셋, 모터 미리보기, Scene 각도, 1회 모터 교정. |
| ContinuousThreatSpawnerBridge | 기존 생성기의 CurrentThreat를 컨트롤러에 연결하는 선택적 어댑터. |
| ContinuousHapticsSetup | 기존 씬을 복제하여 새 시스템 배치, 씬 참조 검사, 연구용 macOS 빌드. |

## 4. 사용한 bHaptics API

`Bhaptics.SDK2.BhapticsLibrary.PlayMotors((int)PositionType.Vest, int[40], durationMillis)`를 사용한다. 출력 정수 범위는 0–100, 0은 꺼짐이다. `IsBhapticsAvailableForce(false)`, `IsConnect(PositionType.Vest)`, `GetDevices()`로 연결을 확인하고 `StopInt(requestId)`로 이 시스템의 요청만 중지한다.

기본 10Hz에서 120ms 펄스를 전송하며 다음 출력 전에 이전 요청을 중지한다. 5–30Hz 범위에서 펄스는 100–220ms이다. 앱이 멈춰도 무기한 재생 요청이 남지 않는다. 중지 API가 실패한 경우에는 이전 펄스 유효기간까지 다음 출력을 대기한다.

## 5. API 선택 이유

현재 설치 소스의 PlayPath도 조사했으나, 연구 실험에서 bilinear/Gaussian 비교, 모터별 가중치, 세기 합과 극점 처리를 직접 통제하려고 PlayMotors를 선택했다. Designer 방향 패턴이나 이벤트 등록은 필요 없다. 설치 SDK의 AppId/API 설정은 필요하다. `GCCD_BHAPTICS_SDK2` 정의가 켜져 있으면 어댑터가 공식 BhapticsSDK2 초기화 컴포넌트를 필요할 때 생성한다. 씬에 SDK 프리팹을 저장할 필요는 없다. SDK 자체와 인증 설정은 GitHub에 포함하지 않는다.

상세 근거: 같은 폴더의 `SDK_AUDIT.md`.

## 6. 360° 방향 매핑

```
relative = player.InverseTransformDirection(target.position - player.position)
azimuth = Repeat(atan2(relative.x, relative.z) * Rad2Deg, 360)
Vest X = azimuth / 360
```

0° 정면, 90° 착용자 오른쪽, 180° 뒤, 270° 왼쪽이다. 입력을 8방향 클래스로 변환하지 않는다. 각 행의 앞뒤 모터 8개를 원형 이웃으로 연결하며 정면 0° 경계와 양옆 90°/270° 경계도 같은 보간식으로 연결한다. 37°,112°,183° 등 임의의 입력을 그대로 처리한다.

플레이어 Transform의 pitch/roll도 반영한다. VR에서 몸통 방향을 전달하려면 HMD 카메라 대신 몸통 방향을 나타내는 Transform을 Player로 사용한다. smoothing을 끄면 다음 햅틱 tick에서 새 플레이어 방향을 그대로 반영하며, 켜면 설정 속도로 따라간다.

## 7. Elevation 매핑

```
elevation = atan2(relative.y, sqrt(relative.x² + relative.z²)) * Rad2Deg
Vest Y = Clamp01(ElevationCurve.Evaluate((elevation + 90) / 180))
```

Y=0 하단, .5 중앙, 1 상단. ElevationCurve를 변경하여 체감 높이 범위를 조정한다. 기본은 선형이다.

절댓값 80°부터 90°까지 SmoothStep으로 방위각 의존도를 줄여 위쪽/아래쪽 8개 모터 행에 분산한다. 정확히 위·아래에서는 azimuth가 달라도 같은 출력 분포다. 방위각 계산 자체도 수평 성분이 거의 0이면 이전 azimuth를 유지한다. 같은 위치에 타깃이 겹쳐 방향이 정의되지 않으면 중지한다.

## 8. 거리 → intensity 계산식

```
closeness = 1 - InverseLerp(NearDistance, FarDistance, worldDistance)
targetIntensity = Clamp01(IntensityCurve.Evaluate(closeness)) * MaximumIntensity
```

거리 ≥ FarDistance이면 곡선 값과 관계없이 0. 기본 curve는 선형. 기본값 Near=1.5m, Far=15m, Maximum=.5이다. Unity world unit 1을 1m로 해석한다.

기본 스무딩: `alpha = 1 - exp(-speed * deltaTime)`. azimuth는 LerpAngle, elevation/intensity는 Lerp를 사용한다. 스무딩 후 intensity ≤ MinimumPerceptibleIntensity(.05)이면 출력하지 않는다. 이 값은 모터별 최소 세기가 아니라 **전체 출력 세기의 dead zone**이다.

Discrete3Level에서는 closeness 기준 1/3, 2/3에서 Far/Mid/Near로 구분하고 .25/.5/1에 MaximumIntensity를 곱한다. 임계값·단계값 모두 조절 가능하다. 이 비교 조건에는 의도적인 단계 경계가 존재한다.

## 9. 모터 보간 방식

- **LinearInterpolation (기본)**: 인접한 원형 두 열 × 상하 두 행에 bilinear 가중치를 배분한다. 모터 경계에서도 값이 연속이다.
- **Gaussian**: 몸 둘레의 최단 각도 거리와 높이 거리로 Gaussian 가중치를 구하고 전체 합을 1로 정규화한다. Sigma 기본 .12.
- **Nearest**: 가장 가까운 모터 선택. 연속 보간 조건과 비교하는 실험용 옵션이다.

전체 intensity 예산을 각 모터에 배분한 뒤 largest-remainder 방식으로 0–100 정수화한다. 모터 값의 합은 `floor(intensity*100)`을 넘지 않는다. 모터 정수화에 따른 작은 출력 단계는 남는다.

## 10. Inspector 설정

새 씬의 **ContinuousHaptics** 선택 → **Haptic Threat Controller**:

| 설정 | 기본값 / 용도 |
|---|---|
| Player | FunIsland 플레이어의 가슴 높이 HapticTorsoOrigin |
| Target | FunIsland에서는 이동하는 붉은 구체 |
| Output | 같은 오브젝트의 BHapticsVestOutput |
| Enable Haptics | FunIsland 기본 꺼짐. 켜면 실제 전송 시도 |
| Distance Near / Far | 1.5 / 15 |
| Minimum Perceptible / Maximum | .05 / .5 |
| Mode / Intensity Curve | Continuous / Linear |
| Spatial Mode / Elevation Curve | LinearInterpolation / Linear |
| Gaussian Sigma / Pole Blend Start | .12 / 80° |
| Haptic Update Rate | 10Hz, 5–30Hz 허용 |
| Enable Smoothing | 켜짐 |
| Direction / Intensity Smoothing Speed | 15 / 10 (초당 지수 수렴 속도) |
| Off Screen Only | 새 씬에서는 켜짐. 테스트 모드에서는 무시 |
| View Camera | 활성 플레이어 카메라 자동 연결 |
| Test Mode / Enable Keyboard Test | 수동 좌표 사용 / 키보드 조작 허용 |
| Debug Mode | 40모터 미리보기, Scene 방향선·각도·수치 |

Intensity curve 프리셋: Linear, Quadratic, Ease-In/Out. AnimationCurve를 직접 편집하여 custom 조건 사용.

**Haptic CSV Recorder → Record**를 켜면 `Application.persistentDataPath/HapticResearch` 아래에 기록한다. Inspector의 Current File에 실제 파일 경로가 표시된다. `sent=1`은 SDK가 요청을 수락했다는 의미이며, 착용자가 진동을 느꼈다는 계측 결과가 아니다. `sent=0`은 미리보기, 무출력, 연결 실패 등을 포함하므로 status와 함께 해석한다.

## 11. Scene에서 붙일 위치

기본 맵은 `Assets/_Project/Scenes/FunIslandHaptics.unity`이다. **GCCD → FunIsland → Open Haptic Research Map** 메뉴로 연다. Play 후 Game 화면 클릭, WASD 이동, Shift 달리기, Space 점프, 마우스 시점, Escape 커서 해제. T로 붉은 테스트 위협의 이동을 멈추거나 재개한다.

맵은 팀원의 Unity 6000.3.25f1 프로젝트에서 에셋을 가져와 기존 6000.3.24f1 URP 프로젝트에 통합했다. 원본 프로젝트 버전을 변경하지 않았다. 별도의 `ContinuousHapticsTest.unity`와 **GCCD → Continuous Haptics → Create or Open Research Scene** 메뉴는 기존 숫자 키 생성기 비교 실험용이다.

다른 씬에 수동 설치할 때 빈 GameObject에 HapticThreatController, BHapticsVestOutput, 선택적으로 HapticCsvRecorder를 붙인다. SDK를 설치하고 활성화하면 공식 초기화 컴포넌트가 자동 생성된다. 기존 이벤트 출력과 동시에 재생하지 않도록 실험 조건을 선택한다.

## 12. Player / Enemy Transform 연결

- **Player**: 몸통 방향과 거리 원점을 나타내는 Transform. 필요하면 가슴 높이의 자식 Transform을 만들어 지정한다.
- **Target**: 추적할 Enemy 또는 위협 위치 Transform.
- 기존 생성기 연결 씬에서는 ContinuousThreatSpawnerBridge가 Target을 덮어쓴다. **임의 Enemy를 수동 지정하려면 Bridge 컴포넌트를 먼저 비활성화한 후 Target을 지정**한다.
- 기존 키 1/2/3/4는 생성기의 정면/오른쪽/뒤/왼쪽 빠른 생성 기능이다. 새 컨트롤러의 방향 분해능을 제한하지 않는다. 생성한 Threat를 Scene에서 자유롭게 움직여 임의 3D 위치를 테스트할 수 있다.

## 13. X40 연결 후 실제 테스트

1. SDK 2.8.1을 로컬 설치하고 **GCCD → Continuous Haptics → Enable Installed bHaptics SDK**를 실행한다. bHaptics Developer Window에서 App ID/API Key를 설정한다. Player를 실행하고 X40 한 대를 연결한다. Designer 방향 이벤트는 필요 없다.
2. 새 연구 씬을 열고 Play. Controller의 Enable Haptics를 끄고 Test Mode를 켜서 장치 없이 슬라이더·40모터 미리보기부터 확인한다.
3. 모델 이름이 X40으로 인식되지 않는 경우 Player에서 실제 모델을 확인한 뒤에만 Output의 Confirm Renamed Device Is X40을 사용한다. Pro/X16에 X40 배열을 강제로 보내는 용도가 아니다.
4. **HapticThreatController 컴포넌트 자체를 비활성화**한 상태에서 Output Inspector의 **Pulse selected motor**로 인덱스 0,3,16,19,20,23,36,39를 각각 확인한다. 각 클릭은 최대 10%, 100ms 한 번만 출력한다. 실제 앞뒤·좌우·상하가 안내와 일치하는지 확인한다.
5. Controller 컴포넌트를 다시 활성화하고 Enable Haptics를 켜고 Test Mode 슬라이더로 각도와 거리를 변경한다. 키보드를 사용하려면 Enable Keyboard Test를 켜고 Game View에 포커스를 둔다. 좌우 화살표=azimuth, 상하=elevation, Page Up/Down=거리, Escape=실제 출력 끄기.
6. 같은 높이 0°,45°,90°,135°,180°,225°,270°,315°와 중간각 22.5°,67.5°,112.5°,157.5°,202.5°,247.5°,292.5°,337.5°를 확인한다.
7. Elevation ±45°, 특히 azimuth 45°,135°,225°,315°의 복합 방향, 그리고 ±90°를 확인한다.
8. 기본 거리 1.5 / 4.875 / 8.25 / 11.625 / 15m를 차례로 확인한다. 보간과 거리를 먼저 분리해서 관찰하려면 smoothing을 잠시 끈다.
9. 359°↔1°, 89°↔91°, 269°↔271°를 천천히 왕복하여 경계 이동을 확인한다.
10. Test Mode를 끄고 실제 Threat를 움직인다. Player 회전, Target 삭제·비활성화, Controller 비활성화, 장치 연결 해제, 앱 포커스 상실 후 출력이 중지되는지 확인한다.
11. Record를 켜서 continuous/discrete, bilinear/Gaussian, 5/10/20/30Hz 조건을 기록한다.

자동 계산 검사 **249/249**, 맵 및 출력 수명주기 실행 검사 **9/9**가 통과했다. SDK 포함·미포함 macOS 빌드 모두 errors=0으로 성공했다.

자동 검증과 **실제 X40 착용 검증은 별개**다. 이번 작업에서 물리 장치의 진동 감각을 측정하지 않았다.

## 14. 알려진 제한사항

- 40개의 몸통 모터로 구면 방향을 부호화하는 방식이며, 공중의 실제 위치에서 촉각을 발생시키지는 않는다. 수학적 연속성이 지각적 연속성을 보장하지 않으므로 사용자 실험이 필요하다.
- 몸통 측면에는 별도 모터가 없으므로 앞뒤 바깥쪽 모터를 혼합한다. 원통 각도·균일 행 간격은 모델이며 착용자 체형별 교정 여지가 있다.
- 전체 가중치 정규화로 출력 예산은 일정하지만, 여러 모터에 분산될수록 개별 모터의 체감 강도는 약해질 수 있다. 특히 Gaussian과 극점 행에서 확인한다.
- 정수 0–100 출력, dead zone, 갱신 주기로 미세 단계와 지연이 존재한다. smoothing은 추가 지연을 만든다. Custom curve가 불연속이면 출력도 불연속일 수 있다.
- 네트워크/Bluetooth가 끊기면 즉시 물리 중지 명령을 전달할 수 없다. 연결 검사는 출력 tick마다 이루어지고 전송한 펄스는 최대 220ms 후 종료된다. 더 높은 갱신률의 체감·전송 특성은 장치 검증이 필요하다.
- X40 모델 ID를 조회하는 SDK API가 없어 이름/수동 확인을 사용한다. 실제 장치 인덱스는 교정 버튼으로 검증한다.
- 검증 빌드는 macOS이며 Windows/Android 빌드·기기 테스트는 별도이다. SDK의 공식 초기화 코드와 기존 플랫폼 설정은 유지했다.
- 기본 화면 밖 판정은 타깃 Transform 한 점의 viewport 검사다. 오브젝트 전체의 가시성, 벽에 의한 가림을 판단하지 않는다.
- 장치가 하나라도 없으면 출력하지 않고 상태를 표시한다. 연결 복구는 다음 tick에서 재확인한다.

## 15. 다중 적 확장

계산기와 공간 mapper는 특정 Enemy 선택 정책에 종속되지 않는다. 향후 `List<HapticThreat>`를 순회해 각 위협의 방향·거리·가중치 배열을 계산하고, 우선순위/시간 분할/가중합 규칙을 별도 Manager에 구현하면 된다. 합성 후 전체 출력 예산을 다시 제한하고 **하나의 BHapticsVestOutput**에 전달한다. 여러 컨트롤러가 같은 조끼에 각각 직접 전송하면 전체 세기 제한을 보장할 수 없으므로 피한다. 프레임 이벤트와 CSV 기록기는 합성 전·후 실험 기록으로 확장 가능하다.
