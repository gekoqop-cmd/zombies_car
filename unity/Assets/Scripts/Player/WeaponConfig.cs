using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>Weapon parameters (placeholder gun).</summary>
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "ZombiCar/Weapon Config")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Tooltip("Metres a bullet travels before it disappears; also the length of the aim strip.")]
        [SerializeField, Min(0.5f)] private float m_Range = 14f;
        [Tooltip("Shots per second while the fire button is held.")]
        [SerializeField, Min(0.1f)] private float m_FireRate = 6f;
        [Tooltip("Health points one bullet takes from a zombie.")]
        [SerializeField, Min(1)] private int m_Damage = 10;
        [SerializeField, Min(1)] private int m_MagazineSize = 24;
        [Tooltip("Spare ammo at the start of the map.")]
        [SerializeField, Min(0)] private int m_StartReserve = 120;
        [Tooltip("Reserve ammo never grows past this (caps the between-waves car resupply).")]
        [SerializeField, Min(0)] private int m_MaxReserve = 240;
        [SerializeField, Min(0f)] private float m_ReloadTime = 1.4f;
        [SerializeField, Min(0.1f)] private float m_BulletSpeed = 35f;
        [SerializeField, Min(0.01f)] private float m_BulletRadius = 0.08f;
        [Tooltip("Bullets created up front so shooting never allocates.")]
        [SerializeField, Min(0)] private int m_PoolPrewarm = 24;

        public float Range => m_Range;
        public float FireRate => m_FireRate;
        public float ShotInterval => 1f / m_FireRate;
        public int Damage => m_Damage;
        public int MagazineSize => m_MagazineSize;
        public int StartReserve => m_StartReserve;
        public int MaxReserve => m_MaxReserve;
        public float ReloadTime => m_ReloadTime;
        public float BulletSpeed => m_BulletSpeed;
        public float BulletRadius => m_BulletRadius;
        public int PoolPrewarm => m_PoolPrewarm;
    }
}
