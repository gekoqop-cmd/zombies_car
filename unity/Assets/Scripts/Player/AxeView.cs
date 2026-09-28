using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// The axe in the player's hand (child of the upright view): carried slanted forward while the axe is the weapon,
    /// swung in a big arc on every attack — raised behind the head in the wind-up, brought down in front with a white
    /// comic slash crescent — and put away with the gun. Mirrors with the body's facing; the stretch against the
    /// camera's pitch sits on this object, so the rotation below it looks right on screen.
    /// </summary>
    public sealed class AxeView : MonoBehaviour
    {
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private PlayerMelee m_Melee;
        [Tooltip("The body sprite: its mirrored scale gives the facing.")]
        [SerializeField] private Transform m_Body;
        [Tooltip("Turns around the grip.")]
        [SerializeField] private Transform m_Pivot;
        [SerializeField] private Renderer m_Axe;
        [SerializeField] private Renderer m_Slash;
        [Tooltip("Grip position in front of the body (x, facing right) and height, metres.")]
        [SerializeField] private Vector2 m_Grip = new Vector2(0.28f, 0.9f);
        [SerializeField] private float m_PitchCompensation = 1.56f;
        [Header("Handle angles from upright (facing right, degrees; positive = leaning back)")]
        [SerializeField] private float m_CarryAngle = -28f;
        [SerializeField] private float m_RaisedAngle = 40f;
        [SerializeField] private float m_StrikeAngle = -95f;
        [SerializeField, Min(0.01f)] private float m_StrikeTime = 0.09f;
        [SerializeField, Min(0.01f)] private float m_RecoverTime = 0.28f;

        private MaterialPropertyBlock m_Block;
        private Color m_SlashColor;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_SlashColor = m_Slash.sharedMaterial.GetColor(s_BaseColor);
        }

        private void LateUpdate()
        {
            float swing = m_Melee.SwingTime;
            float windup = m_Melee.Config.Windup;
            bool swinging = swing < windup + m_StrikeTime + m_RecoverTime;
            bool visible = m_Melee.Active || swinging;
            m_Axe.enabled = visible;
            if (!visible)
            {
                m_Slash.enabled = false;
                return;
            }

            float facing = m_Body.localScale.x < 0f ? -1f : 1f;
            transform.localPosition = new Vector3(m_Grip.x * facing, m_Grip.y, -0.04f);
            transform.localScale = new Vector3(facing, m_PitchCompensation, 1f);

            float angle = m_CarryAngle;
            float slash = 0f;
            if (swinging)
            {
                if (swing < windup)
                {
                    angle = Mathf.Lerp(m_CarryAngle, m_RaisedAngle, EaseOut(swing / windup));
                }
                else if (swing < windup + m_StrikeTime)
                {
                    float t = (swing - windup) / m_StrikeTime;
                    angle = Mathf.Lerp(m_RaisedAngle, m_StrikeAngle, t * t);
                    slash = t;
                }
                else
                {
                    float t = (swing - windup - m_StrikeTime) / m_RecoverTime;
                    angle = Mathf.Lerp(m_StrikeAngle, m_CarryAngle, EaseOut(t));
                    slash = 1f - t;
                }
            }
            m_Pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            m_Slash.enabled = slash > 0.01f;
            if (m_Slash.enabled)
            {
                Color c = m_SlashColor;
                c.a *= slash;
                m_Block.SetColor(s_BaseColor, c);
                m_Slash.SetPropertyBlock(m_Block);
            }
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
