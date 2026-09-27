using UnityEngine;
using UnityEngine.EventSystems;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// Round on-screen stick. Move: writes the left stick to <see cref="MobileControls.Move"/>. Aim (Brawl Stars style):
    /// dragging past the dead zone aims and fires (<see cref="MobileControls.Aiming"/>), a quick tap is an auto-aim
    /// shot (<see cref="MobileControls.TapFrame"/>). The touch area is the whole control, larger than the ring, so a
    /// thumb that lands a little off still catches it. Ignored while the layout is being edited.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public enum Role { Move, Aim }

        [SerializeField] private Role m_Role;
        [SerializeField] private RectTransform m_Ring;
        [SerializeField] private RectTransform m_Knob;
        [Tooltip("Share of the ring radius below which the aim stick does not aim (tap zone).")]
        [SerializeField, Range(0f, 0.9f)] private float m_DeadZone = 0.25f;
        [Tooltip("A release within this time, never past the dead zone, is a tap.")]
        [SerializeField, Min(0.05f)] private float m_TapTime = 0.25f;
        [SerializeField] private MobileControlsLayout m_Layout;

        private int m_Pointer = int.MinValue;
        private float m_DownTime;
        private float m_MaxMagnitude;
        private CanvasGroup m_Group;

        private void Awake() => m_Group = GetComponent<CanvasGroup>();

        private void OnDisable() => Release();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_Pointer != int.MinValue || (m_Layout != null && m_Layout.IsEditing))
                return;
            m_Pointer = eventData.pointerId;
            m_DownTime = Time.unscaledTime;
            m_MaxMagnitude = 0f;
            Track(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == m_Pointer)
                Track(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != m_Pointer)
                return;
            if (m_Role == Role.Aim && Time.unscaledTime - m_DownTime <= m_TapTime && m_MaxMagnitude < m_DeadZone)
                MobileControls.TapFrame = Time.frameCount;
            Release();
        }

        private void Track(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Ring, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            float radius = m_Ring.rect.width * 0.5f;
            Vector2 value = Vector2.ClampMagnitude(local / radius, 1f);
            m_MaxMagnitude = Mathf.Max(m_MaxMagnitude, value.magnitude);
            m_Knob.anchoredPosition = value * radius;
            if (m_Role == Role.Move)
            {
                // Small dead zone, then full range: light thumb pressure already walks.
                float m = value.magnitude;
                MobileControls.Move = m < 0.12f ? Vector2.zero : value / m * Mathf.InverseLerp(0.12f, 0.85f, m);
            }
            else
            {
                bool aiming = value.magnitude >= m_DeadZone;
                MobileControls.Aiming = aiming;
                if (aiming)
                    MobileControls.Aim = value.normalized;
            }
            if (m_Group != null)
                m_Group.alpha = 1f;
        }

        private void Release()
        {
            m_Pointer = int.MinValue;
            if (m_Knob != null)
                m_Knob.anchoredPosition = Vector2.zero;
            if (m_Role == Role.Move)
                MobileControls.Move = Vector2.zero;
            else
                MobileControls.Aiming = false;
            if (m_Group != null)
                m_Group.alpha = 0.75f;
        }
    }
}
