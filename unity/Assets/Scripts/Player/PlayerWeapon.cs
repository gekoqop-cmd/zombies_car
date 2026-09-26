using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Placeholder gun: fires pooled bullets along <see cref="IPlayerInput.AimDirection"/> while fire is held,
    /// with fire rate, magazine, reserve and automatic reload from <see cref="WeaponConfig"/>.
    /// Publishes ammo to <see cref="AmmoState"/> for the HUD.
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

        public WeaponConfig Config => m_Config;

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

            bool wantsToFire = m_Input.FireHeld || m_Input.FirePressed || m_Input.QuickTap;
            if (wantsToFire && !m_IsReloading && Time.time >= m_NextShotTime)
                Fire();

            if (m_InMagazine == 0 && !m_IsReloading && m_Reserve > 0)
                StartReload();
        }

        private void Fire()
        {
            if (m_InMagazine <= 0)
                return;

            Vector3 direction = m_Input.AimDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < Mathf.Epsilon)
                direction = transform.forward;
            direction.Normalize();

            Bullet bullet = m_Pool.Get();
            bullet.Launch(m_Muzzle.position, direction, m_Config, m_BlockMask, m_Pool.Release);

            m_InMagazine--;
            m_NextShotTime = Time.time + m_Config.ShotInterval;
            Publish();
        }

        private void StartReload()
        {
            m_IsReloading = true;
            m_ReloadEndTime = Time.time + m_Config.ReloadTime;
            Publish();
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
                m_Ammo.Set(m_InMagazine, m_Reserve, m_IsReloading);
        }
    }
}
