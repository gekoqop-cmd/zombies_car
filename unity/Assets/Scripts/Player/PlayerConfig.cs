using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>Movement tuning of the player character.</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "ZombiCar/Player Config")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float m_MoveSpeed = 6.25f;
        [Tooltip("Degrees per second the body turns towards the aim direction.")]
        [SerializeField, Min(0f)] private float m_TurnSpeed = 900f;
        [SerializeField] private float m_Gravity = -25f;
        [Tooltip("Small downward speed kept while grounded so the controller stays glued to the ground.")]
        [SerializeField, Min(0f)] private float m_GroundStickSpeed = 2f;

        [Header("Run animation")]
        [Tooltip("Movement speed (m/s) at which the Run clip plays at its own rate. The painted stride (~0.68 m a step, " +
                 "2 steps in 0.83 s) matches 1.6 m/s; a higher value keeps the legs readable at running speed.")]
        [SerializeField, Min(0.1f)] private float m_RunAnimationReferenceSpeed = 4f;
        [Tooltip("Playback-rate limits of the Run clip.")]
        [SerializeField] private Vector2 m_RunAnimationRateRange = new Vector2(0.6f, 2f);

        public float MoveSpeed => m_MoveSpeed;
        public float TurnSpeed => m_TurnSpeed;
        public float Gravity => m_Gravity;
        public float GroundStickSpeed => m_GroundStickSpeed;

        /// <summary>Playback rate of the Run clip for a movement speed (legs speed up and slow down with the body).</summary>
        public float RunAnimationSpeed(float speed) =>
            Mathf.Clamp(speed / m_RunAnimationReferenceSpeed, m_RunAnimationRateRange.x, m_RunAnimationRateRange.y);
    }
}
