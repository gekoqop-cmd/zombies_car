using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// The on-screen touch controls of the HUD (mobile only): a list of movable controls — the two sticks, the reload
    /// button, and any buttons added later — kept inside <see cref="m_Area"/>, the part of the screen below the top HUD
    /// panels, so they never cover health, wave or ammo. Positions (a fraction of the area, so they survive rotation
    /// and other aspect ratios) and the size are saved per player. In edit mode (from the settings) the game pauses and
    /// the controls can be dragged; Done keeps the layout, Reset restores the defaults.
    /// </summary>
    public sealed class MobileControlsLayout : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const string c_KeyPrefix = "mobile.controls.";

        [Serializable]
        private sealed class Control
        {
            [Tooltip("Save key; keep it stable once released.")]
            public string Id;
            public RectTransform Rect;
        }

        [Tooltip("Where the controls may be: the screen below the top HUD.")]
        [SerializeField] private RectTransform m_Area;
        [SerializeField] private Control[] m_Controls = Array.Empty<Control>();
        [Header("Edit mode")]
        [SerializeField] private GameObject m_EditOverlay;
        [SerializeField] private Button m_DoneButton;
        [SerializeField] private Button m_ResetButton;
        [Tooltip("Tint of a control while it is being dragged.")]
        [SerializeField] private Color m_DragTint = new Color(1f, 0.75f, 0.35f, 1f);

        private Vector2[] m_Default;
        private Vector2[] m_Position;
        private Vector2 m_AreaSize;
        private int m_Dragged = -1;

        /// <summary>The HUD's controls (null in scenes without them, e.g. the main menu).</summary>
        public static MobileControlsLayout Instance { get; private set; }

        public bool IsEditing { get; private set; }

        private void Awake()
        {
            if (!DeviceInfo.IsMobile)
            {
                gameObject.SetActive(false);
                return;
            }
            Instance = this;
            m_Default = new Vector2[m_Controls.Length];
            m_Position = new Vector2[m_Controls.Length];
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < m_Controls.Length; i++)
            {
                m_Default[i] = CurrentFraction(m_Controls[i].Rect);
                m_Position[i] = Load(m_Controls[i].Id, m_Default[i]);
                RectTransform rt = m_Controls[i].Rect;
                rt.anchorMin = rt.anchorMax = m_Position[i];
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
            if (m_EditOverlay != null)
                m_EditOverlay.SetActive(false);
            if (m_DoneButton != null)
                m_DoneButton.onClick.AddListener(EndEdit);
            if (m_ResetButton != null)
                m_ResetButton.onClick.AddListener(ResetLayout);
        }

        private void OnEnable()
        {
            SettingsService.ControlsScaleChanged += ApplyScale;
            ApplyScale(SettingsService.ControlsScale);
        }

        private void OnDisable()
        {
            SettingsService.ControlsScaleChanged -= ApplyScale;
            if (IsEditing)
                EndEdit();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            Vector2 size = m_Area.rect.size;
            if (size != m_AreaSize)
            {
                m_AreaSize = size;
                ClampAll();
            }
        }

        // ---------------------------------------------------------------- size
        private void ApplyScale(float scale)
        {
            foreach (Control c in m_Controls)
                c.Rect.localScale = Vector3.one * scale;
            ClampAll();
        }

        // ---------------------------------------------------------------- edit mode
        public void BeginEdit()
        {
            if (IsEditing)
                return;
            IsEditing = true;
            GamePause.Set(GamePause.Reason.ControlsLayout, true);
            MobileControls.Reset();
            if (m_EditOverlay != null)
                m_EditOverlay.SetActive(true);
        }

        public void EndEdit()
        {
            if (!IsEditing)
                return;
            IsEditing = false;
            GamePause.Set(GamePause.Reason.ControlsLayout, false);
            if (m_EditOverlay != null)
                m_EditOverlay.SetActive(false);
            for (int i = 0; i < m_Controls.Length; i++)
                Save(m_Controls[i].Id, m_Position[i]);
            PlayerPrefs.Save();
        }

        public void ResetLayout()
        {
            for (int i = 0; i < m_Controls.Length; i++)
                SetPosition(i, m_Default[i]);
            SettingsService.SetControlsScale(1f);
        }

        // The overlay (a child of this object, above the controls) receives the drags while editing.
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsEditing)
                return;
            m_Dragged = -1;
            for (int i = m_Controls.Length - 1; i >= 0; i--)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(m_Controls[i].Rect, eventData.position, eventData.pressEventCamera))
                {
                    m_Dragged = i;
                    Tint(i, m_DragTint);
                    break;
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsEditing || m_Dragged < 0)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Area, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            Rect r = m_Area.rect;
            SetPosition(m_Dragged, new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (m_Dragged >= 0)
                Tint(m_Dragged, Color.white);
            m_Dragged = -1;
        }

        // ---------------------------------------------------------------- positions
        private void SetPosition(int i, Vector2 fraction)
        {
            m_Position[i] = Clamp(i, fraction);
            m_Controls[i].Rect.anchorMin = m_Controls[i].Rect.anchorMax = m_Position[i];
        }

        private void ClampAll()
        {
            if (m_Position == null)
                return;
            for (int i = 0; i < m_Controls.Length; i++)
                SetPosition(i, m_Position[i]);
        }

        // Keeps the whole (scaled) control inside the area.
        private Vector2 Clamp(int i, Vector2 fraction)
        {
            Rect area = m_Area.rect;
            if (area.width <= 1f || area.height <= 1f)
                return fraction;
            RectTransform rt = m_Controls[i].Rect;
            Vector2 half = Vector2.Scale(rt.rect.size, rt.localScale) * 0.5f;
            float hx = Mathf.Min(0.5f, half.x / area.width), hy = Mathf.Min(0.5f, half.y / area.height);
            return new Vector2(Mathf.Clamp(fraction.x, hx, 1f - hx), Mathf.Clamp(fraction.y, hy, 1f - hy));
        }

        private Vector2 CurrentFraction(RectTransform rt)
        {
            Vector3 world = rt.TransformPoint(rt.rect.center);
            Vector2 local = m_Area.InverseTransformPoint(world);
            Rect r = m_Area.rect;
            return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
        }

        private void Tint(int i, Color color)
        {
            foreach (Graphic g in m_Controls[i].Rect.GetComponentsInChildren<Graphic>())
                g.color = new Color(color.r, color.g, color.b, g.color.a);
        }

        private static Vector2 Load(string id, Vector2 fallback)
        {
            string key = c_KeyPrefix + id;
            return PlayerPrefs.HasKey(key + ".x")
                ? new Vector2(PlayerPrefs.GetFloat(key + ".x"), PlayerPrefs.GetFloat(key + ".y"))
                : fallback;
        }

        private static void Save(string id, Vector2 value)
        {
            PlayerPrefs.SetFloat(c_KeyPrefix + id + ".x", value.x);
            PlayerPrefs.SetFloat(c_KeyPrefix + id + ".y", value.y);
        }
    }
}
