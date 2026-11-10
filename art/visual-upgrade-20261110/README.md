# OUTPOST 07 custom industrial models

Original geometry authored for this project with Blender 5.4.0 Alpha. No third-party model or texture data is used in these two models. The FBX files contain geometry, normals, semantic material assignments and rotor pivots; runtime supplies project materials.

| Resource load path | Triangles | Mesh renderers | Size in Unity meters |
| --- | ---: | ---: | --- |
| `VisualUpgrade/Models/AegisSecurityDrone` | 32,156 | 10 | 1.142 × 0.486 × 0.922 |
| `VisualUpgrade/Models/IndustrialServiceUnit` | 6,100 | 5 | 0.860 × 1.805 × 0.370 |

The drone origin matches the former primitive drone origin. Keep its transform centered at the existing enemy position; preserve the existing gameplay collider. Its optical lens faces +Z. `Rotor_LF`, `Rotor_LB`, `Rotor_RF`, `Rotor_RB` are empty transforms with a single mesh child. Rotate the empty around local Y. Static meshes are combined by material to limit renderers.

The service cabinet is modeled in its mounting plane at Z≈0, front +Z, vertically centered around the cabinet body. The maximum vertical extent includes the three upper conduit stems and lower junction. This visual prop should have no added colliders or navigation impact.

Material names: `Hull`, `Steel`, `Rubber`, `Amber`, `Cyan`, `Lens`. The drone uses all six. The service unit uses the first five. Cyan surfaces are emissive in the Blender render; runtime needs its own emissive material.

`outpost_hard_surface.blend` is the unarranged native model file, stored outside Unity Assets to avoid requiring Blender on other build machines. `build_hero_models.py` deterministically regenerates FBX, manifest, native file and the preview. The beauty preview lighting is illustrative; it is not an in-game screenshot.

Validation completed: Blender export succeeded; exported drone was imported into a clean Blender scene and checked for 10 meshes, four rotor pivots, preserved semantic materials and maximum bounds below the original 1.3 × 0.65 × 1.1 meter hitbox. Rotor local Y was verified as the vertical axis. `fbx_roundtrip.json` records the result. Native Blender uses Z up, so the report's world-Z vertical corresponds to Unity world Y. Unity rendering and gameplay regression are performed by the main task.

SHA-256 values are in `model_manifest.json`.
