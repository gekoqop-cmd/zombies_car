using UnityEngine;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The touch action button's look: faint while there is nothing to use nearby, lit and pulsing while an
    /// interaction can be used (it always stays touchable and movable in the controls editor).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class InteractButtonView : MonoBehaviour
    {
        [SerializeField] private InteractionState m_State;
        [SerializeField] private MobileControlsLayout m_Layout;
        [Tooltip("The drawn part (pulses); the control itself is scaled by the layout's size setting.")]
        [SerializeField] private RectTransform m_Visual;
        [Tooltip("Shows the pictogram of what can be used (the default one when nothing is near).")]
        [SerializeField] private UnityEngine.UI.Image m_Icon;

        private Sprite m_DefaultIcon;
        [SerializeField, Range(0f, 1f)] private float m_IdleAlpha = 0.3f;
        [SerializeField, Min(0f)] private float m_Pulse = 0.08f;

        private CanvasGroup m_Group;

        private void Awake()
        {
            m_Group = GetComponent<CanvasGroup>();
            m_DefaultIcon = m_Icon != null ? m_Icon.sprite : null;
        }

        private void Update()
        {
            bool editing = m_Layout != null && m_Layout.IsEditing;
            bool ready = m_State != null && m_State.Visible && m_State.Available;
            float target = editing || ready ? 1f : m_IdleAlpha;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, target, Time.unscaledDeltaTime * 4f);
            float pulse = ready && !editing ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * m_Pulse : 1f;
            if (m_Visual != null)
                m_Visual.localScale = new Vector3(pulse, pulse, 1f);
            if (m_Icon != null)
            {
                Sprite icon = m_State != null && m_State.Visible && m_State.Icon != null ? m_State.Icon : m_DefaultIcon;
                if (m_Icon.sprite != icon)
                    m_Icon.sprite = icon;
            }
        }
    }
}
