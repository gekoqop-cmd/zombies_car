using UnityEngine;
using ZombiCar.Player;

namespace ZombiCar.Waves
{
    /// <summary>
    /// Placeholder until the wave system exists: shows a fixed wave number with a countdown in the HUD and resets
    /// the player stats shown there. Replace with the real wave spawner.
    /// </summary>
    public sealed class WaveTimerPlaceholder : MonoBehaviour
    {
        [SerializeField] private WaveState m_WaveState;
        [SerializeField] private PlayerStats m_PlayerStats;
        [SerializeField, Min(1)] private int m_Wave = 5;
        [SerializeField, Min(0f)] private float m_StartSeconds = 36f;

        private float m_TimeLeft;

        private void Start()
        {
            m_TimeLeft = m_StartSeconds;
            if (m_PlayerStats != null)
                m_PlayerStats.ResetToDefaults();
            Publish();
        }

        private void Update()
        {
            if (m_TimeLeft <= 0f)
                return;
            m_TimeLeft = Mathf.Max(0f, m_TimeLeft - Time.deltaTime);
            Publish();
        }

        // WaveState ignores repeated values, so the HUD only updates once per second.
        private void Publish()
        {
            if (m_WaveState != null)
                m_WaveState.Set(m_Wave, Mathf.CeilToInt(m_TimeLeft));
        }
    }
}
