# Phase 0 — 재사용 조사 (2026-11-02)

기준 저장소: `/Users/minsub/Documents/GitHub/GCCD/GCCD`, 시작 커밋 `e855d67`.
브랜치: `research/distance-priority-angular-suppression`. 시작 시 작업 트리 clean.
외부/상위 저장소의 미추적 GCCD 폴더와 zip, iCloud 프로젝트, 채팅 sources는 변경하지 않는다.
이전 연구의 추적 파일은 Git 시작 커밋에서 복구 가능하며 신규 실행 경로에서는 제거한다.

| 시스템 | 확인 파일 | 처리 / 근거 |
|---|---|---|
| Unity | GCCD_Project/ProjectSettings/ProjectVersion.txt | 6000.3.24f1 유지 |
| 패키지 | GCCD_Project/Packages/manifest.json | 기존 uGUI 2.0.0 및 모듈 유지; 불필요한 추가 없음 |
| 기존 2×4 실험 | Assets/Scripts/Core/ResearchController.cs, ResearchModels.cs | 새 연구와 불일치, 이전 실행 코드 제거 |
| FPS | ResearchController.cs의 UpdateViewRotation/UpdateShooting | 마우스 회전 및 Raycast/Collider-ID 판정 방식을 이식 |
| 기존 맵 | Assets/Editor/StealthStudyEnvironment.cs | 실험 A의 시각 단서·FOV 통제를 위해 재사용하지 않음 |
| SDK 초기화 | Assets/Editor/BhapticsSdk2EditorSetup.cs | 공식 초기화 프리팹 탐색·삽입 도구 유지 |
| 로그 | Assets/Scripts/Logging/ResearchJsonlWriter.cs | UTC·UTF8·AutoFlush·SHA256 방식을 재사용, 새 CSV/commit-journal 구현 |
| 공간 수학 | Assets/Scripts/Haptics/SpatialMotorMapping.cs | atan2와 원형 오차 방식 재사용, 잘못된 입력을 조용히 0으로 만들지 않음 |
| 기존 합성 | SpatialMotorMapping.cs Mix | 누적 합성·강도 ceiling·연속 추적은 새 max 합성/고정 스냅샷과 불일치, 제외 |
| SDK 브리지 | ResearchSdk2Bridge.cs | 기존 브리지는 Pro32 참고용이며 X40 실행 경로 없음. 새 40채널 요청은 별도 검증 잠금 |
| 구형 네 방향 | 채팅 work/GCCD/.../BhapticsHapticOutput.cs | PlayThreatCue 안 Stop() 및 maximumIntensity=.5f 실제 확인. 동시 출력용 재사용 불가 |

## 장비/API 조사

- 최초 조사: Unity 에디터와 macOS 모듈 존재, Windows Build Support 미설치. 이후 동일 6000.3.24f1의 windows-mono 모듈만 추가 설치하여 Windows 빌드를 생성함. 엔진 업그레이드 없음.
- 공식 Asset Store 로컬 캐시 `bHaptics Haptic Plugin.unitypackage` 존재. vendor 코드는 Git 배포 제외.
- [공식 PlayMotors 문서](https://docs.bhaptics.com/sdk/unity/references/library)는 Vest를 Pro32로 명시한다.
- 설치 캐시의 BhapticsLibrary는 PlayMotors(int,int[],int), IsConnect, GetDevices, StopInt를 제공한다.
- X40 40배열의 실제 허용 여부, 인덱스, 물리 동시성은 문서/컴파일만으로 확인 불가.
- 기존 Player 페어링, SDK 자격증명, 장비 설정은 초기화하거나 출력 테스트를 자동 실행하지 않는다.

## 계획

1. 순수 C# 선별·억제·거리·매칭 테스트 → 구현.
2. 프로파일 기반 합성/양자화/동일성 진단과 장비 어댑터.
3. 18계열/6블록, 원형 응답, 세션 무결성 및 실패 처리.
4. 파일럿 설정 잠금과 B의 고정 스냅샷/첫 대응.
5. Unity 컴파일/씬/플레이 흐름 검증, 가능한 실행 빌드 및 미검증 항목 기록.
