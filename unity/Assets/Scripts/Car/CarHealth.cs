using System;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Car
{
    /// <summary>
    /// Health of the car the player defends; zombies hit it in melee. The damage-stage objects of the Car_Player
    /// prefab (DMG1_Hood_Dented, DMG2_Bumper_Bent, DMG3_Cargo_Fallen) switch on as health drops below the thresholds.
    /// Still planned: repair, the M_Car_Damaged / M_Car_Wrecked material swap and the game-over when it is destroyed.
    /// </summary>
    public sealed class CarHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int m_MaxHealth = 400;
        [Tooltip("Health fractions at which the damage stages 1..3 switch on.")]
        [SerializeField] private float[] m_DamageStageThresholds = { 0.75f, 0.5f, 0.25f };
        [Tooltip("Objects shown from the matching damage stage on.")]
        [SerializeField] private GameObject[] m_DamageStageObjects;
        [Tooltip("The car's body collider: zombies walk to its nearest point and hit from there.")]
        [SerializeField] private Collider m_Body;

        private int m_Health;

        public event Action Changed;

        public int MaxHealth => m_MaxHealth;
        public int Health => m_Health;
        public bool IsAlive => m_Health > 0;
        public float[] DamageStageThresholds => m_DamageStageThresholds;
        public Collider Body => m_Body;

        private void Awake()
        {
            m_Health = m_MaxHealth;
            ApplyStages();
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (!IsAlive)
                return;
            m_Health = Mathf.Max(0, m_Health - amount);
            ApplyStages();
            Changed?.Invoke();
            if (!IsAlive)
                Debug.Log("[CarHealth] The car is destroyed (game over is not implemented yet).", this);
        }

        private void ApplyStages()
        {
            if (m_DamageStageObjects == null)
                return;
            float fraction = (float)m_Health / m_MaxHealth;
            for (int i = 0; i < m_DamageStageObjects.Length; i++)
            {
                bool reached = i < m_DamageStageThresholds.Length && fraction <= m_DamageStageThresholds[i];
                if (m_DamageStageObjects[i] != null)
                    m_DamageStageObjects[i].SetActive(reached);
            }
        }
    }
}
