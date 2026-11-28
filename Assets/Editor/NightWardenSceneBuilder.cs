#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.AI;
using NightWarden.Player;
using NightWarden.SLAM;
using NightWarden.Stealth;
using NightWarden.AI;
using NightWarden.Objectives;
using NightWarden.Core;
using NightWarden.UI;

namespace NightWarden.EditorTools
{
    /// <summary>
    /// Automated 1-Click Level & Scene Generator for Night Warden: Echo Protocol.
    /// Constructs the rain-soaked industrial district, vigilante player, armed guards,
    /// 3 bombs, extraction point, SLAM point cloud buffers, and HUD interface.
    /// Menu: Night Warden -> 1-Click Build Complete Level Scene
    /// </summary>
    public static class NightWardenSceneBuilder
    {
        [MenuItem("Night Warden/1-Click Build Complete Level Scene", false, 0)]
        public static void BuildCompleteLevel()
        {
            EnsureTagsExist();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Cinematic Atmosphere: Night, Fog, Cold Moonlight
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.06f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.09f);
            RenderSettings.fogDensity = 0.028f;

            // Moonlight Directional Light
            GameObject moonObj = new GameObject("Directional_Moonlight");
            var moonLight = moonObj.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.45f, 0.65f, 0.95f);
            moonLight.intensity = 0.35f;
            moonLight.shadows = LightShadows.Soft;
            moonObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Materials
            Material asphaltMat = CreateMaterial("Mat_Asphalt", new Color(0.06f, 0.07f, 0.09f), 0.75f);
            Material brickMat = CreateMaterial("Mat_BrickBuilding", new Color(0.12f, 0.13f, 0.15f), 0.3f);
            Material metalMat = CreateMaterial("Mat_MetalWarehouse", new Color(0.15f, 0.18f, 0.22f), 0.85f);
            Material ledgeMat = CreateMaterial("Mat_Ledge", new Color(0.1f, 0.6f, 0.8f), 0.5f);
            Material bombMat = CreateMaterial("Mat_Bomb", new Color(0.9f, 0.75f, 0.1f), 0.9f);
            Material extractMat = CreateMaterial("Mat_Extraction", new Color(0.0f, 0.9f, 0.4f), 0.8f);

            // 3. Level Geometry: Industrial District
            GameObject envParent = new GameObject("--- DISTRICT_ENVIRONMENT ---");

            // Main Ground Street (100x100m)
            CreateStaticBox("Street_Ground", envParent.transform, new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100), asphaltMat);

            // Perimeter Boundary Invisible Walls (Prevents player from falling off into the void)
            GameObject boundaries = new GameObject("District_Perimeter_Boundaries");
            boundaries.transform.SetParent(envParent.transform);
            CreateInvisibleBoundary("Boundary_North", boundaries.transform, new Vector3(0f, 5f, 50f), new Vector3(100f, 10f, 1f));
            CreateInvisibleBoundary("Boundary_South", boundaries.transform, new Vector3(0f, 5f, -50f), new Vector3(100f, 10f, 1f));
            CreateInvisibleBoundary("Boundary_East", boundaries.transform, new Vector3(50f, 5f, 0f), new Vector3(1f, 10f, 100f));
            CreateInvisibleBoundary("Boundary_West", boundaries.transform, new Vector3(-50f, 5f, 0f), new Vector3(1f, 10f, 100f));

            // Warehouse Structure (Open Industrial Depot with accessible doorways on all 4 sides)
            GameObject warehouse = new GameObject("Warehouse_Building");
            warehouse.transform.SetParent(envParent.transform);

            // South Wall (Main Entrance from Street): 12m wide opening from X=-6 to +6
            CreateStaticBox("WH_Wall_South_L", warehouse.transform, new Vector3(-12f, 4.5f, -5f), new Vector3(12f, 9f, 1f), metalMat);
            CreateStaticBox("WH_Wall_South_R", warehouse.transform, new Vector3(12f, 4.5f, -5f), new Vector3(12f, 9f, 1f), metalMat);
            CreateStaticBox("WH_Wall_South_Lintel", warehouse.transform, new Vector3(0f, 7.5f, -5f), new Vector3(12f, 3f, 1f), metalMat);

            // North Wall (Rear Exit to Extraction Courtyard): 10m wide opening from X=-5 to +5
            CreateStaticBox("WH_Wall_North_L", warehouse.transform, new Vector3(-11.5f, 4.5f, 25f), new Vector3(13f, 9f, 1f), metalMat);
            CreateStaticBox("WH_Wall_North_R", warehouse.transform, new Vector3(11.5f, 4.5f, 25f), new Vector3(13f, 9f, 1f), metalMat);
            CreateStaticBox("WH_Wall_North_Lintel", warehouse.transform, new Vector3(0f, 7.5f, 25f), new Vector3(10f, 3f, 1f), metalMat);

            // East Wall (Side Roll-Up Door to Shipping Container Yard): 10m wide opening from Z=5 to 15
            CreateStaticBox("WH_Wall_East_S", warehouse.transform, new Vector3(18f, 4.5f, 0f), new Vector3(1f, 9f, 10f), metalMat);
            CreateStaticBox("WH_Wall_East_N", warehouse.transform, new Vector3(18f, 4.5f, 20f), new Vector3(1f, 9f, 10f), metalMat);
            CreateStaticBox("WH_Wall_East_Lintel", warehouse.transform, new Vector3(18f, 7.5f, 10f), new Vector3(1f, 3f, 10f), metalMat);

            // West Wall (Side Door to Dark Alleyway): 10m wide opening from Z=5 to 15
            CreateStaticBox("WH_Wall_West_S", warehouse.transform, new Vector3(-18f, 4.5f, 0f), new Vector3(1f, 9f, 10f), metalMat);
            CreateStaticBox("WH_Wall_West_N", warehouse.transform, new Vector3(-18f, 4.5f, 20f), new Vector3(1f, 9f, 10f), metalMat);
            CreateStaticBox("WH_Wall_West_Lintel", warehouse.transform, new Vector3(-18f, 7.5f, 10f), new Vector3(1f, 3f, 10f), metalMat);

            // Warehouse Roof
            CreateStaticBox("WH_Roof", warehouse.transform, new Vector3(0, 9.5f, 10), new Vector3(38, 1, 32), metalMat);

            // Interior Warehouse Lighting (Warm Industrial Overhead Tungsten Lamps)
            CreateInteriorLamp("WH_Lamp_Front", warehouse.transform, new Vector3(0, 7f, 5f));
            CreateInteriorLamp("WH_Lamp_Back", warehouse.transform, new Vector3(0, 7f, 18f));

            // Interior & Street Tactical Cover Crates (Player can crouch behind)
            Material crateMat = CreateMaterial("Mat_Crates", new Color(0.28f, 0.22f, 0.16f), 0.3f);
            CreateStaticBox("Crate_WH_Stack1", warehouse.transform, new Vector3(-7f, 1f, 8f), new Vector3(2.4f, 2f, 2.4f), crateMat);
            CreateStaticBox("Crate_WH_Stack2", warehouse.transform, new Vector3(8f, 1f, 16f), new Vector3(3f, 2f, 2f), crateMat);
            CreateStaticBox("Crate_Street_Left", envParent.transform, new Vector3(-5f, 0.75f, -22f), new Vector3(1.5f, 1.5f, 3f), metalMat);
            CreateStaticBox("Crate_Street_Right", envParent.transform, new Vector3(6f, 0.75f, -18f), new Vector3(2f, 1.5f, 2f), metalMat);

            // Alleyway Buildings
            CreateStaticBox("Building_Alley_Left", envParent.transform, new Vector3(-32, 7.5f, -15), new Vector3(16, 15, 45), brickMat);
            CreateStaticBox("Building_Alley_Right", envParent.transform, new Vector3(-18, 6f, -30), new Vector3(10, 12, 35), brickMat);
            CreateStaticBox("Building_Docks", envParent.transform, new Vector3(30, 8f, -15), new Vector3(18, 16, 50), brickMat);

            // Climbable Ledges (Tagged Ledge for cyan echolocation)
            GameObject ledge1 = CreateStaticBox("Ledge_WH_Container", warehouse.transform, new Vector3(12, 1.5f, -3), new Vector3(6, 3, 4), ledgeMat);
            ledge1.tag = "Ledge";

            GameObject ledge2 = CreateStaticBox("Ledge_Alley_FireEscape", envParent.transform, new Vector3(-23, 3f, -15), new Vector3(2, 6, 8), ledgeMat);
            ledge2.tag = "Ledge";

            // Rain Particle System
            CreateRainSystem(envParent.transform);

            // 4. Managers
            GameObject managersObj = new GameObject("--- MANAGERS ---");
            var pointCloud = managersObj.AddComponent<PointCloudBuffer>();
            var voxelMap = managersObj.AddComponent<VoxelMap>();
            var noiseSystem = managersObj.AddComponent<NoiseSystem>();
            var gameMgr = managersObj.AddComponent<GameManager>();
            var miniMap = managersObj.AddComponent<MiniMap>();
            var hudController = managersObj.AddComponent<HUDController>();

            // 5. The Player: Night Warden (Spawned on Main Street facing North toward warehouse)
            GameObject playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(0, 1.0f, -38f);
            playerObj.transform.rotation = Quaternion.identity;

            var charCtrl = playerObj.AddComponent<CharacterController>();
            charCtrl.height = 1.8f;
            charCtrl.radius = 0.4f;
            charCtrl.center = Vector3.up * 0.9f;

            // Player visual mesh (Stylized dark vigilante silhouette)
            GameObject playerMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerMesh.name = "Vigilante_Body";
            playerMesh.transform.SetParent(playerObj.transform);
            playerMesh.transform.localPosition = Vector3.up * 0.9f;
            playerMesh.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
            Object.DestroyImmediate(playerMesh.GetComponent<Collider>());
            playerMesh.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("Mat_NightWardenSuit", new Color(0.08f, 0.09f, 0.11f), 0.6f);

            // Cowl / Ears
            GameObject cowlEarL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cowlEarL.transform.SetParent(playerMesh.transform);
            cowlEarL.transform.localPosition = new Vector3(-0.25f, 0.85f, 0f);
            cowlEarL.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            Object.DestroyImmediate(cowlEarL.GetComponent<Collider>());
            cowlEarL.GetComponent<MeshRenderer>().sharedMaterial = playerMesh.GetComponent<MeshRenderer>().sharedMaterial;

            GameObject cowlEarR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cowlEarR.transform.SetParent(playerMesh.transform);
            cowlEarR.transform.localPosition = new Vector3(0.25f, 0.85f, 0.0f);
            cowlEarR.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            Object.DestroyImmediate(cowlEarR.GetComponent<Collider>());
            cowlEarR.GetComponent<MeshRenderer>().sharedMaterial = playerMesh.GetComponent<MeshRenderer>().sharedMaterial;

            // Scripts
            playerObj.AddComponent<ThirdPersonPlayerController>();
            playerObj.AddComponent<EchoPulse>();
            playerObj.AddComponent<TakedownAction>();

            // Third-Person Camera
            GameObject camObj = new GameObject("ThirdPersonCamera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 120f;
            camObj.AddComponent<AudioListener>();
            var camFollow = camObj.AddComponent<ThirdPersonCameraFollow>();

            // 6. Objectives: 3 Bombs & Extraction Point
            GameObject objParent = new GameObject("--- OBJECTIVES ---");

            // Bomb 1: Inside the Warehouse
            GameObject bomb1 = CreateBomb("Bomb_Warehouse", objParent.transform, new Vector3(0f, 0.5f, 15f), bombMat);
            // Bomb 2: Dark Alleyway
            GameObject bomb2 = CreateBomb("Bomb_Alley", objParent.transform, new Vector3(-25f, 0.5f, -22f), bombMat);
            // Bomb 3: Shipping Ledge Area
            GameObject bomb3 = CreateBomb("Bomb_ContainerLedge", objParent.transform, new Vector3(25f, 0.5f, -8f), bombMat);

            // Extraction Beacon
            GameObject extractObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            extractObj.name = "Extraction_Beacon";
            extractObj.tag = "Extraction";
            extractObj.transform.SetParent(objParent.transform);
            extractObj.transform.position = new Vector3(0f, 0.1f, 42f);
            extractObj.transform.localScale = new Vector3(5f, 0.2f, 5f);
            extractObj.GetComponent<MeshRenderer>().sharedMaterial = extractMat;
            var extractCol = extractObj.GetComponent<Collider>();
            extractCol.isTrigger = true;
            extractObj.AddComponent<ExtractionPoint>();
            extractObj.SetActive(false);

            // Connect Extraction point to GameManager
            var extField = typeof(GameManager).GetField("extractionPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            extField?.SetValue(gameMgr, extractObj);

            // 7. Armed Guards (4 Guards patrolling key open areas with visible flashlights)
            GameObject guardsParent = new GameObject("--- ARMED_GUARDS ---");

            // Guard 1: Patrols Main Street in front of the warehouse (directly visible from player spawn!)
            CreateGuard("Guard_MainStreet_Patrol", guardsParent.transform, new Vector3(0, 1, -26), new Vector3[] {
                new Vector3(14, 0, -26), new Vector3(-14, 0, -26), new Vector3(0, 0, -16), new Vector3(0, 0, -32)
            });

            // Guard 2: Patrols inside the open warehouse near Bomb #1
            CreateGuard("Guard_Warehouse_Patrol", guardsParent.transform, new Vector3(4, 1, 10), new Vector3[] {
                new Vector3(8, 0, 8), new Vector3(-8, 0, 8), new Vector3(-8, 0, 18), new Vector3(8, 0, 18)
            });

            // Guard 3: Patrols Dark Alleyway near Bomb #2
            CreateGuard("Guard_Alley_Patrol", guardsParent.transform, new Vector3(-24, 1, -15), new Vector3[] {
                new Vector3(-24, 0, -6), new Vector3(-24, 0, -32), new Vector3(-18, 0, -20)
            });

            // Guard 4: Patrols Shipping Docks near Bomb #3
            CreateGuard("Guard_ShippingDocks_Patrol", guardsParent.transform, new Vector3(24, 1, -12), new Vector3[] {
                new Vector3(24, 0, -4), new Vector3(24, 0, -28), new Vector3(16, 0, -16)
            });

            // 8. In-Game HUD & UI Canvas
            BuildNightWardenUI(hudController, miniMap);

            // 9. Apply Tactical Vigilante Visual Overhaul, Dynamic Cape, Cinematic Lighting & Post-Processing
            NightWardenAssetGenerator.GenerateAllVisuals();

            // 10. Bake NavMesh for active scene so guards immediately patrol & track
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            // 11. Save Scene
            string sceneDir = "Assets/Scenes";
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
            string scenePath = Path.Combine(sceneDir, "NightWardenLevel.unity");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"<color=#00e5ff><b>[Night Warden]</b> Complete level successfully built and saved to {scenePath}!</color>");
            Selection.activeGameObject = playerObj;
        }

        private static GameObject CreateStaticBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = pos;
            obj.transform.localScale = scale;
            if (mat != null) obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);
            return obj;
        }

        private static GameObject CreateInvisibleBoundary(string name, Transform parent, Vector3 pos, Vector3 scale)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = pos;
            obj.transform.localScale = scale;
            var mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
            var mf = obj.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.NavigationStatic);
            return obj;
        }

        private static GameObject CreateInteriorLamp(string name, Transform parent, Vector3 pos)
        {
            GameObject lamp = new GameObject(name);
            lamp.transform.SetParent(parent);
            lamp.transform.position = pos;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.45f);
            light.intensity = 1.6f;
            light.range = 18f;
            light.shadows = LightShadows.Soft;
            return lamp;
        }

        private static GameObject CreateBomb(string name, Transform parent, Vector3 pos, Material mat)
        {
            GameObject bomb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bomb.name = name;
            bomb.tag = "Bomb";
            bomb.transform.SetParent(parent);
            bomb.transform.position = pos;
            bomb.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            if (mat != null) bomb.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Beeping blinking LED on bomb
            GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            led.transform.SetParent(bomb.transform);
            led.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            led.transform.localScale = Vector3.one * 0.35f;
            Object.DestroyImmediate(led.GetComponent<Collider>());
            led.GetComponent<MeshRenderer>().sharedMaterial = mat;

            bomb.AddComponent<BombObjective>();
            return bomb;
        }

        private static void CreateGuard(string name, Transform parent, Vector3 startPos, Vector3[] patrolPoints)
        {
            GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            guard.name = name;
            guard.tag = "Guard";
            guard.transform.SetParent(parent);
            guard.transform.position = startPos;

            var mat = CreateMaterial("Mat_GuardOutfit", new Color(0.62f, 0.18f, 0.18f), 0.35f); // Visible dark crimson uniform
            guard.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Tactical glowing visor on guard face
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Guard_Visor";
            visor.transform.SetParent(guard.transform);
            visor.transform.localPosition = new Vector3(0f, 0.65f, 0.4f);
            visor.transform.localScale = new Vector3(0.45f, 0.12f, 0.25f);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            var visorMat = CreateMaterial("Mat_GuardVisor", new Color(1.0f, 0.25f, 0.1f), 0.9f);
            visorMat.EnableKeyword("_EMISSION");
            visorMat.SetColor("_EmissionColor", new Color(1.0f, 0.3f, 0.1f) * 2.5f);
            visor.GetComponent<MeshRenderer>().sharedMaterial = visorMat;

            // Tactical Flashlight Spotlight (Illuminates streets and casts shadow beams through rain)
            GameObject flObj = new GameObject("Guard_Flashlight");
            flObj.transform.SetParent(guard.transform);
            flObj.transform.localPosition = new Vector3(0f, 1.35f, 0.45f);
            flObj.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            var fl = flObj.AddComponent<Light>();
            fl.type = LightType.Spot;
            fl.color = new Color(1f, 0.88f, 0.45f);
            fl.intensity = 3.8f;
            fl.range = 22f;
            fl.spotAngle = 55f;
            fl.shadows = LightShadows.Soft;

            var agent = guard.AddComponent<NavMeshAgent>();
            agent.speed = 2.2f;
            agent.radius = 0.45f;
            agent.height = 1.9f;
            agent.stoppingDistance = 0.5f;

            var guardAI = guard.AddComponent<GuardAI>();

            // Waypoints
            GameObject wpParent = new GameObject($"{name}_Waypoints");
            wpParent.transform.SetParent(guard.transform.parent);
            Transform[] wpTransforms = new Transform[patrolPoints.Length];
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                GameObject wp = new GameObject($"WP_{i}");
                wp.transform.SetParent(wpParent.transform);
                wp.transform.position = patrolPoints[i];
                wpTransforms[i] = wp.transform;
            }

            var wpField = typeof(GuardAI).GetField("waypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            wpField?.SetValue(guardAI, wpTransforms);
        }

        private static void CreateRainSystem(Transform parent)
        {
            GameObject rainObj = new GameObject("Environment_RainSystem");
            rainObj.transform.SetParent(parent);
            rainObj.transform.position = new Vector3(0, 25, 0);

            var ps = rainObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 6000;
            main.startSpeed = 30f;
            main.startLifetime = 1.2f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.7f, 0.85f, 1.0f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 2500;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80, 1, 80);

            var psr = rainObj.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 1.8f;
            psr.velocityScale = 0.05f;
        }

        private static void BuildNightWardenUI(HUDController hud, MiniMap miniMap)
        {
            GameObject canvasObj = new GameObject("Canvas_NightWardenHUD");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Battery Slider
            GameObject batteryObj = new GameObject("EchoBatteryBar");
            batteryObj.transform.SetParent(canvasObj.transform, false);
            var batRect = batteryObj.AddComponent<RectTransform>();
            batRect.anchorMin = new Vector2(0, 0);
            batRect.anchorMax = new Vector2(0, 0);
            batRect.anchoredPosition = new Vector2(170, 45);
            batRect.sizeDelta = new Vector2(260, 18);

            var batSlider = batteryObj.AddComponent<Slider>();
            var batBg = batteryObj.AddComponent<Image>();
            batBg.color = new Color(0.08f, 0.15f, 0.25f, 0.85f);

            // Timer Text (Top Center)
            GameObject timerObj = new GameObject("Text_BombTimer");
            timerObj.transform.SetParent(canvasObj.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0, -40);
            timerRect.sizeDelta = new Vector2(280, 45);
            var timerText = timerObj.AddComponent<Text>();
            timerText.font = defaultFont;
            timerText.fontSize = 32;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.text = "06:00";
            timerText.color = Color.white;

            // Bomb Count Text (Top Left)
            GameObject countObj = new GameObject("Text_BombCount");
            countObj.transform.SetParent(canvasObj.transform, false);
            var countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 1f);
            countRect.anchorMax = new Vector2(0f, 1f);
            countRect.anchoredPosition = new Vector2(160, -40);
            countRect.sizeDelta = new Vector2(280, 35);
            var countText = countObj.AddComponent<Text>();
            countText.font = defaultFont;
            countText.fontSize = 18;
            countText.alignment = TextAnchor.MiddleLeft;
            countText.text = "BOMBS DEFUSED: 0 / 3";
            countText.color = new Color(1f, 0.85f, 0.1f);

            // Prompt Text (Bottom Center)
            GameObject promptObj = new GameObject("Text_InteractionPrompt");
            promptObj.transform.SetParent(canvasObj.transform, false);
            var promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0, 50);
            promptRect.sizeDelta = new Vector2(500, 30);
            var pText = promptObj.AddComponent<Text>();
            pText.font = defaultFont;
            pText.fontSize = 17;
            pText.alignment = TextAnchor.MiddleCenter;
            pText.text = "[Q] Echo Pulse  |  [Tab] MiniMap  |  [C] Crouch";
            pText.color = new Color(0.6f, 0.85f, 1f, 0.85f);

            // MiniMap Panel
            GameObject mapPanel = new GameObject("Panel_MiniMap");
            mapPanel.transform.SetParent(canvasObj.transform, false);
            var mapRect = mapPanel.AddComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.sizeDelta = new Vector2(420, 420);
            var mapImg = mapPanel.AddComponent<RawImage>();
            mapImg.color = Color.white;

            GameObject marker = new GameObject("PlayerMarker");
            marker.transform.SetParent(mapPanel.transform, false);
            var markRect = marker.AddComponent<RectTransform>();
            markRect.sizeDelta = new Vector2(14, 14);
            var markImg = marker.AddComponent<Image>();
            markImg.color = Color.cyan;
            mapPanel.SetActive(false);

            // Wire HUD & MiniMap
            var batField = typeof(HUDController).GetField("batterySlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            batField?.SetValue(hud, batSlider);

            var timerField = typeof(HUDController).GetField("bombTimerText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timerField?.SetValue(hud, timerText);

            var countField = typeof(HUDController).GetField("bombCountText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            countField?.SetValue(hud, countText);

            var promptField = typeof(HUDController).GetField("promptText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            promptField?.SetValue(hud, pText);

            var mmImgField = typeof(MiniMap).GetField("minimapImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mmImgField?.SetValue(miniMap, mapImg);

            var mmPanelField = typeof(MiniMap).GetField("minimapPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mmPanelField?.SetValue(miniMap, mapPanel);

            var mmMarkField = typeof(MiniMap).GetField("playerMarker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mmMarkField?.SetValue(miniMap, markRect);
        }

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(s);
            mat.name = name;
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static void EnsureTagsExist()
        {
            string[] tags = { "Player", "Guard", "Bomb", "Ledge", "Climbable", "Extraction" };
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            foreach (string t in tags)
            {
                bool exists = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == t)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = t;
                }
            }
            tagManager.ApplyModifiedProperties();
        }
    }
}
#endif
