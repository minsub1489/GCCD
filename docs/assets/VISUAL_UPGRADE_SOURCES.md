# 신규 CC0 산업용 3D 에셋 — 2026-11-10

기존 FREE_ASSETS.md의 에셋과 중복하지 않는 신규 Poly Haven 모델 2종입니다. 공식 공개 API에서 원본 FBX와 1K PBR 질감을 다운로드했습니다. 모든 원본 파일의 MD5를 공식 API 응답과 대조하여 일치함을 확인했으며 아래 SHA-256은 로컬 다운로드 결과입니다. 이 원본 등록부를 작성한 뒤, 별도 staging 프로젝트의 Resources/VisualUpgrade/FreeAssets에 복사하고 Standard 셰이더용 금속/매끄러움·AO 텍스처를 파생 생성했습니다. 원본과 변환 후 해시는 각각 visual-upgrade-original-manifest.json / visual-upgrade-converted-manifest.json에 있습니다.

| 에셋 | 제작자 | 공식 원본 | 라이선스 | 용도 제안 |
|---|---|---|---|---|
| Industrial Wall Lamp | Kuutti Siitonen | https://polyhaven.com/a/industrial_wall_lamp | CC0 1.0 | 녹슨 보호 케이지가 있는 0.4m 벽등. 실내 벽 부착용 장식, 기존 충돌 구조와 동선을 유지 |
| Power Box 01 | Rico Cilliers (모델링·질감), Yann Kervran (리깅) | https://polyhaven.com/a/power_box_01 | CC0 1.0 | 0.5m 산업용 배전함. 외벽·실내 벽면 장식, 비충돌 배치 |

공식 라이선스: https://polyhaven.com/license — 모든 HDRI·텍스처·3D 모델을 CC0로 제공하며 상업 사용·재배포가 가능합니다. 모델별 공식 페이지에도 CC0 및 제작자 표시를 확인했습니다. CC0 요약: https://creativecommons.org/publicdomain/zero/1.0/

## Unity 통합 참고

- 각 `model.fbx`는 Unity가 직접 읽을 수 있습니다. 원본 FBX의 내부 텍스처 참조는 EXR을 가리킬 수 있으므로 제공한 JPG를 이용한 프로젝트 전용 Standard 재질로 remap 하세요. 추가 패키지는 필요하지 않습니다.
- 모델 자체는 약 4K tris (벽등), 약 21K tris (전기함)이며 공식 페이지의 치수는 높이 0.4m / 0.5m입니다. Unity importer의 단위와 FBX 축을 확인하고 높이로 normalize하면 기존 RealModel 방식에 맞출 수 있습니다.
- `*_diff_1k.jpg`: albedo/sRGB; `*_nor_gl_1k.jpg`: OpenGL normal, Unity TextureImporter의 NormalMap으로 설정; `*_arm_1k.jpg`: R=AO, G=roughness, B=metallic (linear). Standard 셰이더용 metallic/smoothness는 R=B, A=1-G로 패킹하고 albedo와 같은 해상도를 유지하세요.
- 벽등은 외부 케이지와 유리용 재질이 분리됩니다. `*_glass_*`는 유리 슬롯에 사용하고, `*_emissive_*`는 발광 마스크입니다. 가장 간단하고 안정적인 사용은 유리를 불투명한 저강도 발광 재질로 두는 것입니다. 원래 계정/라이선스와 관계없는 독립 CC0 다운로드입니다.
- 렌더링만 추가하고 collider, rigidbody, NavMesh, 사격 판정, 위협 위치·시험 채점은 추가/변경하지 마세요.

## 다운로드 원본 및 SHA-256

| 파일 | 바이트 | SHA-256 | 공식 다운로드 |
|---|---:|---|---|
| `industrial_wall_lamp/model.fbx` | 153356 | `9d8eb1e2fb81a7e56030adac610f809f10a75a6247f07ca5c91d26f70d4e9d63` | https://dl.polyhaven.org/file/ph-assets/Models/fbx/1k/industrial_wall_lamp/industrial_wall_lamp_1k.fbx |
| `industrial_wall_lamp/textures/industrial_wall_lamp_emissive_1k.jpg` | 62349 | `cf80eeeb6fec87499fb8444f554eae62e471ad79f8906d6fd693d7fde53b012f` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_emissive_1k.jpg |
| `industrial_wall_lamp/textures/industrial_wall_lamp_nor_gl_1k.jpg` | 673018 | `427bafa2f14d9f81a5949507d5d6915bfb4b70a8e5e5076a3a18c7de49b3d78b` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_nor_gl_1k.jpg |
| `industrial_wall_lamp/textures/industrial_wall_lamp_diff_1k.jpg` | 669629 | `e0f824b7e639fceb7ca5b7b7f67a8168bc53db184b818770126167d16d424b2e` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_diff_1k.jpg |
| `industrial_wall_lamp/textures/industrial_wall_lamp_arm_1k.jpg` | 753080 | `70adef2921bfcf985f951f9f85fca8dbfb63dc84535613c3872684fea65ab025` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_arm_1k.jpg |
| `industrial_wall_lamp/textures/industrial_wall_lamp_glass_nor_gl_1k.jpg` | 585913 | `73ce4f34d90aee29db9a5e301934c0642bf89136353d7e400f63db1c2f99f345` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_glass_nor_gl_1k.jpg |
| `industrial_wall_lamp/textures/industrial_wall_lamp_glass_diff_1k.jpg` | 316099 | `215457a5d41ae5d999e94f4ea0f424ff769940e71e1449ce712f35687e33af67` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_glass_diff_1k.jpg |
| `industrial_wall_lamp/textures/industrial_wall_lamp_glass_arm_1k.jpg` | 570850 | `a3ba7f76ff395ada04a804b94f4f2f506bc994336189916d2a414173e2360069` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/industrial_wall_lamp/industrial_wall_lamp_glass_arm_1k.jpg |
| `power_box_01/model.fbx` | 1099948 | `9090b2490bd9d4037f0c2421c1ec24726937cdb25ab4de43bc165d45ddaee3e3` | https://dl.polyhaven.org/file/ph-assets/Models/fbx/1k/power_box_01/power_box_01_1k.fbx |
| `power_box_01/textures/power_box_01_arm_1k.jpg` | 801803 | `4601e901e2f7c1584ab5ecf65e0e192a99a4be86da505527f1622658e1b1e527` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/power_box_01/power_box_01_arm_1k.jpg |
| `power_box_01/textures/power_box_01_nor_gl_1k.jpg` | 646545 | `199b6fe93776aa0934f7bc36d8c82f1b4db9be9243e82e0cb3727bbcf25d85f1` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/power_box_01/power_box_01_nor_gl_1k.jpg |
| `power_box_01/textures/power_box_01_diff_1k.jpg` | 490782 | `845c425ebc0ded263a7d49909e2c26710dd6160da11c0ca44aebd723f4f6f168` | https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/power_box_01/power_box_01_diff_1k.jpg |

세부 API 응답: `industrial_wall_lamp.files.json`, `power_box_01.files.json`. 기계 판독용 해시/URL: `download-manifest.json`.

## Scenery runtime integration

`MissionScenery.cs`의 `BuildEnhancedScenery()`는 벽등 12개(외벽 8 + 통행 복도 4), 배전함 4개, Blender에서 제작한 보조 전원 유닛 2개, 상부 케이블 트레이·배관·지붕 환기 장치·갠트리·환기 그릴·바닥 마킹·터미널 표지를 추가합니다. 추가 박스는 모두 collision=false이며 새 충돌체·강체·무작위 수 생성·연구/햅틱 코드는 없습니다. 메시에 읽기 가능한 정적 장식만 별도 배칭하며, 재질/메시는 기존 ownedVisuals 수명 관리에 등록합니다. Resources로 읽은 공유 질감/모델은 소유권을 가져오지 않습니다. 원래 world가 파괴될 때 장식도 함께 제거됩니다.

검증: 원본 파일 전체 공식 MD5 일치; Blender FBX import 성공; 파생 PBR 3세트의 금속 R/매끄러움 A/occlusion G를 원본 ARM과 픽셀 단위 대조해 일치; 정적 소스 검사에서 RNG/Collider/Rigidbody 생성 없음 및 장식 Box의 collision=false 확인. Unity 컴파일·실행 검증은 루트 작업에서 진행합니다.

시작 통행 복도 추가 개선: 원래 Interior partition/ceiling 렌더러만 복제 재질로 회색/흑연색을 입히고, FloorPlan의 bottom=0인 기존 실벽 구간에서만 하부 벽 패널·상부 레일·이음선을 생성합니다. x=±20/24의 기존 문 개구부는 z=중심±1.2, y<2.6이며 새 jamb 최소 경계는 ±1.215, header 최소 높이는 2.62입니다. 복도 바닥 배수 홈과 강철 그레이트·천장 리브·상부 덕트도 모두 비충돌 장식입니다.
