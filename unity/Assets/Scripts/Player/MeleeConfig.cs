using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// The axe (second weapon): damage, reach, arc and timing of a quick swing, and the charged blow (attack held: the
    /// axe goes up and stays there until released, then comes down much harder).
    /// </summary>
    [CreateAssetMenu(fileName = "MeleeConfig", menuName = "ZombiCar/Melee Config")]
    public sealed class MeleeConfig : ScriptableObject
    {
        [Tooltip("Health points a quick swing takes from every zombie in the arc.")]
        [SerializeField, Min(1)] private int m_Damage = 20;
        [Tooltip("Reach from the player, metres.")]
        [SerializeField, Min(0.5f)] private float m_Range = 2f;
        [Tooltip("Width of the swing in front of the player, degrees.")]
        [SerializeField, Range(10f, 360f)] private float m_Arc = 160f;
        [Tooltip("Seconds from one quick swing to the next.")]
        [SerializeField, Min(0.1f)] private float m_Interval = 0.5f;
        [Tooltip("Seconds from the start of a quick swing to the blow (the wind-up).")]
        [SerializeField, Min(0f)] private float m_Windup = 0.22f;
        [Tooltip("How far a blow pushes zombies back, metres.")]
        [SerializeField, Min(0f)] private float m_Knockback = 0.8f;

        [Header("Charged blow (attack held)")]
        [Tooltip("Attack held this long (seconds) lifts the axe for the charged blow; a shorter press is a quick swing.")]
        [SerializeField, Min(0f)] private float m_HoldThreshold = 0.18f;
        [Tooltip("Seconds of holding (after the lift starts) to a full charge.")]
        [SerializeField, Min(0.05f)] private float m_ChargeTime = 0.6f;
        [Tooltip("Damage of a fully charged blow: quick swing damage x this. A blow released early is a quick swing's.")]
        [SerializeField, Min(1f)] private float m_ChargedMultiplier = 3.5f;
        [Tooltip("A charged blow reaches this much further and pushes this much harder.")]
        [SerializeField, Min(1f)] private float m_ChargedReach = 1.25f;
        [SerializeField, Min(1f)] private float m_ChargedKnockback = 2f;
        [Tooltip("Seconds from the release to the charged blow (the axe is already up).")]
        [SerializeField, Min(0f)] private float m_SlamWindup = 0.07f;
        [Tooltip("Walking speed while the axe is held up, x.")]
        [SerializeField, Range(0.1f, 1f)] private float m_ChargeMoveSpeed = 0.55f;

        public int Damage => m_Damage;
        public float Range => m_Range;
        public float Arc => m_Arc;
        public float Interval => m_Interval;
        public float Windup => m_Windup;
        public float Knockback => m_Knockback;
        public float HoldThreshold => m_HoldThreshold;
        public float ChargeTime => m_ChargeTime;
        public float ChargedMultiplier => m_ChargedMultiplier;
        public float ChargedReach => m_ChargedReach;
        public float ChargedKnockback => m_ChargedKnockback;
        public float SlamWindup => m_SlamWindup;
        public float ChargeMoveSpeed => m_ChargeMoveSpeed;
    }
}
