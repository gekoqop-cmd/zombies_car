using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.UI;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Main menu (background, logo, Play / Settings, map selection) and the HUD, assembled from the parts cut out of
    /// refmenu/ by tools/ui/cut_ui.py: every label and value is a live TextMeshPro text or a filled Image, never part
    /// of a picture. The settings pop-up is the same build in both scenes.
    /// </summary>
    public static partial class SceneBuilder
    {
        private static readonly Color s_ButtonLabel = new Color(0.97f, 0.95f, 0.9f, 1f);
        private static readonly Color s_PlayColor = new Color(0.93f, 0.47f, 0.13f, 1f);
        private const float c_LogoAspect = 1200f / 412f;   // UI_Logo.png (tools/ui/cut_ui.py)
        private const float c_MenuBackgroundAspect = 1600f / 900f;
        private const float c_CardSpacing = 1700f;

        // ================================================================== Main menu
        private static void BuildMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera(Color.black);
            Canvas canvas = UiFactory.Canvas("MenuCanvas", 0);
            Material outline = TmpSetup.OutlineMaterial();

            // Background in cover mode (fills any aspect without distortion) with a slow breathing zoom.
            RectTransform bgRoot = UiFactory.Stretch("Background", canvas.transform);
            bgRoot.gameObject.AddComponent<RectMask2D>();
            var center = new Vector2(0.5f, 0.5f);
            RectTransform bg = UiFactory.Rect("Picture", bgRoot, center, center, Vector2.zero, new Vector2(1920f, 1080f));
            UiFactory.Image(bg, UiFactory.Sprite("UI_MenuBackground"), Color.white, false);
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = c_MenuBackgroundAspect;
            bg.gameObject.AddComponent<UIZoomLoop>();
            RectTransform dimRt = UiFactory.Stretch("Dim", canvas.transform);
            UiFactory.Image(dimRt, null, Color.black, false);
            var dim = dimRt.gameObject.AddComponent<CanvasGroup>();
            dim.alpha = 0f;
            dim.blocksRaycasts = false;

            RectTransform safe = UiFactory.Stretch("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Main screen: logo top right, the buttons in a column under it (the car and hero on the left stay clear).
            RectTransform main = UiFactory.Stretch("MainScreen", safe);
            var mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            var topRight = new Vector2(1f, 1f);
            const float logoWidth = 640f, margin = 40f, buttonWidth = 440f, buttonHeight = 112f;
            RectTransform logo = UiFactory.Rect("Logo", main, topRight, topRight, new Vector2(-margin, -30f),
                new Vector2(logoWidth, logoWidth / c_LogoAspect));
            UiFactory.Image(logo, UiFactory.Sprite("UI_Logo"), Color.white, false).preserveAspect = true;
            float columnX = -margin - (logoWidth - buttonWidth) * 0.5f;
            float y = -30f - logoWidth / c_LogoAspect - 30f;
            Button play = HudButton("PlayButton", main, topRight, new Vector2(columnX, y), new Vector2(buttonWidth, buttonHeight),
                "ИГРАТЬ", 52f, s_PlayColor, outline);
            Button settingsButton = HudButton("SettingsButton", main, topRight, new Vector2(columnX, y - buttonHeight - 24f),
                new Vector2(buttonWidth, buttonHeight), "НАСТРОЙКИ", 46f, Color.white, outline);

            // Map selection.
            RectTransform mapScreen = UiFactory.Stretch("MapScreen", safe);
            var mapGroup = mapScreen.gameObject.AddComponent<CanvasGroup>();
            mapGroup.alpha = 0f;
            Button back = BuildMapSelection(mapScreen, outline);
            mapScreen.gameObject.SetActive(false);

            SettingsPanel settings = BuildSettingsPopup(canvas.transform, outline, false);

            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            PrefabBuilder.Wire(menu, ("m_PlayButton", play), ("m_SettingsButton", settingsButton), ("m_BackButton", back),
                ("m_MainScreen", mainGroup), ("m_MapScreen", mapGroup), ("m_Dim", dim), ("m_Settings", settings),
                ("m_Music", AudioBuilder.Music("Music_Menu")));
            SaveUiPrefab(canvas.gameObject, "MainMenu");
            UiFactory.EventSystem();
            AudioBuilder.AddAudio();
            EditorSceneManager.SaveScene(scene, c_MenuScene);
        }

        private static Button BuildMapSelection(RectTransform screen, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            // Plain warm-dark backdrop (also the swipe area); it stays the same for Day and Night.
            UiFactory.Image(UiFactory.Stretch("Tint", screen), null, new Color(0.16f, 0.1f, 0.07f, 0.45f), false, true);
            OutlinedLabel("Title", screen, top, top, new Vector2(0f, -40f), new Vector2(1200f, 90f), "ВЫБОР КАРТЫ", 64f,
                TextAlignmentOptions.Center, s_ButtonLabel, outline);

            RectTransform strip = UiFactory.Rect("Cards", screen, center, center, new Vector2(0f, -10f), new Vector2(10f, 10f));
            MapCard city = BuildMapCard(strip, 0, "ГОРОД", outline,
                DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_MapConfig), DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_NightMapConfig));
            MapCard farm = BuildMapCard(strip, 1, "ФЕРМА", outline, null, null);

            Button prev = ArrowButton("PrevButton", screen, new Vector2(-600f, -10f), true);
            Button next = ArrowButton("NextButton", screen, new Vector2(600f, -10f), false);
            var bottomLeft = new Vector2(0f, 0f);
            Button back = HudButton("BackButton", screen, bottomLeft, new Vector2(40f, 40f), new Vector2(300f, 96f), "НАЗАД", 40f,
                Color.white, outline);

            var select = screen.gameObject.AddComponent<MapSelectScreen>();
            var so = new SerializedObject(select);
            so.FindProperty("m_Strip").objectReferenceValue = strip;
            SerializedProperty cards = so.FindProperty("m_Cards");
            cards.arraySize = 2;
            cards.GetArrayElementAtIndex(0).objectReferenceValue = city;
            cards.GetArrayElementAtIndex(1).objectReferenceValue = farm;
            so.FindProperty("m_Spacing").floatValue = c_CardSpacing;
            so.FindProperty("m_PrevButton").objectReferenceValue = prev;
            so.FindProperty("m_NextButton").objectReferenceValue = next;
            so.ApplyModifiedPropertiesWithoutUndo();
            return back;
        }

        private static MapCard BuildMapCard(RectTransform strip, int index, string title, Material outline, MapConfig day, MapConfig night)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            const float width = 960f, height = 780f, pictureWidth = 900f, pictureHeight = 506f;
            RectTransform card = UiFactory.Rect($"Card_{title}", strip, center, center, new Vector2(index * c_CardSpacing, 0f),
                new Vector2(width, height));
            UiFactory.Image(card, UiFactory.Sprite("UI_GearButton"), Color.white, true);
            OutlinedLabel("Title", card, top, top, new Vector2(0f, -22f), new Vector2(width - 60f, 90f), title, 72f,
                TextAlignmentOptions.Center, s_ButtonLabel, outline);

            RectTransform picture = UiFactory.Rect("Picture", card, top, top, new Vector2(0f, -118f), new Vector2(pictureWidth, pictureHeight));
            picture.gameObject.AddComponent<RectMask2D>();
            CanvasGroup nightGroup = null;
            bool locked = day == null;
            if (locked)
            {
                UiFactory.Image(picture, null, Color.black, false);
                OutlinedLabel("Question", picture, center, center, new Vector2(0f, 40f), new Vector2(400f, 320f), "?", 300f,
                    TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 0.9f, 1f), outline);
                OutlinedLabel("Soon", picture, bottom, bottom, new Vector2(0f, 40f), new Vector2(800f, 80f), "СКОРО БУДЕТ", 54f,
                    TextAlignmentOptions.Center, s_PlayColor, outline);
            }
            else
            {
                UiFactory.Image(UiFactory.Stretch("Day", picture), UiFactory.Sprite("UI_MapCityDay"), Color.white, false);
                RectTransform nightRt = UiFactory.Stretch("Night", picture);
                UiFactory.Image(nightRt, UiFactory.Sprite("UI_MapCityNight"), Color.white, false);
                nightGroup = nightRt.gameObject.AddComponent<CanvasGroup>();
                nightGroup.alpha = 0f;
                nightGroup.blocksRaycasts = false;
            }

            // Bottom row: Day / Night switch on the left, Play on the right.
            var bottomLeft = new Vector2(0f, 0f);
            var bottomRight = new Vector2(1f, 0f);
            Button dayButton = null, nightButton = null;
            Image dayPlate = null, nightPlate = null;
            if (!locked)
            {
                (dayButton, dayPlate) = ToggleButton("DayButton", card, new Vector2(30f, 30f), "UI_IconSun", "ДЕНЬ", outline);
                (nightButton, nightPlate) = ToggleButton("NightButton", card, new Vector2(270f, 30f), "UI_IconMoon", "НОЧЬ", outline);
            }
            Button play = HudButton("PlayButton", card, bottomRight, new Vector2(-30f, 30f), new Vector2(320f, 110f), "ИГРАТЬ", 50f,
                s_PlayColor, outline);
            ColorBlock colors = play.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.8f);
            play.colors = colors;
            play.interactable = !locked;
            if (locked)
                play.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;   // clearly unavailable
            var mapCard = card.gameObject.AddComponent<MapCard>();
            PrefabBuilder.Wire(mapCard, ("m_DayMap", day), ("m_NightMap", night), ("m_NightPicture", nightGroup),
                ("m_DayButton", dayButton), ("m_NightButton", nightButton), ("m_DayButtonPlate", dayPlate),
                ("m_NightButtonPlate", nightPlate), ("m_PlayButton", play));
            return mapCard;
        }

        private static (Button, Image) ToggleButton(string name, RectTransform parent, Vector2 pos, string icon, string label, Material outline)
        {
            var bottomLeft = new Vector2(0f, 0f);
            RectTransform rt = UiFactory.Rect(name, parent, bottomLeft, bottomLeft, pos, new Vector2(220f, 110f));
            // Light plate: the MapCard paints the chosen one orange, the other dark.
            Image plate = UiFactory.Image(rt, UiFactory.Sprite("UI_Button"), Color.white, true, true);
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform iconRt = UiFactory.Rect("Icon", rt, midLeft, midLeft, new Vector2(20f, 0f), new Vector2(64f, 64f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false);
            OutlinedLabel("Label", rt, midLeft, midLeft, new Vector2(92f, 0f), new Vector2(120f, 80f), label, 38f,
                TextAlignmentOptions.Left, s_ButtonLabel, outline);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            button.colors = colors;
            rt.gameObject.AddComponent<UIButtonFx>();
            return (button, plate);
        }

        private static Button ArrowButton(string name, RectTransform parent, Vector2 pos, bool left)
        {
            var center = new Vector2(0.5f, 0.5f);
            RectTransform rt = UiFactory.Rect(name, parent, center, center, pos, new Vector2(120f, 120f));
            Image plate = UiFactory.Image(rt, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            RectTransform icon = UiFactory.Stretch("Icon", rt);
            icon.offsetMin = new Vector2(26f, 26f);
            icon.offsetMax = new Vector2(-26f, -26f);
            icon.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
            UiFactory.Image(icon, UiFactory.Sprite("UI_Arrow"), Color.white, false);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            button.colors = colors;
            rt.gameObject.AddComponent<UIButtonFx>();
            return button;
        }

        /// <summary>Menu button in the HUD style: the dark rounded plate of the reference, tinted, with an outlined label.</summary>
        private static Button HudButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label,
            float fontSize, Color tint, Material outline)
        {
            RectTransform rt = UiFactory.Rect(name, parent, anchor, anchor, pos, size);
            Image plate = UiFactory.Image(rt, UiFactory.Sprite("UI_GearButton"), tint, true, true);
            OutlinedLabel("Label", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, label, fontSize,
                TextAlignmentOptions.Center, s_ButtonLabel, outline);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            rt.gameObject.AddComponent<UIButtonFx>();
            return button;
        }

        private static TextMeshProUGUI OutlinedLabel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            string text, float fontSize, TextAlignmentOptions align, Color color, Material outline)
        {
            TextMeshProUGUI t = UiFactory.Label(name, parent, anchor, pivot, pos, size, text, fontSize, align, color);
            t.fontSharedMaterial = outline;
            return t;
        }

        // ================================================================== Settings pop-up
        /// <summary>Settings pop-up (hidden): volumes, quality tier, Back. Shared by the main menu and the HUD.</summary>
        private static SettingsPanel BuildSettingsPopup(Transform parent, Material outline, bool menuButton)
        {
            RectTransform root = UiFactory.Stretch("SettingsPanel", parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.6f), false, true);   // dims and blocks clicks behind
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            // Taller by the touch-controls row; SettingsPanel removes that height again on desktop.
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero,
                new Vector2(1060f, 820f + c_MobileSettingsRowHeight));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            OutlinedLabel("Title", window, top, top, new Vector2(0f, -34f), new Vector2(900f, 80f), "НАСТРОЙКИ", 60f,
                TextAlignmentOptions.Center, s_ButtonLabel, outline);

            Slider master = VolumeRow(window, "MasterVolume", "ОБЩАЯ ГРОМКОСТЬ", -150f, outline);
            Slider music = VolumeRow(window, "MusicVolume", "ГРОМКОСТЬ МУЗЫКИ", -250f, outline);
            Slider sfx = VolumeRow(window, "SfxVolume", "ГРОМКОСТЬ ЭФФЕКТОВ", -350f, outline);

            OutlinedLabel("QualityLabel", window, top, top, new Vector2(0f, -440f), new Vector2(900f, 60f), "КАЧЕСТВО ГРАФИКИ", 38f,
                TextAlignmentOptions.Center, new Color(0.8f, 0.78f, 0.74f), outline);
            RectTransform row = UiFactory.Rect("TierButtons", window, top, top, new Vector2(0f, -520f), new Vector2(1000f, UiFactory.c_MinTouchTarget));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var size = new Vector2(230f, UiFactory.c_MinTouchTarget);
            var normal = new Color(0.16f, 0.17f, 0.2f, 0.95f);
            Button low = TierButton("LowButton", row, size, "НИЗКОЕ", normal, outline);
            Button medium = TierButton("MediumButton", row, size, "СРЕДНЕЕ", normal, outline);
            Button high = TierButton("HighButton", row, size, "ВЫСОКОЕ", normal, outline);
            Button ultra = TierButton("UltraButton", row, size, "УЛЬТРА", normal, outline);

            // Touch controls (mobile only): size, and "layout" to drag them around (only in the game, where they exist).
            RectTransform mobileRow = UiFactory.Rect("TouchControls", window, top, top, new Vector2(0f, -655f), new Vector2(940f, 90f));
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            OutlinedLabel("Label", mobileRow, midLeft, midLeft, Vector2.zero, new Vector2(270f, 70f), "ДЖОЙСТИКИ", 36f,
                TextAlignmentOptions.Left, s_ButtonLabel, outline);
            RectTransform sizeSlot = UiFactory.Rect("SizeSlot", mobileRow, midLeft, midLeft, new Vector2(270f, 0f), new Vector2(360f, 56f));
            Slider controlsSize = BuildSlider(sizeSlot);
            Button editControls = HudButton("EditLayoutButton", mobileRow, midRight, Vector2.zero, new Vector2(280f, 90f), "РАСПОЛОЖЕНИЕ",
                30f, Color.white, outline);
            var bottom = new Vector2(0.5f, 0f);
            // In the game: "To menu" next to Back (the main menu's own pop-up only has Back).
            Button close = HudButton("BackButton", window, bottom, new Vector2(menuButton ? 200f : 0f, 40f), new Vector2(360f, 110f),
                "НАЗАД", 48f, s_PlayColor, outline);
            Button toMenu = menuButton
                ? HudButton("MenuButton", window, bottom, new Vector2(-200f, 40f), new Vector2(360f, 110f), "В МЕНЮ", 48f, Color.white, outline)
                : null;

            var settings = root.gameObject.AddComponent<SettingsPanel>();
            PrefabBuilder.Wire(settings, ("m_MasterSlider", master), ("m_MusicSlider", music), ("m_SfxSlider", sfx),
                ("m_LowButton", low), ("m_MediumButton", medium), ("m_HighButton", high), ("m_UltraButton", ultra),
                ("m_CloseButton", close), ("m_MenuButton", toMenu), ("m_MobileSection", mobileRow.gameObject),
                ("m_ControlsSizeSlider", controlsSize), ("m_EditControlsButton", editControls), ("m_Window", window));
            CharacterBuilder.SetProps(settings, ("m_MobileSectionHeight", c_MobileSettingsRowHeight));
            root.gameObject.SetActive(false);
            return settings;
        }

        private static Button TierButton(string name, RectTransform row, Vector2 size, string label, Color color, Material outline)
        {
            RectTransform rt = UiFactory.Rect(name, row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image plate = UiFactory.Image(rt, UiFactory.Sprite("UI_Button"), color, true, true);   // light plate: the chosen tier shows orange
            OutlinedLabel("Label", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, label, 32f,
                TextAlignmentOptions.Center, s_ButtonLabel, outline);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            rt.gameObject.AddComponent<UIButtonFx>();
            return button;
        }

        private static Slider VolumeRow(RectTransform window, string name, string label, float y, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            RectTransform row = UiFactory.Rect(name, window, top, top, new Vector2(0f, y), new Vector2(940f, 80f));
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            OutlinedLabel("Label", row, midLeft, midLeft, Vector2.zero, new Vector2(460f, 70f), label, 36f, TextAlignmentOptions.Left,
                s_ButtonLabel, outline);

            RectTransform sliderSlot = UiFactory.Rect("SliderSlot", row, midRight, midRight, Vector2.zero, new Vector2(440f, 56f));
            return BuildSlider(sliderSlot);
        }

        /// <summary>A HUD-style slider (rounded trough, orange fill, light handle) filling <paramref name="slot"/>.</summary>
        private static Slider BuildSlider(RectTransform slot)
        {
            RectTransform sliderRt = UiFactory.Stretch("Slider", slot);
            UiFactory.Image(sliderRt, UiFactory.Sprite("UI_BarTrack"), Color.white, true, true);   // the trough, also the hit area
            RectTransform fillArea = UiFactory.Stretch("FillArea", sliderRt);
            fillArea.offsetMin = new Vector2(8f, 8f);
            fillArea.offsetMax = new Vector2(-8f, -8f);
            RectTransform fill = UiFactory.Stretch("Fill", fillArea);
            UiFactory.Image(fill, UiFactory.Sprite("UI_BarFillRed"), new Color(1f, 0.72f, 0.35f, 1f), true);
            RectTransform handleArea = UiFactory.Stretch("HandleArea", sliderRt);
            handleArea.offsetMin = new Vector2(20f, 0f);
            handleArea.offsetMax = new Vector2(-20f, 0f);
            // The slider stretches the handle over the area's height: the size here is width and extra height.
            RectTransform handle = UiFactory.Rect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(40f, 18f));
            Image handleImage = UiFactory.Image(handle, UiFactory.Sprite("UI_Button"), new Color(0.97f, 0.94f, 0.88f, 1f), true, true);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        // ================================================================== HUD
        private static void BuildHud()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Canvas canvas = UiFactory.Canvas("HUDCanvas", 10);
            RectTransform safe = UiFactory.Stretch("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Material outline = TmpSetup.OutlineMaterial();
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var topCenter = new Vector2(0.5f, 1f);
            // Same places as before (match-height canvas: a 4:3 screen is 1440 units wide and all three still fit):
            // health / level top-left, wave / timer top-centre, ammo + settings top-right.
            const float margin = 24f;
            const float buttonSize = 112f;

            // ---- Health / level (hp_widget): widget box = the reference's opaque area, parts placed in its fractions.
            const float hpHeight = 140f;
            RectTransform hp = UiFactory.Rect("HealthPanel", safe, topLeft, topLeft, new Vector2(margin, -margin),
                new Vector2(hpHeight * 2006f / 565f, hpHeight));
            Part(hp, "Panel", "UI_HpPanel", 560, 85, 2080, 625, s_HpRef);
            Part(hp, "Portrait", "UI_HpPortrait", 55, 35, 690, 645, s_HpRef);
            Part(hp, "PortraitFrame", "UI_HpFrame", 55, 35, 690, 645, s_HpRef);
            TextMeshProUGUI healthLabel = PartLabel(hp, "HealthLabel", 730, 150, 1320, 265, s_HpRef, "ЗДОРОВЬЕ", 34f, TextAlignmentOptions.Left, outline);
            RectTransform healthTrack = PartRect(hp, "HealthBar", 706, 277, 2022, 420, s_HpRef);
            UiFactory.Image(healthTrack, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            RectTransform healthFillRt = Inset(UiFactory.Stretch("Fill", healthTrack), 7f);
            Image healthFill = UiFactory.Fill(healthFillRt, UiFactory.Sprite("UI_BarFillRed"), Color.white, 1f);
            TextMeshProUGUI healthValue = OutlinedLabel("HealthValue", healthTrack, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(220f, 46f), string.Empty, 34f, TextAlignmentOptions.Right, Color.white, outline);
            TextMeshProUGUI levelLabel = PartLabel(hp, "LevelLabel", 720, 448, 1160, 552, s_HpRef, string.Empty, 26f, TextAlignmentOptions.Left, outline);
            RectTransform levelTrack = PartRect(hp, "LevelBar", 1178, 461, 2020, 556, s_HpRef);
            UiFactory.Image(levelTrack, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            RectTransform levelFillRt = Inset(UiFactory.Stretch("Fill", levelTrack), 6f);
            Image levelFill = UiFactory.Fill(levelFillRt, UiFactory.Sprite("UI_BarFillBlue"), Color.white, 0f);
            healthLabel.color = UiFactory.TextColor;

            // ---- Wave / timer (wave.png): the notched panel, the wave above, the countdown below.
            const float waveHeight = 120f;
            RectTransform wave = UiFactory.Rect("WavePanel", safe, topCenter, topCenter, new Vector2(0f, -margin),
                new Vector2(waveHeight * 1558f / 613f, waveHeight));
            Part(wave, "Panel", "UI_WavePanel", 108, 142, 1666, 755, s_WaveRef);
            TextMeshProUGUI waveLabel = PartLabel(wave, "WaveLabel", 300, 225, 1480, 440, s_WaveRef, string.Empty, 40f,
                TextAlignmentOptions.Center, outline);
            TextMeshProUGUI waveTimer = PartLabel(wave, "WaveTimer", 330, 480, 1450, 705, s_WaveRef, string.Empty, 42f,
                TextAlignmentOptions.Center, outline);

            // ---- Ammo + settings (patronset.png): the ammo panel left of the gear button.
            const float ammoHeight = 90f;
            float ammoWidth = ammoHeight * 1660f / 383f;
            RectTransform ammo = UiFactory.Rect("AmmoPanel", safe, topRight, topRight, new Vector2(-(margin * 2f + buttonSize), -margin),
                new Vector2(ammoWidth, ammoHeight));
            Part(ammo, "Panel", "UI_AmmoPanel", 50, 170, 1710, 553, s_AmmoRef);
            Part(ammo, "AmmoIcon", "UI_AmmoIcon", 135, 235, 365, 470, s_AmmoRef);
            PartLabel(ammo, "AmmoLabel", 395, 285, 960, 430, s_AmmoRef, "ПАТРОНЫ", 26f, TextAlignmentOptions.Left, outline);
            TextMeshProUGUI ammoValue = PartLabel(ammo, "AmmoValue", 1030, 270, 1620, 450, s_AmmoRef, string.Empty, 36f,
                TextAlignmentOptions.Right, outline);
            // Reload: label + progress bar under the ammo panel, shown while reloading.
            RectTransform reload = UiFactory.Rect("ReloadHint", safe, topRight, topRight, new Vector2(-(margin * 2f + buttonSize), -122f),
                new Vector2(ammoWidth, 64f));
            OutlinedLabel("ReloadLabel", reload, topRight, topRight, Vector2.zero, new Vector2(ammoWidth, 34f), "ПЕРЕЗАРЯДКА", 26f,
                TextAlignmentOptions.Right, UiFactory.AccentColor, outline);
            RectTransform reloadBar = UiFactory.Rect("ReloadBar", reload, topRight, topRight, new Vector2(0f, -36f), new Vector2(ammoWidth, 26f));
            UiFactory.Image(reloadBar, UiFactory.Sprite("UI_BarTrack"), Color.white, true);
            RectTransform reloadFillRt = Inset(UiFactory.Stretch("Fill", reloadBar), 4f);
            Image reloadFill = UiFactory.Fill(reloadFillRt, UiFactory.Sprite("UI_BarFillRed"), new Color(1f, 0.72f, 0.35f, 1f), 0f);
            reload.gameObject.SetActive(false);

            // Settings: the gear button (plate + gear), touch area >= 48 dp.
            float touchInset = (UiFactory.c_MinTouchTarget - buttonSize) * 0.5f;
            float hit = Mathf.Max(buttonSize, UiFactory.c_MinTouchTarget);
            RectTransform gearRt = UiFactory.Rect("SettingsButton", safe, topRight, topRight,
                new Vector2(-(margin - Mathf.Max(0f, touchInset)), -(margin - Mathf.Max(0f, touchInset))), new Vector2(hit, hit));
            UiFactory.Image(gearRt, null, Color.clear, false, true);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform gearVisual = UiFactory.Rect("Visual", gearRt, center, center, Vector2.zero, new Vector2(buttonSize, buttonSize * 383f / 390f));
            Image gearPlate = UiFactory.Image(gearVisual, UiFactory.Sprite("UI_GearButton"), Color.white, false);
            RectTransform gearIcon = UiFactory.Rect("Icon", gearVisual, center, center, Vector2.zero, new Vector2(buttonSize * 0.66f, buttonSize * 0.66f));
            UiFactory.Image(gearIcon, UiFactory.Sprite("UI_GearIcon"), Color.white, false);
            var settingsButton = gearRt.gameObject.AddComponent<Button>();
            settingsButton.targetGraphic = gearPlate;
            gearRt.gameObject.AddComponent<UIButtonFx>();

            // Scrap and kills under the health panel; the interaction prompt at the bottom.
            RectTransform moneyPanel = BuildRunCounters(safe, outline, margin + hpHeight + 12f, out TextMeshProUGUI moneyValue,
                out TextMeshProUGUI killsValue);
            BuildInteractionPrompt(safe, outline);
            BuildWaveBanner(safe, outline);
            BuildWeaponSlots(safe, outline);

            BuildTouchControls(safe, outline);
            BuildLevelUpScreen(safe, outline);
            BuildWorkshopScreen(safe, outline);
            BuildGameOverScreen(safe, outline);
            SettingsPanel settings = BuildSettingsPopup(safe, outline, true);

            var hud = canvas.gameObject.AddComponent<HudController>();
            PrefabBuilder.Wire(hud,
                ("m_PlayerStats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_WaveState", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_AmmoState", DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState)),
                ("m_Run", Run), ("m_MoneyValue", moneyValue), ("m_KillsValue", killsValue), ("m_MoneyPanel", moneyPanel),
                ("m_HealthFill", healthFill), ("m_HealthValue", healthValue), ("m_LevelFill", levelFill), ("m_LevelLabel", levelLabel),
                ("m_WaveLabel", waveLabel), ("m_WaveTimer", waveTimer), ("m_AmmoValue", ammoValue), ("m_AmmoPanel", ammo),
                ("m_ReloadHint", reload.gameObject),
                ("m_ReloadFill", reloadFill), ("m_SettingsButton", settingsButton), ("m_SettingsPanel", settings));
            // Every HUD text gets the black outline + shadow: the panels are translucent over a bright map.
            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                text.fontSharedMaterial = outline;
            ShowStartState(hud);
            SaveUiPrefab(canvas.gameObject, "HUD");
            UiFactory.EventSystem();
            EditorSceneManager.SaveScene(scene, c_HudScene);
        }

        // ------------------------------------------------------------------ touch controls (mobile)
        private const float c_MobileSettingsRowHeight = 110f;
        private const float c_HudReserve = 270f;           // the top HUD panels + counters: controls stay below
        private const float c_StickTouch = 380f;           // touch area of a stick
        private const float c_StickRing = 260f;
        private const float c_StickKnob = 120f;
        private const float c_TouchButton = 150f;

        /// <summary>
        /// The mobile controls layer (hidden on desktop by <see cref="MobileControlsLayout"/>): move stick bottom-left,
        /// aim / fire stick bottom-right, reload button above it, all inside the area below the top HUD; plus the
        /// edit overlay (frame of the allowed area, hint, Reset / Done) used to move them.
        /// </summary>
        private static void BuildTouchControls(RectTransform safe, Material outline)
        {
            RectTransform root = UiFactory.Stretch("TouchControls", safe);
            RectTransform area = UiFactory.Stretch("Area", root);
            area.offsetMin = new Vector2(20f, 20f);
            area.offsetMax = new Vector2(-20f, -c_HudReserve);

            var bottomLeft = new Vector2(0f, 0f);
            var bottomRight = new Vector2(1f, 0f);
            var layout = root.gameObject.AddComponent<MobileControlsLayout>();
            RectTransform move = Stick("MoveStick", area, bottomLeft, new Vector2(c_StickTouch * 0.5f, c_StickTouch * 0.5f),
                VirtualJoystick.Role.Move, Color.white, layout);
            RectTransform aim = Stick("AimStick", area, bottomRight, new Vector2(-c_StickTouch * 0.5f, c_StickTouch * 0.5f),
                VirtualJoystick.Role.Aim, new Color(1f, 0.62f, 0.3f, 1f), layout);
            RectTransform reload = UiFactory.Rect("ReloadButton", area, bottomRight, new Vector2(0.5f, 0.5f),
                new Vector2(-c_StickTouch - 30f, c_StickTouch * 0.5f + 150f), new Vector2(c_TouchButton, c_TouchButton));
            UiFactory.Image(reload, UiFactory.Sprite("UI_RoundButton"), Color.white, false, true);
            RectTransform reloadIcon = UiFactory.Rect("Icon", reload, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(c_TouchButton * 0.5f, c_TouchButton * 0.5f));
            UiFactory.Image(reloadIcon, UiFactory.Sprite("UI_AmmoIcon"), Color.white, false).preserveAspect = true;
            PrefabBuilder.Wire(reload.gameObject.AddComponent<TouchButton>(), ("m_Layout", layout));

            // Action (interact): above the aim stick; shows the pictogram of what can be used and lights up then.
            RectTransform action = UiFactory.Rect("ActionButton", area, bottomRight, new Vector2(0.5f, 0.5f),
                new Vector2(-c_StickTouch * 0.5f, c_StickTouch + 100f), new Vector2(c_TouchButton, c_TouchButton));
            UiFactory.Image(action, null, Color.clear, false, true);
            action.gameObject.AddComponent<CanvasGroup>();
            RectTransform actionVisual = UiFactory.Stretch("Visual", action);
            UiFactory.Image(actionVisual, UiFactory.Sprite("UI_RoundButton"), Color.white, false);
            RectTransform actionIcon = UiFactory.Rect("Icon", actionVisual, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(c_TouchButton * 0.6f, c_TouchButton * 0.6f));
            Image actionImage = UiFactory.Image(actionIcon, UiFactory.Sprite("UI_Icon_Repair"), Color.white, false);
            actionImage.preserveAspect = true;
            var actionButton = action.gameObject.AddComponent<TouchButton>();
            PrefabBuilder.Wire(actionButton, ("m_Layout", layout));
            CharacterBuilder.SetProps(actionButton, ("m_Action", (int)TouchButton.Action.Interact));
            PrefabBuilder.Wire(action.gameObject.AddComponent<InteractButtonView>(),
                ("m_State", DataAssetsBuilder.Load<ZombiCar.Progression.InteractionState>(DataAssetsBuilder.c_InteractionState)),
                ("m_Layout", layout), ("m_Visual", actionVisual), ("m_Icon", actionImage));

            // Weapon (gun / axe): under the reload button; shows the weapon it switches to.
            RectTransform weapon = UiFactory.Rect("WeaponButton", area, bottomRight, new Vector2(0.5f, 0.5f),
                new Vector2(-c_StickTouch - 30f, c_StickTouch * 0.5f - 10f), new Vector2(c_TouchButton, c_TouchButton));
            UiFactory.Image(weapon, UiFactory.Sprite("UI_RoundButton"), Color.white, false, true);
            RectTransform weaponIcon = UiFactory.Rect("Icon", weapon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(c_TouchButton * 0.62f, c_TouchButton * 0.62f));
            Image weaponImage = UiFactory.Image(weaponIcon, UiFactory.Sprite("UI_Icon_Axe"), Color.white, false);
            weaponImage.preserveAspect = true;
            var weaponButton = weapon.gameObject.AddComponent<TouchButton>();
            PrefabBuilder.Wire(weaponButton, ("m_Layout", layout));
            CharacterBuilder.SetProps(weaponButton, ("m_Action", (int)TouchButton.Action.SwitchWeapon));
            PrefabBuilder.Wire(weapon.gameObject.AddComponent<WeaponButtonView>(),
                ("m_State", DataAssetsBuilder.Load<WeaponState>(DataAssetsBuilder.c_WeaponState)), ("m_Icon", weaponImage),
                ("m_GunIcon", UiFactory.Sprite("UI_Icon_Rifle")), ("m_AxeIcon", UiFactory.Sprite("UI_Icon_Axe")));

            // Edit overlay: dims the game, shows where the controls may go, takes the drags.
            RectTransform overlay = UiFactory.Stretch("EditOverlay", root);
            UiFactory.Image(overlay, null, new Color(0f, 0f, 0f, 0.45f), false, true);
            RectTransform frame = UiFactory.Stretch("AllowedArea", overlay);
            frame.offsetMin = area.offsetMin;
            frame.offsetMax = area.offsetMax;
            UiFactory.Image(frame, UiFactory.Sprite("UI_Panel"), new Color(1f, 0.85f, 0.6f, 0.18f), true);
            var top = new Vector2(0.5f, 1f);
            OutlinedLabel("Hint", overlay, top, top, new Vector2(0f, -c_HudReserve - 20f), new Vector2(1300f, 60f),
                "ПЕРЕТАЩИТЕ ДЖОЙСТИКИ И КНОПКИ", 40f, TextAlignmentOptions.Center, s_ButtonLabel, outline);
            Button reset = HudButton("ResetButton", overlay, top, new Vector2(-190f, -c_HudReserve - 100f), new Vector2(340f, 100f),
                "СБРОС", 42f, Color.white, outline);
            Button done = HudButton("DoneButton", overlay, top, new Vector2(190f, -c_HudReserve - 100f), new Vector2(340f, 100f),
                "ГОТОВО", 42f, s_PlayColor, outline);

            var so = new SerializedObject(layout);
            so.FindProperty("m_Area").objectReferenceValue = area;
            SerializedProperty controls = so.FindProperty("m_Controls");
            (string id, RectTransform rect)[] list = { ("move", move), ("aim", aim), ("reload", reload), ("action", action), ("weapon", weapon) };
            controls.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                SerializedProperty c = controls.GetArrayElementAtIndex(i);
                c.FindPropertyRelative("Id").stringValue = list[i].id;
                c.FindPropertyRelative("Rect").objectReferenceValue = list[i].rect;
            }
            so.FindProperty("m_EditOverlay").objectReferenceValue = overlay.gameObject;
            so.FindProperty("m_DoneButton").objectReferenceValue = done;
            so.FindProperty("m_ResetButton").objectReferenceValue = reset;
            so.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
        }

        private static RectTransform Stick(string name, RectTransform area, Vector2 anchor, Vector2 position, VirtualJoystick.Role role,
            Color knobTint, MobileControlsLayout layout)
        {
            var center = new Vector2(0.5f, 0.5f);
            RectTransform rt = UiFactory.Rect(name, area, anchor, center, position, new Vector2(c_StickTouch, c_StickTouch));
            UiFactory.Image(rt, null, Color.clear, false, true);                 // the (larger) touch area
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.75f;
            RectTransform ring = UiFactory.Rect("Ring", rt, center, center, Vector2.zero, new Vector2(c_StickRing, c_StickRing));
            UiFactory.Image(ring, UiFactory.Sprite("UI_StickRing"), Color.white, false);
            RectTransform knob = UiFactory.Rect("Knob", ring, center, center, Vector2.zero, new Vector2(c_StickKnob, c_StickKnob));
            UiFactory.Image(knob, UiFactory.Sprite("UI_StickKnob"), knobTint, false);
            var stick = rt.gameObject.AddComponent<VirtualJoystick>();
            var so = new SerializedObject(stick);
            so.FindProperty("m_Role").enumValueIndex = (int)role;
            so.FindProperty("m_Ring").objectReferenceValue = ring;
            so.FindProperty("m_Knob").objectReferenceValue = knob;
            so.FindProperty("m_Layout").objectReferenceValue = layout;
            so.ApplyModifiedPropertiesWithoutUndo();
            return rt;
        }

        // Opaque boxes of the references (px): parts are placed by their reference coordinates inside these.
        private static readonly RectInt s_HpRef = new RectInt(64, 70, 2006, 565);
        private static readonly RectInt s_WaveRef = new RectInt(108, 142, 1558, 613);
        private static readonly RectInt s_AmmoRef = new RectInt(50, 170, 1660, 383);

        /// <summary>A child rect covering reference pixels (x0, y0)-(x1, y1) of <paramref name="reference"/> (y down).</summary>
        private static RectTransform PartRect(RectTransform parent, string name, int x0, int y0, int x1, int y1, RectInt reference)
        {
            RectTransform rt = UiFactory.Stretch(name, parent);
            rt.anchorMin = new Vector2((x0 - reference.x) / (float)reference.width, 1f - (y1 - reference.y) / (float)reference.height);
            rt.anchorMax = new Vector2((x1 - reference.x) / (float)reference.width, 1f - (y0 - reference.y) / (float)reference.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image Part(RectTransform parent, string name, string sprite, int x0, int y0, int x1, int y1, RectInt reference) =>
            UiFactory.Image(PartRect(parent, name, x0, y0, x1, y1, reference), UiFactory.Sprite(sprite), Color.white, false);

        private static TextMeshProUGUI PartLabel(RectTransform parent, string name, int x0, int y0, int x1, int y1, RectInt reference,
            string text, float size, TextAlignmentOptions align, Material outline)
        {
            TextMeshProUGUI t = UiFactory.Text(PartRect(parent, name, x0, y0, x1, y1, reference), text, size, align, Color.white);
            t.fontSharedMaterial = outline;
            t.enableAutoSizing = true;                   // live values of any length stay inside their slot
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.5f;
            return t;
        }
    }
}
