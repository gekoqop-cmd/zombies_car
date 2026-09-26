using UnityEngine;

namespace ZombiCar.Car
{
    /// <summary>
    /// STUB (no logic yet): health of the car the player defends.
    /// Planned: damage from zombies, repair, and the damage stages already present in the Car_Player prefab
    /// (DMG1_Hood_Dented, DMG2_Bumper_Bent, DMG3_Cargo_Fallen objects + M_Car_Damaged / M_Car_Wrecked materials).
    /// </summary>
    public sealed class CarHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int m_MaxHealth = 100;
        [Tooltip("Health fractions at which the damage stages 1..3 switch on.")]
        [SerializeField] private float[] m_DamageStageThresholds = { 0.75f, 0.5f, 0.25f };

        public int MaxHealth => m_MaxHealth;
        public float[] DamageStageThresholds => m_DamageStageThresholds;
    }
}
