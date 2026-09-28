using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Scripted Play-mode walkthrough of Map01 that photographs what the player, camera and car plate do:
    /// HUD start state, idle / run / shot / run + shoot / reload close-ups, camera frames while running right and
    /// after stopping, bullets stopping at the car (sparks, no damage), the durability plate before / on / after
    /// a hit, and the camera at the four corners of the play area (confiner). Waves are paused so nothing else
    /// moves. Output: Screenshots/Task_*.png. Batch (without -quit, it exits by itself):
    /// Unity.exe -batchmode -projectPath &lt;unity&gt; -executeMethod ZombiCar.EditorTools.TaskShots.Run
    /// </summary>
    [InitializeOnLoad]
    public static class TaskShots
    {
        private const string c_StateKey = "ZombiCar.TaskShots.State";   // 0 idle, 1 running, 2 finished
        private const string c_Folder = "Screenshots";
        private const int c_Width = 1920;
        private const int c_Height = 1080;
        private const int c_CloseUp = 420;
        private const float c_SlowMotion = 0.1f;                               // square crop around the player, px

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
            public bool ReloadPressed => Time.frameCount <= ReloadUntilFrame;   // held for a couple of frames
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
            public int Order;          // keeps steps at the same time in the order they were added
            public Action Action;
        }

        private static readonly List<Step> s_Steps = new List<Step>();
        private static bool s_Started;
        private static float s_StartTime;
        private static int s_NextStep;
        private static int s_Errors;
        private static Bot s_Bot;
        private static Transform s_Player;
        private static CharacterController s_Controller;
        private static PlayerWeapon s_Weapon;
        private static CarHealth s_Car;
        private static CameraFollowTarget s_FollowTarget;
        private static CinemachineCamera s_Vcam;
        private static MapConfig s_Map;
        private static string s_OnShot;            // close-up to take right after the next shot

        static TaskShots()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Task Screenshots")]
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
                Debug.Log($"[TaskShots] Finished, errors {s_Errors}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
                Begin();

            float t = Time.time - s_StartTime;
            while (s_NextStep < s_Steps.Count && t >= s_Steps[s_NextStep].Time)
                s_Steps[s_NextStep++].Action();
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
            Object.FindAnyObjectByType<WaveDirector>().enabled = false;     // no zombies: the shots show the player
            s_Weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Player = s_Weapon.transform;
            s_Controller = s_Player.GetComponent<CharacterController>();
            s_Car = Object.FindAnyObjectByType<CarHealth>();
            s_FollowTarget = Object.FindAnyObjectByType<CameraFollowTarget>();
            s_Vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            s_Map = AssetDatabase.LoadAssetAtPath<MapConfig>(DataAssetsBuilder.c_MapConfig);

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
            Debug.Log("[TaskShots] Started.");
        }

        private static void BuildSteps()
        {
            s_Steps.Clear();
            float t = 0.3f;
            void At(float time, Action action) => s_Steps.Add(new Step { Time = time, Order = s_Steps.Count, Action = action });

            At(t, () => Wide("Task_HUD_Start"));
            At(t, () => CloseUp("Task_Player_Idle"));
            At(t += 0.1f, () => Teleport(new Vector3(-12f, 0f, -7f)));

            // Run right: camera leads, frames every 0.3 s; then stop and let it settle back.
            At(t += 0.6f, () => s_Bot.Move = Vector2.right);
            for (int i = 1; i <= 5; i++)
            {
                int n = i;
                At(t + 0.3f * i, () => Wide($"Task_Cam_RunRight_{n}_{0.3f * n:F1}s"));
            }
            At(t + 0.9f, () => CloseUp("Task_Player_Run"));
            At(t += 1.7f, () => s_Bot.Move = Vector2.zero);
            At(t + 0.3f, () => Wide("Task_Cam_Stop_0.3s"));
            At(t + 1.5f, () => Wide("Task_Cam_Stop_1.5s"));

            // Standing shot (full Shot clip), then running while shooting (Run + flash + recoil).
            At(t += 1.8f, () =>
            {
                s_Bot.AimActive = true;
                s_Bot.AimDirection = Vector3.right;
                s_Bot.FireHeld = true;
                s_OnShot = "Task_Player_Shot";
            });
            At(t += 0.6f, () =>
            {
                s_Bot.Move = new Vector2(-1f, 0f);       // backs off to the left, still firing right
                s_OnShot = null;
            });
            At(t += 0.4f, () => s_OnShot = "Task_Player_RunShoot");
            At(t += 0.5f, () =>
            {
                s_Bot.Move = Vector2.zero;
                s_Bot.FireHeld = false;
                s_Bot.AimActive = false;
                s_OnShot = null;
            });

            // Reload (R) — the HUD bar and the reload clip halfway through.
            At(t += 0.4f, () => s_Bot.ReloadUntilFrame = Time.frameCount + 2);
            At(t + 0.5f, () => CloseUp("Task_Player_Reload"));
            At(t + 0.7f, () => Wide("Task_HUD_Reload"));

            // Bullets at the car: sparks where they stop, car health unchanged.
            At(t += 2f, () =>
            {
                Vector3 car = s_Car.transform.position;
                Teleport(car + new Vector3(-6f, 0f, -1.5f));
                Debug.Log($"[TaskShots] Car health before shooting it: {s_Car.Health}/{s_Car.MaxHealth}");
            });
            At(t += 0.6f, () =>
            {
                Vector3 toCar = s_Car.transform.position - s_Player.position;
                toCar.y = 0f;
                s_Bot.AimActive = true;
                s_Bot.AimDirection = toCar.normalized;
                s_Bot.FireHeld = true;
                Time.timeScale = c_SlowMotion;           // batch frames are slow: catch the sparks
            });
            for (int i = 1; i <= 4; i++)
            {
                int n = i;
                At(t + 0.2f + 0.04f * i, () => CloseUpAt($"Task_Bullet_Car_{n}", s_Car.transform.position + Vector3.up, 640));
            }
            At(t += 0.45f, () =>
            {
                Time.timeScale = 1f;
                s_Bot.FireHeld = false;
                s_Bot.AimActive = false;
                Debug.Log($"[TaskShots] Car health after shooting it: {s_Car.Health}/{s_Car.MaxHealth}");
            });

            // Durability plate: before, on a hit (flash, punch), while the trail catches up, after.
            Vector3 plateAt = Vector3.up * 3.2f;
            At(t += 0.5f, () => CloseUpAt("Task_Plate_1_Before", s_Car.transform.position + plateAt, 640));
            At(t += 0.1f, () =>
            {
                Time.timeScale = c_SlowMotion;           // the flash lasts 0.3 s: slow time down to catch it
                s_Car.TakeDamage(70, Vector3.right);
            });
            At(t + 0.03f, () => CloseUpAt("Task_Plate_2_Hit", s_Car.transform.position + plateAt, 640));
            At(t + 0.4f, () => CloseUpAt("Task_Plate_3_Trail", s_Car.transform.position + plateAt, 640));
            At(t += 0.45f, () => Time.timeScale = 1f);
            At(t + 1.2f, () => CloseUpAt("Task_Plate_4_After", s_Car.transform.position + plateAt, 640));
            At(t + 1.2f, () => Wide("Task_Plate_Wide"));

            // Confiner: the camera at the corners of the play area.
            t += 1.5f;
            Vector3 c = s_Map.PlayAreaCenter;
            float hx = s_Map.PlayAreaSize.x * 0.5f - 1.5f, hz = s_Map.PlayAreaSize.y * 0.5f - 1.5f;
            (string name, float x, float z)[] corners = { ("NW", -hx, hz), ("NE", hx, hz), ("SE", hx, -hz), ("SW", -hx, -hz) };
            foreach ((string name, float x, float z) in corners)
            {
                Vector3 p = c + new Vector3(x, 0f, z);
                At(t, () => Teleport(p));
                At(t += 1.2f, () => Wide($"Task_Confiner_{name}"));
                t += 0.1f;
            }
            At(t + 0.2f, Finish);
            s_Steps.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Order.CompareTo(b.Order));
        }

        private static void OnFired()
        {
            if (s_OnShot == null)
                return;
            EditorApplication.delayCall += TakeShotCloseUp;
        }

        private static void TakeShotCloseUp()
        {
            if (s_OnShot == null)
                return;
            CloseUp(s_OnShot);
            s_OnShot = null;
        }

        private static void Teleport(Vector3 position)
        {
            s_Controller.enabled = false;
            s_Player.position = position;
            s_Controller.enabled = true;
            s_FollowTarget.Snap();
            s_Vcam.PreviousStateIsValid = false;             // cut, no damping across the map
        }

        // The HUD is a screen-space overlay, which camera renders leave out: draw it through the camera instead.
        private static void HudOnCamera()
        {
            var hud = Object.FindAnyObjectByType<ZombiCar.UI.HudController>();
            if (hud == null || !hud.TryGetComponent(out Canvas canvas) || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                return;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = Camera.main.nearClipPlane + 0.5f;
        }

        private static void Wide(string name)
        {
            HudOnCamera();
            string path = OutputPath(name);
            MapScreenshots.Render(Camera.main, c_Width, c_Height, path);
            Debug.Log($"[TaskShots] {path}");
        }

        private static void CloseUp(string name) => CloseUpAt(name, s_Player.position + Vector3.up * 0.9f, c_CloseUp);

        private static void CloseUpAt(string name, Vector3 world, int size)
        {
            HudOnCamera();
            string path = OutputPath(name);
            MapScreenshots.Render(Camera.main, c_Width, c_Height, path);
            // The render is c_Width x c_Height whatever the (batch) game view is: project with that aspect.
            Camera cam = Camera.main;
            cam.aspect = (float)c_Width / c_Height;
            Vector3 viewport = cam.WorldToViewportPoint(world);
            cam.ResetAspect();
            var full = new Texture2D(2, 2);
            full.LoadImage(File.ReadAllBytes(path));
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * c_Width) - size / 2, 0, c_Width - size);
            int y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * c_Height) - size / 2, 0, c_Height - size);
            var crop = new Texture2D(size, size, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x, y, size, size));
            crop.Apply();
            File.WriteAllBytes(path, crop.EncodeToPNG());
            Object.DestroyImmediate(full);
            Object.DestroyImmediate(crop);
            Debug.Log($"[TaskShots] {path}");
        }

        private static string OutputPath(string name)
        {
            string folder = System.IO.Path.GetFullPath(c_Folder);
            Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, name + ".png");
        }

        private static void Finish()
        {
            s_Weapon.Fired -= OnFired;
            SessionState.SetInt(c_StateKey, 2);
            EditorApplication.ExitPlaymode();
        }
    }
}
