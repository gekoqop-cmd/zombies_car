using UnityEngine;

namespace ZombiCar.Waves
{
    /// <summary>STUB (no logic yet): wave settings of a map. The wave spawner that reads it is not written yet.</summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "ZombiCar/Wave Config")]
    public sealed class WaveConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int m_WaveCount = 10;
        [SerializeField, Min(1f)] private float m_WaveDuration = 60f;
        [SerializeField, Min(0f)] private float m_BreakBetweenWaves = 10f;
        [SerializeField, Min(1)] private int m_EnemiesFirstWave = 8;
        [Tooltip("Additional enemies per next wave.")]
        [SerializeField, Min(0)] private int m_EnemiesGrowthPerWave = 4;

        public int WaveCount => m_WaveCount;
        public float WaveDuration => m_WaveDuration;
        public float BreakBetweenWaves => m_BreakBetweenWaves;
        public int EnemiesFirstWave => m_EnemiesFirstWave;
        public int EnemiesGrowthPerWave => m_EnemiesGrowthPerWave;
    }
}
