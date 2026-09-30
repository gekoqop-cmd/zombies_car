using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Build Scenes: Boot, MainMenu, UI_HUD and Map01 (assembled only from prefabs), plus Build Settings.
    /// Map01 follows referens/concept.png scaled up to the 44 x 30 m play area: workshop in the north-west, gas-station
    /// sign and sedan in the north-east, the player car in the free centre, fences around, roads outside. Nothing is
    /// placed beyond what the game camera can see (MapConfig ground area). Zombies rise from spawn points along the
    /// inside of the fences and walk on a NavMesh baked from the colliders.
    /// Flat layers are kept at least ~1 cm apart (see the c_*Height constants and PrefabBuilder) so they never z-fight.
    /// </summary>
    public static partial class SceneBuilder
    {
        public const string c_BootScene = EditorPaths.c_Scenes + "/Boot.unity";
        public const string c_MenuScene = EditorPaths.c_Scenes + "/MainMenu.unity";
        public const string c_MapScene = EditorPaths.c_Scenes + "/Map01.unity";
        public const string c_NightMapScene = EditorPaths.c_Scenes + "/Map02.unity";
        public const string c_HudScene = EditorPaths.c_Scenes + "/UI_HUD.unity";
        private const string c_VolumeProfile = EditorPaths.c_Settings + "/Map01_VolumeProfile.asset";
        private const string c_LightingSettings = EditorPaths.c_Settings + "/Lighting_Realtime.lighting";
        private const string c_FontAsset = EditorPaths.c_Fonts + "/UI_Font SDF.asset";
        private const string c_NavMeshData = EditorPaths.c_Scenes + "/Map01_NavMesh.asset";

        // Map01 layout constants (metres). Fence lines sit just inside the play-area border (MapConfig).
        private const float c_FenceInset = 0.2f;
        private const float c_FenceSection = 2f;
        private const float c_GateModelWidth = 4.47f;      // Fence_Gate.fbx bounds
        private const float c_BarrierModelWidth = 2f;      // Barrier.fbx bounds
        private const float c_CornerArm = 1.48f;         // corner piece: post-to-post arm + half post
        // Heights of the flat layers above the asphalt (y = 0). Decals sit at 0.014..0.036 (+0.012 for stones); the
        // workshop's soil slab is clearly above all of them, so decals reaching under it are hidden, not flickering.
        private const float c_WorkshopLift = 0.065f;
        private const float c_RoadHeight = 0.006f;
        private const float c_SidewalkHeight = 0.03f;      // kerb step above the road
        private const float c_DecalStep = 0.001f;           // every overlapping decal gets its own layer
        // The concept's buildings and signs read much larger than real-world size next to the cars.
        private const float c_WorkshopScale = 1.7f;
        private const float c_GasSignScale = 1.5f;
        private const float c_RoadSignScale = 1.3f;
        public static readonly Vector3 PlayerSpawn = new Vector3(-1.5f, 0f, -3f);
        private static readonly Vector3 s_WorkshopPosition = new Vector3(-9f, 0f, 10.3f);
        private const float c_CarPlayerYaw = 120f;

        /// <summary>
        /// Zombie spawn points inside the fences (the lot is closed: the gate and the east exit are blocked), clear of
        /// the workshop yard and the parked cars. The spawner snaps them to the NavMesh and skips those near the player.
        /// </summary>
        private static readonly Vector2[] s_ZombieSpawnPoints =
        {
            new Vector2(-17f, -12.6f), new Vector2(-8.5f, -12.6f), new Vector2(-1f, -12.8f), new Vector2(5.5f, -12.8f),
            new Vector2(12f, -10.2f), new Vector2(-19.3f, -3.5f), new Vector2(-19.8f, 0.5f), new Vector2(19.8f, -5f),
            new Vector2(19.8f, 0f), new Vector2(18.8f, 7.5f), new Vector2(5.7f, 12.8f), new Vector2(0f, 11.5f),
            new Vector2(14f, 11.3f),
        };

        // Look (sunset).
        /// <summary>Lighting of a map build: the same lot as an evening (Map01) or a moonlit night (Map02).</summary>
        private sealed class MapLook
        {
            public Color SunColor;
            public float SunIntensity;
            public Vector3 SunEuler;
            public Color AmbientSky, AmbientEquator, AmbientGround;
            public Color FogColor;
            [Tooltip("Multiplies the intensity / range of every point light (lamps carry the night).")]
            public float LampIntensity = 1f, LampRange = 1f;
            public bool Night;
        }

        private static readonly MapLook s_EveningLook = new MapLook
        {
            SunColor = new Color(1f, 0.86f, 0.7f), SunIntensity = 2f, SunEuler = new Vector3(40f, 70f, 0f),
            AmbientSky = new Color(0.46f, 0.52f, 0.66f),
            AmbientEquator = new Color(0.42f, 0.40f, 0.40f),   // deeper shadows, like the concept
            AmbientGround = new Color(0.28f, 0.24f, 0.22f),
            FogColor = new Color(0.68f, 0.58f, 0.52f),
        };

        // Moonlight (cool, low, from the moon's side in map2.png), dark blue haze, lamps much stronger.
        private static readonly MapLook s_NightLook = new MapLook
        {
            // Low moon: long soft shadows.
            SunColor = new Color(0.62f, 0.7f, 0.95f), SunIntensity = 0.6f, SunEuler = new Vector3(28f, -35f, 0f),
            AmbientSky = new Color(0.21f, 0.21f, 0.33f),              // muted dark blue-violet
            AmbientEquator = new Color(0.16f, 0.16f, 0.23f),
            AmbientGround = new Color(0.09f, 0.09f, 0.12f),
            FogColor = new Color(0.08f, 0.09f, 0.14f),
            LampIntensity = 2.4f, LampRange = 1.35f, Night = true,
        };

        private static MapLook s_Look = s_EveningLook;
        private const int c_NightLayoutSeed = 100;               // added to the layout seeds: a different arrangement at night
        private const float c_FogStart = 28f;                  // only the far top of the view gets a warm haze
        private const float c_FogEnd = 95f;
        private static readonly Color s_LampColor = new Color(1f, 0.62f, 0.3f);

        private static Dictionary<string, string> s_PrefabIndex;

        [MenuItem("Tools/Build Scenes")]
        public static void BuildAll()
        {
            UiFactory.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(c_FontAsset);
            s_PrefabIndex = null;
            EditorPaths.EnsureFolder(EditorPaths.c_Scenes);
            UiIconPainter.EnsureIcons();                               // the gold ad / coin icons the UI uses
            BuildBoot();
            BuildMainMenu();
            BuildHud();
            BuildMap(c_MapScene, DataAssetsBuilder.c_MapConfig, s_EveningLook);
            BuildMap(c_NightMapScene, DataAssetsBuilder.c_NightMapConfig, s_NightLook);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(c_BootScene, true), new EditorBuildSettingsScene(c_MenuScene, true),
                new EditorBuildSettingsScene(c_MapScene, true), new EditorBuildSettingsScene(c_NightMapScene, true),
                new EditorBuildSettingsScene(c_HudScene, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Boot, MainMenu, UI_HUD, Map01 (evening), Map02 (night) built and added to Build Settings.");
        }

        // ================================================================== Boot / MainMenu / HUD
        private static void BuildBoot()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera(Color.black);
            var boot = new GameObject("Boot").AddComponent<BootLoader>();
            PrefabBuilder.Wire(boot, ("m_Settings", DataAssetsBuilder.Load<GameSettings>(DataAssetsBuilder.c_GameSettings)));
            AudioBuilder.AddAudio();
            EditorSceneManager.SaveScene(scene, c_BootScene);
        }

        /// <summary>
        /// Fills the saved HUD with the values a map starts with, all from the data assets: player stats defaults,
        /// wave 1 with the first-wave countdown, a full magazine + the start reserve.
        /// </summary>
        private static void ShowStartState(HudController hud)
        {
            var waves = DataAssetsBuilder.Load<WaveConfig>(DataAssetsBuilder.c_WaveConfig);
            var weapon = DataAssetsBuilder.Load<WeaponConfig>(DataAssetsBuilder.c_WeaponConfig);
            DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats).ResetToDefaults();
            DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState).Set(1, Mathf.CeilToInt(waves.FirstWaveDelay));
            DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState).Set(weapon.MagazineSize, weapon.StartReserve, false);
            hud.Refresh();
        }

        /// <summary>Stores a scene canvas as Prefabs/UI/&lt;name&gt; and keeps the scene object as its instance.</summary>
        private static void SaveUiPrefab(GameObject canvas, string name)
        {
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsUi);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas, $"{EditorPaths.c_PrefabsUi}/{name}.prefab", InteractionMode.AutomatedAction);
        }

        private static void CreateUiCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.cullingMask = 0;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            go.AddComponent<AudioListener>();
        }

        // ================================================================== Map01
        /// <summary>The lot (same layout for every map) with <paramref name="look"/>'s lighting, saved to <paramref name="path"/>.</summary>
        private static void BuildMap(string path, string mapConfig, MapLook look)
        {
            s_Look = look;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Loaded after NewScene: opening a scene unloads assets nothing references, which would leave a
            // config loaded before it readable but saved into the scene as an empty reference.
            MapConfig map = DataAssetsBuilder.Load<MapConfig>(mapConfig);
            SetupLighting();

            Transform systems = Root("Systems");
            Transform lighting = Root("Lighting");
            Transform cameraRoot = Root("Camera");
            Transform environment = Root("Environment");
            Transform fences = Root("Fences");
            Transform props = Root("Props");
            Transform nature = Root("Nature");
            Transform decals = Root("Decals");
            Transform background = Root("Background");
            Transform gameplay = Root("Gameplay");

            var systemsGo = new GameObject("MapSystems");
            systemsGo.transform.SetParent(systems, false);
            PrefabBuilder.Wire(systemsGo.AddComponent<MapBootstrap>(), ("m_Map", map),
                ("m_Run", DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState)),
                ("m_PlayerStats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Cosmetics", CosmeticsBuilder.Catalog));
            PrefabBuilder.Wire(systemsGo.AddComponent<CrosshairCursor>(),
                ("m_Crosshair", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/UI/UI_Crosshair.png")));
            AudioBuilder.AddAudio();

            BuildLights(lighting);
            BuildGround(environment, background, map);
            BuildFences(fences, map);
            BuildBuildingsAndSigns(environment);
            GasSignLanternLights(lighting, environment.Find("Sign_Gas"));
            BuildProps(props);
            BuildNature(nature, map);
            BuildDecals(decals);
            if (look.Night)
            {
                VaryLayout(props, nature, environment, map, c_NightLayoutSeed);
                AddNightLotLamps(props);
            }
            BuildExplosives(Root("Explosives"));

            GameObject car = Place("Car_Player", gameplay, 0f, 0f, c_CarPlayerYaw);
            car.name = "Car_Player";
            GameObject player = Place("Player", gameplay, PlayerSpawn.x, PlayerSpawn.z, 0f);
            player.name = "Player";
            BuildCarHealthBar(car, player);
            BuildCamera(cameraRoot, player.transform, map);
            BuildZombieSystems(systemsGo, gameplay, car, player);
            BuildWorkshopZone(gameplay);
            BuildCarResupply(gameplay, car, player);
            BuildNavMesh(systems);
            UltraQualityBuilder.AddToMap(map, lighting, nature, cameraRoot, systemsGo, player, (x, z) => IsFreeForGrass(x, z, map));
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type != LightType.Point)
                    continue;
                light.intensity *= look.LampIntensity;
                light.range *= look.LampRange;
            }
            if (look.Night)
            {
                NightStreetLamps(lighting);
                SwapNightMaterials();
                AddCarLights(car);
            }

            EditorSceneManager.SaveScene(scene, path);
            ReportModelUsage(scene);
            s_Look = s_EveningLook;
        }

        private static void BuildZombieSystems(GameObject systemsGo, Transform gameplay, GameObject car, GameObject player)
        {
            var points = new GameObject("ZombieSpawnPoints").transform;
            points.SetParent(gameplay, false);
            for (int i = 0; i < s_ZombieSpawnPoints.Length; i++)
            {
                var point = new GameObject($"SpawnPoint_{i + 1:00}").transform;
                point.SetParent(points, false);
                point.localPosition = new Vector3(s_ZombieSpawnPoints[i].x, 0f, s_ZombieSpawnPoints[i].y);
            }

            var spawner = systemsGo.AddComponent<ZombieSpawner>();
            PrefabBuilder.Wire(spawner, ("m_Car", car.GetComponent<CarHealth>()), ("m_Player", player.GetComponent<PlayerHealth>()),
                ("m_SpawnPoints", points));
            PrefabBuilder.Wire(systemsGo.AddComponent<WaveDirector>(),
                ("m_Config", DataAssetsBuilder.Load<WaveConfig>(DataAssetsBuilder.c_WaveConfig)),
                ("m_WaveState", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Spawner", spawner));
            PrefabBuilder.Wire(systemsGo.AddComponent<GameOverDirector>(),
                ("m_Run", DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState)), ("m_Car", car.GetComponent<CarHealth>()),
                ("m_Player", player.GetComponent<PlayerHealth>()), ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Director", systemsGo.GetComponent<WaveDirector>()),
                ("m_Explosion", AssetDatabase.LoadAssetAtPath<ZombiCar.Props.ComicExplosion>(FxBuilder.c_ExplosionPrefab)));
            PrefabBuilder.Wire(systemsGo.AddComponent<MusicDirector>(), ("m_Map", systemsGo.GetComponent<MapBootstrap>().Map),
                ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)));
            PrefabBuilder.Wire(systemsGo.AddComponent<PlayerProgression>(),
                ("m_Config", DataAssetsBuilder.Load<ProgressionConfig>(DataAssetsBuilder.c_ProgressionConfig)),
                ("m_Run", DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState)),
                ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Spawner", spawner), ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Player", player.GetComponent<PlayerHealth>()));
            PrefabBuilder.Wire(systemsGo.AddComponent<TutorialDirector>(),
                ("m_State", DataAssetsBuilder.Load<TutorialState>(DataAssetsBuilder.c_TutorialState)),
                ("m_Input", player.GetComponent<PlayerInputProvider>()),
                ("m_Run", DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState)),
                ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Ammo", DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState)), ("m_Spawner", spawner));
        }

        // Workshop: the spot in front of the garage door (the building faces the lot, its front wall at z 6.4).
        private static readonly Vector3 s_WorkshopZone = new Vector3(-8.6f, 0f, 4.9f);
        private const float c_WorkshopZoneRadius = 2.4f;
        private const float c_WorkshopIconHeight = 2.4f;
        private const float c_WorkshopIconScale = 0.6f;
        private static readonly Color s_MarkerRingColor = new Color(1f, 0.7f, 0.3f, 0.8f);

        private static void BuildWorkshopZone(Transform gameplay)
        {
            var root = new GameObject("WorkshopZone");
            root.transform.SetParent(gameplay, false);
            root.transform.position = s_WorkshopZone;
            Material ringMaterial = FxBuilder.Material("M_FX_MarkerRing", "FX_MarkerRing", s_MarkerRingColor, false);
            MeshRenderer ring = FxBuilder.GroundQuad("Ring", root.transform, ringMaterial, c_WorkshopZoneRadius * 1.3f, 0.04f);

            var iconRoot = new GameObject("Icon");
            iconRoot.transform.SetParent(root.transform, false);
            iconRoot.transform.localPosition = Vector3.up * c_WorkshopIconHeight;
            CharacterBuilder.SetProps(iconRoot.AddComponent<YAxisBillboard>(), ("m_UpdateEveryFrame", false));
            var iconGo = new GameObject("Sprite");
            iconGo.transform.SetParent(iconRoot.transform, false);
            iconGo.transform.localScale = new Vector3(c_WorkshopIconScale, c_WorkshopIconScale * CharacterBuilder.PitchCompensation, 1f);
            var icon = iconGo.AddComponent<SpriteRenderer>();
            icon.sprite = UiFactory.Sprite("UI_Icon_Repair");
            icon.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterBuilder.c_SpriteMaterial);
            icon.sortingOrder = 50;
            icon.shadowCastingMode = ShadowCastingMode.Off;
            icon.receiveShadows = false;

            var zone = root.AddComponent<WorkshopZone>();
            PrefabBuilder.Wire(zone, ("m_Run", DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState)),
                ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Marker", iconRoot.transform), ("m_MarkerSprite", icon), ("m_Ring", ring), ("m_Icon", icon.sprite));
            CharacterBuilder.SetProps(zone, ("m_Radius", c_WorkshopZoneRadius));
        }

        // Ammo refill at the car: the spot behind the trunk (the car's nose is its local +Z), the ammo box above it.
        private const float c_ResupplyRadius = 1.5f;              // from the car's body
        private const float c_ResupplyBehind = 0.9f;              // spot behind the rear bumper
        private const float c_ResupplyIconHeight = 3.2f;                // above the head of the player standing there
        private const float c_ResupplyIconScale = 0.55f;
        private static readonly Color s_ResupplyRingColor = new Color(1f, 0.82f, 0.35f, 0.8f);

        private static void BuildCarResupply(Transform gameplay, GameObject car, GameObject player)
        {
            var root = new GameObject("AmmoRefill");
            root.transform.SetParent(gameplay, false);
            Vector3 rear = PrefabBuilder.CarPlayerColliderCenter - Vector3.forward * (PrefabBuilder.CarPlayerColliderSize.z * 0.5f + c_ResupplyBehind);
            Vector3 position = car.transform.TransformPoint(rear);
            root.transform.position = new Vector3(position.x, 0f, position.z);
            Material ringMaterial = FxBuilder.Material("M_FX_AmmoRing", "FX_MarkerRing", s_ResupplyRingColor, false);
            MeshRenderer ring = FxBuilder.GroundQuad("Ring", root.transform, ringMaterial, 1.8f, 0.04f);

            var iconRoot = new GameObject("Icon");
            iconRoot.transform.SetParent(root.transform, false);
            iconRoot.transform.localPosition = Vector3.up * c_ResupplyIconHeight;
            CharacterBuilder.SetProps(iconRoot.AddComponent<YAxisBillboard>(), ("m_UpdateEveryFrame", false));
            var iconGo = new GameObject("Sprite");
            iconGo.transform.SetParent(iconRoot.transform, false);
            iconGo.transform.localScale = new Vector3(c_ResupplyIconScale, c_ResupplyIconScale * CharacterBuilder.PitchCompensation, 1f);
            var icon = iconGo.AddComponent<SpriteRenderer>();
            icon.sprite = UiFactory.Sprite("UI_Icon_AmmoBox");
            icon.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterBuilder.c_SpriteMaterial);
            icon.sortingOrder = 50;
            icon.shadowCastingMode = ShadowCastingMode.Off;
            icon.receiveShadows = false;

            var resupply = root.AddComponent<CarResupply>();
            PrefabBuilder.Wire(resupply, ("m_Run", DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState)),
                ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Weapon", player.GetComponent<PlayerWeapon>()), ("m_View", player.GetComponentInChildren<PlayerView>()),
                ("m_Body", car.GetComponent<BoxCollider>()),
                ("m_Marker", iconRoot.transform), ("m_MarkerSprite", icon), ("m_Glow", ring), ("m_Icon", icon.sprite));
            CharacterBuilder.SetProps(resupply, ("m_Radius", c_ResupplyRadius));
        }

        // Compact world-space durability plate over the middle of the car body (anchor height; the plate is centred on
        // it, facing the camera, drawn without depth test), so it covers the car itself and not the zombies around it.
        private const float c_CarHealthBarHeight = 1.5f;
        // Canvas units (UI-sized, the plate component scales it to a fixed share of the screen height).
        private static readonly Vector2 s_CarHealthBarSize = new Vector2(380f, 64f);
        private const float c_CarHealthBarScreenHeight = 0.042f;  // share of the screen height (one row: icon, bar)
        private const int c_CarHealthBarSortingOrder = 100;          // after every sprite of the scene
        internal const string c_UiOverlayShader = "ZombiCar/UI World Overlay";
        internal const string c_TmpOverlayShader = "TextMeshPro/Mobile/Distance Field Overlay";

        private static void BuildCarHealthBar(GameObject car, GameObject player)
        {
            var anchor = new GameObject("HealthBarAnchor");
            anchor.transform.SetParent(car.transform, false);
            anchor.transform.position = car.transform.position + Vector3.up * c_CarHealthBarHeight;
            // Saved facing the default camera at its default distance (the component keeps it so at run time).
            anchor.transform.rotation = Quaternion.Euler(ViewConstants.c_CameraPitch, 0f, 0f);
            float viewHeight = 2f * ViewConstants.c_CameraDistance * Mathf.Tan(0.5f * ViewConstants.c_CameraFov * Mathf.Deg2Rad);
            anchor.transform.localScale = Vector3.one * (viewHeight * c_CarHealthBarScreenHeight / s_CarHealthBarSize.y) /
                                          car.transform.lossyScale.x;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform)) { layer = UiFactory.c_UiLayer };
            canvasGo.transform.SetParent(anchor.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = c_CarHealthBarSortingOrder;
            var group = canvasGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            var root = (RectTransform)canvas.transform;
            root.sizeDelta = s_CarHealthBarSize;
            root.pivot = new Vector2(0.5f, 0.5f);                       // centred on the car body

            Material imageMaterial = OverlayMaterial("M_UI_WorldOverlay", Shader.Find(c_UiOverlayShader), null);
            // Text keeps the HUD's black outline + shadow, on the no-depth-test overlay shader.
            Material textMaterial = OverlayMaterial("M_TMP_WorldOverlay", Shader.Find(c_TmpOverlayShader), TmpSetup.OutlineMaterial());

            Sprite panel = UiFactory.Sprite("UI_Panel");
            Sprite bar = UiFactory.Sprite("UI_Bar");
            var midLeft = new Vector2(0f, 0.5f);
            var center = new Vector2(0.5f, 0.5f);
            var images = new List<Image>();
            var texts = new List<TMP_Text>();

            // Panel: soft drop shadow, thin light rim, dark translucent body (reads over the bright ground and roof).
            RectTransform shadow = UiFactory.Stretch("Shadow", root);
            shadow.offsetMin = new Vector2(4f, -6f);
            shadow.offsetMax = new Vector2(4f, -6f);
            images.Add(UiFactory.Image(shadow, panel, new Color(0f, 0f, 0f, 0.4f), true));
            RectTransform rim = UiFactory.Stretch("Rim", root);
            rim.offsetMin = new Vector2(-2f, -2f);
            rim.offsetMax = new Vector2(2f, 2f);
            images.Add(UiFactory.Image(rim, panel, new Color(1f, 0.88f, 0.66f, 0.45f), true));
            images.Add(UiFactory.Image(UiFactory.Stretch("Background", root), panel, new Color(0.05f, 0.06f, 0.08f, 0.85f), true));

            // One row: the car icon on a light tile, then the rounded bar — dark trough with an inner shadow, light
            // "lost" trail, stage-coloured fill with a gloss, the HP number centred on top.
            RectTransform iconFrame = UiFactory.Rect("IconFrame", root, midLeft, midLeft, new Vector2(8f, 0f), new Vector2(48f, 48f));
            images.Add(UiFactory.Image(iconFrame, panel, UiFactory.TextColor, true));   // light tile: the brown icon reads on it
            RectTransform icon = Inset(UiFactory.Stretch("Icon", iconFrame), 4f);
            images.Add(UiFactory.Image(icon, UiFactory.Sprite("UI_IconCar"), Color.white, false));

            RectTransform barRt = UiFactory.Rect("Bar", root, midLeft, midLeft, new Vector2(64f, 0f),
                new Vector2(s_CarHealthBarSize.x - 72f, 46f));
            images.Add(UiFactory.Image(barRt, bar, new Color(0.02f, 0.02f, 0.03f, 1f), true));
            RectTransform lane = Inset(UiFactory.Stretch("Lane", barRt), 3f);
            Image trail = UiFactory.Image(UiFactory.Stretch("Trail", lane), bar, new Color(1f, 0.93f, 0.8f, 0.85f), true);
            images.Add(trail);
            Image fill = UiFactory.Image(UiFactory.Stretch("Fill", lane), bar, new Color(0.34f, 0.82f, 0.3f, 1f), true);
            images.Add(fill);
            RectTransform gloss = UiFactory.Stretch("Gloss", fill.rectTransform);
            gloss.anchorMin = new Vector2(0f, 0.55f);
            gloss.offsetMin = new Vector2(5f, 0f);
            gloss.offsetMax = new Vector2(-5f, -3f);
            images.Add(UiFactory.Image(gloss, bar, new Color(1f, 1f, 1f, 0.22f), true));
            RectTransform innerShadow = UiFactory.Stretch("InnerShadow", lane);
            innerShadow.anchorMin = new Vector2(0f, 0.6f);
            images.Add(UiFactory.Image(innerShadow, bar, new Color(0f, 0f, 0f, 0.35f), true));
            TextMeshProUGUI value = UiFactory.Label("Value", barRt, center, center, Vector2.zero, new Vector2(300f, 46f),
                string.Empty, 28f, TextAlignmentOptions.Center, Color.white);
            texts.Add(value);

            Image flash = UiFactory.Image(UiFactory.Stretch("Flash", root), panel, new Color(1f, 0.45f, 0.35f, 0f), true);
            images.Add(flash);

            foreach (Image image in images)
            {
                image.material = imageMaterial;
                image.raycastTarget = false;
            }
            foreach (TMP_Text text in texts)
                text.fontSharedMaterial = textMaterial;

            var healthBar = anchor.AddComponent<CarHealthBar>();
            PrefabBuilder.Wire(healthBar, ("m_Car", car.GetComponent<CarHealth>()), ("m_Plate", root), ("m_Fill", fill),
                ("m_Trail", trail), ("m_Flash", flash), ("m_Value", value), ("m_Group", group), ("m_Player", player.transform));
            CharacterBuilder.SetProps(healthBar, ("m_ScreenHeight", c_CarHealthBarScreenHeight));
            value.SetText($"{car.GetComponent<CarHealth>().MaxHealth} / {car.GetComponent<CarHealth>().MaxHealth}");
        }

        internal static RectTransform Inset(RectTransform rt, float inset)
        {
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>A material asset drawing UI with no depth test (copies <paramref name="source"/>'s properties when given).</summary>
        internal static Material OverlayMaterial(string name, Shader shader, Material source)
        {
            if (shader == null)
                throw new InvalidOperationException($"[SceneBuilder] Shader for {name} not found");
            string path = $"{EditorPaths.c_Materials}/UI/{name}.mat";
            EditorPaths.EnsureFolder($"{EditorPaths.c_Materials}/UI");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = source != null ? new Material(source) : new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (source != null)
            {
                material.CopyPropertiesFromMaterial(source);
            }
            material.shader = shader;
            if (source != null)
                material.shaderKeywords = source.shaderKeywords;   // e.g. the text shadow (underlay)
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Bakes the zombie NavMesh from the colliders on the Ground and Obstacle layers (default humanoid agent,
        /// 0.5 m clearance) and stores it next to the scene.
        /// </summary>
        private static void BuildNavMesh(Transform parent)
        {
            var go = new GameObject("NavMesh");
            go.transform.SetParent(parent, false);
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = Layers.NavMeshSources;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(c_NavMeshData) != null)
                AssetDatabase.DeleteAsset(c_NavMeshData);
            AssetDatabase.CreateAsset(surface.navMeshData, c_NavMeshData);
            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            Debug.Log($"[SceneBuilder] Map01 NavMesh: {mesh.vertices.Length} vertices, {mesh.indices.Length / 3} triangles.");
        }

        /// <summary>Logs every FBX-based prefab that Map01 does not use (all of them are expected to be placed).</summary>
        private static void ReportModelUsage(Scene scene)
        {
            var used = new HashSet<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                if (source != null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    used.Add(source.name);
            }
            string[] models = AssetDatabase.FindAssets("t:Model", new[] { EditorPaths.c_Models })
                .Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            string[] unused = models.Where(m => !used.Contains(m)).ToArray();
            Debug.Log($"[SceneBuilder] Map01 uses {models.Length - unused.Length}/{models.Length} models" +
                      (unused.Length > 0 ? $"; unused: {string.Join(", ", unused)}" : " (all of them)."));

            long triangles = 0;
            int renderers = 0;
            var materials = new HashSet<Material>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                materials.UnionWith(renderer.sharedMaterials.Where(m => m != null));
                if (!renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    triangles += filter.sharedMesh.GetIndexCount(i) / 3;
            }
            Light[] points = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .Where(l => l.type == LightType.Point).ToArray();
            int basePoints = points.Count(l => l.gameObject.activeInHierarchy);
            Debug.Log($"[SceneBuilder] Map01 stats: {renderers} mesh renderers, {triangles} triangles in the whole map, " +
                      $"{materials.Count} materials, {basePoints} realtime point lights + sun ({points.Length} on Ultra).");
        }

        private static void SetupLighting()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(c_LightingSettings);
            if (settings == null)
            {
                settings = new LightingSettings { name = "Lighting_Realtime" };
                AssetDatabase.CreateAsset(settings, c_LightingSettings);
            }
            settings.bakedGI = false;          // no lightmaps: small WebGL build, lamps are emissive + few realtime lights
            settings.realtimeGI = false;
            Lightmapping.lightingSettings = settings;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s_Look.AmbientSky;
            RenderSettings.ambientEquatorColor = s_Look.AmbientEquator;
            RenderSettings.ambientGroundColor = s_Look.AmbientGround;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(s_Look.AmbientEquator);
            sh.AddDirectionalLight(Vector3.up, s_Look.AmbientSky - s_Look.AmbientEquator, 0.5f);
            sh.AddDirectionalLight(Vector3.down, s_Look.AmbientGround - s_Look.AmbientEquator, 0.5f);
            RenderSettings.ambientProbe = sh;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = s_Look.FogColor;
            RenderSettings.fogStartDistance = c_FogStart;
            RenderSettings.fogEndDistance = c_FogEnd;
        }

        private static void BuildLights(Transform parent)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(s_Look.SunEuler);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = s_Look.SunColor;
            sun.intensity = s_Look.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            RenderSettings.sun = sun;

            // Few dynamic point lights (the gas sign's two lanterns: GasSignLanternLights); every other lamp is emission only.
            PointLight("Light_WorkshopGarage", parent, s_WorkshopPosition + new Vector3(0.5f, 2.6f, -2.3f), 4f, 8f);
            PointLight("Light_StreetLamp", parent, new Vector3(-0.1f, 3.45f, 13.4f), 3f, 7f);
        }

        // The gas sign is lit by its own two lanterns (the glowing glass of the Sign_Gas_Lamps part), not by a light
        // floating in front of the board: one light just under and in front of each lantern.
        private const float c_SignLanternIntensity = 1.7f;
        private const float c_SignLanternRange = 5f;
        private static readonly Vector3 s_SignLanternLightOffset = new Vector3(0f, -0.2f, -0.3f);   // below, towards the lot

        private static void GasSignLanternLights(Transform lighting, Transform sign)
        {
            MeshRenderer lamps = sign != null
                ? sign.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.EndsWith("Lamps"))
                : null;
            int glass = lamps != null ? Array.FindIndex(lamps.sharedMaterials, m => m != null && m.name == "M_Signs_Emissive") : -1;
            if (glass < 0)
            {
                Debug.LogError("[SceneBuilder] Sign_Gas lanterns (Sign_Gas_Lamps / M_Signs_Emissive) not found: the sign stays unlit.");
                return;
            }
            Mesh mesh = lamps.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            foreach (int index in mesh.GetTriangles(glass).Distinct())
            {
                Vector3 world = lamps.transform.TransformPoint(vertices[index]);
                (sign.InverseTransformPoint(world).x < 0f ? left : right).Add(world);
            }
            int n = 0;
            foreach (List<Vector3> lantern in new[] { left, right })
            {
                if (lantern.Count == 0)
                    continue;
                Vector3 center = lantern.Aggregate(Vector3.zero, (sum, v) => sum + v) / lantern.Count;
                PointLight($"Light_GasSignLantern{++n}", lighting, center + s_SignLanternLightOffset, c_SignLanternIntensity, c_SignLanternRange);
            }
        }

        private static void PointLight(string name, Transform parent, Vector3 position, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = s_LampColor;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }

        private static void BuildCamera(Transform parent, Transform target, MapConfig map)
        {
            float pitch = ViewConstants.c_CameraPitch;
            Vector3 offset = Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0f, -ViewConstants.c_CameraDistance);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(parent, false);
            camGo.transform.SetPositionAndRotation(target.position + offset, Quaternion.Euler(pitch, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = ViewConstants.c_CameraFov;
            cam.nearClipPlane = ViewConstants.c_NearClip;
            cam.farClipPlane = ViewConstants.c_FarClip;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = s_Look.FogColor;
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<CinemachineBrain>();
            camGo.AddComponent<AudioListener>();

            var volumeGo = new GameObject("Global Volume");
            volumeGo.transform.SetParent(parent, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = s_Look.Night ? BuildNightVolumeProfile() : BuildVolumeProfile();
            PrefabBuilder.Wire(camGo.AddComponent<QualityPostProcessApplier>(), ("m_Volume", volume), ("m_CameraData", camData));

            var vcamGo = new GameObject("CM_FollowPlayer");
            vcamGo.transform.SetParent(parent, false);
            vcamGo.transform.SetPositionAndRotation(target.position + offset, Quaternion.Euler(pitch, 0f, 0f));
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = ViewConstants.c_CameraFov;
            vcam.Lens.NearClipPlane = ViewConstants.c_NearClip;
            vcam.Lens.FarClipPlane = ViewConstants.c_FarClip;
            // The camera follows a lead point (player + look-ahead + aim offset, CameraConfig), not the player itself.
            var cameraConfig = DataAssetsBuilder.Load<CameraConfig>(DataAssetsBuilder.c_CameraConfig);
            var followTargetGo = new GameObject("CameraFollowTarget");
            followTargetGo.transform.SetParent(parent, false);
            followTargetGo.transform.position = target.position;
            vcam.Target.TrackingTarget = followTargetGo.transform;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = cameraConfig.WorldDamping;
            PrefabBuilder.Wire(followTargetGo.AddComponent<CameraFollowTarget>(), ("m_Config", cameraConfig), ("m_Player", target),
                ("m_InputProvider", target.GetComponent<PlayerInputProvider>()),
                ("m_Weapon", target.GetComponent<PlayerWeapon>()), ("m_Follow", follow));
            vcamGo.AddComponent<CameraAspectAdapter>();
            vcamGo.AddComponent<CameraShake>();

            var boundsGo = new GameObject("CameraBounds") { layer = Layers.c_CameraBoundsIndex };
            boundsGo.transform.SetParent(parent, false);
            var box = boundsGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var confinerFromMap = boundsGo.AddComponent<CameraConfinerFromMap>();
            PrefabBuilder.Wire(confinerFromMap, ("m_Map", map), ("m_Follow", follow));
            confinerFromMap.Apply();
            var confiner = vcamGo.AddComponent<CinemachineConfiner3D>();
            confiner.BoundingVolume = box;
            confiner.SlowingDistance = 2f;
        }

        private static VolumeProfile BuildVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(c_VolumeProfile) != null)
                AssetDatabase.DeleteAsset(c_VolumeProfile);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, c_VolumeProfile);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.7f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(5);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.12f, 0.06f, 0.03f));

            var color = AddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0.05f);
            color.contrast.Override(14f);
            color.saturation.Override(10f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            var dof = AddOverride<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(38f);
            dof.gaussianEnd.Override(75f);
            dof.gaussianMaxRadius.Override(1.1f);
            dof.highQualitySampling.Override(false);
            dof.active = false;   // enabled on the High quality tier by QualityPostProcessApplier

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------ ground, roads, background
        private static void BuildGround(Transform environment, Transform background, MapConfig map)
        {
            Place("Env_Ground", environment, map.GroundCenter.x, map.GroundCenter.z);
            // North road (z 17.5..24.5) with sidewalks across the whole ground, east road (x 25..32) with sidewalks
            // from the north sidewalk (z 15.8, so no two strips overlap) to the south edge of the ground.
            Place("Env_Road_North", background, map.GroundCenter.x, 21f, 0f, 1f, c_RoadHeight);
            Place("Env_Sidewalk_North", background, map.GroundCenter.x, 16.65f, 0f, 1f, c_SidewalkHeight);
            Place("Env_Sidewalk_North", background, map.GroundCenter.x, 25.35f, 0f, 1f, c_SidewalkHeight);
            float eastStripZ = PrefabBuilder.c_NorthSidewalkEdge - PrefabBuilder.EastRoadLength(map) * 0.5f;
            Place("Env_Road_East", background, 28.5f, eastStripZ, 90f, 1f, c_RoadHeight);
            Place("Env_Sidewalk_East", background, 24.15f, eastStripZ, 90f, 1f, c_SidewalkHeight);
            Place("Env_Sidewalk_East", background, 32.85f, eastStripZ, 90f, 1f, c_SidewalkHeight);

            // Street furniture outside the lot.
            foreach (float x in new[] { -38f, -16f, 40f })   // the lamp at x 11 (by the gas sign) removed on request
                Place("Lamp_Street", background, x, 16.7f, 90f);
            foreach (float z in new[] { -12f, 6f })
                Place("Pole_Power", background, 33.8f, z, 90f);
            foreach (float x in new[] { -32f, -12f, 8f })
                Place("Pole_Power", background, x, -22f, 0f);
        }

        // ------------------------------------------------------------------ fences around the play area
        private static void BuildFences(Transform parent, MapConfig map)
        {
            float fenceX = map.PlayAreaCenter.x + map.PlayAreaSize.x * 0.5f - c_FenceInset;
            float fenceZ = map.PlayAreaCenter.z + map.PlayAreaSize.y * 0.5f - c_FenceInset;
            Place("Fence_Corner", parent, -fenceX, fenceZ, 270f);
            Place("Fence_Corner", parent, fenceX, fenceZ, 0f);
            Place("Fence_Corner", parent, fenceX, -fenceZ, 90f);
            Place("Fence_Corner", parent, -fenceX, -fenceZ, 180f);

            float start = -fenceX + c_CornerArm, end = fenceX - c_CornerArm;
            // North gate (to the road) and east exit (closed with barriers): whole fence sections are left out and
            // the opening is filled edge to edge, so there is no slit the player could squeeze through.
            (float from, float to) gate = FenceRun(parent, true, fenceZ, start, end, 180f, 11, (4f, 7.4f));
            GameObject gateGo = Place("Fence_Gate", parent, (gate.from + gate.to) * 0.5f, fenceZ, 180f);
            gateGo.transform.localScale = new Vector3((gate.to - gate.from) / c_GateModelWidth, 1f, 1f);
            FenceRun(parent, true, -fenceZ, start, end, 180f, 12);

            float zStart = -fenceZ + c_CornerArm, zEnd = fenceZ - c_CornerArm;
            FenceRun(parent, false, -fenceX, zStart, zEnd, 90f, 13);
            (float from, float to) exit = FenceRun(parent, false, fenceX, zStart, zEnd, 270f, 14, (-1.2f, 1.2f));
            int barriers = Mathf.CeilToInt((exit.to - exit.from) / c_BarrierModelWidth - 0.01f);
            float barrierStep = (exit.to - exit.from) / barriers;
            for (int i = 0; i < barriers; i++)
                Place("Barrier", parent, fenceX, exit.from + barrierStep * (i + 0.5f), 90f);
        }

        /// <summary>
        /// Lays 2 m sections from <paramref name="from"/> to <paramref name="to"/>; sections overlapping
        /// <paramref name="gap"/> are left out. Returns the exact opening that was left (empty if none).
        /// </summary>
        private static (float from, float to) FenceRun(Transform parent, bool alongX, float line, float from, float to, float yaw,
            int seed, (float a, float b) gap = default)
        {
            var rnd = new Random(seed + (s_Look.Night ? c_NightLayoutSeed : 0));
            float openFrom = float.MaxValue, openTo = float.MinValue;
            for (float c = from + c_FenceSection * 0.5f; c <= to - c_FenceSection * 0.5f + 0.01f; c += c_FenceSection)
            {
                float lo = c - c_FenceSection * 0.5f, hi = c + c_FenceSection * 0.5f;
                if (gap.b > gap.a && hi > gap.a && lo < gap.b)
                {
                    openFrom = Mathf.Min(openFrom, lo);
                    openTo = Mathf.Max(openTo, hi);
                    continue;
                }
                double r = rnd.NextDouble();
                string type = r < 0.12 ? "Fence_Broken" : r < 0.37 ? "Fence_Straight2" : "Fence_Straight1";
                if (alongX)
                    Place(type, parent, c, line, yaw);
                else
                    Place(type, parent, line, c, yaw);
            }
            return openTo > openFrom ? (openFrom, openTo) : (0f, 0f);
        }

        // ------------------------------------------------------------------ buildings, signs, parked cars
        private static void BuildBuildingsAndSigns(Transform parent)
        {
            // North-west: the workshop, garage facing the lot (south) like in the concept.
            // Building at scale 1.7: x -14.3..-3.7, z 6.4..14.2; its soil slab x -15.9..-2.1, z 4.3..15.5
            // (stops short of the north sidewalk at z 15.8).
            Place("Workshop", parent, s_WorkshopPosition.x, s_WorkshopPosition.z, 180f, c_WorkshopScale, c_WorkshopLift);
            Place("Car_Pickup", parent, -18.5f, 5.8f, 90f);                   // left of the workshop, nose to the right
            Place("Lamp_Street", parent, -1f, 13.4f, 180f);
            Place("Lamp_Street", parent, 8.6f, 13.4f, 180f);

            // North-east: the gas station / cafe sign just beyond the fence, the sedan parked in front of it,
            // the road sign by the east fence.
            Place("Sign_Gas", parent, 16.5f, 16.5f, 180f, c_GasSignScale);
            Place("Car_Sedan", parent, 16.8f, 10.8f, 235f);                   // nose towards the lot, like the concept
            Place("Sign_Road", parent, 20.7f, 8f, 210f, c_RoadSignScale);
            Place("Pole_Power", parent, 20.4f, -5f, 90f);

            // Background trees beyond the north road (generated low-poly models, TreeBuilder).
            Place($"{TreeBuilder.c_Prefix}1", parent, 2.5f, 17f);
            Place($"{TreeBuilder.c_Prefix}2", parent, 28f, 17f, 30f);
            // Concept objects without a model: stand-in cubes (see the build report).
            Place("Stub_Hydrant", parent, 5f, 16.7f);
            Place("Stub_Bicycle", parent, -13.5f, -13.6f, 8f);
        }

        private static void BuildProps(Transform p)
        {
            // Workshop yard (north-west): red cabinets left of the garage, junk around the parked pickup.
            Place("Shelf_Red", p, -13.6f, 5.8f, 180f);
            Place("Toolbox_Red", p, -12.4f, 5.7f, 180f);
            Place("Crate_Small2", p, -16.6f, 2.4f, 15f);
            Place("Prop_Cardboard_In", p, -15f, 3.6f, 20f);
            Place("Tires_Stack", p, -20.3f, 11.2f);
            Place("Tire_Single", p, -19.4f, 1.4f, 30f);
            Place("Shelf_Metal", p, -20.5f, 13.4f, 180f);
            Place("Prop_Workbench", p, -16.6f, 13.6f, 180f);
            Place("Prop_TireStack_In", p, -18.9f, 13.5f);
            Place("Prop_Cabinet", p, -20.3f, 3.2f, 90f);
            Place("Workbench", p, -2.4f, 11.3f, 90f);
            Place("Cart_Tools", p, -2.2f, 8f, 200f);
            // No barrels or canisters by the workshop (removed on request): the yard stays clear.

            // North fence line (the gate is at x 3.7..7.7). The two barrels right of the workshop door were removed on
            // request (no barrels by the workshop).
            Place("Cart_Cargo", p, 2.6f, 12f, 12f);
            Place("Crate_Big", p, 9.8f, 13.5f, 5f);
            Place("Crate_Small1", p, 11.1f, 13.7f, -8f);
            Place("Crate_Small3", p, 10.4f, 12.5f, 20f);
            Place("Prop_Crate_In", p, 12.8f, 13.5f);
            Place("Prop_Cardboard_In", p, 13.6f, 13.8f, -15f);
            Place("Prop_Barrel_Back", p, 15f, 13.6f, 30f);

            // North-east corner and east exit.
            Place("Barrel_Grey", p, 20.4f, 13.3f, 70f);
            Place("Tires_Stack", p, 20.5f, 5.4f);
            Place("Cone", p, 17.7f, 2.5f, 10f);
            Place("Cone", p, 19f, 1.8f, -20f);
            Place("Cone", p, 19.3f, -2.2f, 35f);

            // South-east.
            Place("Crate_Big", p, 15f, -11.2f, 12f);
            Place("Crate_Small3", p, 13.7f, -12.3f, -15f);
            Place("Crate_Small1", p, 16.3f, -12.6f, 5f);
            Place("Tire_Single", p, 8.5f, -12.3f, 40f);
            Place("Prop_TireStack_Out", p, 19.5f, -12.5f);
            Place("Prop_Crate_Back", p, 19.3f, -9.4f, 20f);
            Place("Prop_Boxes_Out", p, 18.9f, -7.6f, 270f);
            Place("Barrel_RedWhite", p, 10.7f, -11.9f, 150f);

            // South-west and west.
            Place("Barrel_RedWhite", p, -11.5f, -12.4f, 30f);
            Place("Tire_Single", p, -15.6f, -11.9f, 10f);
            Place("Tires_Stack", p, -19.8f, -9.3f);
            Place("Prop_Barrel", p, -19.6f, -6.8f);
            Place("Prop_Fence_L", p, -18.8f, -1.2f, 15f);
            Place("Prop_Fence_R", p, -18.5f, 2.2f, -10f);
            Place("Workshop_Vegetation", p, -30f, 3f);
        }

        // Red explosive barrels out in the lot (the ones by the fences are in BuildProps): where the zombies walk in
        // from the spawn points towards the car, clear of the car's free circle. None by the workshop (removed on
        // request: the one at (-13.5, 1.5) stood in front of it). The two on the right were moved by hand in Map01.
        private static readonly (float x, float z, float yaw)[] s_LotExplosives =
        {
            (-11.5f, -6.5f, 20f), (9.327f, -6.497f, 140f), (12.695f, 5.071f, 70f),
        };

        private static void BuildExplosives(Transform parent)
        {
            foreach ((float x, float z, float yaw) in s_LotExplosives)
                Place("Barrel_RedWhite", parent, x, z, yaw);
        }

        // ------------------------------------------------------------------ nature
        private const float c_FenceBushScale = 1.3f;        // fence-line bushes: 1.3 .. 1.8
        private const float c_FenceBushScaleRange = 0.5f;
        private const float c_LotGrassScale = 1.1f;         // grass tufts: 1.1 .. 1.6 (the cards are 0.7 m)
        private const float c_EdgeGrassScale = 1.3f;        // tufts hugging the fences: 1.3 .. 1.8
        private const float c_GrassScaleRange = 0.5f;

        private static void BuildNature(Transform parent, MapConfig map)
        {
            (string name, float x, float z)[] bushes =
            {
                ("Bush1", -19.5f, -13.4f), ("Bush2", -17.8f, -13.8f), ("Bush4", -16.1f, -13.3f), ("Bush3", -10.5f, -13.7f),
                ("Bush5", -9.3f, -13.3f), ("Bush1", -4f, -13.8f), ("Bush4", 3.5f, -13.6f), ("Bush2", 8f, -13.5f),
                ("Bush3", 9.2f, -13.9f), ("Bush5", 17.8f, -13.6f), ("Bush1", 19.6f, -13.2f),
                ("Bush2", -20.7f, -4.6f), ("Bush3", -20.6f, -2.8f), ("Bush4", -20.9f, -0.2f), ("Bush5", -3f, 13.9f),
                ("Bush1", 16.2f, 13.9f), ("Bush3", 18.3f, 13.9f), ("Bush2", 21f, 11.2f), ("Bush4", 20.7f, -6.4f),
                ("Bush5", 20.8f, -11f), ("Bush2", 20.3f, 14f),
            };
            var rnd = new Random(21);
            foreach ((string name, float x, float z) in bushes)
                Place(name, parent, x, z, (float)rnd.NextDouble() * 360f, c_FenceBushScale + (float)rnd.NextDouble() * c_FenceBushScaleRange);
            FenceGreenery(parent, map, new Random(9));

            Place("Rock1", parent, -18.9f, -12.1f, 20f);
            Place("Rock2", parent, 19.3f, -3.5f, 120f);
            Place("Rock3", parent, -5.5f, -12.6f);
            Place("Rock2", parent, -36f, -14f, 45f, 1.5f);
            Place("Rock3", parent, -32f, 19f, 90f, 1.3f);

            // Grass inside the lot grows in clumps that hug the fences (the concept's overgrown edges); the open
            // centre gets only a few stray tufts. The workshop and the parked cars stay clear.
            var lotRnd = new Random(7);
            Vector3 lotCenter = map.PlayAreaCenter;
            float lotX = map.PlayAreaSize.x * 0.5f - 1.4f, lotZ = map.PlayAreaSize.y * 0.5f - 1.4f;
            int clumps = 0;
            for (int attempt = 0; attempt < 800 && clumps < c_LotGrassClumps; attempt++)
            {
                float x = lotCenter.x + Mathf.Lerp(-lotX, lotX, (float)lotRnd.NextDouble());
                float z = lotCenter.z + Mathf.Lerp(-lotZ, lotZ, (float)lotRnd.NextDouble());
                float toEdge = Mathf.Min(lotX - Mathf.Abs(x - lotCenter.x), lotZ - Mathf.Abs(z - lotCenter.z));
                bool edge = toEdge < c_LotGrassEdgeBand;
                if (lotRnd.NextDouble() > (edge ? 1.0 : c_LotGrassCentreChance) || !IsFreeForGrass(x, z, map))
                    continue;
                int tufts = edge ? 3 + lotRnd.Next(4) : 1;
                for (int t = 0; t < tufts; t++)
                {
                    double angle = lotRnd.NextDouble() * Math.PI * 2.0;
                    float r = t == 0 ? 0f : c_LotGrassClumpRadius * (0.35f + 0.65f * (float)lotRnd.NextDouble());
                    float tx = x + r * (float)Math.Cos(angle), tz = z + r * (float)Math.Sin(angle);
                    if (IsFreeForGrass(tx, tz, map))
                        PlaceGrass(parent, lotRnd, tx, tz, c_LotGrassScale * (1f - 0.35f * r / c_LotGrassClumpRadius));   // smaller at the rim
                }
                clumps++;
            }
            // Outside the fences (verges), within the visible ground.
            var outRnd = new Random(8);
            float groundX = map.GroundSize.x * 0.5f - 2f;
            float groundSouth = map.GroundCenter.z - map.GroundSize.y * 0.5f + 1.5f;
            for (int i = 0; i < 45; i++)
            {
                bool south = i % 2 == 0;
                float x = south ? Mathf.Lerp(-groundX, groundX, (float)outRnd.NextDouble()) : Mathf.Lerp(-40f, -23f, (float)outRnd.NextDouble());
                float z = south ? Mathf.Lerp(groundSouth, -16f, (float)outRnd.NextDouble()) : Mathf.Lerp(groundSouth, 15f, (float)outRnd.NextDouble());
                if (x > 23f && x < 34f)
                    continue;                                                         // east road and its sidewalks
                PlaceGrass(parent, outRnd, x, z, c_EdgeGrassScale);
                if (i % 3 == 0)
                    Place($"Bush{1 + outRnd.Next(5)}", parent, x + 1.5f, z + 0.7f, (float)outRnd.NextDouble() * 360f,
                        1f + (float)outRnd.NextDouble() * 0.4f);
            }
        }

        /// <summary>Grass tufts along the inside of the fence lines (the concept's lot edges are overgrown).</summary>
        private static void FenceGreenery(Transform parent, MapConfig map, Random rnd)
        {
            const float inset = 1f, step = 1.6f, chance = 0.6f, jitter = 0.8f;
            Vector3 c = map.PlayAreaCenter;
            float fx = map.PlayAreaSize.x * 0.5f - c_FenceInset - inset;
            float fz = map.PlayAreaSize.y * 0.5f - c_FenceInset - inset;
            var spots = new List<Vector2>();
            for (float x = -fx; x <= fx; x += step)
            {
                spots.Add(new Vector2(c.x + x, c.z + fz));
                spots.Add(new Vector2(c.x + x, c.z - fz));
            }
            for (float z = -fz + step; z < fz; z += step)
            {
                spots.Add(new Vector2(c.x - fx, c.z + z));
                spots.Add(new Vector2(c.x + fx, c.z + z));
            }
            foreach (Vector2 spot in spots)
            {
                if (rnd.NextDouble() > chance)
                    continue;
                float x = spot.x + (float)(rnd.NextDouble() - 0.5) * jitter;
                float z = spot.y + (float)(rnd.NextDouble() - 0.5) * jitter;
                if (IsFreeForGrass(x, z, map))
                    PlaceGrass(parent, rnd, x, z, c_EdgeGrassScale);
            }
        }

        private const int c_LotGrassClumps = 16;
        private const float c_LotGrassEdgeBand = 4f;          // metres from the fence where clumps grow
        private const double c_LotGrassCentreChance = 0.08;   // share of centre candidates kept (as single tufts)
        private const float c_LotGrassClumpRadius = 1.2f;

        private static bool IsFreeForGrass(float x, float z, MapConfig map)
        {
            if (x * x + z * z < (map.FreeZoneRadius + 0.5f) * (map.FreeZoneRadius + 0.5f))
                return false;
            if (x > -16.2f && x < -1.8f && z > 4f)                                   // workshop + yard
                return false;
            if (Near(x, z, -18.5f, 6.5f, 3.4f) || Near(x, z, 16.8f, 11.3f, 3.3f))    // parked cars
                return false;
            float eastFence = map.PlayAreaCenter.x + map.PlayAreaSize.x * 0.5f - c_FenceInset;
            return !(Mathf.Abs(x - eastFence) < 1.2f && Mathf.Abs(z) < 3.5f);        // east exit
        }

        private static bool Near(float x, float z, float cx, float cz, float r) => (x - cx) * (x - cx) + (z - cz) * (z - cz) < r * r;

        private static void PlaceGrass(Transform parent, Random rnd, float x, float z, float minScale)
        {
            string name = $"Grass{1 + rnd.Next(4)}";
            Place(name, parent, x, z, (float)rnd.NextDouble() * 360f, minScale + (float)rnd.NextDouble() * c_GrassScaleRange);
        }

        // ------------------------------------------------------------------ decals
        private static void BuildDecals(Transform parent)
        {
            // Few, varied marks: each is stretched differently (x / z scale) and turned, so the two pothole and crack
            // textures never read as the same stamp twice. The dark potholes are the fewest.
            (string name, float x, float z, float yaw, float scaleX, float scaleZ)[] decals =
            {
                ("Decal_Crack1", -9.8f, -4.2f, 20f, 1.25f, 0.8f), ("Decal_Crack2", 5.6f, -8.4f, 110f, 0.9f, 1.35f),
                ("Decal_Crack1", 11.9f, 2.8f, 245f, 0.75f, 1.1f), ("Decal_Crack2", -1.5f, 2.2f, 150f, 1.1f, 0.7f),
                ("Decal_Crack1", -14f, -9.1f, 300f, 1.4f, 1f), ("Decal_Crack2", 16.8f, -8.4f, 35f, 0.8f, 0.95f),
                ("Decal_Pothole1", -12f, 2f, 130f, 1.3f, 0.75f), ("Decal_Pothole2", 11.2f, 7.7f, 200f, 0.7f, 0.95f),
                ("Decal_Pothole1", 2.8f, -10.5f, 280f, 0.85f, 0.6f), ("Decal_Pothole2", 15.4f, -1.4f, 40f, 1.05f, 1.2f),
                ("Decal_Manhole", 10.9f, -7.4f, 0f, 1f, 1f),
                // No stone decals: removed from the lot on request.
            };
            // Neighbouring decals may overlap: each one gets its own height step.
            for (int i = 0; i < decals.Length; i++)
            {
                (string name, float x, float z, float yaw, float scaleX, float scaleZ) = decals[i];
                GameObject decal = Place(name, parent, x, z, yaw, 1f, i * c_DecalStep);
                decal.transform.localScale = new Vector3(scaleX, 1f, scaleZ);
            }
        }

        // ------------------------------------------------------------------ helpers
        private static Transform Root(string name) => new GameObject(name).transform;

        private static GameObject PrefabAsset(string name)
        {
            if (s_PrefabIndex == null)
            {
                s_PrefabIndex = new Dictionary<string, string>();
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EditorPaths.c_Prefabs }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    s_PrefabIndex[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }
            if (!s_PrefabIndex.TryGetValue(name, out string prefabPath))
                throw new InvalidOperationException($"[SceneBuilder] Prefab '{name}' not found under {EditorPaths.c_Prefabs}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject Place(string prefab, Transform parent, float x, float z, float yaw = 0f, float scale = 1f, float y = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(PrefabAsset(prefab), parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
    }
}
