using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The screens around a run: the difficulty choice (with the saved-run question and the Hardcore warning), the
    /// achievements and the daily bonus in the main menu; the stage-cleared window, the difficulty banner and the
    /// achievement notification in the HUD.
    /// </summary>
    public static partial class SceneBuilder
    {
        private static readonly string[] s_DifficultyFaces = { "UI_Diff_Easy", "UI_Diff_Normal", "UI_Diff_Hardcore" };
        private static readonly Color s_MarkRed = new Color(0.92f, 0.18f, 0.14f, 1f);
        private static readonly Color s_HardRed = new Color(0.85f, 0.16f, 0.12f, 1f);

        private static Sprite[] DifficultyFaces()
        {
            var faces = new Sprite[s_DifficultyFaces.Length];
            for (int i = 0; i < faces.Length; i++)
                faces[i] = UiFactory.Sprite(s_DifficultyFaces[i]);
            return faces;
        }

        /// <summary>A red disc with "!" on a button's top right corner (something waits there).</summary>
        private static GameObject Mark(Transform button, Material outline)
        {
            var corner = new Vector2(1f, 1f);
            RectTransform mark = UiFactory.Rect("Mark", button, corner, new Vector2(0.5f, 0.5f), new Vector2(-12f, -12f), new Vector2(46f, 46f));
            UiFactory.Image(mark, UiFactory.Sprite("UI_StickKnob"), s_MarkRed, false);
            OutlinedLabel("Label", mark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(46f, 46f), "!", 34f,
                TextAlignmentOptions.Center, Color.white, outline);
            mark.gameObject.SetActive(false);
            return mark.gameObject;
        }

        private static Image Icon(RectTransform parent, string name, string sprite, Vector2 anchor, Vector2 pos, float size)
        {
            RectTransform rt = UiFactory.Rect(name, parent, anchor, anchor, pos, new Vector2(size, size));
            Image image = UiFactory.Image(rt, UiFactory.Sprite(sprite), Color.white, false);
            image.preserveAspect = true;
            return image;
        }

        private static CanvasGroup Popup(RectTransform parent, string name, float dim)
        {
            RectTransform root = UiFactory.Stretch(name, parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, dim), false, true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            return group;
        }

        // ================================================================== difficulty choice (main menu)
        private static readonly Vector2 s_DifficultyCard = new Vector2(440f, 520f);

        private static DifficultyScreen BuildDifficultyScreen(RectTransform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform root = UiFactory.Stretch("DifficultyScreen", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(root, null, new Color(0.05f, 0.04f, 0.04f, 1f), false, true);   // opaque: the map card stays hidden
            OutlinedLabel("Title", root, top, top, new Vector2(0f, -36f), new Vector2(1300f, 90f), "ВЫБЕРИ СЛОЖНОСТЬ", 66f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI subtitle = OutlinedLabel("Subtitle", root, top, top, new Vector2(0f, -122f), new Vector2(1000f, 44f), "ГОРОД  •  ДЕНЬ",
                32f, TextAlignmentOptions.Center, s_SubtitleColor, outline);

            var cards = new DifficultyScreen.Card[DifficultyRules.Count];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = DifficultyCard(root, (Difficulty)i, outline);
            Button back = HudButton("BackButton", root, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(300f, 96f), "НАЗАД", 40f,
                Color.white, outline);

            // "КАК РАБОТАЮТ СОХРАНЕНИЯ?" under the cards, and its short explanation.
            Button help = HudButton("HelpButton", root, bottom, new Vector2(0f, 34f), new Vector2(660f, 92f), "КАК РАБОТАЮТ СОХРАНЕНИЯ?", 36f,
                new Color(0.3f, 0.56f, 0.9f, 1f), outline);
            TMP_Text helpLabel = help.GetComponentInChildren<TMP_Text>();
            helpLabel.enableAutoSizing = true;
            helpLabel.fontSizeMin = 22f;
            helpLabel.fontSizeMax = 36f;
            CanvasGroup helpPanel = Popup(root, "SavesHelp", 0.7f);
            RectTransform helpWindow = UiFactory.Rect("Window", helpPanel.transform, center, center, Vector2.zero, new Vector2(1240f, 700f));
            UiFactory.Image(helpWindow, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            Icon(helpWindow, "Flag", "UI_Icon_Flag", top, new Vector2(-250f, -26f), 84f);
            OutlinedLabel("Title", helpWindow, top, top, new Vector2(40f, -26f), new Vector2(700f, 84f), "СОХРАНЕНИЯ", 60f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI helpText = OutlinedLabel("Text", helpWindow, top, top, new Vector2(0f, -130f), new Vector2(1120f, 400f),
                "• На <color=#7CE36B>ЛЁГКОМ</color> и <color=#FFA238>СРЕДНЕМ</color> игра сохраняется <b>сама</b> — нажимать ничего не надо.\n" +
                "• Сохранение — после босса 10-й и 20-й волны: появится «ЭТАП ПРОЙДЕН!».\n" +
                "• Сохраняются прокачка, карточки и детали.\n" +
                "• Проиграл или вышел? Выбери ту же сложность и жми «ПРОДОЛЖИТЬ» — игра пойдёт с 11-й или 21-й волны.\n" +
                "• На <color=#FF4A3A>ХАРДКОРЕ</color> сохранений нет: все 30 волн за один раз.",
                34f, TextAlignmentOptions.TopLeft, Color.white, outline);
            helpText.textWrappingMode = TextWrappingModes.Normal;
            helpText.richText = true;
            helpText.enableAutoSizing = true;
            helpText.fontSizeMin = 22f;
            helpText.fontSizeMax = 34f;
            helpText.lineSpacing = 12f;
            Button helpOk = HudButton("OkButton", helpWindow, bottom, new Vector2(0f, 36f), new Vector2(360f, 110f), "ПОНЯТНО", 46f, s_PlayColor,
                outline);

            // A saved run: go on from it, start over, or back to the cards.
            CanvasGroup resume = Popup(root, "Resume", 0.6f);
            RectTransform resumeWindow = UiFactory.Rect("Window", resume.transform, center, center, Vector2.zero, new Vector2(1160f, 520f));
            UiFactory.Image(resumeWindow, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            Icon(resumeWindow, "Flag", "UI_Icon_Flag", top, new Vector2(-300f, -70f), 90f);
            OutlinedLabel("Title", resumeWindow, top, top, new Vector2(40f, -34f), new Vector2(700f, 84f), "ЕСТЬ СОХРАНЕНИЕ", 60f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI resumeText = OutlinedLabel("Text", resumeWindow, top, top, new Vector2(0f, -150f), new Vector2(1060f, 150f),
                "ЛЁГКИЙ  •  ВОЛНА 11 ИЗ 30", 38f, TextAlignmentOptions.Center, Color.white, outline);
            resumeText.enableAutoSizing = true;
            resumeText.fontSizeMin = 24f;
            resumeText.fontSizeMax = 38f;
            Button restart = HudButton("RestartButton", resumeWindow, bottom, new Vector2(-370f, 40f), new Vector2(330f, 110f), "С НАЧАЛА", 42f,
                Color.white, outline);
            Button resumeButton = HudButton("ResumeButton", resumeWindow, bottom, new Vector2(0f, 40f), new Vector2(360f, 120f), "ПРОДОЛЖИТЬ", 44f,
                s_PlayColor, outline);
            Button resumeBack = HudButton("BackButton", resumeWindow, bottom, new Vector2(370f, 40f), new Vector2(330f, 110f), "НАЗАД", 42f,
                Color.white, outline);

            // Hardcore, the first time: "no saves — sure?" (ДА / НАЗАД) ...
            CanvasGroup warn = Popup(root, "HardcoreWarning", 0.7f);
            RectTransform warnWindow = UiFactory.Rect("Window", warn.transform, center, center, Vector2.zero, new Vector2(1040f, 660f));
            UiFactory.Image(warnWindow, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            Icon(warnWindow, "Demon", "UI_Diff_Hardcore", top, new Vector2(0f, -100f), 170f);
            OutlinedLabel("Title", warnWindow, top, top, new Vector2(0f, -194f), new Vector2(900f, 84f), "ХАРДКОР", 68f,
                TextAlignmentOptions.Center, s_HardRed, outline);
            OutlinedLabel("Text", warnWindow, top, top, new Vector2(0f, -284f), new Vector2(940f, 190f),
                "В ЭТОМ РЕЖИМЕ НЕТ СОХРАНЕНИЙ:\nВСЕ 30 ВОЛН ЗА ОДИН РАЗ.\nВЫ УВЕРЕНЫ?", 40f, TextAlignmentOptions.Center, Color.white, outline);
            Button warnYes = HudButton("YesButton", warnWindow, bottom, new Vector2(-200f, 40f), new Vector2(340f, 116f), "ДА", 52f, s_HardRed, outline);
            Button warnBack = HudButton("BackButton", warnWindow, bottom, new Vector2(200f, 40f), new Vector2(340f, 116f), "НАЗАД", 46f,
                Color.white, outline);

            // ... then "ТОЧНО?)" with one big shaking ДА and no way back.
            CanvasGroup sure = Popup(root, "HardcoreSure", 0.9f);
            OutlinedLabel("Title", sure.transform, center, center, new Vector2(0f, 170f), new Vector2(1200f, 220f), "ТОЧНО?)", 170f,
                TextAlignmentOptions.Center, s_HardRed, outline);
            Button sureYes = HudButton("YesButton", sure.transform, center, new Vector2(0f, -130f), new Vector2(500f, 180f), "ДА", 96f, s_HardRed,
                outline);

            var screen = root.gameObject.AddComponent<DifficultyScreen>();
            PrefabBuilder.Wire(screen, ("m_Group", group), ("m_Subtitle", subtitle), ("m_BackButton", back), ("m_ResumePanel", resume),
                ("m_ResumeText", resumeText), ("m_ResumeButton", resumeButton), ("m_RestartButton", restart), ("m_ResumeBackButton", resumeBack),
                ("m_WarnPanel", warn), ("m_WarnYesButton", warnYes), ("m_WarnBackButton", warnBack), ("m_SurePanel", sure),
                ("m_SureYesButton", sureYes), ("m_HelpButton", help), ("m_HelpPanel", helpPanel), ("m_HelpCloseButton", helpOk));
            var so = new SerializedObject(screen);
            SerializedProperty list = so.FindProperty("m_Cards");
            list.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty c = list.GetArrayElementAtIndex(i);
                c.FindPropertyRelative("Button").objectReferenceValue = cards[i].Button;
                c.FindPropertyRelative("Body").objectReferenceValue = cards[i].Body;
                c.FindPropertyRelative("Glow").objectReferenceValue = cards[i].Glow;
                c.FindPropertyRelative("SaveBadge").objectReferenceValue = cards[i].SaveBadge;
                c.FindPropertyRelative("SaveText").objectReferenceValue = cards[i].SaveText;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        private static DifficultyScreen.Card DifficultyCard(RectTransform parent, Difficulty difficulty, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var midLeft = new Vector2(0f, 0.5f);
            int i = (int)difficulty;
            Color color = DifficultyRules.Color(difficulty);
            RectTransform card = UiFactory.Rect($"Card_{DifficultyRules.Title(difficulty)}", parent, center, center,
                new Vector2((i - 1) * 490f, -40f), s_DifficultyCard);
            RectTransform body = UiFactory.Stretch("Body", card);
            RectTransform glowRt = UiFactory.Stretch("Glow", body);
            glowRt.offsetMin = new Vector2(-7f, -7f);
            glowRt.offsetMax = new Vector2(7f, 7f);
            Image glow = UiFactory.Image(glowRt, UiFactory.Sprite("UI_GearButton"), new Color(color.r, color.g, color.b, 0.3f), true);
            Image plate = UiFactory.Image(UiFactory.Stretch("Plate", body), UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            // The difficulty's ribbon with its name, the face under it.
            RectTransform ribbon = UiFactory.Rect("Ribbon", body, top, top, new Vector2(0f, -18f), new Vector2(400f, 78f));
            UiFactory.Image(ribbon, UiFactory.Sprite("UI_Button"), color, true);
            OutlinedLabel("Title", ribbon, center, center, Vector2.zero, new Vector2(390f, 76f), DifficultyRules.Title(difficulty), 50f,
                TextAlignmentOptions.Center, Color.white, outline);
            RectTransform disc = UiFactory.Rect("FaceDisc", body, top, top, new Vector2(0f, -108f), new Vector2(236f, 236f));
            UiFactory.Image(disc, UiFactory.Sprite("UI_StickKnob"), new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.9f), false);
            Icon(disc, "Face", s_DifficultyFaces[i], center, Vector2.zero, 210f);
            // What it changes: the zombies and whether the run is saved.
            string zombies = difficulty switch { Difficulty.Easy => "ЗОМБИ СЛАБЕЕ", Difficulty.Hardcore => "ЗОМБИ СИЛЬНЕЕ", _ => "ОБЫЧНЫЕ ЗОМБИ" };
            string saves = DifficultyRules.HasCheckpoints(difficulty) ? "ЕСТЬ СОХРАНЕНИЯ" : "БЕЗ СОХРАНЕНИЙ";
            (string icon, string text)[] lines = { ("UI_Icon_Skull", zombies), ("UI_Icon_Flag", saves) };
            for (int l = 0; l < lines.Length; l++)
            {
                RectTransform row = UiFactory.Rect($"Line{l + 1}", body, top, top, new Vector2(0f, -364f - l * 56f), new Vector2(380f, 50f));
                Icon(row, "Icon", lines[l].icon, midLeft, new Vector2(8f, 0f), 44f);
                TextMeshProUGUI label = OutlinedLabel("Text", row, midLeft, midLeft, new Vector2(62f, 0f), new Vector2(316f, 50f), lines[l].text, 28f,
                    TextAlignmentOptions.Left, Color.white, outline);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 28f;
            }
            // A saved run of this difficulty: a green ribbon over the card's foot.
            RectTransform badge = UiFactory.Rect("SaveBadge", body, bottom, bottom, new Vector2(0f, -14f), new Vector2(410f, 62f));
            UiFactory.Image(badge, UiFactory.Sprite("UI_Button"), new Color(0.3f, 0.72f, 0.3f, 1f), true);
            TextMeshProUGUI saveText = OutlinedLabel("Text", badge, center, center, Vector2.zero, new Vector2(360f, 56f), "СОХРАНЕНИЕ: ВОЛНА 11", 30f,
                TextAlignmentOptions.Center, Color.white, outline);
            saveText.enableAutoSizing = true;
            saveText.fontSizeMin = 18f;
            saveText.fontSizeMax = 30f;
            badge.gameObject.SetActive(false);

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            card.gameObject.AddComponent<UIButtonFx>();
            return new DifficultyScreen.Card { Button = button, Body = body, Glow = glow, SaveBadge = badge.gameObject, SaveText = saveText };
        }

        // ================================================================== achievements (main menu)
        private static AchievementsPanel BuildAchievementsPanel(RectTransform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var topLeft = new Vector2(0f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var midRight = new Vector2(1f, 0.5f);
            RectTransform root = UiFactory.Stretch("AchievementsPanel", parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.7f), false, true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            int count = Achievements.All.Length;
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, new Vector2(1160f, 330f + count * 196f));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            Icon(window, "Trophy", "UI_Icon_Trophy", top, new Vector2(-260f, -26f), 80f);           // level with the title
            OutlinedLabel("Title", window, top, top, new Vector2(40f, -26f), new Vector2(600f, 84f), "ДОСТИЖЕНИЯ", 62f,
                TextAlignmentOptions.Center, s_TitleColor, outline);

            var items = new AchievementsPanel.Item[count];
            for (int i = 0; i < count; i++)
            {
                // A compact plate: the picture (a padlock over it while locked), the name and what it takes, the reward.
                RectTransform rt = UiFactory.Rect($"Item{i + 1}", window, top, top, new Vector2(0f, -132f - i * 196f), new Vector2(1060f, 180f));
                var item = new AchievementsPanel.Item();
                item.Plate = UiFactory.Image(rt, UiFactory.Sprite("UI_Panel"), new Color(0.12f, 0.12f, 0.14f, 0.92f), true);
                RectTransform picture = UiFactory.Rect("Picture", rt, midLeft, midLeft, new Vector2(20f, 0f), new Vector2(140f, 140f));
                item.Icon = UiFactory.Image(picture, UiFactory.Sprite("UI_Diff_Hardcore"), Color.white, false);
                item.Icon.preserveAspect = true;
                Image lockImage = Icon(picture, "Lock", "UI_Icon_Lock", new Vector2(1f, 0f), new Vector2(-14f, 14f), 74f);
                item.Lock = lockImage.gameObject;
                item.Title = OutlinedLabel("Title", rt, topLeft, topLeft, new Vector2(184f, -22f), new Vector2(560f, 64f), "БЕЗУМЕЦ!", 50f,
                    TextAlignmentOptions.Left, Color.white, outline);
                TextMeshProUGUI description = OutlinedLabel("Description", rt, topLeft, topLeft, new Vector2(184f, -92f), new Vector2(560f, 66f),
                    "Пройди все 30 волн", 28f, TextAlignmentOptions.TopLeft, s_SubtitleColor, outline);
                description.textWrappingMode = TextWrappingModes.Normal;
                description.enableAutoSizing = true;
                description.fontSizeMin = 18f;
                description.fontSizeMax = 28f;
                item.Description = description;
                // The reward: "676" and the coin; it glows and is taken with a tap once unlocked.
                RectTransform reward = UiFactory.Rect("Reward", rt, midRight, midRight, new Vector2(-24f, 0f), new Vector2(250f, 120f));
                RectTransform glowRt = UiFactory.Stretch("Glow", reward);
                glowRt.offsetMin = new Vector2(-12f, -12f);
                glowRt.offsetMax = new Vector2(12f, 12f);
                item.RewardGlow = UiFactory.Image(glowRt, UiFactory.Sprite("UI_Panel"), new Color(1f, 0.85f, 0.35f, 0f), true);
                item.RewardPlate = UiFactory.Image(UiFactory.Stretch("Plate", reward), UiFactory.Sprite("UI_GearButton"), Color.gray, true, true);
                item.RewardText = OutlinedLabel("Amount", reward, center, center, new Vector2(-36f, 0f), new Vector2(150f, 100f), "676", 54f,
                    TextAlignmentOptions.Right, Color.white, outline);
                item.Coin = Icon(reward, "Coin", "UI_Icon_Coin", center, new Vector2(72f, 0f), 64f).gameObject;
                item.Taken = OutlinedLabel("Taken", reward, center, center, Vector2.zero, new Vector2(226f, 100f), "ПОЛУЧЕНО", 36f,
                    TextAlignmentOptions.Center, new Color(0.6f, 1f, 0.55f, 1f), outline);
                ((TextMeshProUGUI)item.Taken).enableAutoSizing = true;
                ((TextMeshProUGUI)item.Taken).fontSizeMin = 22f;
                ((TextMeshProUGUI)item.Taken).fontSizeMax = 36f;
                item.Taken.gameObject.SetActive(false);
                item.Reward = reward.gameObject.AddComponent<Button>();
                item.Reward.targetGraphic = item.RewardPlate;
                ColorBlock colors = item.Reward.colors;
                colors.disabledColor = Color.white;                          // the panel paints a locked reward itself
                item.Reward.colors = colors;
                reward.gameObject.AddComponent<UIButtonFx>();
                items[i] = item;
            }
            Button close = HudButton("CloseButton", window, bottom, new Vector2(0f, 30f), new Vector2(340f, 96f), "ЗАКРЫТЬ", 42f, s_PlayColor, outline);

            var panel = root.gameObject.AddComponent<AchievementsPanel>();
            PrefabBuilder.Wire(panel, ("m_Group", group), ("m_Window", window), ("m_CloseButton", close));
            var so = new SerializedObject(panel);
            SerializedProperty list = so.FindProperty("m_Items");
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                SerializedProperty p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("Plate").objectReferenceValue = items[i].Plate;
                p.FindPropertyRelative("Lock").objectReferenceValue = items[i].Lock;
                p.FindPropertyRelative("Icon").objectReferenceValue = items[i].Icon;
                p.FindPropertyRelative("Title").objectReferenceValue = items[i].Title;
                p.FindPropertyRelative("Description").objectReferenceValue = items[i].Description;
                p.FindPropertyRelative("Reward").objectReferenceValue = items[i].Reward;
                p.FindPropertyRelative("RewardPlate").objectReferenceValue = items[i].RewardPlate;
                p.FindPropertyRelative("RewardGlow").objectReferenceValue = items[i].RewardGlow;
                p.FindPropertyRelative("RewardText").objectReferenceValue = items[i].RewardText;
                p.FindPropertyRelative("Taken").objectReferenceValue = items[i].Taken;
                p.FindPropertyRelative("Coin").objectReferenceValue = items[i].Coin;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return panel;
        }

        // ================================================================== daily bonus (main menu)
        private static DailyBonusPanel BuildDailyBonusPanel(RectTransform parent, Material outline, CasesScreen cases)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform root = UiFactory.Stretch("DailyBonusPanel", parent);
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.72f), false, true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, new Vector2(1560f, 720f));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            // The gift beside the title (level with it), the status line under both.
            Icon(window, "Gift", "UI_Icon_Gift", top, new Vector2(-440f, -24f), 86f);
            OutlinedLabel("Title", window, top, top, new Vector2(40f, -26f), new Vector2(900f, 84f), "ЕЖЕДНЕВНЫЕ БОНУСЫ", 60f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI status = OutlinedLabel("Status", window, top, top, new Vector2(0f, -128f), new Vector2(1300f, 44f), "СЕГОДНЯ — ДЕНЬ 1",
                32f, TextAlignmentOptions.Center, s_SubtitleColor, outline);

            var tiles = new DailyBonusPanel.Tile[DailyBonusPanel.Days.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                DailyBonusPanel.Reward reward = DailyBonusPanel.Days[i];
                bool big = reward.Kind != DailyBonusPanel.Kind.Coins;
                RectTransform rt = UiFactory.Rect($"Day{i + 1}", window, center, center, new Vector2((i - 3) * 206f, 0f), new Vector2(190f, 250f));
                var tile = new DailyBonusPanel.Tile { Root = rt };
                RectTransform glowRt = UiFactory.Stretch("Glow", rt);
                glowRt.offsetMin = new Vector2(-12f, -12f);
                glowRt.offsetMax = new Vector2(12f, 12f);
                tile.Glow = UiFactory.Image(glowRt, UiFactory.Sprite("UI_Panel"), new Color(1f, 0.82f, 0.3f, 0f), true);
                tile.Plate = UiFactory.Image(UiFactory.Stretch("Plate", rt), UiFactory.Sprite("UI_Panel"), new Color(0.1f, 0.1f, 0.12f, 0.92f), true);
                tile.Day = OutlinedLabel("Day", rt, top, top, new Vector2(0f, -10f), new Vector2(180f, 44f), $"ДЕНЬ {i + 1}", 30f,
                    TextAlignmentOptions.Center, Color.white, outline);
                // Coins; a case (the gift); the legendary case (the gift in gold rays); the black bullets (a black
                // cartridge in violet rays).
                string sprite = reward.Kind switch
                {
                    DailyBonusPanel.Kind.Case or DailyBonusPanel.Kind.LegendaryCase => "UI_Icon_Gift",
                    DailyBonusPanel.Kind.Exclusive => "UI_AmmoIcon",
                    _ => "UI_Icon_Coin",
                };
                Color amountColor = reward.Kind switch
                {
                    DailyBonusPanel.Kind.LegendaryCase => CosmeticCatalog.RarityColor(Rarity.Legendary),
                    DailyBonusPanel.Kind.Exclusive => new Color(0.78f, 0.55f, 1f, 1f),
                    DailyBonusPanel.Kind.Case => new Color(1f, 0.82f, 0.3f, 1f),
                    _ => Color.white,
                };
                if (reward.Kind == DailyBonusPanel.Kind.LegendaryCase || reward.Kind == DailyBonusPanel.Kind.Exclusive)
                {
                    RectTransform rays = UiFactory.Rect("Rays", rt, center, center, new Vector2(0f, 14f), new Vector2(190f, 190f));
                    UiFactory.Image(rays, UiFactory.Sprite("UI_Rays"), reward.Kind == DailyBonusPanel.Kind.LegendaryCase
                        ? new Color(1f, 0.78f, 0.25f, 0.85f) : new Color(0.6f, 0.2f, 1f, 0.9f), false);
                }
                Image picture = Icon(rt, "Icon", sprite, center, new Vector2(0f, 14f), big ? 112f : 96f);
                if (reward.Kind == DailyBonusPanel.Kind.Exclusive)
                    picture.color = new Color(0.07f, 0.06f, 0.09f, 1f);
                tile.Amount = OutlinedLabel("Amount", rt, bottom, bottom, new Vector2(0f, 8f), new Vector2(180f, 64f), "25", 38f,
                    TextAlignmentOptions.Center, amountColor, outline);
                tile.Amount.textWrappingMode = TextWrappingModes.Normal;
                tile.Amount.enableAutoSizing = true;
                tile.Amount.fontSizeMin = 18f;
                tile.Amount.fontSizeMax = 38f;
                // Taken: a green "ПОЛУЧЕНО" ribbon across the tile.
                RectTransform tick = UiFactory.Rect("Taken", rt, center, center, new Vector2(0f, 6f), new Vector2(210f, 54f));
                tick.localRotation = Quaternion.Euler(0f, 0f, 14f);
                UiFactory.Image(tick, UiFactory.Sprite("UI_Button"), new Color(0.3f, 0.72f, 0.3f, 1f), true);
                OutlinedLabel("Label", tick, center, center, Vector2.zero, new Vector2(200f, 52f), "ПОЛУЧЕНО", 28f, TextAlignmentOptions.Center,
                    Color.white, outline);
                tile.Tick = tick.gameObject;
                tick.gameObject.SetActive(false);
                tiles[i] = tile;
            }
            Button take = HudButton("TakeButton", window, bottom, new Vector2(0f, 36f), new Vector2(400f, 116f), "ЗАБРАТЬ", 50f, s_PlayColor, outline);
            Button close = HudButton("CloseButton", window, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(260f, 92f), "НАЗАД", 40f,
                Color.white, outline);

            var panel = root.gameObject.AddComponent<DailyBonusPanel>();
            PrefabBuilder.Wire(panel, ("m_Group", group), ("m_Window", window), ("m_TakeButton", take),
                ("m_TakeLabel", take.GetComponentInChildren<TMP_Text>()), ("m_Status", status), ("m_CloseButton", close), ("m_Cases", cases));
            var so = new SerializedObject(panel);
            SerializedProperty list = so.FindProperty("m_Tiles");
            list.arraySize = tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                SerializedProperty p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("Root").objectReferenceValue = tiles[i].Root;
                p.FindPropertyRelative("Plate").objectReferenceValue = tiles[i].Plate;
                p.FindPropertyRelative("Glow").objectReferenceValue = tiles[i].Glow;
                p.FindPropertyRelative("Day").objectReferenceValue = tiles[i].Day;
                p.FindPropertyRelative("Amount").objectReferenceValue = tiles[i].Amount;
                p.FindPropertyRelative("Tick").objectReferenceValue = tiles[i].Tick;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return panel;
        }

        // ================================================================== HUD: stage cleared, banner, toast
        private static StageClearScreen BuildStageClearScreen(RectTransform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform host = UiFactory.Stretch("StageClearScreen", parent);
            RectTransform root = UiFactory.Stretch("Root", host);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", root), null, s_DimColor, false, true);
            RectTransform window = UiFactory.Rect("Window", root, center, center, Vector2.zero, new Vector2(1060f, 620f));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            // The trophy in its rays, all inside the window and over the title (not on it).
            RectTransform rays = UiFactory.Rect("Rays", window, top, center, new Vector2(0f, -100f), new Vector2(190f, 190f));
            UiFactory.Image(rays, UiFactory.Sprite("UI_Rays"), new Color(1f, 0.8f, 0.3f, 0.6f), false);
            RectTransform trophy = UiFactory.Rect("Trophy", window, top, center, new Vector2(0f, -100f), new Vector2(104f, 104f));
            UiFactory.Image(trophy, UiFactory.Sprite("UI_Icon_Trophy"), Color.white, false).preserveAspect = true;
            TextMeshProUGUI title = OutlinedLabel("Title", window, top, top, new Vector2(0f, -192f), new Vector2(980f, 84f), "ЭТАП 1 ИЗ 3 ПРОЙДЕН!",
                62f, TextAlignmentOptions.Center, s_TitleColor, outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 36f;
            title.fontSizeMax = 62f;
            // The coin, a gap, then "+17" (left-aligned from the gap): never on each other.
            RectTransform coinsRow = UiFactory.Rect("Coins", window, top, top, new Vector2(0f, -284f), new Vector2(400f, 76f));
            Icon(coinsRow, "Coin", "UI_Icon_Coin", center, new Vector2(-70f, 0f), 66f);
            TextMeshProUGUI coins = OutlinedLabel("Value", coinsRow, center, center, new Vector2(84f, 0f), new Vector2(220f, 76f), "+0", 60f,
                TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f), outline);
            TextMeshProUGUI next = OutlinedLabel("Next", window, top, top, new Vector2(0f, -372f), new Vector2(980f, 46f),
                "ПРОГРЕСС СОХРАНЁН", 32f, TextAlignmentOptions.Center, new Color(0.6f, 1f, 0.55f, 1f), outline);
            next.enableAutoSizing = true;
            next.fontSizeMin = 20f;
            next.fontSizeMax = 32f;
            Button menu = HudButton("MenuButton", window, bottom, new Vector2(-210f, 40f), new Vector2(380f, 116f), "В МЕНЮ", 46f, Color.white, outline);
            Button go = HudButton("ContinueButton", window, bottom, new Vector2(210f, 40f), new Vector2(380f, 116f), "ПРОДОЛЖИТЬ", 44f, s_PlayColor,
                outline);

            var screen = host.gameObject.AddComponent<StageClearScreen>();
            PrefabBuilder.Wire(screen, ("m_Run", Run), ("m_Root", group), ("m_Window", window), ("m_Title", title), ("m_Coins", coins),
                ("m_Next", next), ("m_ContinueButton", go), ("m_MenuButton", menu));
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return screen;
        }

        private static ModeBanner BuildModeBanner(RectTransform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var midLeft = new Vector2(0f, 0.5f);
            RectTransform root = UiFactory.Stretch("ModeBanner", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.45f), false);
            RectTransform body = UiFactory.Rect("Body", root, center, center, new Vector2(0f, 60f), new Vector2(1240f, 320f));
            Image plate = UiFactory.Image(body, UiFactory.Sprite("UI_Panel"), new Color(0.5f, 0.1f, 0.08f, 0.92f), true);
            RectTransform faceDisc = UiFactory.Rect("FaceDisc", body, midLeft, midLeft, new Vector2(30f, 0f), new Vector2(260f, 260f));
            UiFactory.Image(faceDisc, UiFactory.Sprite("UI_StickKnob"), new Color(0f, 0f, 0f, 0.35f), false);
            Image face = Icon(faceDisc, "Face", "UI_Diff_Normal", center, Vector2.zero, 236f);
            TextMeshProUGUI title = OutlinedLabel("Title", body, midLeft, midLeft, new Vector2(320f, 46f), new Vector2(890f, 120f), "РЕЖИМ: СРЕДНИЙ",
                92f, TextAlignmentOptions.Left, Color.white, outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 50f;
            title.fontSizeMax = 92f;
            TextMeshProUGUI tagline = OutlinedLabel("Tagline", body, midLeft, midLeft, new Vector2(320f, -62f), new Vector2(890f, 70f), "ЧЕСТНЫЙ БОЙ",
                46f, TextAlignmentOptions.Left, Color.white, outline);
            tagline.enableAutoSizing = true;
            tagline.fontSizeMin = 26f;
            tagline.fontSizeMax = 46f;
            var banner = root.gameObject.AddComponent<ModeBanner>();
            PrefabBuilder.Wire(banner, ("m_Run", Run), ("m_Group", group), ("m_Body", body), ("m_Plate", plate), ("m_Face", face),
                ("m_Title", title), ("m_Tagline", tagline));
            CharacterBuilder.SetProps(banner, ("m_Faces", DifficultyFaces()));
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            return banner;
        }

        /// <summary>The kill streak under the wave plate: "СЕРИЯ 12" and its scrap multiplier in gold.</summary>
        private static StreakView BuildStreak(RectTransform parent, Material outline)
        {
            var top = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            RectTransform root = UiFactory.Rect("Streak", parent, top, top, new Vector2(0f, -146f), new Vector2(440f, 70f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            RectTransform body = UiFactory.Stretch("Body", root);
            UiFactory.Image(body, UiFactory.Sprite("UI_Panel"), s_PillColor, true);
            TextMeshProUGUI count = OutlinedLabel("Count", body, center, center, new Vector2(-60f, 0f), new Vector2(280f, 66f), "СЕРИЯ 5", 40f,
                TextAlignmentOptions.Center, Color.white, outline);
            TextMeshProUGUI multiplier = OutlinedLabel("Multiplier", body, center, center, new Vector2(140f, 0f), new Vector2(140f, 66f), "×1.25", 42f,
                TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.3f, 1f), outline);
            var view = root.gameObject.AddComponent<StreakView>();
            PrefabBuilder.Wire(view, ("m_Run", Run), ("m_Group", group), ("m_Body", body), ("m_Count", count), ("m_Multiplier", multiplier));
            group.alpha = 0f;
            return view;
        }

        private static AchievementToast BuildAchievementToast(RectTransform parent, Material outline, GameOverScreen gameOver)
        {
            var top = new Vector2(0.5f, 1f);
            var midLeft = new Vector2(0f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            RectTransform root = UiFactory.Stretch("AchievementToast", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            RectTransform body = UiFactory.Rect("Body", root, top, top, new Vector2(0f, 260f), new Vector2(980f, 170f));
            UiFactory.Image(body, UiFactory.Sprite("UI_GearButton"), new Color(1f, 0.86f, 0.45f, 1f), true);
            RectTransform rays = UiFactory.Rect("Rays", body, midLeft, new Vector2(0.5f, 0.5f), new Vector2(90f, 0f), new Vector2(190f, 190f));
            UiFactory.Image(rays, UiFactory.Sprite("UI_Rays"), new Color(1f, 0.8f, 0.3f, 0.8f), false);
            Icon(body, "Trophy", "UI_Icon_Trophy", midLeft, new Vector2(30f, 0f), 120f);
            OutlinedLabel("Header", body, topLeft, topLeft, new Vector2(176f, -12f), new Vector2(780f, 40f), "ДОСТИЖЕНИЕ ПОЛУЧЕНО!", 30f,
                TextAlignmentOptions.Left, s_TitleColor, outline);
            TextMeshProUGUI name = OutlinedLabel("Name", body, topLeft, topLeft, new Vector2(176f, -50f), new Vector2(780f, 70f), "БЕЗУМЕЦ!", 62f,
                TextAlignmentOptions.Left, Color.white, outline);
            TextMeshProUGUI reward = OutlinedLabel("Reward", body, topLeft, topLeft, new Vector2(176f, -120f), new Vector2(780f, 40f),
                "+676", 28f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f), outline);
            reward.enableAutoSizing = true;
            reward.fontSizeMin = 18f;
            reward.fontSizeMax = 28f;
            var toast = root.gameObject.AddComponent<AchievementToast>();
            PrefabBuilder.Wire(toast, ("m_Run", Run), ("m_GameOver", gameOver), ("m_Group", group), ("m_Body", body), ("m_Title", name),
                ("m_Reward", reward));
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
            return toast;
        }
    }
}
