using Unity.Cinemachine;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// The point the follow camera tracks: the player plus a lead made of look-ahead along the movement and a shift
    /// towards the aim (<see cref="CameraConfig"/>). The lead is smoothed with a critically damped spring, so it
    /// eases out when the player stops or stops aiming instead of snapping or swaying. Runs after the player moved
    /// (Update, late execution order) and before the CinemachineBrain (LateUpdate).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraFollowTarget : MonoBehaviour
    {
        [SerializeField] private CameraConfig m_Config;
        [SerializeField] private Transform m_Player;
        [SerializeField] private CharacterController m_Controller;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("Gets the config's damping at start.")]
        [SerializeField] private CinemachineFollow m_Follow;

        private Vector3 m_Lead;
        private Vector3 m_LeadVelocity;

        private void Start()
        {
            if (m_Follow != null && m_Config != null)
                m_Follow.TrackerSettings.PositionDamping = m_Config.WorldDamping;
            Snap();
        }

        /// <summary>Jumps to the player with no lead (spawn, respawn).</summary>
        public void Snap()
        {
            m_Lead = m_LeadVelocity = Vector3.zero;
            if (m_Player != null)
                transform.position = m_Player.position;
        }

        private void Update()
        {
            if (m_Player == null || m_Config == null)
                return;
            m_Lead = Vector3.SmoothDamp(m_Lead, TargetLead(), ref m_LeadVelocity, m_Config.LeadSmoothTime);
            transform.position = m_Player.position + m_Lead;
        }

        private Vector3 TargetLead()
        {
            Vector3 lead = Vector3.zero;
            if (m_Controller != null)
            {
                Vector3 velocity = m_Controller.velocity;
                velocity.y = 0f;
                lead += Vector3.ClampMagnitude(velocity * m_Config.LookAheadTime, m_Config.LookAheadMax);
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
            return lead;
        }
    }
}
