using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.UI;
using ZombiCar.Waves;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Build Scenes: Boot, MainMenu, UI_HUD and Map01 (assembled only from prefabs), plus Build Settings.
    /// Map01 follows referens/concept.png scaled up to the 44 x 30 m play area: workshop in the north-west, gas-station
    /// sign and sedan in the north-east, the player car in the free centre, fences around, road and houses outside.
    /// Flat layers are kept at least ~1 cm apart (see the c_*Height constants and PrefabBuilder) so they never z-fight.
    /// </summary>
    public static class SceneBuilder
    {
        public const string c_BootScene = EditorPaths.c_Scenes + "/Boot.unity";
        public const string c_MenuScene = EditorPaths.c_Scenes + "/MainMenu.unity";
        public const string c_MapScene = EditorPaths.c_Scenes + "/Map01.unity";
        public const string c_HudScene = EditorPaths.c_Scenes + "/UI_HUD.unity";
        private const string c_VolumeProfile = EditorPaths.c_Settings + "/Map01_VolumeProfile.asset";
        private const string c_LightingSettings = EditorPaths.c_Settings + "/Lighting_Realtime.lighting";
        private const string c_FontAsset = EditorPaths.c_Fonts + "/UI_Font SDF.asset";

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

        // Look (sunset).
        private static readonly Color s_SunColor = new Color(1f, 0.86f, 0.7f);
        private const float c_SunIntensity = 2f;
        private static readonly Vector3 s_SunEuler = new Vector3(40f, 70f, 0f);
        private static readonly Color s_AmbientSky = new Color(0.46f, 0.52f, 0.66f);
        private static readonly Color s_AmbientEquator = new Color(0.42f, 0.40f, 0.40f);   // deeper shadows, like the concept
        private static readonly Color s_AmbientGround = new Color(0.28f, 0.24f, 0.22f);
        private static readonly Color s_FogColor = new Color(0.68f, 0.58f, 0.52f);
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
            BuildBoot();
            BuildMainMenu();
            BuildHud();
            BuildMap01();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(c_BootScene, true), new EditorBuildSettingsScene(c_MenuScene, true),
                new EditorBuildSettingsScene(c_MapScene, true), new EditorBuildSettingsScene(c_HudScene, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Boot, MainMenu, UI_HUD, Map01 built and added to Build Settings.");
        }

        // ================================================================== Boot / MainMenu / HUD
        private static void BuildBoot()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera(Color.black);
            var boot = new GameObject("Boot").AddComponent<BootLoader>();
            PrefabBuilder.Wire(boot, ("m_Settings", DataAssetsBuilder.Load<GameSettings>(DataAssetsBuilder.c_GameSettings)));
            EditorSceneManager.SaveScene(scene, c_BootScene);
        }

        private static void BuildMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera(new Color(0.11f, 0.09f, 0.09f));
            Canvas canvas = UiFactory.Canvas("MenuCanvas", 0);
            RectTransform safe = UiFactory.Stretch("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var center = new Vector2(0.5f, 0.5f);
            UiFactory.Label("Title", safe, center, center, new Vector2(0f, 250f), new Vector2(1500f, 200f),
                "ZOMBIES CAR", 150f, TextAlignmentOptions.Center, UiFactory.AccentColor);
            UiFactory.Label("Subtitle", safe, center, center, new Vector2(0f, 120f), new Vector2(1400f, 70f),
                "Защити машину от волн зомби", 44f, TextAlignmentOptions.Center, UiFactory.TextColor, false);
            Button play = UiFactory.Button("PlayButton", safe, center, center, new Vector2(0f, -60f), new Vector2(480f, 150f),
                "ИГРАТЬ", 70f, UiFactory.AccentColor, new Color(0.12f, 0.08f, 0.06f));
            var bottom = new Vector2(0.5f, 0f);
            UiFactory.Label("Controls", safe, bottom, bottom, new Vector2(0f, 60f), new Vector2(1600f, 60f),
                "WASD — движение   ·   мышь — прицел   ·   ЛКМ — огонь", 36f, TextAlignmentOptions.Center,
                new Color(0.75f, 0.72f, 0.68f), false);

            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            PrefabBuilder.Wire(menu, ("m_Map", DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_MapConfig)), ("m_PlayButton", play));
            SaveUiPrefab(canvas.gameObject, "MainMenu");
            UiFactory.EventSystem();
            EditorSceneManager.SaveScene(scene, c_MenuScene);
        }

        private static void BuildHud()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Canvas canvas = UiFactory.Canvas("HUDCanvas", 10);
            RectTransform safe = UiFactory.Stretch("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Sprite panel = UiFactory.Sprite("UI_Panel");
            Sprite bar = UiFactory.Sprite("UI_Bar");
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var topCenter = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            // Match-height scaling keeps 1080 units of height, so a 4:3 screen is only 1440 units wide: the three top
            // panels + settings button (24 + 520 | 250 | 410 + 24 + 112 + 24) are sized to fit it without overlap.
            const float margin = 24f;
            const float buttonSize = 112f;

            // Health + level (top-left).
            RectTransform health = UiFactory.Rect("HealthPanel", safe, topLeft, topLeft, new Vector2(margin, -margin), new Vector2(520f, 150f));
            UiFactory.Image(health, panel, UiFactory.PanelColor, true);
            RectTransform frame = UiFactory.Rect("PortraitFrame", health, topLeft, topLeft, new Vector2(14f, -14f), new Vector2(122f, 122f));
            UiFactory.Image(frame, panel, new Color(0.32f, 0.14f, 0.09f, 1f), true);
            RectTransform portrait = UiFactory.Stretch("Portrait", frame);
            portrait.offsetMin = new Vector2(6f, 6f);
            portrait.offsetMax = new Vector2(-6f, -6f);
            UiFactory.Image(portrait, UiFactory.Sprite("UI_Portrait"), Color.white, false);
            UiFactory.Label("HealthLabel", health, topLeft, topLeft, new Vector2(150f, -12f), new Vector2(240f, 40f),
                "ЗДОРОВЬЕ", 28f, TextAlignmentOptions.Left, UiFactory.TextColor);
            RectTransform healthBar = UiFactory.Rect("HealthBar", health, topLeft, topLeft, new Vector2(150f, -54f), new Vector2(354f, 40f));
            UiFactory.Image(healthBar, bar, new Color(0.22f, 0.06f, 0.05f, 1f), true);
            RectTransform healthFillRt = UiFactory.Stretch("Fill", healthBar);
            healthFillRt.offsetMin = new Vector2(4f, 4f);
            healthFillRt.offsetMax = new Vector2(-4f, -4f);
            Image healthFill = UiFactory.Fill(healthFillRt, bar, new Color(0.86f, 0.2f, 0.16f, 1f), 0.84f);
            TextMeshProUGUI healthValue = UiFactory.Label("HealthValue", healthBar, midRight, midRight, new Vector2(-12f, 0f),
                new Vector2(180f, 40f), "84 / 100", 26f, TextAlignmentOptions.Right, Color.white);
            TextMeshProUGUI levelLabel = UiFactory.Label("LevelLabel", health, topLeft, topLeft, new Vector2(150f, -102f),
                new Vector2(150f, 36f), "УРОВЕНЬ 4", 23f, TextAlignmentOptions.Left, UiFactory.TextColor);
            RectTransform levelBar = UiFactory.Rect("LevelBar", health, topLeft, topLeft, new Vector2(300f, -108f), new Vector2(204f, 24f));
            UiFactory.Image(levelBar, bar, new Color(0.08f, 0.12f, 0.2f, 1f), true);
            RectTransform levelFillRt = UiFactory.Stretch("Fill", levelBar);
            levelFillRt.offsetMin = new Vector2(3f, 3f);
            levelFillRt.offsetMax = new Vector2(-3f, -3f);
            Image levelFill = UiFactory.Fill(levelFillRt, bar, new Color(0.26f, 0.62f, 0.98f, 1f), 0.35f);

            // Wave + timer (top-centre).
            RectTransform wave = UiFactory.Rect("WavePanel", safe, topCenter, topCenter, new Vector2(0f, -margin), new Vector2(250f, 130f));
            UiFactory.Image(wave, panel, UiFactory.PanelColor, true);
            TextMeshProUGUI waveLabel = UiFactory.Label("WaveLabel", wave, topCenter, topCenter, new Vector2(0f, -8f),
                new Vector2(236f, 56f), "ВОЛНА 5", 42f, TextAlignmentOptions.Center, UiFactory.TextColor);
            var bottomCenter = new Vector2(0.5f, 0f);
            TextMeshProUGUI waveTimer = UiFactory.Label("WaveTimer", wave, bottomCenter, bottomCenter, new Vector2(0f, 10f),
                new Vector2(236f, 60f), "00:36", 48f, TextAlignmentOptions.Center, UiFactory.TextColor);

            // Ammo (top-right, left of the settings button).
            RectTransform ammo = UiFactory.Rect("AmmoPanel", safe, topRight, topRight, new Vector2(-(margin * 2f + buttonSize), -margin),
                new Vector2(410f, 90f));
            UiFactory.Image(ammo, panel, UiFactory.PanelColor, true);
            RectTransform ammoIcon = UiFactory.Rect("AmmoIcon", ammo, midLeft, midLeft, new Vector2(12f, 0f), new Vector2(64f, 64f));
            UiFactory.Image(ammoIcon, UiFactory.Sprite("UI_IconAmmo"), Color.white, false);
            UiFactory.Label("AmmoLabel", ammo, midLeft, midLeft, new Vector2(84f, 0f), new Vector2(150f, 50f),
                "ПАТРОНЫ", 24f, TextAlignmentOptions.Left, UiFactory.TextColor);
            TextMeshProUGUI ammoValue = UiFactory.Label("AmmoValue", ammo, midRight, midRight, new Vector2(-16f, 0f),
                new Vector2(160f, 50f), "24 / 120", 36f, TextAlignmentOptions.Right, UiFactory.TextColor);
            TextMeshProUGUI reload = UiFactory.Label("ReloadHint", safe, topRight, topRight, new Vector2(-(margin * 2f + buttonSize), -122f),
                new Vector2(410f, 40f), "ПЕРЕЗАРЯДКА...", 26f, TextAlignmentOptions.Right, UiFactory.AccentColor);
            reload.gameObject.SetActive(false);

            // Settings button: 112 visible, touch area >= 48 dp (the visible panel keeps the 24 margin) and window.
            float touchInset = (UiFactory.c_MinTouchTarget - buttonSize) * 0.5f;
            Button settingsButton = UiFactory.IconButton("SettingsButton", safe, topRight, topRight,
                new Vector2(-(margin - touchInset), -(margin - touchInset)), buttonSize, UiFactory.Sprite("UI_IconGear"), 20f,
                UiFactory.PanelColor, UiFactory.TextColor);

            SettingsPanel settings = BuildSettingsPanel(safe, panel);

            var hud = canvas.gameObject.AddComponent<HudController>();
            PrefabBuilder.Wire(hud,
                ("m_PlayerStats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_WaveState", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_AmmoState", DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState)),
                ("m_HealthFill", healthFill), ("m_HealthValue", healthValue), ("m_LevelFill", levelFill), ("m_LevelLabel", levelLabel),
                ("m_WaveLabel", waveLabel), ("m_WaveTimer", waveTimer), ("m_AmmoValue", ammoValue), ("m_ReloadHint", reload.gameObject),
                ("m_SettingsButton", settingsButton), ("m_SettingsPanel", settings));
            SaveUiPrefab(canvas.gameObject, "HUD");
            UiFactory.EventSystem();
            EditorSceneManager.SaveScene(scene, c_HudScene);
        }

        private static SettingsPanel BuildSettingsPanel(RectTransform parent, Sprite panel)
        {
            RectTransform root = UiFactory.Stretch("SettingsPanel", parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.55f), false, true);   // dims and blocks clicks behind
            var center = new Vector2(0.5f, 0.5f);
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, new Vector2(840f, 580f));
            UiFactory.Image(window, panel, new Color(0.1f, 0.11f, 0.13f, 0.97f), true, true);
            var top = new Vector2(0.5f, 1f);
            UiFactory.Label("Title", window, top, top, new Vector2(0f, -36f), new Vector2(800f, 70f), "НАСТРОЙКИ", 58f,
                TextAlignmentOptions.Center, UiFactory.TextColor);
            UiFactory.Label("Subtitle", window, top, top, new Vector2(0f, -120f), new Vector2(800f, 50f), "Качество графики", 36f,
                TextAlignmentOptions.Center, new Color(0.75f, 0.72f, 0.68f), false);
            var normal = new Color(0.16f, 0.17f, 0.2f, 0.95f);
            var size = new Vector2(250f, UiFactory.c_MinTouchTarget);
            Button low = UiFactory.Button("LowButton", window, top, top, new Vector2(-260f, -200f), size, "НИЗКОЕ", 34f, normal, UiFactory.TextColor);
            Button medium = UiFactory.Button("MediumButton", window, top, top, new Vector2(0f, -200f), size, "СРЕДНЕЕ", 34f, normal, UiFactory.TextColor);
            Button high = UiFactory.Button("HighButton", window, top, top, new Vector2(260f, -200f), size, "ВЫСОКОЕ", 34f, normal, UiFactory.TextColor);
            var bottom = new Vector2(0.5f, 0f);
            Button close = UiFactory.Button("CloseButton", window, bottom, bottom, new Vector2(0f, 36f), new Vector2(340f, UiFactory.c_MinTouchTarget), "ЗАКРЫТЬ", 40f,
                UiFactory.AccentColor, new Color(0.12f, 0.08f, 0.06f));
            var settings = root.gameObject.AddComponent<SettingsPanel>();
            PrefabBuilder.Wire(settings, ("m_LowButton", low), ("m_MediumButton", medium), ("m_HighButton", high), ("m_CloseButton", close));
            root.gameObject.SetActive(false);
            return settings;
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
        private static void BuildMap01()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapConfig map = DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_MapConfig);
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
            PrefabBuilder.Wire(systemsGo.AddComponent<MapBootstrap>(), ("m_Map", map));
            PrefabBuilder.Wire(systemsGo.AddComponent<WaveTimerPlaceholder>(),
                ("m_WaveState", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_PlayerStats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)));
            PrefabBuilder.Wire(systemsGo.AddComponent<CrosshairCursor>(),
                ("m_Crosshair", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/UI/UI_Crosshair.png")));

            BuildLights(lighting);
            BuildGround(environment, background);
            BuildFences(fences, map);
            BuildBuildingsAndSigns(environment);
            BuildProps(props);
            BuildNature(nature, map);
            BuildDecals(decals);

            GameObject car = Place("Car_Player", gameplay, 0f, 0f, c_CarPlayerYaw);
            car.name = "Car_Player";
            GameObject player = Place("Player", gameplay, PlayerSpawn.x, PlayerSpawn.z, 0f);
            player.name = "Player";
            BuildCamera(cameraRoot, player.transform, map);

            EditorSceneManager.SaveScene(scene, c_MapScene);
            ReportModelUsage(scene);
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
            Debug.Log($"[SceneBuilder] Map01 stats: {renderers} renderers, {triangles} triangles in the whole map, " +
                      $"{materials.Count} materials, 3 realtime point lights + sun.");
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
            RenderSettings.ambientSkyColor = s_AmbientSky;
            RenderSettings.ambientEquatorColor = s_AmbientEquator;
            RenderSettings.ambientGroundColor = s_AmbientGround;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(s_AmbientEquator);
            sh.AddDirectionalLight(Vector3.up, s_AmbientSky - s_AmbientEquator, 0.5f);
            sh.AddDirectionalLight(Vector3.down, s_AmbientGround - s_AmbientEquator, 0.5f);
            RenderSettings.ambientProbe = sh;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = s_FogColor;
            RenderSettings.fogStartDistance = c_FogStart;
            RenderSettings.fogEndDistance = c_FogEnd;
        }

        private static void BuildLights(Transform parent)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(s_SunEuler);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = s_SunColor;
            sun.intensity = c_SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            RenderSettings.sun = sun;

            // Max three dynamic point lights; every other lamp is emission only.
            PointLight("Light_WorkshopGarage", parent, s_WorkshopPosition + new Vector3(0.5f, 2.6f, -2.3f), 4f, 8f);
            PointLight("Light_GasSign", parent, new Vector3(16.5f, 3.6f, 15.6f), 3f, 6.5f);
            PointLight("Light_StreetLamp", parent, new Vector3(-0.1f, 3.45f, 13.4f), 3f, 7f);
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
            cam.backgroundColor = s_FogColor;
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<CinemachineBrain>();
            camGo.AddComponent<AudioListener>();

            var volumeGo = new GameObject("Global Volume");
            volumeGo.transform.SetParent(parent, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildVolumeProfile();
            PrefabBuilder.Wire(camGo.AddComponent<QualityPostProcessApplier>(), ("m_Volume", volume), ("m_CameraData", camData));

            var vcamGo = new GameObject("CM_FollowPlayer");
            vcamGo.transform.SetParent(parent, false);
            vcamGo.transform.SetPositionAndRotation(target.position + offset, Quaternion.Euler(pitch, 0f, 0f));
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = ViewConstants.c_CameraFov;
            vcam.Lens.NearClipPlane = ViewConstants.c_NearClip;
            vcam.Lens.FarClipPlane = ViewConstants.c_FarClip;
            vcam.Target.TrackingTarget = target;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);
            vcamGo.AddComponent<CameraAspectAdapter>();

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
        private static void BuildGround(Transform environment, Transform background)
        {
            Place("Env_Ground", environment, 0f, 0f);
            // North road (z 17.5..24.5) with sidewalks, east road (x 25..32) with sidewalks. The east road and its
            // sidewalks stop at the north sidewalk (z 15.8) so no two strips overlap.
            Place("Env_Road_100", background, 0f, 21f, 0f, 1f, c_RoadHeight);
            Place("Env_Sidewalk_100", background, 0f, 16.65f, 0f, 1f, c_SidewalkHeight);
            Place("Env_Sidewalk_100", background, 0f, 25.35f, 0f, 1f, c_SidewalkHeight);
            const float eastStripZ = 15.8f - 62.5f * 0.5f;
            Place("Env_Road_62", background, 28.5f, eastStripZ, 90f, 1f, c_RoadHeight);
            Place("Env_Sidewalk_62", background, 24.15f, eastStripZ, 90f, 1f, c_SidewalkHeight);
            Place("Env_Sidewalk_62", background, 32.85f, eastStripZ, 90f, 1f, c_SidewalkHeight);

            string[] houses = { "House_Stub_1", "House_Stub_2", "House_Stub_3", "House_Stub_4" };
            float[] northX = { -44f, -31.5f, -19f, -7f, 5f, 17f, 29f, 43f };
            for (int i = 0; i < northX.Length; i++)
                Place(houses[i % houses.Length], background, northX[i], 30f, 180f);
            float[] eastZ = { -33f, -21f, -9f, 3f };
            for (int i = 0; i < eastZ.Length; i++)
                Place(houses[(i + 2) % houses.Length], background, 38.2f, eastZ[i], 90f);

            // Street furniture outside the lot.
            foreach (float x in new[] { -38f, -16f, 11f, 40f })
                Place("Lamp_Street", background, x, 16.7f, 90f);
            foreach (float z in new[] { -30f, -12f, 6f })
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
            var rnd = new Random(seed);
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

            // Concept objects without a model: stand-in cubes (see the build report).
            Place("Stub_Tree", parent, 2.5f, 17f);
            Place("Stub_Tree", parent, 28f, 17f, 30f);
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
            Place("Jerrycan_Red", p, -3f, 5.6f, 20f);
            Place("Jerrycan_Green", p, -2.4f, 6.1f, -10f);

            // North fence line (the gate is at x 3.7..7.7).
            Place("Barrel_RedWhite", p, 0.5f, 13.4f);
            Place("Barrel_Grey", p, 1.5f, 13.7f, 40f);
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
            Place("Rock1", parent, 18f, -31f, 200f, 1.4f);
            Place("Rock2", parent, -36f, -14f, 45f, 1.5f);
            Place("Rock3", parent, -32f, 19f, 90f, 1.3f);

            // Grass tufts inside the lot, keeping the centre, the workshop and the parked cars clear.
            var lotRnd = new Random(7);
            float lotX = map.PlayAreaSize.x * 0.5f - 1.4f, lotZ = map.PlayAreaSize.y * 0.5f - 1.4f;
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < 42; attempt++)
            {
                float x = Mathf.Lerp(-lotX, lotX, (float)lotRnd.NextDouble());
                float z = Mathf.Lerp(-lotZ, lotZ, (float)lotRnd.NextDouble());
                if (!IsFreeForGrass(x, z, map))
                    continue;
                PlaceGrass(parent, lotRnd, x, z, c_LotGrassScale);
                placed++;
            }
            // Outside the fences (verges).
            var outRnd = new Random(8);
            for (int i = 0; i < 45; i++)
            {
                bool south = i % 2 == 0;
                float x = south ? Mathf.Lerp(-48f, 48f, (float)outRnd.NextDouble()) : Mathf.Lerp(-40f, -23f, (float)outRnd.NextDouble());
                float z = south ? Mathf.Lerp(-33f, -16f, (float)outRnd.NextDouble()) : Mathf.Lerp(-33f, 15f, (float)outRnd.NextDouble());
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
            (string name, float x, float z, float yaw, float scale)[] decals =
            {
                ("Decal_Crack1", -9.8f, -4.2f, 20f, 1.1f), ("Decal_Crack2", 5.6f, -8.4f, 110f, 1.2f), ("Decal_Crack1", 11.9f, 2.8f, 60f, 1f),
                ("Decal_Crack2", -1.5f, 2.2f, 150f, 0.95f), ("Decal_Crack1", -14f, -9.1f, 30f, 1f), ("Decal_Crack2", 2.5f, 3.2f, 75f, 0.9f),
                ("Decal_Crack1", 7.7f, 4.9f, 200f, 1.1f), ("Decal_Crack2", 1.5f, 9.5f, 250f, 1f), ("Decal_Crack1", 16.8f, -8.4f, 320f, 1.05f),
                ("Decal_Pothole1", -5.6f, -2.8f, 0f, 1f), ("Decal_Pothole2", 7f, -4.5f, 60f, 1.1f), ("Decal_Pothole1", -12f, 2f, 130f, 0.9f),
                ("Decal_Pothole2", 11.2f, 7.7f, 200f, 1f), ("Decal_Pothole1", 2.8f, -10.5f, 280f, 1.2f), ("Decal_Pothole2", -15.4f, -8.4f, 20f, 0.9f),
                ("Decal_Pothole1", 15.4f, -1.4f, 90f, 0.85f), ("Decal_Pothole2", 4f, 11f, 310f, 0.8f),
                ("Decal_Manhole", 10.9f, -7.4f, 0f, 1f),
                ("Decal_Stone1", -16.8f, -2.8f, 0f, 1.2f), ("Decal_Stone2", 7.7f, 9.5f, 30f, 1.1f), ("Decal_Stone3", -7f, -11.2f, 70f, 1.15f),
                ("Decal_Stone2", 0f, 6.6f, 10f, 1f), ("Decal_Stone3", 13f, -4.2f, 140f, 1f),
            };
            // The lot is denser than before, so neighbouring decals overlap: each one gets its own height step.
            for (int i = 0; i < decals.Length; i++)
            {
                (string name, float x, float z, float yaw, float scale) = decals[i];
                Place(name, parent, x, z, yaw, scale, i * c_DecalStep);
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
