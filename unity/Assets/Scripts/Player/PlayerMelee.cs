using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;

namespace ZombiCar.Player
{
    /// <summary>
    /// The axe: while it is the active weapon (<see cref="WeaponSwitcher"/>), a short press of attack (a click, a tap or a
    /// short flick of the aim stick) is a quick swing towards the aim, the two painted swings taking turns. Attack held
    /// longer lifts the axe and keeps it up — the player can still aim and walk, slower — until it is let go: then it
    /// comes down, the full charge hitting several times harder, further and pushing harder (let go early: a quick
    /// swing's damage). After a short wind-up every blow hits every zombie within reach and inside the arc in front of
    /// the player (and shootable props such as explosive barrels), pushing them back. Cards of the run make the axe hit
    /// harder and reach further.
    /// </summary>
    public sealed class PlayerMelee : MonoBehaviour
    {
        private static readonly Collider[] s_Hits = new Collider[48];

        /// <summary>Which painted blow is playing.</summary>
        public enum Blow { SwingA, SwingB, Slam }

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
        private float m_PendingWindup;
        private float m_Power = 1f;                  // damage multiplier of the pending blow
        private bool m_ChargedBlow;
        private Vector3 m_Direction = Vector3.forward;
        private bool m_Held;
        private float m_HeldSince;
        private float m_ChargeStart = float.NegativeInfinity;
        private int m_Swings;

        /// <summary>The axe is the weapon in hand.</summary>
        public bool Active { get; set; }

        public MeleeConfig Config => m_Config;
        public float Range => m_Config.Range * (m_Run != null ? m_Run.Modifiers.Multiplier(StatEffect.AxeRange) : 1f);
        public int Damage => Mathf.RoundToInt(m_Config.Damage * (m_Run != null ? m_Run.Modifiers.Multiplier(StatEffect.AxeDamage) : 1f));

        /// <summary>Damage of a fully charged blow.</summary>
        public int ChargedDamage => Mathf.RoundToInt(Damage * m_Config.ChargedMultiplier);

        /// <summary>The axe is out: in hand as the weapon, or still finishing a swing after switching back.</summary>
        public bool InHand => Active || IsCharging || SwingTime < m_Config.Windup + c_SwingShownTime;

        // Strike + recovery of the swing animation: the axe stays out that long.
        private const float c_SwingShownTime = 0.4f;

        /// <summary>Seconds since the current swing started (large when not swinging).</summary>
        public float SwingTime => Time.time - m_SwingStart;

        /// <summary>Seconds from the start of the current blow to its hit.</summary>
        public float CurrentWindup => m_ChargedBlow || CurrentBlow == Blow.Slam ? m_Config.SlamWindup : m_Config.Windup;

        /// <summary>The painted blow of the current (or last) swing.</summary>
        public Blow CurrentBlow { get; private set; }

        /// <summary>The axe is held up for the charged blow.</summary>
        public bool IsCharging { get; private set; }

        /// <summary>Seconds the axe has been up (0 when not charging).</summary>
        public float ChargeHeld => IsCharging ? Time.time - m_ChargeStart : 0f;

        /// <summary>How charged the lifted axe is, 0..1 (1: the full blow).</summary>
        public float Charge => IsCharging ? Mathf.Clamp01(ChargeHeld / m_Config.ChargeTime) : 0f;

        /// <summary>The last blow was a fully charged one.</summary>
        public bool LastBlowCharged => m_ChargedBlow;

        /// <summary>The body sprite shows the axe this frame (its painted frames): the separate axe is hidden.</summary>
        public bool ViewDrawsAxe { get; set; }

        /// <summary>Walking speed multiplier (slower with the axe held up).</summary>
        public float MoveMultiplier => IsCharging ? m_Config.ChargeMoveSpeed : 1f;

        /// <summary>Direction of the current swing (level).</summary>
        public Vector3 Direction => m_Direction;

        /// <summary>A swing started (sound, the arm and axe animation, the lunge).</summary>
        public event Action Swung;

        /// <summary>The axe went up for a charged blow.</summary>
        public event Action ChargeStarted;

        /// <summary>The axe reached the full charge.</summary>
        public event Action ChargeFull;

        /// <summary>A blow landed on this many targets (hit sound, shake).</summary>
        public event Action<int> Hit;

        private bool m_FullAnnounced;

        private void Start() => m_Input = m_InputProvider != null ? m_InputProvider.Input : null;

        private void Update()
        {
            if (m_Pending && SwingTime >= m_PendingWindup)
                Strike();
            if (!Active || m_Input == null || (m_Run != null && m_Run.IsOver) || (m_Health != null && !m_Health.IsAlive))
            {
                IsCharging = false;
                m_Held = false;
                return;
            }

            bool held = m_Input.FireHeld;
            if (held && !m_Held)
                m_HeldSince = Time.time;
            if (held && !IsCharging && !m_Pending && Time.time - m_HeldSince >= m_Config.HoldThreshold)
                BeginCharge();
            if (IsCharging)
            {
                m_Direction = AimDirection();
                if (!m_FullAnnounced && Charge >= 1f)
                {
                    m_FullAnnounced = true;
                    AudioManager.PlaySfx(Sound.WeaponSwitch, 0.8f, 1.5f);   // a click: it is ready
                    ChargeFull?.Invoke();
                }
            }
            if (!held && m_Held)
            {
                if (IsCharging)
                    Release();
                else if (Time.time - m_HeldSince < m_Config.HoldThreshold && Time.time >= m_NextSwing)
                    Swing();
            }
            m_Held = held;
            // A tap on the touch screen's aim stick: one quick swing.
            if (m_Input.QuickTap && !IsCharging && Time.time >= m_NextSwing)
                Swing();
        }

        /// <summary>Starts a quick swing now (input, tests): the two painted swings take turns.</summary>
        public void Swing()
        {
            CurrentBlow = m_Swings++ % 2 == 0 ? Blow.SwingA : Blow.SwingB;
            StartBlow(m_Config.Windup, 1f, false);
            AudioManager.PlaySfx(Sound.AxeSwing);
        }

        /// <summary>Lifts the axe for the charged blow (input, tests).</summary>
        public void BeginCharge()
        {
            IsCharging = true;
            m_FullAnnounced = false;
            m_ChargeStart = Time.time;
            m_Direction = AimDirection();
            AudioManager.PlaySfx(Sound.AxeSwing, 0.6f, 0.75f);
            ChargeStarted?.Invoke();
        }

        /// <summary>Brings the lifted axe down (input, tests): the full charge hits hardest.</summary>
        public void Release()
        {
            if (!IsCharging)
                return;
            bool full = Charge >= 1f;
            IsCharging = false;
            CurrentBlow = Blow.Slam;
            StartBlow(m_Config.SlamWindup, full ? m_Config.ChargedMultiplier : 1f, full);
            AudioManager.PlaySfx(Sound.AxeSwing, 1f, full ? 0.8f : 1f);
        }

        private void StartBlow(float windup, float power, bool charged)
        {
            m_NextSwing = Time.time + m_Config.Interval;
            m_SwingStart = Time.time;
            m_Pending = true;
            m_PendingWindup = windup;
            m_Power = power;
            m_ChargedBlow = charged;
            m_Direction = AimDirection();
            Swung?.Invoke();
        }

        private Vector3 AimDirection()
        {
            Vector3 d = m_Weapon != null ? m_Weapon.ShotDirection : transform.forward;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : m_Direction;
        }

        private void Strike()
        {
            m_Pending = false;
            m_Struck.Clear();
            float range = Range * (m_ChargedBlow ? m_Config.ChargedReach : 1f);
            float knockback = m_Config.Knockback * (m_ChargedBlow ? m_Config.ChargedKnockback : 1f);
            int damage = Mathf.RoundToInt(Damage * m_Power);
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
                target.TakeDamage(damage, m_Direction);
                if (target is ZombieController zombie && zombie.IsAlive)
                    zombie.Push(m_Direction * knockback);
                landed++;
            }
            if (m_ChargedBlow)
                CameraShake.Add(0.3f);                                   // the charged blow shakes even on a miss
            if (landed <= 0)
                return;
            AudioManager.PlaySfx(Sound.AxeHit, 1f, m_ChargedBlow ? 0.8f : 1f);
            CameraShake.Add(m_ChargedBlow ? 0.25f : 0.12f);
            GameFeel.Impact();                                           // the blow has weight: a hit stop, a buzz
            Hit?.Invoke(landed);
        }
    }
}
