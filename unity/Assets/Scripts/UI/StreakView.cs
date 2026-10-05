using TMPro;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The kill streak on the HUD (<see cref="RunState.Streak"/>): "СЕРИЯ 12" from 5 kills in a row, with the scrap
    /// multiplier it brings ("×1.25" from 10, "×1.5" from 25, "×2" from 50) in gold. It pops with every kill, flares and
    /// chimes on a new multiplier, and fades out when the streak breaks. Runs on unscaled time.
    /// </summary>
    public sealed class StreakView : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private TMP_Text m_Count;
        [SerializeField] private TMP_Text m_Multiplier;
        [Tooltip("Kills in a row before the streak shows.")]
        [SerializeField, Min(1)] private int m_ShowFrom = 5;

        private float m_PopAt = float.NegativeInfinity;
        private float m_FlareAt = float.NegativeInfinity;
        private float m_LastMultiplier = 1f;
        private bool m_Visible;

        private void OnEnable()
        {
            m_Run.StreakChanged += OnStreak;
            m_Group.alpha = 0f;
        }

        private void OnDisable() => m_Run.StreakChanged -= OnStreak;

        private void OnStreak()
        {
            int streak = m_Run.Streak;
            float multiplier = m_Run.StreakMultiplier;
            m_Visible = streak >= m_ShowFrom;
            if (m_Visible)
            {
                m_Count.SetText($"СЕРИЯ {streak}");
                m_Multiplier.SetText(multiplier > 1f ? $"×{multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}" : string.Empty);
                m_PopAt = Time.unscaledTime;
                if (multiplier > m_LastMultiplier)
                {
                    m_FlareAt = Time.unscaledTime;
                    AudioManager.PlaySfx(Sound.CardPick);
                }
            }
            m_LastMultiplier = multiplier;
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Visible ? 1f : 0f, Time.unscaledDeltaTime / (m_Visible ? 0.15f : 0.6f));
            float pop = UiMotion.Punch((Time.unscaledTime - m_PopAt) / 0.22f) * 0.14f;
            float flare = UiMotion.Punch((Time.unscaledTime - m_FlareAt) / 0.5f) * 0.45f;
            m_Body.localScale = Vector3.one * (1f + pop + flare);
        }
    }
}
