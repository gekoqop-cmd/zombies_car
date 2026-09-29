using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Characters
{
    /// <summary>
    /// Plays <see cref="CharacterSprites"/> clips on an upright SpriteRenderer and adds the motion the few painted
    /// frames lack: a bounce and sway on every step, a colour flash on hits, a vertical offset for rising from or
    /// sinking into the ground, and left/right facing (mirrored scale, so child sprites such as the muzzle flash
    /// follow). With procedural motion on, squash &amp; stretch and lean on top: breathing, a squash on every foot fall and
    /// a forward lean while walking, a wind-up and lunge on attacks, a recoil on hits, a thud on death and a wobble
    /// while rising. The Y scale includes 1 / cos(camera pitch) (set at build time) so the top-down camera does not
    /// squash the upright sprite.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        private const float c_FacingDeadZone = 0.05f;

        [SerializeField] private CharacterSprites m_Sprites;
        [Tooltip("Local scale of the sprite (x, y); y includes the camera-pitch compensation.")]
        [SerializeField] private Vector2 m_Scale = Vector2.one;
        [Tooltip("Multiplies the painted colours so the characters sit in the scene's sunset light.")]
        [SerializeField] private Color m_Tint = Color.white;
        [Tooltip("Bounce on every step while moving, metres.")]
        [SerializeField, Min(0f)] private float m_StepBounce = 0.05f;
        [Tooltip("Side-to-side lean while moving, degrees.")]
        [SerializeField, Min(0f)] private float m_StepSway = 4f;

        [Header("Procedural motion (squash & stretch over the painted frames)")]
        [Tooltip("Breathing, step squash, forward lean, attack wind-up and lunge, hit recoil, death thud, rising wobble.")]
        [SerializeField] private bool m_Procedural = true;
        [Tooltip("Breathing while standing: stretch of the height (0.03 = 3 %).")]
        [SerializeField, Min(0f)] private float m_Breath = 0.03f;
        [SerializeField, Min(0.1f)] private float m_BreathRate = 0.55f;
        [Tooltip("Squash when a foot lands / stretch between steps while walking.")]
        [SerializeField, Min(0f)] private float m_StepSquash = 0.06f;
        [Tooltip("Forward lean while walking, degrees.")]
        [SerializeField, Min(0f)] private float m_WalkLean = 5f;
        [Tooltip("Attack: lean back in the wind-up, forward in the strike, degrees.")]
        [SerializeField, Min(0f)] private float m_AttackLean = 12f;
        [Tooltip("Attack: how far the body lunges forward at the strike, metres.")]
        [SerializeField, Min(0f)] private float m_AttackLunge = 0.18f;
        [Tooltip("Hit: recoil lean, degrees, and its time.")]
        [SerializeField, Min(0f)] private float m_HitLean = 14f;
        [SerializeField, Min(0.01f)] private float m_HitTime = 0.28f;
        [Tooltip("Death: the squash when the body hits the ground.")]
        [SerializeField, Min(0f)] private float m_DeathThud = 0.14f;
        [Tooltip("Rising from the ground: side-to-side wobble, degrees.")]
        [SerializeField, Min(0f)] private float m_RiseWobble = 7f;
        [Tooltip("Screen shake on every foot fall near the camera (heavy bosses; 0 = none).")]
        [SerializeField, Min(0f)] private float m_StepShake;

        private SpriteRenderer m_Renderer;
        private CharacterSprites.Clip m_Clip;
        private CharacterAnim m_Anim;
        private float m_ClipTime;
        private float m_PlaybackSpeed = 1f;
        private int m_Facing = 1;
        private float m_Lift;
        private float m_Alpha = 1f;
        private Color m_FlashColor = Color.white;
        private float m_FlashEnd;
        private float m_FlashDuration;
        private float m_Phase;                       // per character: a crowd does not breathe in step
        private float m_HitStart = float.NegativeInfinity;
        private float m_HitSide = 1f;                // +1: pushed towards its back, -1: towards its front
        private float m_DeadSince = float.NegativeInfinity;
        private float m_Rising;                      // 0..1 while coming out of the ground
        private int m_LastStep = -1;

        public CharacterSprites Sprites => m_Sprites;
        public CharacterAnim Anim => m_Anim;

        /// <summary>Extra size of the sprite (an enraged boss grows).</summary>
        public float ScaleMultiplier { get; set; } = 1f;

        /// <summary>Multiplies the sprite colour on top of the tint (an enraged boss glows red).</summary>
        public Color TintMultiplier { get; set; } = Color.white;
        public int Facing => m_Facing;
        public Vector2 Scale => m_Scale;

        /// <summary>Playback position of the current clip, 0..1.</summary>
        public float Progress => m_Clip == null ? 1f : Mathf.Clamp01(m_ClipTime / m_Clip.Duration);

        /// <summary>A clip that does not loop has shown its last frame for one frame time.</summary>
        public bool IsFinished => m_Clip == null || (!m_Clip.Loop && m_ClipTime >= m_Clip.Duration);

        private void Awake()
        {
            m_Renderer = GetComponent<SpriteRenderer>();
            m_Phase = UnityEngine.Random.value * 10f;
            Play(CharacterAnim.Idle, 1f, true);
        }

        /// <summary>Switches to <paramref name="anim"/> (kept running when it is already playing, unless restarted).</summary>
        public void Play(CharacterAnim anim, float speed = 1f, bool restart = false)
        {
            m_PlaybackSpeed = speed;
            if (!restart && anim == m_Anim && m_Clip != null)
                return;
            if (anim == CharacterAnim.Dead && m_Anim != CharacterAnim.Dead)
                m_DeadSince = Time.time;
            m_Anim = anim;
            m_Clip = m_Sprites != null ? m_Sprites.Get(anim) : null;
            m_ClipTime = 0f;
            ApplyFrame();
        }

        /// <summary>Faces right for positive, left for negative screen-x movement; keeps the facing near zero.</summary>
        public void SetFacing(float screenX)
        {
            if (screenX > c_FacingDeadZone)
                m_Facing = 1;
            else if (screenX < -c_FacingDeadZone)
                m_Facing = -1;
        }

        /// <summary>Vertical offset of the sprite, metres (negative = under the ground, hidden by it).</summary>
        public void SetLift(float metres) => m_Lift = metres;

        public void SetAlpha(float alpha) => m_Alpha = alpha;

        /// <summary>Tints the sprite towards <paramref name="color"/>, fading back over <paramref name="duration"/>.</summary>
        public void Flash(Color color, float duration)
        {
            m_FlashColor = color;
            m_FlashDuration = Mathf.Max(duration, 1e-3f);
            m_FlashEnd = Time.time + m_FlashDuration;
        }

        /// <summary>
        /// A hit: the body recoils away from <paramref name="direction"/> (world, level) with a squash, then settles.
        /// </summary>
        public void Kick(Vector3 direction)
        {
            m_HitStart = Time.time;
            // Pushed along its facing (hit from behind) leans it forward, against its facing (from the front) back.
            m_HitSide = direction.x * m_Facing > 0.05f ? -1f : 1f;
        }

        /// <summary>Coming out of the ground, 0..1 (1 = done): the body wobbles on its way up.</summary>
        public void SetRising(float progress) => m_Rising = progress >= 1f ? 0f : Mathf.Clamp01(1f - progress);

        /// <summary>Back to a fresh idle state (pooled characters).</summary>
        public void ResetState()
        {
            m_Lift = 0f;
            m_Alpha = 1f;
            m_FlashEnd = 0f;
            m_Facing = 1;
            m_HitStart = m_DeadSince = float.NegativeInfinity;
            m_Rising = 0f;
            ScaleMultiplier = 1f;
            TintMultiplier = Color.white;
            Play(CharacterAnim.Idle, 1f, true);
            ApplyPose();
        }

        private void LateUpdate()
        {
            m_ClipTime += Time.deltaTime * m_PlaybackSpeed;
            ApplyFrame();
            ApplyPose();
        }

        private void ApplyPose()
        {
            float bounce = 0f, sway = 0f;
            if (m_Clip != null && m_Anim == CharacterAnim.Move)
            {
                // Two foot falls per walk cycle (a 2-frame walk: one per frame, a 4-frame walk: every second frame);
                // the body rises between them.
                float phase = StepPhase;
                bounce = Mathf.Abs(Mathf.Sin(phase)) * m_StepBounce;
                sway = Mathf.Sin(phase) * m_StepSway;
                int step = Mathf.FloorToInt(phase / Mathf.PI);
                if (step != m_LastStep && m_StepShake > 0f)
                    CameraShake.AddAt(transform.position, m_StepShake);
                m_LastStep = step;
            }
            float lean = 0f, forward = 0f, sx = 1f, sy = 1f;
            if (m_Procedural)
                Procedural(ref lean, ref forward, ref sx, ref sy);
            // The pivot is at the feet: squash and stretch keep them on the ground. Lean is "forward" for the facing
            // (the mirrored scale is applied before the rotation, so the angle flips with the facing).
            transform.localPosition = new Vector3(forward * m_Facing, m_Lift + bounce, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, sway - lean * m_Facing);
            transform.localScale = new Vector3(m_Scale.x * m_Facing * sx, m_Scale.y * sy, 1f) * ScaleMultiplier;

            Color color = m_Tint * TintMultiplier;
            if (Time.time < m_FlashEnd)
                color = Color.Lerp(m_Tint * TintMultiplier, m_FlashColor, (m_FlashEnd - Time.time) / m_FlashDuration);
            color *= SceneTint.Characters;
            color.a = m_Alpha;
            m_Renderer.color = color;
        }

        // Squash & stretch and lean on top of the painted frames, by what the character does (lean: degrees forward,
        // forward: metres, sx / sy: scale multipliers; volume is roughly kept, sx ~ 1 / sy).
        private void Procedural(ref float lean, ref float forward, ref float sx, ref float sy)
        {
            float t = Time.time + m_Phase;
            float stretch = 0f;
            switch (m_Anim)
            {
                case CharacterAnim.Idle:
                    stretch = Mathf.Sin(t * m_BreathRate * Mathf.PI * 2f) * m_Breath;
                    break;
                case CharacterAnim.Move when m_Clip != null:
                {
                    float phase = StepPhase;
                    // Up between two foot falls (stretch), down when a foot lands (squash).
                    stretch = (Mathf.Abs(Mathf.Sin(phase)) - 0.5f) * 2f * m_StepSquash;
                    lean = m_WalkLean;
                    break;
                }
                case CharacterAnim.Attack:
                {
                    // Wind-up (lean back, crouch) to the strike (lunge forward, stretch), then back.
                    float p = Progress;
                    if (p < 0.45f)
                    {
                        float k = Ease(p / 0.45f);
                        lean = -m_AttackLean * 0.6f * k;
                        stretch = -0.07f * k;
                    }
                    else if (p < 0.65f)
                    {
                        float k = Ease((p - 0.45f) / 0.2f);
                        lean = Mathf.Lerp(-m_AttackLean * 0.6f, m_AttackLean, k);
                        forward = m_AttackLunge * k;
                        stretch = Mathf.Lerp(-0.07f, 0.06f, k);
                    }
                    else
                    {
                        float k = Ease((p - 0.65f) / 0.35f);
                        lean = Mathf.Lerp(m_AttackLean, 0f, k);
                        forward = Mathf.Lerp(m_AttackLunge, 0f, k);
                        stretch = Mathf.Lerp(0.06f, 0f, k);
                    }
                    break;
                }
                case CharacterAnim.Dead:
                {
                    // The body drops: a squash when it hits the ground, settling back.
                    float d = Time.time - m_DeadSince;
                    float fall = Mathf.Clamp01(d / 0.18f);
                    float settle = Mathf.Clamp01((d - 0.18f) / 0.35f);
                    stretch = d < 0.18f ? 0.04f * fall : -m_DeathThud * (1f - settle) * Mathf.Cos(settle * Mathf.PI * 1.5f);
                    break;
                }
            }
            // Hit recoil on top of anything (a short, damped kick).
            float h = (Time.time - m_HitStart) / m_HitTime;
            if (h >= 0f && h < 1f && m_Anim != CharacterAnim.Dead)
            {
                float kick = Mathf.Sin(h * Mathf.PI) * (1f - h);
                lean += -m_HitLean * m_HitSide * kick;
                stretch -= 0.08f * kick;
                forward += -0.06f * m_HitSide * kick;
            }
            // Rising from the ground: a wobble that calms down as the body comes up.
            if (m_Rising > 0f)
                lean += Mathf.Sin(t * 22f) * m_RiseWobble * m_Rising;
            sy = 1f + stretch;
            sx = 1f - stretch * 0.6f;
        }

        // pi per foot fall: a walk cycle of N painted frames has two steps.
        private float StepPhase => m_ClipTime * m_Clip.Fps * Mathf.PI / Mathf.Max(1f, m_Clip.Frames.Length * 0.5f);

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void ApplyFrame()
        {
            if (m_Clip == null)
                return;
            int count = m_Clip.Frames.Length;
            int frame = (int)(m_ClipTime * m_Clip.Fps);
            frame = m_Clip.Loop ? frame % count : Mathf.Min(frame, count - 1);
            m_Renderer.sprite = m_Clip.Frames[frame];
        }
    }
}
