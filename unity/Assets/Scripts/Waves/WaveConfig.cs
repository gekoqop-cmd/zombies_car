using System;
using UnityEngine;
using ZombiCar.Enemies;

namespace ZombiCar.Waves
{
    /// <summary>Wave settings of a map, read by <see cref="WaveDirector"/>.</summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "ZombiCar/Wave Config")]
    public sealed class WaveConfig : ScriptableObject
    {
        /// <summary>A zombie type that can appear from a given wave on, picked by weight.</summary>
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private ZombieConfig m_Zombie;
            [SerializeField, Min(1)] private int m_FromWave = 1;
            [SerializeField, Min(0f)] private float m_Weight = 1f;

            public Entry(ZombieConfig zombie, int fromWave, float weight)
            {
                m_Zombie = zombie;
                m_FromWave = fromWave;
                m_Weight = weight;
            }

            public ZombieConfig Zombie => m_Zombie;
            public int FromWave => m_FromWave;
            public float Weight => m_Weight;
        }

        /// <summary>A boss that comes out at the start of its wave.</summary>
        [Serializable]
        public sealed class BossWave
        {
            [SerializeField, Min(1)] private int m_Wave = 10;
            [SerializeField] private ZombieConfig m_Boss;
            [Tooltip("Share of the wave's usual zombies that still come with the boss.")]
            [SerializeField, Range(0f, 1f)] private float m_RegularShare = 0.5f;

            public BossWave(int wave, ZombieConfig boss, float regularShare)
            {
                m_Wave = wave;
                m_Boss = boss;
                m_RegularShare = regularShare;
            }

            public int Wave => m_Wave;
            public ZombieConfig Boss => m_Boss;
            public float RegularShare => m_RegularShare;
        }

        [SerializeField, Min(1)] private int m_WaveCount = 10;
        [SerializeField, Min(1f)] private float m_WaveDuration = 60f;
        [SerializeField, Min(0f)] private float m_BreakBetweenWaves = 25f;
        [Tooltip("Countdown before the first wave.")]
        [SerializeField, Min(0f)] private float m_FirstWaveDelay = 8f;
        [SerializeField, Min(1)] private int m_EnemiesFirstWave = 8;
        [Tooltip("Additional enemies per next wave.")]
        [SerializeField, Min(0)] private int m_EnemiesGrowthPerWave = 4;
        [Tooltip("Share of the wave duration over which its zombies arrive (the rest is time to finish them off).")]
        [SerializeField, Range(0.1f, 1f)] private float m_SpawnWindow = 0.6f;
        [Tooltip("No new zombies while this many are alive.")]
        [SerializeField, Min(1)] private int m_MaxAlive = 30;
        [SerializeField] private Entry[] m_Enemies = Array.Empty<Entry>();
        [Tooltip("Boss waves: the wave lasts until its boss and all its zombies are dead (no time limit).")]
        [SerializeField] private BossWave[] m_Bosses = Array.Empty<BossWave>();

        public int WaveCount => m_WaveCount;
        public float WaveDuration => m_WaveDuration;
        public float BreakBetweenWaves => m_BreakBetweenWaves;
        public float FirstWaveDelay => m_FirstWaveDelay;
        public int EnemiesFirstWave => m_EnemiesFirstWave;
        public int EnemiesGrowthPerWave => m_EnemiesGrowthPerWave;
        public float SpawnWindow => m_SpawnWindow;
        public int MaxAlive => m_MaxAlive;
        public Entry[] Enemies => m_Enemies;
        public BossWave[] Bosses => m_Bosses;

        /// <summary>The boss of <paramref name="wave"/>, or null.</summary>
        public BossWave BossFor(int wave)
        {
            foreach (BossWave b in m_Bosses)
            {
                if (b != null && b.Boss != null && b.Wave == wave)
                    return b;
            }
            return null;
        }

        public int EnemiesInWave(int wave) => m_EnemiesFirstWave + m_EnemiesGrowthPerWave * (wave - 1);

        /// <summary>Weighted random zombie type allowed in <paramref name="wave"/> (null if none is).</summary>
        public ZombieConfig Pick(int wave)
        {
            float total = 0f;
            foreach (Entry e in m_Enemies)
            {
                if (e.Zombie != null && e.FromWave <= wave)
                    total += e.Weight;
            }
            float roll = UnityEngine.Random.value * total;
            ZombieConfig last = null;
            foreach (Entry e in m_Enemies)
            {
                if (e.Zombie == null || e.FromWave > wave)
                    continue;
                last = e.Zombie;
                roll -= e.Weight;
                if (roll <= 0f)
                    return e.Zombie;
            }
            return last;
        }

#if UNITY_EDITOR
        public void EditorSetEnemies(Entry[] enemies) => m_Enemies = enemies;

        public void EditorSetBosses(BossWave[] bosses, int waveCount)
        {
            m_Bosses = bosses;
            m_WaveCount = Mathf.Max(m_WaveCount, waveCount);
        }
#endif
    }
}
