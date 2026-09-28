using System;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.Player
{
    /// <summary>
    /// The gun: fires pooled bullets from the Muzzle (the barrel on the player sprite) while fire is held, with fire
    /// rate, magazine, reserve and reload (R or an empty magazine) from <see cref="WeaponConfig"/>; the level-up cards
    /// of the run (<see cref="RunState"/>) add to them on top (the config itself is never changed).
    /// With a cursor the shot goes level from the muzzle through the resolved aim point (<see cref="AimSolver"/>),
    /// otherwise along <see cref="IPlayerInput.AimDirection"/>. While holstered (the axe is out) it neither fires nor
    /// reloads. Publishes ammo to <see cref="AmmoState"/> for the HUD and raises <see cref="Fired"/> for the sprite.
    /// </summary>
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponConfig m_Config;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private Transform m_Muzzle;
        [SerializeField] private BulletPool m_Pool;
        [SerializeField] private AmmoState m_Ammo;
        [SerializeField] private RunState m_Run;

        private IPlayerInput m_Input;
        private int m_InMagazine;
        private int m_Reserve;
        private float m_NextShotTime;
        private float m_ReloadEndTime;
        private bool m_IsReloading;
        private int m_BlockMask;
        private AimSolution m_Aim;
        private bool m_Holstered;

        // Shots closer than this to the muzzle keep the input direction (a target inside the gun would flip it).
        private const float c_MinShotDistance = 0.25f;

        public WeaponConfig Config => m_Config;
        public bool IsReloading => m_IsReloading;
        public Transform Muzzle => m_Muzzle;
        public int InMagazine => m_InMagazine;
        public int Reserve => m_Reserve;

        /// <summary>No rounds at all: the magazine and the reserve are empty.</summary>
        public bool IsEmpty => m_InMagazine == 0 && m_Reserve == 0;

        /// <summary>The gun is put away (the axe is out): no shots, no reloads.</summary>
        public bool Holstered
        {
            get => m_Holstered;
            set
            {
                m_Holstered = value;
                if (value && m_IsReloading)
                {
                    m_IsReloading = false;                         // switching weapons cancels the reload
                    Publish();
                }
            }
        }

        private RunModifiers Mods => m_Run != null ? m_Run.Modifiers : null;

        public int MagazineSize => m_Config.MagazineSize + (Mods?.Whole(StatEffect.MagazineSize) ?? 0);
        public int MaxReserve => m_Config.MaxReserve + (Mods?.Whole(StatEffect.MaxReserve) ?? 0);
        public float ShotInterval => m_Config.ShotInterval / (Mods?.Multiplier(StatEffect.FireRate) ?? 1f);
        public float ReloadTime => m_Config.ReloadTime / (Mods?.Multiplier(StatEffect.ReloadSpeed) ?? 1f);
        public float Range => m_Config.Range * (Mods?.Multiplier(StatEffect.WeaponRange) ?? 1f);
        public int Damage => Mathf.Max(1, Mathf.RoundToInt(m_Config.Damage * (Mods?.Multiplier(StatEffect.WeaponDamage) ?? 1f)));

        /// <summary>Where the current aim points (valid with a cursor); used by the aim gizmos.</summary>
        public AimSolution Aim => m_Aim;

        /// <summary>Level direction the next bullet will take from the muzzle.</summary>
        public Vector3 ShotDirection { get; private set; } = Vector3.forward;

        /// <summary>Raised on every shot (muzzle flash, shooting pose, sound).</summary>
        public event Action Fired;

        /// <summary>Raised when a reload starts (animation stretched to <see cref="ReloadTime"/>, sound).</summary>
        public event Action ReloadStarted;

        private void OnEnable()
        {
            if (m_Run != null)
                m_Run.EffectApplied += OnEffectApplied;
        }

        private void OnDisable()
        {
            if (m_Run != null)
                m_Run.EffectApplied -= OnEffectApplied;
        }

        private void Start()
        {
            m_Input = m_InputProvider != null ? m_InputProvider.Input : null;
            if (m_Input == null || m_Config == null || m_Pool == null || m_Muzzle == null)
            {
                Debug.LogError("[PlayerWeapon] Missing references.", this);
                enabled = false;
                return;
            }
            m_BlockMask = Layers.ProjectileBlockers;
            m_InMagazine = MagazineSize;
            m_Reserve = m_Config.StartReserve;
            m_Pool.Prewarm(m_Config.PoolPrewarm);
            Publish();
        }

        private void Update()
        {
            if (m_IsReloading && Time.time >= m_ReloadEndTime)
                FinishReload();
            UpdateAim();
            if (m_Holstered || (m_Run != null && m_Run.IsOver))
                return;

            bool wantsToFire = m_Input.FireHeld || m_Input.FirePressed || m_Input.QuickTap;
            if (wantsToFire && !m_IsReloading && Time.time >= m_NextShotTime)
                Fire();

            bool magazineEmpty = m_InMagazine == 0;
            bool requested = m_Input.ReloadPressed && m_InMagazine < MagazineSize;
            if ((magazineEmpty || requested) && !m_IsReloading && m_Reserve > 0)
                StartReload();
        }

        private void Fire()
        {
            if (m_InMagazine <= 0)
                return;

            // The bullet is drawn at the barrel of the sprite; it collides over the whole height band from
            // WeaponConfig.HitBandBottom up to the barrel, so low cover and small zombies stop it too.
            Bullet bullet = m_Pool.Get();
            var stats = new ShotStats
            {
                Damage = Damage,
                Range = Range,
                Pierce = Mods?.Whole(StatEffect.BulletPierce) ?? 0,
                CritChance = Mods?[StatEffect.CritChance] ?? 0f,
            };
            bullet.Launch(m_Muzzle.position, ShotDirection, m_Config, stats, m_BlockMask, m_Pool.Release);

            m_InMagazine--;
            m_NextShotTime = Time.time + ShotInterval;
            Publish();
            Fired?.Invoke();
        }

        private void UpdateAim()
        {
            m_Aim = m_Input.TryGetAimRay(out Ray ray)
                ? AimSolver.FromRay(ray, m_Config.MuzzleHeight, m_Config)
                : default;
            if (m_Aim.IsValid && AimSolver.LevelDirection(m_Muzzle.position, m_Aim.Point, c_MinShotDistance, out Vector3 toAim))
            {
                ShotDirection = toAim;
                return;
            }
            Vector3 direction = m_Input.AimDirection;
            direction.y = 0f;
            ShotDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : transform.forward;
        }

        /// <summary>Tops up the reserve, capped at <see cref="MaxReserve"/> (the car resupplying the player between waves).</summary>
        public void Resupply(int amount)
        {
            if (amount <= 0)
                return;
            m_Reserve = Mathf.Min(m_Reserve + amount, MaxReserve);
            Publish();
        }

        /// <summary>Full magazine and full reserve (the refill at the car).</summary>
        public void Refill()
        {
            m_IsReloading = false;
            m_InMagazine = MagazineSize;
            m_Reserve = MaxReserve;
            Publish();
        }

        /// <summary>The magazine and the reserve are both full.</summary>
        public bool IsFull => m_InMagazine >= MagazineSize && m_Reserve >= MaxReserve;

        private void OnEffectApplied(StatEffect effect, float value)
        {
            if (effect == StatEffect.MaxReserve)
                Resupply(Mathf.RoundToInt(value));                // a bigger ammo bag comes filled
        }

        private void StartReload()
        {
            m_IsReloading = true;
            m_ReloadEndTime = Time.time + ReloadTime;
            Publish();
            ReloadStarted?.Invoke();
        }

        private void FinishReload()
        {
            int loaded = Mathf.Min(MagazineSize - m_InMagazine, m_Reserve);
            m_InMagazine += loaded;
            m_Reserve -= loaded;
            m_IsReloading = false;
            Publish();
        }

        private void Publish()
        {
            if (m_Ammo != null)
                m_Ammo.Set(m_InMagazine, m_Reserve, m_IsReloading, ReloadTime);
        }
    }
}
