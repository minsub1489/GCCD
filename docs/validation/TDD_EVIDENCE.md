# 개발·회귀 검증 근거

사용자 최종 개발 지시서에서 요구한 선별·억제·응답·자극·기록 보장을 독립 C# 테스트로 정의했다.

- RED: `scripts/run-core-tests.sh`에서 미구현 GCCD.Dts/Threat/MotorProfile 참조의 compile-time 실패. `core-red.txt`. 체크포인트 `9e4cce9`.
- GREEN: 동일 러너로 핵심 구현 후 55개 통과.
- 추가 자극 검증 RED: 최소 쌍별 간격이 family 구간 안에 있어야 한다는 테스트가 실패. `gap-red.txt`. 세 번째 방향을 반대편 중앙으로 바꾼 뒤 통과.
- 추가 저장 검증 RED: 완료 저널 손상이 재개 시 거부되어야 한다는 테스트가 실패. `journal-red.txt`. payload SHA-256을 원자적 envelope에 저장하고 읽을 때 검증하도록 수정 후 통과.
- 현재 결과: `core-green.txt`의 실제 테스트 수/결과를 기준으로 한다.

| 보장 | 검증 위치 |
|---|---|
| 0°/6조건, 원형·포함 경계, ID 동률, 억제 부모, 입력 순서, 빈자리 미충원 | CoreTests.cs |
| 거리 함수·범주·범위, max 합성·정수 양자화·프로파일 검증 | CoreTests.cs |
| /3 공동 채점, 순서 독립 매칭, 무응답 결측, 중복 대응 금지, 모호성 | CoreTests.cs |
| 18계열/108시행, 정확한 순서 6행, 시드 재현성, offscreen, 최소 간격, 거리 분포 | CoreTests.cs |
| 첫 명중·첫 빗나감·정면 과제 제외·deadline 포함 시간초과 | CoreTests.cs / DtsPlayProbe.cs |
| 세션 중복·pending·resume 메타데이터 일치·완료 저널 손상 거부 | CoreTests.cs |
| Unity 상태 전이, 0~3마커·수정·삭제·timeout 보존, 정상 출력 없는 MAIN 차단, CSV | DtsValidation.cs |
| 실제 Play Mode 108 A / 24 B, Raycast와 프레임 진행 | DtsPlayProbe.cs |

알고리즘 RED/GREEN은 Unity에 번들된 Mono C# 컴파일러·런타임으로 실행했다. Unity 통합/Play Mode 테스트는 별도로 실행했다. 신규 테스트가 먼저 정의된 핵심 로직과, 통합 뒤 발견한 회귀 테스트를 구분한다. 모든 런타임 행에 대한 TDD 선행을 주장하지 않는다. 계측 가능한 커버리지 수집 도구를 추가 설치하지 않았으므로 80% 커버리지 달성을 주장하지 않는다. 실제 Windows 실행·물리 장비·참가자 파일럿은 별도 항목이다.
