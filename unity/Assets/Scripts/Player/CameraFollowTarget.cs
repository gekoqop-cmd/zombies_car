using Unity.Cinemachine;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// The point the follow camera tracks: the player plus a lead made of look-ahead along the movement and a shift
    /// towards the aim (<see cref="CameraConfig"/>), capped in total. The movement is measured from the player's
    /// own displacement (smoothed), not CharacterController.velocity, which jumps on collisions and ground snapping.
    /// The lead eases with a critically damped spring, so it settles without overshoot when the player stops,
    /// turns or stops aiming. Runs after the player moved (Update, late execution order) and before the
    /// CinemachineBrain (LateUpdate).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraFollowTarget : MonoBehaviour
    {
        private const float c_VelocitySmoothing = 0.08f;   // seconds; filters frame-time noise of the measured speed

        [SerializeField] private CameraConfig m_Config;
        [SerializeField] private Transform m_Player;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("Gets the config's damping at start.")]
        [SerializeField] private CinemachineFollow m_Follow;

        private Vector3 m_Lead;
        private Vector3 m_LeadVelocity;
        private Vector3 m_LastPlayerPosition;
        private Vector3 m_Velocity;

        /// <summary>Current lead of the camera point over the player (diagnostics).</summary>
        public Vector3 Lead => m_Lead;

        private void Start()
        {
            ApplyDamping();
            Snap();
        }

        /// <summary>Copies the config's damping to the Cinemachine follow (also after editing the config at run time).</summary>
        public void ApplyDamping()
        {
            if (m_Follow != null && m_Config != null)
                m_Follow.TrackerSettings.PositionDamping = m_Config.WorldDamping;
        }

        /// <summary>Jumps to the player with no lead (spawn, respawn, teleport).</summary>
        public void Snap()
        {
            m_Lead = m_LeadVelocity = m_Velocity = Vector3.zero;
            if (m_Player == null)
                return;
            m_LastPlayerPosition = m_Player.position;
            transform.position = m_Player.position;
        }

        private void Update()
        {
            if (m_Player == null || m_Config == null)
                return;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 moved = m_Player.position - m_LastPlayerPosition;
                moved.y = 0f;
                m_Velocity = Vector3.Lerp(m_Velocity, moved / dt, 1f - Mathf.Exp(-dt / c_VelocitySmoothing));
            }
            m_LastPlayerPosition = m_Player.position;

            m_Lead = Vector3.SmoothDamp(m_Lead, TargetLead(), ref m_LeadVelocity, m_Config.LeadSmoothTime);
            transform.position = m_Player.position + m_Lead;
        }

        private Vector3 TargetLead()
        {
            Vector3 lead = Vector3.zero;
            float speed = m_Velocity.magnitude;
            if (speed > m_Config.LookAheadDeadZone)
            {
                // Grows from zero at the dead zone, so crossing it does not kick the camera.
                float effective = speed - m_Config.LookAheadDeadZone;
                lead += m_Velocity / speed * Mathf.Min(effective * m_Config.LookAheadTime, m_Config.LookAheadMax);
            }

            IPlayerInput input = m_InputProvider != null ? m_InputProvider.Input : null;
            if (input != null && input.AimActive && m_Config.AimOffset > 0f)
            {
                float distance = m_Config.AimOffsetMax;
                if (m_Weapon != null && m_Weapon.Aim.IsValid)
                {
                    Vector3 toAim = m_Weapon.Aim.Point - m_Player.position;
                    toAim.y = 0f;
                    distance = Mathf.Min(toAim.magnitude * m_Config.AimOffset, m_Config.AimOffsetMax);
                }
                lead += input.AimDirection * distance;
            }
            return Vector3.ClampMagnitude(lead, m_Config.MaxLead);
        }
    }
}
