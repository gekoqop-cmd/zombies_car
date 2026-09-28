using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Scripted Play-mode checks of the release features, one scenario per run: a bot drives the player, the scenario
    /// calls the game's public APIs, every check is logged as PASS / FAIL and screenshots go to
    /// Screenshots/Feature_*.png. Steps run on real time, so they go on while the game is paused (cards, workshop).
    /// Batch (without -quit, it exits by itself with 0 when every check passed and nothing logged an error):
    /// Unity.exe -batchmode -projectPath &lt;unity&gt; -executeMethod ZombiCar.EditorTools.FeatureTests.Run
    /// -featureTest &lt;scenario&gt; [-featureMap night] [-forceMobile]
    /// </summary>
    [InitializeOnLoad]
    public static partial class FeatureTests
    {
        private const string c_StateKey = "ZombiCar.FeatureTests.State";      // 0 idle, 1 running, 2 finished
        private const string c_ScenarioKey = "ZombiCar.FeatureTests.Scenario";
        private const string c_ResultKey = "ZombiCar.FeatureTests.Result";
        private const string c_ScenarioArgument = "-featureTest";
        private const string c_MapArgument = "-featureMap";
        private const string c_Folder = "Screenshots";
        private const int c_Width = 1920;
        private const int c_Height = 1080;

        /// <summary>Scripted input: the scenario writes here, the player components read it.</summary>
        private sealed class Bot : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public Vector3 AimDirection { get; set; } = Vector3.right;
            public bool AimActive { get; set; }
            public bool FireHeld { get; set; }
            public bool FirePressed => false;
            public bool FireReleased => false;
            public bool QuickTap => false;
            public int ReloadUntilFrame { get; set; } = -1;
            public bool ReloadPressed => Time.frameCount <= ReloadUntilFrame;
            public int InteractUntilFrame { get; set; } = -1;
            public bool InteractPressed => Time.frameCount <= InteractUntilFrame;

            public bool TryGetAimRay(out Ray ray)
            {
                ray = default;
                return false;
            }
        }

        private struct Step
        {
            public float Time;
            public int Order;
            public Action Action;
        }

        private static readonly Dictionary<string, Action> s_Scenarios = new Dictionary<string, Action>();
        private static readonly List<Step> s_Steps = new List<Step>();
        private static bool s_Started;
        private static float s_StartTime;
        private static int s_NextStep;
        private static int s_Checks;
        private static int s_Failures;
        private static int s_Errors;
        private static string s_Scenario;

        private static Bot s_Bot;
        private static Transform s_Player;
        private static CharacterController s_Controller;
        private static PlayerWeapon s_Weapon;
        private static PlayerHealth s_PlayerHealth;
        private static PlayerInteractor s_Interactor;
        private static CarHealth s_Car;
        private static ZombieSpawner s_Spawner;
        private static WaveDirector s_Director;
        private static PlayerProgression s_Progression;
        private static CameraFollowTarget s_FollowTarget;
        private static CinemachineCamera s_Vcam;
        private static RunState s_Run;
        private static PlayerStats s_Stats;
        private static WaveState s_Waves;
        private static InteractionState s_Interaction;

        static FeatureTests()
        {
            RegisterScenarios();
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        private static void Register(string name, Action build) => s_Scenarios[name] = build;

        /// <summary>Batch entry: the scenario comes from -featureTest.</summary>
        public static void Run() => Run(Argument(c_ScenarioArgument) ?? "progression");

        public static void Run(string scenario)
        {
            string scene = Argument(c_MapArgument) == "night" ? SceneBuilder.c_NightMapScene : SceneBuilder.c_MapScene;
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            SessionState.SetString(c_ScenarioKey, scenario);
            SessionState.SetInt(c_StateKey, 1);
            SessionState.SetInt(c_ResultKey, 1);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }

        private static void Tick()
        {
            int state = SessionState.GetInt(c_StateKey, 0);
            if (state == 2)
            {
                if (EditorApplication.isPlaying)
                    return;
                EditorApplication.update -= Tick;
                SessionState.SetInt(c_StateKey, 0);
                int result = SessionState.GetInt(c_ResultKey, 1);
                Debug.Log($"[FeatureTests] Finished, exit code {result}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(result);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
                Begin();
            float t = Time.realtimeSinceStartup - s_StartTime;
            while (s_NextStep < s_Steps.Count && t >= s_Steps[s_NextStep].Time)
            {
                try
                {
                    s_Steps[s_NextStep++].Action();
                }
                catch (Exception e)
                {
                    s_Failures++;
                    Debug.LogError($"[FeatureTests] Step failed: {e}");
                }
            }
        }

        private static void Begin()
        {
            s_Started = true;
            s_StartTime = Time.realtimeSinceStartup;
            s_Checks = s_Failures = s_Errors = 0;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    s_Errors++;
            };
            s_Scenario = SessionState.GetString(c_ScenarioKey, "progression");
            s_Weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Player = s_Weapon.transform;
            s_Controller = s_Player.GetComponent<CharacterController>();
            s_PlayerHealth = s_Player.GetComponent<PlayerHealth>();
            s_Interactor = s_Player.GetComponent<PlayerInteractor>();
            s_Car = Object.FindAnyObjectByType<CarHealth>();
            s_Spawner = Object.FindAnyObjectByType<ZombieSpawner>();
            s_Director = Object.FindAnyObjectByType<WaveDirector>();
            s_Progression = Object.FindAnyObjectByType<PlayerProgression>();
            s_FollowTarget = Object.FindAnyObjectByType<CameraFollowTarget>();
            s_Vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            s_Run = AssetDatabase.LoadAssetAtPath<RunState>(DataAssetsBuilder.c_RunState);
            s_Stats = AssetDatabase.LoadAssetAtPath<PlayerStats>(DataAssetsBuilder.c_PlayerStats);
            s_Waves = AssetDatabase.LoadAssetAtPath<WaveState>(DataAssetsBuilder.c_WaveState);
            s_Interaction = AssetDatabase.LoadAssetAtPath<InteractionState>(DataAssetsBuilder.c_InteractionState);

            s_Bot = new Bot();
            var provider = s_Player.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in s_Player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }

            s_Steps.Clear();
            s_NextStep = 0;
            if (!s_Scenarios.TryGetValue(s_Scenario, out Action build))
            {
                Debug.LogError($"[FeatureTests] Unknown scenario '{s_Scenario}'. Known: {string.Join(", ", s_Scenarios.Keys)}");
                At(0.1f, Finish);
            }
            else
            {
                build();
            }
            s_Steps.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Order.CompareTo(b.Order));
            Debug.Log($"[FeatureTests] Scenario '{s_Scenario}': {s_Steps.Count} steps.");
        }

        // ================================================================== helpers for scenarios
        private static void At(float time, Action action) => s_Steps.Add(new Step { Time = time, Order = s_Steps.Count, Action = action });

        private static bool Check(bool condition, string what)
        {
            s_Checks++;
            if (!condition)
                s_Failures++;
            Debug.Log($"[FeatureTests] {(condition ? "PASS" : "FAIL")}: {what}");
            return condition;
        }

        private static void Press() => s_Bot.InteractUntilFrame = Time.frameCount + 2;

        private static void Teleport(Vector3 position)
        {
            s_Controller.enabled = false;
            s_Player.position = position;
            s_Controller.enabled = true;
            if (s_FollowTarget != null)
                s_FollowTarget.Snap();
            if (s_Vcam != null)
                s_Vcam.PreviousStateIsValid = false;
        }

        private static ZombieConfig Zombie(string name) =>
            AssetDatabase.LoadAssetAtPath<ZombieConfig>($"{EditorPaths.c_DataZombies}/Zombie_{name}.asset");

        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        private static IEnumerable<ZombieController> LiveZombies() =>
            Object.FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude).Where(z => z.IsAlive);

        // Overlay canvases are not part of camera renders: draw them through the camera for screenshots.
        private static void CanvasesOnCamera()
        {
            Camera cam = Camera.main;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.5f - canvas.sortingOrder * 0.001f;
                canvas.sortingOrder += 1000;          // over the world-space plates, as an overlay canvas would be
            }
        }

        private static void Shot(string name)
        {
            CanvasesOnCamera();
            string folder = Path.GetFullPath(c_Folder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"Feature_{name}.png");
            MapScreenshots.Render(Camera.main, c_Width, c_Height, path);
            Debug.Log($"[FeatureTests] {path}");
        }

        private static void Finish()
        {
            bool ok = s_Failures == 0 && s_Errors == 0;
            Debug.Log($"[FeatureTests] Scenario '{s_Scenario}': {s_Checks - s_Failures}/{s_Checks} checks passed, " +
                      $"{s_Errors} errors logged -> {(ok ? "PASS" : "FAIL")}");
            Time.timeScale = 1f;
            SessionState.SetInt(c_ResultKey, ok ? 0 : 1);
            SessionState.SetInt(c_StateKey, 2);
            EditorApplication.ExitPlaymode();
        }
    }
}
