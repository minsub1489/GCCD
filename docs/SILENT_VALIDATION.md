# 공포게임·교정·파일럿 검증

2026-10-14, Unity 6000.3.24f1, macOS Apple Silicon. 실제 참가자·X40 체감 자료는 없습니다.

| 검증 | 결과 |
|---|---|
| SDK2 2.8.1 설치·define·직접 API 컴파일 | 통과; 핵심 3개 파일이 공식 캐시와 바이트 일치 |
| 공포게임 장면·유일한 AudioSource·비침묵 음원·UI/파일럿 연결 | 통과 |
| SDK 폴더 없이 시뮬레이션 컴파일·장면 검증 | 통과; SDK 폴더 및 .meta는 확인 후 복원 |
| EditMode | 274/274 통과, 실패·스킵 0 |
| PlayMode | 14/14 통과, 실패·스킵 0 |
| Python 분석·복구 | 두 파일럿 목적 제외, 미완료 저널 복구 시 invalid 유지 통과 |
| macOS 빌드·네이티브 라이브러리 포함 | 성공; arm64/x86_64 bHaptics dylib 포함 |
| 실제 앱 화면·버튼·SDK Check·교정/블라인드 식별·Esc 저장 | 확인 |
| 실물 X40 출력·체감 교정·사람의 식별/난이도 파일럿 | **대기: 장비를 현재 사용할 수 없음** |

EditMode는 기존 공유 모터 수학과 공포게임 계획·지표를 포함합니다. 새 검증은 교정의 전 채널/편안함/전달 조건, 누락·오답·중단·중복·시뮬레이션 거부, 장치·패턴·참가자 결합, 클래스별 식별 기준, 무응답 포함 혼동행렬, 전체 난이도 블록 요구와 바닥/천장 기준, 본 실험 데이터와 파일럿 분리를 확인했습니다.

PlayMode는 저장된 유일한 게임 장면을 다시 로드합니다. 네 조건의 출력 분리, 실제 은폐/회피·추격·목표, 데이터 즉시 저장 및 중단, UI 연결·실험 중 관리 화면 숨김과 직접 폰트 참조, 파일럿의 40채널 선택, 정답 숨김·무응답·중단 저장, 중복 최종 행 방지, 난이도 파일럿 목적·시드·보고서를 검증했습니다. 시험 fixture는 실제 참가자 자료가 아닙니다.

앱 실행 중 이동한 USS의 오래된 상대 경로가 폰트를 MissingAssetReference로 처리하는 문제를 발견했습니다. 런타임 UIFont의 직접 GUID 참조와 스타일 재임포트로 수정했고 실제 앱의 텍스트·버튼 표시를 다시 확인했습니다. 파일럿 안내는 첫 선택 잠금과 NoResponse 저장을 명시합니다.

네이티브 앱의 실제 SDK Check는 `bHaptics Player unavailable / physical calibration pending`을 표시했습니다. X40는 연결하지 않았고 모터 전달 성공으로 보고하지 않습니다. 두 네이티브 시뮬레이션 실행은 각각 Completed/무응답 및 Aborted를 중복 없이 저장했고 모든 행 Valid=false, 증명 Passed=false였습니다. 기본 익명 ID는 기능 점검에만 사용됐습니다. 개인 실험 데이터가 아닙니다.

[최종 검증 수치·소스 해시](validation/silent-study-tests.json). 상세 로그·NUnit XML은 `/tmp/gccd-pilot-*` (최종 PlayMode: `gccd-pilot-all-final.xml`), SDK 없는 컴파일 로그는 `/tmp/gccd-no-sdk.*`에 있습니다. 삭제된 예전 데모 테스트는 현재 결과에 포함하지 않습니다. 이전 개편 단계의 285 EditMode/25 PlayMode 수치는 현재 정리 후 결과와 구분합니다.

실물 단계는 [X40 교정·파일럿 안내](X40_PILOTS.md)를 따릅니다. 하드웨어 지연·지각 강도 동등성·실제 참가자 결과를 자동 테스트 통과로 대신하지 않습니다.

## 이어서 수행한 입력 경로 검증

최종 반복 실행에서 헤드리스 Editor의 Game View 포커스 정책이 합성 포인터를 차단하는 조건을 확인했습니다. 추가 테스트는 해당 정책을 테스트 동안만 처리하고 원복합니다. 실제 Input System 마우스 상태 → UI 모듈 → 패널 hit testing → Check Hardware 버튼 콜백이 통과했습니다. 제품의 포커스 상실 중단 설정은 바꾸지 않았습니다. 테스트 어셈블리의 남은 이전 AcousticResearch 참조도 제거했습니다.

추가 실행 중 iCloud가 생성 PackageCache에 만든 dataless 충돌 파일(`… 2.cs`, `… 2.meta`)의 읽기 대기로 Unity 시작이 멈췄습니다. 스택으로 확인한 뒤 캐시와 오래된 경로 정보를 가진 Library를 보관하고, 같은 manifest/lock으로 재임포트했습니다. 캐시는 로컬 임시 `gccd-icloud-cache-conflicts-*`, Library는 작업 폴더의 `GCCD-library-backup-20261014`에 보관했습니다. 프로젝트 소스와 SDK 설정은 유지했습니다.
