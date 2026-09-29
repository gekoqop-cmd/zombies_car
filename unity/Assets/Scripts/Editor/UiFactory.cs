using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ZombiCar.EditorTools
{
    /// <summary>Small helpers to build uGUI + TextMeshPro layouts from editor code (reference 1920 x 1080).</summary>
    public static class UiFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>Built-in "UI" layer (camera-space canvases are culled by layer).</summary>
        public const int c_UiLayer = 5;

        /// <summary>
        /// Minimum touch target in reference units: with match-height scaling a landscape phone about 360 dp tall
        /// maps 1080 units to 360 dp, so 144 units = 48 dp.
        /// </summary>
        public const float c_MinTouchTarget = 144f;
        public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.1f, 0.82f);
        public static readonly Color TextColor = new Color(0.96f, 0.94f, 0.9f, 1f);
        public static readonly Color AccentColor = new Color(0.93f, 0.47f, 0.13f, 1f);

        private const string c_UiTextures = "Assets/Art/Textures/UI/";

        public static TMP_FontAsset Font { get; set; }

        public static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{c_UiTextures}{name}.png");

        public static Canvas Canvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = c_UiLayer };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // Expand: at 16:9 and wider the UI keeps its height (landscape phones, desktops); on narrower screens (a
            // browser in fullscreen, 4:3 tablets) it keeps the 1920 width instead, so the top panels never overlap.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = c_UiLayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = c_UiLayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform rt, Sprite sprite, Color color, bool sliced, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Fill(RectTransform rt, Sprite sprite, Color color, float amount)
        {
            Image img = Image(rt, sprite, color, false);
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            img.fillAmount = amount;
            return img;
        }

        public static TextMeshProUGUI Text(RectTransform rt, string text, float size, TextAlignmentOptions align, Color color, bool bold = true)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            string text, float fontSize, TextAlignmentOptions align, Color color, bool bold = true) =>
            Text(Rect(name, parent, anchor, pivot, pos, size), text, fontSize, align, color, bold);

        public static Button Button(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            string label, float fontSize, Color background, Color labelColor)
        {
            RectTransform rt = Rect(name, parent, anchor, pivot, pos, size);
            Image img = Image(rt, Sprite("UI_Button"), background, true, true);
            Button button = AddButton(rt.gameObject, img);
            if (!string.IsNullOrEmpty(label))
            {
                RectTransform labelRt = Stretch("Label", rt);
                Text(labelRt, label, fontSize, TextAlignmentOptions.Center, labelColor);
            }
            return button;
        }

        /// <summary>
        /// Square icon button whose invisible touch area is at least <see cref="c_MinTouchTarget"/> while the visible
        /// panel keeps <paramref name="visualSize"/>. <paramref name="pos"/> places the touch area.
        /// </summary>
        public static Button IconButton(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float visualSize,
            Sprite icon, float iconInset, Color background, Color iconColor)
        {
            float hitSize = Mathf.Max(visualSize, c_MinTouchTarget);
            RectTransform rt = Rect(name, parent, anchor, pivot, pos, new Vector2(hitSize, hitSize));
            Image(rt, null, Color.clear, false, true);                        // touch area only, never visible
            var center = new Vector2(0.5f, 0.5f);
            RectTransform visualRt = Rect("Visual", rt, center, center, Vector2.zero, new Vector2(visualSize, visualSize));
            Image visual = Image(visualRt, Sprite("UI_Button"), background, true);
            RectTransform iconRt = Stretch("Icon", visualRt);
            iconRt.offsetMin = new Vector2(iconInset, iconInset);
            iconRt.offsetMax = -iconRt.offsetMin;
            Image(iconRt, icon, iconColor, false);
            return AddButton(rt.gameObject, visual);
        }

        private static Button AddButton(GameObject go, Graphic target)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            return button;
        }
    }
}
