using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.Player
{
    /// <summary>
    /// Moves the player with a CharacterController relative to the camera heading and turns the body towards the
    /// aim direction (or the movement direction when not aiming). Reads input only through <see cref="IPlayerInput"/>.
    /// Stands still during a timed interaction (<see cref="PlayerInteractor"/>) and after death; cards of the run can
    /// make the player faster.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float c_MinLookSqr = 1e-4f;
        private const float c_PushDecay = 9f;              // knockback velocity lost per second (exponential)

        [SerializeField] private PlayerConfig m_Config;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerInteractor m_Interactor;
        [SerializeField] private PlayerHealth m_Health;
        [SerializeField] private RunState m_Run;
        [Tooltip("The axe: slower while it is held up for the charged blow.")]
        [SerializeField, OptionalReference] private PlayerMelee m_Melee;

        private CharacterController m_Controller;
        private IPlayerInput m_Input;
        private Transform m_Camera;
        private float m_VerticalSpeed;
        private Vector3 m_Push;

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
            bool rooted = (m_Interactor != null && m_Interactor.IsBusy) || (m_Health != null && !m_Health.IsAlive);
            Vector3 planar = rooted ? Vector3.zero : PlanarVelocity(m_Input.Move);

            if (m_Controller.isGrounded && m_VerticalSpeed < 0f)
                m_VerticalSpeed = -m_Config.GroundStickSpeed;
            else
                m_VerticalSpeed += m_Config.Gravity * dt;

            Vector3 velocity = planar + m_Push;
            m_Push *= Mathf.Exp(-c_PushDecay * dt);
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

        /// <summary>Knocks the player back: <paramref name="velocity"/> (m/s, level) fades out over a few tenths of a second.</summary>
        public void Push(Vector3 velocity)
        {
            velocity.y = 0f;
            m_Push += velocity;
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
            float speed = m_Config.MoveSpeed * (m_Run != null ? m_Run.Modifiers.Multiplier(StatEffect.MoveSpeed) : 1f)
                * (m_Melee != null ? m_Melee.MoveMultiplier : 1f);
            return (right * move.x + forward * move.y) * speed;
        }
    }
}
