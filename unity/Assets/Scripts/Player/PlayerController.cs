using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Moves the player with a CharacterController relative to the camera heading and turns the body towards the
    /// aim direction (or the movement direction when not aiming). Reads input only through <see cref="IPlayerInput"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float c_MinLookSqr = 1e-4f;

        [SerializeField] private PlayerConfig m_Config;
        [SerializeField] private PlayerInputProvider m_InputProvider;

        private CharacterController m_Controller;
        private IPlayerInput m_Input;
        private Transform m_Camera;
        private float m_VerticalSpeed;

        private void Awake() => m_Controller = GetComponent<CharacterController>();

        private void Start()
        {
            m_Input = m_InputProvider != null ? m_InputProvider.Input : null;
            if (m_Input == null || m_Config == null)
            {
                Debug.LogError("[PlayerController] Input provider or config is missing.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 planar = PlanarVelocity(m_Input.Move);

            if (m_Controller.isGrounded && m_VerticalSpeed < 0f)
                m_VerticalSpeed = -m_Config.GroundStickSpeed;
            else
                m_VerticalSpeed += m_Config.Gravity * dt;

            Vector3 velocity = planar;
            velocity.y = m_VerticalSpeed;
            m_Controller.Move(velocity * dt);

            Vector3 look = m_Input.AimActive ? m_Input.AimDirection : planar;
            look.y = 0f;
            if (look.sqrMagnitude > c_MinLookSqr)
            {
                Quaternion target = Quaternion.LookRotation(look, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, m_Config.TurnSpeed * dt);
            }
        }

        private Vector3 PlanarVelocity(Vector2 move)
        {
            if (m_Camera == null)
            {
                Camera cam = Camera.main;
                m_Camera = cam != null ? cam.transform : null;
            }
            Vector3 forward = m_Camera != null ? m_Camera.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > c_MinLookSqr ? forward.normalized : Vector3.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);
            return (right * move.x + forward * move.y) * m_Config.MoveSpeed;
        }
    }
}
