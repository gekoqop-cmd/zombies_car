using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.UI;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Play-mode walkthrough of the menus and the HUD, driven through the real UI: main menu, settings pop-up, map
    /// selection by day / by night / the locked Farm card, then Play (Map01 + HUD): wave countdown, player hit, firing,
    /// reloading, the settings pop-up from the HUD gear, back to the menu from it; then Map02 from the night card and
    /// its street lamps. Screenshots/UI/*.png.
    /// Tools/Diagnostics/UI Screenshots (batch: without -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class UiShots
    {
        private const string c_StateKey = "ZombiCar.UiShots.State";
        private const string c_Folder = "Screenshots/UI";
        private const int c_Width = 1920;
        private const int c_Height = 1080;

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
            public bool InteractPressed => false;
            public bool SwitchWeaponPressed => false;
            public int WeaponSlotPressed => -1;

            public bool TryGetAimRay(out Ray ray)
            {
                ray = default;
                return false;
            }
        }

        private static readonly List<(float time, Action action)> s_Steps = new List<(float, Action)>();
        private static bool s_Started;
        private static float s_Start;
        private static int s_Next;
        private static int s_Errors;
        private static Bot s_Bot;

        static UiShots()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/UI Screenshots")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SceneBuilder.c_MenuScene, OpenSceneMode.Single);
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
                Debug.Log($"[UiShots] Finished, errors {s_Errors}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
            {
                s_Started = true;
                s_Start = Time.realtimeSinceStartup;
                Application.logMessageReceived += (m, st, type) =>
                {
                    if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    {
                        s_Errors++;
                        Debug.Log($"[UiShots] error seen: {m}");
                    }
                };
                BuildSteps();
            }
            float t = Time.realtimeSinceStartup - s_Start;
            while (s_Next < s_Steps.Count && t >= s_Steps[s_Next].time)
                s_Steps[s_Next++].action();
        }

        private static void BuildSteps()
        {
            float t = 0.5f;
            void At(float time, Action action) => s_Steps.Add((time, action));
            MainMenuController Menu() => Object.FindAnyObjectByType<MainMenuController>();
            MapSelectScreen Select() => Object.FindAnyObjectByType<MapSelectScreen>(FindObjectsInactive.Include);

            At(t, () => Shot("01_Menu_Main"));
            At(t += 0.3f, () => Menu().Settings.Show());
            At(t += 0.5f, () => Shot("02_Menu_Settings"));
            At(t += 0.1f, () => { Menu().Settings.Hide(); Menu().Settings.Snap(); });
            At(t += 0.2f, () => Click(Menu(), "PlayButton"));
            At(t += 0.8f, () => Shot("03_Map_City_Day"));
            At(t += 0.1f, () => Click(Select().Current, "NightButton"));
            At(t += 0.12f, () => Shot("04_Map_City_Crossfade"));
            At(t += 0.5f, () => Shot("05_Map_City_Night"));
            At(t += 0.1f, () => Click(Select(), "NextButton"));
            At(t += 0.8f, () => Shot("06_Map_Farm_Locked"));
            At(t += 0.1f, () => Click(Select(), "PrevButton"));
            At(t += 0.6f, () => Click(Select().Current, "DayButton"));
            At(t += 0.4f, () => Click(Select().Current, "PlayButton"));      // loads Map01 + HUD

            // HUD in Map01 with live data.
            At(t += 3f, () =>
            {
                InstallBot();
                Shot("07_HUD_Countdown");
            });
            At(t += 0.6f, () => Object.FindAnyObjectByType<PlayerHealth>().TakeDamage(16, Vector3.left));
            At(t += 0.6f, () => { s_Bot.AimActive = true; s_Bot.FireHeld = true; });
            At(t += 1.2f, () => Shot("08_HUD_Damaged_Firing"));
            At(t += 0.2f, () => { s_Bot.FireHeld = false; s_Bot.ReloadUntilFrame = Time.frameCount + 2; });
            At(t += 0.7f, () => Shot("09_HUD_Reloading"));
            At(t += 1.5f, () => Click(Object.FindAnyObjectByType<HudController>(), "SettingsButton"));
            At(t += 0.5f, () => Shot("10_HUD_Settings_From_Gear"));
            // "To menu" from the in-game settings, then the night map through the menu.
            At(t += 0.2f, () => Click(Object.FindAnyObjectByType<SettingsPanel>(), "MenuButton"));
            At(t += 2.5f, () => Shot("11_Back_In_Menu"));
            At(t += 0.2f, () => Click(Menu(), "PlayButton"));
            At(t += 0.8f, () => Click(Select().Current, "NightButton"));
            At(t += 0.5f, () => Click(Select().Current, "PlayButton"));      // loads Map02 + HUD
            At(t += 3.5f, () => Shot("12_Map02_Play"));
            At(t += 0.1f, () => Teleport(new Vector3(4f, 0f, 10.5f)));
            At(t += 1.2f, () => Shot("13_Map02_North_Lamps"));
            At(t += 0.1f, () => Teleport(new Vector3(-10f, 0f, -9f)));
            At(t += 1.2f, () => Shot("14_Map02_Lot_Lamps"));
            At(t += 0.1f, () => Teleport(new Vector3(9f, 0f, -6f)));
            At(t += 1.2f, () => Shot("15_Map02_Lot_Lamps_2"));
            At(t += 0.3f, () =>
            {
                SessionState.SetInt(c_StateKey, 2);
                EditorApplication.ExitPlaymode();
            });
        }

        /// <summary>Clicks the named button under <paramref name="root"/> through its onClick (as a real press would).</summary>
        private static void Click(Component root, string name)
        {
            Button button = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            if (button == null || !button.IsInteractable())
            {
                Debug.LogError($"[UiShots] Button {name} not found or disabled under {root.name}");
                return;
            }
            button.onClick.Invoke();
        }

        private static void Teleport(Vector3 position)
        {
            var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            var controller = weapon.GetComponent<CharacterController>();
            controller.enabled = false;
            weapon.transform.position = position;
            controller.enabled = true;
            Object.FindAnyObjectByType<CameraFollowTarget>().Snap();
            var vcam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
                vcam.PreviousStateIsValid = false;
        }

        private static void InstallBot()
        {
            var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Bot = new Bot();
            var provider = weapon.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in weapon.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }
        }

        // Overlay canvases are not part of camera renders: draw them through the main camera for the picture.
        private static void Shot(string name)
        {
            Camera cam = Camera.main;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.5f;
            }
            cam.cullingMask |= 1 << UiFactory.c_UiLayer;   // the menu's UI camera renders nothing else
            string folder = Path.GetFullPath(c_Folder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".png");
            MapScreenshots.Render(cam, c_Width, c_Height, path);
            Debug.Log($"[UiShots] {path}");
        }
    }
}
