using UnityEngine;
using ZombiCar.Characters;

namespace ZombiCar.Player
{
    /// <summary>
    /// 2D look of the player: picks the sprite clip from movement and shooting (idle, run, shoot, run + shoot, hurt),
    /// faces the aim (or the movement when not aiming) and flashes the muzzle on every shot.
    /// </summary>
    public sealed class PlayerSpriteView : MonoBehaviour
    {
        private static readonly Color s_HurtFlash = new Color(1f, 0.35f, 0.3f, 1f);

        [SerializeField] private SpriteAnimator m_Animator;
        [SerializeField] private SpriteRenderer m_MuzzleFlash;
        [SerializeField] private CharacterController m_Controller;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("Speed that counts as running, metres per second.")]
        [SerializeField, Min(0f)] private float m_RunThreshold = 0.3f;
        [Tooltip("Running speed at which the run clip plays at its own rate.")]
        [SerializeField, Min(0.1f)] private float m_ReferenceSpeed = 5f;
        [Tooltip("How long the shooting pose is kept after a shot, seconds.")]
        [SerializeField, Min(0f)] private float m_ShootPoseTime = 0.25f;
        [SerializeField, Min(0f)] private float m_MuzzleFlashTime = 0.05f;
        [SerializeField, Min(0f)] private float m_HurtTime = 0.2f;
        [Tooltip("While idle and aiming, the aim must point this many times more forward/back than sideways to show the turn-away/towards-camera pose instead of the side view.")]
        [SerializeField, Min(1f)] private float m_VerticalFacingBias = 1.2f;

        private IPlayerInput m_Input;
        private float m_ShootUntil;
        private float m_FlashUntil;
        private float m_HurtUntil;
        private Vector3 m_FlashScale;

        private void Awake()
        {
            if (m_MuzzleFlash != null)
            {
                m_FlashScale = m_MuzzleFlash.transform.localScale;
                m_MuzzleFlash.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (m_Weapon != null)
                m_Weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (m_Weapon != null)
                m_Weapon.Fired -= OnFired;
        }

        private void Start() => m_Input = m_InputProvider != null ? m_InputProvider.Input : null;

        /// <summary>Hurt pose + red flash (called by <see cref="PlayerHealth"/>).</summary>
        public void ShowHurt()
        {
            m_HurtUntil = Time.time + m_HurtTime;
            m_Animator.Flash(s_HurtFlash, m_HurtTime);
        }

        private void OnFired()
        {
            m_ShootUntil = Time.time + m_ShootPoseTime;
            m_FlashUntil = Time.time + m_MuzzleFlashTime;
            if (m_MuzzleFlash != null)
            {
                // A different flash every shot: random length and mirrored up/down.
                Vector3 scale = m_FlashScale;
                scale.x *= Random.Range(0.8f, 1.15f);
                scale.y *= Random.value < 0.5f ? -1f : 1f;
                m_MuzzleFlash.transform.localScale = scale;
            }
        }

        private void LateUpdate()
        {
            Vector3 velocity = m_Controller != null ? m_Controller.velocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool running = speed > m_RunThreshold;
            bool shooting = Time.time < m_ShootUntil;
            bool aiming = m_Input != null && m_Input.AimActive;
            bool facingVertically = !running && !shooting && aiming &&
                Mathf.Abs(m_Input.AimDirection.z) > Mathf.Abs(m_Input.AimDirection.x) * m_VerticalFacingBias;

            if (Time.time < m_HurtUntil)
                m_Animator.Play(CharacterAnim.Hit);
            else if (m_Weapon != null && m_Weapon.IsReloading)
                m_Animator.Play(CharacterAnim.Reload);
            else if (running)
                m_Animator.Play(CharacterAnim.Move, speed / m_ReferenceSpeed);
            else if (shooting)
                m_Animator.Play(CharacterAnim.Shoot);
            else if (facingVertically)
                m_Animator.Play(m_Input.AimDirection.z > 0f ? CharacterAnim.TurnUp : CharacterAnim.TurnDown);
            else
                m_Animator.Play(CharacterAnim.Idle);

            m_Animator.SetFacing(aiming ? m_Input.AimDirection.x : velocity.x);

            if (m_MuzzleFlash != null)
                m_MuzzleFlash.enabled = Time.time < m_FlashUntil;
        }
    }
}
