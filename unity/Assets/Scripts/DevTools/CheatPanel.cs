#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.DevTools
{
    /// <summary>
    /// Developer cheats for testing, opened and closed with Insert or F8: every workshop upgrade of the car one level
    /// up or to the maximum for free, a jump to wave 10 / 20 / 30 (the boss waves) or the next one, scrap, ammo,
    /// healing, clearing the zombies, coins for the cases and the tutorial again. The game keeps running (clicks on the
    /// panel never shoot: the player's input ignores clicks over the UI). A uGUI panel with the game's TextMeshPro font
    /// (Cyrillic in the WebGL build too), built at runtime. Exists only in the editor and in development builds — the
    /// release build for Yandex Games has no cheats. Created by itself before the first scene.
    /// </summary>
    public sealed class CheatPanel : MonoBehaviour
    {
        private static readonly Color s_Panel = new Color(0.06f, 0.07f, 0.09f, 0.94f);
        private static readonly Color s_ButtonColor = new Color(0.22f, 0.24f, 0.29f, 1f);
        private static readonly Color s_Accent = new Color(0.93f, 0.47f, 0.13f, 1f);
        private static readonly Color s_Title = new Color(1f, 0.8f, 0.3f, 1f);

        private GameObject m_Canvas;
        private RectTransform m_Content;
        private TextMeshProUGUI m_Message;
        private float m_MessageUntil;

        /// <summary>The panel is on screen (tests).</summary>
        public bool IsOpen => m_Canvas != null && m_Canvas.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Object.FindAnyObjectByType<CheatPanel>() != null)
                return;
            var go = new GameObject("[CheatPanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<CheatPanel>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.insertKey.wasPressedThisFrame || keyboard.f8Key.wasPressedThisFrame))
                SetOpen(!IsOpen);
            if (m_Message != null && m_Message.gameObject.activeSelf && Time.unscaledTime > m_MessageUntil)
                m_Message.SetText(string.Empty);
        }

        public void SetOpen(bool open)
        {
            if (open && m_Canvas == null)
                Build();
            if (m_Canvas == null)
                return;
            m_Canvas.SetActive(open);
            if (open)
            {
                EnsureEventSystem();
                Refresh();
            }
        }

        // ------------------------------------------------------------------ actions (public for the tests)
        public WorkshopScreen Workshop => Object.FindAnyObjectByType<WorkshopScreen>(FindObjectsInactive.Include);
        public WaveDirector Waves => Object.FindAnyObjectByType<WaveDirector>();

        public int UpgradeOnce(CarUpgrade upgrade)
        {
            RunState run = Workshop != null ? Workshop.Run : null;
            return run != null && run.GrantUpgrade(upgrade) ? 1 : 0;
        }

        public int UpgradeMax(CarUpgrade upgrade)
        {
            RunState run = Workshop != null ? Workshop.Run : null;
            if (run == null || upgrade.Repeatable)
                return 0;
            int levels = 0;
            while (levels < 100 && run.GrantUpgrade(upgrade))
                levels++;
            return levels;
        }

        public int CarMax()
        {
            WorkshopScreen workshop = Workshop;
            if (workshop == null)
                return 0;
            int levels = 0;
            foreach (CarUpgrade upgrade in workshop.Catalog.Upgrades)
                levels += UpgradeMax(upgrade);
            Object.FindAnyObjectByType<CarHealth>()?.Repair(100000);
            return levels;
        }

        /// <summary>
        /// Jumps to <paramref name="wave"/>: the zombies on the map are removed without rewards (no burst of level-up
        /// cards pausing the game), the countdown to it (3 s) starts now.
        /// </summary>
        public void SkipTo(int wave)
        {
            WaveDirector waves = Waves;
            if (waves == null)
                return;
            ClearZombies(false);
            waves.SkipTo(wave, 3f);
        }

        /// <summary>
        /// Every zombie on the map dies, also the ones still rising from the ground; <paramref name="rewarded"/>: as
        /// kills (scrap and experience) or just removed.
        /// </summary>
        public int ClearZombies(bool rewarded)
        {
            int killed = 0;
            foreach (ZombieController z in Object.FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude))
            {
                if (!z.IsAlive)
                    continue;
                z.Kill(rewarded);
                killed++;
            }
            return killed;
        }

        // ------------------------------------------------------------------ view
        private void Refresh()
        {
            if (m_Content == null)
                return;
            for (int i = m_Content.childCount - 1; i >= 0; i--)
                Destroy(m_Content.GetChild(i).gameObject);

            WorkshopScreen workshop = Workshop;
            WaveDirector waves = Waves;
            if (workshop != null && waves != null)
            {
                RunState run = workshop.Run;
                Header("МАШИНА (МАСТЕРСКАЯ)");
                foreach (CarUpgrade upgrade in workshop.Catalog.Upgrades)
                {
                    CarUpgrade u = upgrade;
                    string level = u.Repeatable ? $"использовано {run.LevelOf(u)}" : $"ур. {run.LevelOf(u)}/{u.MaxLevel}";
                    RectTransform row = Row();
                    Label(row, $"{u.Title}  <color=#9aa>{level}</color>", 380f);
                    Button(row, u.Repeatable ? "СДЕЛАТЬ" : "+1", 90f, s_ButtonColor,
                        () => Done(UpgradeOnce(u) > 0 ? $"{u.Title}: +1" : $"{u.Title}: уже максимум"));
                    if (!u.Repeatable)
                        Button(row, "МАКС", 100f, s_ButtonColor, () => Done($"{u.Title}: максимум (+{UpgradeMax(u)})"));
                }
                Button(Row(), "ВСЯ МАШИНА НА МАКСИМУМ", 0f, s_Accent, () => Done($"Машина на максимуме (+{CarMax()} ур.), починена"));

                Header($"ВОЛНЫ  (сейчас {waves.Wave}{(waves.IsWaveRunning ? ", идёт" : ", перерыв")})");
                RectTransform wavesRow = Row();
                foreach (int wave in new[] { 10, 20, 30 })
                {
                    int w = wave;
                    Button(wavesRow, $"ВОЛНА {w}", 0f, s_ButtonColor, () => { SkipTo(w); Done($"Волна {w} через 3 с"); });
                }
                Button(Row(), "СЛЕДУЮЩАЯ ВОЛНА", 0f, s_ButtonColor, () =>
                {
                    int next = Mathf.Min(waves.Wave + (waves.IsWaveRunning ? 1 : 0), 30);
                    SkipTo(next);
                    Done($"Волна {next} через 3 с");
                });

                Header("ИГРОК");
                Button(Row(), "+1000 МЕТАЛЛОЛОМА", 0f, s_ButtonColor, () => { run.AddMoney(1000); Done("+1000 металлолома"); });
                Button(Row(), "ПОЛНЫЕ ПАТРОНЫ", 0f, s_ButtonColor, () =>
                {
                    Object.FindAnyObjectByType<PlayerWeapon>()?.Refill();
                    Done("Патроны: полный магазин и запас");
                });
                Button(Row(), "ВЫЛЕЧИТЬ ИГРОКА И МАШИНУ", 0f, s_ButtonColor, () =>
                {
                    Object.FindAnyObjectByType<PlayerHealth>()?.Heal(100000);
                    Object.FindAnyObjectByType<CarHealth>()?.Repair(100000);
                    Done("Игрок и машина вылечены");
                });
                Button(Row(), "УБИТЬ ВСЕХ ЗОМБИ", 0f, s_ButtonColor, () => Done($"Убито зомби: {ClearZombies(true)}"));
            }
            else
            {
                Header("МАШИНА И ВОЛНЫ — НА КАРТЕ");
            }

            Header($"МЕНЮ  (монет {MetaProgress.Coins})");
            Button(Row(), "+1000 МОНЕТ ДЛЯ КЕЙСОВ", 0f, s_ButtonColor, () => { MetaProgress.AddCoins(1000); Done("+1000 монет"); });
            Button(Row(), "ПОКАЗАТЬ ОБУЧЕНИЕ СНОВА", 0f, s_ButtonColor, () =>
            {
                MetaProgress.TutorialDone = false;
                Object.FindAnyObjectByType<TutorialDirector>()?.Restart();
                Done("Обучение начнётся заново");
            });
        }

        private void Done(string message)
        {
            Debug.Log($"[CheatPanel] {message}");
            m_Message.SetText(message);
            m_MessageUntil = Time.unscaledTime + 3f;
            Refresh();
        }

        // ------------------------------------------------------------------ building (runtime uGUI)
        private void Build()
        {
            m_Canvas = new GameObject("CheatCanvas", typeof(RectTransform));
            m_Canvas.transform.SetParent(transform, false);
            var canvas = m_Canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;                                   // over the HUD and its pop-ups
            var scaler = m_Canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            m_Canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            RectTransform window = Rect("Window", m_Canvas.transform);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0f, 1f);
            window.anchoredPosition = new Vector2(24f, -24f);
            window.sizeDelta = new Vector2(640f, 1030f);
            Image(window, s_Panel, true);

            TextMeshProUGUI title = Text(Rect("Title", window), "ЧИТЫ   (Insert / F8 — закрыть)", 30f, s_Title);
            Place(title.rectTransform, 16f, -12f, 608f, 44f);
            m_Message = Text(Rect("Message", window), string.Empty, 24f, new Color(0.6f, 1f, 0.6f, 1f));
            Place(m_Message.rectTransform, 16f, -60f, 608f, 34f);

            // Scroll view: Viewport (RectMask2D + Image) / Content (VerticalLayoutGroup + ContentSizeFitter).
            RectTransform scroll = Rect("Scroll", window);
            Place(scroll, 12f, -100f, 616f, 860f);
            RectTransform viewport = Rect("Viewport", scroll);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            Image(viewport, new Color(0f, 0f, 0f, 0.001f), true);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            m_Content = Rect("Content", viewport);
            m_Content.anchorMin = new Vector2(0f, 1f);
            m_Content.anchorMax = new Vector2(1f, 1f);
            m_Content.pivot = new Vector2(0.5f, 1f);
            m_Content.offsetMin = m_Content.offsetMax = Vector2.zero;
            var layout = m_Content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            m_Content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = m_Content;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

            UnityEngine.UI.Button close = Button(window, "ЗАКРЫТЬ", 0f, s_Accent, () => SetOpen(false));
            Place((RectTransform)close.transform, 16f, -966f, 608f, 52f);
            m_Canvas.SetActive(false);
        }

        // The map scene's HUD has an EventSystem; this makes sure the panel is clickable in any scene.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void Header(string text)
        {
            RectTransform row = Row(44f);
            Label(row, $"<b>{text}</b>", 0f).color = s_Title;
        }

        private RectTransform Row(float height = 50f)
        {
            RectTransform row = Rect("Row", m_Content);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return row;
        }

        private TextMeshProUGUI Label(RectTransform row, string text, float width)
        {
            TextMeshProUGUI label = Text(Rect("Label", row), text, 24f, Color.white);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var element = label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            if (width > 0f)
            {
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
            }
            else
            {
                element.flexibleWidth = 1f;
            }
            return label;
        }

        private UnityEngine.UI.Button Button(Transform parent, string label, float width, Color color, Action onClick)
        {
            RectTransform rt = Rect("Button", parent);
            UnityEngine.UI.Image image = Image(rt, color, true);
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            TextMeshProUGUI text = Text(Rect("Label", rt), label, 22f, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var element = rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            if (width > 0f)
            {
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
            }
            else
            {
                element.flexibleWidth = 1f;
            }
            return button;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static UnityEngine.UI.Image Image(RectTransform rt, Color color, bool raycast)
        {
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color color)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;                      // the game's font (Cyrillic)
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }
    }
}
#endif
