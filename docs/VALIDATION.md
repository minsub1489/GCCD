# 최종 소프트웨어 검증 — 2026-11-02 (KST)

**구현·개발 검증과 실제 연구 가능 상태를 구분한다.** Windows/macOS 빌드는 생성했으나 Windows 실행, 실제 X40, 사람 대상 파일럿은 검증하지 않았다. MAIN 사용 준비 완료로 보고하지 않는다.

| 항목 | 실행 결과 | 근거 |
|---|---|---|
| 순수 C# 코어·기록 | **61 passed / 0 failed** | `./scripts/run-core-tests.sh`, `validation/core-green.txt` |
| 분석기 | **5 tests OK** | `python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v`, `validation/analysis-tests.txt` |
| Unity 컴파일·A 상태 흐름 | **PASS 108시행** | `GCCD.Editor.DtsValidation.Run`, `validation/unity-integration.txt` |
| 실제 Unity Play Mode | **PASS A 108 / B 24**, 372프레임 이상 진행 | `GCCD.Editor.DtsPlayValidation.Start`, `validation/unity-playmode.txt` |
| B Raycast | 최근접 첫 명중 성공 / 첫 빗나감 실패 / 무대응 timeout, 정면 과제 제외 | 동일 Play Mode 검사, 개발용 합성 응답 |
| 자극 검사 | 108배치 × 6조건 = **648 출력**, 15개 조건 쌍 진단 | Unity 통합 검사 |
| 세션 안정성 | 완료 시행 중복 재개 방지, pending 기술 실패, 체크섬 손상 거부 | C# 및 Unity 검사 |
| Windows x64 Mono 빌드 | **SUCCESS**, 보고 크기 185,890,868 bytes | `Builds/Windows/GCCD.exe` 및 동반 파일 |
| macOS Mono 빌드 | **SUCCESS**, 보고 크기 354,356,545 bytes | `Builds/macOS/GCCD.app` |
| macOS 앱 표시 | 실제 앱 실행·한글 메뉴/입력 필드 표시 확인, 초기 화면 로그 오류 없음 | `DTS_UI_FONT Apple SD Gothic Neo` |
| 네이티브 UI 자동 조작 | **미검증**: 자동 도구의 Unity IMGUI 좌표 입력으로 시작 전환 확인 못함 | 상태 전이는 Play Mode 검사로 별도 검증 |
| Windows 실제 실행 | **미실행** | Windows PC에서 수동 확인 필요 |
| 실제 X40/파일럿 | **미실행** | `HARDWARE_ACCEPTANCE.md` |
| 계측 커버리지 | **미수집** | 80% 달성 주장 안 함 |
| Git diff 공백 검사 | PASS | `git diff --check` |

마지막 Windows/macOS 빌드와 A 통합 검사는 `GCCD.Editor.DtsProjectBuilder.ValidateAndBuildBoth`로 실행했다. 초기 macOS OS-font fallback 배열에서 존재하지 않는 첫 글꼴을 TextCore가 사용하는 문제를 발견해, 실제 설치된 글꼴 한 개를 선택하도록 수정하고 양쪽을 다시 빌드했다. 기본 Windows 선택은 설치된 Malgun Gothic이며 해당 PC에서 한글 표시를 확인해야 한다.

Play Mode 테스트에는 Unity 6000.3.24f1의 `UnityEditor.Search.SearchDatabase` 시작 인덱싱 `ArgumentOutOfRangeException`이 한 차례 기록되었다. 연구 런타임의 호출 스택이 아니며 108/24 검사와 양쪽 빌드는 통과했다. 에디터 검색 인덱스 진단이므로 프로젝트 게임 로직 오류로 숨기거나, 전체 로그가 오류 없이 깨끗했다고 보고하지 않는다.

## 단계별 상태

- **Phase 0:** 경로/버전/재사용 조사·별도 브랜치 완료. 기존 연구 코드·씬·분석기·미사용 아트 제거. 시작 커밋에서 복구 가능. 외부 사본·사용자 미추적 파일·sources 유지.
- **Phase 1:** 선별/억제/거리/채점 순수 C# 구현 및 테스트 완료.
- **Phase 2:** max 합성, 교정·개별 모터 시험 UI, 정수 제출 배열 로깅, 공식 SDK 호출 컴파일 완료. **실측 X40 매핑·40채널 처리·물리 동시성 미완료.**
- **Phase 3:** 18계열·6조건 자극과 배열 동일성 도구 구현/재현성 검사 완료. **실측 맵에서 재검사 필요.**
- **Phase 4:** A 전체 108시행 상태 흐름·응답·설문·CSV·복구 개발 검증 완료. **실제 참가자 입력 장비·Windows UI 수동 확인 필요.**
- **Phase 5:** 파일럿 기준/근거/잠금·훈련 기록 지원 구현. **실제 파일럿과 연구 설정 확정 미완료.**
- **Phase 6:** B 24이벤트·고정 스냅샷·첫 사격·성공/실패/시간초과 구현과 개발 검증 완료. **실제 A 후보/독립 참가자 확인 실험 미실행.**
- **Phase 7:** 소스·두 OS 빌드·사용 설명서·검증 기록 제공. **전체 연구 완료 아님.**

## 기록 해석과 한계

테스트 ID는 SOFTWARE_TEST / PLAYMODE_TEST, 자료는 DEVELOPMENT 별도 경로다. 자동 테스트의 응답/사격은 소프트웨어 검증용이며 참가자 측정치가 아니다. 분석 예시의 정답률/성공률/시간초과율도 연구 결과로 사용하지 않는다.

데이터 원본은 SHA-256 완료 저널이고 CSV는 파생 파일이다. 여러 CSV를 동시에 원자적으로 교체하지 않으므로 저장 중 종료되면 Data Review로 재생성한다. 전원 차단 내구성이나 Windows 파일시스템의 모든 오류 형태를 실제 시험한 것은 아니다. 원본 저널을 수정해 결측을 채우지 않는다.

본 실험에는 장비/SDK 앱 설정, 파일럿 기준 충족 확인과 근거 해시, 훈련 통과, 설정 해시 일치가 필요하다. 독립 B 참가자 확인에는 연구자 확인과 같은 익명 ID의 A 이력 검사를 사용하며, 다른 ID로 등록한 실제 인물의 동일성은 자동 판정할 수 없다. 판단 기준의 과학적 타당성, 통계적 검정력·주 분석모형은 연구자가 확정한다.

제공 실행 빌드에는 연구자의 SDK 앱 자격증명을 넣지 않았다. 현재 빌드는 개발용 시연이 가능하며 실제 출력용 빌드는 공식 SDK Developer Window에서 앱 설정 후 다시 생성해야 한다. 이미 연결된 Player의 페어링을 초기화하지 않는다.
