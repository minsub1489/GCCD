# GCCD — 몸통 방향성 햅틱 경고 연구 환경

**동시 위협 수와 진동 강도에 따른 몸통 방향성 햅틱 경고의 효과와 비용**

화면 밖에 동시에 나타나는 정지 위협이 **1개 또는 3개**일 때, 몸통 진동의 강도 **없음/약/중/강**이 미처리 위협·반응·회전 비용·피해·작업부담에 어떤 관계를 보이는지 검토하는 Unity 실험 플랫폼이다. 참여자 내 2×4 설계, 조건 코드 `S0 SL SM SH M0 ML MM MH`를 사용한다. 모든 조건의 화면 방향 화살표와 공간 음향은 동일하게 유지한다. **최신 프로토콜은 소리의 위치를 현재 플레이어 카메라 기준으로 360° 연속 추적**하여 카메라를 돌리면 몸통의 진동 방향도 함께 움직이도록 설계되었다. 실제 모터 배열별 위치 정확도와 물리적 진동은 별도로 확인해야 한다.

## 프로젝트 상태

Unity **6000.3.24f1** 기반 신규 연구 환경. 실험 제어·로그 기록·운영 UI·Mock 출력·Python 분석기를 구축했고, 360° 청자 상대 방향 추적 및 **산업 시설형 잠입 FPS 분위기**의 그래픽 개편을 적용했다. 장면에는 산업 시설 복도, 보안문, 배관·기둥·상자, 비상 조명·안개, 1인칭 전술 총기 모델, 생성된 콘크리트 텍스처가 포함된다. **사진처럼 사실적인 고해상도 외부 아트 에셋 적용을 의미하지 않는다.** Demo에서는 물리 진동이 발생하지 않는다. 공식 bHaptics Unity SDK2는 개발자의 Unity Asset Store 캐시에서 **로컬로 가져와 컴파일 검증**했지만, 실제 장치별 모터 위치·지연·피부 출력은 미검증이다. Pilot/Main은 실기기 연결과 교정이 충족되어야 진행할 수 있다. Demo 기능 검증과 합성 데이터는 실험 성과가 아니다.

**이전 버전의 개발 검증 기록:** Unity 에디터 C# 스모크 검사 통과, macOS Standalone Development 빌드 성공(`GCCD_STUDY_MAC_BUILD_SUCCESS`, `GCCD_Project/Builds/ThreatIntensityStudy.app`), Unity Demo 단일 위협 JSONL 분석(유효 위협 1·제외 0·파싱 경고 0), Python 합성 사례 테스트 통과. **360° 추적 및 새 FPS 장면 변경 후에는 별도의 빌드·플레이·로그 재검증이 필요하다.** 실기기 진동 출력과 인간 참가자 실험 검증은 완료되지 않았다.

### 360° 진동으로 인한 연구 프로토콜 변경

최초 구현은 출현 순간 앞왼쪽/앞오른쪽/뒤왼쪽/뒤오른쪽 네 방향 중 하나에 짧게 진동을 배정해 유지했다. 새 프로토콜은 **세계 좌표에 있는 각 소리·위협의 상대 방위각을 지속 갱신**해 몸을 돌릴 때 진동이 발생하는 방향을 변하게 한다. 초기 네 구역은 표적 생성 균형을 위한 기준이며, 진동이 계속 해당 구역에 머무르지 않는다. **기존 고정 경고 기록과 신규 연속 경고 기록은 동일한 자극이라고 볼 수 없다.** 새 버전의 강도·방향 교정, 신호 갱신 주기·모터 활성 비율·중첩 부하 측정, 별도 파일럿을 실시해야 한다. 자세한 내용은 [연구 프로토콜](docs/EXPERIMENT.md)을 참조한다.

## Unity 실행

1. Unity Hub에서 `GCCD_Project/` 폴더를 **Unity 6000.3.24f1**로 연다.
2. 필요한 경우 Unity 메뉴 **GCCD → Generate Threat Intensity Study Scene**으로 연구 씬을 생성한다. 생성 경로는 `GCCD_Project/Assets/Scenes/ThreatIntensityStudy.unity`이다. 이미 씬이 준비되어 있다면 해당 씬을 열면 된다.
3. ▶ Play를 누르고 운영 화면에서 **Demo · 시뮬레이션**을 선택한다. 실명/학번/연락처 대신 익명 참여자 코드를 기입한다. 조건당 시행 수, 연습 수, 준비 jitter, 고정 시행 시간·위협 마감 시각을 연구 계획에 맞게 확인한다.
4. 교정 화면에서 360° 위치 추적과 실장치 연결·모터 배열 검증 상태를 확인하고 **Demo 시작**을 누른다. Demo에서는 물리적 교정과 진동 출력을 확인할 수 없다. 안내에 따라 연습 → 조건 블록 → 종료 설문 → 휴식을 진행한다.
5. 시행 중 마우스를 움직여 카메라를 회전하고, 화면 중앙으로 표적을 조준해 **마우스 왼쪽 버튼**으로 사격한다. **ESC**는 연구 세션 중단 명령이다. 위협은 마감 시간이 지나면 피해를 가하지만 게임오버는 없다.
6. 세션 화면에 표시되는 결과 폴더를 기록한다. Unity `Application.persistentDataPath/GCCDResearch/<session>/` 안에 `events.jsonl`, `session.json`, 설문을 마치면 `surveys.jsonl`이 저장된다.

실험 파라미터의 프로젝트 기본값은 편의를 위한 초기 설정일 뿐 연구 승인/권장 시행 수가 아니다. Main용 값은 별도 파일럿 및 분석계획 확정이 필요하다.

### 공식 bHaptics Unity SDK2 연동

연구용 자체 코드와 공식 SDK는 분리되어 있다. 원본 Unity Asset Store 플러그인은 라이선스와 바이너리 배포를 고려해 **공개 GitHub에 포함하지 않는다.** 새 컴퓨터에서는 다음 순서로 설정한다.

1. Unity Asset Store의 **bHaptics Haptic Plugin (SDK2)**을 `GCCD_Project`에 설치한다. 로컬 개발 맥에서는 기존 Asset Store 캐시에 있던 `bHaptics Haptic Plugin.unitypackage`를 가져와 확인했다. SDK2의 UI 구성 요소 때문에 `com.unity.ugui` **2.0.0**이 패키지 의존성에 포함되어 있다.
2. **bHaptics Player**를 실행하고 Developer Portal에서 햅틱 앱을 생성·배포한다. 에디터 메뉴 **bHaptics → Developer Window**에서 발급받은 App ID/API Key로 **LINK**한다. 키를 저장소나 설정 스냅샷에 넣지 않는다.
3. 에디터 메뉴 **GCCD → bHaptics SDK2 → Check Official SDK and Add Prefab**으로 현재 씬에 공식 `[bhaptics]` 초기화 프리팹을 추가한다. 모의 Demo 씬 파일은 플러그인 없이도 열리도록 기본적으로 이 프리팹을 포함하지 않는다.
4. **GCCD → bHaptics SDK2 → Enable Compile-Time Adapter**에서 SDK2 호출 코드의 컴파일을 활성화한다. `GCCD_BHAPTICS_SDK2`가 정의되어야 공식 `BhapticsLibrary`를 사용하는 코드가 컴파일된다. 동일 메뉴에서 다시 비활성화할 수 있다.
5. SDK의 공식 `PlayMotors(position, levels, durationMillis)`는 문서상 TactSuit Pro **32모터** 배열을 사용하며, **100ms 이상** 출력을 권장한다. 우리 프로젝트의 40채널 360° 가상 미리보기는 X40의 실제 모터 인덱스를 의미하지 않는다. SDK 이벤트를 방향 회전시키는 `PlayParam(..., angleX, ...)`도 공식 API이지만, 물리 진동에 사용하려면 대상 장치·패턴·회전 부호·강도를 실장치에서 검증해야 한다.

공식 [Unity SDK 설치 가이드](https://docs.bhaptics.com/sdk/unity/guide) · [BhapticsLibrary API](https://docs.bhaptics.com/sdk/unity/references/library). macOS 앱에 App Sandbox를 사용하는 경우 `com.apple.security.network.client` entitlement가 필요하다. **SDK 패키지의 컴파일 성공과 장치에서의 실제 출력 성공은 서로 다른 검증 단계**다. 현 단계에서 X40 Pilot/Main 잠금은 유지된다.

## 로그 분석

**Python 3.10 이상, 외부 패키지 불필요.** 프로젝트 최상위 디렉터리에서:

```bash
python3 -m unittest discover -s analysis/tests -v

# 제공된 합성 Demo 로그의 정상 처리 확인
python3 analysis/replay.py analysis/examples/demo_events.jsonl --out analysis_output/demo --mode Demo

# 실제 세션 예시. session.json은 events.jsonl과 같은 폴더라면 자동 감지됨
python3 analysis/replay.py /path/to/events.jsonl --out analysis_output/main --mode Main

# 연구 검증 이전의 Pilot은 별도 분석
python3 analysis/replay.py /path/to/events.jsonl --out analysis_output/pilot --mode Pilot
```

생성물은 `per_threat.csv`(위협별 실패·비응답·획득·사격), `per_trial.csv`(시행별 결과·회전·피해·연속 진동 가상 출력 샘플 통계), `by_condition.csv`(**프로토콜 버전**/참여자/조건별 기술통계), `contrasts.json`(`by_protocol`로 분리한 1차·2차 대비), `parse_warnings.json`(손상/미지원 이벤트), `provenance.json`(원본 SHA-256/설정 해시)이다. 시행 로그는 고치지 않고 출력 폴더에 파생 결과를 남긴다. 기본 분석 모드는 Main이며 `--mode Demo`로 데모를 분리한다. 모드가 없는 로그는 자동으로 Main으로 취급하지 않는다. 옛 고정 경고 및 새 연속 추적 로그는 합치지 않는다.

**주요 효과:** `(p(MH) - p(ML)) - (p(SH) - p(SL))` (위협별 마감 실패 확률의 차이의 차이). **보조 대비:** `p(MH) - p(M0)`. 두 대비 모두 유효한 조건 데이터가 있을 때만 계산하며 빈 조건은 `null`이다. 3위협 시행의 3개 결과는 같은 시행에 속하므로 서로 독립인 샘플로 통계 검정하지 않는다. 파이썬 출력은 **기술통계**이며 p값이나 인과효과를 주장하지 않는다.

## 파일 구성

| 경로 | 역할 |
| --- | --- |
| `GCCD_Project/Assets/Scripts/Core/` | 실험 상태·8조건·시행 제어·동시 표적·반응/피해 계산 |
| `GCCD_Project/Assets/Scripts/Haptics/` | 출력 인터페이스·Mock 및 검증 전 하드웨어 차단 구현 |
| `GCCD_Project/Assets/Scripts/Logging/` | append-only JSONL 이벤트·설정 snapshot·설문 저장 |
| `GCCD_Project/Assets/Scripts/GUI/` | 연구자/참가자 화면, 교정, 시행, 설문 |
| `GCCD_Project/Assets/Editor/` | 연구용 Unity 씬 생성과 에디터 검증 도구 |
| `analysis/replay.py` | JSONL → 위협/시행/조건 CSV 및 대비·기술적 제외 |
| `analysis/tests/` | 마감 경계·단일/동시 위협·손상 로그·Main 안전 등 회귀 테스트 |
| `analysis/examples/` | 합성 데모 기록 (인간 대상 실험 자료 아님) |
| `docs/EXPERIMENT.md` | 연구 설계, 교정·순서·평가·자료 해석·재현성 |
| `docs/DATA_SCHEMA.md` | Unity 기록 필드, 입력/출력, 분석 계산 계약 |

문서 먼저 읽기: [연구 설계와 운영 절차](docs/EXPERIMENT.md) · [이벤트 스키마/분석 규칙](docs/DATA_SCHEMA.md).

## 현재 남아 있는 검증

연구용 **실제 기기 모델과 모터 프로파일 확인**, 360° 회전 시 실출력 위치 정확도·지연·듀티사이클 및 지속 노출 안전, 음향/화살표 동등성, 잠입 FPS 씬에서 화면 밖 위협/엄폐/프레임 드롭, 전체 세션 로그 무결성, Main 잠금 및 오류 복구, 신규 파일럿, 설문 신뢰성, 분석계획/표본설계를 확인해야 한다. bHaptics Unity SDK2는 [Unity 플러그인·Player·배포된 App ID/API Key 및 초기화 프리팹](https://docs.bhaptics.com/sdk/unity/guide) 설정을 요구한다. [공식 모터 인덱스 표](https://docs.bhaptics.com/sdk/further/motor)는 TactSuit Pro를 **32모터**로 정의하며 X40의 40모터 배열을 검증해 주지 않는다. 모터 수나 순서를 유추해 실제 진동 성공으로 표시하면 안 된다. NASA 원문 TLX 소개: https://www.nasa.gov/human-systems-integration-division/nasa-task-load-index-tlx/ . 6개 평정값만 수집하면 **Raw(비가중) TLX**이며, 가중 NASA-TLX 산출에는 별도의 15개 차원쌍 비교가 필요하다.
