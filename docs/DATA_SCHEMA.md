# 기록 스키마 및 분석기 사용 계약

Unity의 실제 JSONL 출력 형식을 기준으로 정리한다. **2026-10-20 프로토콜 변경:** 실험 신호를 네 방향의 경고 시점 고정 펄스에서 **소리 원점의 세계 위치를 따르는 연속 360° 청자 상대 출력**으로 바꾸었다. 아래 기본 시행/위협 필드는 이전 버전부터 있었고, 추가 `haptic_update`, `haptic_tracking`, `haptic_stop` 이벤트는 새 런타임 구현에서 정의된 기록이다. **햅틱 프레임 전송과 파일 전체 재현의 수동 검증은 별도 확인**해야 한다. 실제 기기 전달 여부는 Mock 프레임으로 판단할 수 없다.

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

`payload`의 기본 키: `mode`(`Demo`/`Pilot`/`Main`), `practice`, `zone`, `block_index`, `trial_index`, `strength`, `event_source`, `attempted`, `acquired`, `neutralized`, `damage`, `deadline_time`, `first_shot_target`, `validity`, `reason`, `camera_yaw`, `camera_pitch`, `yaw_path_deg`, `turn_reversals`, `yaw_delta_deg`, `spawn_angle_deg`. `trial_start`와 `spawn`에 **실행 조건과 위협별 마감 정보가 반드시 기록**되어야 한다. Main 장치 확인에 `device_verified=true`, `output_mode=real` 같은 판정 가능한 증거가 필요하다. 이는 반드시 검증된 SDK/장치 상태를 반영해야 하며 bool 플래그를 로그에 썼다는 사실만으로 물리 출력이 확인되지는 않는다.

## 360° 연속 추적 이벤트의 의미와 노출량

이전 프로토콜의 `spawn.payload.zone`은 출현 순간의 초기 사분면이다. 새 프로토콜에서는 **그 값으로 시행 내 실제 출력 방향을 복원하면 안 된다.** 소리의 세계 좌표와 청자 위치, 카메라 yaw가 바뀌면 각 위협의 방위각이 갱신되기 때문이다. 방위각은 `atan2(source.x-listener.x, source.z-listener.z) - camera_yaw`로 계산하고 -180°와 +180° 경계를 원형으로 처리한다(0° 전면, +90° 우측, ±180° 후면). 실제 **40개 균등 각도 채널은 Demo용 합성 모터 프로파일**이며, 제조사가 검증한 X40 하드웨어 인덱스 정보가 아니다. 공식 [Motor Index](https://docs.bhaptics.com/sdk/further/motor)의 TactSuit Pro 프로파일은 32모터를 사용한다.

지속 출력으로 바뀌면서 추가 보존해야 할 정보는 다음과 같다.

| 항목 | 필요한 의미/단위 |
| --- | --- |
| `protocol_version`, `direction_mapping_mode`, `profile_id` | 출현시 고정 v1과 지속 추적 v2를 명확히 식별, 검증 여부를 구분한 모터 프로파일 ID |
| 시각 `trial_time`, 대상 `target_id` | 샘플이 발생한 경고 상대 초 및 출처 위협. 상태가 누락된 구간의 출력을 보간해 사실로 만들지 않기 |
| `source_world`, `listener_world`, `camera_yaw` 또는 실측 상대 방위각 | 샘플마다 360° 방향을 재현·검증할 위치 및 시점 정보 |
| 모터 출력 벡터·최대 출력·평활화·업데이트 주기 | 추정 40채널 Mock 프레임과 실제 인증된 배열·모터 상태를 별도 기록, 0~1 강도와 시간 기준 일치 |
| 겹친 위협 수, 활성 채널 수, 평균/최대 진폭 | 혼합·클리핑의 효과와 1개 대 3개 위협의 누적 부담 비교 |
| `haptic_request`/`haptic_result` 구분 | SDK **요청·승인·실제 물리 전달**을 구분, 실패/취소/지연 표시; Mock은 가상 신호로 유지 |
| 지속 시간, 요청 간격, duty fraction | `[전달된 것으로 검증된 활성시간 / 관측구간]`(실측)과 `[요청된 활성시간 / 관측구간]`(명령 수준 추정)을 구분해 표기 |

**현재 새 Unity 이벤트 계약 (Mock 모드):**

| `event_type` | 주요 `payload` 필드 | 뜻과 측정 범위 |
| --- | --- | --- |
| `haptic_request` | `output_status=stream_scheduled`, `motor_update_hz`, `strength` | 표적에 대한 스트림 계획. 기기 출력 발생 증거가 아님 |
| `haptic_result` | `output_status=mock_simulated_not_delivered`, `delivered=false` 또는 0조건의 `not_requested` | 최초 프레임 승인 응답. Mock은 피부 전달 없음 |
| `haptic_update` | `motor_levels_csv`(40개의 쉼표 구분 0~1 값), `motor_frame_number`, `motor_peak`, `motor_mean`, `active_sound_sources`, `motor_update_hz`, `delivered`, `output_status` | 런타임에서 약 1Hz로 추출한 **가상 40채널 출력 스냅샷**. 실제 스트림 제출은 별도 설정 주기(예: 20Hz) |
| `haptic_tracking` | `target_id`, `tracked_azimuth_degrees`, `camera_yaw`, `world_x/y/z`, `motor_frame_number`, `active_sound_sources` | 활성 목표별 약 1Hz 상대 방위각·소리 세계 원점 추적 |
| `haptic_stop` | `output_status=immediate_all_channel_zero` | 모든 활성 소리 원점의 위협이 종료될 때 채널 해제 추적 |

새 세션 `session.json`에서 `haptic_tracking_protocol=continuous_listener_relative_azimuth_v2`, `motor_update_hz`, `motor_spread_degrees`, `motor_attack_seconds`, `motor_release_seconds`, `motor_output_ceiling`, `motor_layout_status`와 `motor_profile`(실제 여부 명시)을 보존한다. **하드웨어 프로파일은 아직 검증되지 않았으며, `synthetic_40_equal_angles_unverified`는 가상 40채널의 9° 간격 미리보기**다. 기기별 모터 배열을 임의로 인증된 것처럼 표시해서는 안 된다. 새 추적 이벤트의 `active_sound_sources`는 위협과 연결된 **논리적 소리 원점의 활성 수**여야 한다. 0.22초짜리 음향 경고 파형 재생이 끝났다는 이유만으로 위협이 남아 있는데 연속 햅틱을 중단해서는 안 된다. 공간 음향 단서 자체는 모든 조건에서 동일하게 재생하며, 헤드셋·스피커·화살표 동등성 검사가 필요하다.

Python 분석기는 `haptic_update`의 CSV 벡터를 40개 수치인지 확인한다. 잘못된 CSV는 `invalid_motor_frame_snapshot`으로 해당 시행 제외한다. `per_trial.csv`에는 `haptic_sampled_frames`, `haptic_reported_max_frame_number`, `haptic_mean_sampled_peak`, `haptic_mean_sampled_amplitude`, `haptic_mean_sampled_motor_occupancy`(기록된 1Hz 스냅샷에서 진폭이 0.0005보다 큰 모터 비율의 **산술평균**), `haptic_max_sampled_active_motors`, `haptic_tracked_azimuth_samples`, `haptic_max_sampled_azimuth_step_deg`, `haptic_stop_events`, `haptic_measurement_scope`를 기록한다. 이 값들은 **실측된 출력 듀티사이클이나 누적 활성 시간·전체 프레임 누락률이 아니다**. 1Hz 간격에서는 갱신 사이의 출력을 복원할 수 없으므로 실제 누적 노출량·클리핑 시간·물리적 전달은 별도 고주파 로그나 검증된 하드웨어로 측정해야 한다.

신호 중첩 시 가상 매퍼는 `1−∏(1−vi)`로 진폭을 합성하고 설정된 상한으로 클리핑한다. 이는 실제 피부 진동량과 같다고 볼 수 없다. 위협 중화/마감 이후 출력은 0이어야 하며 중단/오류 시에도 즉시 해제되어야 한다. 연속 진동의 **총 켜진 시간과 클리핑 비율**이 강도별 비교를 혼동시키지 않는지 파일럿에서 확인한다. 장치/Player/API 키 인증, 프로파일 검증, 갱신 실패는 연구 결과의 **기술적 제외 근거**로 남긴다.

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
| `per_trial.csv` | 시행별 유효 위협 수/실패율, 첫 발사 대상·귀속 방식/획득, 가상 모터 샘플 요약, 사전 획득 회전량·방향 반전, 피해, seed·session snapshot·config hash·제외 사유 |
| `by_condition.csv` | `protocol_version`/모드/조건/참여자별 및 `ALL` 전체 위협 분모의 기술 통계 |
| `contrasts.json` | `by_protocol → [protocol_version] → [Demo/Pilot/Main]`별 실패 비율, 1차 `MH-ML-SH+SL`, 2차 `MH-M0`, 대응 참여자 평균/수, 해석 경고 |
| `parse_warnings.json` | 손상된/지원하지 않는 이벤트 및 필수 ID 누락 위치 |
| `provenance.json` | 입력과 세션 설정 파일 SHA-256, 설정 해시, 분석 모드, 계산 기준 |

출력되는 빈 칸은 결측값(`null`)이며 성과 0이 아니다. 예를 들어 마감 정보가 없는 위협은 실패로 추정하지 않고 `missing_deadline`으로 기술적 제외한다. 전체 로그에 파손된 행이 있으면 해당 행의 시행을 특정할 수 없으므로 보수적으로 파일 안 모든 시행을 제외한다. 분석 전에 오류 원인을 고치고 새 로그를 획득하는 것이 올바르다.

## 계산 제약

`per_threat.failure`은 기술적 제외 시행에서 비어 있다. 유효 시행일 때 마감보다 일찍 중화된 표적만 `0`; 마감 동시/이후 및 미중화는 `1`. `per_threat.no_response`는 위협에 대한 시도 기록이 없으면 `1`. `first_acquisition_latency_s`는 획득 이벤트가 없다면 비어 있다. `per_trial.camera_path_deg`는 경고~최초 표적 획득(미획득이면 종료) 구간의 원시 yaw 샘플에서 계산한다. 1개 미만 구간 차이를 정확한 경로로 추정하지 않으며 `null` 반환한다. 런타임에서 기록한 `yaw_path_deg`/`turn_reversals`는 *집계 구간을 단정할 수 없어서* 별도 `reported_*` 칼럼으로 유지한다.

런타임에서 직접 맞힌 표적은 `attribution_method=direct_collider`, 가까이 빗나간 발사는 `angle_proximity_inference`로 구분한다. 후자의 `response_attempt`/`inferred_near_miss_shots`는 **조준 광선과 가장 가까운 유효 표적의 각도 차이가 설정된 임계값 이내라는 휴리스틱**에 따른 시도 추정이다. 참가자가 그 표적을 의도했다는 직접적인 증거가 아니다. 기본 임계값은 연구에서 아직 타당화되지 않았으며 세션 snapshot의 `near_miss_inference_angle_degrees`를 보존한다. 고정된 임계값의 민감도 분석을 별도로 계획해야 한다. 최초 발사 귀속 근거는 `per_trial.first_shot_attribution`으로 제공한다.

현재 결과는 독립 표본 가정, 혼합효과 모형, 통계적 신뢰구간/유의성 검정을 포함하지 않는다. 실제 참여자 집단에 대한 결론은 별도 분석계획·계층 구조·결측 및 사전 제외 기준 검토 후 내려야 한다. 특히 3위협 시행의 3개 결과를 3명의 독립 참여자로 해석하면 안 된다. **고정 경고 v1 결과와 연속 추적 v2 결과를 같은 조건 셀에서 풀링하면 안 된다.** 연구가 변경되었으므로 분석도 프로토콜 버전별로 분리해야 한다.
