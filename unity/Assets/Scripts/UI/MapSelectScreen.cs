using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiCar.UI
{
    /// <summary>
    /// Map selection: a strip of <see cref="MapCard"/>s, one visible at a time; arrows or a horizontal swipe on the
    /// strip move to the previous / next card with a short slide.
    /// </summary>
    public sealed class MapSelectScreen : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform m_Strip;
        [SerializeField] private MapCard[] m_Cards = System.Array.Empty<MapCard>();
        [Tooltip("Distance between card centres in the strip, canvas units.")]
        [SerializeField, Min(1f)] private float m_Spacing = 1200f;
        [SerializeField] private Button m_PrevButton;
        [SerializeField] private Button m_NextButton;
        [SerializeField, Min(0.01f)] private float m_SlideTime = 0.18f;
        [Tooltip("Swipe distance (canvas units) that turns the page.")]
        [SerializeField, Min(1f)] private float m_SwipeThreshold = 120f;

        private int m_Index;
        private float m_DragOffset;
        private float m_Velocity;
        private bool m_Dragging;

        public int Index => m_Index;
        public MapCard Current => m_Cards.Length > 0 ? m_Cards[m_Index] : null;

        private void Awake()
        {
            if (m_PrevButton != null)
                m_PrevButton.onClick.AddListener(() => Go(m_Index - 1));
            if (m_NextButton != null)
                m_NextButton.onClick.AddListener(() => Go(m_Index + 1));
            Go(0, true);
        }

        public void Go(int index) => Go(index, false);

        private void Go(int index, bool instant)
        {
            m_Index = Mathf.Clamp(index, 0, m_Cards.Length - 1);
            if (m_PrevButton != null)
                m_PrevButton.interactable = m_Index > 0;
            if (m_NextButton != null)
                m_NextButton.interactable = m_Index < m_Cards.Length - 1;
            if (instant)
                SetStrip(TargetX());
        }

        private float TargetX() => -m_Index * m_Spacing;

        private void SetStrip(float x) => m_Strip.anchoredPosition = new Vector2(x, m_Strip.anchoredPosition.y);

        private void Update()
        {
            if (!m_Dragging)
            {
                float x = Mathf.SmoothDamp(m_Strip.anchoredPosition.x, TargetX(), ref m_Velocity, m_SlideTime,
                    float.PositiveInfinity, Time.unscaledDeltaTime);
                SetStrip(x);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            m_Dragging = true;
            m_DragOffset = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            m_DragOffset += eventData.delta.x / scale;
            SetStrip(TargetX() + m_DragOffset);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            m_Dragging = false;
            if (m_DragOffset <= -m_SwipeThreshold)
                Go(m_Index + 1);
            else if (m_DragOffset >= m_SwipeThreshold)
                Go(m_Index - 1);
        }
    }
}
