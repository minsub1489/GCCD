using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GCCD.Dts
{
    public sealed partial class MissionDemo
    {
        GameObject enhancedScenery;
        Mesh sceneryPipeMesh;
        readonly Dictionary<string, Material> sceneryMaterials = new Dictionary<string, Material>();

        // Presentation only. Every decoration has no collision and stays clear of door openings.
        // Call after BuildEnvironment; the original floor plan, spawn checks and shot rays stay intact.
        void BuildEnhancedScenery()
        {
            enhancedScenery = new GameObject("Terminal detail · CC0 industrial dressing");
            enhancedScenery.transform.SetParent(world.transform, false);
            var copper = Mat("Service pipe · oxidized copper", new Color(.24f, .29f, .27f), .68f, .28f);
            var duct = Mat("Ductwork · brushed zinc", new Color(.33f, .37f, .38f), .75f, .30f);
            var warning = Mat("Safety paint · faded ivory", new Color(.72f, .69f, .51f), .05f, .12f);
            var teal = SceneryEmission("Guidance strip · muted teal", new Color(.13f, .48f, .44f), .55f);
            var amber = SceneryEmission("Service lamp · warm amber", new Color(.90f, .48f, .15f), .85f);
            BuildSceneryInteriorPanels(duct, warning, teal, amber);

            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 11.70f;
                var orientation = Quaternion.Euler(0, side < 0 ? -90 : 90, 0);
                for (int bay = 0; bay < 4; bay++)
                {
                    float z = -10 + bay * 18;
                    SceneryModel("industrial_wall_lamp", new Vector3(x, 3.55f, z + 3.3f), .58f, orientation);
                    if (bay % 2 == 0)
                    {
                        var lamp = new GameObject("Caged lamp warm spill").AddComponent<Light>();
                        lamp.transform.SetParent(enhancedScenery.transform, false);
                        lamp.transform.position = new Vector3(side * 11.15f, 3.35f, z + 3.3f);
                        lamp.type = LightType.Point; lamp.color = new Color(1f, .73f, .41f);
                        lamp.range = 4.5f; lamp.intensity = 1.6f; lamp.shadows = LightShadows.None;
                        lamp.renderMode = LightRenderMode.ForceVertex;
                    }
                    // Cable trays and their wall brackets sit above every 3.2 m entrance.
                    SceneryBox("Galvanized cable tray", new Vector3(side * 11.64f, 4.52f, z + 1), new Vector3(.42f, .07f, 16.4f), duct);
                    SceneryBox("Cable tray outer lip", new Vector3(side * 11.42f, 4.61f, z + 1), new Vector3(.035f, .18f, 16.4f), trim);
                    foreach (float dz in new[] { -5f, 3f })
                        SceneryBox("Cable tray cantilever", new Vector3(side * 11.65f, 4.42f, z + dz), new Vector3(.64f, .07f, .09f), steel);

                    // Interior equipment remains flush with walls and above head height.
                    SceneryBox("High ventilation grille frame", new Vector3(side * 33.77f, 3.75f, z + 4.8f), new Vector3(.09f, .80f, 2.0f), trim);
                    for (int slat = 0; slat < 6; slat++)
                        SceneryBox("Vent grille louvre", new Vector3(side * 33.70f, 3.44f + slat * .12f, z + 4.8f), new Vector3(.12f, .035f, 1.85f), duct);
                    SceneryBox("Equipment route floor line", new Vector3(side * 21.15f, .079f, z), new Vector3(.06f, .012f, 13.5f), warning);
                    for (int mark = 0; mark < 4; mark++)
                    {
                        var stripe = SceneryBox("Threshold safety hatch", new Vector3(side * 13.4f, .079f, z - 1.1f + mark * .7f), new Vector3(1.1f, .012f, .12f), warning);
                        stripe.transform.rotation = Quaternion.Euler(0, side * 30, 0);
                    }
                    // Roof furniture builds a readable industrial silhouette beyond the lane.
                    SceneryBox("Roof extractor base", new Vector3(side * 26f, 5.72f, z + 4.8f), new Vector3(2.5f, .28f, 2.4f), trim);
                    SceneryBox("Roof extractor housing", new Vector3(side * 26f, 6.25f, z + 4.8f), new Vector3(2f, .85f, 1.9f), duct);
                    SceneryBox("Extractor weather cap", new Vector3(side * 26f, 6.75f, z + 4.8f), new Vector3(2.3f, .14f, 2.2f), steel);
                }
                foreach (float z in new[] { -3.5f, 32.5f })
                {
                    SceneryModel("power_box_01", new Vector3(side * 11.76f, 2.2f, z), .67f, orientation);
                    SceneryPipe(new Vector3(side * 11.77f, 2.58f, z), new Vector3(side * 11.77f, 4.45f, z), .035f, copper);
                }

                // Long overhead routes inside the central service corridor, never across doors.
                SceneryPipe(new Vector3(side * 22.3f, 4.40f, -16), new Vector3(side * 22.3f, 4.40f, 48), .085f, copper);
                SceneryPipe(new Vector3(side * 22.6f, 4.42f, -16), new Vector3(side * 22.6f, 4.42f, 48), .045f, steel);
                for (int support = 0; support < 9; support++)
                    SceneryBox("Pipe suspension clamp", new Vector3(side * 22.45f, 4.52f, -15 + support * 7.6f), new Vector3(.62f, .055f, .08f), duct);
                SceneryBox("High service guide rail", new Vector3(side * 11.81f, 3.28f, 16), new Vector3(.03f, .045f, 64), teal);
                SceneryAuthoredUnit(new Vector3(side * 23.74f, 2.10f, -4.5f), orientation);
            }

            foreach (float z in new[] { -20.5f, 49f })
            {
                // Gantries cross the outdoor lane at 6.4 m with clear sightlines underneath.
                SceneryBox("Lane overhead gantry", new Vector3(0, 6.4f, z), new Vector3(24, .32f, .32f), steel);
                SceneryBox("Lane overhead gantry upper", new Vector3(0, 7.15f, z), new Vector3(24, .13f, .16f), trim);
                for (int bay = 0; bay < 8; bay++)
                {
                    var brace = SceneryBox("Gantry diagonal bracing", new Vector3(-10.5f + bay * 3, 6.77f, z), new Vector3(3.1f, .07f, .07f), duct);
                    brace.transform.rotation = Quaternion.Euler(0, 0, bay % 2 == 0 ? 14 : -14);
                }
                SceneryBox("Gantry amber caution light", new Vector3(-5.8f, 6.16f, z), new Vector3(.5f, .06f, .12f), amber);
                SceneryBox("Gantry amber caution light", new Vector3(5.8f, 6.16f, z), new Vector3(.5f, .06f, .12f), amber);
            }

            // A weathered loading-zone stencil and edging add detail without obscuring targets.
            for (int dash = 0; dash < 6; dash++)
                SceneryBox("Loading zone edge stencil", new Vector3(-4.8f + dash * 1.9f, .026f, -21f), new Vector3(1.0f, .014f, .13f), warning);
            ScenerySign("07 / FREIGHT TERMINAL", new Vector3(0, 7.5f, 53.70f), Quaternion.identity, .16f, warning.color);
            ScenerySign("SECTOR A", new Vector3(-11.77f, 4f, 16.5f), Quaternion.Euler(0, -90, 0), .12f, new Color(.68f, .79f, .75f));
            ScenerySign("SECTOR B", new Vector3(11.77f, 4f, 16.5f), Quaternion.Euler(0, 90, 0), .12f, new Color(.68f, .79f, .75f));
            var batchable = new List<GameObject>();
            foreach (var filter in enhancedScenery.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh && filter.sharedMesh.isReadable && filter.GetComponent<MeshRenderer>())
                    batchable.Add(filter.gameObject);
            if (batchable.Count > 0) StaticBatchingUtility.Combine(batchable.ToArray(), enhancedScenery);
        }

        void BuildSceneryInteriorPanels(Material duct, Material warning, Material teal, Material amber)
        {
            var lowerPanel = Mat("Corridor enamel · charcoal petrol", new Color(.10f, .16f, .18f), .42f, .27f);
            var panelInset = Mat("Corridor panel · dark inset", new Color(.055f, .085f, .10f), .25f, .22f);
            var upperWall = new Material(Pbr("painted_plaster_wall"));
            upperWall.name = "Corridor mineral paint · slate grey";
            upperWall.color = new Color(.47f, .55f, .57f); ownedVisuals.Add(upperWall);
            var ceiling = new Material(upperWall);
            ceiling.name = "Service ceiling · graphite";
            ceiling.color = new Color(.25f, .31f, .33f); ownedVisuals.Add(ceiling);
            // Renderer-local remaps preserve original colliders, shared PBR assets and floor plan.
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name == "Interior partition") renderer.sharedMaterial = upperWall;
                if (renderer.name == "Warehouse ceiling") renderer.sharedMaterial = ceiling;
            }

            foreach (var wall in MissionFloorPlan.Walls)
            {
                if (wall.Bottom > .001 || wall.Height < 1.3) continue;
                bool alongZ = wall.Width < wall.Depth;
                bool interior = alongZ
                    ? Math.Abs(Math.Abs(wall.X) - 20) < .01 || Math.Abs(Math.Abs(wall.X) - 24) < .01
                    : Math.Abs(wall.Z + 1) < .01 || Math.Abs(wall.Z - 17) < .01 || Math.Abs(wall.Z - 35) < .01;
                if (!interior) continue;
                float width = (float)wall.Width, depth = (float)wall.Depth;
                float span = alongZ ? depth : width;
                if (span < .18f) continue;
                Vector3 at = new Vector3((float)wall.X, .64f, (float)wall.Z);
                Vector3 panelSize = alongZ ? new Vector3(width + .045f, 1.12f, depth - .045f) : new Vector3(width - .045f, 1.12f, depth + .045f);
                SceneryBox("Charcoal wall wainscot", at, panelSize, lowerPanel);
                Vector3 railSize = alongZ ? new Vector3(width + .072f, .065f, depth - .035f) : new Vector3(width - .035f, .065f, depth + .072f);
                SceneryBox("Wainscot brushed top rail", new Vector3(at.x, 1.23f, at.z), railSize, duct);
                SceneryBox("Wall recessed kick plate", new Vector3(at.x, .16f, at.z),
                    alongZ ? new Vector3(width + .063f, .16f, depth - .03f) : new Vector3(width - .03f, .16f, depth + .063f), panelInset);
                int divisions = Mathf.Max(1, Mathf.FloorToInt(span / 3.3f));
                for (int rib = 0; rib < divisions; rib++)
                {
                    float offset = -span * .5f + (rib + .5f) * span / divisions;
                    Vector3 ribAt = at + (alongZ ? Vector3.forward : Vector3.right) * offset;
                    ribAt.y = 2.42f;
                    SceneryBox("Mineral wall modular seam", ribAt,
                        alongZ ? new Vector3(width + .055f, 2.22f, .035f) : new Vector3(.035f, 2.22f, depth + .055f), panelInset);
                    ribAt.y = .68f;
                    SceneryBox("Wainscot panel joint", ribAt,
                        alongZ ? new Vector3(width + .063f, .91f, .028f) : new Vector3(.028f, .91f, depth + .063f), trim);
                }
            }

            foreach (int side in new[] { -1, 1 })
            {
                for (int bay = 0; bay < 4; bay++)
                {
                    float z = -10 + bay * 18;
                    foreach (float wallX in new[] { side * 20f, side * 24f })
                    {
                        // The existing opening is z +/-1.2 and y<2.6: all trim stays outside it.
                        foreach (int jamb in new[] { -1, 1 })
                        {
                            SceneryBox("Corridor portal steel jamb", new Vector3(wallX, 1.34f, z + jamb * 1.27f), new Vector3(.40f, 2.55f, .11f), trim);
                            SceneryBox("Portal amber edge insert", new Vector3(wallX, 1.16f, z + jamb * 1.27f), new Vector3(.425f, .72f, .035f), warning);
                        }
                        SceneryBox("Corridor portal header", new Vector3(wallX, 2.72f, z), new Vector3(.40f, .20f, 2.65f), lowerPanel);
                        SceneryBox("Portal cyan header accent", new Vector3(wallX, 2.72f, z), new Vector3(.425f, .033f, 1.90f), teal);
                        SceneryBox("Portal upper shadow reveal", new Vector3(wallX, 3.08f, z), new Vector3(.32f, .028f, 2.60f), panelInset);
                    }
                    // Imported cage lamps inside the transit corridor, on original solid walls.
                    if (bay % 2 == 0)
                        SceneryModel("industrial_wall_lamp", new Vector3(side * 20.21f, 3.35f, z + 5.2f), .52f,
                            Quaternion.Euler(0, side < 0 ? 90 : -90, 0));
                    SceneryBox("Portal floor threshold plate", new Vector3(side * 22f, .079f, z - 1.33f), new Vector3(3.38f, .012f, .16f), lowerPanel);
                    SceneryBox("Portal floor caution insert", new Vector3(side * 22f, .086f, z - 1.33f), new Vector3(.88f, .008f, .06f), warning);
                }

                foreach (float gutterX in new[] { side * 20.40f, side * 23.60f })
                {
                    SceneryBox("Floor service drain recess", new Vector3(gutterX, .079f, 16), new Vector3(.22f, .012f, 65), panelInset);
                    SceneryBox("Drain brushed steel edge", new Vector3(gutterX + .13f, .085f, 16), new Vector3(.025f, .012f, 65), duct);
                    for (int grate = 0; grate < 28; grate++)
                        SceneryBox("Drain cross grate", new Vector3(gutterX, .088f, -16 + grate * 2.35f), new Vector3(.19f, .012f, .035f), steel);
                }
                SceneryBox("Transit low guidance strip", new Vector3(side * 23.40f, .086f, 16), new Vector3(.027f, .008f, 65), teal);
                SceneryBox("Ceiling service duct", new Vector3(side * 21.05f, 4.90f, 16), new Vector3(.36f, .31f, 65), duct);
                for (int rib = 0; rib < 11; rib++)
                {
                    float z = -15 + rib * 6;
                    SceneryBox("Transit ceiling boxed rib", new Vector3(side * 22f, 4.54f, z), new Vector3(3.70f, .16f, .18f), lowerPanel);
                    SceneryBox("Ceiling rib warm service tab", new Vector3(side * 23.3f, 4.43f, z), new Vector3(.26f, .026f, .12f), amber);
                }
                ScenerySign(side < 0 ? "A / TRANSIT" : "B / TRANSIT", new Vector3(side * 20.22f, 2.18f, -3.3f),
                    Quaternion.Euler(0, side < 0 ? 90 : -90, 0), .055f, new Color(.69f, .78f, .76f));
            }
        }

        Material SceneryEmission(string name, Color color, float gain)
        {
            var material = Mat(name, color * .55f, .15f, .25f);
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * gain);
            return material;
        }

        GameObject SceneryBox(string name, Vector3 at, Vector3 size, Material material)
        {
            var item = Box(name, at, size, material, false);
            item.transform.SetParent(enhancedScenery.transform, true);
            return item;
        }

        Material SceneryPbr(string asset, bool isGlass)
        {
            string key = asset + (isGlass ? "_glass" : "");
            if (sceneryMaterials.TryGetValue(key, out var cached)) return cached;
            string path = "VisualUpgrade/FreeAssets/" + asset + "/" + key;
            var material = Mat("CC0 · " + key, Color.white, 0, .3f);
            material.mainTexture = Resources.Load<Texture2D>(path + "_diff_1k");
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(path + "_nor_gl_1k"));
            material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", .8f);
            material.SetTexture("_MetallicGlossMap", Resources.Load<Texture2D>(path + "_metallic_smoothness_1k"));
            material.EnableKeyword("_METALLICGLOSSMAP"); material.SetFloat("_GlossMapScale", .75f);
            material.SetTexture("_OcclusionMap", Resources.Load<Texture2D>(path + "_occlusion_1k"));
            material.SetFloat("_OcclusionStrength", .65f);
            if (asset == "industrial_wall_lamp")
            {
                material.EnableKeyword("_EMISSION");
                if (isGlass) material.SetColor("_EmissionColor", new Color(.8f, .54f, .24f) * 1.4f);
                else
                {
                    material.SetTexture("_EmissionMap", Resources.Load<Texture2D>("VisualUpgrade/FreeAssets/industrial_wall_lamp/industrial_wall_lamp_emissive_1k"));
                    material.SetColor("_EmissionColor", new Color(1f, .69f, .3f) * 1.5f);
                }
            }
            sceneryMaterials.Add(key, material);
            return material;
        }

        void SceneryModel(string asset, Vector3 at, float height, Quaternion facing)
        {
            var resource = Resources.Load<GameObject>("VisualUpgrade/FreeAssets/" + asset + "/model");
            if (!resource) { Debug.LogWarning("Optional scenery missing: " + asset); return; }
            var holder = new GameObject("CC0 " + asset);
            holder.transform.SetParent(enhancedScenery.transform, false);
            var model = Instantiate(resource, holder.transform, false);
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder); return; }
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = SceneryPbr(asset, asset == "industrial_wall_lamp" && i == 1);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= bounds.center;
            holder.transform.localScale = Vector3.one * (height / Mathf.Max(.001f, bounds.size.y));
            holder.transform.SetPositionAndRotation(at, facing);
        }

        void SceneryPipe(Vector3 start, Vector3 end, float radius, Material material)
        {
            if (!sceneryPipeMesh) sceneryPipeMesh = CreateSceneryPipeMesh();
            var item = new GameObject("Overhead service pipe");
            item.transform.SetParent(enhancedScenery.transform, false);
            item.transform.position = (start + end) * .5f;
            item.transform.rotation = Quaternion.FromToRotation(Vector3.up, end - start);
            item.transform.localScale = new Vector3(radius, (end - start).magnitude, radius);
            item.AddComponent<MeshFilter>().sharedMesh = sceneryPipeMesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void SceneryAuthoredUnit(Vector3 at, Quaternion facing)
        {
            var resource = Resources.Load<GameObject>("VisualUpgrade/Models/IndustrialServiceUnit");
            if (!resource) return;
            var holder = new GameObject("Authored auxiliary power station");
            holder.transform.SetParent(enhancedScenery.transform, false);
            var model = Instantiate(resource, holder.transform, false);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder); return; }
            foreach (var renderer in renderers)
            {
                string part = renderer.name;
                string key = part.Contains("Cyan") ? "station-glow" : part.Contains("Amber") ? "station-warning" :
                    part.Contains("Steel") ? "station-steel" : part.Contains("Rubber") ? "station-rubber" : "station-hull";
                if (!sceneryMaterials.TryGetValue(key, out var material))
                {
                    switch (key)
                    {
                        case "station-glow": material = SceneryEmission(key, new Color(.08f, .58f, .62f), 1.5f); break;
                        case "station-warning": material = Mat(key, new Color(.72f, .29f, .07f), .35f, .3f); break;
                        case "station-steel": material = Mat(key, new Color(.34f, .39f, .40f), .8f, .35f); break;
                        case "station-rubber": material = Mat(key, new Color(.025f, .032f, .034f), 0, .18f); break;
                        default: material = Mat(key, new Color(.10f, .14f, .15f), .7f, .3f); break;
                    }
                    sceneryMaterials.Add(key, material);
                }
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
            foreach (var collider in model.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= bounds.center;
            holder.transform.localScale = Vector3.one * (1.55f / Mathf.Max(.001f, bounds.size.y));
            holder.transform.SetPositionAndRotation(at, facing);
        }

        Mesh CreateSceneryPipeMesh()
        {
            const int sides = 12;
            var vertices = new Vector3[(sides + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                vertices[i * 2] = new Vector3(Mathf.Cos(a), -.5f, Mathf.Sin(a));
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(a), .5f, Mathf.Sin(a));
                uv[i * 2] = new Vector2(i / (float)sides, 0); uv[i * 2 + 1] = new Vector2(i / (float)sides, 1);
                if (i == sides) continue;
                int n = i * 2, t = i * 6;
                triangles[t] = n; triangles[t + 1] = n + 1; triangles[t + 2] = n + 2;
                triangles[t + 3] = n + 2; triangles[t + 4] = n + 1; triangles[t + 5] = n + 3;
            }
            var mesh = new Mesh { name = "Shared service pipe mesh" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            ownedVisuals.Add(mesh); return mesh;
        }

        void ScenerySign(string text, Vector3 at, Quaternion facing, float size, Color color)
        {
            var item = new GameObject("Terminal stencil · " + text);
            item.transform.SetParent(enhancedScenery.transform, false);
            item.transform.SetPositionAndRotation(at, facing);
            var label = item.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font.RequestCharactersInTexture(text, 64);
            label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.color = color;
            var material = new Material(Resources.Load<Shader>("Realism/WorldLabel"));
            material.mainTexture = label.font.material.mainTexture;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            ownedVisuals.Add(material);
        }
    }
}
