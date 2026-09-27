using System;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Placeholder gun: fires pooled bullets from the Muzzle (the barrel on the player sprite) while fire is held,
    /// with fire rate, magazine, reserve and reload (R or an empty magazine) from <see cref="WeaponConfig"/>.
    /// With a cursor the shot goes level from the muzzle through the resolved aim point (<see cref="AimSolver"/>),
    /// otherwise along <see cref="IPlayerInput.AimDirection"/>.
    /// Publishes ammo to <see cref="AmmoState"/> for the HUD and raises <see cref="Fired"/> for the player's sprite.
    /// </summary>
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponConfig m_Config;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private Transform m_Muzzle;
        [SerializeField] private BulletPool m_Pool;
        [SerializeField] private AmmoState m_Ammo;

        private IPlayerInput m_Input;
        private int m_InMagazine;
        private int m_Reserve;
        private float m_NextShotTime;
        private float m_ReloadEndTime;
        private bool m_IsReloading;
        private int m_BlockMask;
        private AimSolution m_Aim;

        // Shots closer than this to the muzzle keep the input direction (a target inside the gun would flip it).
        private const float c_MinShotDistance = 0.25f;

        public WeaponConfig Config => m_Config;
        public bool IsReloading => m_IsReloading;
        public Transform Muzzle => m_Muzzle;

        /// <summary>Where the current aim points (valid with a cursor); used by the aim gizmos.</summary>
        public AimSolution Aim => m_Aim;

        /// <summary>Level direction the next bullet will take from the muzzle.</summary>
        public Vector3 ShotDirection { get; private set; } = Vector3.forward;

        /// <summary>Raised on every shot (muzzle flash, shooting pose, sound).</summary>
        public event Action Fired;

        /// <summary>Raised when a reload starts (animation stretched to <see cref="WeaponConfig.ReloadTime"/>, sound).</summary>
        public event Action ReloadStarted;

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
            m_InMagazine = m_Config.MagazineSize;
            m_Reserve = m_Config.StartReserve;
            m_Pool.Prewarm(m_Config.PoolPrewarm);
            Publish();
        }

        private void Update()
        {
            if (m_IsReloading && Time.time >= m_ReloadEndTime)
                FinishReload();
            UpdateAim();

            bool wantsToFire = m_Input.FireHeld || m_Input.FirePressed || m_Input.QuickTap;
            if (wantsToFire && !m_IsReloading && Time.time >= m_NextShotTime)
                Fire();

            bool magazineEmpty = m_InMagazine == 0;
            bool requested = m_Input.ReloadPressed && m_InMagazine < m_Config.MagazineSize;
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
            bullet.Launch(m_Muzzle.position, ShotDirection, m_Config, m_BlockMask, m_Pool.Release);

            m_InMagazine--;
            m_NextShotTime = Time.time + m_Config.ShotInterval;
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

        /// <summary>Tops up the reserve, capped at <see cref="WeaponConfig.MaxReserve"/> (the car resupplying the player between waves).</summary>
        public void Resupply(int amount)
        {
            if (amount <= 0)
                return;
            m_Reserve = Mathf.Min(m_Reserve + amount, m_Config.MaxReserve);
            Publish();
        }

        private void StartReload()
        {
            m_IsReloading = true;
            m_ReloadEndTime = Time.time + m_Config.ReloadTime;
            Publish();
            ReloadStarted?.Invoke();
        }

        private void FinishReload()
        {
            int loaded = Mathf.Min(m_Config.MagazineSize - m_InMagazine, m_Reserve);
            m_InMagazine += loaded;
            m_Reserve -= loaded;
            m_IsReloading = false;
            Publish();
        }

        private void Publish()
        {
            if (m_Ammo != null)
                m_Ammo.Set(m_InMagazine, m_Reserve, m_IsReloading, m_Config.ReloadTime);
        }
    }
}
