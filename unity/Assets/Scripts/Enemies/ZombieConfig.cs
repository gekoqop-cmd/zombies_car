using UnityEngine;

namespace ZombiCar.Enemies
{
    public enum ZombieTarget
    {
        /// <summary>Goes for the car; turns to the player only when the player comes close.</summary>
        Car,
        /// <summary>Hunts the player; goes for the car only when the player is down.</summary>
        Player,
    }

    /// <summary>Stats of one zombie type (Data/Zombies). The prefab carries the sprites, collider and NavMesh agent.</summary>
    [CreateAssetMenu(fileName = "Zombie", menuName = "ZombiCar/Zombie Config")]
    public sealed class ZombieConfig : ScriptableObject
    {
        [SerializeField] private ZombieController m_Prefab;

        [Header("Health / movement")]
        [SerializeField, Min(1)] private int m_MaxHealth = 30;
        [Tooltip("Metres per second.")]
        [SerializeField, Min(0.1f)] private float m_MoveSpeed = 1.4f;
        [Tooltip("Every zombie gets a random speed within +- this share of the move speed.")]
        [SerializeField, Range(0f, 0.5f)] private float m_SpeedVariation = 0.15f;

        [Header("Attack")]
        [SerializeField, Min(1)] private int m_Damage = 4;
        [Tooltip("Seconds from the start of one attack to the next.")]
        [SerializeField, Min(0.1f)] private float m_AttackInterval = 1.3f;
        [Tooltip("Reach from the zombie's body to the surface of the target, metres.")]
        [SerializeField, Min(0.05f)] private float m_AttackRange = 0.6f;
        [Tooltip("Playback speed of the attack clip (lower = slower wind-up).")]
        [SerializeField, Min(0.1f)] private float m_AttackAnimSpeed = 1f;

        [Header("Targeting")]
        [SerializeField] private ZombieTarget m_Preference = ZombieTarget.Car;
        [Tooltip("Car-seekers switch to the player inside this radius (and back beyond 1.5x of it).")]
        [SerializeField, Min(0f)] private float m_AggroRadius = 4.5f;

        [Header("Getting shot")]
        [Tooltip("Seconds a hit stops the zombie (0 = never staggers).")]
        [SerializeField, Min(0f)] private float m_StaggerTime = 0.2f;
        [Tooltip("Metres a hit pushes the zombie back.")]
        [SerializeField, Min(0f)] private float m_Knockback = 0.2f;

        public ZombieController Prefab => m_Prefab;
        public int MaxHealth => m_MaxHealth;
        public float MoveSpeed => m_MoveSpeed;
        public float SpeedVariation => m_SpeedVariation;
        public int Damage => m_Damage;
        public float AttackInterval => m_AttackInterval;
        public float AttackRange => m_AttackRange;
        public float AttackAnimSpeed => m_AttackAnimSpeed;
        public ZombieTarget Preference => m_Preference;
        public float AggroRadius => m_AggroRadius;
        public float StaggerTime => m_StaggerTime;
        public float Knockback => m_Knockback;
    }
}
