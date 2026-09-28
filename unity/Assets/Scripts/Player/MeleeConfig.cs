using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>The axe (second weapon): damage, reach, arc and timing of a swing.</summary>
    [CreateAssetMenu(fileName = "MeleeConfig", menuName = "ZombiCar/Melee Config")]
    public sealed class MeleeConfig : ScriptableObject
    {
        [Tooltip("Health points a blow takes from every zombie in the arc.")]
        [SerializeField, Min(1)] private int m_Damage = 40;
        [Tooltip("Reach from the player, metres.")]
        [SerializeField, Min(0.5f)] private float m_Range = 2f;
        [Tooltip("Width of the swing in front of the player, degrees.")]
        [SerializeField, Range(10f, 360f)] private float m_Arc = 160f;
        [Tooltip("Seconds from one swing to the next while attack is held.")]
        [SerializeField, Min(0.1f)] private float m_Interval = 0.5f;
        [Tooltip("Seconds from the start of the swing to the blow (the wind-up).")]
        [SerializeField, Min(0f)] private float m_Windup = 0.1f;
        [Tooltip("How far a blow pushes zombies back, metres.")]
        [SerializeField, Min(0f)] private float m_Knockback = 0.8f;

        public int Damage => m_Damage;
        public float Range => m_Range;
        public float Arc => m_Arc;
        public float Interval => m_Interval;
        public float Windup => m_Windup;
        public float Knockback => m_Knockback;
    }
}
