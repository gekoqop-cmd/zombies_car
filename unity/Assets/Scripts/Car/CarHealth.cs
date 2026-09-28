using System;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.Car
{
    /// <summary>
    /// Health of the car the player defends; zombies hit it in melee. Its damage stage (Intact / Damaged / Wrecked,
    /// thresholds in <see cref="CarConfig"/>) follows the health in both directions — <see cref="CarDamageView"/>
    /// switches the model's look on <see cref="StageChanged"/>. Workshop upgrades and cards of the run
    /// (<see cref="RunState"/>) add durability, armour (less damage taken), self-repair, repairs and spikes that hurt
    /// the zombies hitting it. At zero health <see cref="Destroyed"/> is raised once (the run ends).
    /// </summary>
    public sealed class CarHealth : MonoBehaviour, IDamageable
    {
        private const float c_MaxArmor = 0.75f;

        [SerializeField] private CarConfig m_Config;
        [Tooltip("The car's body collider: zombies walk to its nearest point and hit from there.")]
        [SerializeField] private Collider m_Body;
        [SerializeField] private RunState m_Run;
        [Tooltip("Durability shared with the HUD (workshop status, the under-attack warning).")]
        [SerializeField] private CarState m_State;

        private int m_Health;
        private CarDamageStage m_Stage;
        private float m_RegenBuffer;
        private bool m_Destroyed;

        /// <summary>Health changed (damage or repair).</summary>
        public event Action Changed;

        /// <summary>The damage stage changed; raised right when the health crosses a threshold.</summary>
        public event Action<CarDamageStage> StageChanged;

        /// <summary>The car took a hit (the damage after armour): warning, hit sound.</summary>
        public event Action<int> Damaged;

        /// <summary>Health reached zero (raised once).</summary>
        public event Action Destroyed;

        public CarConfig Config => m_Config;
        public int MaxHealth => (m_Config != null ? m_Config.MaxHealth : 1) + BonusHealth;
        public int Health => m_Health;
        public bool IsAlive => m_Health > 0;
        public CarDamageStage Stage => m_Stage;
        public Collider Body => m_Body;

        /// <summary>Damage a zombie takes every time it hits the car (spikes).</summary>
        public int Spikes => m_Run != null ? m_Run.Modifiers.Whole(StatEffect.CarSpikes) : 0;

        /// <summary>Share of every hit the car ignores.</summary>
        public float Armor => m_Run != null ? Mathf.Clamp(m_Run.Modifiers[StatEffect.CarArmor], 0f, c_MaxArmor) : 0f;

        private int BonusHealth => m_Run != null ? m_Run.Modifiers.Whole(StatEffect.CarMaxHealth) : 0;

        private void Awake()
        {
            m_Health = MaxHealth;
            m_Stage = StageFor(m_Health);
        }

        private void OnEnable()
        {
            if (m_Run == null)
                return;
            m_Run.EffectApplied += OnEffectApplied;
            m_Run.ModifiersChanged += Publish;
        }

        private void OnDisable()
        {
            if (m_Run == null)
                return;
            m_Run.EffectApplied -= OnEffectApplied;
            m_Run.ModifiersChanged -= Publish;
        }

        // The run is reset (MapBootstrap) before this Start: start full with no bonuses.
        private void Start() => SetHealth(MaxHealth);

        private void Update()
        {
            if (!IsAlive || m_Run == null || m_Run.IsOver)
                return;
            float regen = m_Run.Modifiers[StatEffect.CarRegen];
            if (regen <= 0f || m_Health >= MaxHealth)
            {
                m_RegenBuffer = 0f;
                return;
            }
            m_RegenBuffer += regen * Time.deltaTime;
            if (m_RegenBuffer >= 1f)
            {
                int whole = Mathf.FloorToInt(m_RegenBuffer);
                m_RegenBuffer -= whole;
                Repair(whole);
            }
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (!IsAlive || amount <= 0)
                return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - Armor)));
            SetHealth(m_Health - damage);
            Damaged?.Invoke(damage);
            if (m_State != null)
                m_State.RaiseAttacked(damage);
            if (!IsAlive && !m_Destroyed)
            {
                m_Destroyed = true;
                Debug.Log("[CarHealth] The car is destroyed.", this);
                Destroyed?.Invoke();
            }
        }

        /// <summary>Restores health (repair); the look goes back up the stages as thresholds are crossed.</summary>
        public void Repair(int amount)
        {
            if (amount > 0 && IsAlive)
                SetHealth(m_Health + amount);
        }

        private void OnEffectApplied(StatEffect effect, float value)
        {
            switch (effect)
            {
                case StatEffect.CarMaxHealth:
                case StatEffect.RepairCar:
                    Repair(Mathf.RoundToInt(value));          // new durability comes repaired; a repair heals
                    Changed?.Invoke();
                    break;
            }
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
            Publish();
        }

        private void Publish()
        {
            if (m_State != null)
                m_State.Set(m_Health, MaxHealth, Armor, Spikes);
        }

        private CarDamageStage StageFor(int health) => m_Config != null ? m_Config.StageFor(health) : CarDamageStage.Intact;
    }
}
