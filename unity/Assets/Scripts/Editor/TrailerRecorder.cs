using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;
using Object = UnityEngine.Object;
using SettingsService = ZombiCar.Core.SettingsService;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Records the raw footage of the store trailer: a bot plays two takes and every frame is saved at a fixed 30 fps
    /// (Time.captureFramerate, so slow batch frames do not speed the video up). Take "Day" (Map01): the car fully
    /// upgraded, the real wave 15 rushing the car, the bot circling and shooting. Take "Night" (Map02): wave 10, the bot
    /// fights the brood mother until she dies. The player and the car are kept alive and the boss takes extra damage
    /// so the fight fits the clip; nothing of it shows on screen. Output: Trailer/Day/*.jpg, Trailer/Night/*.jpg.
    /// Batch (without -quit, it exits by itself):
    /// Unity.exe -batchmode -projectPath &lt;unity&gt; -executeMethod ZombiCar.EditorTools.TrailerRecorder.Run
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerRecorder
    {
        private const string c_StateKey = "ZombiCar.Trailer.State";     // 0 idle, 1 day, 2 switch, 3 night, 4 finished
        private const string c_Folder = "Trailer";
        private const int c_Width = 1920;
        private const int c_Height = 1080;
        private const int c_Fps = 30;
        private const int c_Quality = 95;

        private const int c_DayWave = 15;
        private const float c_DayWarmUp = 14f;          // game seconds before recording: the horde gets close
        private const float c_DayLength = 14f;

        private const int c_BossWave = 10;
        private const float c_NightWarmUp = 1.5f;
        private const float c_BossFight = 8f;         // the boss dies after about this many recorded seconds
        private const float c_AfterBoss = 4f;
        private const float c_NightMaxLength = 22f;

        private sealed class Bot : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public Vector3 AimDirection { get; set; } = Vector3.right;
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
                ray = default;
                return false;
            }
        }

        private static bool s_Started;
        private static float s_StartTime;
        private static float s_RecordFrom;
        private static int s_LastFrame;
        private static int s_Frames;
        private static int s_Skipped;
        private static int s_Errors;
        private static float s_BossDiedAt;
        private static Bot s_Bot;
        private static Transform s_Player;
        private static CharacterController s_Controller;
        private static PlayerWeapon s_Weapon;
        private static PlayerHealth s_PlayerHealth;
        private static CarHealth s_Car;
        private static WaveDirector s_Director;
        private static ZombieSpawner s_Spawner;
        private static ZombieController s_Boss;
        private static WaveConfig s_Waves;
        private static LevelUpScreen s_LevelUp;
        private static RunState s_Run;
        private static QualityTier s_PreviousQuality;
        private static bool s_QualitySaved;
        private static int s_HiddenUntil;              // frames after a card pick are left out (the screen fades)
        private static RenderTexture s_Target;
        private static Texture2D s_Readback;

        static TrailerRecorder()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Record Trailer Footage")]
        public static void Run()
        {
            string root = Path.GetFullPath(c_Folder);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            SessionState.SetInt(c_StateKey, 1);
            Enter(SceneBuilder.c_MapScene);
        }

        private static bool Night => SessionState.GetInt(c_StateKey, 0) == 3;
        private static string Take => Night ? "Night" : "Day";

        private static void Enter(string scene)
        {
            s_Started = false;                                       // statics survive between plays (no domain reload)
            MetaProgress.TutorialDone = MetaProgress.LiveHintsDone = true;
            BossIntroView.Suppressed = true;
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            int state = SessionState.GetInt(c_StateKey, 0);
            if (state == 2 || state == 4)
            {
                if (EditorApplication.isPlaying)
                    return;
                if (state == 2)
                {
                    SessionState.SetInt(c_StateKey, 3);
                    Enter(SceneBuilder.c_NightMapScene);
                    return;
                }
                EditorApplication.update -= Tick;
                SessionState.SetInt(c_StateKey, 0);
                BossIntroView.Suppressed = false;
                if (s_QualitySaved)
                    SettingsService.SetQuality(s_PreviousQuality);           // the editor's own setting back
                s_QualitySaved = false;
                Debug.Log($"[Trailer] Finished, errors {s_Errors}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
                return;
            }
            if ((state != 1 && state != 3) || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
                Begin();

            SkipLevelUps();
            if (GamePause.IsPaused && Time.frameCount % 300 == 0)
                Debug.Log($"[Trailer] Paused: {GamePause.Reasons}");
            KeepAlive();
            DriveBot();
            float t = Time.time - s_StartTime;
            if (t >= s_RecordFrom && !GamePause.IsPaused && Time.frameCount > s_HiddenUntil)
                Capture();
            if (Night ? NightDone(t) : t >= s_RecordFrom + c_DayLength)
                FinishTake();
        }

        private static void Begin()
        {
            s_Started = true;
            s_Frames = s_Skipped = 0;
            s_LastFrame = -1;
            s_Boss = null;
            s_BossDiedAt = -1f;
            Application.logMessageReceived -= CountErrors;
            Application.logMessageReceived += CountErrors;
            Time.captureFramerate = c_Fps;
            s_StartTime = Time.time;

            s_Weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Player = s_Weapon.transform;
            s_Controller = s_Player.GetComponent<CharacterController>();
            s_PlayerHealth = s_Player.GetComponent<PlayerHealth>();
            s_Car = Object.FindAnyObjectByType<CarHealth>();
            s_Director = Object.FindAnyObjectByType<WaveDirector>();
            s_Spawner = s_Director.Spawner;
            s_Waves = (WaveConfig)typeof(WaveDirector).GetField("m_Config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s_Director);
            s_LevelUp = Object.FindAnyObjectByType<LevelUpScreen>(FindObjectsInactive.Include);
            s_HiddenUntil = -1;
            s_Run = AssetDatabase.LoadAssetAtPath<RunState>(DataAssetsBuilder.c_RunState);
            if (!s_QualitySaved)
            {
                s_PreviousQuality = SettingsService.Quality;
                s_QualitySaved = true;
            }
            SettingsService.SetQuality(QualityTier.Ultra);                   // as the settings menu: Ultra materials too
            PlayerProgression progression = Object.FindAnyObjectByType<PlayerProgression>();
            if (progression != null)
                progression.enabled = false;                                 // no experience: no level-up cards
            SkipLevelUps();

            s_Bot = new Bot();
            var provider = s_Player.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in s_Player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }

            // The fully built car: spikes, lights, armour — what a player has by the boss waves.
            WorkshopScreen workshop = Object.FindAnyObjectByType<WorkshopScreen>(FindObjectsInactive.Include);
            if (workshop != null)
            {
                foreach (CarUpgrade upgrade in workshop.Catalog.Upgrades.Where(u => !u.Repeatable))
                    for (int i = 0; i < 100 && workshop.Run.GrantUpgrade(upgrade); i++) { }
            }
            s_Car.Repair(100000);

            if (Night)
            {
                s_Director.SkipTo(c_BossWave, 0.2f);
                s_RecordFrom = c_NightWarmUp;
            }
            else
            {
                s_Director.SkipTo(c_DayWave, 0.2f);
                s_RecordFrom = c_DayWarmUp;
            }

            string folder = Path.Combine(Path.GetFullPath(c_Folder), Take);
            Directory.CreateDirectory(folder);
            CanvasesOnCamera();
            Debug.Log($"[Trailer] Take {Take} started.");
        }

        // Level-up cards would pause the take (in batch they come faster than their animation can close them): the
        // screen stays off, the level-ups are used up unseen and their pause is lifted.
        private static void SkipLevelUps()
        {
            MethodInfo close = typeof(LevelUpScreen).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo rootField = typeof(LevelUpScreen).GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (LevelUpScreen screen in Object.FindObjectsByType<LevelUpScreen>(FindObjectsInactive.Include))
            {
                if (screen.IsOpen)
                    close?.Invoke(screen, null);
                screen.enabled = false;
                if (rootField?.GetValue(screen) is CanvasGroup root && root.gameObject.activeSelf)
                {
                    root.alpha = 0f;
                    root.gameObject.SetActive(false);
                    s_HiddenUntil = Time.frameCount + 2;
                }
            }
            if (s_Run != null)
                while (s_Run.PendingLevelUps > 0)
                    s_Run.ConsumeLevelUp();
            if (GamePause.Has(GamePause.Reason.LevelUp))
                GamePause.Set(GamePause.Reason.LevelUp, false);
        }

        private static void CountErrors(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                s_Errors++;
        }

        private static void KeepAlive()
        {
            s_PlayerHealth.Heal(1000);
            if (s_Car.Health < s_Car.MaxHealth / 2)
                s_Car.Repair(s_Car.MaxHealth);
            if (s_Weapon.Reserve < s_Weapon.MagazineSize)
                s_Weapon.Resupply(s_Weapon.MaxReserve);

            float t = Time.time - s_StartTime;
            if (Night)
            {
                if (s_Boss == null && s_Director.Boss != null)
                    s_Boss = s_Director.Boss;
                if (s_Boss != null && s_Boss.IsAlive && t > s_RecordFrom)
                {
                    // Extra damage so the fight lasts about c_BossFight seconds of the clip.
                    float perSecond = s_Boss.MaxHealth / c_BossFight;
                    int due = Mathf.FloorToInt(perSecond * Time.deltaTime * 0.8f);
                    if (due > 0)
                        s_Boss.TakeDamage(due, Vector3.right);
                }
                if (s_Boss != null && !s_Boss.IsAlive && s_BossDiedAt < 0f)
                {
                    s_BossDiedAt = t;
                    Debug.Log($"[Trailer] Boss down at {t - s_RecordFrom:F1} s of the take.");
                }
            }
        }

        private static void DriveBot()
        {
            Vector3 center = s_Car.transform.position;
            ZombieController target = null;
            float range = s_Weapon.Range;

            if (Night && s_Boss != null && s_Boss.IsAlive)
            {
                target = s_Boss;
                // Keep firing distance from the boss and strafe around it.
                Vector3 away = s_Player.position - s_Boss.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                Vector3 dir = away.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                Vector3 move = side * 0.6f;
                if (distance < 5f) move += dir;
                else if (distance > Mathf.Min(7.5f, range * 0.8f)) move -= dir;
                Vector3 fromCar = s_Player.position - center;
                fromCar.y = 0f;
                if (fromCar.magnitude < 6f) move += fromCar.normalized;          // off the car: no ammo hint
                s_Bot.Move = new Vector2(move.x, move.z).normalized * 0.8f;
            }
            else
            {
                // Circle around the car, out of its ammo-hint range.
                Vector3 offset = s_Player.position - center;
                offset.y = 0f;
                float radius = offset.magnitude;
                Vector3 tangent = Vector3.Cross(Vector3.up, offset.normalized);
                Vector3 move = tangent * 0.7f + offset.normalized * Mathf.Clamp(7f - radius, -1f, 1f) * 0.6f;
                s_Bot.Move = new Vector2(move.x, move.z).normalized * 0.7f;

                float best = range * range;
                foreach (ZombieController zombie in Object.FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude))
                {
                    if (!zombie.IsAlive || !zombie.GetComponent<Collider>().enabled)
                        continue;
                    Vector3 d = zombie.transform.position - s_Player.position;
                    d.y = 0f;
                    if (d.sqrMagnitude < best)
                    {
                        best = d.sqrMagnitude;
                        target = zombie;
                    }
                }
            }

            s_Bot.AimActive = target != null;
            s_Bot.FireHeld = target != null;
            if (target != null)
            {
                Vector3 aim = target.transform.position - s_Player.position;
                aim.y = 0f;
                s_Bot.AimDirection = aim.normalized;
            }
        }

        private static bool NightDone(float t)
        {
            if (t >= s_RecordFrom + c_NightMaxLength)
                return true;
            return s_BossDiedAt >= 0f && t >= s_BossDiedAt + c_AfterBoss;
        }

        // Overlay canvases are not part of camera renders: draw them through the camera.
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
                canvas.sortingOrder += 1000;
            }
        }

        private static void Capture()
        {
            if (Time.frameCount == s_LastFrame)
                return;
            if (s_LastFrame >= 0 && Time.frameCount > s_LastFrame + 1)
                s_Skipped += Time.frameCount - s_LastFrame - 1;
            s_LastFrame = Time.frameCount;
            CanvasesOnCamera();                                      // canvases that appeared since (banners, boss bar)

            Camera cam = Camera.main;
            if (s_Target == null)
            {
                s_Target = new RenderTexture(c_Width, c_Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                s_Target.Create();
                s_Readback = new Texture2D(c_Width, c_Height, TextureFormat.RGB24, false);
            }
            cam.targetTexture = s_Target;
            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Exclude))
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }
            Canvas.ForceUpdateCanvases();
            var request = new RenderPipeline.StandardRequest { destination = s_Target };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();
            cam.targetTexture = null;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = s_Target;
            s_Readback.ReadPixels(new Rect(0, 0, c_Width, c_Height), 0, 0);
            s_Readback.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(Path.GetFullPath(c_Folder), Take, $"f_{s_Frames:D5}.jpg");
            File.WriteAllBytes(path, s_Readback.EncodeToJPG(c_Quality));
            s_Frames++;
        }

        private static void FinishTake()
        {
            Debug.Log($"[Trailer] Take {Take}: {s_Frames} frames ({s_Frames / (float)c_Fps:F1} s), {s_Skipped} frames skipped, " +
                      $"errors so far {s_Errors}.");
            Time.captureFramerate = 0;
            if (s_Target != null)
            {
                s_Target.Release();
                Object.DestroyImmediate(s_Target);
                Object.DestroyImmediate(s_Readback);
                s_Target = null;
                s_Readback = null;
            }
            SessionState.SetInt(c_StateKey, Night ? 4 : 2);
            EditorApplication.ExitPlaymode();
        }
    }
}
