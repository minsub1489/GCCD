# GCCD — 최소 방향성 햅틱 프로토타입

경희대학교 게임콘텐츠캡스톤디자인 연구 준비용 Unity 프로젝트입니다. 단일 Threat의 화면 밖 여부와 플레이어 기준 4방향을 계산하여 햅틱 서비스에 전달합니다. 기존 ThreatLab의 자동 실험, 통계 도구, UDP 출력 코드는 제거했습니다. 과거 버전은 Git 이력에 남아 있습니다.

## 실행

1. Unity Hub에서 `GCCD_Project`를 **6000.3.24f1**로 엽니다.
2. `Assets/_Project/Scenes/ThreatDirectionTest.unity`를 엽니다.
3. Play 후 Game 화면을 클릭합니다. WASD 이동, 마우스 시점 변경, Esc 커서 해제입니다.
4. 숫자 **1=앞, 2=오른쪽, 3=뒤, 4=왼쪽**에 Capsule을 생성합니다. 기존 Threat는 교체됩니다. Backspace/Delete로 제거합니다.
5. `Window > General > Console`에서 방향·각도·화면 밖 여부·강도를 확인합니다. 기본 출력은 **Debug Only**이며 실제 진동을 발생시키지 않습니다.

URP와 Input System을 사용합니다. 무기, AI, 참가자 관리, 자동 trial, CSV, Audio Only/Audio+Haptic 비교 조건은 아직 구현하지 않았습니다.

## Inspector

| Hierarchy / 컴포넌트 | 설정 |
|---|---|
| Player / First Person Controller | Move Speed, Mouse Sensitivity, Pitch Limit |
| ThreatSystem / Threat Spawner | Distance, Height Offset, Enable Debug Keys, Test Direction |
| ThreatSystem / Threat Sensor | Front Half Angle, Back Half Angle, Stable Seconds, Fixed Intensity, Log Debug |
| Haptics / Haptic Cue Service | Output, Cues Enabled, Minimum Interval |
| Haptics / Debug Haptic Output | Log Cues |
| Haptics / Bhaptics Haptic Output | 4개 Event ID, Maximum Intensity, Log Requests |

기본 앞/뒤 영역은 각각 ±45°입니다. 나머지를 좌/우로 분류합니다. 각도는 수평 플레이어 heading 기준이며 카메라 pitch는 방향 분류에 영향을 주지 않습니다. 화면 판정은 **Threat 중심점**의 viewport 좌표와 near/far clip 기준입니다. 가림(occlusion) 또는 전체 Capsule 경계 판정은 하지 않습니다.

화면 밖 진입, Threat 교체 또는 방향 변경 후 0.1초 안정화되면 한 번 요청합니다. 최소 요청 간격은 0.5초입니다. 계속 같은 방향에 있다는 이유로 반복 진동하지 않습니다. 화면 안으로 들어오거나 Threat가 제거되면 진행 중 요청을 정지합니다. 기본 강도는 0.5이며 거리에 따른 변화는 없습니다.

## 실제 TactSuit X40 연결

설치된 **bHaptics Haptic Plugin 2.8.1 (SDK2)**의 `BhapticsLibrary` 소스를 확인하여 `PlayParam`, `StopInt`, 연결 확인 API를 사용했습니다. SDK와 계정 설정은 저장소에 포함하지 않습니다. SDK 없이도 기본 Debug 모드로 컴파일됩니다.

1. `Window > Package Management > Package Manager > My Assets`에서 bHaptics Haptic Plugin을 가져옵니다. 이 개발 환경에는 이미 설치되어 있습니다.
2. bHaptics Designer의 프로젝트 Haptic App에 짧은 **앞/오른쪽/뒤/왼쪽** 패턴을 각각 연결하고 배포합니다. 실제 X40의 착용자 기준 좌우를 확인합니다. 네 이벤트의 길이와 강도는 같게 하고 위치만 바꿉니다. 이벤트 이름 자체가 방향을 보장하지는 않습니다.
3. `bHaptics > Developer Window`에서 App ID/API Key를 연결하고 배포된 이벤트를 동기화합니다. 키를 채팅 또는 GitHub에 올리지 않습니다.
4. SDK에 포함된 `[bhaptics]` prefab을 Hierarchy에 넣습니다. SDK의 초기화는 이 공식 prefab이 담당합니다.
5. `Edit > Project Settings > Player > Other Settings > Script Compilation > Scripting Define Symbols`의 Standalone 설정에 `GCCD_BHAPTICS_SDK2`를 추가하고 Apply 합니다.
6. Hierarchy `Haptics`의 `Bhaptics Haptic Output`에 배포된 정확한 4개 Event ID를 입력합니다. `Haptic Cue Service > Output`을 같은 오브젝트의 `Bhaptics Haptic Output`으로 바꿉니다.
7. bHaptics Player에서 X40 연결을 확인한 뒤 Play합니다. `[bHaptics] ... requested`는 SDK의 요청 접수이며 실제 착용 감각 검증을 대신하지 않습니다.

현재 확인된 로컬 설정은 App ID/Key 입력, 배포 버전 -1, 이벤트 0개입니다. 따라서 **네 방향 실제 진동 검증은 미완료**입니다. 기본 Scene에는 SDK prefab 참조를 저장하지 않아 SDK를 설치하지 않은 checkout에도 missing script가 생기지 않습니다.

## 구조

`Assets/_Project/Scripts` 아래 Player, Threat, Haptics, Core를 분리했습니다. 게임 로직은 `HapticCueService.PlayThreatCue(direction, intensity)`를 호출하며, SDK 참조는 `BhapticsHapticOutput` 하나에만 있습니다. `HapticOutputBase`를 교체하면 출력 방식을 바꿀 수 있습니다. `Editor/PrototypeSetup.cs`는 최초 Scene 생성 및 계산 검증용이며 기존 Scene을 덮어쓰지 않습니다.

macOS 빌드는 `File > Build Profiles`에서 macOS를 선택하고 Scene List에 `ThreatDirectionTest`만 포함하여 Build합니다. 실제 장비 출력은 macOS 앱에서도 별도 확인해야 합니다.

검증 결과와 남은 제한은 [docs/VALIDATION.md](docs/VALIDATION.md)를 확인하세요.
