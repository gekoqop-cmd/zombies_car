using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ПРОПУСТИТЬ ПЕРЕРЫВ [E]" at the bottom of the HUD during the break between waves: E (when E is not needed for the
    /// workshop or the car) or a click / tap starts the next wave at once (<see cref="WaveState.RequestSkipBreak"/>).
    /// Hidden during a wave, behind a pause, and while another interaction prompt shows. Runs on unscaled time.
    /// </summary>
    public sealed class BreakSkipView : MonoBehaviour
    {
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private RunState m_Run;
        [SerializeField] private InteractionState m_Interaction;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private Button m_Button;
        [Tooltip("The \"E\" key badge (hidden on phones).")]
        [SerializeField] private GameObject m_KeyBadge;
        [SerializeField] private TMP_Text m_Label;

        /// <summary>The skip is offered now (tests).</summary>
        public bool Offered { get; private set; }

        private int m_HiddenFrame = -1;

        private void Awake()
        {
            m_Button.onClick.AddListener(Skip);
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            m_KeyBadge.SetActive(!DeviceInfo.IsMobile);
        }

        /// <summary>Starts the next wave now (button, E, tests).</summary>
        public void Skip()
        {
            if (!Offered)
                return;
            AudioManager.PlaySfx(Sound.UiClick);
            m_Waves.RequestSkipBreak();
        }

        private void Update()
        {
            Offered = m_Waves.Phase == WavePhase.Countdown && m_Waves.SecondsLeft > 1 && !GamePause.IsPaused &&
                      (m_Run == null || !m_Run.IsOver) && (m_Interaction == null || !m_Interaction.Visible);
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, Offered ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            m_Group.blocksRaycasts = m_Group.interactable = Offered;
            if (!Offered)
            {
                m_HiddenFrame = Time.frameCount;
                return;
            }
            m_Label.SetText($"ПРОПУСТИТЬ ПЕРЕРЫВ  •  {m_Waves.SecondsLeft} С");
            Keyboard keys = Keyboard.current;
            // E also closes the workshop: only a press while the skip was already showing counts.
            if (keys != null && keys.eKey.wasPressedThisFrame && Time.frameCount - m_HiddenFrame > 1)
                Skip();
        }
    }
}
