using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The HUD's run parts: scrap / kill counters, the interaction prompt, the level-up cards and the workshop window,
    /// all in the style of the menu (dark rounded plates of the reference, outlined labels, orange primary buttons).
    /// </summary>
    public static partial class SceneBuilder
    {
        private static readonly Color s_DimColor = new Color(0.02f, 0.02f, 0.03f, 0.72f);
        private static readonly Color s_SubtitleColor = new Color(0.82f, 0.8f, 0.76f, 1f);
        private static readonly Color s_TitleColor = new Color(1f, 0.8f, 0.3f, 1f);
        private static readonly Color s_PillColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);

        private static RunState Run => DataAssetsBuilder.Load<RunState>(DataAssetsBuilder.c_RunState);

        // ------------------------------------------------------------------ counters
        /// <summary>
        /// The grey damage trail behind a health fill (<see cref="DamageTrailBar"/> on <paramref name="host"/>): a
        /// translucent strip over the fill's rect, drawn under it. Returns the fill.
        /// </summary>
        internal static Image DamageTrail(RectTransform host, Image fill)
        {
            RectTransform fillRt = fill.rectTransform;
            RectTransform trail = UiFactory.Stretch("DamageTrail", fillRt.parent);
            trail.anchorMin = fillRt.anchorMin;
            trail.anchorMax = fillRt.anchorMax;
            trail.offsetMin = fillRt.offsetMin;
            trail.offsetMax = fillRt.offsetMax;
            trail.SetSiblingIndex(fillRt.GetSiblingIndex());
            RectTransform strip = UiFactory.Stretch("Strip", trail);             // 0..fill of the same lane as the fill
            UiFactory.Image(strip, null, new Color(0.9f, 0.9f, 0.92f, 0.55f), false);
            CharacterBuilder.SetProps(host.gameObject.AddComponent<DamageTrailBar>(), ("m_Fill", fill), ("m_Trail", strip));
            return fill;
        }

        /// <summary>Scrap under the health panel (the kills are counted up on the end screen only). Returns its panel: it pops when scrap comes in.</summary>
        private static RectTransform BuildRunCounters(RectTransform safe, Material outline, float top, out TextMeshProUGUI money) =>
            CounterPill("ScrapCounter", safe, new Vector2(24f, top), 200f, "UI_Icon_Scrap", outline, out money);

        private static RectTransform CounterPill(string name, RectTransform parent, Vector2 pos, float width, string icon, Material outline,
            out TextMeshProUGUI value)
        {
            var topLeft = new Vector2(0f, 1f);
            const float height = 58f;
            RectTransform pill = UiFactory.Rect(name, parent, topLeft, topLeft, new Vector2(pos.x, -pos.y), new Vector2(width, height));
            UiFactory.Image(pill, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform iconRt = UiFactory.Rect("Icon", pill, midLeft, midLeft, new Vector2(8f, 0f), new Vector2(46f, 46f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            value = OutlinedLabel("Value", pill, midLeft, midLeft, new Vector2(62f, 0f), new Vector2(width - 72f, height), "0", 36f,
                TextAlignmentOptions.Left, Color.white, outline);
            return pill;
        }

        // ------------------------------------------------------------------ weapon slots (desktop)
        private const float c_WeaponSlot = 96f;

        private const float c_WeaponSlotGap = 14f;

        /// <summary>
        /// Bottom right (desktop): a column with the first-aid kits [3] on top, the gun [1] and the axe [2] under it,
        /// the weapon in hand lit orange; a click on a slot takes it (or uses a kit). The "out of ammo: axe" notice shows
        /// left of the column when needed.
        /// </summary>
        private static void BuildWeaponSlots(RectTransform safe, Material outline)
        {
            var bottomRight = new Vector2(1f, 0f);
            RectTransform root = UiFactory.Rect("WeaponSlots", safe, bottomRight, bottomRight, new Vector2(-24f, 24f),
                new Vector2(c_WeaponSlot, c_WeaponSlot * 3f + c_WeaponSlotGap * 2f));
            // Top to bottom in the order of their keys: 1 the gun, 2 the axe, 3 the first-aid kit.
            const float step = c_WeaponSlot + c_WeaponSlotGap;
            Image gun = WeaponSlotPlate("Gun", root, c_WeaponSlot, step * 2f, "UI_Icon_Rifle", "1", outline);
            Image axe = WeaponSlotPlate("Axe", root, c_WeaponSlot, step, "UI_Icon_Axe", "2", outline);
            MedkitSlot("Medkit", root, new Vector2(0.5f, 0f), new Vector2(0f, c_WeaponSlot * 0.5f), c_WeaponSlot, "3", outline);
            var bottomLeft = new Vector2(0f, 0f);
            var hintPivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI hint = OutlinedLabel("EmptyHint", root, bottomLeft, hintPivot, new Vector2(-16f, step + c_WeaponSlot * 0.5f),
                new Vector2(520f, 38f), "ПАТРОНЫ КОНЧИЛИСЬ: ТОПОР", 26f, TextAlignmentOptions.Right, UiFactory.AccentColor, outline);
            hint.gameObject.SetActive(false);
            PrefabBuilder.Wire(root.gameObject.AddComponent<WeaponSlotsView>(),
                ("m_State", DataAssetsBuilder.Load<WeaponState>(DataAssetsBuilder.c_WeaponState)),
                ("m_GunPlate", gun), ("m_AxePlate", axe), ("m_Hint", hint));
        }

        /// <summary>
        /// The first-aid kits carried: a slot like the weapon ones with the kit and "n/5", red while there are kits;
        /// <paramref name="key"/> null = no number (touch).
        /// </summary>
        private static RectTransform MedkitSlot(string name, RectTransform parent, Vector2 anchor, Vector2 pos, float size, string key,
            Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            RectTransform slot = UiFactory.Rect(name, parent, anchor, center, pos, new Vector2(size, size));
            Image plate = UiFactory.Image(slot, UiFactory.Sprite("UI_Panel"), s_PillColor, true, true);
            RectTransform iconRt = UiFactory.Rect("Icon", slot, center, center, new Vector2(0f, 6f), new Vector2(size * 0.6f, size * 0.6f));
            Image icon = UiFactory.Image(iconRt, UiFactory.Sprite("UI_Icon_Medkit"), Color.white, false);
            icon.preserveAspect = true;
            if (key != null)
            {
                var topLeft = new Vector2(0f, 1f);
                OutlinedLabel("Key", slot, topLeft, topLeft, new Vector2(8f, -2f), new Vector2(40f, 40f), key, 30f,
                    TextAlignmentOptions.TopLeft, Color.white, outline);
            }
            var bottom = new Vector2(0.5f, 0f);
            TextMeshProUGUI count = OutlinedLabel("Count", slot, bottom, bottom, new Vector2(0f, 2f), new Vector2(size, 30f), "0/5", 24f,
                TextAlignmentOptions.Center, Color.white, outline);
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            PrefabBuilder.Wire(slot.gameObject.AddComponent<MedkitSlotView>(), ("m_Run", Run), ("m_Button", button), ("m_Plate", plate),
                ("m_Icon", icon), ("m_Count", count));
            return slot;
        }

        /// <summary>A square slot at <paramref name="y"/> from the bottom of the column; <paramref name="key"/> null = no number.</summary>
        private static Image WeaponSlotPlate(string name, RectTransform parent, float size, float y, string icon, string key, Material outline)
        {
            var bottomCenter = new Vector2(0.5f, 0f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform slot = UiFactory.Rect(name, parent, bottomCenter, center, new Vector2(0f, y + size * 0.5f), new Vector2(size, size));
            Image plate = UiFactory.Image(slot, UiFactory.Sprite("UI_Panel"), s_PillColor, true, true);
            RectTransform iconRt = UiFactory.Rect("Icon", slot, center, center, new Vector2(key != null ? 4f : 0f, -2f),
                new Vector2(size * 0.74f, size * 0.74f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            if (key != null)
            {
                var topLeft = new Vector2(0f, 1f);
                OutlinedLabel("Key", slot, topLeft, topLeft, new Vector2(8f, -2f), new Vector2(40f, 40f), key, 30f,
                    TextAlignmentOptions.TopLeft, Color.white, outline);
            }
            return plate;
        }

        // ------------------------------------------------------------------ break skip
        /// <summary>"ПРОПУСТИТЬ ПЕРЕРЫВ • 18 С" with an "E" key badge, bottom centre, under the interaction prompt.</summary>
        private static void BuildBreakSkip(RectTransform safe, Material outline)
        {
            var bottom = new Vector2(0.5f, 0f);
            var center = new Vector2(0.5f, 0.5f);
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform root = UiFactory.Rect("BreakSkip", safe, bottom, bottom, new Vector2(0f, 40f), new Vector2(560f, 76f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Image plate = UiFactory.Image(root, UiFactory.Sprite("UI_Panel"), s_PillColor, true, true);
            RectTransform key = UiFactory.Rect("Key", root, midLeft, midLeft, new Vector2(12f, 0f), new Vector2(56f, 56f));
            UiFactory.Image(key, UiFactory.Sprite("UI_Button"), new Color(1f, 0.93f, 0.8f, 1f), true);
            UiFactory.Label("Letter", key, center, center, Vector2.zero, new Vector2(56f, 56f), "E", 36f,
                TextAlignmentOptions.Center, new Color(0.18f, 0.12f, 0.08f, 1f));
            TextMeshProUGUI label = OutlinedLabel("Label", root, center, center, new Vector2(30f, 0f), new Vector2(470f, 70f),
                "ПРОПУСТИТЬ ПЕРЕРЫВ", 32f, TextAlignmentOptions.Center, Color.white, outline);
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 32f;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            root.gameObject.AddComponent<UIButtonFx>();
            PrefabBuilder.Wire(root.gameObject.AddComponent<BreakSkipView>(),
                ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)), ("m_Run", Run),
                ("m_Interaction", DataAssetsBuilder.Load<ZombiCar.Progression.InteractionState>(DataAssetsBuilder.c_InteractionState)),
                ("m_Group", group), ("m_Button", button), ("m_KeyBadge", key.gameObject), ("m_Label", label));
        }

        // ------------------------------------------------------------------ car under attack
        private static readonly Color s_AlertColor = new Color(1f, 0.32f, 0.24f, 1f);

        /// <summary>"МАШИНУ АТАКУЮТ!" right under the wave panel: car icon and blinking red text on a dark pill.</summary>
        private static void BuildCarAttackAlert(RectTransform safe, Material outline, WaveBanner banner)
        {
            var top = new Vector2(0.5f, 1f);
            RectTransform root = UiFactory.Rect("CarAttackAlert", safe, top, top, new Vector2(0f, -156f), new Vector2(540f, 70f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            RectTransform body = UiFactory.Stretch("Body", root);
            UiFactory.Image(body, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform tile = UiFactory.Rect("IconFrame", body, midLeft, midLeft, new Vector2(9f, 0f), new Vector2(52f, 52f));
            UiFactory.Image(tile, UiFactory.Sprite("UI_Panel"), UiFactory.TextColor, true);
            UiFactory.Image(Inset(UiFactory.Stretch("Icon", tile), 4f), UiFactory.Sprite("UI_IconCar"), Color.white, false);
            TextMeshProUGUI text = OutlinedLabel("Text", body, midLeft, midLeft, new Vector2(66f, 0f), new Vector2(464f, 70f),
                "МАШИНУ АТАКУЮТ!", 42f, TextAlignmentOptions.Center, s_AlertColor, outline);
            PrefabBuilder.Wire(root.gameObject.AddComponent<CarAttackAlert>(),
                ("m_Car", DataAssetsBuilder.Load<CarState>(DataAssetsBuilder.c_CarState)), ("m_Banner", banner), ("m_Group", group),
                ("m_Body", body), ("m_Text", text));
        }

        // ------------------------------------------------------------------ wave banner
        private static WaveBanner BuildWaveBanner(RectTransform safe, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            RectTransform root = UiFactory.Rect("WaveBanner", safe, top, top, new Vector2(0f, -190f), new Vector2(1400f, 170f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            RectTransform body = UiFactory.Stretch("Body", root);
            TextMeshProUGUI title = OutlinedLabel("Title", body, top, top, Vector2.zero, new Vector2(1400f, 96f), "ВОЛНА 1", 84f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI subtitle = OutlinedLabel("Subtitle", body, top, top, new Vector2(0f, -94f), new Vector2(1400f, 50f),
                "ЗОМБИ ИДУТ!", 36f, TextAlignmentOptions.Center, Color.white, outline);
            var banner = root.gameObject.AddComponent<WaveBanner>();
            PrefabBuilder.Wire(banner, ("m_Waves", DataAssetsBuilder.Load<ZombiCar.Waves.WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Group", group), ("m_Body", body), ("m_Title", title), ("m_Subtitle", subtitle));
            return banner;
        }

        // ------------------------------------------------------------------ interaction prompt
        private static void BuildInteractionPrompt(RectTransform safe, Material outline)
        {
            var bottom = new Vector2(0.5f, 0f);
            RectTransform root = UiFactory.Rect("InteractionPrompt", safe, bottom, bottom, new Vector2(0f, 150f), new Vector2(900f, 80f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;

            // The pill grows with its content: [НАЖМИТЕ [E]] [text].
            RectTransform pill = UiFactory.Rect("Pill", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 80f));
            UiFactory.Image(pill, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            var layout = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(28, 30, 8, 8);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = pill.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform hint = UiFactory.Rect("KeyHint", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(250f, 64f));
            var hintLayout = hint.gameObject.AddComponent<HorizontalLayoutGroup>();
            hintLayout.spacing = 12f;
            hintLayout.childAlignment = TextAnchor.MiddleLeft;
            hintLayout.childControlWidth = true;
            hintLayout.childControlHeight = false;
            hintLayout.childForceExpandWidth = hintLayout.childForceExpandHeight = false;
            TextMeshProUGUI press = OutlinedLabel("Press", hint, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(170f, 60f), "НАЖМИТЕ", 34f, TextAlignmentOptions.Left, s_SubtitleColor, outline);
            press.gameObject.AddComponent<LayoutElement>();
            RectTransform key = UiFactory.Rect("Key", hint, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            UiFactory.Image(key, UiFactory.Sprite("UI_Button"), new Color(1f, 0.93f, 0.8f, 1f), true);
            key.gameObject.AddComponent<LayoutElement>().preferredWidth = 62f;
            OutlinedLabel("Letter", key, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f), "E", 40f,
                TextAlignmentOptions.Center, new Color(0.18f, 0.12f, 0.08f, 1f), outline);
            hint.gameObject.AddComponent<LayoutElement>();

            TextMeshProUGUI text = OutlinedLabel("Text", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(400f, 64f), "ПОПОЛНИТЬ ПАТРОНЫ", 36f, TextAlignmentOptions.Left, Color.white, outline);
            text.gameObject.AddComponent<LayoutElement>();

            // Progress of a timed interaction, under the pill.
            RectTransform bar = UiFactory.Rect("Progress", root, bottom, bottom, new Vector2(0f, -26f), new Vector2(420f, 22f));
            UiFactory.Image(bar, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            Image fill = UiFactory.Fill(Inset(UiFactory.Stretch("Fill", bar), 4f), UiFactory.Sprite("UI_BarFillRed"),
                new Color(1f, 0.75f, 0.3f, 1f), 0f);
            bar.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<InteractionPromptView>();
            PrefabBuilder.Wire(view, ("m_State", DataAssetsBuilder.Load<InteractionState>(DataAssetsBuilder.c_InteractionState)),
                ("m_Group", group), ("m_Text", text), ("m_KeyHint", hint.gameObject), ("m_ProgressRoot", bar.gameObject),
                ("m_ProgressFill", fill));
        }

        // ------------------------------------------------------------------ wave progress
        private static readonly Vector2 s_WaveProgress = new Vector2(660f, 108f);
        private const float c_WaveBarHeight = 24f;
        private const float c_BossMark = 52f;
        private const float c_FinalBossMark = 72f;
        private static readonly Color s_FinalBossColor = new Color(0.52f, 0.16f, 0.78f, 1f);
        private const float c_BossBarIcon = 84f;

        /// <summary>
        /// Top centre: a dark pill with "ВОЛНА N" (left) and the timer (right) over a progress line from wave 1 to the
        /// last wave; a boss mark (the boss icon on a red disc) sits on the line at every boss wave; the last boss's mark is bigger and purple.
        /// While a boss is alive the same pill shows the boss's health bar instead (<see cref="WaveProgressView"/>).
        /// </summary>
        private static void BuildWaveProgress(RectTransform safe, Material outline, float margin, out TextMeshProUGUI label,
            out TextMeshProUGUI timer)
        {
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform root = UiFactory.Rect("WaveProgress", safe, top, top, new Vector2(0f, -margin), s_WaveProgress);
            UiFactory.Image(root, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            RectTransform content = UiFactory.Stretch("Waves", root);
            var waveGroup = content.gameObject.AddComponent<CanvasGroup>();
            waveGroup.blocksRaycasts = false;
            label = OutlinedLabel("WaveLabel", content, topLeft, topLeft, new Vector2(26f, -8f), new Vector2(320f, 50f), "ВОЛНА 1", 38f,
                TextAlignmentOptions.Left, Color.white, outline);
            timer = OutlinedLabel("WaveTimer", content, topRight, topRight, new Vector2(-84f, -8f), new Vector2(240f, 50f), "00:05", 38f,
                TextAlignmentOptions.Right, s_TitleColor, outline);

            float barWidth = s_WaveProgress.x - 80f;
            RectTransform bar = UiFactory.Rect("Line", content, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(barWidth, c_WaveBarHeight));
            UiFactory.Image(bar, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            Image fill = UiFactory.Fill(Inset(UiFactory.Stretch("Fill", bar), 4f), UiFactory.Sprite("UI_BarFillRed"),
                new Color(1f, 0.72f, 0.35f, 1f), 0f);

            var waves = DataAssetsBuilder.Load<WaveConfig>(DataAssetsBuilder.c_WaveConfig);
            int count = Mathf.Max(1, waves.WaveCount);
            int[] bossWaves = waves.Bosses.Where(b => b != null && b.Boss != null).Select(b => b.Wave).OrderBy(w => w).ToArray();
            var marks = new RectTransform[bossWaves.Length];
            for (int i = 0; i < bossWaves.Length; i++)
            {
                float x = -barWidth * 0.5f + barWidth * bossWaves[i] / count;
                bool final = i == bossWaves.Length - 1;              // the last boss: a bigger purple mark
                float size = final ? c_FinalBossMark : c_BossMark;
                RectTransform mark = UiFactory.Rect($"Boss{bossWaves[i]}", bar, center, center, new Vector2(x, 0f),
                    new Vector2(size, size));
                UiFactory.Image(mark, UiFactory.Sprite("UI_StickKnob"), final ? s_FinalBossColor : new Color(0.75f, 0.12f, 0.1f, 1f), false);
                RectTransform icon = UiFactory.Rect("Icon", mark, center, center, Vector2.zero, new Vector2(size * 0.78f, size * 0.78f));
                UiFactory.Image(icon, UiFactory.Sprite("UI_Icon_Boss"), Color.white, false).preserveAspect = true;
                marks[i] = mark;
            }
            // The boss bar over the same pill (hidden until a boss comes out): the boss icon on a disc, its name and
            // the wave, a big health bar with the lost-health trail and the number on it.
            RectTransform bossRoot = UiFactory.Stretch("BossBar", root);
            var bossGroup = bossRoot.gameObject.AddComponent<CanvasGroup>();
            bossGroup.blocksRaycasts = false;
            bossGroup.alpha = 0f;
            var left = new Vector2(0f, 0.5f);
            RectTransform disc = UiFactory.Rect("Icon", bossRoot, left, center, new Vector2(18f + c_BossBarIcon * 0.5f, 0f),
                new Vector2(c_BossBarIcon, c_BossBarIcon));
            Image discImage = UiFactory.Image(disc, UiFactory.Sprite("UI_StickKnob"), new Color(0.75f, 0.12f, 0.1f, 1f), false);
            RectTransform bossIcon = UiFactory.Rect("Face", disc, center, center, Vector2.zero, new Vector2(c_BossBarIcon * 0.78f, c_BossBarIcon * 0.78f));
            UiFactory.Image(bossIcon, UiFactory.Sprite("UI_Icon_Boss"), Color.white, false).preserveAspect = true;
            float textLeft = 18f + c_BossBarIcon + 14f;
            float textWidth = s_WaveProgress.x - textLeft - 22f;
            TextMeshProUGUI bossName = OutlinedLabel("Name", bossRoot, topLeft, topLeft, new Vector2(textLeft, -6f),
                new Vector2(textWidth - 170f, 46f), "БОСС", 34f, TextAlignmentOptions.Left, new Color(1f, 0.55f, 0.4f, 1f), outline);
            bossName.enableAutoSizing = true;
            bossName.fontSizeMin = 22f;
            bossName.fontSizeMax = 34f;
            TextMeshProUGUI bossWave = OutlinedLabel("Wave", bossRoot, topRight, topRight, new Vector2(-22f, -10f), new Vector2(170f, 40f),
                "ВОЛНА 10", 28f, TextAlignmentOptions.Right, Color.white, outline);
            RectTransform hpBar = UiFactory.Rect("Health", bossRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textLeft, 12f),
                new Vector2(textWidth, 44f));
            UiFactory.Image(hpBar, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            RectTransform lane = Inset(UiFactory.Stretch("Lane", hpBar), 5f);
            Image hpFill = DamageTrail(lane, UiFactory.Fill(UiFactory.Stretch("Fill", lane), UiFactory.Sprite("UI_BarFillRed"), Color.white, 1f));
            TextMeshProUGUI hpValue = OutlinedLabel("Value", hpBar, center, center, Vector2.zero, new Vector2(textWidth, 44f), "1000 / 1000",
                28f, TextAlignmentOptions.Center, Color.white, outline);
            bossRoot.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<WaveProgressView>();
            PrefabBuilder.Wire(view, ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)), ("m_Fill", fill),
                ("m_WaveGroup", waveGroup), ("m_BossGroup", bossGroup), ("m_BossIcon", disc), ("m_BossDisc", discImage),
                ("m_BossName", bossName), ("m_BossWave", bossWave), ("m_BossValue", hpValue), ("m_BossFill", hpFill));
            CharacterBuilder.SetProps(view, ("m_Marks", marks.Cast<Object>().ToArray()), ("m_FinalBossColor", s_FinalBossColor));
            var so = new SerializedObject(view);
            SerializedProperty list = so.FindProperty("m_BossWaves");
            list.arraySize = bossWaves.Length;
            for (int i = 0; i < bossWaves.Length; i++)
                list.GetArrayElementAtIndex(i).intValue = bossWaves[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ perks taken
        private const float c_PerkIcon = 60f;

        /// <summary>Left, under the scrap / kill counters: the level-up cards the run took, icon + level (a template row).</summary>
        private static void BuildPerkTray(RectTransform safe, Material outline, float top)
        {
            var topLeft = new Vector2(0f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform root = UiFactory.Rect("PerkTray", safe, topLeft, topLeft, new Vector2(24f, -top), new Vector2(c_PerkIcon * 6f + 40f, 300f));
            var grid = root.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(c_PerkIcon, c_PerkIcon);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;

            RectTransform item = UiFactory.Rect("ItemTemplate", root, center, center, Vector2.zero, new Vector2(c_PerkIcon, c_PerkIcon));
            Image disc = UiFactory.Image(item, UiFactory.Sprite("UI_StickKnob"), Color.white, false);
            RectTransform iconRt = UiFactory.Rect("Icon", item, center, center, Vector2.zero, new Vector2(c_PerkIcon * 0.72f, c_PerkIcon * 0.72f));
            Image icon = UiFactory.Image(iconRt, null, Color.white, false);
            icon.preserveAspect = true;
            var bottomRight = new Vector2(1f, 0f);
            RectTransform badge = UiFactory.Rect("Badge", item, bottomRight, bottomRight, new Vector2(8f, -6f), new Vector2(40f, 28f));
            UiFactory.Image(badge, UiFactory.Sprite("UI_Panel"), new Color(0.05f, 0.06f, 0.08f, 0.92f), true);
            TextMeshProUGUI level = OutlinedLabel("Level", badge, center, center, Vector2.zero, new Vector2(40f, 28f), "1", 22f,
                TextAlignmentOptions.Center, Color.white, outline);
            level.enableAutoSizing = true;
            level.fontSizeMin = 14f;
            level.fontSizeMax = 22f;
            item.gameObject.SetActive(false);

            var tray = root.gameObject.AddComponent<PerkTrayView>();
            PrefabBuilder.Wire(tray, ("m_Run", Run), ("m_Pool", DataAssetsBuilder.Load<PerkPool>(DataAssetsBuilder.c_PerkPool)),
                ("m_Template", item), ("m_Disc", disc), ("m_Icon", icon), ("m_Level", level), ("m_LegendaryRays", UiFactory.Sprite("UI_Rays")));
        }

        // ------------------------------------------------------------------ tutorial hint
        /// <summary>First-run hints: a pill above the interaction prompt, "ОБУЧЕНИЕ n/5" over the hint.</summary>
        private static void BuildTutorialHint(RectTransform safe, Material outline)
        {
            var bottom = new Vector2(0.5f, 0f);
            var top = new Vector2(0.5f, 1f);
            RectTransform root = UiFactory.Rect("TutorialHint", safe, bottom, bottom, new Vector2(0f, 250f), new Vector2(1100f, 110f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            RectTransform body = UiFactory.Stretch("Body", root);
            UiFactory.Image(body, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            TextMeshProUGUI step = OutlinedLabel("Step", body, top, top, new Vector2(0f, -8f), new Vector2(1040f, 34f), "ОБУЧЕНИЕ 1/5", 24f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI text = OutlinedLabel("Text", body, top, top, new Vector2(0f, -40f), new Vector2(1040f, 62f), "WASD — ДВИЖЕНИЕ", 36f,
                TextAlignmentOptions.Center, Color.white, outline);
            text.enableAutoSizing = true;
            text.fontSizeMin = 22f;
            text.fontSizeMax = 38f;
            PrefabBuilder.Wire(root.gameObject.AddComponent<TutorialView>(),
                ("m_State", DataAssetsBuilder.Load<TutorialState>(DataAssetsBuilder.c_TutorialState)), ("m_Group", group), ("m_Body", body),
                ("m_Step", step), ("m_Text", text));
        }

        // ------------------------------------------------------------------ tutorial intro cards
        private static readonly Vector2 s_TutorialWindow = new Vector2(1200f, 900f);

        /// <summary>
        /// The tutorial's intro cards (hidden until <see cref="TutorialState.CardsOpen"/>): a dimmed screen with a card
        /// window — picture with one or two icons and a caption, title, text — dots, "ПРОПУСТИТЬ" and "ДАЛЕЕ".
        /// </summary>
        private static void BuildTutorialCards(Transform parent, Material outline)
        {
            RectTransform root = UiFactory.Stretch("TutorialCards", parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.72f), false, true);        // blocks the game behind
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var topRight = new Vector2(1f, 1f);
            var bottomLeft = new Vector2(0f, 0f);
            var bottomRight = new Vector2(1f, 0f);

            RectTransform card = UiFactory.Rect("Card", root, center, center, Vector2.zero, s_TutorialWindow);
            UiFactory.Image(card, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            TextMeshProUGUI counter = OutlinedLabel("Counter", card, topRight, topRight, new Vector2(-40f, -26f), new Vector2(200f, 44f),
                "1 / 6", 30f, TextAlignmentOptions.Right, s_SubtitleColor, outline);
            OutlinedLabel("Header", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(400f, 44f),
                "ОБУЧЕНИЕ", 30f, TextAlignmentOptions.Left, s_SubtitleColor, outline);

            RectTransform picture = UiFactory.Rect("Picture", card, top, top, new Vector2(0f, -80f), new Vector2(s_TutorialWindow.x - 120f, 290f));
            Image pictureBack = UiFactory.Image(picture, UiFactory.Sprite("UI_Panel"), new Color(0.3f, 0.15f, 0.05f, 0.9f), true);
            const float icon = 200f;
            RectTransform icon1 = UiFactory.Rect("Icon", picture, center, center, new Vector2(-150f, 22f), new Vector2(icon, icon));
            Image iconImage = UiFactory.Image(icon1, UiFactory.Sprite("UI_Icon_Boss"), Color.white, false);
            iconImage.preserveAspect = true;
            RectTransform icon2 = UiFactory.Rect("Icon2", picture, center, center, new Vector2(150f, 22f), new Vector2(icon, icon));
            Image icon2Image = UiFactory.Image(icon2, UiFactory.Sprite("UI_Icon_Skull"), Color.white, false);
            icon2Image.preserveAspect = true;
            TextMeshProUGUI caption = OutlinedLabel("Caption", picture, bottom, bottom, new Vector2(0f, 10f), new Vector2(s_TutorialWindow.x - 160f, 56f),
                "30 ВОЛН · 3 БОССА", 40f, TextAlignmentOptions.Center, Color.white, outline);

            TextMeshProUGUI title = OutlinedLabel("Title", card, top, top, new Vector2(0f, -390f), new Vector2(s_TutorialWindow.x - 100f, 80f),
                "ЦЕЛЬ", 62f, TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI text = OutlinedLabel("Text", card, top, top, new Vector2(0f, -474f), new Vector2(s_TutorialWindow.x - 140f, 190f),
                "Продержись 30 волн и убей 3 боссов.", 40f, TextAlignmentOptions.Center, Color.white, outline);
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 40f;
            text.fontStyle = FontStyles.Normal;

            int count = s_TutorialCards.Length;
            const float dot = 22f, dotGap = 20f;
            var dots = new Image[count];
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * (dot + dotGap);
                RectTransform d = UiFactory.Rect($"Dot{i + 1}", card, bottom, center, new Vector2(x, 190f), new Vector2(dot, dot));
                dots[i] = UiFactory.Image(d, UiFactory.Sprite("UI_StickKnob"), Color.white, false);
            }
            Button skip = HudButton("SkipButton", card, bottomLeft, new Vector2(50f, 40f), new Vector2(360f, 104f), "ПРОПУСТИТЬ", 40f,
                Color.white, outline);
            Button next = HudButton("NextButton", card, bottomRight, new Vector2(-50f, 40f), new Vector2(400f, 112f), "ДАЛЕЕ", 48f,
                s_PlayColor, outline);

            var view = root.gameObject.AddComponent<TutorialCardsView>();
            PrefabBuilder.Wire(view, ("m_State", DataAssetsBuilder.Load<TutorialState>(DataAssetsBuilder.c_TutorialState)), ("m_Group", group),
                ("m_Card", card), ("m_PictureBack", pictureBack), ("m_Icon", iconImage), ("m_Icon2", icon2Image), ("m_Caption", caption),
                ("m_Title", title), ("m_Text", text), ("m_Counter", counter), ("m_NextButton", next),
                ("m_NextLabel", next.GetComponentInChildren<TMP_Text>()), ("m_SkipButton", skip));
            CharacterBuilder.SetProps(view, ("m_Dots", dots.Cast<Object>().ToArray()));
            var so = new SerializedObject(view);
            SerializedProperty cards = so.FindProperty("m_Cards");
            cards.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                TutorialCard c = s_TutorialCards[i];
                SerializedProperty p = cards.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("Title").stringValue = c.Title;
                p.FindPropertyRelative("Text").stringValue = c.Text;
                p.FindPropertyRelative("TouchText").stringValue = c.TouchText ?? string.Empty;
                p.FindPropertyRelative("Icon").objectReferenceValue = UiFactory.Sprite(c.Icon);
                p.FindPropertyRelative("Icon2").objectReferenceValue = c.Icon2 != null ? UiFactory.Sprite(c.Icon2) : null;
                p.FindPropertyRelative("TouchIcon").objectReferenceValue = c.TouchIcon != null ? UiFactory.Sprite(c.TouchIcon) : null;
                p.FindPropertyRelative("Caption").stringValue = c.Caption ?? string.Empty;
                p.FindPropertyRelative("TouchCaption").stringValue = c.TouchCaption ?? string.Empty;
                p.FindPropertyRelative("Accent").colorValue = c.Accent;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct TutorialCard
        {
            public string Title, Text, TouchText, Icon, Icon2, TouchIcon, Caption, TouchCaption;
            public Color Accent;
        }

        private static readonly TutorialCard[] s_TutorialCards =
        {
            new TutorialCard
            {
                Title = "ЦЕЛЬ", Icon = "UI_Icon_Skull", Icon2 = "UI_Icon_Boss", Caption = "30 ВОЛН · 3 БОССА",
                Text = "Продержись 30 волн и убей 3 боссов.", Accent = new Color(1f, 0.8f, 0.3f, 1f),
            },
            new TutorialCard
            {
                Title = "УПРАВЛЕНИЕ", Icon = "UI_Icon_Speed", Icon2 = "UI_Icon_Rifle", TouchIcon = "UI_StickRing",
                Caption = "WASD · МЫШЬ · 1 / 2 / 3", TouchCaption = "ДЖОЙСТИКИ · КНОПКИ",
                Text = "WASD — идти, мышь — целиться, левая кнопка — атака.\n1 / 2 — автомат и топор, 3 — аптечка из запаса.",
                TouchText = "Левый джойстик — идти, правый — целиться и стрелять.\nКнопки рядом — автомат, топор и аптечка.",
                Accent = new Color(0.45f, 0.78f, 1f, 1f),
            },
            new TutorialCard
            {
                Title = "ОРУЖИЕ", Icon = "UI_Icon_Rifle", Icon2 = "UI_Icon_Axe", Caption = "АВТОМАТ · ТОПОР",
                Text = "Патроны на оружие заканчиваются — пополняй их у машины между волнами.\nТопор — он топор, и он с тобой всегда :)",
                Accent = new Color(0.93f, 0.47f, 0.13f, 1f),
            },
            new TutorialCard
            {
                Title = "МАШИНА И МАСТЕРСКАЯ", Icon = "UI_IconCar", Icon2 = "UI_Icon_Repair", Caption = "ЗАЩИЩАЙ МАШИНУ",
                Text = "Защищай машину: зомби идут и к ней. Между волнами — мастерская\nи патроны у машины. Готов раньше? E — пропустить перерыв.",
                Accent = new Color(0.55f, 0.9f, 0.45f, 1f),
            },
            new TutorialCard
            {
                Title = "УРОВНИ И КАРТОЧКИ", Icon = "UI_Icon_Xp", Icon2 = "UI_Icon_Damage", Caption = "«НОВОЕ» — ЕЩЁ НЕ БРАЛ",
                Text = "За опыт выбираешь улучшение. «НОВОЕ» — такую карточку\nты ещё не брал. Взятые видны слева.",
                Accent = new Color(0.62f, 0.5f, 1f, 1f),
            },
            new TutorialCard
            {
                Title = "БОССЫ И НАГРАДА", Icon = "UI_Icon_Boss", Icon2 = "UI_Icon_Scrap", Caption = "ВОЛНЫ 10 · 20 · 30",
                Text = "Боссы приходят на 10, 20 и 30 волне — метки на полосе сверху.\nЗа победу — монеты на кейсы и скины.",
                Accent = new Color(1f, 0.35f, 0.3f, 1f),
            },
        };

        // ------------------------------------------------------------------ level-up cards
        private const int c_CardCount = 3;
        private static readonly Vector2 s_CardSize = new Vector2(430f, 560f);
        private const float c_CardSpacingX = 480f;

        private static LevelUpScreen BuildLevelUpScreen(RectTransform parent, Material outline)
        {
            RectTransform host = UiFactory.Stretch("LevelUpScreen", parent);
            RectTransform root = UiFactory.Stretch("Root", host);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", root), null, s_DimColor, false, true);   // blocks the game behind
            var top = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI title = OutlinedLabel("Title", root, top, top, new Vector2(0f, -150f), new Vector2(1400f, 106f), "НОВЫЙ УРОВЕНЬ!",
                88f, TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI subtitle = OutlinedLabel("Subtitle", root, top, top, new Vector2(0f, -262f), new Vector2(1400f, 60f),
                "ВЫБЕРИТЕ УЛУЧШЕНИЕ", 40f, TextAlignmentOptions.Center, s_SubtitleColor, outline);

            var cards = new PerkCardView[c_CardCount];
            for (int i = 0; i < c_CardCount; i++)
                cards[i] = BuildPerkCard(root, i, outline);

            // Reroll for a rewarded ad (the shared gold ad button): bottom left, clear of the cards, the weapon column
            // (bottom right) and the tutorial hint (centre).
            var bottomLeft = new Vector2(0f, 0f);
            Button reroll = AdButton("RerollButton", root, bottomLeft, new Vector2(30f, 36f), new Vector2(370f, 96f), "ПОМЕНЯТЬ", 34f,
                outline);

            var screen = host.gameObject.AddComponent<LevelUpScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Pool", DataAssetsBuilder.Load<PerkPool>(DataAssetsBuilder.c_PerkPool)),
                ("m_Config", DataAssetsBuilder.Load<ProgressionConfig>(DataAssetsBuilder.c_ProgressionConfig)),
                ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Root", group), ("m_Title", title), ("m_Subtitle", subtitle), ("m_RerollButton", reroll));
            CharacterBuilder.SetProps(screen, ("m_Cards", cards));
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        private static PerkCardView BuildPerkCard(RectTransform parent, int index, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            float x = (index - (c_CardCount - 1) * 0.5f) * c_CardSpacingX;
            RectTransform card = UiFactory.Rect($"Card{index + 1}", parent, center, center, new Vector2(x, -90f), s_CardSize);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            RectTransform body = UiFactory.Stretch("Body", card);

            RectTransform glowRt = UiFactory.Stretch("Glow", body);
            glowRt.offsetMin = new Vector2(-22f, -22f);
            glowRt.offsetMax = new Vector2(22f, 22f);
            Image glow = UiFactory.Image(glowRt, UiFactory.Sprite("UI_Panel"), new Color(1f, 0.7f, 0.2f, 0f), true);
            // Legendary only: a gold halo round the plate, rays behind the icon, a shine over the card.
            RectTransform haloRt = UiFactory.Stretch("LegendaryHalo", body);
            haloRt.offsetMin = new Vector2(-10f, -10f);
            haloRt.offsetMax = new Vector2(10f, 10f);
            Image halo = UiFactory.Image(haloRt, UiFactory.Sprite("UI_Panel"), new Color(1f, 0.8f, 0.25f, 1f), true);
            halo.gameObject.SetActive(false);
            Image plate = UiFactory.Image(UiFactory.Stretch("Plate", body), UiFactory.Sprite("UI_GearButton"), Color.white, true, true);

            RectTransform raysRt = UiFactory.Rect("LegendaryRays", body, top, center, new Vector2(0f, -131f), new Vector2(380f, 380f));
            Image rays = UiFactory.Image(raysRt, UiFactory.Sprite("UI_Rays"), new Color(1f, 0.8f, 0.25f, 1f), false);
            rays.gameObject.SetActive(false);
            RectTransform disc = UiFactory.Rect("IconDisc", body, top, top, new Vector2(0f, -36f), new Vector2(190f, 190f));
            Image discImage = UiFactory.Image(disc, UiFactory.Sprite("UI_StickKnob"), Color.white, false);
            RectTransform iconRt = UiFactory.Rect("Icon", disc, center, center, Vector2.zero, new Vector2(150f, 150f));
            Image icon = UiFactory.Image(iconRt, UiFactory.Sprite("UI_Icon_Damage"), Color.white, false);
            icon.preserveAspect = true;

            TextMeshProUGUI title = OutlinedLabel("Title", body, top, top, new Vector2(0f, -248f), new Vector2(s_CardSize.x - 40f, 70f),
                "НАЗВАНИЕ", 40f, TextAlignmentOptions.Center, Color.white, outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 40f;
            TextMeshProUGUI description = OutlinedLabel("Description", body, top, top, new Vector2(0f, -325f),
                new Vector2(s_CardSize.x - 50f, 140f), "Описание", 31f, TextAlignmentOptions.Top, s_SubtitleColor, outline);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 22f;
            description.fontSizeMax = 31f;
            TextMeshProUGUI level = OutlinedLabel("Level", body, bottom, bottom, new Vector2(0f, 34f), new Vector2(s_CardSize.x - 40f, 46f),
                "НОВОЕ", 28f, TextAlignmentOptions.Center, UiFactory.AccentColor, outline);
            level.enableAutoSizing = true;                           // the legendary card's line is long
            level.fontSizeMin = 16f;
            level.fontSizeMax = 28f;

            var topLeft = new Vector2(0f, 1f);
            RectTransform keyRt = UiFactory.Rect("Key", body, topLeft, topLeft, new Vector2(22f, -22f), new Vector2(60f, 60f));
            UiFactory.Image(keyRt, UiFactory.Sprite("UI_Button"), new Color(1f, 0.93f, 0.8f, 1f), true);
            TextMeshProUGUI key = UiFactory.Label("Number", keyRt, center, center, Vector2.zero, new Vector2(60f, 60f), "1", 38f,
                TextAlignmentOptions.Center, new Color(0.18f, 0.12f, 0.08f, 1f));

            RectTransform shineClip = UiFactory.Stretch("LegendaryShine", body);
            shineClip.offsetMin = new Vector2(8f, 8f);
            shineClip.offsetMax = new Vector2(-8f, -8f);
            shineClip.gameObject.AddComponent<RectMask2D>();
            RectTransform shine = UiFactory.Rect("Shine", shineClip, center, center, Vector2.zero, new Vector2(140f, 900f));
            shine.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var shineImage = shine.gameObject.AddComponent<RawImage>();
            shineImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/T_BulletGlow.png");
            shineImage.color = new Color(1f, 0.95f, 0.75f, 0.55f);
            shineImage.raycastTarget = false;
            shineClip.gameObject.SetActive(false);

            // The legendary card for an ad: the shared ad disc in the top right corner.
            var topRight = new Vector2(1f, 1f);
            RectTransform adRt = UiFactory.Rect("AdBadge", body, topRight, topRight, new Vector2(-18f, -18f), new Vector2(96f, 96f));
            UiFactory.Image(adRt, UiFactory.Sprite("UI_Icon_Ad"), Color.white, false).preserveAspect = true;
            adRt.gameObject.SetActive(false);

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;
            card.gameObject.AddComponent<UIButtonFx>();

            var view = card.gameObject.AddComponent<PerkCardView>();
            PrefabBuilder.Wire(view, ("m_Button", button), ("m_Body", body), ("m_Group", group), ("m_IconDisc", discImage),
                ("m_Icon", icon), ("m_Title", title), ("m_Description", description), ("m_Level", level), ("m_KeyHint", key),
                ("m_Glow", glow), ("m_LegendaryHalo", halo), ("m_LegendaryRays", rays), ("m_LegendaryShineClip", shineClip.gameObject),
                ("m_LegendaryShine", shine), ("m_AdBadge", adRt.gameObject));
            return view;
        }

        // ------------------------------------------------------------------ game over
        private static readonly Vector2 s_GameOverWindow = new Vector2(1160f, 960f);
        private static readonly Color s_Gold = new Color(1f, 0.82f, 0.3f, 1f);
        private const float c_EndButtonStep = 390f;
        private const float c_CoinLineStep = 54f;

        /// <summary>
        /// The end-of-run window with two panels: victory (medal, the run's lines 2x2, the coins step by step with
        /// the total, "К КЕЙСАМ") and defeat (the wave reached over the wave line with the boss marks, waves to the next
        /// boss, the run in one line, "ВОСКРЕСНУТЬ ЗА РЕКЛАМУ"); "ЗАНОВО" and "В МЕНЮ" in both.
        /// </summary>
        private static GameOverScreen BuildGameOverScreen(RectTransform parent, Material outline)
        {
            RectTransform host = UiFactory.Stretch("GameOverScreen", parent);
            RectTransform root = UiFactory.Stretch("Root", host);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", root), null, new Color(0.02f, 0.01f, 0.01f, 0.72f), false, true);
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform window = UiFactory.Rect("Window", root, center, center, new Vector2(0f, -10f), s_GameOverWindow);
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            TextMeshProUGUI title = OutlinedLabel("Title", window, top, top, new Vector2(0f, -28f), new Vector2(700f, 100f), "ИГРА ОКОНЧЕНА",
                84f, TextAlignmentOptions.Center, new Color(1f, 0.38f, 0.3f, 1f), outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 50f;
            title.fontSizeMax = 84f;
            TextMeshProUGUI reason = OutlinedLabel("Reason", window, top, top, new Vector2(0f, -130f), new Vector2(620f, 50f),
                "МАШИНА УНИЧТОЖЕНА", 36f, TextAlignmentOptions.Center, s_SubtitleColor, outline);
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 24f;
            reason.fontSizeMax = 36f;

            // ---- victory
            RectTransform victory = UiFactory.Stretch("Victory", window);
            RectTransform medal = BuildMedal(victory, outline);
            var statLines = new CanvasGroup[4];
            statLines[0] = EndStat(victory, 0, "UI_Icon_Speed", "ВРЕМЯ", outline, out TextMeshProUGUI time);
            statLines[1] = EndStat(victory, 1, "UI_Icon_Skull", "ЗОМБИ", outline, out TextMeshProUGUI kills);
            statLines[2] = EndStat(victory, 2, "UI_Icon_Boss", "БОССЫ", outline, out TextMeshProUGUI bosses);
            statLines[3] = EndStat(victory, 3, "UI_Icon_Xp", "УРОВЕНЬ", outline, out TextMeshProUGUI level);
            string[] coinLabels = { "МОНЕТЫ ЗА ЗОМБИ", "МОНЕТЫ ЗА ВОЛНЫ", "ОСТАТОК ДЕТАЛЕЙ 0 →", "НАГРАДА ЗА ПОБЕДУ", "ИТОГО" };
            var coinLines = new CanvasGroup[coinLabels.Length];
            var coinValues = new TMP_Text[coinLabels.Length];
            for (int i = 0; i < coinLabels.Length; i++)
                coinLines[i] = CoinLine(victory, i, coinLabels[i], i == coinLabels.Length - 1, outline, out coinValues[i]);
            TMP_Text scrapLabel = coinLines[2].transform.Find("Label").GetComponent<TMP_Text>();
            var flying = new RectTransform[12];
            for (int i = 0; i < flying.Length; i++)
            {
                int line = i % 4;
                var home = new Vector2(360f - (i / 4) * 34f, -436f - line * c_CoinLineStep);
                flying[i] = CoinPicture($"FlyingCoin{i + 1}", victory, top, home, 40f);
                flying[i].gameObject.SetActive(false);
            }
            Button cases = HudButton("CasesButton", victory, bottom, new Vector2(-c_EndButtonStep, 40f), new Vector2(360f, 116f), "К КЕЙСАМ",
                46f, s_Gold, outline);
            var topRight = new Vector2(1f, 1f);
            Button leaderboard = HudButton("LeaderboardButton", victory, topRight, new Vector2(-36f, -36f), new Vector2(300f, 84f),
                "ТАБЛИЦА ЛИДЕРОВ", 26f, new Color(0.3f, 0.56f, 0.9f, 1f), outline);

            // ---- defeat
            RectTransform defeat = UiFactory.Stretch("Defeat", window);
            TextMeshProUGUI reached = OutlinedLabel("Reached", defeat, top, top, new Vector2(0f, -210f), new Vector2(1060f, 76f),
                "ДОШЁЛ ДО ВОЛНЫ 17 ИЗ 30", 58f, TextAlignmentOptions.Center, Color.white, outline);
            reached.enableAutoSizing = true;
            reached.fontSizeMin = 34f;
            reached.fontSizeMax = 58f;
            const float lineWidth = 960f;
            RectTransform waveLine = UiFactory.Rect("WaveLine", defeat, top, top, new Vector2(0f, -330f), new Vector2(lineWidth, 34f));
            UiFactory.Image(waveLine, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            Image waveFill = UiFactory.Fill(Inset(UiFactory.Stretch("Fill", waveLine), 5f), UiFactory.Sprite("UI_BarFillRed"),
                new Color(1f, 0.72f, 0.35f, 1f), 0.5f);
            var waves = DataAssetsBuilder.Load<WaveConfig>(DataAssetsBuilder.c_WaveConfig);
            int count = Mathf.Max(1, waves.WaveCount);
            int[] bossWaves = waves.Bosses.Where(b => b != null && b.Boss != null).Select(b => b.Wave).OrderBy(w => w).ToArray();
            var marks = new RectTransform[bossWaves.Length];
            for (int i = 0; i < bossWaves.Length; i++)
            {
                bool final = i == bossWaves.Length - 1;
                float size = final ? 88f : 66f;
                float x = -lineWidth * 0.5f + lineWidth * bossWaves[i] / count;
                RectTransform mark = UiFactory.Rect($"Boss{bossWaves[i]}", waveLine, center, center, new Vector2(x, 0f), new Vector2(size, size));
                mark.gameObject.AddComponent<CanvasGroup>();
                UiFactory.Image(mark, UiFactory.Sprite("UI_StickKnob"), final ? s_FinalBossColor : new Color(0.75f, 0.12f, 0.1f, 1f), false);
                RectTransform face = UiFactory.Rect("Icon", mark, center, center, Vector2.zero, new Vector2(size * 0.78f, size * 0.78f));
                UiFactory.Image(face, UiFactory.Sprite("UI_Icon_Boss"), Color.white, false).preserveAspect = true;
                OutlinedLabel("Wave", mark, bottom, top, new Vector2(0f, -6f), new Vector2(90f, 34f), bossWaves[i].ToString(), 28f,
                    TextAlignmentOptions.Center, Color.white, outline);
                marks[i] = mark;
            }
            TextMeshProUGUI toBoss = OutlinedLabel("ToBoss", defeat, top, top, new Vector2(0f, -432f), new Vector2(1060f, 62f),
                "ДО БОССА: 3 ВОЛНЫ", 46f, TextAlignmentOptions.Center, s_TitleColor, outline);
            // The run in three picture tiles (zombies killed, level, coins earned) and the parts exchanged for coins.
            var defeatTiles = new CanvasGroup[4];
            defeatTiles[0] = DefeatTile(defeat, -1, "UI_Icon_Skull", "УБИТО ЗОМБИ", Color.white, outline, out TextMeshProUGUI defeatKills);
            defeatTiles[1] = DefeatTile(defeat, 0, "UI_Icon_Xp", "УРОВЕНЬ", new Color(0.55f, 0.85f, 1f, 1f), outline,
                out TextMeshProUGUI defeatLevel);
            defeatTiles[2] = DefeatTile(defeat, 1, "UI_Icon_Coin", "МОНЕТЫ", s_Gold, outline, out TextMeshProUGUI defeatCoins);
            defeatTiles[3] = ScrapExchange(defeat, outline, out TextMeshProUGUI defeatScrap);
            // Revive for a rewarded ad: the shared gold ad button in the left bottom slot (the victory's cases slot).
            Button revive = AdButton("ReviveButton", defeat, bottom, new Vector2(-c_EndButtonStep, 40f), new Vector2(360f, 116f),
                "ВОСКРЕСНУТЬ", 40f, outline);

            // ---- ×2 coins for an ad (by the total / under the coins, placed by the screen) and the coin balance
            Button twice = AdButton("DoubleButton", window, top, new Vector2(380f, -636f), new Vector2(240f, 84f), "×2", 44f, outline);
            // A bright solid gold plate, not the dark ad plate: the one button that should beg to be pressed.
            ((Image)twice.targetGraphic).sprite = UiFactory.Sprite("UI_Button");
            ((Image)twice.targetGraphic).color = new Color(1f, 0.72f, 0.12f, 1f);
            TMP_Text twiceLabel = twice.transform.Find("Label").GetComponent<TMP_Text>();
            GameObject twiceIcon = twice.transform.Find("AdIcon").gameObject;
            TextMeshProUGUI balance = CoinCounter("Balance", window, bottom, bottom, new Vector2(0f, 172f), 56f, 240f, "0", 44f, outline);
            TextMeshProUGUI gain = OutlinedLabel("BalanceGain", window, bottom, bottom, new Vector2(190f, 176f), new Vector2(200f, 50f), "+0",
                40f, TextAlignmentOptions.Left, s_Gold, outline);

            // ---- shared buttons
            Button restart = HudButton("RestartButton", window, bottom, new Vector2(0f, 40f), new Vector2(360f, 116f), "ЗАНОВО", 50f,
                s_PlayColor, outline);
            Button menu = HudButton("MenuButton", window, bottom, new Vector2(c_EndButtonStep, 40f), new Vector2(360f, 116f), "В МЕНЮ", 50f,
                Color.white, outline);

            var screen = host.gameObject.AddComponent<GameOverScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_Root", group), ("m_Window", window), ("m_Title", title), ("m_Reason", reason),
                ("m_RestartButton", restart), ("m_MenuButton", menu),
                ("m_VictoryPanel", victory.gameObject), ("m_Medal", medal), ("m_TimeValue", time), ("m_KillsValue", kills),
                ("m_BossesValue", bosses), ("m_LevelValue", level), ("m_CasesButton", cases), ("m_LeaderboardButton", leaderboard),
                ("m_DefeatPanel", defeat.gameObject), ("m_Reached", reached), ("m_WaveFill", waveFill), ("m_ToBoss", toBoss),
                ("m_DefeatKills", defeatKills), ("m_DefeatLevel", defeatLevel), ("m_DefeatCoins", defeatCoins), ("m_ReviveButton", revive), ("m_ScrapLabel", scrapLabel), ("m_DefeatScrap", defeatScrap),
                ("m_DoubleButton", twice), ("m_DoubleLabel", twiceLabel), ("m_DoubleIcon", twiceIcon), ("m_Balance", balance),
                ("m_BalanceGain", gain));
            CharacterBuilder.SetProps(screen, ("m_StatLines", statLines.Cast<Object>().ToArray()),
                ("m_DefeatTiles", defeatTiles.Cast<Object>().ToArray()),
                ("m_CoinLines", coinLines.Cast<Object>().ToArray()), ("m_CoinValues", coinValues.Cast<Object>().ToArray()),
                ("m_FlyingCoins", flying.Cast<Object>().ToArray()), ("m_BossMarks", marks.Cast<Object>().ToArray()));
            var so = new SerializedObject(screen);
            SerializedProperty list = so.FindProperty("m_BossWaves");
            list.arraySize = bossWaves.Length;
            for (int i = 0; i < bossWaves.Length; i++)
                list.GetArrayElementAtIndex(i).intValue = bossWaves[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            defeat.gameObject.SetActive(false);
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>The victory medal: a gold disc with "30 ВОЛН" and two ribbon tails, top left of the window.</summary>
        private static RectTransform BuildMedal(RectTransform parent, Material outline)
        {
            var topLeft = new Vector2(0f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            const float size = 170f;
            RectTransform medal = UiFactory.Rect("Medal", parent, topLeft, center, new Vector2(60f + size * 0.5f, -30f - size * 0.5f),
                new Vector2(size, size));
            for (int side = -1; side <= 1; side += 2)
            {
                RectTransform tail = UiFactory.Rect(side < 0 ? "RibbonL" : "RibbonR", medal, center, new Vector2(0.5f, 1f),
                    new Vector2(side * 34f, -40f), new Vector2(48f, 110f));
                tail.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
                UiFactory.Image(tail, UiFactory.Sprite("UI_Panel"), side < 0 ? new Color(0.85f, 0.2f, 0.18f, 1f) : new Color(0.2f, 0.45f, 0.9f, 1f), true);
            }
            UiFactory.Image(UiFactory.Stretch("Disc", medal), UiFactory.Sprite("UI_StickKnob"), s_Gold, false);
            RectTransform inner = UiFactory.Rect("Inner", medal, center, center, Vector2.zero, new Vector2(size * 0.76f, size * 0.76f));
            UiFactory.Image(inner, UiFactory.Sprite("UI_StickKnob"), new Color(0.85f, 0.55f, 0.12f, 1f), false);
            OutlinedLabel("Number", medal, center, center, new Vector2(0f, 14f), new Vector2(size, 80f), "30", 72f,
                TextAlignmentOptions.Center, Color.white, outline);
            OutlinedLabel("Caption", medal, center, center, new Vector2(0f, -38f), new Vector2(size, 34f), "ВОЛН", 28f,
                TextAlignmentOptions.Center, Color.white, outline);
            return medal;
        }

        /// <summary>One of the victory's 2x2 run lines: icon, label and value on a dark plate.</summary>
        /// <summary>A defeat tile: the picture on the left, the number big, what it counts under it.</summary>
        private static CanvasGroup DefeatTile(RectTransform parent, int column, string icon, string caption, Color valueColor, Material outline,
            out TextMeshProUGUI value)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            var bottomLeft = new Vector2(0f, 0f);
            RectTransform tile = UiFactory.Rect($"Tile{column + 2}", parent, top, top, new Vector2(column * 330f, -508f), new Vector2(310f, 96f));
            var group = tile.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(tile, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.65f), true);
            RectTransform iconRt = UiFactory.Rect("Icon", tile, midLeft, midLeft, new Vector2(14f, 0f), new Vector2(70f, 70f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            value = OutlinedLabel("Value", tile, topLeft, topLeft, new Vector2(98f, -6f), new Vector2(200f, 54f), "0", 46f,
                TextAlignmentOptions.Left, valueColor, outline);
            value.enableAutoSizing = true;
            value.fontSizeMin = 24f;
            value.fontSizeMax = 46f;
            TextMeshProUGUI label = OutlinedLabel("Caption", tile, bottomLeft, bottomLeft, new Vector2(98f, 8f), new Vector2(200f, 30f), caption,
                22f, TextAlignmentOptions.Left, s_SubtitleColor, outline);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 22f;
            return group;
        }

        /// <summary>The parts left exchanged: "ДЕТАЛИ", the parts icon, "59 → +11", the coin icon, on one pill.</summary>
        private static CanvasGroup ScrapExchange(RectTransform parent, Material outline, out TextMeshProUGUI value)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform pill = UiFactory.Rect("ScrapExchange", parent, top, top, new Vector2(0f, -616f), new Vector2(560f, 58f));
            var group = pill.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(pill, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.65f), true);
            OutlinedLabel("Caption", pill, midLeft, midLeft, new Vector2(18f, 0f), new Vector2(150f, 58f), "ДЕТАЛИ", 24f,
                TextAlignmentOptions.Left, s_SubtitleColor, outline);
            RectTransform scrap = UiFactory.Rect("ScrapIcon", pill, midLeft, center, new Vector2(170f, 0f), new Vector2(46f, 46f));
            UiFactory.Image(scrap, UiFactory.Sprite("UI_Icon_Scrap"), Color.white, false).preserveAspect = true;
            value = OutlinedLabel("Scrap", pill, midLeft, midLeft, new Vector2(200f, 0f), new Vector2(290f, 58f), "0 → +0", 34f,
                TextAlignmentOptions.Center, s_Gold, outline);
            value.enableAutoSizing = true;
            value.fontSizeMin = 20f;
            value.fontSizeMax = 34f;
            RectTransform coin = UiFactory.Rect("CoinIcon", pill, midRight, center, new Vector2(-40f, 0f), new Vector2(46f, 46f));
            UiFactory.Image(coin, UiFactory.Sprite("UI_Icon_Coin"), Color.white, false).preserveAspect = true;
            return group;
        }

        private static CanvasGroup EndStat(RectTransform parent, int index, string icon, string label, Material outline, out TextMeshProUGUI value)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            float x = index % 2 == 0 ? -272f : 272f;
            float y = -222f - (index / 2) * 98f;
            RectTransform row = UiFactory.Rect($"Stat{index + 1}", parent, top, top, new Vector2(x, y), new Vector2(528f, 86f));
            var group = row.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(row, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.6f), true);
            RectTransform iconRt = UiFactory.Rect("Icon", row, midLeft, midLeft, new Vector2(14f, 0f), new Vector2(62f, 62f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            OutlinedLabel("Label", row, midLeft, midLeft, new Vector2(88f, 0f), new Vector2(200f, 70f), label, 32f,
                TextAlignmentOptions.Left, s_SubtitleColor, outline);
            value = OutlinedLabel("Value", row, midRight, midRight, new Vector2(-20f, 0f), new Vector2(240f, 70f), "0", 44f,
                TextAlignmentOptions.Right, Color.white, outline);
            value.enableAutoSizing = true;
            value.fontSizeMin = 22f;
            value.fontSizeMax = 44f;
            return group;
        }

        /// <summary>A coin line of the victory: label left, "+N" with a coin right; the total is bigger and gold.</summary>
        private static CanvasGroup CoinLine(RectTransform parent, int index, string label, bool total, Material outline, out TMP_Text value)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            float height = total ? 84f : 48f;
            float y = total ? -636f : -412f - index * c_CoinLineStep;
            // The total is shorter: the ×2 button sits to its right.
            RectTransform row = UiFactory.Rect(total ? "Total" : $"Coins{index + 1}", parent, top, top, new Vector2(total ? -130f : 0f, y),
                new Vector2(total ? 740f : 1000f, height));
            var group = row.gameObject.AddComponent<CanvasGroup>();
            if (total)
                UiFactory.Image(row, UiFactory.Sprite("UI_Panel"), new Color(0.3f, 0.2f, 0.04f, 0.75f), true);
            OutlinedLabel("Label", row, midLeft, midLeft, new Vector2(24f, 0f), new Vector2(600f, height), label, total ? 48f : 34f,
                TextAlignmentOptions.Left, total ? s_Gold : s_SubtitleColor, outline);
            TextMeshProUGUI text = OutlinedLabel("Value", row, midRight, midRight, new Vector2(-84f, 0f), new Vector2(360f, height), "+0",
                total ? 60f : 38f, TextAlignmentOptions.Right, s_Gold, outline);
            CoinPicture("Coin", row, midRight, new Vector2(-40f, 0f), total ? 56f : 40f);
            value = text;
            return group;
        }

        /// <summary>A coin: the shared gold coin icon (UI_Icon_Coin).</summary>
        private static RectTransform CoinPicture(string name, RectTransform parent, Vector2 anchor, Vector2 pos, float size)
        {
            var center = new Vector2(0.5f, 0.5f);
            RectTransform coin = UiFactory.Rect(name, parent, anchor, center, pos, new Vector2(size, size));
            UiFactory.Image(coin, UiFactory.Sprite("UI_Icon_Coin"), Color.white, false).preserveAspect = true;
            return coin;
        }

        // ------------------------------------------------------------------ boss intro
        private struct BossMove
        {
            public string Title, Text, Fx;
            public int Pose;
            public Color FxColor;
            public bool SecondPhase;
        }

        private struct BossIntro
        {
            public string Name, Sheet;
            public Color Accent;
            public BossMove[] Moves;
        }

        private static readonly Color s_AcidFx = new Color(0.55f, 1f, 0.3f, 0.9f);
        private static readonly Color s_PurpleFx = new Color(0.8f, 0.55f, 1f, 0.9f);
        private static readonly Color s_FireFx = new Color(1f, 0.55f, 0.15f, 0.95f);

        // Poses: 0 idle, 6 wind-up, 7 blow (the painted sheets' frame order).
        private static readonly BossIntro[] s_BossIntros =
        {
            new BossIntro
            {
                Name = "МАТКА", Sheet = "Boss1", Accent = new Color(0.55f, 0.95f, 0.35f, 1f),
                Moves = new[]
                {
                    new BossMove { Title = "ВЫВОДОК", Pose = 6, Fx = "FX_Aura", FxColor = s_AcidFx,
                        Text = "Зовёт зомби вокруг себя.\nСначала руби мелочь." },
                    new BossMove { Title = "КИСЛОТА", Pose = 7, Fx = "FX_AcidSplat", FxColor = s_AcidFx,
                        Text = "Плюётся кислотой, остаются лужи.\nНе стой в зелёном." },
                    new BossMove { Title = "РОЙ", Pose = 0, Fx = "FX_ComicBurst", FxColor = s_AcidFx, SecondPhase = true,
                        Text = "Со злости зовёт чаще,\nвместе с гончими." },
                },
            },
            new BossIntro
            {
                Name = "ТАРАН", Sheet = "Boss2", Accent = new Color(0.8f, 0.5f, 1f, 1f),
                Moves = new[]
                {
                    new BossMove { Title = "РЫВОК", Pose = 7, Fx = "FX_ChargeLine", FxColor = s_PurpleFx,
                        Text = "Рисует линию и несётся по ней.\nОтойди — потом он оглушён, бей!" },
                    new BossMove { Title = "КИСЛОТНЫЙ ДОЖДЬ", Pose = 6, Fx = "FX_AcidBall", FxColor = s_AcidFx,
                        Text = "Капли падают вокруг тебя.\nУходи из кругов." },
                    new BossMove { Title = "ЗЕМЛЕТРЯСЕНИЕ", Pose = 7, Fx = "FX_ShockRing", FxColor = s_PurpleFx, SecondPhase = true,
                        Text = "Бьёт о землю — волна вокруг.\nДержи дистанцию." },
                },
            },
            new BossIntro
            {
                Name = "ЖНЕЦ", Sheet = "Boss3", Accent = new Color(1f, 0.55f, 0.2f, 1f),
                Moves = new[]
                {
                    new BossMove { Title = "КОСА", Pose = 7, Fx = "FX_Slash", FxColor = s_FireFx,
                        Text = "Бьёт дугой перед собой\nи отбрасывает. Не стой рядом." },
                    new BossMove { Title = "ОГНЕННЫЕ ТРЕЩИНЫ", Pose = 6, Fx = "FX_Scorch", FxColor = s_FireFx,
                        Text = "Земля горит по линиям.\nУходи с линий, пока горят." },
                    new BossMove { Title = "ЖАТВА", Pose = 0, Fx = "FX_TargetRing", FxColor = s_FireFx, SecondPhase = true,
                        Text = "Ставит метку и прыгает к тебе.\nБеги от метки!" },
                },
            },
        };

        private const int c_BossMoveCount = 3;
        private static readonly Vector2 s_BossCardSize = new Vector2(440f, 560f);
        private const float c_BossCardSpacing = 480f;
        private static readonly Color s_BossCardPlate = new Color(0.07f, 0.05f, 0.06f, 0.97f);

        /// <summary>
        /// The screen before every boss fight: header, the boss's name, how it picks targets, three dark ability cards
        /// (a framed picture of the move over its effect, the move's name in the boss's colour, what it does, a "2 ФАЗА"
        /// badge for the enraged moves) and "В БОЙ!". Deliberately unlike the light perk cards with their round icons.
        /// </summary>
        private static void BuildBossIntro(RectTransform parent, Material outline)
        {
            RectTransform root = UiFactory.Stretch("BossIntro", parent);
            UiFactory.Image(root, null, new Color(0.03f, 0f, 0f, 0.94f), false, true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);

            RectTransform head = UiFactory.Rect("Head", root, top, top, new Vector2(0f, -30f), new Vector2(1400f, 230f));
            TextMeshProUGUI header = OutlinedLabel("Header", head, top, top, new Vector2(0f, -8f), new Vector2(1400f, 50f),
                "БОСС  •  ВОЛНА 10", 36f, TextAlignmentOptions.Center, s_SubtitleColor, outline);
            TextMeshProUGUI name = OutlinedLabel("Name", head, top, top, new Vector2(0f, -56f), new Vector2(1400f, 116f), "МАТКА",
                110f, TextAlignmentOptions.Center, Color.white, outline);
            OutlinedLabel("Targets", head, top, top, new Vector2(0f, -176f), new Vector2(1400f, 44f),
                "Идёт к машине. Подойдёшь близко — переключится на тебя!", 30f, TextAlignmentOptions.Center, s_TitleColor, outline);

            var cards = new BossIntroView.Card[c_BossMoveCount];
            for (int i = 0; i < c_BossMoveCount; i++)
                cards[i] = BuildBossMoveCard(root, i, outline);

            Button fight = HudButton("FightButton", root, bottom, new Vector2(0f, 30f), new Vector2(400f, 104f), "В БОЙ!", 54f,
                s_PlayColor, outline);
            ((RectTransform)fight.transform).pivot = bottom;

            var view = root.gameObject.AddComponent<BossIntroView>();
            PrefabBuilder.Wire(view, ("m_Waves", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)), ("m_Group", group),
                ("m_Head", head), ("m_Header", header), ("m_Title", name), ("m_FightButton", fight));
            var so = new SerializedObject(view);
            SerializedProperty cardList = so.FindProperty("m_Cards");
            cardList.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty c = cardList.GetArrayElementAtIndex(i);
                c.FindPropertyRelative("Root").objectReferenceValue = cards[i].Root;
                c.FindPropertyRelative("Group").objectReferenceValue = cards[i].Group;
                c.FindPropertyRelative("Frame").objectReferenceValue = cards[i].Frame;
                c.FindPropertyRelative("PictureBack").objectReferenceValue = cards[i].PictureBack;
                c.FindPropertyRelative("Fx").objectReferenceValue = cards[i].Fx;
                c.FindPropertyRelative("Pose").objectReferenceValue = cards[i].Pose;
                c.FindPropertyRelative("Title").objectReferenceValue = cards[i].Title;
                c.FindPropertyRelative("Text").objectReferenceValue = cards[i].Text;
                c.FindPropertyRelative("PhaseBadge").objectReferenceValue = cards[i].PhaseBadge;
            }
            SerializedProperty list = so.FindProperty("m_Bosses");
            list.arraySize = s_BossIntros.Length;
            for (int i = 0; i < s_BossIntros.Length; i++)
            {
                BossIntro b = s_BossIntros[i];
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Name").stringValue = b.Name;
                e.FindPropertyRelative("Accent").colorValue = b.Accent;
                SerializedProperty moves = e.FindPropertyRelative("Abilities");
                moves.arraySize = b.Moves.Length;
                for (int m = 0; m < b.Moves.Length; m++)
                {
                    BossMove move = b.Moves[m];
                    SerializedProperty a = moves.GetArrayElementAtIndex(m);
                    a.FindPropertyRelative("Title").stringValue = move.Title;
                    a.FindPropertyRelative("Text").stringValue = move.Text;
                    a.FindPropertyRelative("Pose").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(
                        $"{EditorPaths.c_CharacterTextures}/{b.Sheet}/{b.Sheet}_{move.Pose}.png");
                    a.FindPropertyRelative("Fx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/VFX/{move.Fx}.png");
                    a.FindPropertyRelative("FxColor").colorValue = move.FxColor;
                    a.FindPropertyRelative("SecondPhase").boolValue = move.SecondPhase;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>One ability card: accent frame, dark plate, the picture window (effect behind the boss's pose), name, text.</summary>
        private static BossIntroView.Card BuildBossMoveCard(RectTransform parent, int index, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var topRight = new Vector2(1f, 1f);
            float x = (index - (c_BossMoveCount - 1) * 0.5f) * c_BossCardSpacing;
            RectTransform card = UiFactory.Rect($"Move{index + 1}", parent, center, center, new Vector2(x, -40f), s_BossCardSize);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            Image frame = UiFactory.Image(UiFactory.Stretch("Frame", card), UiFactory.Sprite("UI_Panel"), Color.red, true);
            RectTransform plateRt = UiFactory.Stretch("Plate", card);
            plateRt.offsetMin = new Vector2(8f, 8f);
            plateRt.offsetMax = new Vector2(-8f, -8f);
            UiFactory.Image(plateRt, UiFactory.Sprite("UI_Panel"), s_BossCardPlate, true);

            RectTransform picture = UiFactory.Rect("Picture", plateRt, top, top, new Vector2(0f, -14f), new Vector2(396f, 290f));
            Image pictureBack = UiFactory.Image(picture, UiFactory.Sprite("UI_Panel"), Color.gray, true);
            picture.gameObject.AddComponent<RectMask2D>();
            RectTransform fxRt = UiFactory.Rect("Fx", picture, center, center, new Vector2(0f, -20f), new Vector2(380f, 380f));
            var fx = fxRt.gameObject.AddComponent<RawImage>();
            fx.raycastTarget = false;
            RectTransform poseRt = UiFactory.Rect("Pose", picture, bottom, bottom, new Vector2(0f, -6f), new Vector2(300f, 290f));
            Image pose = UiFactory.Image(poseRt, null, Color.white, false);
            pose.preserveAspect = true;

            TextMeshProUGUI title = OutlinedLabel("Title", plateRt, top, top, new Vector2(0f, -318f), new Vector2(400f, 62f), "НАЗВАНИЕ",
                42f, TextAlignmentOptions.Center, Color.white, outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 42f;
            TextMeshProUGUI text = OutlinedLabel("Text", plateRt, top, top, new Vector2(0f, -390f), new Vector2(396f, 140f), "Описание",
                30f, TextAlignmentOptions.Top, Color.white, outline);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 30f;

            RectTransform badge = UiFactory.Rect("PhaseBadge", card, topRight, topRight, new Vector2(10f, 14f), new Vector2(170f, 52f));
            badge.localRotation = Quaternion.Euler(0f, 0f, -8f);
            UiFactory.Image(badge, UiFactory.Sprite("UI_Button"), new Color(0.85f, 0.12f, 0.1f, 1f), true);
            OutlinedLabel("Label", badge, center, center, Vector2.zero, new Vector2(170f, 52f), "2 ФАЗА", 30f, TextAlignmentOptions.Center,
                Color.white, outline);

            return new BossIntroView.Card
            {
                Root = card, Group = group, Frame = frame, PictureBack = pictureBack, Fx = fx, Pose = pose, Title = title, Text = text,
                PhaseBadge = badge.gameObject,
            };
        }

        // ------------------------------------------------------------------ leaderboard
        private const int c_LeaderboardRows = 10;
        private static readonly Vector2 s_LeaderboardWindow = new Vector2(1100f, 1030f);
        private const float c_LeaderboardRowHeight = 56f;

        /// <summary>
        /// "ТАБЛИЦА ЛИДЕРОВ" (hidden until opened): a window with the top-10 rows (place, avatar, name, time), the
        /// player's own row under them, a status line (loading / nothing to show) and "ЗАКРЫТЬ". Main menu and HUD.
        /// </summary>
        private static LeaderboardPanel BuildLeaderboardPanel(Transform parent, Material outline)
        {
            RectTransform root = UiFactory.Stretch("LeaderboardPanel", parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.7f), false, true);            // dims and blocks clicks behind
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, s_LeaderboardWindow);
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            OutlinedLabel("Title", window, top, top, new Vector2(0f, -24f), new Vector2(1000f, 76f), "ТАБЛИЦА ЛИДЕРОВ", 60f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            // One table per difficulty: the tabs under the title (the chosen one in its colour).
            var tabs = new Button[DifficultyRules.Count];
            var tabPlates = new Image[DifficultyRules.Count];
            for (int i = 0; i < DifficultyRules.Count; i++)
            {
                var difficulty = (Difficulty)i;
                RectTransform tab = UiFactory.Rect($"Tab{DifficultyRules.Title(difficulty)}", window, top, top,
                    new Vector2((i - 1) * 320f, -100f), new Vector2(300f, 64f));
                tabPlates[i] = UiFactory.Image(tab, UiFactory.Sprite("UI_Button"), new Color(0.16f, 0.17f, 0.2f, 0.95f), true, true);
                OutlinedLabel("Label", tab, center, center, Vector2.zero, new Vector2(290f, 60f), DifficultyRules.Title(difficulty), 32f,
                    TextAlignmentOptions.Center, s_ButtonLabel, outline);
                tabs[i] = tab.gameObject.AddComponent<Button>();
                tabs[i].targetGraphic = tabPlates[i];
                tab.gameObject.AddComponent<UIButtonFx>();
            }

            var rows = new LeaderboardPanel.Row[c_LeaderboardRows];
            for (int i = 0; i < c_LeaderboardRows; i++)
                rows[i] = LeaderboardRow(window, $"Row{i + 1}", -172f - i * (c_LeaderboardRowHeight + 6f), outline);
            LeaderboardPanel.Row player = LeaderboardRow(window, "PlayerRow", -172f - c_LeaderboardRows * (c_LeaderboardRowHeight + 6f) - 14f,
                outline);
            TextMeshProUGUI status = OutlinedLabel("Status", window, center, center, new Vector2(0f, 20f), new Vector2(960f, 220f), "ЗАГРУЗКА…",
                44f, TextAlignmentOptions.Center, Color.white, outline);
            status.enableAutoSizing = true;
            status.fontSizeMin = 26f;
            status.fontSizeMax = 44f;
            Button close = HudButton("CloseButton", window, bottom, new Vector2(0f, 24f), new Vector2(360f, 96f), "ЗАКРЫТЬ", 44f,
                s_PlayColor, outline);

            var panel = root.gameObject.AddComponent<LeaderboardPanel>();
            PrefabBuilder.Wire(panel, ("m_Group", group), ("m_Window", window), ("m_Status", status), ("m_CloseButton", close),
                ("m_AvatarPlaceholder", UiFactory.Sprite("UI_Icon_Skull")));
            CharacterBuilder.SetProps(panel, ("m_Tabs", tabs), ("m_TabPlates", tabPlates));
            var so = new SerializedObject(panel);
            SerializedProperty list = so.FindProperty("m_Rows");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
                WriteRow(list.GetArrayElementAtIndex(i), rows[i]);
            WriteRow(so.FindProperty("m_PlayerRow"), player);
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (LeaderboardPanel.Row row in rows)
                row.Root.SetActive(false);
            player.Root.SetActive(false);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static LeaderboardPanel.Row LeaderboardRow(RectTransform window, string name, float y, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            RectTransform rt = UiFactory.Rect(name, window, top, top, new Vector2(0f, y), new Vector2(s_LeaderboardWindow.x - 100f, c_LeaderboardRowHeight));
            var row = new LeaderboardPanel.Row { Root = rt.gameObject };
            row.Plate = UiFactory.Image(rt, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.6f), true);
            row.Rank = OutlinedLabel("Rank", rt, midLeft, midLeft, new Vector2(12f, 0f), new Vector2(76f, 52f), "1", 36f,
                TextAlignmentOptions.Center, Color.white, outline);
            RectTransform avatar = UiFactory.Rect("Avatar", rt, midLeft, midLeft, new Vector2(98f, 0f), new Vector2(46f, 46f));
            row.Avatar = UiFactory.Image(avatar, UiFactory.Sprite("UI_Icon_Skull"), Color.white, false);
            row.Avatar.preserveAspect = true;
            TextMeshProUGUI playerName = OutlinedLabel("Name", rt, midLeft, midLeft, new Vector2(160f, 0f), new Vector2(560f, 52f), "ИГРОК", 32f,
                TextAlignmentOptions.Left, Color.white, outline);
            playerName.enableAutoSizing = true;
            playerName.fontSizeMin = 20f;
            playerName.fontSizeMax = 32f;
            row.Name = playerName;
            row.Time = OutlinedLabel("Time", rt, midRight, midRight, new Vector2(-20f, 0f), new Vector2(200f, 52f), "00:00", 36f,
                TextAlignmentOptions.Right, s_Gold, outline);
            return row;
        }

        private static void WriteRow(SerializedProperty p, LeaderboardPanel.Row row)
        {
            p.FindPropertyRelative("Root").objectReferenceValue = row.Root;
            p.FindPropertyRelative("Plate").objectReferenceValue = row.Plate;
            p.FindPropertyRelative("Rank").objectReferenceValue = row.Rank;
            p.FindPropertyRelative("Avatar").objectReferenceValue = row.Avatar;
            p.FindPropertyRelative("Name").objectReferenceValue = row.Name;
            p.FindPropertyRelative("Time").objectReferenceValue = row.Time;
        }

        // ------------------------------------------------------------------ victory finale
        /// <summary>"ГОРОД СПАСЁН!" huge in the middle with the run in a line under it, and the fireworks layer.</summary>
        private static void BuildVictoryFinale(RectTransform parent, Material outline)
        {
            RectTransform host = UiFactory.Stretch("VictoryFinale", parent);
            UiFireworks fireworks = BuildFireworks(host);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform textRoot = UiFactory.Stretch("Text", host);
            var group = textRoot.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            TextMeshProUGUI big = OutlinedLabel("Saved", textRoot, center, center, new Vector2(0f, 90f), new Vector2(1700f, 200f), "ГОРОД СПАСЁН!",
                150f, TextAlignmentOptions.Center, s_Gold, outline);
            big.enableAutoSizing = true;
            big.fontSizeMin = 80f;
            big.fontSizeMax = 150f;
            TextMeshProUGUI subtitle = OutlinedLabel("Subtitle", textRoot, center, center, new Vector2(0f, -40f), new Vector2(1500f, 64f),
                "30 ВОЛН  •  3 БОССА", 46f, TextAlignmentOptions.Center, Color.white, outline);
            PrefabBuilder.Wire(host.gameObject.AddComponent<VictoryFinaleView>(), ("m_Run", Run), ("m_Group", group),
                ("m_Text", big.rectTransform), ("m_Subtitle", subtitle), ("m_Fireworks", fireworks));
        }

        /// <summary>A fireworks layer over the whole of <paramref name="parent"/> (victory finale, a legendary skin).</summary>
        private static UiFireworks BuildFireworks(RectTransform parent)
        {
            RectTransform sky = UiFactory.Stretch("Fireworks", parent);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform spark = UiFactory.Rect("Spark", sky, center, center, Vector2.zero, new Vector2(22f, 22f));
            Image sparkImage = UiFactory.Image(spark, UiFactory.Sprite("UI_Spark"), Color.white, false);
            var fireworks = sky.gameObject.AddComponent<UiFireworks>();
            PrefabBuilder.Wire(fireworks, ("m_Sky", sky), ("m_SparkTemplate", sparkImage));
            return fireworks;
        }

        // ------------------------------------------------------------------ workshop
        private const int c_WorkshopRows = 5;            // car upgrades (left column)
        private const int c_SupplyRows = 3;              // supplies (right column)
        private static readonly Vector2 s_WorkshopWindow = new Vector2(1600f, 1000f);
        private static readonly Vector2 s_WorkshopRow = new Vector2(740f, 118f);
        private const float c_WorkshopRowsTop = -236f;

        private static WorkshopScreen BuildWorkshopScreen(RectTransform parent, Material outline)
        {
            RectTransform host = UiFactory.Stretch("WorkshopScreen", parent);
            RectTransform root = UiFactory.Stretch("Root", host);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", root), null, s_DimColor, false, true);
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var topRight = new Vector2(1f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, s_WorkshopWindow);
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            OutlinedLabel("Title", window, top, top, new Vector2(0f, -22f), new Vector2(900f, 80f), "МАСТЕРСКАЯ", 64f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            OutlinedLabel("Subtitle", window, top, top, new Vector2(0f, -98f), new Vector2(900f, 42f), "ПРОКАЧКА МАШИНЫ ЗА МЕТАЛЛОЛОМ", 30f,
                TextAlignmentOptions.Center, s_SubtitleColor, outline);

            // Scrap the player has (top right of the window).
            RectTransform moneyRt = UiFactory.Rect("Money", window, topRight, topRight, new Vector2(-40f, -34f), new Vector2(260f, 76f));
            UiFactory.Image(moneyRt, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform moneyIcon = UiFactory.Rect("Icon", moneyRt, midLeft, midLeft, new Vector2(10f, 0f), new Vector2(58f, 58f));
            UiFactory.Image(moneyIcon, UiFactory.Sprite("UI_Icon_Scrap"), Color.white, false).preserveAspect = true;
            TextMeshProUGUI money = OutlinedLabel("Value", moneyRt, midLeft, midLeft, new Vector2(80f, 0f), new Vector2(170f, 76f), "0", 44f,
                TextAlignmentOptions.Left, Color.white, outline);

            TextMeshProUGUI status = OutlinedLabel("CarStatus", window, top, top, new Vector2(0f, -142f), new Vector2(1400f, 42f),
                "ПРОЧНОСТЬ 400 / 400", 30f, TextAlignmentOptions.Center, new Color(0.62f, 0.86f, 1f, 1f), outline);

            // Left: the car upgrades (master levels after the last); right: the supplies, bought again and again.
            float leftX = -0.5f * (s_WorkshopRow.x + 24f), rightX = -leftX;
            OutlinedLabel("UpgradesHeader", window, top, top, new Vector2(leftX, -190f), new Vector2(s_WorkshopRow.x, 40f), "УЛУЧШЕНИЯ МАШИНЫ",
                32f, TextAlignmentOptions.Center, s_TitleColor, outline);
            OutlinedLabel("SuppliesHeader", window, top, top, new Vector2(rightX, -190f), new Vector2(s_WorkshopRow.x, 40f),
                "РАСХОДНИКИ (МЕЖДУ ВОЛНАМИ)", 32f, TextAlignmentOptions.Center, s_TitleColor, outline);
            var rows = new WorkshopRowView[c_WorkshopRows];
            for (int i = 0; i < c_WorkshopRows; i++)
                rows[i] = BuildWorkshopRow(window, $"Row{i + 1}", new Vector2(leftX, c_WorkshopRowsTop - i * (s_WorkshopRow.y + 8f)), outline);
            var supplyRows = new WorkshopRowView[c_SupplyRows];
            for (int i = 0; i < c_SupplyRows; i++)
                supplyRows[i] = BuildWorkshopRow(window, $"Supply{i + 1}", new Vector2(rightX, c_WorkshopRowsTop - i * (s_WorkshopRow.y + 8f)),
                    outline);

            Button close = HudButton("DoneButton", window, bottom, new Vector2(0f, 24f), new Vector2(360f, 96f), "ГОТОВО", 44f,
                s_PlayColor, outline);
            OutlinedLabel("Hint", window, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 50f), new Vector2(460f, 40f),
                "E / ESC: ЗАКРЫТЬ", 26f, TextAlignmentOptions.Right, new Color(0.6f, 0.58f, 0.55f, 1f), outline);

            var screen = host.gameObject.AddComponent<WorkshopScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Catalog", DataAssetsBuilder.Load<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades)),
                ("m_Car", DataAssetsBuilder.Load<CarState>(DataAssetsBuilder.c_CarState)), ("m_Root", group), ("m_Window", window),
                ("m_Money", money), ("m_CarStatus", status), ("m_CloseButton", close),
                ("m_Player", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Ammo", DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState)));
            CharacterBuilder.SetProps(screen, ("m_Rows", rows), ("m_SupplyRows", supplyRows));
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        private static WorkshopRowView BuildWorkshopRow(RectTransform window, string name, Vector2 pos, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform row = UiFactory.Rect(name, window, top, top, pos, s_WorkshopRow);
            RectTransform body = UiFactory.Stretch("Body", row);
            UiFactory.Image(body, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.55f), true);

            RectTransform disc = UiFactory.Rect("IconDisc", body, midLeft, midLeft, new Vector2(12f, 0f), new Vector2(98f, 98f));
            UiFactory.Image(disc, UiFactory.Sprite("UI_StickKnob"), new Color(0.3f, 0.56f, 0.9f, 1f), false);
            RectTransform iconRt = UiFactory.Rect("Icon", disc, center, center, Vector2.zero, new Vector2(78f, 78f));
            Image icon = UiFactory.Image(iconRt, UiFactory.Sprite("UI_Icon_Armor"), Color.white, false);
            icon.preserveAspect = true;

            TextMeshProUGUI title = OutlinedLabel("Title", body, topLeft, topLeft, new Vector2(124f, -6f), new Vector2(400f, 40f), "БРОНЯ",
                32f, TextAlignmentOptions.Left, Color.white, outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 22f;
            title.fontSizeMax = 32f;
            TextMeshProUGUI description = OutlinedLabel("Description", body, topLeft, topLeft, new Vector2(124f, -44f),
                new Vector2(420f, 40f), "Описание", 23f, TextAlignmentOptions.TopLeft, s_SubtitleColor, outline);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 16f;
            description.fontSizeMax = 23f;

            const int pipCount = 8;                                   // the deepest upgrade's levels
            var pips = new Image[pipCount];
            for (int i = 0; i < pipCount; i++)
            {
                RectTransform pip = UiFactory.Rect($"Pip{i + 1}", body, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(126f + i * 30f, 8f), new Vector2(24f, 24f));
                pips[i] = UiFactory.Image(pip, UiFactory.Sprite("UI_StickKnob"), Color.gray, false);
            }
            TextMeshProUGUI count = OutlinedLabel("Count", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(124f, 4f),
                new Vector2(420f, 32f), string.Empty, 24f, TextAlignmentOptions.Left, UiFactory.AccentColor, outline);

            // Buy button: price with the scrap icon.
            RectTransform buyRt = UiFactory.Rect("BuyButton", body, midRight, midRight, new Vector2(-12f, 0f), new Vector2(176f, 96f));
            Image plate = UiFactory.Image(buyRt, UiFactory.Sprite("UI_GearButton"), s_PlayColor, true, true);
            RectTransform priceIcon = UiFactory.Rect("ScrapIcon", buyRt, midLeft, midLeft, new Vector2(16f, 0f), new Vector2(46f, 46f));
            UiFactory.Image(priceIcon, UiFactory.Sprite("UI_Icon_Scrap"), Color.white, false).preserveAspect = true;
            // Over the whole button: a number right of the scrap icon (margins set by the row), a word alone centred.
            TextMeshProUGUI price = OutlinedLabel("Price", buyRt, center, center, Vector2.zero, new Vector2(176f, 70f), "100",
                38f, TextAlignmentOptions.Right, Color.white, outline);
            price.enableAutoSizing = true;
            price.fontSizeMin = 18f;
            price.fontSizeMax = 38f;
            // A supply paid with a rewarded ad shows the shared gold ad icon instead of a price.
            RectTransform adIcon = UiFactory.Rect("AdIcon", buyRt, center, center, Vector2.zero, new Vector2(78f, 78f));
            UiFactory.Image(adIcon, UiFactory.Sprite("UI_Icon_Ad"), Color.white, false).preserveAspect = true;
            adIcon.gameObject.SetActive(false);
            var buy = buyRt.gameObject.AddComponent<Button>();
            buy.targetGraphic = plate;
            ColorBlock colors = buy.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = Color.white;                          // the row paints the disabled plate itself
            buy.colors = colors;
            buyRt.gameObject.AddComponent<UIButtonFx>();

            var view = row.gameObject.AddComponent<WorkshopRowView>();
            PrefabBuilder.Wire(view, ("m_Icon", icon), ("m_Title", title), ("m_Description", description), ("m_Count", count),
                ("m_BuyButton", buy), ("m_BuyPlate", plate), ("m_Price", price), ("m_PriceIcon", priceIcon.gameObject), ("m_Body", body),
                ("m_AdIcon", adIcon.gameObject));
            CharacterBuilder.SetProps(view, ("m_Pips", pips));
            return view;
        }
    }
}
