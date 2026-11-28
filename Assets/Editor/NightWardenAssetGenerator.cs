#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NightWarden.Player;
using NightWarden.SLAM;

namespace NightWarden.EditorTools
{
    /// <summary>
    /// AAA Tactical Vigilante visual generator and lighting setup for Night Warden: Echo Protocol.
    /// Creates realistic PBR materials, normal maps, cowl and segmented armor pieces,
    /// dynamic cape sway, cinematic moonlight, cyan rim light, post-processing volume,
    /// and wet rain city atmosphere.
    /// </summary>
    public static class NightWardenAssetGenerator
    {
        private const string TexturesPath = "Assets/Textures";
        private const string MaterialsPath = "Assets/Materials";
        private const string SettingsPath = "Assets/Settings";

        [MenuItem("Night Warden/Generate Tactical Vigilante & Cinematic Lighting", false, 1)]
        public static void GenerateAllVisuals()
        {
            EnsureDirectories();
            GenerateNormalMaps();
            CreateTacticalMaterials();
            ConfigureShadowDistance(40f);
            CreateGlobalVolumeProfile();
            BuildTacticalVigilanteAndLightingInActiveScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00e5ff><b>[Night Warden]</b> Tactical Vigilante visual overhaul and cinematic lighting setup complete!</color>");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(TexturesPath)) Directory.CreateDirectory(TexturesPath);
            if (!Directory.Exists(MaterialsPath)) Directory.CreateDirectory(MaterialsPath);
            if (!Directory.Exists(SettingsPath)) Directory.CreateDirectory(SettingsPath);
        }

        public static void GenerateNormalMaps()
        {
            string weavePath = Path.Combine(TexturesPath, "T_TacticalWeave_Normal.png");
            if (!File.Exists(weavePath))
            {
                Texture2D weaveTex = CreateCarbonWeaveNormalTexture(256, 256);
                byte[] bytes = weaveTex.EncodeToPNG();
                File.WriteAllBytes(weavePath, bytes);
                AssetDatabase.ImportAsset(weavePath);
                ConfigureAsNormalMap(weavePath);
            }

            string platePath = Path.Combine(TexturesPath, "T_ArmorPlate_Normal.png");
            if (!File.Exists(platePath))
            {
                Texture2D plateTex = CreateArmorPlateNormalTexture(256, 256);
                byte[] bytes = plateTex.EncodeToPNG();
                File.WriteAllBytes(platePath, bytes);
                AssetDatabase.ImportAsset(platePath);
                ConfigureAsNormalMap(platePath);
            }

            string asphaltPath = Path.Combine(TexturesPath, "T_Asphalt_Normal.png");
            if (!File.Exists(asphaltPath))
            {
                Texture2D asphaltTex = CreateAsphaltNormalTexture(256, 256);
                byte[] bytes = asphaltTex.EncodeToPNG();
                File.WriteAllBytes(asphaltPath, bytes);
                AssetDatabase.ImportAsset(asphaltPath);
                ConfigureAsNormalMap(asphaltPath);
            }
        }

        private static void ConfigureAsNormalMap(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D CreateCarbonWeaveNormalTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width * 32f;
                    float v = (float)y / height * 32f;
                    float pattern = Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Cos(v * Mathf.PI * 2f);

                    float nx = pattern * 0.18f;
                    float ny = -pattern * 0.18f;
                    Vector3 n = new Vector3(nx, ny, 1f).normalized;

                    // Pack into tangent space normal (0..1 range)
                    Color c = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateArmorPlateNormalTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    // Beveled border effect around square armor module
                    float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float bevel = Mathf.Clamp01(edgeDist * 16f);

                    float dx = (u > 0.5f ? -1f : 1f) * (1f - bevel) * 0.35f;
                    float dy = (v > 0.5f ? -1f : 1f) * (1f - bevel) * 0.35f;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;

                    Color c = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateAsphaltNormalTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float scale1 = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    float scale2 = Mathf.PerlinNoise(x * 0.45f + 50f, y * 0.45f + 50f);
                    float h = scale1 * 0.7f + scale2 * 0.3f;

                    float hR = Mathf.PerlinNoise((x + 1) * 0.15f, y * 0.15f);
                    float hU = Mathf.PerlinNoise(x * 0.15f, (y + 1) * 0.15f);

                    float dx = (h - hR) * 1.5f;
                    float dy = (h - hU) * 1.5f;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;

                    Color c = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static void CreateTacticalMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Texture2D weaveNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TexturesPath, "T_TacticalWeave_Normal.png"));
            Texture2D armorNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TexturesPath, "T_ArmorPlate_Normal.png"));
            Texture2D asphaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TexturesPath, "T_Asphalt_Normal.png"));

            // 1. Tactical Fabric (Charcoal, Smoothness 0.35, Metallic 0.0)
            SaveMaterial("Mat_TacticalFabric.mat", litShader, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.075f, 0.082f, 0.095f, 1.0f)); // #131518
                mat.SetFloat("_Smoothness", 0.35f);
                mat.SetFloat("_Metallic", 0.0f);
                if (weaveNormal != null)
                {
                    mat.SetTexture("_BumpMap", weaveNormal);
                    mat.SetFloat("_BumpScale", 0.65f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            });

            // 2. Armor Plates (Dark Matte Slate, Smoothness 0.60, Metallic 0.50)
            SaveMaterial("Mat_ArmorPlates.mat", litShader, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.095f, 0.106f, 0.125f, 1.0f)); // #181B20
                mat.SetFloat("_Smoothness", 0.60f);
                mat.SetFloat("_Metallic", 0.50f);
                if (armorNormal != null)
                {
                    mat.SetTexture("_BumpMap", armorNormal);
                    mat.SetFloat("_BumpScale", 0.85f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            });

            // 3. Utility Belt & Buckles (Dark Gunmetal, Smoothness 0.65, Metallic 0.75)
            SaveMaterial("Mat_UtilityBeltBuckles.mat", litShader, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.13f, 0.15f, 0.18f, 1.0f));
                mat.SetFloat("_Smoothness", 0.65f);
                mat.SetFloat("_Metallic", 0.75f);
            });

            // 4. Cowl Eye Lenses (Emissive Angular White Glow)
            SaveMaterial("Mat_EyeLenses.mat", litShader, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.92f, 0.96f, 1.0f, 1.0f));
                mat.SetFloat("_Smoothness", 0.90f);
                mat.SetFloat("_Metallic", 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1.8f, 2.2f, 2.6f) * 1.5f); // Ice cold emissive glow
            });

            // 5. Vigilante Cape (Double-sided dark fabric, subtle sheen)
            SaveMaterial("Mat_VigilanteCape.mat", litShader, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.055f, 0.062f, 0.075f, 1.0f)); // Midnight charcoal
                mat.SetFloat("_Smoothness", 0.35f);
                mat.SetFloat("_Metallic", 0.05f);
                mat.SetFloat("_Cull", 0f); // Double-sided rendering
                if (weaveNormal != null)
                {
                    mat.SetTexture("_BumpMap", weaveNormal);
                    mat.SetFloat("_BumpScale", 0.5f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            });

            // 6. Wet Asphalt Ground (High smoothness for rain puddle reflections)
            SaveMaterial("Mat_WetAsphalt.mat", litShader, mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.045f, 0.052f, 0.065f, 1.0f));
                mat.SetFloat("_Smoothness", 0.85f);
                mat.SetFloat("_Metallic", 0.15f);
                if (asphaltNormal != null)
                {
                    mat.SetTexture("_BumpMap", asphaltNormal);
                    mat.SetFloat("_BumpScale", 1.0f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            });
        }

        private static void SaveMaterial(string fileName, Shader shader, System.Action<Material> configure)
        {
            string path = Path.Combine(MaterialsPath, fileName);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                configure(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                configure(mat);
                EditorUtility.SetDirty(mat);
            }
        }

        public static void ConfigureShadowDistance(float distance)
        {
            string pcAssetPath = "Assets/Settings/PC_RPAsset.asset";
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pcAssetPath);
            if (urpAsset != null)
            {
                urpAsset.shadowDistance = distance;
                EditorUtility.SetDirty(urpAsset);
            }
        }

        public static void CreateGlobalVolumeProfile()
        {
            string profilePath = Path.Combine(SettingsPath, "NightWardenVolumeProfile.asset");
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            // 1. Bloom
            var bloom = GetOrAddVolumeComponent<Bloom>(profile, profilePath);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.9f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.5f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.7f;

            // 2. Vignette
            var vignette = GetOrAddVolumeComponent<Vignette>(profile, profilePath);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.35f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;

            // 3. Film Grain
            var filmGrain = GetOrAddVolumeComponent<FilmGrain>(profile, profilePath);
            filmGrain.type.overrideState = true;
            filmGrain.type.value = FilmGrainLookup.Thin1;
            filmGrain.intensity.overrideState = true;
            filmGrain.intensity.value = 0.15f;

            // 4. Color Adjustments
            var colorAdjust = GetOrAddVolumeComponent<ColorAdjustments>(profile, profilePath);
            colorAdjust.saturation.overrideState = true;
            colorAdjust.saturation.value = -15f; // Slight gritty desaturation
            colorAdjust.colorFilter.overrideState = true;
            colorAdjust.colorFilter.value = new Color(0.85f, 0.92f, 1.0f); // Cold blue-teal tint
            colorAdjust.contrast.overrideState = true;
            colorAdjust.contrast.value = 12f;

            // 5. Tonemapping
            var tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile, profilePath);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            // 6. Depth of Field
            var dof = GetOrAddVolumeComponent<DepthOfField>(profile, profilePath);
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.overrideState = true;
            dof.gaussianStart.value = 14f;
            dof.gaussianEnd.overrideState = true;
            dof.gaussianEnd.value = 45f;

            // 7. Motion Blur
            var motionBlur = GetOrAddVolumeComponent<MotionBlur>(profile, profilePath);
            motionBlur.mode.overrideState = true;
            motionBlur.mode.value = MotionBlurMode.CameraAndObjects;
            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value = 0.18f;
            motionBlur.quality.overrideState = true;
            motionBlur.quality.value = MotionBlurQuality.Low;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile, string assetPath) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var comp) && comp != null) return comp;
            comp = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(comp, assetPath);
            return comp;
        }

        public static void BuildTacticalVigilanteAndLightingInActiveScene()
        {
            // Load Materials
            Material matFabric = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialsPath, "Mat_TacticalFabric.mat"));
            Material matArmor = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialsPath, "Mat_ArmorPlates.mat"));
            Material matBuckles = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialsPath, "Mat_UtilityBeltBuckles.mat"));
            Material matEyes = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialsPath, "Mat_EyeLenses.mat"));
            Material matCape = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialsPath, "Mat_VigilanteCape.mat"));
            Material matWetAsphalt = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialsPath, "Mat_WetAsphalt.mat"));

            // 1. Atmosphere: RenderSettings Fog
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.04f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.025f, 0.038f, 0.065f);
            RenderSettings.fogDensity = 0.024f;

            // 2. Moonlight Directional Light
            GameObject moonObj = GameObject.Find("Directional_Moonlight");
            if (moonObj == null) moonObj = new GameObject("Directional_Moonlight");
            var moonLight = moonObj.GetComponent<Light>();
            if (moonLight == null) moonLight = moonObj.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.48f, 0.68f, 0.95f); // Cold blue moonlight
            moonLight.intensity = 0.28f; // Low intensity so district remains realistically dark
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowNormalBias = 0.4f;
            moonObj.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            // 3. Update Street Ground Material to Wet Asphalt
            GameObject ground = GameObject.Find("Street_Ground");
            if (ground != null && matWetAsphalt != null)
            {
                ground.GetComponent<MeshRenderer>().sharedMaterial = matWetAsphalt;
            }

            // 4. Global Volume
            GameObject volObj = GameObject.Find("Global_PostProcessingVolume");
            if (volObj == null) volObj = new GameObject("Global_PostProcessingVolume");
            var volume = volObj.GetComponent<Volume>();
            if (volume == null) volume = volObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Path.Combine(SettingsPath, "NightWardenVolumeProfile.asset"));

            // 5. Reflection Probe
            GameObject probeObj = GameObject.Find("District_ReflectionProbe");
            if (probeObj == null) probeObj = new GameObject("District_ReflectionProbe");
            var refProbe = probeObj.GetComponent<ReflectionProbe>();
            if (refProbe == null) refProbe = probeObj.AddComponent<ReflectionProbe>();
            refProbe.mode = ReflectionProbeMode.Baked;
            refProbe.boxProjection = true;
            refProbe.size = new Vector3(120f, 30f, 120f);
            refProbe.center = new Vector3(0f, 10f, 0f);
            refProbe.intensity = 0.65f;

            // 6. Light Probes for Dynamic Player & Guard Shading
            GameObject probeGroupObj = GameObject.Find("District_LightProbeGroup");
            if (probeGroupObj == null)
            {
                probeGroupObj = new GameObject("District_LightProbeGroup");
                var lpg = probeGroupObj.AddComponent<LightProbeGroup>();
                List<Vector3> probePositions = new List<Vector3>();
                for (float x = -35f; x <= 35f; x += 14f)
                {
                    for (float z = -45f; z <= 45f; z += 15f)
                    {
                        probePositions.Add(new Vector3(x, 0.5f, z));
                        probePositions.Add(new Vector3(x, 2.5f, z));
                    }
                }
                lpg.probePositions = probePositions.ToArray();
            }

            // 7. Player Character Overhaul
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                // CRUCIAL: Set Player to Layer 2 ("Ignore Raycast") so Echo Pulse never hits player
                SetLayerRecursively(playerObj, 2);

                // Configure EchoPulse hitLayers to explicitly ignore Layer 2
                var pulse = playerObj.GetComponent<EchoPulse>();
                if (pulse != null)
                {
                    var hitLayersProp = typeof(EchoPulse).GetField("hitLayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    int maskWithoutPlayer = ~((1 << 2) | (1 << 3)); // Ignore Raycast & Water
                    hitLayersProp?.SetValue(pulse, (LayerMask)maskWithoutPlayer);
                }

                // Remove existing simple capsule body if present
                Transform oldBody = playerObj.transform.Find("Vigilante_Body");
                if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);

                Transform oldCape = playerObj.transform.Find("Vigilante_Cape");
                if (oldCape != null) Object.DestroyImmediate(oldCape.gameObject);

                Transform oldRim = playerObj.transform.Find("Player_RimLight");
                if (oldRim != null) Object.DestroyImmediate(oldRim.gameObject);

                // Build New Grounded Tactical Vigilante Visual Rig
                GameObject rigRoot = new GameObject("Vigilante_VisualRig");
                rigRoot.transform.SetParent(playerObj.transform, false);
                rigRoot.transform.localPosition = Vector3.zero;
                rigRoot.transform.localRotation = Quaternion.identity;

                // --- Base Body / Under-suit (Dark Tactical Fabric) ---
                GameObject torso = CreateVisualChild("Torso_Base", rigRoot.transform, PrimitiveType.Cube, 
                    new Vector3(0f, 1.15f, 0f), new Vector3(0.48f, 0.65f, 0.28f), matFabric);

                GameObject pelvis = CreateVisualChild("Pelvis_Base", rigRoot.transform, PrimitiveType.Cube, 
                    new Vector3(0f, 0.85f, 0f), new Vector3(0.44f, 0.22f, 0.26f), matFabric);

                // --- Segmented Chest Armor Plate (Smoothness 0.6, Metallic 0.5) ---
                GameObject chestPlate = CreateVisualChild("Armor_ChestPlate", torso.transform, PrimitiveType.Cube,
                    new Vector3(0f, 0.12f, 0.14f), new Vector3(0.46f, 0.32f, 0.08f), matArmor);

                GameObject abPlate1 = CreateVisualChild("Armor_AbPlate_Upper", torso.transform, PrimitiveType.Cube,
                    new Vector3(0f, -0.10f, 0.13f), new Vector3(0.38f, 0.10f, 0.07f), matArmor);

                GameObject abPlate2 = CreateVisualChild("Armor_AbPlate_Lower", torso.transform, PrimitiveType.Cube,
                    new Vector3(0f, -0.22f, 0.12f), new Vector3(0.34f, 0.09f, 0.06f), matArmor);

                GameObject backPlate = CreateVisualChild("Armor_BackPlate", torso.transform, PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, -0.14f), new Vector3(0.44f, 0.40f, 0.07f), matArmor);

                // Tactical Shoulder Pauldrons
                GameObject shoulderL = CreateVisualChild("Armor_Shoulder_L", torso.transform, PrimitiveType.Cube,
                    new Vector3(-0.31f, 0.24f, 0f), new Vector3(0.18f, 0.12f, 0.22f), matArmor);
                shoulderL.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);

                GameObject shoulderR = CreateVisualChild("Armor_Shoulder_R", torso.transform, PrimitiveType.Cube,
                    new Vector3(0.31f, 0.24f, 0f), new Vector3(0.18f, 0.12f, 0.22f), matArmor);
                shoulderR.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);

                // --- Utility Belt ---
                GameObject belt = CreateVisualChild("UtilityBelt_Strap", rigRoot.transform, PrimitiveType.Cube,
                    new Vector3(0f, 0.88f, 0f), new Vector3(0.48f, 0.09f, 0.30f), matBuckles);

                GameObject buckle = CreateVisualChild("Belt_Buckle_Center", belt.transform, PrimitiveType.Cube,
                    new Vector3(0f, 0f, 0.16f), new Vector3(0.12f, 0.09f, 0.04f), matBuckles);

                // Tactical Pouches on Belt
                CreateVisualChild("Pouch_FL", belt.transform, PrimitiveType.Cube, new Vector3(-0.16f, 0f, 0.15f), new Vector3(0.08f, 0.11f, 0.06f), matArmor);
                CreateVisualChild("Pouch_FR", belt.transform, PrimitiveType.Cube, new Vector3(0.16f, 0f, 0.15f), new Vector3(0.08f, 0.11f, 0.06f), matArmor);
                CreateVisualChild("Pouch_SideL", belt.transform, PrimitiveType.Cube, new Vector3(-0.25f, 0f, 0f), new Vector3(0.06f, 0.11f, 0.10f), matArmor);
                CreateVisualChild("Pouch_SideR", belt.transform, PrimitiveType.Cube, new Vector3(0.25f, 0f, 0f), new Vector3(0.06f, 0.11f, 0.10f), matArmor);

                // --- Arms & Bracers ---
                GameObject armL = CreateVisualChild("Arm_Upper_L", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(-0.35f, 1.20f, 0f), new Vector3(0.14f, 0.24f, 0.14f), matFabric);
                GameObject forearmL = CreateVisualChild("Forearm_L", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(-0.35f, 0.88f, 0.04f), new Vector3(0.13f, 0.22f, 0.13f), matFabric);
                GameObject bracerL = CreateVisualChild("Bracer_Armor_L", forearmL.transform, PrimitiveType.Cube,
                    new Vector3(-0.06f, 0f, 0f), new Vector3(0.06f, 0.26f, 0.15f), matArmor);

                GameObject armR = CreateVisualChild("Arm_Upper_R", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(0.35f, 1.20f, 0f), new Vector3(0.14f, 0.24f, 0.14f), matFabric);
                GameObject forearmR = CreateVisualChild("Forearm_R", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(0.35f, 0.88f, 0.04f), new Vector3(0.13f, 0.22f, 0.13f), matFabric);
                GameObject bracerR = CreateVisualChild("Bracer_Armor_R", forearmR.transform, PrimitiveType.Cube,
                    new Vector3(0.06f, 0f, 0f), new Vector3(0.06f, 0.26f, 0.15f), matArmor);

                // Tactical Gloves
                CreateVisualChild("Glove_L", forearmL.transform, PrimitiveType.Cube, new Vector3(0f, -0.22f, 0f), new Vector3(0.11f, 0.13f, 0.09f), matArmor);
                CreateVisualChild("Glove_R", forearmR.transform, PrimitiveType.Cube, new Vector3(0f, -0.22f, 0f), new Vector3(0.11f, 0.13f, 0.09f), matArmor);

                // --- Legs & Combat Boots ---
                GameObject thighL = CreateVisualChild("Thigh_L", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(-0.16f, 0.62f, 0f), new Vector3(0.16f, 0.26f, 0.16f), matFabric);
                GameObject calfL = CreateVisualChild("Calf_L", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(-0.16f, 0.28f, 0f), new Vector3(0.15f, 0.24f, 0.15f), matFabric);
                CreateVisualChild("KneeGuard_L", calfL.transform, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.08f), new Vector3(0.14f, 0.10f, 0.05f), matArmor);
                CreateVisualChild("CombatBoot_L", calfL.transform, PrimitiveType.Cube, new Vector3(0f, -0.24f, 0.05f), new Vector3(0.15f, 0.14f, 0.26f), matArmor);

                GameObject thighR = CreateVisualChild("Thigh_R", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(0.16f, 0.62f, 0f), new Vector3(0.16f, 0.26f, 0.16f), matFabric);
                GameObject calfR = CreateVisualChild("Calf_R", rigRoot.transform, PrimitiveType.Capsule,
                    new Vector3(0.16f, 0.28f, 0f), new Vector3(0.15f, 0.24f, 0.15f), matFabric);
                CreateVisualChild("KneeGuard_R", calfR.transform, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.08f), new Vector3(0.14f, 0.10f, 0.05f), matArmor);
                CreateVisualChild("CombatBoot_R", calfR.transform, PrimitiveType.Cube, new Vector3(0f, -0.24f, 0.05f), new Vector3(0.15f, 0.14f, 0.26f), matArmor);

                // --- Full Cowl with Sharp Angular Ears & White Eye Lenses ---
                GameObject cowlHead = CreateVisualChild("Cowl_Head", rigRoot.transform, PrimitiveType.Cube,
                    new Vector3(0f, 1.62f, 0.02f), new Vector3(0.26f, 0.28f, 0.26f), matArmor);

                // Sleek Sharp Cowl Ears (Prism angular aesthetic)
                GameObject earL = CreateVisualChild("Cowl_Ear_L", cowlHead.transform, PrimitiveType.Cube,
                    new Vector3(-0.10f, 0.21f, -0.04f), new Vector3(0.05f, 0.24f, 0.06f), matArmor);
                earL.transform.localRotation = Quaternion.Euler(8f, 0f, 14f);

                GameObject earR = CreateVisualChild("Cowl_Ear_R", cowlHead.transform, PrimitiveType.Cube,
                    new Vector3(0.10f, 0.21f, -0.04f), new Vector3(0.05f, 0.24f, 0.06f), matArmor);
                earR.transform.localRotation = Quaternion.Euler(8f, 0f, -14f);

                // Jaw / Chin Guard
                CreateVisualChild("Cowl_ChinGuard", cowlHead.transform, PrimitiveType.Cube,
                    new Vector3(0f, -0.12f, 0.09f), new Vector3(0.18f, 0.10f, 0.08f), matArmor);

                // Angular Emissive White Eye Lenses
                GameObject eyeL = CreateVisualChild("Cowl_EyeLens_L", cowlHead.transform, PrimitiveType.Cube,
                    new Vector3(-0.065f, 0.02f, 0.132f), new Vector3(0.065f, 0.026f, 0.015f), matEyes);
                eyeL.transform.localRotation = Quaternion.Euler(0f, 0f, -12f); // Sharp stylized downward-slant

                GameObject eyeR = CreateVisualChild("Cowl_EyeLens_R", cowlHead.transform, PrimitiveType.Cube,
                    new Vector3(0.065f, 0.02f, 0.132f), new Vector3(0.065f, 0.026f, 0.015f), matEyes);
                eyeR.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);

                // --- 4-Segment Tactical Bone-Chain Cape with CapeSway Physics ---
                BuildDynamicCape(rigRoot.transform, matCape);

                // --- Faint Warm-Cyan Rim Light (Follows Player for Silhouette Separation) ---
                GameObject rimLightObj = new GameObject("Player_RimLight");
                rimLightObj.transform.SetParent(playerObj.transform, false);
                // Position high and slightly behind player pointing forward down
                rimLightObj.transform.localPosition = new Vector3(0f, 2.2f, -1.8f);
                rimLightObj.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);

                var rimLight = rimLightObj.AddComponent<Light>();
                rimLight.type = LightType.Spot;
                rimLight.color = new Color(0.28f, 0.88f, 0.95f); // Warm-cyan / vibrant stealth teal
                rimLight.intensity = 0.45f; // Faint, subtle rim
                rimLight.range = 4.8f;
                rimLight.spotAngle = 68f;
                rimLight.innerSpotAngle = 35f;
                rimLight.shadows = LightShadows.None; // Low-overhead rim light

                // Ensure all created children are set to Ignore Raycast layer (Layer 2)
                SetLayerRecursively(rigRoot, 2);
                SetLayerRecursively(rimLightObj, 2);
            }

            // 8. Rain Particles (Following Main Camera) & Ground Splashes
            SetupAtmosphericRainAndSplashes();
        }

        private static GameObject CreateVisualChild(string name, Transform parent, PrimitiveType primType, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(primType);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = localScale;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            if (mat != null) obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return obj;
        }

        private static void BuildDynamicCape(Transform visualRigParent, Material capeMat)
        {
            GameObject capeRoot = new GameObject("Vigilante_Cape");
            capeRoot.transform.SetParent(visualRigParent, false);
            capeRoot.transform.localPosition = new Vector3(0f, 1.42f, -0.16f); // Upper back / shoulder line

            var sway = capeRoot.AddComponent<CapeSway>();

            List<Transform> segments = new List<Transform>();
            Transform currentParent = capeRoot.transform;

            // 4 Segment bone-chain ending at mid-calf (total drop ~1.15m)
            float[] segmentLengths = new float[] { 0.28f, 0.30f, 0.30f, 0.26f };
            float[] widths = new float[] { 0.54f, 0.64f, 0.74f, 0.82f }; // Flare slightly outward toward calf

            for (int i = 0; i < 4; i++)
            {
                GameObject segObj = new GameObject($"Cape_Segment_{i}");
                segObj.transform.SetParent(currentParent, false);
                segObj.transform.localPosition = (i == 0) ? Vector3.zero : new Vector3(0f, -segmentLengths[i - 1], 0f);

                // Visual mesh for this segment
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = $"Visual_Mesh_{i}";
                visual.transform.SetParent(segObj.transform, false);
                visual.transform.localPosition = new Vector3(0f, -segmentLengths[i] * 0.5f, -0.005f);
                visual.transform.localScale = new Vector3(widths[i], segmentLengths[i], 0.015f); // Thin double-sided sheet
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<MeshRenderer>().sharedMaterial = capeMat;

                segments.Add(segObj.transform);
                currentParent = segObj.transform;
            }

            sway.SetCapeSegments(segments);
        }

        private static void SetupAtmosphericRainAndSplashes()
        {
            // Find or clean old rain systems
            GameObject oldRain = GameObject.Find("Environment_RainSystem");
            if (oldRain != null) Object.DestroyImmediate(oldRain);

            Camera mainCam = Camera.main;
            Transform rainParent = (mainCam != null) ? mainCam.transform : null;

            GameObject rainObj = GameObject.Find("Camera_RainSystem");
            if (rainObj == null)
            {
                rainObj = new GameObject("Camera_RainSystem");
                if (rainParent != null) rainObj.transform.SetParent(rainParent, false);
            }

            rainObj.transform.localPosition = new Vector3(0f, 9f, 6f);
            rainObj.transform.localRotation = Quaternion.Euler(78f, 0f, 0f); // Angled down-forward

            var ps = rainObj.GetComponent<ParticleSystem>();
            if (ps == null) ps = rainObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 3000;
            main.startSpeed = 24f;
            main.startLifetime = 0.85f;
            main.startSize = 0.028f;
            main.startColor = new Color(0.72f, 0.86f, 1.0f, 0.28f); // Translucent cold rain drops
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 1600;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(28f, 22f, 1f);

            var psr = rainObj.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 2.0f;
            psr.velocityScale = 0.045f;

            // Ground Rain Splashes (Parented to Player to restrict performance cost to immediate vicinity)
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                GameObject splashObj = GameObject.Find("Player_GroundRainSplashes");
                if (splashObj == null)
                {
                    splashObj = new GameObject("Player_GroundRainSplashes");
                    splashObj.transform.SetParent(playerObj.transform, false);
                }
                splashObj.transform.localPosition = new Vector3(0f, 0.02f, 0f);

                var splashPS = splashObj.GetComponent<ParticleSystem>();
                if (splashPS == null) splashPS = splashObj.AddComponent<ParticleSystem>();
                var spMain = splashPS.main;
                spMain.maxParticles = 120; // Lightweight for Apple Silicon
                spMain.startSpeed = 1.2f;
                spMain.startLifetime = 0.18f;
                spMain.startSize = 0.12f;
                spMain.startColor = new Color(0.6f, 0.8f, 1.0f, 0.35f);
                spMain.simulationSpace = ParticleSystemSimulationSpace.World;

                var spEmission = splashPS.emission;
                spEmission.rateOverTime = 65;

                var spShape = splashPS.shape;
                spShape.shapeType = ParticleSystemShapeType.Circle;
                spShape.radius = 7.5f; // Localized around player

                var spRenderer = splashObj.GetComponent<ParticleSystemRenderer>();
                spRenderer.renderMode = ParticleSystemRenderMode.Billboard;

                SetLayerRecursively(splashObj, 2); // Ignore Raycast
            }
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
#endif
