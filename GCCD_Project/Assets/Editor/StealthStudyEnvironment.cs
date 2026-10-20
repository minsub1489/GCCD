using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GCCD.Editor
{
    /// <summary>
    /// Procedurally built industrial security test chamber. The center radius < 8.8m
    /// is empty above floor level, preserving the original spawn and line-of-sight rules.
    /// No remotely hosted assets, proprietary game content, or source audio are required.
    /// </summary>
    public static class StealthStudyEnvironment
    {
        private static Transform root;
        private static Material concrete, metal, darkMetal, wallPaint, warningPaint, hazard, pipe, glowingRed, glowingCyan, glowingAmber, blackGlass;

        public static void Generate(Transform firstPersonCamera)
        {
            root = new GameObject("SECTOR 07  /  INDUSTRIAL INFILTRATION TEST SITE").transform;
            PrepareMaterials();
            BuildFloor();
            BuildArchitecture();
            BuildUtilityInfrastructure();
            BuildLighting();
            BuildOperativeWeapon(firstPersonCamera);

            var effects = root.gameObject.AddComponent<GCCD.Visuals.StealthVisualEffects>();
            effects.ambientLightA = GameObject.Find("Red Security Beacon West").GetComponent<Light>();
            effects.ambientLightB = GameObject.Find("Red Security Beacon East").GetComponent<Light>();

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.15f, .21f, .25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.065f, .085f, .096f);
            RenderSettings.fogDensity = .012f;
            QualitySettings.shadowDistance = 45f;
            Debug.Log("GCCD_STEALTH_ENVIRONMENT_CREATED central_spawning_clearance_m=8.8");
        }

        private static Material Material(string name, Color baseColor, float metallic, float smoothness, bool emission = false)
        {
            var shader = Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Unity Standard shader missing");
            var material = new Material(shader) { name = name, color = baseColor };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", baseColor * 2.7f);
            }
            return material;
        }

        private static void PrepareMaterials()
        {
            concrete = Material("Concrete | weathered graphite", new Color(.22f, .26f, .26f), .04f, .12f);
            metal = Material("Steel | worn gunmetal", new Color(.24f, .31f, .34f), .69f, .47f);
            darkMetal = Material("Armor | graphite", new Color(.082f, .11f, .14f), .72f, .4f);
            wallPaint = Material("Wall | institutional jade grey", new Color(.21f, .31f, .30f), .26f, .17f);
            warningPaint = Material("SafetyOchre | dull construction paint", new Color(.7f, .48f, .12f), .3f, .24f);
            hazard = Material("SafetyRed | industrial danger marking", new Color(.63f, .17f, .14f), .35f, .32f);
            pipe = Material("Utilities | cold metal", new Color(.19f, .22f, .25f), .6f, .58f);
            glowingRed = Material("Beacon | emergency red", new Color(1f, .12f, .07f), .1f, .5f, true);
            glowingCyan = Material("Operations | pale blue screen", new Color(.13f, .55f, .73f), .1f, .63f, true);
            glowingAmber = Material("Lights | warm sodium", new Color(1f, .62f, .25f), .15f, .45f, true);
            blackGlass = Material("Glass | black surveillance", new Color(.028f, .055f, .075f), .52f, .85f);

            // Deterministic weathered concrete texture; adds material depth without external licenses.
            const string texturePath = "Assets/Art/Textures/ConcreteGrunge.png";
            Directory.CreateDirectory("Assets/Art/Textures");
            if (!File.Exists(texturePath))
            {
                const int n = 512;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color32[n * n];
                var rand = new System.Random(17032026);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float coarse = Mathf.PerlinNoise(x / 72f, y / 72f) * 35f;
                    float fine = Mathf.PerlinNoise(x / 9f, y / 9f) * 25f;
                    int noise = rand.Next(-13, 14);
                    bool seam = x % 256 < 3 || y % 256 < 3;
                    byte value = (byte)Mathf.Clamp(104 + coarse + fine + noise - (seam ? 58f : 0f), 16, 240);
                    pixels[y * n + x] = new Color32(value, value, (byte)Mathf.Clamp(value + 4, 0, 255), 255);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D grunge = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            concrete.mainTexture = grunge;
            concrete.mainTextureScale = new Vector2(14, 14);
            wallPaint.mainTexture = grunge;
            wallPaint.mainTextureScale = new Vector2(4, 4);

            Directory.CreateDirectory("Assets/Art/Materials");
            SaveMaterial(concrete); SaveMaterial(metal); SaveMaterial(darkMetal); SaveMaterial(wallPaint);
            SaveMaterial(warningPaint); SaveMaterial(hazard); SaveMaterial(pipe);
            SaveMaterial(glowingRed); SaveMaterial(glowingCyan); SaveMaterial(glowingAmber); SaveMaterial(blackGlass);
        }

        private static void SaveMaterial(Material material)
        {
            var clean = material.name.Split('|')[0].Trim().Replace(' ', '_');
            string path = "Assets/Art/Materials/" + clean + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                UnityEngine.Object.DestroyImmediate(material);
                if (ReferenceEquals(concrete, material)) concrete = existing;
                if (ReferenceEquals(metal, material)) metal = existing;
                if (ReferenceEquals(darkMetal, material)) darkMetal = existing;
                if (ReferenceEquals(wallPaint, material)) wallPaint = existing;
                if (ReferenceEquals(warningPaint, material)) warningPaint = existing;
                if (ReferenceEquals(hazard, material)) hazard = existing;
                if (ReferenceEquals(pipe, material)) pipe = existing;
                if (ReferenceEquals(glowingRed, material)) glowingRed = existing;
                if (ReferenceEquals(glowingCyan, material)) glowingCyan = existing;
                if (ReferenceEquals(glowingAmber, material)) glowingAmber = existing;
                if (ReferenceEquals(blackGlass, material)) blackGlass = existing;
            }
            else AssetDatabase.CreateAsset(material, path);
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool collider = false, Transform parent = null)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent ?? root, false);
            obj.transform.position = position;
            obj.transform.localScale = size;
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static GameObject Tube(string name, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void BuildFloor()
        {
            Box("Concrete Base • cleared arena", new Vector3(0, -.24f, 0), new Vector3(62, .44f, 62), concrete, true);
            // Quiet floor panel grids create motion cues without obstructing research targets.
            for (int i = -9; i <= 9; i++)
            {
                Box("Panel joint east/west", new Vector3(i * 2, -.017f, 0), new Vector3(.023f, .008f, 37f), darkMetal);
                Box("Panel joint north/south", new Vector3(0, -.016f, i * 2), new Vector3(37f, .009f, .023f), darkMetal);
            }
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                float angle = quadrant * 90f;
                Quaternion spin = Quaternion.Euler(0, angle, 0);
                for (int distance = 9; distance <= 23; distance += 2)
                {
                    Vector3 position = spin * new Vector3(0, .02f, distance);
                    GameObject line = Box("Maintenance lane safety paint", position, new Vector3(.11f, .017f, .85f),
                        distance % 4 == 1 ? warningPaint : wallPaint);
                    line.transform.rotation = spin;
                }
            }
            // A thin hazard boundary is VISUAL ONLY, never a collider that might invalidate a spawn.
            for (int n = 0; n < 48; n++)
            {
                float angle = n * Mathf.PI * 2 / 48f;
                float radius = 8.75f;
                GameObject stripe = Box("Tactical arena boundary / marking", new Vector3(Mathf.Sin(angle) * radius, .019f, Mathf.Cos(angle) * radius),
                    new Vector3(.12f, .014f, .8f), n % 3 == 0 ? warningPaint : metal);
                stripe.transform.rotation = Quaternion.Euler(0, n * 360f / 48f, 0);
            }
        }

        private static void BuildArchitecture()
        {
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0, side * 90, 0);
                Action<string, float, float, float, float, float, float, Material, bool> local =
                    (name, x, y, z, sx, sy, sz, mat, coll) =>
                    {
                        var obj = Box(name, q * new Vector3(x, y, z), new Vector3(sx, sy, sz), mat, coll);
                        obj.transform.rotation = q;
                    };

                local("Blastproof perimeter wall L", -10.5f, 3.1f, 14f, 15f, 6.2f, .85f, wallPaint, true);
                local("Blastproof perimeter wall R", 10.5f, 3.1f, 14f, 15f, 6.2f, .85f, wallPaint, true);
                local("Gateway header", 0, 5.55f, 14f, 6.15f, 1.32f, 1.15f, darkMetal, true);
                local("Gateway left steel jamb", -3.25f, 2.85f, 13.8f, .65f, 5.7f, 1.4f, metal, true);
                local("Gateway right steel jamb", 3.25f, 2.85f, 13.8f, .65f, 5.7f, 1.4f, metal, true);
                local("Gateway caution beam", 0, 4.95f, 13.3f, 5.9f, .13f, .19f, warningPaint, false);
                local("Armored lintel red inlay", 0, 5.7f, 13.24f, 4.8f, .16f, .13f, glowingRed, false);
                local("Ceiling return beams", 0, 6.8f, 17.5f, 6.4f, .42f, 12f, metal, false);

                // Narrow utility tunnel beyond each gate: visual perspective, no close-range threat obstruction.
                for (int i = 0; i < 3; i++)
                {
                    float z = 17f + i * 4f;
                    local("Tunnel rib left", -3.1f, 3.1f, z, .4f, 5.9f, .48f, metal, true);
                    local("Tunnel rib right", 3.1f, 3.1f, z, .4f, 5.9f, .48f, metal, true);
                    local("Tunnel rib lintel", 0, 6f, z, 6.2f, .4f, .48f, darkMetal, false);
                    local("Tunnel overhead luminous strip", 0, 5.74f, z - .16f, 3.9f, .035f, .1f, glowingCyan, false);
                }
                local("Distant armored locked exit", 0, 2.6f, 29.1f, 6.1f, 5.2f, .55f, darkMetal, true);
                local("Distant exit striped lower edge", 0, .4f, 28.7f, 5f, .16f, .11f, hazard, false);

                // Repeated wall panel details, ribs and pipes create scale and material richness.
                for (int i = -4; i <= 4; i++)
                {
                    if (Mathf.Abs(i) == 1 || i == 0) continue;
                    float x = i * 3.1f;
                    local("Reinforced panel vertical division", x, 3.1f, 13.45f, .12f, 5.85f, .17f, metal, false);
                    local("Concrete armored panel insert", x, 3.5f, 13.51f, 2.65f, 3.9f, .07f, concrete, false);
                    local("Lower brushed-metal wainscot", x, 1f, 13.42f, 2.65f, 1.8f, .16f, darkMetal, false);
                    local("Rusted safety paint cap", x, 2.0f, 13.3f, 2.63f, .055f, .07f, warningPaint, false);
                }
            }

            // Elevated dark service walkways, entirely above the target plane.
            for (int direction = 0; direction < 4; direction++)
            {
                Quaternion q = Quaternion.Euler(0f, direction * 90f, 0f);
                for (int n = -3; n <= 3; n++)
                {
                    var p = q * new Vector3(n * 1.4f, 5.5f, 10.8f);
                    var railing = Box("Catwalk railing / square tubing", p, new Vector3(.08f, 1.3f, .08f), pipe);
                    railing.transform.rotation = q;
                }
                var ledge = Box("Maintenance catwalk / metal grate", q * new Vector3(0f, 4.75f, 10.8f), new Vector3(9.6f, .24f, 1.6f), darkMetal);
                ledge.transform.rotation = q;
                var upper = Box("Maintenance catwalk / horizontal handrail", q * new Vector3(0f, 6.18f, 10.7f), new Vector3(9.9f, .10f, .12f), pipe);
                upper.transform.rotation = q;
            }
        }

        private static void BuildUtilityInfrastructure()
        {
            // Decorations stay outside the 8.8m interaction bubble to avoid collision bias.
            for (int n = 0; n < 12; n++)
            {
                float angle = n * 30 * Mathf.Deg2Rad;
                float radius = n % 2 == 0 ? 10.15f : 11.4f;
                Vector3 center = new Vector3(Mathf.Cos(angle) * radius, .52f, Mathf.Sin(angle) * radius);
                if (n % 3 == 0)
                {
                    var crate = Box("Armored logistics supply crate", center, new Vector3(1.25f, 1.1f, 1.25f), darkMetal, true);
                    crate.transform.rotation = Quaternion.Euler(0, n * 23, 0);
                    Box("Supply crate red caution cap", center + Vector3.up * .56f, new Vector3(1.05f, .08f, 1.05f), hazard);
                    for (int stripe = 0; stripe < 3; stripe++)
                        Box("Cargo band hardware", center + Vector3.up * (-.30f + stripe * .30f), new Vector3(1.37f, .048f, 1.37f), metal);
                }
                else
                {
                    var tank = Tube("Vertical pressurized cooling tank", center + Vector3.up * .85f,
                        new Vector3(.44f, 1.4f, .44f), Quaternion.identity, metal);
                    Tube("Tank valve collar", tank.transform.position + Vector3.up * 1.42f, new Vector3(.3f, .12f, .3f), Quaternion.identity, warningPaint);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Quaternion q = Quaternion.Euler(0, i * 90, 0);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 pos = q * new Vector3(-4.2f + j * 4.2f, 5.3f, 13.3f);
                    Tube("HVAC copper conduit above perimeter", pos, new Vector3(.16f, 2.8f, .16f),
                        q * Quaternion.Euler(90, 0, 0), j == 1 ? warningPaint : pipe);
                }
                // Active-looking CCTV equipment pointed toward the center.
                Vector3 mount = q * new Vector3(4.3f, 4.5f, 12.9f);
                var bracket = Box("CCTV articulated mount", mount, new Vector3(.45f, .14f, .65f), metal);
                bracket.transform.rotation = q;
                var lens = Box("CCTV sensor module", mount + q * new Vector3(0, -.12f, -.40f), new Vector3(.35f, .22f, .54f), blackGlass);
                lens.transform.rotation = q;
                var diode = Box("CCTV armed status diode", mount + q * new Vector3(0, -.13f, -.73f), new Vector3(.08f, .08f, .045f), glowingRed);
                diode.transform.rotation = q;

                // Amber warning beacon and paired data monitors beyond the original threat radius.
                Box("Industrial operator control panel", q * new Vector3(-4.6f, 2.4f, 12.9f), new Vector3(1.55f, 1.3f, .28f), blackGlass);
                Box("Control panel scanning display", q * new Vector3(-4.6f, 2.45f, 12.72f), new Vector3(1.26f, .86f, .042f), glowingCyan);
                for (int j = 0; j < 6; j++)
                    Box("Monitor telemetry bars", q * new Vector3(-5.05f + j * .17f, 2.26f + (j % 3) * .15f, 12.69f),
                        new Vector3(.065f, .04f + j * .015f, .015f), darkMetal);
            }
        }

        private static Light Light(string name, Vector3 pos, Color color, float intensity, float range, LightType type, float spotAngle = 80)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(root, false);
            obj.transform.position = pos;
            Light light = obj.AddComponent<Light>();
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.type = type;
            light.spotAngle = spotAngle;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void BuildLighting()
        {
            var overhead = Light("Cold floodlight / primary", new Vector3(0, 6.2f, 2), new Color(.42f, .68f, .82f), 2.8f, 18f, LightType.Spot, 102f);
            overhead.transform.rotation = Quaternion.Euler(85f, 0, 0);
            Box("Overhead grated cold floodlight fixture", new Vector3(0, 6.28f, 2), new Vector3(2.4f, .22f, .85f), darkMetal);
            Box("Overhead fluorescent tube", new Vector3(0, 6.14f, 2), new Vector3(2.0f, .03f, .35f), glowingCyan);

            for (int i = 0; i < 4; i++)
            {
                Quaternion q = Quaternion.Euler(0, i * 90f, 0);
                Vector3 pos = q * new Vector3(0, 5.45f, 12f);
                var spot = Light("Corridor spill light " + i, pos, i % 2 == 0 ? new Color(.28f, .69f, .95f) : new Color(.9f, .55f, .28f),
                    2.5f, 18f, LightType.Spot, 70f);
                spot.transform.rotation = Quaternion.LookRotation((Vector3.zero - pos).normalized, Vector3.up);
                Box("Corridor strip light grille", pos + Vector3.up * .1f, new Vector3(2.2f, .22f, .8f), darkMetal);
                Box("Corridor strip illuminated optic", pos - Vector3.up * .08f, new Vector3(1.8f, .035f, .42f), i % 2 == 0 ? glowingCyan : glowingAmber);
            }

            var redA = Light("Red Security Beacon West", new Vector3(-11.4f, 4.25f, 10.5f), new Color(1f, .15f, .1f), 1.5f, 11f, LightType.Point);
            var redB = Light("Red Security Beacon East", new Vector3(11.4f, 4.25f, -10.5f), new Color(1f, .14f, .08f), 1.5f, 11f, LightType.Point);
            Box("Security beacon red lens west", redA.transform.position, Vector3.one * .27f, glowingRed);
            Box("Security beacon red lens east", redB.transform.position, Vector3.one * .27f, glowingRed);
            Light("Emergency pale bounce", new Vector3(1, 4, -5), new Color(.13f, .4f, .58f), 1.5f, 16f, LightType.Point);
        }

        private static void BuildOperativeWeapon(Transform camera)
        {
            var mount = new GameObject("OPERATIVE / FIRST PERSON CARBINE / visual only");
            mount.transform.SetParent(camera, false);
            mount.transform.localPosition = new Vector3(.35f, -.40f, .69f);
            mount.transform.localRotation = Quaternion.Euler(-5f, -1f, -4f);
            var model = mount.AddComponent<GCCD.Visuals.StealthViewmodelEffects>();
            model.cameraTransform = camera;

            // A procedural submachine-gun silhouette: barrel, rail, optic, stock and gloved hands.
            Action<string, Vector3, Vector3, Material> part = (name, p, scale, mat) =>
            {
                var obj = Box(name, Vector3.zero, scale, mat, false, mount.transform);
                obj.transform.localPosition = p;
                obj.transform.localRotation = Quaternion.identity;
            };
            part("CARBINE / upper receiver", new Vector3(0, .0f, .1f), new Vector3(.13f, .145f, .49f), darkMetal);
            part("CARBINE / side steel housing", new Vector3(.065f, 0, .07f), new Vector3(.024f, .09f, .29f), metal);
            part("CARBINE / lower trigger assembly", new Vector3(0, -.095f, -.06f), new Vector3(.085f, .11f, .20f), darkMetal);
            part("CARBINE / magazine", new Vector3(0, -.185f, .16f), new Vector3(.081f, .19f, .13f), darkMetal);
            part("CARBINE / handguard", new Vector3(0, .0f, .43f), new Vector3(.125f, .12f, .30f), metal);
            part("CARBINE / rail", new Vector3(0, .094f, .30f), new Vector3(.105f, .025f, .38f), blackGlass);
            part("CARBINE / holographic sight case", new Vector3(0, .145f, .1f), new Vector3(.094f, .10f, .11f), darkMetal);
            part("CARBINE / holographic glass", new Vector3(0, .153f, .1f), new Vector3(.059f, .056f, .014f), blackGlass);
            part("CARBINE / barrel", new Vector3(0, .015f, .71f), new Vector3(.058f, .058f, .35f), darkMetal);
            part("CARBINE / suppressor", new Vector3(0, .015f, .94f), new Vector3(.083f, .083f, .32f), blackGlass);
            part("CARBINE / muzzle ring", new Vector3(0, .015f, 1.102f), new Vector3(.088f, .09f, .02f), metal);
            part("CARBINE / rear stock", new Vector3(0, -.025f, -.31f), new Vector3(.11f, .14f, .34f), darkMetal);
            part("GLOVE / support arm", new Vector3(-.13f, -.27f, .29f), new Vector3(.19f, .18f, .35f), wallPaint);
            part("GLOVE / right arm", new Vector3(.11f, -.31f, -.04f), new Vector3(.20f, .23f, .32f), wallPaint);
            var muzzleLight = new GameObject("Muzzle flash illumination - short camera recoil only");
            muzzleLight.transform.SetParent(mount.transform, false);
            muzzleLight.transform.localPosition = new Vector3(0, .015f, 1.12f);
            var light = muzzleLight.AddComponent<Light>();
            light.color = new Color(1f, .5f, .15f);
            light.range = 2f;
            light.intensity = 0;
            model.muzzleLight = light;
        }
    }
}
