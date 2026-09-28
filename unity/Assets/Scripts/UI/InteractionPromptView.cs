using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// HUD prompt at the bottom of the screen for what the player can use nearby ("НАЖМИТЕ [E]  МАСТЕРСКАЯ ..."),
    /// from <see cref="InteractionState"/>: the key cap on desktop (touch screens use the action button), greyed out
    /// with the reason when it cannot be used now, and a progress bar while a timed interaction runs.
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private InteractionState m_State;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private TMP_Text m_Text;
        [Tooltip("\"НАЖМИТЕ [E]\" part; hidden on touch screens and while the prompt is only a reason.")]
        [SerializeField] private GameObject m_KeyHint;
        [SerializeField] private GameObject m_ProgressRoot;
        [SerializeField] private Image m_ProgressFill;
        [SerializeField] private Color m_AvailableColor = Color.white;
        [SerializeField] private Color m_UnavailableColor = new Color(0.72f, 0.7f, 0.68f, 1f);
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.15f;

        private void OnEnable()
        {
            if (m_State != null)
                m_State.Changed += Refresh;
            Refresh();
            m_Group.alpha = m_State != null && m_State.Visible ? 1f : 0f;
        }

        private void OnDisable()
        {
            if (m_State != null)
                m_State.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (m_State == null || !m_State.Visible)
                return;
            m_Text.SetText(m_State.Text);
            m_Text.color = m_State.Available ? m_AvailableColor : m_UnavailableColor;
            if (m_KeyHint != null)
                m_KeyHint.SetActive(m_State.Available && !DeviceInfo.IsMobile);
        }

        private void Update()
        {
            bool visible = m_State != null && m_State.Visible && !GamePause.IsPaused;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / m_FadeTime);
            float progress = m_State != null ? m_State.Progress : -1f;
            bool channel = progress >= 0f;
            if (m_ProgressRoot != null && m_ProgressRoot.activeSelf != channel)
                m_ProgressRoot.SetActive(channel);
            if (channel && m_ProgressFill != null)
                m_ProgressFill.fillAmount = progress;
        }
    }
}
