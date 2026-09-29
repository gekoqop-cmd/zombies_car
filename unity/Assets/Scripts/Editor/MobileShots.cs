using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.UI;
using Object = UnityEngine.Object;
using SettingsService = ZombiCar.Core.SettingsService;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Play-mode check of the touch controls, run as a phone (start the editor with -forceMobile): menu → Map01,
    /// then the sticks are pressed and dragged through the UI event handlers like a finger would: walking, aiming +
    /// firing (aim line), a quick tap (auto-aim at the nearest zombie), the settings with the touch-controls row, the
    /// layout editor (a stick dragged, size changed, Done) and the saved result. Screenshots/Mobile/*.png at 19.5:9.
    /// Batch: Unity.exe -batchmode -projectPath &lt;unity&gt; -forceMobile -executeMethod ZombiCar.EditorTools.MobileShots.Run
    /// </summary>
    [InitializeOnLoad]
    public static class MobileShots
    {
        private const string c_StateKey = "ZombiCar.MobileShots.State";
        private const string c_Folder = "Screenshots/Mobile";
        private const int c_Width = 2340;
        private const int c_Height = 1080;

        private static readonly List<(float time, Action action)> s_Steps = new List<(float, Action)>();
        private static bool s_Started;
        private static float s_Start;
        private static int s_Next;
        private static int s_Errors;
        private static readonly Dictionary<VirtualJoystick, PointerEventData> s_Held = new Dictionary<VirtualJoystick, PointerEventData>();

        static MobileShots()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Mobile Screenshots")]
        public static void Run()
        {
            foreach (string key in new[] { "move", "aim", "reload", "action", "weapons" })
            {
                PlayerPrefs.DeleteKey($"mobile.controls.{key}.x");
                PlayerPrefs.DeleteKey($"mobile.controls.{key}.y");
            }
            PlayerPrefs.DeleteKey("settings.controls.scale");
            PlayerPrefs.SetInt("settings.quality", (int)QualityTier.Medium);   // no first-run auto-tuning in the test
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
                Debug.Log($"[MobileShots] Finished, errors {s_Errors}.");
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
                        Debug.Log($"[MobileShots] error seen: {m}");
                    }
                };
                Debug.Log($"[MobileShots] DeviceInfo.IsMobile = {DeviceInfo.IsMobile}");
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
            VirtualJoystick Stick(string name) => Object.FindObjectsByType<VirtualJoystick>().First(j => j.name == name);

            At(t, () => Click(Menu(), "PlayButton"));
            At(t += 0.8f, () => Click(Select().Current, "PlayButton"));
            At(t += 3.5f, () =>
            {
                var provider = Object.FindAnyObjectByType<PlayerInputProvider>();
                Debug.Log($"[MobileShots] player input: {provider.Input.GetType().Name}; layout {(MobileControlsLayout.Instance != null ? "on" : "OFF")}");
                Shot("M01_HUD_Sticks");
                // The controls keep apart at the phone's size (the rects still hold the layout of the shot).
                Transform area = MobileControlsLayout.Instance.transform.Find("Area");
                string[] names = { "ReloadButton", "WeaponSlots", "ActionButton" };
                var rects = names.Select(n => area.Find(n) as RectTransform).ToArray();
                Rect World(RectTransform rt)
                {
                    var c = new Vector3[4];
                    rt.GetWorldCorners(c);
                    return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                }
                for (int a = 0; a < rects.Length; a++)
                    for (int b = a + 1; b < rects.Length; b++)
                        if (World(rects[a]).Overlaps(World(rects[b])))
                            Debug.LogError($"[MobileShots] {names[a]} overlaps {names[b]}");
                Debug.Log("[MobileShots] touch buttons checked for overlaps");
            });
            // Walk up-right with the left stick.
            At(t += 0.2f, () => Press(Stick("MoveStick"), new Vector2(0.7f, 0.5f)));
            At(t += 1.0f, () =>
            {
                Shot("M02_Moving");
                Debug.Log($"[MobileShots] moving: MobileControls.Move = {MobileControls.Move}");
            });
            // Aim left and fire with the right stick while still walking.
            At(t += 0.1f, () => Press(Stick("AimStick"), new Vector2(-0.9f, 0.25f)));
            At(t += 0.6f, () =>
            {
                Shot("M03_Aiming_Firing");
                var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
                Debug.Log($"[MobileShots] aiming: dir {weapon.ShotDirection}, ammo {Object.FindAnyObjectByType<HudController>() != null}");
            });
            At(t += 0.1f, () => { ReleaseAll(); });
            // Quick tap once a zombie is around: the burst turns towards it.
            At(t += 5.5f, () =>
            {
                Tap(Stick("AimStick"));
                ZombieController zombie = Object.FindObjectsByType<ZombieController>().Where(z => z.IsAlive)
                    .OrderBy(z => (z.transform.position - Object.FindAnyObjectByType<PlayerWeapon>().transform.position).sqrMagnitude)
                    .FirstOrDefault();
                Debug.Log($"[MobileShots] tap: nearest zombie {(zombie != null ? zombie.name : "none")}");
            });
            At(t += 0.12f, () =>
            {
                var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
                ZombieController zombie = Object.FindObjectsByType<ZombieController>().Where(z => z.IsAlive)
                    .OrderBy(z => (z.transform.position - weapon.transform.position).sqrMagnitude).FirstOrDefault();
                if (zombie != null)
                {
                    Vector3 to = zombie.transform.position - weapon.transform.position;
                    to.y = 0f;
                    Debug.Log($"[MobileShots] tap burst: shot dir {weapon.ShotDirection}, to zombie {to.normalized}, angle {Vector3.Angle(weapon.ShotDirection, to):F1}°");
                }
                Shot("M04_Tap_AutoAim");
            });
            // Settings from the gear: the touch-controls row.
            At(t += 0.6f, () => Click(Object.FindAnyObjectByType<HudController>(), "SettingsButton"));
            At(t += 0.5f, () => Shot("M05_Settings_Mobile"));
            At(t += 0.1f, () => Click(Object.FindAnyObjectByType<SettingsPanel>(), "EditLayoutButton"));
            At(t += 0.3f, () => Shot("M06_Edit_Mode"));
            // Drag the aim stick up-left a bit and the reload button to the left side, make everything bigger, Done.
            At(t += 0.1f, () =>
            {
                MobileControlsLayout layout = MobileControlsLayout.Instance;
                DragControl(layout, Stick("AimStick").GetComponent<RectTransform>(), new Vector2(-180f, 120f));
                DragControl(layout, layout.GetComponentsInChildren<TouchButton>(true)[0].GetComponent<RectTransform>(), new Vector2(-900f, 60f));
                DragControl(layout, Stick("MoveStick").GetComponent<RectTransform>(), new Vector2(0f, 2000f));   // clamped below the HUD
                SettingsService.SetControlsScale(1.25f);
            });
            At(t += 0.3f, () => Shot("M07_Edit_Moved"));
            At(t += 0.1f, () => Click(MobileControlsLayout.Instance, "DoneButton"));
            At(t += 0.5f, () =>
            {
                Shot("M08_Custom_Layout");
                Debug.Log($"[MobileShots] saved: aim x={PlayerPrefs.GetFloat("mobile.controls.aim.x"):F3} y={PlayerPrefs.GetFloat("mobile.controls.aim.y"):F3}, " +
                          $"scale {SettingsService.ControlsScale}, timeScale {Time.timeScale}");
            });
            At(t += 0.3f, () =>
            {
                SessionState.SetInt(c_StateKey, 2);
                EditorApplication.ExitPlaymode();
            });
        }

        // ---------------------------------------------------------------- simulated touches (overlay canvas: camera null)
        private static Vector2 CenterOf(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

        private static void Press(VirtualJoystick stick, Vector2 direction)
        {
            var ring = stick.transform.Find("Ring") as RectTransform;
            Vector2 center = CenterOf(ring);
            float radius = ring.rect.width * 0.5f * ring.lossyScale.x;
            var data = new PointerEventData(EventSystem.current) { pointerId = s_Held.Count + 1, position = center };
            stick.OnPointerDown(data);
            data.position = center + direction * radius;
            stick.OnDrag(data);
            s_Held[stick] = data;
        }

        private static void Tap(VirtualJoystick stick)
        {
            var ring = stick.transform.Find("Ring") as RectTransform;
            var data = new PointerEventData(EventSystem.current) { pointerId = 9, position = CenterOf(ring) };
            stick.OnPointerDown(data);
            stick.OnPointerUp(data);
        }

        private static void ReleaseAll()
        {
            foreach (KeyValuePair<VirtualJoystick, PointerEventData> held in s_Held)
                held.Key.OnPointerUp(held.Value);
            s_Held.Clear();
        }

        private static void DragControl(MobileControlsLayout layout, RectTransform control, Vector2 delta)
        {
            Vector2 from = CenterOf(control);
            float scale = control.GetComponentInParent<Canvas>().scaleFactor;
            var data = new PointerEventData(EventSystem.current) { pointerId = 20, position = from };
            layout.OnPointerDown(data);
            data.position = from + delta * scale;
            layout.OnDrag(data);
            layout.OnPointerUp(data);
        }

        private static void Click(Component root, string name)
        {
            Button button = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            if (button == null || !button.IsInteractable() || !button.gameObject.activeInHierarchy)
            {
                Debug.LogError($"[MobileShots] Button {name} not found, hidden or disabled under {root.name}");
                return;
            }
            button.onClick.Invoke();
        }

        // Overlay canvases are not part of camera renders: switch them to the camera for the picture, then back.
        private static void Shot(string name)
        {
            Camera cam = Camera.main;
            var switched = new List<Canvas>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.5f;
                switched.Add(canvas);
            }
            cam.cullingMask |= 1 << UiFactory.c_UiLayer;
            string folder = Path.GetFullPath(c_Folder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".png");
            MapScreenshots.Render(cam, c_Width, c_Height, path);
            foreach (Canvas canvas in switched)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Exclude))
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }
            Canvas.ForceUpdateCanvases();
            Debug.Log($"[MobileShots] {path}");
        }
    }
}
