using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// The run's progress at the top of the HUD: a line from wave 1 to the last wave, filled up to the current one,
    /// with a boss mark at every boss wave (10 / 20 / 30). The next boss's mark pulses; a mark goes away once its wave
    /// is reached. The wave number and the timer are the HUD's texts beside it.
    /// While a boss is alive the pill shows the boss instead: its name, the wave and a big health bar that eases down
    /// with a light "lost" trail (orange in the enraged phase); after the kill it drains and fades back to the waves.
    /// </summary>
    public sealed class WaveProgressView : MonoBehaviour
    {
        private const float c_SwapTime = 0.35f;
        private const float c_LingerAfterKill = 0.9f;

        [SerializeField] private WaveState m_Waves;
        [SerializeField] private Image m_Fill;
        [Tooltip("Boss marks, in the order of Boss Waves.")]
        [SerializeField] private RectTransform[] m_Marks = System.Array.Empty<RectTransform>();
        [SerializeField] private int[] m_BossWaves = { 10, 20, 30 };
        [SerializeField, Min(0.01f)] private float m_FillSpeed = 0.6f;

        [Header("Boss bar")]
        [SerializeField] private CanvasGroup m_WaveGroup;
        [SerializeField] private CanvasGroup m_BossGroup;
        [SerializeField] private RectTransform m_BossIcon;
        [Tooltip("Disc under the boss icon: red, purple for the final boss (the last boss wave).")]
        [SerializeField] private Image m_BossDisc;
        [SerializeField] private Color m_DiscColor = new Color(0.75f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color m_FinalBossColor = new Color(0.52f, 0.16f, 0.78f, 1f);
        [SerializeField] private TMP_Text m_BossName;
        [SerializeField] private TMP_Text m_BossWave;
        [SerializeField] private TMP_Text m_BossValue;
        [SerializeField] private Image m_BossFill;
        [SerializeField] private string m_BossWaveFormat = "ВОЛНА {0}";
        [SerializeField] private Color m_BossColor = new Color(0.86f, 0.18f, 0.16f, 1f);
        [SerializeField] private Color m_EnragedColor = new Color(1f, 0.45f, 0.12f, 1f);
        [SerializeField, Min(0.01f)] private float m_HealthTime = 0.15f;

        private float m_Shown = -1f;
        private float m_BossShow;
        private float m_BossGoneAt = float.NegativeInfinity;
        private bool m_HadBoss;
        private float m_Health = 1f;
        private float m_HealthVelocity;
        private float m_HitAt = float.NegativeInfinity;
        private int m_LastHealth = -1;

        private void Update()
        {
            int count = Mathf.Max(1, m_Waves.WaveCount > 0 ? m_Waves.WaveCount : 30);
            int wave = Mathf.Max(1, m_Waves.Wave);
            // Filled up to the wave being played (a finished wave counts whole).
            float target = Mathf.Clamp01((wave - (m_Waves.IsWaveRunning ? 0.5f : 1f)) / count);
            if (m_Waves.Phase == WavePhase.Finished)
                target = 1f;
            m_Shown = m_Shown < 0f ? target : Mathf.MoveTowards(m_Shown, target, Time.unscaledDeltaTime * m_FillSpeed);
            m_Fill.fillAmount = m_Shown;

            int next = -1;
            for (int i = 0; i < m_Marks.Length && i < m_BossWaves.Length; i++)
            {
                bool reached = wave >= m_BossWaves[i];
                m_Marks[i].gameObject.SetActive(!reached);
                if (!reached && next < 0)
                    next = i;
            }
            for (int i = 0; i < m_Marks.Length; i++)
            {
                if (!m_Marks[i].gameObject.activeSelf)
                    continue;
                // The coming boss pulses harder the closer it is.
                float closeness = i == next ? Mathf.Clamp01(1f - (m_BossWaves[i] - wave) / 5f) : 0f;
                float pulse = 1f + (i == next ? 0.08f + 0.12f * closeness : 0f) * Mathf.Sin(Time.unscaledTime * (3f + 5f * closeness));
                m_Marks[i].localScale = Vector3.one * pulse;
            }
            UpdateBoss(wave);
        }

        private void UpdateBoss(int wave)
        {
            if (m_BossGroup == null || m_WaveGroup == null)
                return;
            bool boss = m_Waves.HasBoss;
            if (boss && !m_HadBoss)
                OnBossAppeared(wave);
            else if (!boss && m_HadBoss)
                m_BossGoneAt = Time.time;
            m_HadBoss = boss;

            bool show = boss || Time.time - m_BossGoneAt < c_LingerAfterKill;
            m_BossShow = Mathf.MoveTowards(m_BossShow, show ? 1f : 0f, Time.unscaledDeltaTime / c_SwapTime);
            m_BossGroup.alpha = m_BossShow;
            m_WaveGroup.alpha = 1f - m_BossShow;
            m_BossGroup.gameObject.SetActive(m_BossShow > 0f);
            if (m_BossShow <= 0f)
                return;

            int health = boss ? m_Waves.BossHealth : 0;
            int max = Mathf.Max(1, m_Waves.BossMaxHealth);
            if (health != m_LastHealth)
            {
                if (m_LastHealth >= 0 && health < m_LastHealth)
                    m_HitAt = Time.time;                                     // the grey damage trail: DamageTrailBar
                m_LastHealth = health;
                m_BossValue.SetText(boss ? $"{health} / {max}" : "0");
            }
            float target = (float)health / max;
            m_Health = Mathf.SmoothDamp(m_Health, target, ref m_HealthVelocity, m_HealthTime);
            m_BossFill.fillAmount = m_Health;
            m_BossFill.color = m_Waves.BossEnraged ? m_EnragedColor : m_BossColor;
            // The icon jolts on every hit.
            float hit = 1f - Mathf.Clamp01((Time.time - m_HitAt) / 0.2f);
            if (m_BossIcon != null)
                m_BossIcon.localScale = Vector3.one * (1f + 0.15f * hit * hit);
        }

        private void OnBossAppeared(int wave)
        {
            m_Health = 1f;
            m_HealthVelocity = 0f;
            m_LastHealth = -1;
            m_BossName.SetText(m_Waves.BossName);
            if (m_BossWave != null)
                m_BossWave.SetText(string.Format(m_BossWaveFormat, wave));
            if (m_BossDisc != null)
            {
                bool final = m_BossWaves.Length > 0 && wave >= m_BossWaves[m_BossWaves.Length - 1];
                m_BossDisc.color = final ? m_FinalBossColor : m_DiscColor;
            }
        }
    }
}
