using TMPro;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ДОСТИЖЕНИЕ ПОЛУЧЕНО!": a plate that slides down from the top over the victory screen ("ГОРОД СПАСЁН") when
    /// the run unlocked an achievement (<see cref="RunState.NewAchievement"/>) — its name and reward, taken in the
    /// menu's "ДОСТИЖЕНИЯ" — holds a few seconds and slides back up. Runs on unscaled time.
    /// </summary>
    public sealed class AchievementToast : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private GameOverScreen m_GameOver;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Reward;
        [Tooltip("Real seconds after the victory screen shows.")]
        [SerializeField, Min(0f)] private float m_Delay = 1.6f;
        [SerializeField, Min(0f)] private float m_Hold = 4f;
        [SerializeField] private float m_HiddenY = 260f;
        [SerializeField] private float m_ShownY = -24f;

        private float m_ShowAt = float.PositiveInfinity;
        private string m_Id;

        /// <summary>The toast came for this run (tests).</summary>
        public bool Shown { get; private set; }

        private void Start()
        {
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            m_Body.anchoredPosition = new Vector2(m_Body.anchoredPosition.x, m_HiddenY);
        }

        private void Update()
        {
            if (m_Id == null)
            {
                if (string.IsNullOrEmpty(m_Run.NewAchievement) || !m_GameOver.IsShown)
                    return;
                m_Id = m_Run.NewAchievement;
                Achievements.Info info = Achievements.Get(m_Id);
                m_Title.SetText(info.Title);
                m_Reward.SetText($"+{info.Reward} — ЗАБЕРИ В МЕНЮ «ДОСТИЖЕНИЯ»");
                m_ShowAt = Time.unscaledTime + m_Delay;
            }
            float t = Time.unscaledTime - m_ShowAt;
            if (t < 0f)
                return;
            if (!Shown)
            {
                Shown = true;
                AudioManager.PlaySfx(Sound.LegendaryDrop);
            }
            float slide = t < 0.45f ? UiMotion.BackOut(t / 0.45f, 1.6f) : t < 0.45f + m_Hold ? 1f : 1f - UiMotion.CubicOut((t - 0.45f - m_Hold) / 0.4f);
            m_Body.anchoredPosition = new Vector2(m_Body.anchoredPosition.x, Mathf.LerpUnclamped(m_HiddenY, m_ShownY, slide));
            m_Group.alpha = Mathf.Clamp01(slide * 3f);
            if (t > m_Hold + 1f)
                enabled = false;
        }
    }
}
