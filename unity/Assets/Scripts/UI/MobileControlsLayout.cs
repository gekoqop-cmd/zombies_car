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
    /// panels, so they never cover health, wave or ammo. The default places are the designed distances from the area's
    /// corners, worked out again whenever the area changes size (a resize, a rotation), so the controls never slide
    /// into each other; a control the player moved keeps its place as a fraction of the area. Moved places and the
    /// size are saved per player. In edit mode (from the settings) the game pauses and the controls can be dragged;
    /// Done keeps the layout, Reset restores the defaults.
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

        private Vector2[] m_Corner;           // the designed anchor corner of each control (area fractions)
        private Vector2[] m_Offset;           // its centre from that corner, canvas units
        private bool[] m_Custom;              // moved by the player: kept as a fraction, not recomputed
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
            int n = m_Controls.Length;
            m_Corner = new Vector2[n];
            m_Offset = new Vector2[n];
            m_Custom = new bool[n];
            m_Position = new Vector2[n];
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < n; i++)
            {
                RectTransform rt = m_Controls[i].Rect;
                m_Corner[i] = rt.anchorMin;
                m_Offset[i] = rt.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - rt.pivot, rt.rect.size);
                m_Custom[i] = TryLoad(m_Controls[i].Id, out Vector2 saved);
                m_Position[i] = m_Custom[i] ? saved : DefaultFraction(i);
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

        private void LateUpdate() => Refit();

        /// <summary>Follows a new size of the area now (every frame by itself; tools rendering at another size call it).</summary>
        public void Refit()
        {
            if (m_Position == null)
                return;
            Vector2 size = m_Area.rect.size;
            if (size != m_AreaSize)
            {
                m_AreaSize = size;
                for (int i = 0; i < m_Controls.Length; i++)
                {
                    if (!m_Custom[i])
                        m_Position[i] = DefaultFraction(i);
                }
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
            {
                if (m_Custom[i])
                    Save(m_Controls[i].Id, m_Position[i]);
                else
                    Forget(m_Controls[i].Id);
            }
            PlayerPrefs.Save();
        }

        public void ResetLayout()
        {
            for (int i = 0; i < m_Controls.Length; i++)
            {
                m_Custom[i] = false;
                SetPosition(i, DefaultFraction(i));
            }
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
            m_Custom[m_Dragged] = true;
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

        // The designed place for the area's current size: the same distance from the same corner.
        private Vector2 DefaultFraction(int i)
        {
            Vector2 size = m_Area.rect.size;
            if (size.x <= 1f || size.y <= 1f)
                return m_Corner[i];
            return m_Corner[i] + new Vector2(m_Offset[i].x / size.x, m_Offset[i].y / size.y);
        }

        private void Tint(int i, Color color)
        {
            foreach (Graphic g in m_Controls[i].Rect.GetComponentsInChildren<Graphic>())
                g.color = new Color(color.r, color.g, color.b, g.color.a);
        }

        private static bool TryLoad(string id, out Vector2 value)
        {
            string key = c_KeyPrefix + id;
            bool saved = PlayerPrefs.HasKey(key + ".x");
            value = saved ? new Vector2(PlayerPrefs.GetFloat(key + ".x"), PlayerPrefs.GetFloat(key + ".y")) : Vector2.zero;
            return saved;
        }

        private static void Forget(string id)
        {
            PlayerPrefs.DeleteKey(c_KeyPrefix + id + ".x");
            PlayerPrefs.DeleteKey(c_KeyPrefix + id + ".y");
        }

        private static void Save(string id, Vector2 value)
        {
            PlayerPrefs.SetFloat(c_KeyPrefix + id + ".x", value.x);
            PlayerPrefs.SetFloat(c_KeyPrefix + id + ".y", value.y);
        }
    }
}
