using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// 2D look of the player (frames cut from refsanim/Player/*.mp4): drives the Animator (Idle / Run by speed,
    /// Shot while standing, Reload stretched to <see cref="WeaponConfig.ReloadTime"/>), mirrors the sprite left/right
    /// by the aim (never rotates it in its plane), shows the muzzle flash with a short recoil kick on every shot
    /// (also while running, when the Run clip keeps playing), and plays the shot / reload / footstep sounds.
    /// Reads input only through <see cref="IPlayerInput"/>.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private static readonly int s_Speed = Animator.StringToHash("Speed");
        private static readonly int s_RunSpeed = Animator.StringToHash("RunSpeed");
        private static readonly int s_Shoot = Animator.StringToHash("Shoot");
        private static readonly int s_ShotSpeed = Animator.StringToHash("ShotSpeed");
        private static readonly int s_Reloading = Animator.StringToHash("Reloading");
        private static readonly int s_ReloadSpeed = Animator.StringToHash("ReloadSpeed");
        private static readonly Color s_HurtColor = new Color(1f, 0.35f, 0.3f, 1f);

        [Header("References")]
        [SerializeField] private Animator m_Animator;
        [SerializeField] private SpriteRenderer m_Body;
        [SerializeField] private SpriteRenderer m_MuzzleFlash;
        [SerializeField] private CharacterController m_Controller;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private PlayerConfig m_Config;

        [Header("Clips (lengths in seconds at 1x)")]
        [SerializeField, Min(0.01f)] private float m_ShotClipLength = 0.25f;
        [SerializeField, Min(0.01f)] private float m_ReloadClipLength = 1.4f;

        [Header("Look")]
        [Tooltip("Local scale of the body sprite (x, y); y includes the 1 / cos(camera pitch) compensation.")]
        [SerializeField] private Vector2 m_Scale = Vector2.one;
        [Tooltip("Multiplies the painted colours so the character sits in the scene's sunset light.")]
        [SerializeField] private Color m_Tint = Color.white;
        [Tooltip("Horizontal speed (m/s) above which the character counts as running.")]
        [SerializeField, Min(0f)] private float m_RunThreshold = 0.3f;
        [SerializeField, Min(0f)] private float m_MuzzleFlashTime = 0.06f;
        [Tooltip("Recoil kick of the body against the facing on every shot, metres.")]
        [SerializeField, Min(0f)] private float m_RecoilDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float m_RecoilTime = 0.08f;
        [SerializeField, Min(0f)] private float m_HurtTime = 0.2f;

        [Header("Sounds")]
        [SerializeField] private AudioClip m_ShotSound;
        [SerializeField, Range(0f, 1f)] private float m_ShotVolume = 0.7f;
        [SerializeField] private AudioClip m_ReloadSound;
        [SerializeField, Range(0f, 1f)] private float m_ReloadVolume = 0.8f;
        [SerializeField] private AudioClip[] m_StepSounds = System.Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float m_StepVolume = 0.35f;

        private IPlayerInput m_Input;
        private int m_Facing = 1;
        private float m_FlashUntil;
        private float m_RecoilStart = float.NegativeInfinity;
        private float m_HurtUntil;
        private Vector3 m_FlashScale;
        private float m_Speed;

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
            if (m_Weapon == null)
                return;
            m_Weapon.Fired += OnFired;
            m_Weapon.ReloadStarted += OnReloadStarted;
        }

        private void OnDisable()
        {
            if (m_Weapon == null)
                return;
            m_Weapon.Fired -= OnFired;
            m_Weapon.ReloadStarted -= OnReloadStarted;
        }

        private void Start() => m_Input = m_InputProvider != null ? m_InputProvider.Input : null;

        /// <summary>Red flash on a hit (called by <see cref="PlayerHealth"/>).</summary>
        public void ShowHurt() => m_HurtUntil = Time.time + m_HurtTime;

        /// <summary>Animation event of the Run clip (foot strike), forwarded by <see cref="PlayerAnimationEvents"/>.</summary>
        public void Footstep()
        {
            if (m_StepSounds.Length == 0 || AudioManager.Instance == null || m_Speed < m_RunThreshold)
                return;
            AudioManager.Instance.Play(m_StepSounds[Random.Range(0, m_StepSounds.Length)], m_StepVolume, 0.08f);
        }

        private void OnFired()
        {
            m_FlashUntil = Time.time + m_MuzzleFlashTime;
            m_RecoilStart = Time.time;
            if (m_MuzzleFlash != null)
            {
                // A different flash every shot: random length, mirrored up/down.
                Vector3 scale = m_FlashScale;
                scale.x *= Random.Range(0.85f, 1.15f);
                scale.y *= Random.value < 0.5f ? -1f : 1f;
                m_MuzzleFlash.transform.localScale = scale;
            }
            if (m_Speed < m_RunThreshold)
                m_Animator.SetTrigger(s_Shoot);                          // the whole Shot clip only while standing
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(m_ShotSound, m_ShotVolume, 0.05f);
        }

        private void OnReloadStarted()
        {
            if (AudioManager.Instance != null && m_Weapon != null)
                AudioManager.Instance.PlayFitted(m_ReloadSound, m_Weapon.Config.ReloadTime, m_ReloadVolume);
        }

        private void LateUpdate()
        {
            Vector3 velocity = m_Controller != null ? m_Controller.velocity : Vector3.zero;
            velocity.y = 0f;
            m_Speed = velocity.magnitude;
            bool running = m_Speed > m_RunThreshold;

            // Animator.
            m_Animator.SetFloat(s_Speed, m_Speed);
            if (m_Config != null)
                m_Animator.SetFloat(s_RunSpeed, m_Config.RunAnimationSpeed(m_Speed));
            if (running)
                m_Animator.ResetTrigger(s_Shoot);
            if (m_Weapon != null && m_Weapon.Config != null)
            {
                WeaponConfig weapon = m_Weapon.Config;
                m_Animator.SetBool(s_Reloading, m_Weapon.IsReloading);
                m_Animator.SetFloat(s_ReloadSpeed, m_ReloadClipLength / weapon.ReloadTime);
                // Automatic fire is faster than the clip: it is sped up so every shot still shows all of it.
                m_Animator.SetFloat(s_ShotSpeed, Mathf.Max(1f, m_ShotClipLength / weapon.ShotInterval));
            }

            // Facing: aim when aiming (mouse / fire stick), otherwise the movement; the camera looks along +Z, so
            // world x is screen x.
            float side = m_Input != null && m_Input.AimActive ? m_Input.AimDirection.x : velocity.x;
            if (side > 0.05f)
                m_Facing = 1;
            else if (side < -0.05f)
                m_Facing = -1;

            float recoil = 1f - Mathf.Clamp01((Time.time - m_RecoilStart) / m_RecoilTime);
            Transform body = m_Body.transform;
            body.localScale = new Vector3(m_Scale.x * m_Facing, m_Scale.y, 1f);
            body.localPosition = new Vector3(-m_Facing * m_RecoilDistance * recoil * recoil, 0f, 0f);
            m_Body.color = Time.time < m_HurtUntil ? s_HurtColor * m_Tint : m_Tint;

            if (m_MuzzleFlash != null)
                m_MuzzleFlash.enabled = Time.time < m_FlashUntil;
        }
    }
}
