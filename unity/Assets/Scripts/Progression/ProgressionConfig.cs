using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// Experience curve and the rewards for clearing a wave (kills give the XP / scrap of their <c>ZombieConfig</c>).
    /// Experience to the next level = Base + Linear x (level - 1) + Quadratic x (level - 1)^2.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionConfig", menuName = "ZombiCar/Progression Config")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Header("Experience to the next level")]
        [SerializeField, Min(1)] private int m_XpBase = 30;
        [SerializeField, Min(0)] private int m_XpLinear = 20;
        [SerializeField, Min(0)] private int m_XpQuadratic = 2;

        [Header("Wave cleared")]
        [SerializeField, Min(0)] private int m_WaveMoneyBase = 10;
        [SerializeField, Min(0)] private int m_WaveMoneyPerWave = 3;
        [SerializeField, Min(0)] private int m_WaveXpBase = 10;
        [SerializeField, Min(0)] private int m_WaveXpPerWave = 2;

        [Header("Cards")]
        [Tooltip("Cards offered on every level-up.")]
        [SerializeField, Range(1, 4)] private int m_CardsPerLevelUp = 3;

        public int CardsPerLevelUp => m_CardsPerLevelUp;

        public int XpToNext(int level)
        {
            int n = Mathf.Max(0, level - 1);
            return m_XpBase + m_XpLinear * n + m_XpQuadratic * n * n;
        }

        public int WaveMoney(int wave) => m_WaveMoneyBase + m_WaveMoneyPerWave * wave;

        public int WaveXp(int wave) => m_WaveXpBase + m_WaveXpPerWave * wave;
    }
}
