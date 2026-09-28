using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;

namespace ZombiCar.Player
{
    /// <summary>
    /// The axe: while it is the active weapon (<see cref="WeaponSwitcher"/>), attack (left mouse button / the aim stick)
    /// swings it towards the aim; after a short wind-up the blow hits every zombie within reach and inside the arc in
    /// front of the player (and shootable props such as explosive barrels), pushing them back. Cards of the run make
    /// the axe hit harder and reach further.
    /// </summary>
    public sealed class PlayerMelee : MonoBehaviour
    {
        private static readonly Collider[] s_Hits = new Collider[48];

        [SerializeField] private MeleeConfig m_Config;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [Tooltip("The gun's aim (aim assist included) gives the swing direction.")]
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private PlayerHealth m_Health;
        [SerializeField] private RunState m_Run;

        private readonly HashSet<IDamageable> m_Struck = new HashSet<IDamageable>();
        private IPlayerInput m_Input;
        private float m_NextSwing;
        private float m_SwingStart = float.NegativeInfinity;
        private bool m_Pending;
        private Vector3 m_Direction = Vector3.forward;

        /// <summary>The axe is the weapon in hand.</summary>
        public bool Active { get; set; }

        public MeleeConfig Config => m_Config;
        public float Range => m_Config.Range * (m_Run != null ? m_Run.Modifiers.Multiplier(StatEffect.AxeRange) : 1f);
        public int Damage => Mathf.RoundToInt(m_Config.Damage * (m_Run != null ? m_Run.Modifiers.Multiplier(StatEffect.AxeDamage) : 1f));

        /// <summary>Seconds since the current swing started (large when not swinging).</summary>
        public float SwingTime => Time.time - m_SwingStart;

        /// <summary>Direction of the current swing (level).</summary>
        public Vector3 Direction => m_Direction;

        /// <summary>A swing started (sound, the arm and axe animation, the lunge).</summary>
        public event Action Swung;

        /// <summary>A blow landed on this many targets (hit sound, shake).</summary>
        public event Action<int> Hit;

        private void Start() => m_Input = m_InputProvider != null ? m_InputProvider.Input : null;

        private void Update()
        {
            if (m_Pending && SwingTime >= m_Config.Windup)
                Strike();
            if (!Active || m_Input == null || (m_Run != null && m_Run.IsOver) || (m_Health != null && !m_Health.IsAlive))
                return;
            bool attack = m_Input.FireHeld || m_Input.FirePressed || m_Input.QuickTap;
            if (attack && Time.time >= m_NextSwing)
                Swing();
        }

        /// <summary>Starts a swing now (input, tests).</summary>
        public void Swing()
        {
            m_NextSwing = Time.time + m_Config.Interval;
            m_SwingStart = Time.time;
            m_Pending = true;
            Vector3 d = m_Weapon != null ? m_Weapon.ShotDirection : transform.forward;
            d.y = 0f;
            m_Direction = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            AudioManager.PlaySfx(Sound.AxeSwing);
            Swung?.Invoke();
        }

        private void Strike()
        {
            m_Pending = false;
            m_Struck.Clear();
            float range = Range;
            float halfArc = m_Config.Arc * 0.5f;
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            int mask = Layers.Enemies | (1 << Layers.c_ObstacleIndex);
            int count = Physics.OverlapSphereNonAlloc(origin, range, s_Hits, mask, QueryTriggerInteraction.Ignore);
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                Collider c = s_Hits[i];
                var target = c.GetComponentInParent<IDamageable>();
                bool enemy = ((1 << c.gameObject.layer) & Layers.Enemies) != 0;
                if (target == null || !target.IsAlive || (!enemy && !(target is IShootable)) || !m_Struck.Add(target))
                    continue;
                Vector3 to = c.ClosestPoint(origin) - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-4f && Vector3.Angle(m_Direction, to) > halfArc)
                    continue;
                target.TakeDamage(Damage, m_Direction);
                if (target is ZombieController zombie && zombie.IsAlive)
                    zombie.Push(m_Direction * m_Config.Knockback);
                landed++;
            }
            if (landed <= 0)
                return;
            AudioManager.PlaySfx(Sound.AxeHit);
            CameraShake.Add(0.12f);
            Hit?.Invoke(landed);
        }
    }
}
