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

        [Header("Aiming")]
        [Tooltip("Height of the barrel on the player sprite, metres (world, set by the prefab builder from the frames). " +
                 "Bullets are drawn level at this height and the cursor ray is intersected with a plane at this height, " +
                 "so every shot passes exactly under the cursor.")]
        [SerializeField, Range(0.5f, 3f)] private float m_MuzzleHeight = 1.05f;
        [Tooltip("Lowest point a bullet collides at, metres: the bullet sweeps a vertical capsule from here up to the " +
                 "barrel, so low cover (barriers, barrels) and small zombies stop it although it is drawn higher.")]
        [SerializeField, Min(0f)] private float m_HitBandBottom = 0.35f;
        [Tooltip("Radius of the cast along the cursor ray that picks the enemy under the cursor, metres.")]
        [SerializeField, Min(0f)] private float m_CursorPickRadius = 0.15f;
        [Tooltip("Aim assist: a shot aimed within Aim Assist Radius of an enemy goes to the enemy's centre.")]
        [SerializeField] private bool m_AimAssist = true;
        [SerializeField, Min(0f)] private float m_AimAssistRadius = 0.5f;

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
        public float MuzzleHeight => m_MuzzleHeight;
        public float HitBandBottom => m_HitBandBottom;
        public float CursorPickRadius => m_CursorPickRadius;
        public bool AimAssist => m_AimAssist;
        public float AimAssistRadius => m_AimAssistRadius;
    }
}
