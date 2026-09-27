using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Follow-camera tuning: Cinemachine position damping plus how far the followed point leads the player
    /// (look-ahead along the movement, offset towards the aim). Read by <c>CameraFollowTarget</c> and applied to
    /// the CinemachineFollow of the map at start, so edits here take effect without rebuilding the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "ZombiCar/Camera Config")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Tooltip("Cinemachine position damping (seconds): x = screen horizontal (world X), y = screen vertical (world Z).")]
        [SerializeField] private Vector2 m_Damping = new Vector2(0.45f, 0.45f);

        [Header("Look-ahead")]
        [Tooltip("The followed point leads the player by velocity × this time (seconds).")]
        [SerializeField, Min(0f)] private float m_LookAheadTime = 0.35f;
        [Tooltip("Maximum look-ahead distance, metres.")]
        [SerializeField, Min(0f)] private float m_LookAheadMax = 2.2f;

        [Header("Aim offset")]
        [Tooltip("Share of the distance to the aim point the camera shifts towards it (0 = off).")]
        [SerializeField, Range(0f, 0.5f)] private float m_AimOffset = 0.2f;
        [Tooltip("Maximum shift towards the aim, metres (also the shift for stick aiming, which has no aim point).")]
        [SerializeField, Min(0f)] private float m_AimOffsetMax = 2.5f;

        [Header("Smoothing")]
        [Tooltip("Seconds the lead (look-ahead + aim) takes to settle; critically damped, so it returns without overshoot.")]
        [SerializeField, Min(0.01f)] private float m_LeadSmoothTime = 0.45f;

        public Vector2 Damping => m_Damping;
        public float LookAheadTime => m_LookAheadTime;
        public float LookAheadMax => m_LookAheadMax;
        public float AimOffset => m_AimOffset;
        public float AimOffsetMax => m_AimOffsetMax;
        public float LeadSmoothTime => m_LeadSmoothTime;

        /// <summary>CinemachineFollow damping in world axes (X, Y, Z); the height follows like the screen vertical.</summary>
        public Vector3 WorldDamping => new Vector3(m_Damping.x, m_Damping.y, m_Damping.y);
    }
}
