using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Automated play test of Map01: enters Play mode, lets the waves run at double speed while a bot plays (walks a
    /// circle around the car, aims at the nearest zombie in range and holds fire), logs waves / zombies / kills /
    /// car and player health every few seconds, saves game-camera screenshots to Screenshots/Playtest_*.png and exits
    /// with code 0 when zombies spawned, were killed and no errors were logged.
    /// Batch: Unity.exe -batchmode -projectPath &lt;unity&gt; -executeMethod ZombiCar.EditorTools.Playtest.Run
    /// [-playtestWave N] (without -quit: the editor has to keep running through Play mode; the test exits by itself).
    /// -playtestWave starts at a later wave, so the zombie types that appear later are tested too.
    /// </summary>
    [InitializeOnLoad]
    public static class Playtest
    {
        private const string c_StateKey = "ZombiCar.Playtest.State";   // 0 idle, 1 running, 2 finished
        private const string c_ResultKey = "ZombiCar.Playtest.Result";
        private const string c_WaveArgument = "-playtestWave";
        private const float c_GameSeconds = 110f;
        private const float c_TimeScale = 2f;
        private const float c_LogInterval = 10f;
        private const float c_BotCircleSpeed = 0.5f;                   // radians per second of the walking circle
        private const float c_BotMove = 0.5f;
        private static readonly float[] s_ShotTimes = { 20f, 45f, 75f, 105f };
        private const int c_ShotWidth = 1920;
        private const int c_ShotHeight = 1080;

        private static bool s_Started;
        private static float s_NextLog;
        private static int s_NextShot;
        private static int s_Errors;
        private static Bot s_Bot;
        private static ZombieSpawner s_Spawner;
        private static WaveDirector s_Director;
        private static CarHealth s_Car;
        private static PlayerStats s_PlayerStats;
        private static AmmoState s_Ammo;
        private static Transform s_Player;
        private static float s_WeaponRange;

        /// <summary>Scripted input: the bot's decisions are written here and read by the player components.</summary>
        private sealed class Bot : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public Vector3 AimDirection { get; set; } = Vector3.forward;
            public bool AimActive { get; set; }
            public bool FireHeld { get; set; }
            public bool FirePressed => false;
            public bool FireReleased => false;
            public bool QuickTap => false;
            public bool ReloadPressed => false;
            public bool InteractPressed => false;
            public bool SwitchWeaponPressed => false;
            public int WeaponSlotPressed => -1;

            public bool TryGetAimRay(out Ray ray)
            {
                ray = default;   // the bot aims by direction
                return false;
            }
        }

        static Playtest()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Run Playtest")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SessionState.SetInt(c_StateKey, 1);
            SessionState.SetInt(c_ResultKey, 1);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            int state = SessionState.GetInt(c_StateKey, 0);
            if (state == 2)
            {
                if (EditorApplication.isPlaying)
                    return;                                             // still leaving Play mode
                EditorApplication.update -= Tick;
                SessionState.SetInt(c_StateKey, 0);
                int result = SessionState.GetInt(c_ResultKey, 1);
                Debug.Log($"[Playtest] Finished, exit code {result}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(result);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 3)
                return;
            if (!s_Started)
                Begin();
            DriveBot();
            Report(false);
            if (s_NextShot < s_ShotTimes.Length && Time.time >= s_ShotTimes[s_NextShot])
                Screenshot($"Playtest_w{StartWaveArgument()}_{s_NextShot + 1}_{Mathf.RoundToInt(Time.time)}s");
            if (Time.time >= c_GameSeconds)
                Finish();
        }

        private static void Begin()
        {
            s_Started = true;
            Time.timeScale = c_TimeScale;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    s_Errors++;
            };
            s_Spawner = Object.FindAnyObjectByType<ZombieSpawner>();
            s_Director = Object.FindAnyObjectByType<WaveDirector>();
            s_Car = Object.FindAnyObjectByType<CarHealth>();
            var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Player = weapon.transform;
            s_WeaponRange = weapon.Config.Range;
            s_PlayerStats = AssetDatabase.LoadAssetAtPath<PlayerStats>(DataAssetsBuilder.c_PlayerStats);
            s_Ammo = AssetDatabase.LoadAssetAtPath<AmmoState>(DataAssetsBuilder.c_AmmoState);
            int startWave = StartWaveArgument();
            if (startWave > 1)
                typeof(WaveDirector).GetField("m_Wave", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(s_Director, startWave);

            // Swap the device input for the bot everywhere it was cached (provider + every m_Input field).
            s_Bot = new Bot();
            var provider = s_Player.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in s_Player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }
            Debug.Log($"[Playtest] Started at wave {s_Director.Wave}: bot input installed, time scale x{c_TimeScale}");
        }

        private static int StartWaveArgument()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == c_WaveArgument && int.TryParse(args[i + 1], out int wave))
                    return wave;
            }
            return 1;
        }

        private static void DriveBot()
        {
            float angle = Time.time * c_BotCircleSpeed;
            s_Bot.Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * c_BotMove;

            ZombieController nearest = null;
            float best = s_WeaponRange * s_WeaponRange;
            foreach (ZombieController zombie in Object.FindObjectsByType<ZombieController>())
            {
                if (!zombie.IsAlive || !zombie.GetComponent<Collider>().enabled)
                    continue;
                Vector3 offset = zombie.transform.position - s_Player.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < best)
                {
                    best = offset.sqrMagnitude;
                    nearest = zombie;
                }
            }
            s_Bot.AimActive = nearest != null;
            s_Bot.FireHeld = nearest != null;
            if (nearest != null)
            {
                Vector3 aim = nearest.transform.position - s_Player.position;
                aim.y = 0f;
                s_Bot.AimDirection = aim.normalized;
            }
        }

        private static void Report(bool final)
        {
            if (!final && Time.time < s_NextLog)
                return;
            s_NextLog = Time.time + c_LogInterval;
            int onScreen = 0;
            foreach (ZombieController zombie in Object.FindObjectsByType<ZombieController>())
                onScreen += zombie.IsAlive ? 1 : 0;
            Debug.Log($"[Playtest] t={Time.time:F0}s wave {s_Director.Wave} (running: {s_Director.IsWaveRunning}), " +
                      $"alive {s_Spawner.Alive} (objects {onScreen}), killed {s_Spawner.Killed}, " +
                      $"car {s_Car.Health}/{s_Car.MaxHealth}, player {s_PlayerStats.Health}/{s_PlayerStats.MaxHealth}, " +
                      $"ammo {s_Ammo.InMagazine}/{s_Ammo.Reserve}, errors {s_Errors}");
        }

        private static void Screenshot(string name)
        {
            s_NextShot++;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            string folder = Path.GetFullPath("Screenshots");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".png");
            MapScreenshots.Render(cam, c_ShotWidth, c_ShotHeight, path);
            Debug.Log($"[Playtest] {path}");
        }

        private static void Finish()
        {
            Report(true);
            bool ok = s_Spawner.Killed > 0 && s_Spawner.Killed + s_Spawner.Alive > 0 && s_Errors == 0;
            Debug.Log(ok ? "[Playtest] PASS" : "[Playtest] FAIL (no kills or errors were logged)");
            Time.timeScale = 1f;
            SessionState.SetInt(c_ResultKey, ok ? 0 : 1);
            SessionState.SetInt(c_StateKey, 2);
            EditorApplication.ExitPlaymode();
        }
    }
}
