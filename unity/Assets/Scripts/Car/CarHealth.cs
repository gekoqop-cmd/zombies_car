using System;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Car
{
    /// <summary>
    /// Health of the car the player defends; zombies hit it in melee. Its damage stage (Intact / Damaged / Wrecked,
    /// thresholds in <see cref="CarConfig"/>) follows the health in both directions — <see cref="CarDamageView"/>
    /// switches the model's look on <see cref="StageChanged"/>. Still planned: the game-over when it is destroyed.
    /// </summary>
    public sealed class CarHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private CarConfig m_Config;
        [Tooltip("The car's body collider: zombies walk to its nearest point and hit from there.")]
        [SerializeField] private Collider m_Body;

        private int m_Health;
        private CarDamageStage m_Stage;

        /// <summary>Health changed (damage or repair).</summary>
        public event Action Changed;

        /// <summary>The damage stage changed; raised right when the health crosses a threshold.</summary>
        public event Action<CarDamageStage> StageChanged;

        public CarConfig Config => m_Config;
        public int MaxHealth => m_Config != null ? m_Config.MaxHealth : 1;
        public int Health => m_Health;
        public bool IsAlive => m_Health > 0;
        public CarDamageStage Stage => m_Stage;
        public Collider Body => m_Body;

        private void Awake()
        {
            m_Health = MaxHealth;
            m_Stage = StageFor(m_Health);
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (!IsAlive || amount <= 0)
                return;
            SetHealth(m_Health - amount);
            if (!IsAlive)
                Debug.Log("[CarHealth] The car is destroyed (game over is not implemented yet).", this);
        }

        /// <summary>Restores health (repair); the look goes back up the stages as thresholds are crossed.</summary>
        public void Repair(int amount)
        {
            if (amount > 0)
                SetHealth(m_Health + amount);
        }

        private void SetHealth(int health)
        {
            m_Health = Mathf.Clamp(health, 0, MaxHealth);
            CarDamageStage stage = StageFor(m_Health);
            bool stageChanged = stage != m_Stage;
            m_Stage = stage;
            if (stageChanged)
                StageChanged?.Invoke(stage);
            Changed?.Invoke();
        }

        private CarDamageStage StageFor(int health) => m_Config != null ? m_Config.StageFor(health) : CarDamageStage.Intact;
    }
}
