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
    /// While the break's ammo still waits at the car, the skip asks first: "пополни патроны", then "без патронов?", and
    /// only the third press ("ну ладно...") starts the wave. Hidden during a wave, behind a pause, and while another
    /// interaction prompt shows. Runs on unscaled time.
    /// </summary>
    public sealed class BreakSkipView : MonoBehaviour
    {
        private static readonly string[] s_Nags = { "ПОПОЛНИ ПАТРОНЫ У МАШИНЫ :)", "ТЫ БУДЕШЬ БЕЗ ПАТРОНОВ?", "НУ ЛАДНО..." };

        [SerializeField] private WaveState m_Waves;
        [SerializeField] private RunState m_Run;
        [SerializeField] private InteractionState m_Interaction;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private Button m_Button;
        [Tooltip("The \"E\" key badge (hidden on phones).")]
        [SerializeField] private GameObject m_KeyBadge;
        [SerializeField] private TMP_Text m_Label;
        [Tooltip("Seconds a reminder stays on the button before the countdown text comes back.")]
        [SerializeField, Min(0.1f)] private float m_NagTime = 2.5f;
        [Tooltip("Seconds \"НУ ЛАДНО...\" is read before the wave starts.")]
        [SerializeField, Min(0f)] private float m_GiveUpDelay = 0.9f;

        /// <summary>The skip is offered now (tests).</summary>
        public bool Offered { get; private set; }

        /// <summary>Reminders shown in this break (0..3; the third one starts the wave).</summary>
        public int NagStep => m_Nag;

        private int m_HiddenFrame = -1;
        private int m_Nag;
        private float m_NagAt = float.NegativeInfinity;
        private bool m_GivingUp;
        private float m_GiveUpAt;

        private void Awake()
        {
            m_Button.onClick.AddListener(Skip);
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            m_KeyBadge.SetActive(!DeviceInfo.IsMobile);
        }

        /// <summary>
        /// Starts the next wave now (button, E, tests) — unless the ammo still waits at the car: then a reminder, and
        /// the wave only on the third press.
        /// </summary>
        public void Skip()
        {
            if (!Offered || m_GivingUp)
                return;
            AudioManager.PlaySfx(Sound.UiClick);
            if (m_Waves.RefillWaiting && m_Nag < s_Nags.Length)
            {
                m_Nag++;
                m_NagAt = Time.unscaledTime;
                if (m_Nag == s_Nags.Length)
                {
                    m_GivingUp = true;                               // "НУ ЛАДНО..." is read, then the wave comes
                    m_GiveUpAt = Time.unscaledTime + m_GiveUpDelay;
                }
                return;
            }
            m_Waves.RequestSkipBreak();
        }

        private void Update()
        {
            if (m_Waves.Phase != WavePhase.Countdown)
            {
                m_Nag = 0;                                           // every break asks again
                m_GivingUp = false;
            }
            if (m_GivingUp && Time.unscaledTime >= m_GiveUpAt && !GamePause.IsPaused)
            {
                m_GivingUp = false;
                m_Waves.RequestSkipBreak();
            }
            Offered = m_Waves.Phase == WavePhase.Countdown && m_Waves.SecondsLeft > 1 && !GamePause.IsPaused &&
                      (m_Run == null || !m_Run.IsOver) && (m_Interaction == null || !m_Interaction.Visible);
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, Offered ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            m_Group.blocksRaycasts = m_Group.interactable = Offered;
            if (!Offered)
            {
                m_HiddenFrame = Time.frameCount;
                return;
            }
            float sinceNag = Time.unscaledTime - m_NagAt;
            bool nagging = m_Nag > 0 && (sinceNag < m_NagTime || m_GivingUp);
            m_Label.SetText(nagging ? s_Nags[m_Nag - 1] : $"ПРОПУСТИТЬ ПЕРЕРЫВ  •  {m_Waves.SecondsLeft} С");
            m_Label.rectTransform.localScale = Vector3.one * (1f + UiMotion.Punch(sinceNag / 0.3f) * 0.12f);
            Keyboard keys = Keyboard.current;
            // E also closes the workshop: only a press while the skip was already showing counts.
            if (keys != null && keys.eKey.wasPressedThisFrame && Time.frameCount - m_HiddenFrame > 1)
                Skip();
        }
    }
}
