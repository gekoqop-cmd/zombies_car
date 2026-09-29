using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;

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
        /// <summary>Scrap and kills under the health panel. Returns the scrap panel (it pops when scrap comes in).</summary>
        private static RectTransform BuildRunCounters(RectTransform safe, Material outline, float top, out TextMeshProUGUI money,
            out TextMeshProUGUI kills)
        {
            RectTransform moneyPanel = CounterPill("ScrapCounter", safe, new Vector2(24f, top), 200f, "UI_Icon_Scrap", outline, out money);
            CounterPill("KillCounter", safe, new Vector2(24f + 200f + 12f, top), 170f, "UI_Icon_Skull", outline, out kills);
            return moneyPanel;
        }

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

        /// <summary>Bottom right (desktop): the gun [1] and the axe [2], the one in hand lit orange, the switch hint above.</summary>
        private static void BuildWeaponSlots(RectTransform safe, Material outline)
        {
            var bottomRight = new Vector2(1f, 0f);
            RectTransform root = UiFactory.Rect("WeaponSlots", safe, bottomRight, bottomRight, new Vector2(-24f, 24f),
                new Vector2(c_WeaponSlot * 2f + 14f, c_WeaponSlot + 44f));
            Image gun = WeaponSlotPlate("Gun", root, 0f, "UI_Icon_Rifle", "1", outline);
            Image axe = WeaponSlotPlate("Axe", root, c_WeaponSlot + 14f, "UI_Icon_Axe", "2", outline);
            var top = new Vector2(1f, 1f);
            TextMeshProUGUI hint = OutlinedLabel("Hint", root, top, top, Vector2.zero, new Vector2(520f, 38f), "Q: СМЕНИТЬ ОРУЖИЕ", 26f,
                TextAlignmentOptions.Right, Color.white, outline);
            PrefabBuilder.Wire(root.gameObject.AddComponent<WeaponSlotsView>(),
                ("m_State", DataAssetsBuilder.Load<WeaponState>(DataAssetsBuilder.c_WeaponState)),
                ("m_GunPlate", gun), ("m_AxePlate", axe), ("m_Hint", hint));
        }

        private static Image WeaponSlotPlate(string name, RectTransform parent, float x, string icon, string key, Material outline)
        {
            var bottomLeft = new Vector2(0f, 0f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform slot = UiFactory.Rect(name, parent, bottomLeft, center, new Vector2(x + c_WeaponSlot * 0.5f, c_WeaponSlot * 0.5f),
                new Vector2(c_WeaponSlot, c_WeaponSlot));
            Image plate = UiFactory.Image(slot, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            RectTransform iconRt = UiFactory.Rect("Icon", slot, center, center, new Vector2(4f, -2f), new Vector2(c_WeaponSlot * 0.74f, c_WeaponSlot * 0.74f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            var topLeft = new Vector2(0f, 1f);
            OutlinedLabel("Key", slot, topLeft, topLeft, new Vector2(8f, -2f), new Vector2(40f, 40f), key, 30f,
                TextAlignmentOptions.TopLeft, Color.white, outline);
            return plate;
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

            var screen = host.gameObject.AddComponent<LevelUpScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Pool", DataAssetsBuilder.Load<PerkPool>(DataAssetsBuilder.c_PerkPool)),
                ("m_Config", DataAssetsBuilder.Load<ProgressionConfig>(DataAssetsBuilder.c_ProgressionConfig)),
                ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Root", group), ("m_Title", title), ("m_Subtitle", subtitle));
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
            Image plate = UiFactory.Image(UiFactory.Stretch("Plate", body), UiFactory.Sprite("UI_GearButton"), Color.white, true, true);

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

            var topLeft = new Vector2(0f, 1f);
            RectTransform keyRt = UiFactory.Rect("Key", body, topLeft, topLeft, new Vector2(22f, -22f), new Vector2(60f, 60f));
            UiFactory.Image(keyRt, UiFactory.Sprite("UI_Button"), new Color(1f, 0.93f, 0.8f, 1f), true);
            TextMeshProUGUI key = UiFactory.Label("Number", keyRt, center, center, Vector2.zero, new Vector2(60f, 60f), "1", 38f,
                TextAlignmentOptions.Center, new Color(0.18f, 0.12f, 0.08f, 1f));

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
                ("m_Glow", glow));
            return view;
        }

        // ------------------------------------------------------------------ game over
        private static readonly Vector2 s_GameOverWindow = new Vector2(1060f, 860f);

        private static GameOverScreen BuildGameOverScreen(RectTransform parent, Material outline)
        {
            RectTransform host = UiFactory.Stretch("GameOverScreen", parent);
            RectTransform root = UiFactory.Stretch("Root", host);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", root), null, new Color(0.02f, 0.01f, 0.01f, 0.78f), false, true);
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform window = UiFactory.Rect("Window", root, center, center, new Vector2(0f, -20f), s_GameOverWindow);
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            TextMeshProUGUI title = OutlinedLabel("Title", window, top, top, new Vector2(0f, -30f), new Vector2(980f, 100f), "ИГРА ОКОНЧЕНА",
                84f, TextAlignmentOptions.Center, new Color(1f, 0.38f, 0.3f, 1f), outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 50f;
            title.fontSizeMax = 84f;
            TextMeshProUGUI reason = OutlinedLabel("Reason", window, top, top, new Vector2(0f, -132f), new Vector2(980f, 52f),
                "МАШИНА УНИЧТОЖЕНА", 38f, TextAlignmentOptions.Center, s_SubtitleColor, outline);
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 24f;
            reason.fontSizeMax = 38f;

            TextMeshProUGUI money = StatRow(window, 0, "UI_Icon_Scrap", "ЗАРАБОТАНО", outline, new Color(1f, 0.82f, 0.35f, 1f));
            TextMeshProUGUI level = StatRow(window, 1, "UI_Icon_Xp", "УРОВЕНЬ", outline, Color.white);
            TextMeshProUGUI kills = StatRow(window, 2, "UI_Icon_Skull", "УБИТО ЗОМБИ", outline, Color.white);
            TextMeshProUGUI wave = StatRow(window, 3, "UI_Icon_Boss", "ВОЛНА", outline, Color.white);
            TextMeshProUGUI coins = OutlinedLabel("Coins", window, top, top, new Vector2(0f, -640f), new Vector2(980f, 52f),
                "+0 МОНЕТ", 36f, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.35f, 1f), outline);
            coins.enableAutoSizing = true;
            coins.fontSizeMin = 24f;
            coins.fontSizeMax = 36f;

            Button restart = HudButton("RestartButton", window, bottom, new Vector2(-220f, 40f), new Vector2(380f, 116f), "ЗАНОВО", 50f,
                s_PlayColor, outline);
            Button menu = HudButton("MenuButton", window, bottom, new Vector2(220f, 40f), new Vector2(380f, 116f), "В МЕНЮ", 50f,
                Color.white, outline);

            var screen = host.gameObject.AddComponent<GameOverScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_Root", group), ("m_Window", window), ("m_Title", title), ("m_Reason", reason), ("m_Money", money), ("m_Level", level),
                ("m_Kills", kills), ("m_Wave", wave), ("m_Coins", coins), ("m_RestartButton", restart), ("m_MenuButton", menu));
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>One result line of the game-over window: icon, label, value (right-aligned).</summary>
        private static TextMeshProUGUI StatRow(RectTransform window, int index, string icon, string label, Material outline, Color valueColor)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            RectTransform row = UiFactory.Rect($"Stat{index + 1}", window, top, top, new Vector2(0f, -210f - index * 108f), new Vector2(900f, 96f));
            UiFactory.Image(row, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.55f), true);
            RectTransform iconRt = UiFactory.Rect("Icon", row, midLeft, midLeft, new Vector2(18f, 0f), new Vector2(72f, 72f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            OutlinedLabel("Label", row, midLeft, midLeft, new Vector2(108f, 0f), new Vector2(420f, 80f), label, 40f,
                TextAlignmentOptions.Left, s_SubtitleColor, outline);
            TextMeshProUGUI value = OutlinedLabel("Value", row, midRight, midRight, new Vector2(-28f, 0f), new Vector2(440f, 80f), "0", 52f,
                TextAlignmentOptions.Right, valueColor, outline);
            value.enableAutoSizing = true;
            value.fontSizeMin = 30f;
            value.fontSizeMax = 52f;
            return value;
        }

        // ------------------------------------------------------------------ workshop
        private const int c_WorkshopRows = 6;
        private static readonly Vector2 s_WorkshopWindow = new Vector2(1600f, 900f);
        private static readonly Vector2 s_WorkshopRow = new Vector2(740f, 176f);

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
            RectTransform window = UiFactory.Rect("Window", root, center, center, new Vector2(0f, -50f), s_WorkshopWindow);
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

            var rows = new WorkshopRowView[c_WorkshopRows];
            for (int i = 0; i < c_WorkshopRows; i++)
            {
                int column = i % 2, line = i / 2;
                var pos = new Vector2((column - 0.5f) * (s_WorkshopRow.x + 24f), -196f - line * (s_WorkshopRow.y + 14f));
                rows[i] = BuildWorkshopRow(window, i, pos, outline);
            }

            Button close = HudButton("DoneButton", window, bottom, new Vector2(0f, 24f), new Vector2(360f, 96f), "ГОТОВО", 44f,
                s_PlayColor, outline);
            OutlinedLabel("Hint", window, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 50f), new Vector2(460f, 40f),
                "E / ESC: ЗАКРЫТЬ", 26f, TextAlignmentOptions.Right, new Color(0.6f, 0.58f, 0.55f, 1f), outline);

            var screen = host.gameObject.AddComponent<WorkshopScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Catalog", DataAssetsBuilder.Load<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades)),
                ("m_Car", DataAssetsBuilder.Load<CarState>(DataAssetsBuilder.c_CarState)), ("m_Root", group), ("m_Window", window),
                ("m_Money", money), ("m_CarStatus", status), ("m_CloseButton", close));
            CharacterBuilder.SetProps(screen, ("m_Rows", rows));
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        private static WorkshopRowView BuildWorkshopRow(RectTransform window, int index, Vector2 pos, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform row = UiFactory.Rect($"Row{index + 1}", window, top, top, pos, s_WorkshopRow);
            RectTransform body = UiFactory.Stretch("Body", row);
            UiFactory.Image(body, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.55f), true);

            RectTransform disc = UiFactory.Rect("IconDisc", body, midLeft, midLeft, new Vector2(18f, 0f), new Vector2(132f, 132f));
            UiFactory.Image(disc, UiFactory.Sprite("UI_StickKnob"), new Color(0.3f, 0.56f, 0.9f, 1f), false);
            RectTransform iconRt = UiFactory.Rect("Icon", disc, center, center, Vector2.zero, new Vector2(106f, 106f));
            Image icon = UiFactory.Image(iconRt, UiFactory.Sprite("UI_Icon_Armor"), Color.white, false);
            icon.preserveAspect = true;

            TextMeshProUGUI title = OutlinedLabel("Title", body, topLeft, topLeft, new Vector2(168f, -10f), new Vector2(360f, 50f), "БРОНЯ",
                38f, TextAlignmentOptions.Left, Color.white, outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 38f;
            TextMeshProUGUI description = OutlinedLabel("Description", body, topLeft, topLeft, new Vector2(168f, -58f),
                new Vector2(360f, 70f), "Описание", 26f, TextAlignmentOptions.TopLeft, s_SubtitleColor, outline);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 20f;
            description.fontSizeMax = 26f;

            const int pipCount = 5;
            var pips = new Image[pipCount];
            for (int i = 0; i < pipCount; i++)
            {
                RectTransform pip = UiFactory.Rect($"Pip{i + 1}", body, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(170f + i * 34f, 14f), new Vector2(28f, 28f));
                pips[i] = UiFactory.Image(pip, UiFactory.Sprite("UI_StickKnob"), Color.gray, false);
            }
            TextMeshProUGUI count = OutlinedLabel("Count", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(168f, 10f),
                new Vector2(340f, 36f), string.Empty, 26f, TextAlignmentOptions.Left, UiFactory.AccentColor, outline);

            // Buy button: price with the scrap icon.
            RectTransform buyRt = UiFactory.Rect("BuyButton", body, midRight, midRight, new Vector2(-16f, 0f), new Vector2(176f, 116f));
            Image plate = UiFactory.Image(buyRt, UiFactory.Sprite("UI_GearButton"), s_PlayColor, true, true);
            RectTransform priceIcon = UiFactory.Rect("ScrapIcon", buyRt, midLeft, midLeft, new Vector2(16f, 0f), new Vector2(46f, 46f));
            UiFactory.Image(priceIcon, UiFactory.Sprite("UI_Icon_Scrap"), Color.white, false).preserveAspect = true;
            TextMeshProUGUI price = OutlinedLabel("Price", buyRt, midRight, midRight, new Vector2(-14f, 0f), new Vector2(110f, 70f), "100",
                38f, TextAlignmentOptions.Right, Color.white, outline);
            price.enableAutoSizing = true;
            price.fontSizeMin = 24f;
            price.fontSizeMax = 38f;
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
                ("m_BuyButton", buy), ("m_BuyPlate", plate), ("m_Price", price), ("m_PriceIcon", priceIcon.gameObject), ("m_Body", body));
            CharacterBuilder.SetProps(view, ("m_Pips", pips));
            return view;
        }
    }
}
