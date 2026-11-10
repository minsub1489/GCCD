# Finding Threat 그래픽·메인 UI 업그레이드

2026-11-10 · Unity 6000.3.24f1 / Built-in Render Pipeline

## 적용 내용

- 메인화면: 청록·황금색 계기판, 벡터 360° 스캔, 패널 등장, 버튼 모션. 이름/억제각과 최소/최대 위협 수를 묶고 세부 판정 입력은 접이식으로 정리. 원래 입력값·검증·이벤트는 유지.
- HUD: 플레이어 방향만 읽는 나침반, 탄약 스트립, 재장전 진행, 사격/명중/회수 피드백. 모션 줄이기 선택 제공.
- 맵: 짙은 하부 패널, 모듈 이음선, 문틀과 유도선, 천장 리브·배관·덕트, 배수 홈·바닥 그레이트, 옥상 설비·외부 갠트리와 구역 표식.
- 모델: Blender 제작 드론/보조 전원 유닛. 드론 32,156 삼각형/10 렌더러/팬 회전 4개. 기존 드론 충돌체 유지.
- 신규 다운로드: Poly Haven CC0 벽등 12개·배전함 4개를 맵에 배치. 원본 FBX와 1K PBR/정상/금속·거칠기/발광 맵을 실제 리소스로 포함.
- 이펙트: 풀링한 탄착 불꽃/먼지·탄도 궤적·총구 연기, 교차 면 총구 섬광, 회수 링, 공간 먼지. 1/4 해상도 하이라이트 블룸과 색 보정.

## 보존한 범위

원래 미커밋 작업을 기준으로 추가했다. 이동·점프·앉기·발사 속도·탄수·재장전 시간·맵 좌표·벽 충돌·위협 생성·햅틱 출력·실험·채점·저장 규칙은 변경하지 않았다. 응답 지도/실험/일시정지 중 장식 UI를 숨기고 입자·로터를 멈춘다. 위협 개수/정답 위치를 장식 UI에 전달하지 않는다.

기존 씬·ProjectSettings·SDK 자격증명·sources는 원본에 복사하지 않는다. 배포 사본의 SDK App ID/API Key는 비웠으며 기본 Player 직접 연결 경로는 유지한다. 변경한 기존 파일은 적용 전 백업하고 원본 해시가 바뀌었으면 복사를 중단한다.

## 검증

- 핵심 C# 검사: **109 passed, 0 failed**.
- Python 분석기 unittest: **7 passed**.
- Unity 기존 연구 플랫폼: **DTS_SOFTWARE_VALIDATION_PASS**.
- Unity 기존 미션/응답/CSV/로비 제어 흐름: **DTS_MISSION_PROBE_PASS** (최종 수정 후 재실행).
- 새 리소스 검사: **VISUAL_UPGRADE_RESOURCE_PASS**.
- 새 플레이 검사: **VISUAL_UPGRADE_PLAY_PASS**. 원래 드론 5개와 충돌 크기, 로터 수직축, CC0 배치 수, 무충돌 장식, 720개 이하 입자 예산, 사격, 정지, 응답 중 연출 차단, 종료 정리 확인.
- UI 렌더 캡처: 1600×1000 / 1280×720. 작은 화면 필수 입력칸 표시·배치 수정 후 재확인.
- 다운로드 원본 12개/6.82MB: 공식 API MD5 일치. 변환한 ARM→Standard 맵 3세트 픽셀 확인. 원본/변환 파일 SHA-256 기록.
- Windows x64·macOS Release 빌드 성공. Mac 실행 파일에서 메인화면·스캔 모션·입력란 표시·키보드 포커스와 팝업 열림을 확인했다. 최종 팝업 색상 보정은 API/패널 스타일 범위를 확인했으며, batch Editor의 팝업 자동 닫힘 때문에 펼친 상태의 재캡처는 확인하지 못했다.

## 한계와 환경 기록

Windows PC 실제 실행, 장시간 FPS/메모리 프로파일링, 착용 진동·실물 방향 매핑 검증은 이 그래픽 작업에서 수행하지 않았다. 참가자 본 실험 자료를 생성하지 않았다. 커버리지 비율은 측정하지 않았다.

Unity Editor Search의 기존 내부 SearchDatabase IndexOutOfRangeException이 검증 실행에서도 기록되었지만 게임 코드에서 발생한 예외는 없고 실행 검사는 통과했다. Unity CLI beta.13 → beta.15 업데이트 알림이 있었으며 설치 변경 없이 고정 Unity 에디터로 직접 검증했다.

## 에셋 출처

- Industrial Wall Lamp — Kuutti Siitonen: https://polyhaven.com/a/industrial_wall_lamp
- Power Box 01 — Rico Cilliers / Yann Kervran: https://polyhaven.com/a/power_box_01
- 원 배포 라이선스: https://polyhaven.com/license (CC0)
- 모든 파일 URL·SHA-256·변환법: 동봉 `VISUAL_UPGRADE_SOURCES.md`와 JSON manifests.
- 직접 제작 모델 원본 `.blend`와 재생성 Python: 프로젝트 `art/visual-upgrade-20261110`.

## 원본 반영 확인

그래픽 파일 73개를 SHA-256 대조 후 반영했다. 기존 변경 파일 6개를 백업했으며, 그 외 원본 940개가 작업 시작 시점과 일치함을 확인했다. 원본 SDK/프로젝트 설정, 기존 실험·채점·햅틱 코드와 미커밋 변경을 보존했다.
