# 공포게임만 유지하도록 정리

2026-10-14 사용자 지시에 따라 FunIsland와 이전 과녁·연속 데모 장면을 프로젝트에서 제거했습니다. 현재 `Assets` 및 Build Settings에는 `SilentHapticResearch.unity` 하나만 있습니다. Unity 자산 API로 공유 이동·음원·폰트·패널을 이동해 GUID를 유지했습니다. 사용하지 않는 데모 코드·테스트·문서는 저장소 밖으로 이동했습니다.

복구 보관 위치는 작업 폴더의 `GCCD-retired-20261014/`입니다. 첫 전체 백업은 `/tmp/gccd-before-horror-only-20261014.tar.gz`이며 이동 전 예전 콘텐츠 212개 파일의 SHA256을 대조했습니다. 이전 미커밋 변경을 포함합니다. 자동 승인 검토가 넓은 폴더 삭제를 거부했으므로 원본과 `.meta`의 복구 가능한 이동으로 처리했습니다.

남긴 자산: `ResearchPlayerMotion`, X40 모터 배열/방향 계산, `BHapticsVestOutput`, SDK2 어댑터, Footsteps/Door CC0 음원과 라이선스, UI Toolkit 패널·폰트·테마. 기존 씬의 새 재질 참조는 모두 새 공포게임 자체 재질입니다.

bHaptics SDK2는 같은 사용자의 기존 로컬 설치에서 예제를 제외하고 설치했습니다. `GCCD_BHAPTICS_SDK2`를 Unity API로 활성화했습니다. 비공개 App ID/API Key 설정은 재사용하되 Git과 문서에서 제외합니다. 실제 X40는 현재 사용할 수 없어 실물 확인 결과는 생성하지 않았습니다.
