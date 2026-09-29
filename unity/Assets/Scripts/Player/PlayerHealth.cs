using System;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.Player
{
    /// <summary>
    /// Takes zombie hits for the player and writes them to <see cref="PlayerStats"/> (shown by the HUD). Level-up
    /// cards raise the maximum health, add regeneration and heal (<see cref="RunState"/>). At zero health
    /// <see cref="Died"/> is raised once (the run ends) and zombies stop going for the player.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerStats m_Stats;
        [SerializeField] private PlayerView m_View;
        [SerializeField] private RunState m_Run;

        private CharacterController m_Controller;
        private float m_RegenBuffer;
        private bool m_Dead;

        public bool IsAlive => m_Stats == null || m_Stats.Health > 0;

        /// <summary>Body radius, metres (zombies measure their reach from it).</summary>
        public float Radius => m_Controller.radius;

        /// <summary>A hit landed, with its damage (hurt sound, screen flash).</summary>
        public event Action<int> Damaged;

        /// <summary>Health reached zero (raised once).</summary>
        public event Action Died;

        private void Awake() => m_Controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            if (m_Run == null)
                return;
            m_Run.ModifiersChanged += ApplyModifiers;
            m_Run.EffectApplied += OnEffectApplied;
        }

        private void OnDisable()
        {
            if (m_Run == null)
                return;
            m_Run.ModifiersChanged -= ApplyModifiers;
            m_Run.EffectApplied -= OnEffectApplied;
        }

        private void Start() => ApplyModifiers();

        private void Update()
        {
            if (m_Run == null || m_Stats == null || !IsAlive || m_Run.IsOver)
                return;
            float regen = m_Run.Modifiers[StatEffect.PlayerRegen];
            if (regen <= 0f || m_Stats.Health >= m_Stats.MaxHealth)
            {
                m_RegenBuffer = 0f;
                return;
            }
            m_RegenBuffer += regen * Time.deltaTime;
            if (m_RegenBuffer >= 1f)
            {
                int whole = Mathf.FloorToInt(m_RegenBuffer);
                m_RegenBuffer -= whole;
                Heal(whole);
            }
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (!IsAlive || m_Stats == null || amount <= 0)
                return;
            m_Stats.SetHealth(m_Stats.Health - amount);
            AudioManager.PlaySfx(Sound.PlayerHurt);
            if (m_View != null)
                m_View.ShowHurt();
            Damaged?.Invoke(amount);
            if (!IsAlive && !m_Dead)
            {
                m_Dead = true;
                Debug.Log("[PlayerHealth] The player is down.", this);
                if (m_View != null)
                    m_View.ShowDown();
                Died?.Invoke();
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive || m_Stats == null || amount <= 0)
                return;
            m_Stats.SetHealth(m_Stats.Health + amount);
        }

        /// <summary>Back on their feet with full health (the revive for an ad).</summary>
        public void Revive()
        {
            if (m_Stats == null)
                return;
            m_Dead = false;
            m_Stats.SetHealth(m_Stats.MaxHealth);
            if (m_View != null)
                m_View.ShowUp();
        }

        private void ApplyModifiers()
        {
            if (m_Run != null && m_Stats != null)
                m_Stats.SetBonusMaxHealth(m_Run.Modifiers.Whole(StatEffect.PlayerMaxHealth));
        }

        private void OnEffectApplied(StatEffect effect, float value)
        {
            switch (effect)
            {
                case StatEffect.PlayerMaxHealth:
                    Heal(Mathf.RoundToInt(value));                 // the new health comes filled
                    break;
                case StatEffect.HealPlayerFull:
                    Heal(m_Stats.MaxHealth);
                    break;
            }
        }
    }
}
