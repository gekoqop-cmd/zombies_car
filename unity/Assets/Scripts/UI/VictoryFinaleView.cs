using TMPro;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The HUD side of the victory finale: the fanfare, a huge "ГОРОД СПАСЁН!" that slams in and pulses, and
    /// fireworks — bursts of coloured sparks over the screen that fall and fade — which keep going behind the victory
    /// screen. Everything runs on unscaled time (the map is in slow motion, then paused).
    /// </summary>
    public sealed class VictoryFinaleView : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Text;
        [SerializeField] private TMP_Text m_Subtitle;
        [Tooltip("The fireworks over the screen (they keep going behind the victory screen).")]
        [SerializeField] private UiFireworks m_Fireworks;
        [Tooltip("Seconds the big text stays before it fades (the victory screen comes then).")]
        [SerializeField, Min(0.5f)] private float m_TextTime = 3.6f;

        private float m_Start = float.NegativeInfinity;
        private bool m_Playing;

        /// <summary>The finale shows (tests).</summary>
        public bool IsPlaying => m_Playing;

        private void Awake()
        {
            m_Group.alpha = 0f;
            m_Group.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_Run.OutcomeChanged += OnOutcome;
            OnOutcome();
        }

        private void OnDisable() => m_Run.OutcomeChanged -= OnOutcome;

        private void OnOutcome()
        {
            bool victory = m_Run.Outcome == RunOutcome.Victory;
            if (victory == m_Playing)
                return;
            m_Playing = victory;
            m_Group.gameObject.SetActive(victory);
            m_Fireworks.Playing = victory;
            if (!victory)
                return;
            m_Start = Time.unscaledTime;
            m_Subtitle.SetText($"30 ВОЛН  •  {m_Run.BossKills} БОССА  •  {MetaProgress.FormatTime(m_Run.PlayTime)}");
            AudioManager.PlaySfx(Sound.Victory);
        }

        private void Update()
        {
            if (!m_Playing)
                return;
            float t = Time.unscaledTime - m_Start;
            // The text slams in big, settles, pulses; fades when the victory screen comes.
            float fade = 1f - Mathf.Clamp01((t - m_TextTime) / 0.4f);
            m_Group.alpha = Mathf.Clamp01(t / 0.15f) * fade;
            float slam = t < 0.35f ? Mathf.Lerp(2.4f, 1f, UiMotion.CubicOut(t / 0.35f)) : 1f + 0.03f * Mathf.Sin(t * 6f);
            m_Text.localScale = Vector3.one * slam;
            m_Text.localRotation = Quaternion.Euler(0f, 0f, -4f * Mathf.Clamp01(1f - t / 0.35f));
        }
    }
}
