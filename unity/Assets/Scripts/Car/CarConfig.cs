using UnityEngine;

namespace ZombiCar.Car
{
    /// <summary>Visual damage stage of the player's car (Car_Player model: atlas + damage parts per stage).</summary>
    public enum CarDamageStage
    {
        Intact,
        Damaged,
        Wrecked,
    }

    /// <summary>
    /// Durability of the player's car and the health at which its look changes stage. Thresholds are in HP:
    /// the car is Damaged at <see cref="DamagedAtOrBelow"/> HP and below, Wrecked at <see cref="WreckedAtOrBelow"/>
    /// and below. The durability plate colours its bar by the same thresholds.
    /// </summary>
    [CreateAssetMenu(fileName = "CarConfig", menuName = "ZombiCar/Car Config")]
    public sealed class CarConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int m_MaxHealth = 400;
        [Tooltip("Stage 2 (badly dented, damaged atlas, dented hood + bent bumper) from this health down.")]
        [SerializeField, Min(0)] private int m_DamagedAtOrBelow = 350;
        [Tooltip("Stage 3 (almost dead, wrecked atlas, all damage parts, smoke) from this health down.")]
        [SerializeField, Min(0)] private int m_WreckedAtOrBelow = 250;

        public int MaxHealth => m_MaxHealth;
        public int DamagedAtOrBelow => m_DamagedAtOrBelow;
        public int WreckedAtOrBelow => m_WreckedAtOrBelow;

        public CarDamageStage StageFor(int health) =>
            health <= m_WreckedAtOrBelow ? CarDamageStage.Wrecked
            : health <= m_DamagedAtOrBelow ? CarDamageStage.Damaged
            : CarDamageStage.Intact;

        private void OnValidate()
        {
            m_DamagedAtOrBelow = Mathf.Min(m_DamagedAtOrBelow, m_MaxHealth);
            m_WreckedAtOrBelow = Mathf.Min(m_WreckedAtOrBelow, m_DamagedAtOrBelow);
        }
    }
}
