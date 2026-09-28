using UnityEngine;
using ZombiCar.Waves;

namespace ZombiCar.Core
{
    /// <summary>
    /// Switches the map's music for bosses: the boss track cross-fades in when a boss comes out and the map's own
    /// track comes back once it is dead.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private MapConfig m_Map;
        [SerializeField] private WaveState m_Waves;

        private bool m_BossMusic;

        private void OnEnable()
        {
            m_Waves.BossAppeared += OnBoss;
            m_Waves.Changed += OnChanged;
        }

        private void OnDisable()
        {
            m_Waves.BossAppeared -= OnBoss;
            m_Waves.Changed -= OnChanged;
        }

        private void OnBoss(string name)
        {
            if (m_Map == null || m_Map.BossMusic == null)
                return;
            m_BossMusic = true;
            AudioManager.PlayMusic(m_Map.BossMusic);
        }

        private void OnChanged()
        {
            if (!m_BossMusic || !string.IsNullOrEmpty(m_Waves.BossName))
                return;
            m_BossMusic = false;
            AudioManager.PlayMusic(m_Map.Music);
        }
    }
}
