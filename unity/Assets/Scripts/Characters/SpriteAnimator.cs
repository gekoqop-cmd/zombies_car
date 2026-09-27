using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Characters
{
    /// <summary>
    /// Plays <see cref="CharacterSprites"/> clips on an upright SpriteRenderer and adds the motion the few painted
    /// frames lack: a bounce and sway on every step, a colour flash on hits, a vertical offset for rising from or
    /// sinking into the ground, and left/right facing (mirrored scale, so child sprites such as the muzzle flash
    /// follow). The Y scale includes 1 / cos(camera pitch) (set at build time) so the top-down camera does not
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

        public CharacterSprites Sprites => m_Sprites;
        public CharacterAnim Anim => m_Anim;
        public int Facing => m_Facing;
        public Vector2 Scale => m_Scale;

        /// <summary>Playback position of the current clip, 0..1.</summary>
        public float Progress => m_Clip == null ? 1f : Mathf.Clamp01(m_ClipTime / m_Clip.Duration);

        /// <summary>A clip that does not loop has shown its last frame for one frame time.</summary>
        public bool IsFinished => m_Clip == null || (!m_Clip.Loop && m_ClipTime >= m_Clip.Duration);

        private void Awake()
        {
            m_Renderer = GetComponent<SpriteRenderer>();
            Play(CharacterAnim.Idle, 1f, true);
        }

        /// <summary>Switches to <paramref name="anim"/> (kept running when it is already playing, unless restarted).</summary>
        public void Play(CharacterAnim anim, float speed = 1f, bool restart = false)
        {
            m_PlaybackSpeed = speed;
            if (!restart && anim == m_Anim && m_Clip != null)
                return;
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

        /// <summary>Back to a fresh idle state (pooled characters).</summary>
        public void ResetState()
        {
            m_Lift = 0f;
            m_Alpha = 1f;
            m_FlashEnd = 0f;
            m_Facing = 1;
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
                // One bounce per painted frame: the body rises between two foot falls.
                float phase = m_ClipTime * m_Clip.Fps * Mathf.PI;
                bounce = Mathf.Abs(Mathf.Sin(phase)) * m_StepBounce;
                sway = Mathf.Sin(phase) * m_StepSway;
            }
            transform.localPosition = new Vector3(0f, m_Lift + bounce, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, sway);
            transform.localScale = new Vector3(m_Scale.x * m_Facing, m_Scale.y, 1f);

            Color color = m_Tint;
            if (Time.time < m_FlashEnd)
                color = Color.Lerp(m_Tint, m_FlashColor, (m_FlashEnd - Time.time) / m_FlashDuration);
            color *= SceneTint.Characters;
            color.a = m_Alpha;
            m_Renderer.color = color;
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
