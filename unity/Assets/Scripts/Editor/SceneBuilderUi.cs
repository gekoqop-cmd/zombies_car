using System.Collections.Generic;
using System.Linq;
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
            // Cases and skins share one row: two half buttons.
            const float halfWidth = (buttonWidth - 16f) * 0.5f;
            Button casesButton = HudButton("CasesButton", main, topRight, new Vector2(columnX - halfWidth - 16f, y - (buttonHeight + 24f) * 2f),
                new Vector2(halfWidth, buttonHeight), "КЕЙСЫ", 40f, new Color(1f, 0.82f, 0.35f, 1f), outline);
            Button skinsButton = HudButton("SkinsButton", main, topRight, new Vector2(columnX, y - (buttonHeight + 24f) * 2f),
                new Vector2(halfWidth, buttonHeight), "СКИНЫ", 40f, new Color(0.72f, 0.5f, 1f, 1f), outline);
            Button leadersButton = HudButton("LeaderboardButton", main, topRight, new Vector2(columnX, y - (buttonHeight + 24f) * 3f),
                new Vector2(buttonWidth, buttonHeight), "ТАБЛИЦА ЛИДЕРОВ", 34f, new Color(0.3f, 0.56f, 0.9f, 1f), outline);
            // The coins for the cases: the gold coin and the number, centred under the column.
            const float coinsWidth = 240f;
            TextMeshProUGUI coins = CoinCounter("Coins", main, topRight, topRight,
                new Vector2(columnX - (buttonWidth - coinsWidth) * 0.5f, y - (buttonHeight + 24f) * 4f + 6f), 56f, coinsWidth, "0", 40f, outline);

            // Map selection.
            RectTransform mapScreen = UiFactory.Stretch("MapScreen", safe);
            var mapGroup = mapScreen.gameObject.AddComponent<CanvasGroup>();
            mapGroup.alpha = 0f;
            MapRecordsPanel records = BuildMapRecordsPanel(safe, outline);          // over the map screen
            Button back = BuildMapSelection(mapScreen, outline, records);
            mapScreen.gameObject.SetActive(false);

            CasesScreen cases = BuildCasesScreen(safe, outline);
            SettingsPanel settings = BuildSettingsPopup(canvas.transform, outline, false);
            BuildTutorialCards(safe, outline);                         // replayed from the settings
            LeaderboardPanel leaders = BuildLeaderboardPanel(safe, outline);
            SkinsPanel skins = BuildSkinsPanel(safe, outline, skinsButton);

            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            PrefabBuilder.Wire(menu, ("m_PlayButton", play), ("m_SettingsButton", settingsButton), ("m_BackButton", back),
                ("m_MainScreen", mainGroup), ("m_MapScreen", mapGroup), ("m_Dim", dim), ("m_Settings", settings),
                ("m_Music", AudioBuilder.Music("Music_Menu")), ("m_CasesButton", casesButton), ("m_Cases", cases),
                ("m_CoinsLabel", coins), ("m_LeaderboardButton", leadersButton), ("m_Leaderboard", leaders),
                ("m_SkinsButton", skinsButton), ("m_Skins", skins));
            SaveUiPrefab(canvas.gameObject, "MainMenu");
            UiFactory.EventSystem();
            AudioBuilder.AddAudio();
            EditorSceneManager.SaveScene(scene, c_MenuScene);
        }

        private static Button BuildMapSelection(RectTransform screen, Material outline, MapRecordsPanel records)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            // Plain warm-dark backdrop (also the swipe area); it stays the same for Day and Night.
            UiFactory.Image(UiFactory.Stretch("Tint", screen), null, new Color(0.16f, 0.1f, 0.07f, 0.45f), false, true);
            OutlinedLabel("Title", screen, top, top, new Vector2(0f, -40f), new Vector2(1200f, 90f), "ВЫБОР КАРТЫ", 64f,
                TextAlignmentOptions.Center, s_ButtonLabel, outline);

            RectTransform strip = UiFactory.Rect("Cards", screen, center, center, new Vector2(0f, -10f), new Vector2(10f, 10f));
            MapCard city = BuildMapCard(strip, 0, "ГОРОД", outline,
                DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_MapConfig), DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_NightMapConfig),
                records);
            MapCard farm = BuildMapCard(strip, 1, "ФЕРМА", outline, null, null, null);

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

        private static MapCard BuildMapCard(RectTransform strip, int index, string title, Material outline, MapConfig day, MapConfig night,
            MapRecordsPanel records)
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
            RectTransform badge = null;
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
                // "ПРОЙДЕНО" stamped in the corner once the map shown (day or night) was beaten.
                var topRight = new Vector2(1f, 1f);
                badge = UiFactory.Rect("BeatenBadge", picture, topRight, center, new Vector2(-170f, -70f), new Vector2(340f, 92f));
                badge.localRotation = Quaternion.Euler(0f, 0f, -10f);
                UiFactory.Image(badge, UiFactory.Sprite("UI_Button"), new Color(0.3f, 0.75f, 0.3f, 1f), true);
                OutlinedLabel("Label", badge, center, center, Vector2.zero, new Vector2(320f, 88f), "ПРОЙДЕНО", 54f,
                    TextAlignmentOptions.Center, Color.white, outline);
                badge.gameObject.SetActive(false);
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
            // "РЕКОРДЫ" hangs under the card (moves with it when the cards slide).
            Button recordsButton = null;
            if (!locked)
            {
                recordsButton = HudButton("RecordsButton", card, bottom, new Vector2(0f, -92f), new Vector2(400f, 76f), "РЕКОРДЫ", 34f,
                    new Color(1f, 0.78f, 0.28f, 1f), outline);
                var recordsRt = (RectTransform)recordsButton.transform;
                recordsRt.pivot = bottom;
                RectTransform trophy = UiFactory.Rect("Trophy", recordsRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f),
                    new Vector2(58f, 58f));
                UiFactory.Image(trophy, UiFactory.Sprite("UI_Icon_Trophy"), Color.white, false).preserveAspect = true;
            }
            var mapCard = card.gameObject.AddComponent<MapCard>();
            PrefabBuilder.Wire(mapCard, ("m_DayMap", day), ("m_NightMap", night), ("m_NightPicture", nightGroup),
                ("m_DayButton", dayButton), ("m_NightButton", nightButton), ("m_DayButtonPlate", dayPlate),
                ("m_NightButtonPlate", nightPlate), ("m_PlayButton", play),
                ("m_BeatenBadge", badge != null ? badge.gameObject : null), ("m_RecordsButton", recordsButton), ("m_Records", records));
            var cardSo = new SerializedObject(mapCard);
            cardSo.FindProperty("m_Title").stringValue = title;
            cardSo.ApplyModifiedPropertiesWithoutUndo();
            return mapCard;
        }

        // ------------------------------------------------------------------ map records
        private static readonly Vector2 s_RecordTile = new Vector2(330f, 140f);

        /// <summary>
        /// "РЕКОРДЫ" window (hidden): the map and time of day, six picture tiles in two rows (best time, victories, best
        /// wave / most zombies, most bosses, runs) and "ЗАКРЫТЬ"; a click beside the window closes it too.
        /// </summary>
        private static MapRecordsPanel BuildMapRecordsPanel(RectTransform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform root = UiFactory.Stretch("MapRecords", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Image dim = UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.7f), false, true);
            var backdrop = root.gameObject.AddComponent<Button>();
            backdrop.targetGraphic = dim;
            backdrop.transition = Selectable.Transition.None;
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, new Vector2(1140f, 660f));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            RectTransform trophy = UiFactory.Rect("Trophy", window, top, center, new Vector2(-250f, -70f), new Vector2(84f, 84f));
            UiFactory.Image(trophy, UiFactory.Sprite("UI_Icon_Trophy"), Color.white, false).preserveAspect = true;
            OutlinedLabel("Title", window, top, top, new Vector2(40f, -28f), new Vector2(460f, 84f), "РЕКОРДЫ", 64f,
                TextAlignmentOptions.Center, new Color(1f, 0.8f, 0.3f, 1f), outline);
            TextMeshProUGUI subtitle = OutlinedLabel("Subtitle", window, top, top, new Vector2(0f, -112f), new Vector2(900f, 44f),
                "ГОРОД  •  ДЕНЬ", 32f, TextAlignmentOptions.Center, new Color(0.82f, 0.8f, 0.76f, 1f), outline);

            (string icon, string caption, Color color)[] specs =
            {
                ("UI_Icon_Stopwatch", "ЛУЧШЕЕ ВРЕМЯ", new Color(1f, 0.85f, 0.4f, 1f)),
                ("UI_Icon_Trophy", "ПОБЕД", new Color(1f, 0.85f, 0.4f, 1f)),
                ("UI_Icon_Flag", "ЛУЧШАЯ ВОЛНА", Color.white),
                ("UI_Icon_Skull", "ЗОМБИ ЗА ЗАБЕГ", Color.white),
                ("UI_Icon_Boss", "БОССОВ ЗА ЗАБЕГ", new Color(1f, 0.55f, 0.45f, 1f)),
                ("UI_Icon_Reload", "ЗАБЕГОВ", new Color(0.6f, 0.9f, 1f, 1f)),
            };
            var tiles = new RectTransform[specs.Length];
            var values = new TextMeshProUGUI[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                float x = (i % 3 - 1) * (s_RecordTile.x + 24f);
                float y = -250f - (i / 3) * (s_RecordTile.y + 22f);
                tiles[i] = RecordTile(window, new Vector2(x, y), specs[i].icon, specs[i].caption, specs[i].color, outline, out values[i]);
            }
            Button close = HudButton("CloseButton", window, bottom, new Vector2(0f, 30f), new Vector2(340f, 92f), "ЗАКРЫТЬ", 40f,
                s_PlayColor, outline);
            ((RectTransform)close.transform).pivot = bottom;

            var panel = root.gameObject.AddComponent<MapRecordsPanel>();
            PrefabBuilder.Wire(panel, ("m_Group", group), ("m_Window", window), ("m_Subtitle", subtitle), ("m_Time", values[0]),
                ("m_Wins", values[1]), ("m_Wave", values[2]), ("m_Kills", values[3]), ("m_Bosses", values[4]), ("m_Runs", values[5]),
                ("m_CloseButton", close), ("m_Backdrop", backdrop),
                ("m_Waves", DataAssetsBuilder.Load<ZombiCar.Waves.WaveConfig>(DataAssetsBuilder.c_WaveConfig)));
            CharacterBuilder.SetProps(panel, ("m_Tiles", tiles.Cast<Object>().ToArray()));
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            return panel;
        }

        private static RectTransform RecordTile(RectTransform parent, Vector2 pos, string icon, string caption, Color valueColor,
            Material outline, out TextMeshProUGUI value)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            var bottomLeft = new Vector2(0f, 0f);
            RectTransform tile = UiFactory.Rect(caption, parent, top, new Vector2(0.5f, 0.5f), pos, s_RecordTile);
            UiFactory.Image(tile, UiFactory.Sprite("UI_Panel"), new Color(0.03f, 0.03f, 0.05f, 0.7f), true);
            RectTransform iconRt = UiFactory.Rect("Icon", tile, midLeft, midLeft, new Vector2(16f, 6f), new Vector2(96f, 96f));
            UiFactory.Image(iconRt, UiFactory.Sprite(icon), Color.white, false).preserveAspect = true;
            value = OutlinedLabel("Value", tile, topLeft, topLeft, new Vector2(124f, -14f), new Vector2(196f, 70f), "0", 58f,
                TextAlignmentOptions.Left, valueColor, outline);
            value.enableAutoSizing = true;
            value.fontSizeMin = 28f;
            value.fontSizeMax = 58f;
            value.richText = true;
            TextMeshProUGUI label = OutlinedLabel("Caption", tile, bottomLeft, bottomLeft, new Vector2(124f, 14f), new Vector2(196f, 40f),
                caption, 22f, TextAlignmentOptions.Left, new Color(0.82f, 0.8f, 0.76f, 1f), outline);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 22f;
            return tile;
        }

        // ------------------------------------------------------------------ skins ("СКИНЫ")
        private const int c_CharacterCards = 5;
        private static readonly Vector2 s_CharacterCard = new Vector2(220f, 330f);
        private static readonly string[] s_SkinTabs = { "ПЕРСОНАЖИ", "ОРУЖИЕ", "ТОПОР", "ЭФФЕКТ ПУЛЬ" };
        private static readonly Color s_TabOff = new Color(0.14f, 0.15f, 0.19f, 0.95f);

        /// <summary>
        /// "СКИНЫ" window (hidden): four tabs (ПЕРСОНАЖИ, ОРУЖИЕ, ТОПОР, ЭФФЕКТ ПУЛЬ) with a count of new skins on the
        /// collection ones; the characters page (the hero's card with the breathing portrait, four dark "?" cards and the
        /// "В РАЗРАБОТКЕ :)" bubble) and the collection page (a hint and a 5-column grid of skin tiles); "ЗАКРЫТЬ" or a
        /// click beside the window closes it. The count of all new skins also goes on the menu's СКИНЫ button.
        /// </summary>
        private static SkinsPanel BuildSkinsPanel(RectTransform parent, Material outline, Button menuButton)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform root = UiFactory.Stretch("SkinsPanel", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Image dim = UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.7f), false, true);
            var backdrop = root.gameObject.AddComponent<Button>();
            backdrop.targetGraphic = dim;
            backdrop.transition = Selectable.Transition.None;
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, new Vector2(1340f, 850f));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            OutlinedLabel("Title", window, top, top, new Vector2(0f, -20f), new Vector2(900f, 80f), "СКИНЫ", 62f,
                TextAlignmentOptions.Center, new Color(0.8f, 0.65f, 1f, 1f), outline);

            // Tabs.
            var tabs = new Button[s_SkinTabs.Length];
            var plates = new Image[s_SkinTabs.Length];
            var badges = new GameObject[s_SkinTabs.Length];
            var counts = new TMP_Text[s_SkinTabs.Length];
            for (int i = 0; i < s_SkinTabs.Length; i++)
            {
                float x = (i - (s_SkinTabs.Length - 1) * 0.5f) * 306f;
                RectTransform tab = UiFactory.Rect($"Tab{i}", window, top, top, new Vector2(x, -108f), new Vector2(290f, 70f));
                plates[i] = UiFactory.Image(tab, UiFactory.Sprite("UI_Button"), s_TabOff, true, true);
                TextMeshProUGUI tabLabel = OutlinedLabel("Label", tab, center, center, Vector2.zero, new Vector2(260f, 66f), s_SkinTabs[i], 30f,
                    TextAlignmentOptions.Center, Color.white, outline);
                tabLabel.enableAutoSizing = true;
                tabLabel.fontSizeMin = 20f;
                tabLabel.fontSizeMax = 30f;
                tabs[i] = tab.gameObject.AddComponent<Button>();
                tabs[i].targetGraphic = plates[i];
                (badges[i], counts[i]) = CountBadge(tab, outline);
            }

            // Characters page.
            RectTransform characters = UiFactory.Stretch("CharactersPage", window);
            Button hero = null;
            var locked = new Button[c_CharacterCards - 1];
            for (int i = 0; i < c_CharacterCards; i++)
            {
                float x = (i - (c_CharacterCards - 1) * 0.5f) * (s_CharacterCard.x + 28f);
                RectTransform card = UiFactory.Rect(i == 0 ? "Hero" : $"Locked{i}", characters, top, top, new Vector2(x, -250f), s_CharacterCard);
                Button button = i == 0 ? HeroCard(card, outline) : LockedCard(card, outline);
                if (i == 0)
                    hero = button;
                else
                    locked[i - 1] = button;
            }

            // Collection page.
            RectTransform collection = UiFactory.Stretch("CollectionPage", window);
            OutlinedLabel("Hint", collection, top, top, new Vector2(0f, -196f), new Vector2(1000f, 40f), "НАЖМИТЕ НА СКИН, ЧТОБЫ НАДЕТЬ", 28f,
                TextAlignmentOptions.Center, s_SubtitleColor, outline);
            RectTransform grid = UiFactory.Rect("Grid", collection, top, top, new Vector2(0f, -248f), new Vector2(1220f, 460f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(c_TileSize, c_TileSize);
            layout.spacing = new Vector2(20f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            ItemTile template = BuildItemTile(grid, "TileTemplate", outline, true);
            collection.gameObject.SetActive(false);

            // The bubble: placed over the tapped "?" card by the panel.
            RectTransform bubble = UiFactory.Rect("Bubble", window, center, bottom, Vector2.zero, new Vector2(380f, 74f));
            var bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.blocksRaycasts = bubbleGroup.interactable = false;
            UiFactory.Image(bubble, UiFactory.Sprite("UI_Button"), new Color(1f, 0.6f, 0.15f, 1f), true);
            TextMeshProUGUI bubbleText = OutlinedLabel("Text", bubble, center, center, Vector2.zero, new Vector2(360f, 70f), "В РАЗРАБОТКЕ :)",
                34f, TextAlignmentOptions.Center, Color.white, outline);

            Button close = HudButton("CloseButton", window, bottom, new Vector2(0f, 26f), new Vector2(340f, 92f), "ЗАКРЫТЬ", 40f,
                s_PlayColor, outline);
            ((RectTransform)close.transform).pivot = bottom;

            (GameObject menuBadge, TMP_Text menuCount) = CountBadge((RectTransform)menuButton.transform, outline);

            var panel = root.gameObject.AddComponent<SkinsPanel>();
            PrefabBuilder.Wire(panel, ("m_Catalog", CosmeticsBuilder.Catalog), ("m_Group", group), ("m_Window", window),
                ("m_CloseButton", close), ("m_Backdrop", backdrop), ("m_MenuBadge", menuBadge), ("m_MenuBadgeCount", menuCount),
                ("m_CharactersPage", characters.gameObject), ("m_HeroCard", hero), ("m_Bubble", bubbleGroup), ("m_BubbleText", bubbleText),
                ("m_CollectionPage", collection.gameObject), ("m_Grid", grid), ("m_TileTemplate", template));
            CharacterBuilder.SetProps(panel, ("m_LockedCards", locked.Cast<Object>().ToArray()), ("m_Tabs", tabs.Cast<Object>().ToArray()),
                ("m_TabPlates", plates.Cast<Object>().ToArray()), ("m_TabBadges", badges.Cast<Object>().ToArray()),
                ("m_TabBadgeCounts", counts.Cast<Object>().ToArray()),
                ("m_SlotIcons", new Object[] { UiFactory.Sprite("UI_Icon_Rifle"), UiFactory.Sprite("UI_Icon_Axe"), UiFactory.Sprite("UI_AmmoIcon") }));
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            return panel;
        }

        // A small orange disc with a number in the top right corner of a button (new skins).
        private static (GameObject, TMP_Text) CountBadge(RectTransform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var topRight = new Vector2(1f, 1f);
            RectTransform badge = UiFactory.Rect("NewBadge", parent, topRight, center, new Vector2(-10f, -8f), new Vector2(46f, 46f));
            UiFactory.Image(badge, UiFactory.Sprite("UI_StickKnob"), new Color(1f, 0.45f, 0.1f, 1f), false);
            TextMeshProUGUI count = OutlinedLabel("Count", badge, center, center, Vector2.zero, new Vector2(46f, 46f), "1", 28f,
                TextAlignmentOptions.Center, Color.white, outline);
            badge.gameObject.SetActive(false);
            return (badge.gameObject, count);
        }

        // The hero: the HUD's breathing portrait in the metal frame, his name and "ВЫБРАН".
        private static Button HeroCard(RectTransform card, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            Image plate = UiFactory.Image(card, UiFactory.Sprite("UI_Panel"), new Color(0.93f, 0.47f, 0.13f, 1f), true, true);
            RectTransform inner = UiFactory.Stretch("Inner", card);
            inner.offsetMin = new Vector2(6f, 6f);
            inner.offsetMax = new Vector2(-6f, -6f);
            UiFactory.Image(inner, UiFactory.Sprite("UI_Panel"), new Color(0.08f, 0.09f, 0.12f, 1f), true);
            const float w = 196f, h = w * 472f / 533f;                     // the metal frame's aspect
            RectTransform portrait = UiFactory.Rect("Portrait", card, top, top, new Vector2(0f, -14f), new Vector2(w, h));
            Image back = UiFactory.Image(portrait, UiFactory.Sprite("UI_HpPortraitBack"), Color.white, false);
            back.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Sprite[] frames = Enumerable.Range(0, 64).Select(i => UiFactory.Sprite($"UI_PortraitIdle_{i:00}")).TakeWhile(s => s != null).ToArray();
            Image face = UiFactory.Image(UiFactory.Stretch("Face", portrait), frames.FirstOrDefault(), Color.white, false);
            CharacterBuilder.SetProps(face.gameObject.AddComponent<UiFrameAnimation>(), ("m_Frames", frames.Cast<Object>().ToArray()), ("m_Fps", 8f));
            UiFactory.Image(UiFactory.Stretch("Frame", portrait), UiFactory.Sprite("UI_HpFrameMetal"), Color.white, false);
            OutlinedLabel("Name", card, top, top, new Vector2(0f, -200f), new Vector2(206f, 44f), "ВЫЖИВШИЙ", 32f,
                TextAlignmentOptions.Center, Color.white, outline).enableAutoSizing = true;
            RectTransform badge = UiFactory.Rect("Selected", card, bottom, bottom, new Vector2(0f, 18f), new Vector2(170f, 52f));
            UiFactory.Image(badge, UiFactory.Sprite("UI_Button"), new Color(0.3f, 0.75f, 0.3f, 1f), true);
            OutlinedLabel("Label", badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 52f), "ВЫБРАН",
                30f, TextAlignmentOptions.Center, Color.white, outline);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            card.gameObject.AddComponent<UIButtonFx>();
            return button;
        }

        // A character still to come: a dark card with a big "?".
        private static Button LockedCard(RectTransform card, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var bottom = new Vector2(0.5f, 0f);
            Image plate = UiFactory.Image(card, UiFactory.Sprite("UI_Panel"), new Color(0.2f, 0.21f, 0.25f, 1f), true, true);
            RectTransform inner = UiFactory.Stretch("Inner", card);
            inner.offsetMin = new Vector2(6f, 6f);
            inner.offsetMax = new Vector2(-6f, -6f);
            UiFactory.Image(inner, UiFactory.Sprite("UI_Panel"), new Color(0.06f, 0.06f, 0.08f, 1f), true);
            OutlinedLabel("Question", card, center, center, new Vector2(0f, 30f), new Vector2(200f, 200f), "?", 170f,
                TextAlignmentOptions.Center, new Color(0.55f, 0.56f, 0.62f, 1f), outline);
            OutlinedLabel("Name", card, bottom, bottom, new Vector2(0f, 22f), new Vector2(200f, 44f), "???", 32f,
                TextAlignmentOptions.Center, new Color(0.55f, 0.56f, 0.62f, 1f), outline);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            button.colors = colors;
            return button;
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

        private static readonly Color s_AdGold = new Color(1f, 0.8f, 0.32f, 1f);

        /// <summary>
        /// The one look of every "reward for a rewarded ad" button: a gold plate with the shared UI_Icon_Ad disc (TV +
        /// play on gold) and, where the reward needs a word, a short caption beside it ("+200", "×2", "ВОСКРЕСНУТЬ") —
        /// never the word "реклама".
        /// </summary>
        private static Button AdButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string caption,
            float fontSize, Material outline)
        {
            RectTransform rt = UiFactory.Rect(name, parent, anchor, anchor, pos, size);
            Image plate = UiFactory.Image(rt, UiFactory.Sprite("UI_GearButton"), s_AdGold, true, true);
            float icon = size.y * 0.78f;
            var center = new Vector2(0.5f, 0.5f);
            bool text = !string.IsNullOrEmpty(caption);
            var iconAnchor = text ? new Vector2(0f, 0.5f) : center;
            RectTransform iconRt = UiFactory.Rect("AdIcon", rt, iconAnchor, center, text ? new Vector2(12f + icon * 0.5f, 0f) : Vector2.zero,
                new Vector2(icon, icon));
            UiFactory.Image(iconRt, UiFactory.Sprite("UI_Icon_Ad"), Color.white, false).preserveAspect = true;
            if (text)
            {
                TextMeshProUGUI label = OutlinedLabel("Label", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f + icon, 0f),
                    new Vector2(size.x - icon - 34f, size.y), caption, fontSize, TextAlignmentOptions.Center, s_ButtonLabel, outline);
                label.enableAutoSizing = true;
                label.fontSizeMin = fontSize * 0.55f;
                label.fontSizeMax = fontSize;
            }
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.9f);
            button.colors = colors;
            rt.gameObject.AddComponent<UIButtonFx>();
            return button;
        }

        /// <summary>A counter: the gold coin icon and the number beside it (no "МОНЕТЫ" word). Returns the number's label.</summary>
        private static TextMeshProUGUI CoinCounter(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float height,
            float width, string value, float fontSize, Material outline)
        {
            RectTransform rt = UiFactory.Rect(name, parent, anchor, pivot, pos, new Vector2(width, height));
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform icon = UiFactory.Rect("Icon", rt, midLeft, midLeft, Vector2.zero, new Vector2(height, height));
            UiFactory.Image(icon, UiFactory.Sprite("UI_Icon_Coin"), Color.white, false).preserveAspect = true;
            return OutlinedLabel("Value", rt, midLeft, midLeft, new Vector2(height + 12f, 0f), new Vector2(width - height - 12f, height), value,
                fontSize, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f), outline);
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
            // In the game (the pause): the coin balance, top left — the HUD itself never shows it during a run.
            TextMeshProUGUI coins = menuButton
                ? CoinCounter("Coins", window, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -46f), 56f, 230f, "0", 40f, outline)
                : null;

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
            // Bottom row: "Tutorial" (replays the intro cards), in the game also "To menu", and Back.
            var buttonSize = new Vector2(menuButton ? 310f : 360f, 110f);
            float step = menuButton ? 330f : 200f;
            Button close = HudButton("BackButton", window, bottom, new Vector2(step, 40f), buttonSize, "НАЗАД", 46f, s_PlayColor, outline);
            Button tutorial = HudButton("TutorialButton", window, bottom, new Vector2(menuButton ? 0f : -step, 40f), buttonSize,
                "ОБУЧЕНИЕ", 42f, Color.white, outline);
            Button toMenu = menuButton
                ? HudButton("MenuButton", window, bottom, new Vector2(-step, 40f), buttonSize, "В МЕНЮ", 46f, Color.white, outline)
                : null;

            var settings = root.gameObject.AddComponent<SettingsPanel>();
            PrefabBuilder.Wire(settings, ("m_MasterSlider", master), ("m_MusicSlider", music), ("m_SfxSlider", sfx),
                ("m_LowButton", low), ("m_MediumButton", medium), ("m_HighButton", high), ("m_UltraButton", ultra),
                ("m_CloseButton", close), ("m_MenuButton", toMenu), ("m_MobileSection", mobileRow.gameObject), ("m_Coins", coins),
                ("m_ControlsSizeSlider", controlsSize), ("m_EditControlsButton", editControls), ("m_Window", window),
                ("m_TutorialButton", tutorial),
                ("m_Tutorial", DataAssetsBuilder.Load<ZombiCar.Progression.TutorialState>(DataAssetsBuilder.c_TutorialState)));
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
            // The portrait breathes: the idle take's head and shoulders (UI_PortraitIdle_*, tools/characters/player_mp4/
            // portrait_idle.py) looped in the metal frame's opening (ram/rrr.png; the slot keeps the frame's 1.13 aspect).
            Image portraitBack = Part(hp, "Portrait", "UI_HpPortraitBack", 55, 59, 690, 621, s_HpRef);
            portraitBack.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Sprite[] portraitFrames = Enumerable.Range(0, 64).Select(i => UiFactory.Sprite($"UI_PortraitIdle_{i:00}"))
                .TakeWhile(s => s != null).ToArray();
            Image face = UiFactory.Image(UiFactory.Stretch("Face", portraitBack.rectTransform), portraitFrames.FirstOrDefault(), Color.white, false);
            CharacterBuilder.SetProps(face.gameObject.AddComponent<UiFrameAnimation>(), ("m_Frames", portraitFrames.Cast<Object>().ToArray()),
                ("m_Fps", 8f));
            Part(hp, "PortraitFrame", "UI_HpFrameMetal", 55, 59, 690, 621, s_HpRef);
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

            // ---- Wave progress: "ВОЛНА 7" and the timer over a line from wave 1 to 30 with the boss marks.
            BuildWaveProgress(safe, outline, margin, out TextMeshProUGUI waveLabel, out TextMeshProUGUI waveTimer);

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
            RectTransform moneyPanel = BuildRunCounters(safe, outline, margin + hpHeight + 12f, out TextMeshProUGUI moneyValue);
            BuildPerkTray(safe, outline, margin + hpHeight + 12f + 58f + 14f);
            BuildInteractionPrompt(safe, outline);
            WaveBanner banner = BuildWaveBanner(safe, outline);
            BuildCarAttackAlert(safe, outline, banner);
            BuildWeaponSlots(safe, outline);

            BuildTouchControls(safe, outline);
            BuildLevelUpScreen(safe, outline);
            BuildWorkshopScreen(safe, outline);
            BuildTutorialHint(safe, outline);                          // over the level-up cards (the first card's hint)
            BuildBossIntro(safe, outline);
            BuildVictoryFinale(safe, outline);                         // the fireworks go on behind the victory screen
            GameOverScreen gameOver = BuildGameOverScreen(safe, outline);
            PrefabBuilder.Wire(gameOver, ("m_Leaderboard", BuildLeaderboardPanel(safe, outline)));   // over the victory screen
            SettingsPanel settings = BuildSettingsPopup(safe, outline, true);
            BuildTutorialCards(safe, outline);

            var hud = canvas.gameObject.AddComponent<HudController>();
            PrefabBuilder.Wire(hud,
                ("m_PlayerStats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)),
                ("m_WaveState", DataAssetsBuilder.Load<WaveState>(DataAssetsBuilder.c_WaveState)),
                ("m_AmmoState", DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState)),
                ("m_Run", Run), ("m_MoneyValue", moneyValue), ("m_MoneyPanel", moneyPanel),
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
        private const float c_TouchSlot = 124f;             // a weapon slot of the touch column

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
                new Vector2(-c_StickTouch - 16f - c_TouchSlot * 0.5f, c_StickTouch * 0.5f + c_TouchSlot + c_WeaponSlotGap * 0.5f + 20f + c_TouchButton * 0.5f),
                new Vector2(c_TouchButton, c_TouchButton));                 // above the weapon column
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

            // Weapons: the same column as on PC (gun on top, axe under it, the one in hand orange, no key numbers),
            // right next to the fire stick; a tap takes that weapon. Moves with the other controls as one piece.
            RectTransform weapon = UiFactory.Rect("WeaponSlots", area, bottomRight, new Vector2(0.5f, 0.5f),
                new Vector2(-c_StickTouch - 16f - c_TouchSlot * 0.5f, c_StickTouch * 0.5f),
                new Vector2(c_TouchSlot, c_TouchSlot * 2f + c_WeaponSlotGap));
            Image gunSlot = WeaponSlotPlate("Gun", weapon, c_TouchSlot, c_TouchSlot + c_WeaponSlotGap, "UI_Icon_Rifle", null, outline);
            Image axeSlot = WeaponSlotPlate("Axe", weapon, c_TouchSlot, 0f, "UI_Icon_Axe", null, outline);
            var slots = weapon.gameObject.AddComponent<WeaponSlotsView>();
            PrefabBuilder.Wire(slots, ("m_State", DataAssetsBuilder.Load<WeaponState>(DataAssetsBuilder.c_WeaponState)),
                ("m_GunPlate", gunSlot), ("m_AxePlate", axeSlot), ("m_Layout", layout));
            CharacterBuilder.SetProps(slots, ("m_Mobile", true));

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
            (string id, RectTransform rect)[] list = { ("move", move), ("aim", aim), ("reload", reload), ("action", action), ("weapons", weapon) };
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
