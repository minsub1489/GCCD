# 거리 세기와 시작 전 검사 검증

MISSION_DEMO의 기존 선형 거리 감쇠는 유지하고 기본 범위를 20~45%에서 20~80%로 넓혔다. 6m/21m/36m의 기준 세기는 80/50/20%다. 각 모터는 방향 공간 가중치를 적용하므로 기본 앞 방향에서는 동일한 10개 모터에 각각 40/25/10이 전달된다. 가장 먼 거리의 하한은 낮추지 않았다. 연구 A/B 개인별 교정 L/H는 변경하지 않았다.

시작 전 진동 창에 방향/거리·세기 탭을 추가했다. 거리 검사는 앞 방향과 200ms를 고정하고 미션과 같은 MissionHapticEncoding 렌더러와 X40 보정 프로필을 사용한다. 거리 단계 3개와 방향 4개의 확인 수를 따로 관리한다. 제출 성공 전 확인 불가, 탭 전환 시 이전 확인 대상 해제, 보정 시 두 확인 수 초기화, 연속 요청 간 0.5초 제한을 적용했다. 실제로 느꼈다는 사용자의 확인 버튼을 자동으로 누르지 않는다.

RED 체크포인트 c7669b3: 공통 렌더러가 없는 상태에서 새 검사 4개의 컴파일 실패를 기록했다. GREEN: C# 95 passed / 0 failed. 거리 세 단계의 40/25/10 출력, 모터 집합 동일, 미션/검사 배열 일치, 전 방위의 단조 감소·범위·거리 제한, 잘못된 단계 거부를 검사했다. Python 분석기 5개도 통과했다. 커버리지는 측정하지 않았다.

Unity DTS_DISTANCE_PREFLIGHT_PASS / DTS_RESULT_FLOW_PASS / DTS_MISSION_PROBE_PASS / DTS_SOFTWARE_VALIDATION_PASS를 확인했다. 방향/거리 탭과 버튼, 미제출 확인 차단, 실제 클릭 좌표와 결과 표시 동일, 실내 이동·점프·앉기·지도·조준·미션 완료·CSV 재저장을 검사했다. 결과 CSV 두 행의 haptic_encoding, intensity_low/high가 distance-linear-20-80-v2와 0.2/0.8인지 대조했다. UI 렌더도 직접 확인했다. Unity Editor Search 내부 ArgumentOutOfRangeException은 기존에 있던 항목이며 게임 검사와 빌드는 통과했다.

Windows64 Release 및 macOS Release 빌드 성공. vendor SDK와 자격 증명은 포함하지 않는 Player 로컬 연결 배포본이다. 실제 X40 착용 시 세기 차이·방향 체감, 물리 동시성 및 Windows 실기기 실행은 확인하지 않았다. 소프트웨어 검사는 진동을 물리 출력하지 않았다.
