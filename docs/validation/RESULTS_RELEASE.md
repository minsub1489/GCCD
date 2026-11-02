# 미션 결과·CSV 및 강도 변경 검증

미션 완료 → 응답 기록 로드 → 1대1 공간 채점 → 결과 지도·이벤트 비교 → CSV 자동 저장 흐름을 구현했다. 정답 공개는 완료 이후에만 가능하다. 저장 실패 시 오류와 재시도를 제공한다. 중도 종료 이벤트는 aborted이며 정확도에서 제외한다.

TDD RED: 새 MissionLocationScorer를 참조하는 8개 검사에서 미구현 컴파일 실패 확인. 체크포인트 3ed2d0e.
GREEN: C# 83 passed / 0 failed. 순서 무관 1대1 매칭, 중복·추가 표시, 무응답, 3m 경계, 최대 정답 대응, 각도 순환·관찰자 위치 표시, 비정상 수치 검사를 포함한다.

Unity DTS_RESULT_FLOW_PASS 및 DTS_MISSION_PROBE_PASS: 실제 회수·명중 후 완료, 완료 이전 정답 접근 차단, 결과 UI 표시, 실제 위치 지도, CSV 3종 존재, 화면 체험 구분과 재저장 중복 방지를 확인했다. PanelSettings targetTexture를 이용해 실제 UI 렌더를 검토했다. 표시값은 명시적인 소프트웨어 테스트 응답이며 참가자 측정값이 아니다.

Python csv 파서로 UTF-8 BOM, 열 수, 빈 응답 0%/오차 빈값, 종합 합계와 요청/시뮬레이션 분리를 확인했다. 기존 분석기 unittest 5개도 통과했다. Windows/macOS Release 빌드와 연구 플랫폼 DTS_SOFTWARE_VALIDATION_PASS를 확인했다. 커버리지 비율은 측정하지 않았다.

미션 강도를 8~20%에서 20~45%로 올리고 기존 DistanceEncoding 선형 함수를 재사용했다. 실제 세기의 착용 체감·모터 매핑·Windows 실기기 실행은 확인하지 않았다. 연구 A/B의 개인별 강도 설정과 채점 코드는 변경하지 않았다.
