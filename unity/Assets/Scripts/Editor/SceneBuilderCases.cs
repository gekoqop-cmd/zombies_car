using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The main menu's cases screen (<see cref="CasesScreen"/>): three cases side by side (fits a 4:3 screen, 1440 units
    /// wide), the roulette overlay with its result and the collection grid. Tiles are instantiated at runtime from the
    /// hidden templates built here.
    /// </summary>
    public static partial class SceneBuilder
    {
        private static readonly Vector2 s_CaseCard = new Vector2(420f, 720f);
        private const float c_CaseStep = 450f;
        private const float c_TileSize = 220f;
        private const float c_TileStep = 236f;
        private static readonly Color s_CaseTint = new Color(0.1f, 0.11f, 0.14f, 0.96f);

        private static CasesScreen BuildCasesScreen(Transform parent, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var topRight = new Vector2(1f, 1f);
            var bottomLeft = new Vector2(0f, 0f);

            RectTransform host = UiFactory.Stretch("CasesScreen", parent);
            RectTransform root = UiFactory.Stretch("Root", host);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(UiFactory.Stretch("Dim", root), null, new Color(0.02f, 0.02f, 0.03f, 0.86f), false, true);
            OutlinedLabel("Title", root, top, top, new Vector2(0f, -36f), new Vector2(1200f, 90f), "КЕЙСЫ", 70f,
                TextAlignmentOptions.Center, s_TitleColor, outline);
            TextMeshProUGUI coins = CoinCounter("Coins", root, topRight, topRight, new Vector2(-40f, -44f), 64f, 300f, "0", 48f, outline);
            OutlinedLabel("Hint", root, top, top, new Vector2(0f, -120f), new Vector2(1300f, 44f),
                "МОНЕТЫ ДАЮТСЯ В КОНЦЕ КАЖДОГО ЗАБЕГА", 28f, TextAlignmentOptions.Center, s_SubtitleColor, outline);
            Button close = HudButton("BackButton", root, bottomLeft, new Vector2(40f, 40f), new Vector2(300f, 96f), "НАЗАД", 40f,
                Color.white, outline);

            string[] icons = { "UI_Icon_Rifle", "UI_Icon_Axe", "UI_AmmoIcon" };
            var cards = new CaseCardRefs[3];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = BuildCaseCard(root, i, UiFactory.Sprite(icons[i]), outline);

            // ---- roulette
            RectTransform roulette = UiFactory.Stretch("Roulette", root);
            var rouletteGroup = roulette.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(roulette, null, new Color(0.02f, 0.02f, 0.03f, 0.97f), false, true);   // the cases fade out behind
            RectTransform viewport = UiFactory.Rect("Viewport", roulette, center, center, new Vector2(0f, 150f), new Vector2(1340f, 270f));
            UiFactory.Image(viewport, UiFactory.Sprite("UI_Panel"), new Color(0.06f, 0.07f, 0.09f, 0.95f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform strip = UiFactory.Rect("Strip", viewport, center, center, Vector2.zero, new Vector2(10f, c_TileSize));
            ItemTile stripTemplate = BuildItemTile(strip, "TileTemplate", outline, false);
            RectTransform marker = UiFactory.Rect("Marker", roulette, center, center, new Vector2(0f, 150f), new Vector2(8f, 300f));
            UiFactory.Image(marker, null, new Color(1f, 0.8f, 0.25f, 1f), false);

            RectTransform result = UiFactory.Rect("Result", roulette, center, center, new Vector2(0f, -170f), new Vector2(1200f, 340f));
            TextMeshProUGUI rarity = OutlinedLabel("Rarity", result, top, top, Vector2.zero, new Vector2(1200f, 56f), "ЛЕГЕНДАРНОЕ", 40f,
                TextAlignmentOptions.Center, Color.white, outline);
            TextMeshProUGUI prizeName = OutlinedLabel("Name", result, top, top, new Vector2(0f, -56f), new Vector2(1200f, 86f), "ЗОЛОТО", 72f,
                TextAlignmentOptions.Center, Color.white, outline);
            TextMeshProUGUI note = OutlinedLabel("Note", result, top, top, new Vector2(0f, -146f), new Vector2(1200f, 46f),
                "НОВЫЙ СКИН В КОЛЛЕКЦИИ!", 32f, TextAlignmentOptions.Center, s_SubtitleColor, outline);
            Button equip = HudButton("EquipButton", result, bottom, new Vector2(-190f, 0f), new Vector2(340f, 100f), "НАДЕТЬ", 44f,
                s_PlayColor, outline);
            Button ok = HudButton("OkButton", result, bottom, new Vector2(190f, 0f), new Vector2(340f, 100f), "ОК", 44f, Color.white, outline);

            // ---- collection
            RectTransform collection = UiFactory.Stretch("Collection", root);
            var collectionGroup = collection.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Image(collection, null, new Color(0f, 0f, 0f, 0.7f), false, true);
            RectTransform window = UiFactory.Rect("Window", collection, center, center, Vector2.zero, new Vector2(1320f, 820f));
            UiFactory.Image(window, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            TextMeshProUGUI collectionTitle = OutlinedLabel("Title", window, top, top, new Vector2(0f, -26f), new Vector2(1000f, 80f),
                "ОРУЖИЕ", 60f, TextAlignmentOptions.Center, s_TitleColor, outline);
            OutlinedLabel("Hint", window, top, top, new Vector2(0f, -100f), new Vector2(1000f, 40f), "НАЖМИТЕ НА СКИН, ЧТОБЫ НАДЕТЬ", 28f,
                TextAlignmentOptions.Center, s_SubtitleColor, outline);
            RectTransform grid = UiFactory.Rect("Grid", window, top, top, new Vector2(0f, -160f), new Vector2(1220f, 480f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(c_TileSize, c_TileSize);
            layout.spacing = new Vector2(20f, 20f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            ItemTile gridTemplate = BuildItemTile(grid, "TileTemplate", outline, true);
            Button collectionClose = HudButton("DoneButton", window, bottom, new Vector2(0f, 30f), new Vector2(340f, 100f), "ГОТОВО", 44f,
                s_PlayColor, outline);

            var screen = host.gameObject.AddComponent<CasesScreen>();
            PrefabBuilder.Wire(screen, ("m_Catalog", CosmeticsBuilder.Catalog), ("m_Root", group), ("m_Coins", coins),
                ("m_CloseButton", close), ("m_Roulette", rouletteGroup), ("m_Strip", strip), ("m_StripTemplate", stripTemplate),
                ("m_ResultPanel", result.gameObject), ("m_ResultRarity", rarity), ("m_ResultName", prizeName), ("m_ResultNote", note),
                ("m_EquipButton", equip), ("m_OkButton", ok), ("m_Collection", collectionGroup), ("m_CollectionTitle", collectionTitle),
                ("m_Grid", grid), ("m_GridTemplate", gridTemplate), ("m_CollectionClose", collectionClose));
            CharacterBuilder.SetProps(screen, ("m_SlotIcons", new Object[] { UiFactory.Sprite(icons[0]), UiFactory.Sprite(icons[1]),
                UiFactory.Sprite(icons[2]) }), ("m_TileStep", c_TileStep));
            var so = new SerializedObject(screen);
            SerializedProperty list = so.FindProperty("m_Cards");
            list.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty c = list.GetArrayElementAtIndex(i);
                c.FindPropertyRelative("Title").objectReferenceValue = cards[i].Title;
                c.FindPropertyRelative("Chances").objectReferenceValue = cards[i].Chances;
                c.FindPropertyRelative("Icon").objectReferenceValue = cards[i].Icon;
                c.FindPropertyRelative("OpenButton").objectReferenceValue = cards[i].OpenButton;
                c.FindPropertyRelative("OpenLabel").objectReferenceValue = cards[i].OpenLabel;
                c.FindPropertyRelative("AdButton").objectReferenceValue = cards[i].AdButton;
                c.FindPropertyRelative("CollectionButton").objectReferenceValue = cards[i].CollectionButton;
                c.FindPropertyRelative("CollectionLabel").objectReferenceValue = cards[i].CollectionLabel;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        private static CaseCardRefs BuildCaseCard(RectTransform parent, int index, Sprite icon, Material outline)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform card = UiFactory.Rect($"Case{index + 1}", parent, center, center, new Vector2((index - 1) * c_CaseStep, -40f), s_CaseCard);
            UiFactory.Image(card, UiFactory.Sprite("UI_GearButton"), Color.white, true, true);
            var refs = new CaseCardRefs();
            refs.Title = OutlinedLabel("Title", card, top, top, new Vector2(0f, -22f), new Vector2(390f, 60f), "КЕЙС", 40f,
                TextAlignmentOptions.Center, Color.white, outline);
            ((TextMeshProUGUI)refs.Title).enableAutoSizing = true;
            ((TextMeshProUGUI)refs.Title).fontSizeMin = 26f;
            ((TextMeshProUGUI)refs.Title).fontSizeMax = 40f;
            RectTransform disc = UiFactory.Rect("IconDisc", card, top, top, new Vector2(0f, -94f), new Vector2(170f, 170f));
            UiFactory.Image(disc, UiFactory.Sprite("UI_StickKnob"), new Color(0.95f, 0.55f, 0.18f, 1f), false);
            RectTransform iconRt = UiFactory.Rect("Icon", disc, center, center, Vector2.zero, new Vector2(126f, 126f));
            refs.Icon = UiFactory.Image(iconRt, icon, Color.white, false);
            refs.Icon.preserveAspect = true;
            TextMeshProUGUI chances = OutlinedLabel("Chances", card, top, top, new Vector2(0f, -272f), new Vector2(380f, 170f),
                "ОБЫЧНОЕ 55%", 26f, TextAlignmentOptions.Center, Color.white, outline);
            chances.enableAutoSizing = true;
            chances.fontSizeMin = 18f;
            chances.fontSizeMax = 26f;
            chances.textWrappingMode = TextWrappingModes.Normal;
            chances.richText = true;
            refs.Chances = chances;

            refs.OpenButton = HudButton("OpenButton", card, bottom, new Vector2(0f, 186f), new Vector2(380f, 90f), "ОТКРЫТЬ", 32f,
                s_PlayColor, outline);
            refs.OpenLabel = refs.OpenButton.GetComponentInChildren<TMP_Text>();
            // Open for a rewarded ad: the shared gold ad button, icon only.
            refs.AdButton = AdButton("AdButton", card, bottom, new Vector2(0f, 100f), new Vector2(380f, 78f), null, 0f, outline);
            refs.CollectionButton = HudButton("CollectionButton", card, bottom, new Vector2(0f, 20f), new Vector2(380f, 72f), "КОЛЛЕКЦИЯ",
                26f, Color.white, outline);
            refs.CollectionLabel = refs.CollectionButton.GetComponentInChildren<TMP_Text>();
            return refs;
        }

        private static ItemTile BuildItemTile(RectTransform parent, string name, Material outline, bool clickable)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            RectTransform tile = UiFactory.Rect(name, parent, center, center, Vector2.zero, new Vector2(c_TileSize, c_TileSize));
            RectTransform equippedRt = UiFactory.Stretch("Equipped", tile);
            equippedRt.offsetMin = new Vector2(-8f, -8f);
            equippedRt.offsetMax = new Vector2(8f, 8f);
            Image equipped = UiFactory.Image(equippedRt, UiFactory.Sprite("UI_Panel"), new Color(1f, 0.75f, 0.2f, 1f), true);
            equipped.enabled = false;
            Image plate = UiFactory.Image(UiFactory.Stretch("Plate", tile), UiFactory.Sprite("UI_Panel"), Color.gray, true, clickable);
            RectTransform iconRt = UiFactory.Rect("Icon", tile, top, top, new Vector2(0f, -18f), new Vector2(140f, 140f));
            Image icon = UiFactory.Image(iconRt, null, Color.white, false);
            icon.preserveAspect = true;
            TextMeshProUGUI label = OutlinedLabel("Name", tile, bottom, bottom, new Vector2(0f, 10f), new Vector2(206f, 48f), "СКИН", 30f,
                TextAlignmentOptions.Center, Color.white, outline);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 30f;
            TextMeshProUGUI locked = OutlinedLabel("Locked", tile, center, center, new Vector2(0f, 24f), new Vector2(140f, 140f), "?", 110f,
                TextAlignmentOptions.Center, new Color(0.75f, 0.75f, 0.8f, 1f), outline);
            locked.gameObject.SetActive(false);
            // "НОВЫЙ": a tilted orange ribbon over the top right corner.
            var topRight = new Vector2(1f, 1f);
            RectTransform fresh = UiFactory.Rect("New", tile, topRight, center, new Vector2(-30f, -14f), new Vector2(120f, 40f));
            fresh.localRotation = Quaternion.Euler(0f, 0f, -14f);
            UiFactory.Image(fresh, UiFactory.Sprite("UI_Button"), new Color(1f, 0.55f, 0.15f, 1f), true);
            OutlinedLabel("Label", fresh, center, center, Vector2.zero, new Vector2(120f, 40f), "НОВЫЙ", 24f,
                TextAlignmentOptions.Center, Color.white, outline);
            fresh.gameObject.SetActive(false);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.interactable = clickable;
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;                          // the tile paints its own locked look
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            button.colors = colors;
            if (clickable)
                tile.gameObject.AddComponent<UIButtonFx>();
            var view = tile.gameObject.AddComponent<ItemTile>();
            PrefabBuilder.Wire(view, ("m_Plate", plate), ("m_Icon", icon), ("m_Title", label), ("m_Equipped", equipped),
                ("m_Locked", locked), ("m_Button", button), ("m_New", fresh.gameObject));
            return view;
        }
    }
}
