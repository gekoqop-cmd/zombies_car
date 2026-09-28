using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Enemies;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// World-space plates drawn over the scene with no depth test (the UI World Overlay / TMP overlay materials of the
    /// car's durability plate): the boss health plate built into the boss prefabs.
    /// </summary>
    public static class WorldPlateBuilder
    {
        private static readonly Vector2 s_BossPlateSize = new Vector2(560f, 128f);
        private const int c_SortingOrder = 110;                        // over the car's plate
        private const float c_BossPlateScreenHeight = 0.065f;

        /// <summary>The plate over a boss: name with the boss icon, red bar with the health number.</summary>
        public static BossHealthPlate BuildBossPlate(GameObject boss, ZombieController zombie, BossBrain brain, float height)
        {
            if (UiFactory.Font == null)
                UiFactory.Font = TmpSetup.EnsureFont();               // prefabs are built before the scenes set it
            var anchor = new GameObject("HealthPlate");
            anchor.transform.SetParent(boss.transform, false);
            anchor.transform.localPosition = Vector3.up * height;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform)) { layer = UiFactory.c_UiLayer };
            canvasGo.transform.SetParent(anchor.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = c_SortingOrder;
            var root = (RectTransform)canvas.transform;
            root.sizeDelta = s_BossPlateSize;
            root.pivot = new Vector2(0.5f, 0f);
            root.localScale = Vector3.one * 0.005f;
            var group = canvasGo.AddComponent<CanvasGroup>();

            Material imageMaterial = SceneBuilder.OverlayMaterial("M_UI_WorldOverlay", Shader.Find(SceneBuilder.c_UiOverlayShader), null);
            Material textMaterial = SceneBuilder.OverlayMaterial("M_TMP_WorldOverlay", Shader.Find(SceneBuilder.c_TmpOverlayShader),
                TmpSetup.OutlineMaterial());
            Sprite panel = UiFactory.Sprite("UI_Panel");
            Sprite bar = UiFactory.Sprite("UI_Bar");
            var images = new List<Image>();
            var texts = new List<TMP_Text>();
            var topLeft = new Vector2(0f, 1f);
            var bottomLeft = new Vector2(0f, 0f);
            var center = new Vector2(0.5f, 0.5f);

            RectTransform shadow = UiFactory.Stretch("Shadow", root);
            shadow.offsetMin = new Vector2(6f, -9f);
            shadow.offsetMax = new Vector2(6f, -9f);
            images.Add(UiFactory.Image(shadow, panel, new Color(0f, 0f, 0f, 0.45f), true));
            RectTransform rim = UiFactory.Stretch("Rim", root);
            rim.offsetMin = new Vector2(-3f, -3f);
            rim.offsetMax = new Vector2(3f, 3f);
            images.Add(UiFactory.Image(rim, panel, new Color(1f, 0.45f, 0.35f, 0.6f), true));
            images.Add(UiFactory.Image(UiFactory.Stretch("Background", root), panel, new Color(0.08f, 0.03f, 0.04f, 0.9f), true));

            RectTransform icon = UiFactory.Rect("Icon", root, topLeft, topLeft, new Vector2(14f, -8f), new Vector2(50f, 50f));
            images.Add(UiFactory.Image(icon, UiFactory.Sprite("UI_Icon_Boss"), Color.white, false));
            TextMeshProUGUI title = UiFactory.Label("Name", root, topLeft, topLeft, new Vector2(72f, -8f),
                new Vector2(s_BossPlateSize.x - 88f, 50f), "БОСС", 34f, TextAlignmentOptions.Left, new Color(1f, 0.55f, 0.4f, 1f));
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 34f;
            texts.Add(title);

            RectTransform barRt = UiFactory.Rect("Bar", root, bottomLeft, bottomLeft, new Vector2(14f, 12f),
                new Vector2(s_BossPlateSize.x - 28f, 54f));
            images.Add(UiFactory.Image(barRt, bar, new Color(0.02f, 0.02f, 0.03f, 1f), true));
            RectTransform lane = SceneBuilder.Inset(UiFactory.Stretch("Lane", barRt), 4f);
            Image trail = UiFactory.Image(UiFactory.Stretch("Trail", lane), bar, new Color(1f, 0.9f, 0.8f, 0.85f), true);
            images.Add(trail);
            Image fill = UiFactory.Image(UiFactory.Stretch("Fill", lane), bar, new Color(0.86f, 0.18f, 0.16f, 1f), true);
            images.Add(fill);
            RectTransform gloss = UiFactory.Stretch("Gloss", fill.rectTransform);
            gloss.anchorMin = new Vector2(0f, 0.55f);
            gloss.offsetMin = new Vector2(6f, 0f);
            gloss.offsetMax = new Vector2(-6f, -4f);
            images.Add(UiFactory.Image(gloss, bar, new Color(1f, 1f, 1f, 0.22f), true));
            TextMeshProUGUI value = UiFactory.Label("Value", barRt, center, center, Vector2.zero, new Vector2(400f, 54f), "1000 / 1000", 30f,
                TextAlignmentOptions.Center, Color.white);
            texts.Add(value);
            Image flash = UiFactory.Image(UiFactory.Stretch("Flash", root), panel, new Color(1f, 0.5f, 0.4f, 0f), true);
            images.Add(flash);

            foreach (Image image in images)
            {
                image.material = imageMaterial;
                image.raycastTarget = false;
            }
            foreach (TMP_Text text in texts)
                text.fontSharedMaterial = textMaterial;

            var plate = canvasGo.AddComponent<BossHealthPlate>();
            PrefabBuilder.Wire(plate, ("m_Boss", zombie), ("m_Brain", brain), ("m_Plate", root), ("m_Group", group), ("m_Fill", fill),
                ("m_Trail", trail), ("m_Flash", flash), ("m_Title", title), ("m_Value", value));
            CharacterBuilder.SetProps(plate, ("m_ScreenHeight", c_BossPlateScreenHeight));
            return plate;
        }
    }
}
