using UnityEngine;
using ZombiCar.Waves;

namespace ZombiCar.Core
{
    /// <summary>
    /// The map's music follows the fight: the boss track cross-fades in when a boss comes out and the map's own track
    /// comes back once it is dead; the closer the end, the tenser it plays (<see cref="AudioManager.SetMusicIntensity"/>)
    /// — a little from wave 25, more on the last wave and with a boss out, most when the boss is enraged.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private MapConfig m_Map;
        [SerializeField] private WaveState m_Waves;
        [Tooltip("The waves from which the music grows tenser.")]
        [SerializeField, Min(1)] private int m_TenseFrom = 25;

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
            AudioManager.SetMusicIntensity(0f);                          // the menu plays calm again
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

        private void Update() => AudioManager.SetMusicIntensity(Intensity());

        /// <summary>How tense the fight is now, 0..1.</summary>
        public float Intensity()
        {
            bool boss = !string.IsNullOrEmpty(m_Waves.BossName);
            if (boss && m_Waves.BossEnraged)
                return 1f;
            int count = Mathf.Max(1, m_Waves.WaveCount);
            int wave = m_Waves.Wave;
            float late = wave >= m_TenseFrom ? Mathf.Lerp(0.3f, 0.7f, Mathf.InverseLerp(m_TenseFrom, count, wave)) : 0f;
            return Mathf.Max(late, boss ? 0.45f : 0f);
        }
    }
}
