using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>Movement tuning of the player character.</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "ZombiCar/Player Config")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float m_MoveSpeed = 5f;
        [Tooltip("Degrees per second the body turns towards the aim direction.")]
        [SerializeField, Min(0f)] private float m_TurnSpeed = 900f;
        [SerializeField] private float m_Gravity = -25f;
        [Tooltip("Small downward speed kept while grounded so the controller stays glued to the ground.")]
        [SerializeField, Min(0f)] private float m_GroundStickSpeed = 2f;

        public float MoveSpeed => m_MoveSpeed;
        public float TurnSpeed => m_TurnSpeed;
        public float Gravity => m_Gravity;
        public float GroundStickSpeed => m_GroundStickSpeed;
    }
}
