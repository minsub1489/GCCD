# 기록 스키마 및 분석기 사용 계약

실제 Unity 런타임에서 출력할 JSONL 계약을 기준으로 작성했다. Unity 실행 중 생성된 진짜 출력과 아래 파서가 완전히 호환되는지는 **통합 테스트 전**까지 미검증이다.

## JSONL 한 줄의 필드

한 줄마다 UTF-8 JSON 객체. 기록 순서는 `sequence_number`로 확인한다. 아래 필드는 설계된 공통 필드다.

| 필드 | 의미 |
| --- | --- |
| `event_type` | `trial_start`, `spawn`, `warning`, `response_attempt`, `shot`, `first_acquired`, `neutralized`, `damage`, `camera_sample`, `trial_end` 등 |
| `session_id`, `trial_id`, `participant_id` | 세션·시행·익명 참여자 키. 세 필드의 조합으로 시행 구분 |
| `condition`, `threat_count`, `intensity_level` | 8개 조건 코드와 적용 자극 정보 |
| `trial_time` | **경고 시점을 0초**로 한 초 단위 시간; 준비구간은 음수 가능 |
| `session_time_monotonic`, `timestamp_utc` | 세션 단조시계(초), UTC 진단 타임스탬프. 표적 판정에는 동일한 trial 시계를 사용 |
| `sequence_number` | 이벤트 흐름 추적을 위한 정수 번호. 결손/역행 진단 |
| `target_id` | 대상 표적 ID. 표적이 없는 이벤트는 빈 값 가능 |
| `payload` | 이벤트별 추가 항목을 보유한 JSON 객체 |

런타임 이벤트 예시는 다음과 같다. 아래 5초 마감은 **합성 데이터 예시**이며 연구용 권장값이 아니다.

```json
{"event_type":"spawn","session_id":"demo-A","trial_id":"t1","participant_id":"P001","condition":"SL","threat_count":1,"intensity_level":"low","trial_time":0.0,"sequence_number":3,"target_id":"enemy1","payload":{"mode":"Demo","zone":"FrontLeft","deadline_time":5.0}}
{"event_type":"response_attempt","session_id":"demo-A","trial_id":"t1","participant_id":"P001","condition":"SL","trial_time":1.2,"sequence_number":4,"target_id":"enemy1","payload":{"attempted":true,"first_shot_target":"enemy1"}}
{"event_type":"neutralized","session_id":"demo-A","trial_id":"t1","participant_id":"P001","condition":"SL","trial_time":2.1,"sequence_number":5,"target_id":"enemy1","payload":{"neutralized":true}}
```

`payload`의 주요 키: `mode`(`Demo`/`Pilot`/`Main`), `practice`, `zone`, `block_index`, `trial_index`, `strength`, `event_source`, `attempted`, `acquired`, `neutralized`, `damage`, `deadline_time`, `first_shot_target`, `validity`, `reason`, `camera_yaw`, `camera_pitch`, `yaw_path_deg`, `turn_reversals`, `yaw_delta_deg`, `spawn_angle_deg`. `trial_start`와 `spawn`에 **실행 조건과 위협별 마감 정보가 반드시 기록**되어야 한다. Main 장치 확인이 가능하도록 `device_verified=true`와 `output_mode=real` 등 판정 가능한 증거를 세션 또는 시행 이벤트에 명시할 것을 권장한다. 현재 런타임과 이 증거 필드의 실제 연동은 별도 확인한다.

파서는 흔한 다른 필드명(`event`/`type`, `threat_id`, `timestamp_ms`, `camera_yaw_deg`)과 `payload` 객체·JSON 문자열을 허용하지만, 모든 변형을 의미 추론으로 받아들이지는 않는다. 미확인 종류는 `parse_warnings.json`에 보존한다. `hit`과 `attack`만으로 `neutralized`/`damage`를 추측하지 않는다. `deadline_time`은 경고 시점 기준의 절대 trial 시각으로 읽는다. 동일하게 기록된 `trial_time`과 비교해 성공 여부를 판정한다.

## 실행

프로젝트 최상위에서 Python **3.10 이상**:

```bash
python3 -m unittest discover -s analysis/tests -v
python3 analysis/replay.py analysis/examples/demo_events.jsonl --out analysis_output/demo --mode Demo
python3 analysis/replay.py /path/to/events.jsonl --session-json /path/to/session.json --out analysis_output/main --mode Main
```

인수 `--mode` 기본값은 `Main`; `Demo`, `Pilot`, `Main` 각각 따로 집계. `--mode all`은 확인용으로 모두 출력할 수 있지만 `by_condition.csv`/대비 계산도 모드별로 분리한다. `--session-json`을 생략하면 입력 로그 옆 `session.json`을 찾아 해시와 config hash를 기록한다. `session.json`을 의도적으로 지정했는데 없거나 읽을 수 없다면 분석 결과를 기술적으로 제외한다.

출력 파일:

| 파일 | 내용 |
| --- | --- |
| `per_threat.csv` | 표적별 위협 구역·출현·마감·획득·중화 시각, 실패, 비응답, 발사, 근접 빗나감 추정 횟수, 기술적 제외 사유 |
| `per_trial.csv` | 시행별 유효 위협 수/실패율, 첫 발사 대상·귀속 방식/획득, 사전 획득 회전량·방향 반전, 피해, seed·session snapshot·config hash·제외 사유 |
| `by_condition.csv` | 모드/조건/참여자별 및 `ALL` 전체 위협 분모의 기술 통계 |
| `contrasts.json` | 조건별 실패 비율, 1차 `MH-ML-SH+SL`, 2차 `MH-M0`, 대응 참여자 수/대응 평균, 해석 경고 |
| `parse_warnings.json` | 손상된/지원하지 않는 이벤트 및 필수 ID 누락 위치 |
| `provenance.json` | 입력과 세션 설정 파일 SHA-256, 설정 해시, 분석 모드, 계산 기준 |

출력되는 빈 칸은 결측값(`null`)이며 성과 0이 아니다. 예를 들어 마감 정보가 없는 위협은 실패로 추정하지 않고 `missing_deadline`으로 기술적 제외한다. 전체 로그에 파손된 행이 있으면 해당 행의 시행을 특정할 수 없으므로 보수적으로 파일 안 모든 시행을 제외한다. 분석 전에 오류 원인을 고치고 새 로그를 획득하는 것이 올바르다.

## 계산 제약

`per_threat.failure`은 기술적 제외 시행에서 비어 있다. 유효 시행일 때 마감보다 일찍 중화된 표적만 `0`; 마감 동시/이후 및 미중화는 `1`. `per_threat.no_response`는 위협에 대한 시도 기록이 없으면 `1`. `first_acquisition_latency_s`는 획득 이벤트가 없다면 비어 있다. `per_trial.camera_path_deg`는 경고~최초 표적 획득(미획득이면 종료) 구간의 원시 yaw 샘플에서 계산한다. 1개 미만 구간 차이를 정확한 경로로 추정하지 않으며 `null` 반환한다. 런타임에서 기록한 `yaw_path_deg`/`turn_reversals`는 *집계 구간을 단정할 수 없어서* 별도 `reported_*` 칼럼으로 유지한다.

런타임에서 직접 맞힌 표적은 `attribution_method=direct_collider`, 가까이 빗나간 발사는 `angle_proximity_inference`로 구분한다. 후자의 `response_attempt`/`inferred_near_miss_shots`는 **조준 광선과 가장 가까운 유효 표적의 각도 차이가 설정된 임계값 이내라는 휴리스틱**에 따른 시도 추정이다. 참가자가 그 표적을 의도했다는 직접적인 증거가 아니다. 기본 임계값은 연구에서 아직 타당화되지 않았으며 세션 snapshot의 `near_miss_inference_angle_degrees`를 보존한다. 고정된 임계값의 민감도 분석을 별도로 계획해야 한다. 최초 발사 귀속 근거는 `per_trial.first_shot_attribution`으로 제공한다.

현재 결과는 독립 표본 가정, 혼합효과 모형, 통계적 신뢰구간/유의성 검정을 포함하지 않는다. 실제 참여자 집단에 대한 결론은 별도 분석계획·계층 구조·결측 및 사전 제외 기준 검토 후 내려야 한다. 특히 3위협 시행의 3개 결과를 3명의 독립 참여자로 해석하면 안 된다.
