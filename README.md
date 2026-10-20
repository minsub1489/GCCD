# GCCD — 몸통 방향성 햅틱 경고 연구 환경

**동시 위협 수와 진동 강도에 따른 몸통 방향성 햅틱 경고의 효과와 비용**

화면 밖에 동시에 나타나는 정지 위협이 **1개 또는 3개**일 때, 방향을 알려 주는 몸통 진동의 강도 **없음/약/중/강**이 미처리 위협·반응·회전 비용·피해·작업부담에 어떤 관계를 보이는지 검토하는 Unity 실험 플랫폼이다. 참여자 내 2×4 설계, 조건 코드 `S0 SL SM SH M0 ML MM MH`를 사용한다. 모든 조건의 화면 방향 화살표와 공간 음향을 동일하게 유지하며, 몸통 방향은 경고 시각의 카메라 기준 네 구역(`FrontLeft`, `FrontRight`, `BackLeft`, `BackRight`)으로 고정된다.

## 프로젝트 상태

Unity **6000.3.24f1** 기반 신규 연구 환경. 기존 실험의 코드는 제거 대상으로 정리되었고, 현재 연구용 씬 생성기·실험 제어·로그 기록·기본 UI·Mock 출력·Python 분석기를 개발한 상태다. **Demo는 물리 진동을 발생시키지 않는다. 실제 bHaptics SDK 및 장치 매핑은 미연결/미검증이며, Pilot/Main은 실기기 검증과 교정이 충족되어야 실행 가능하다.** Demo로 얻은 기능 검증/합성 데이터는 실험 결과가 아니다. 실제 참가자 데이터 수집 및 연구 가설 검증은 아직 수행하지 않았다.

**확인된 개발 검증:** Unity 에디터 C# 스모크 검사 통과, macOS Standalone Development 빌드 성공(`GCCD_STUDY_MAC_BUILD_SUCCESS`, `GCCD_Project/Builds/ThreatIntensityStudy.app`), Unity가 생성한 Demo 단일 위협 JSONL의 분석기 입력 확인(유효 위협 1·제외 0·파싱 경고 0), Python 분석기 합성 사례 11개 테스트 통과. **실기기 진동 출력과 인간 참가자 실험 검증은 아직 완료되지 않았다.**

## Unity 실행

1. Unity Hub에서 `GCCD_Project/` 폴더를 **Unity 6000.3.24f1**로 연다.
2. 필요한 경우 Unity 메뉴 **GCCD → Generate Threat Intensity Study Scene**으로 연구 씬을 생성한다. 생성 경로는 `GCCD_Project/Assets/Scenes/ThreatIntensityStudy.unity`이다. 이미 씬이 준비되어 있다면 해당 씬을 열면 된다.
3. ▶ Play를 누르고 운영 화면에서 **Demo · 시뮬레이션**을 선택한다. 실명/학번/연락처 대신 익명 참여자 코드를 기입한다. 조건당 시행 수, 연습 수, 준비 jitter, 고정 시행 시간·위협 마감 시각을 연구 계획에 맞게 확인한다.
4. 구역별 교정 화면을 살펴보고 **Demo 시작**을 누른다. Demo에서는 물리적 교정과 진동 출력을 확인할 수 없다. 안내에 따라 연습 → 조건 블록 → 종료 설문 → 휴식을 진행한다.
5. 시행 중 마우스를 움직여 카메라를 회전하고, 화면 중앙으로 표적을 조준해 **마우스 왼쪽 버튼**으로 사격한다. **ESC**는 연구 세션 중단 명령이다. 위협은 마감 시간이 지나면 피해를 가하지만 게임오버는 없다.
6. 세션 화면에 표시되는 결과 폴더를 기록한다. Unity `Application.persistentDataPath/GCCDResearch/<session>/` 안에 `events.jsonl`, `session.json`, 설문을 마치면 `surveys.jsonl`이 저장된다.

실험 파라미터의 프로젝트 기본값은 편의를 위한 초기 설정일 뿐 연구 승인/권장 시행 수가 아니다. Main용 값은 별도 파일럿 및 분석계획 확정이 필요하다.

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

생성물은 `per_threat.csv`(위협별 실패·비응답·획득·사격), `per_trial.csv`(시행별 결과·회전·피해), `by_condition.csv`(참여자/조건별 기술통계), `contrasts.json`(1차·2차 대비), `parse_warnings.json`(손상/미지원 이벤트), `provenance.json`(원본 SHA-256/설정 해시)이다. 시행 로그는 고치지 않고 출력 폴더에 파생 결과를 남긴다. 기본 분석 모드는 Main이며 `--mode Demo`로 데모를 분리한다. 모드가 없는 로그는 자동으로 Main으로 취급하지 않는다.

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

연구용 실기기 연결과 모터 구역 매핑, 전 구역 개인 교정, 실제 조건 균형 및 음향·화살표 동등성, 런타임의 전체 세션 로그 무결성, Main 잠금 및 오류 복구, 독립 참여자 파일럿, 설문 신뢰성, 분석계획/표본설계는 실제 환경에서 확인해야 한다. bHaptics 공식 Unity 안내: https://docs.bhaptics.com/sdk/unity/guide . NASA 원문 TLX 소개: https://www.nasa.gov/human-systems-integration-division/nasa-task-load-index-tlx/ . 설문 여섯 차원만 실시하면 **Raw(비가중) TLX**이고, 가중 NASA-TLX 산출에는 별도의 15개 차원쌍 비교가 필요하다.
