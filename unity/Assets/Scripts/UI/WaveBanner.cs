using TMPro;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// Big banner under the wave panel for the moments of a run, with their sounds: a wave starts ("ВОЛНА 5", war
    /// horn), a wave is cleared (chime, what to do in the break), a boss comes out. It pops in, holds and fades
    /// (real time, so it is never frozen by a pause).
    /// </summary>
    public sealed class WaveBanner : MonoBehaviour
    {
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Subtitle;
        [SerializeField] private Color m_WaveColor = new Color(1f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color m_ClearedColor = new Color(0.55f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color m_BossColor = new Color(1f, 0.32f, 0.25f, 1f);
        [SerializeField, Min(0.01f)] private float m_In = 0.25f;
        [SerializeField, Min(0f)] private float m_Hold = 1.8f;
        [SerializeField, Min(0.01f)] private float m_Out = 0.5f;
        [SerializeField] private string m_BreakHint = "ПАТРОНЫ — У МАШИНЫ, ПРОКАЧКА — В МАСТЕРСКОЙ";

        private float m_ShownAt = float.NegativeInfinity;

        /// <summary>The title shown last (tests).</summary>
        public string LastTitle { get; private set; }

        /// <summary>The subtitle shown last (tests).</summary>
        public string LastSubtitle { get; private set; }

        private void OnEnable()
        {
            m_Waves.WaveStarted += OnWaveStarted;
            m_Waves.WaveEnded += OnWaveEnded;
            m_Waves.BossAppeared += OnBoss;
            m_Group.alpha = 0f;
        }

        private void OnDisable()
        {
            m_Waves.WaveStarted -= OnWaveStarted;
            m_Waves.WaveEnded -= OnWaveEnded;
            m_Waves.BossAppeared -= OnBoss;
        }

        private void OnWaveStarted(int wave)
        {
            bool last = m_Waves.WaveCount > 0 && wave >= m_Waves.WaveCount;
            Show($"ВОЛНА {wave}", last ? "ПОСЛЕДНЯЯ ВОЛНА!" : "ЗОМБИ ИДУТ!", m_WaveColor);
            AudioManager.PlaySfx(Sound.WaveStart);
        }

        private void OnWaveEnded(int wave)
        {
            if (m_Waves.Phase == WavePhase.Finished)
                return;                                              // the victory screen takes over
            Show("ВОЛНА ПРОЙДЕНА!", m_BreakHint, m_ClearedColor);
            AudioManager.PlaySfx(Sound.WaveEnd);
        }

        private void OnBoss(string name) => Show("БОСС!", name, m_BossColor);

        public void Show(string title, string subtitle, Color color)
        {
            LastTitle = title;
            LastSubtitle = subtitle;
            m_Title.SetText(title);
            m_Title.color = color;
            m_Subtitle.SetText(subtitle);
            m_ShownAt = Time.unscaledTime;
        }

        private void Update()
        {
            float t = Time.unscaledTime - m_ShownAt;
            float alpha = t < m_In ? t / m_In : t < m_In + m_Hold ? 1f : 1f - (t - m_In - m_Hold) / m_Out;
            m_Group.alpha = Mathf.Clamp01(alpha);
            float pop = UiMotion.BackOut(t / m_In, 2.4f);
            m_Body.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, pop);
        }
    }
}
