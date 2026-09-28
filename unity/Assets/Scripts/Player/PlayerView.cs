using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// 2D look of the player (frames cut from refsanim/Player/*.mp4): drives the Animator (Idle / Run by speed,
    /// Shot while standing, Reload stretched to <see cref="PlayerWeapon.ReloadTime"/>), mirrors the sprite left/right
    /// by the aim (never rotates it in its plane), shows a short muzzle flash (a few frames of a compact star,
    /// a soft glow and a puff of smoke) with a recoil kick on every shot (also while running, when the Run clip
    /// keeps playing), and plays the shot / reload / footstep sounds. The flash sits under the Muzzle, so it flips
    /// with the body; the smoke stays where the shot was fired.
    /// Reads input only through <see cref="IPlayerInput"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // facing is set before PlayerWeapon fires from the (mirrored) muzzle
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
        [SerializeField] private SpriteRenderer m_MuzzleGlow;
        [Tooltip("Smoke puffs used in turn (world-placed at the muzzle when fired).")]
        [SerializeField] private SpriteRenderer[] m_Smoke = System.Array.Empty<SpriteRenderer>();
        [SerializeField] private CharacterController m_Controller;
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("The axe: the body lunges forward on every swing.")]
        [SerializeField] private PlayerMelee m_Melee;
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
        [Tooltip("Recoil kick of the body against the facing on every shot, metres.")]
        [SerializeField, Min(0f)] private float m_RecoilDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float m_RecoilTime = 0.08f;
        [SerializeField, Min(0f)] private float m_HurtTime = 0.2f;
        [Tooltip("Lunge of the body into an axe swing, metres.")]
        [SerializeField, Min(0f)] private float m_LungeDistance = 0.14f;
        [SerializeField, Min(0.01f)] private float m_LungeTime = 0.3f;

        [Header("Muzzle flash")]
        [Tooltip("Frames of the flash, brightest first; each shows for Flash Frame Time.")]
        [SerializeField] private Sprite[] m_FlashFrames = System.Array.Empty<Sprite>();
        [SerializeField, Min(0.005f)] private float m_FlashFrameTime = 0.025f;
        [Tooltip("Random turn of the flash in its plane each shot, degrees.")]
        [SerializeField, Range(0f, 45f)] private float m_FlashTwist = 12f;
        [SerializeField] private Vector2 m_FlashScaleRange = new Vector2(0.9f, 1.1f);
        [SerializeField, Range(0f, 1f)] private float m_GlowAlpha = 0.7f;
        [SerializeField, Min(0.01f)] private float m_SmokeTime = 0.35f;
        [SerializeField, Range(0f, 1f)] private float m_SmokeAlpha = 0.35f;
        [Tooltip("How far a smoke puff rises while it fades, metres.")]
        [SerializeField, Min(0f)] private float m_SmokeRise = 0.25f;
        [Tooltip("Smoke puff scale at the start and the end of its life (times its built scale).")]
        [SerializeField] private Vector2 m_SmokeScale = new Vector2(0.5f, 1.1f);

        [Header("Sounds")]
        [SerializeField] private AudioClip m_ShotSound;
        [SerializeField, Range(0f, 1f)] private float m_ShotVolume = 0.7f;
        [SerializeField] private AudioClip m_ReloadSound;
        [SerializeField, Range(0f, 1f)] private float m_ReloadVolume = 0.8f;
        [SerializeField] private AudioClip[] m_StepSounds = System.Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float m_StepVolume = 0.35f;

        private IPlayerInput m_Input;
        private int m_Facing = 1;
        private float m_FlashStart = float.NegativeInfinity;
        private int m_NextSmoke;
        private float[] m_SmokeStart = System.Array.Empty<float>();
        private Vector3 m_FlashBaseScale;
        private Vector3 m_SmokeBaseScale;
        private float m_RecoilStart = float.NegativeInfinity;
        private float m_HurtUntil;
        private float m_Speed;
        private float m_DownSince = float.NegativeInfinity;
        private float m_LungeStart = float.NegativeInfinity;
        private bool m_Down;

        private void Awake()
        {
            if (m_MuzzleFlash != null)
            {
                m_FlashBaseScale = m_MuzzleFlash.transform.localScale;
                m_MuzzleFlash.enabled = false;
            }
            if (m_MuzzleGlow != null)
                m_MuzzleGlow.enabled = false;
            m_SmokeStart = new float[m_Smoke.Length];
            for (int i = 0; i < m_Smoke.Length; i++)
            {
                m_SmokeStart[i] = float.NegativeInfinity;
                m_Smoke[i].enabled = false;
                m_SmokeBaseScale = m_Smoke[i].transform.localScale;
            }
        }

        private void OnEnable()
        {
            if (m_Weapon == null)
                return;
            m_Weapon.Fired += OnFired;
            m_Weapon.ReloadStarted += OnReloadStarted;
            if (m_Melee != null)
                m_Melee.Swung += OnSwung;
        }

        private void OnDisable()
        {
            if (m_Weapon == null)
                return;
            m_Weapon.Fired -= OnFired;
            m_Weapon.ReloadStarted -= OnReloadStarted;
            if (m_Melee != null)
                m_Melee.Swung -= OnSwung;
        }

        private void OnSwung() => m_LungeStart = Time.time;

        private void Start() => m_Input = m_InputProvider != null ? m_InputProvider.Input : null;

        /// <summary>Red flash on a hit (called by <see cref="PlayerHealth"/>).</summary>
        public void ShowHurt() => m_HurtUntil = Time.time + m_HurtTime;

        /// <summary>The player is down: the body tips over and darkens, the animation stops.</summary>
        public void ShowDown()
        {
            if (m_Down)
                return;
            m_Down = true;
            m_DownSince = Time.time;
            m_Animator.speed = 0f;
            if (m_MuzzleFlash != null)
                m_MuzzleFlash.enabled = false;
            if (m_MuzzleGlow != null)
                m_MuzzleGlow.enabled = false;
        }

        /// <summary>Animation event of the Run clip (foot strike), forwarded by <see cref="PlayerAnimationEvents"/>.</summary>
        public void Footstep()
        {
            if (m_StepSounds.Length == 0 || AudioManager.Instance == null || m_Speed < m_RunThreshold)
                return;
            AudioManager.Instance.Play(m_StepSounds[Random.Range(0, m_StepSounds.Length)], m_StepVolume, 0.08f);
        }

        private void OnFired()
        {
            m_FlashStart = Time.time;
            m_RecoilStart = Time.time;
            if (m_MuzzleFlash != null)
            {
                // A different flash every shot: size, a small twist, mirrored up/down.
                Transform t = m_MuzzleFlash.transform;
                Vector3 scale = m_FlashBaseScale * Random.Range(m_FlashScaleRange.x, m_FlashScaleRange.y);
                scale.y *= Random.value < 0.5f ? -1f : 1f;
                t.localScale = scale;
                t.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-m_FlashTwist, m_FlashTwist));
            }
            if (m_Smoke.Length > 0 && m_MuzzleFlash != null)
            {
                int i = m_NextSmoke;
                m_NextSmoke = (m_NextSmoke + 1) % m_Smoke.Length;
                m_SmokeStart[i] = Time.time;
                m_Smoke[i].transform.position = m_MuzzleFlash.transform.parent.position;   // the muzzle
            }
            if (m_Speed < m_RunThreshold)
                m_Animator.SetTrigger(s_Shoot);                          // the whole Shot clip only while standing
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(m_ShotSound, m_ShotVolume, 0.05f);
        }

        private void OnReloadStarted()
        {
            if (AudioManager.Instance != null && m_Weapon != null)
                AudioManager.Instance.PlayFitted(m_ReloadSound, m_Weapon.ReloadTime, m_ReloadVolume);
        }

        // Facing before the weapon's Update: a shot fired on the frame the player turns leaves from the new side.
        private void Update()
        {
            UpdateFacing();
            m_Body.transform.localScale = new Vector3(m_Scale.x * m_Facing, m_Scale.y, 1f);
        }

        private void UpdateFacing()
        {
            // Aim when aiming (mouse / fire stick), otherwise the movement; the camera looks along +Z, so world x is
            // screen x.
            Vector3 velocity = m_Controller != null ? m_Controller.velocity : Vector3.zero;
            float side = m_Input != null && m_Input.AimActive ? m_Input.AimDirection.x : velocity.x;
            if (side > 0.05f)
                m_Facing = 1;
            else if (side < -0.05f)
                m_Facing = -1;
        }

        private void LateUpdate()
        {
            if (m_Down)
            {
                // Falls over sideways (away from where it faced) in a quarter of a second and stays there, darkened.
                float t = Mathf.Clamp01((Time.time - m_DownSince) / 0.3f);
                float fall = 1f - (1f - t) * (1f - t);
                Transform b = m_Body.transform;
                b.localRotation = Quaternion.Euler(0f, 0f, 82f * m_Facing * fall);
                b.localPosition = new Vector3(-m_Facing * 0.35f * fall, 0f, 0f);
                m_Body.color = Color.Lerp(m_Tint, new Color(0.72f, 0.62f, 0.6f, 1f), fall) * SceneTint.Characters;
                return;
            }
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
                m_Animator.SetBool(s_Reloading, m_Weapon.IsReloading);
                m_Animator.SetFloat(s_ReloadSpeed, m_ReloadClipLength / m_Weapon.ReloadTime);
                // Automatic fire is faster than the clip: it is sped up so every shot still shows all of it.
                m_Animator.SetFloat(s_ShotSpeed, Mathf.Max(1f, m_ShotClipLength / m_Weapon.ShotInterval));
            }

            UpdateFacing();
            float recoil = 1f - Mathf.Clamp01((Time.time - m_RecoilStart) / m_RecoilTime);
            Transform body = m_Body.transform;
            body.localScale = new Vector3(m_Scale.x * m_Facing, m_Scale.y, 1f);
            float lunge = Mathf.Sin(Mathf.Clamp01((Time.time - m_LungeStart) / m_LungeTime) * Mathf.PI);
            body.localPosition = new Vector3(m_Facing * (m_LungeDistance * lunge - m_RecoilDistance * recoil * recoil), 0f, 0f);
            m_Body.color = (Time.time < m_HurtUntil ? s_HurtColor * m_Tint : m_Tint) * SceneTint.Characters;

            UpdateMuzzleEffects();
        }

        private void UpdateMuzzleEffects()
        {
            float age = Time.time - m_FlashStart;
            int frame = m_FlashFrames.Length > 0 ? Mathf.FloorToInt(age / m_FlashFrameTime) : -1;
            bool flashing = frame >= 0 && frame < m_FlashFrames.Length;
            if (m_MuzzleFlash != null)
            {
                m_MuzzleFlash.enabled = flashing;
                if (flashing)
                    m_MuzzleFlash.sprite = m_FlashFrames[frame];
            }
            if (m_MuzzleGlow != null)
            {
                float life = m_FlashFrames.Length * m_FlashFrameTime * 1.5f;
                float k = 1f - Mathf.Clamp01(age / life);
                m_MuzzleGlow.enabled = k > 0f;
                Color c = m_MuzzleGlow.color;
                c.a = m_GlowAlpha * k;
                m_MuzzleGlow.color = c;
            }
            for (int i = 0; i < m_Smoke.Length; i++)
            {
                float p = (Time.time - m_SmokeStart[i]) / m_SmokeTime;
                SpriteRenderer smoke = m_Smoke[i];
                smoke.enabled = p >= 0f && p < 1f;
                if (!smoke.enabled)
                    continue;
                smoke.transform.position += Vector3.up * (m_SmokeRise / m_SmokeTime * Time.deltaTime);
                smoke.transform.localScale = m_SmokeBaseScale * Mathf.Lerp(m_SmokeScale.x, m_SmokeScale.y, p);
                Color c = smoke.color;
                c.a = m_SmokeAlpha * (1f - p) * (1f - p);
                smoke.color = c;
            }
        }
    }
}
