using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Player;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Play-mode check of the HUD, the car durability plate, the muzzle flash and the car's damage stages:
    /// HUD at the start, the plate close up, the flash frame by frame (slow motion) standing and running, and the car
    /// taken down to 351 / 350 / 251 / 250 HP and repaired back (logs HP, stage and the bar colour each time).
    /// Output: Screenshots/Review/*.png. Tools/Diagnostics/Review Screenshots (batch: without -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class ReviewShots
    {
        private const string c_StateKey = "ZombiCar.ReviewShots.State";
        private const string c_Folder = "Screenshots/Review";
        private const int c_Width = 1920;
        private const int c_Height = 1080;
        private const float c_SlowMotion = 0.04f;

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

        private struct Step
        {
            public float Time;
            public int Order;
            public Action Action;
        }

        private static readonly List<Step> s_Steps = new List<Step>();
        private static bool s_Started;
        private static float s_StartTime;
        private static int s_Next;
        private static int s_Errors;
        private static Bot s_Bot;
        private static Transform s_Player;
        private static CharacterController s_Controller;
        private static PlayerWeapon s_Weapon;
        private static CarHealth s_Car;
        private static CameraFollowTarget s_Target;
        private static Unity.Cinemachine.CinemachineCamera s_Vcam;
        private static int s_FlashShots;
        private static string s_FlashPrefix;
        private static float s_FlashCaptureUntil;
        private static int s_FlashFrame;

        static ReviewShots()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Review Screenshots")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SessionState.SetInt(c_StateKey, 1);
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
                    return;
                EditorApplication.update -= Tick;
                SessionState.SetInt(c_StateKey, 0);
                Debug.Log($"[ReviewShots] Finished, errors {s_Errors}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
                Begin();
            float t = Time.time - s_StartTime;
            while (s_Next < s_Steps.Count && t >= s_Steps[s_Next].Time)
                s_Steps[s_Next++].Action();
            CaptureFlashFrames();
        }

        private static void Begin()
        {
            s_Started = true;
            s_StartTime = Time.time;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    s_Errors++;
            };
            Object.FindAnyObjectByType<WaveDirector>().enabled = false;
            s_Weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Player = s_Weapon.transform;
            s_Controller = s_Player.GetComponent<CharacterController>();
            s_Car = Object.FindAnyObjectByType<CarHealth>();
            s_Target = Object.FindAnyObjectByType<CameraFollowTarget>();
            s_Vcam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            s_Bot = new Bot();
            var provider = s_Player.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in s_Player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }
            s_Weapon.Fired += OnFired;
            BuildSteps();
        }

        private static void BuildSteps()
        {
            s_Steps.Clear();
            float t = 0.4f;
            void At(float time, Action action) => s_Steps.Add(new Step { Time = time, Order = s_Steps.Count, Action = action });
            Vector3 car = s_Car.transform.position;

            At(t, () => Wide("HUD_Full"));
            At(t, () => CloseUp("Plate_400", car + Vector3.up * 3.3f, 760, 380));

            // Muzzle flash, standing then running (slow motion, every frame of the first shot).
            At(t += 0.2f, () => Teleport(new Vector3(-10f, 0f, -6f)));
            At(t += 0.5f, () => { Time.timeScale = c_SlowMotion; s_FlashPrefix = "Flash_Stand"; s_FlashShots = 0; Fire(true); });
            At(t += 0.03f, () => Fire(false));
            At(t += 0.02f, () => { Time.timeScale = 1f; s_Bot.Move = Vector2.right; });
            At(t += 0.6f, () => { Time.timeScale = c_SlowMotion; s_FlashPrefix = "Flash_Run"; s_FlashShots = 0; Fire(true); });
            At(t += 0.03f, () => Fire(false));
            At(t += 0.02f, () => { Time.timeScale = 1f; s_Bot.Move = Vector2.zero; });
            // Facing left: the flash must mirror with the sprite.
            At(t += 0.6f, () => { Time.timeScale = c_SlowMotion; s_FlashPrefix = "Flash_Left"; s_FlashShots = 0; Fire(true, Vector3.left); });
            At(t += 0.03f, () => Fire(false, Vector3.left));
            At(t += 0.02f, () => Time.timeScale = 1f);

            // Car damage stages, down and back up.
            At(t += 0.5f, () => Teleport(car + new Vector3(-5f, 0f, -4f)));
            int[] targets = { 351, 350, 251, 250, 251, 351, 400 };
            foreach (int hp in targets)
            {
                int target = hp;
                At(t += 0.6f, () => SetCarHealth(target));
                At(t += 0.9f, () =>
                {
                    CloseUp($"Car_{target}_{s_Car.Stage}", car + Vector3.up * 1.6f, 900, 600);
                    Image fill = FindFill();
                    Transform smoke = s_Car.transform.Find("EngineSmoke");
                    if (smoke != null && smoke.TryGetComponent(out ParticleSystem ps))
                        Debug.Log($"[ReviewShots] smoke active {smoke.gameObject.activeInHierarchy} playing {ps.isPlaying} particles {ps.particleCount} at {smoke.position}");
                    Debug.Log($"[ReviewShots] car {s_Car.Health}/{s_Car.MaxHealth} stage {s_Car.Stage} bar colour {(fill != null ? fill.color.ToString() : "?")}");
                });
            }
            At(t += 0.3f, Finish);
            s_Steps.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Order.CompareTo(b.Order));
        }

        private static void Fire(bool on) => Fire(on, Vector3.right);

        private static void Fire(bool on, Vector3 direction)
        {
            s_Bot.AimActive = on;
            s_Bot.AimDirection = direction;
            s_Bot.FireHeld = on;
        }

        private static void OnFired()
        {
            if (s_FlashPrefix == null || s_FlashShots++ > 0)
                return;
            s_FlashFrame = 0;
            s_FlashCaptureUntil = Time.time + 0.14f;      // flash 0.075 s + glow + start of the smoke
        }

        private static void CaptureFlashFrames()
        {
            if (s_FlashPrefix == null || Time.time > s_FlashCaptureUntil || s_FlashShots == 0)
                return;
            CloseUp($"{s_FlashPrefix}_{s_FlashFrame++:00}", s_Player.position + Vector3.up * 1.1f, 360, 240);
            if (Time.time + 0.001f > s_FlashCaptureUntil)
                s_FlashPrefix = null;
        }

        private static void SetCarHealth(int target)
        {
            int delta = target - s_Car.Health;
            if (delta < 0)
                s_Car.TakeDamage(-delta, Vector3.right);
            else if (delta > 0)
                s_Car.Repair(delta);
        }

        private static Image FindFill()
        {
            var bar = Object.FindAnyObjectByType<CarHealthBar>();
            if (bar == null)
                return null;
            foreach (Image image in bar.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Fill")
                    return image;
            }
            return null;
        }

        private static void Teleport(Vector3 position)
        {
            s_Controller.enabled = false;
            s_Player.position = position;
            s_Controller.enabled = true;
            s_Target.Snap();
            s_Vcam.PreviousStateIsValid = false;
        }

        private static void HudOnCamera()
        {
            var hud = Object.FindAnyObjectByType<ZombiCar.UI.HudController>();
            if (hud == null || !hud.TryGetComponent(out Canvas canvas) || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                return;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = Camera.main.nearClipPlane + 0.5f;
        }

        private static string OutputPath(string name)
        {
            string folder = Path.GetFullPath(c_Folder);
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, name + ".png");
        }

        private static void Wide(string name)
        {
            HudOnCamera();
            MapScreenshots.Render(Camera.main, c_Width, c_Height, OutputPath(name));
        }

        private static void CloseUp(string name, Vector3 world, int width, int height)
        {
            HudOnCamera();
            string path = OutputPath(name);
            Camera cam = Camera.main;
            MapScreenshots.Render(cam, c_Width, c_Height, path);
            cam.aspect = (float)c_Width / c_Height;
            Vector3 viewport = cam.WorldToViewportPoint(world);
            cam.ResetAspect();
            var full = new Texture2D(2, 2);
            full.LoadImage(File.ReadAllBytes(path));
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * c_Width) - width / 2, 0, c_Width - width);
            int y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * c_Height) - height / 2, 0, c_Height - height);
            var crop = new Texture2D(width, height, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x, y, width, height));
            crop.Apply();
            File.WriteAllBytes(path, crop.EncodeToPNG());
            Object.DestroyImmediate(full);
            Object.DestroyImmediate(crop);
        }

        private static void Finish()
        {
            Time.timeScale = 1f;
            s_Weapon.Fired -= OnFired;
            SessionState.SetInt(c_StateKey, 2);
            EditorApplication.ExitPlaymode();
        }
    }
}
