using UnityEngine;
using UnityEngine.UI;

namespace ZombiCar.UI
{
    /// <summary>
    /// The damage trail behind a health bar (the boss, the player): when the fill drops, a translucent grey strip
    /// stays where the lost health was, holds for a moment and then drains down to the fill. Healing moves it up at
    /// once. Follows <see cref="m_Fill"/>'s fill amount, so the bar's own view needs no changes.
    /// </summary>
    public sealed class DamageTrailBar : MonoBehaviour
    {
        [Tooltip("The health fill (a Filled image) the trail follows.")]
        [SerializeField] private Image m_Fill;
        [Tooltip("The trail strip: stretched over the fill's lane, its right edge moves (anchorMax.x).")]
        [SerializeField] private RectTransform m_Trail;
        [Tooltip("How long the lost part stays before it drains (unscaled seconds).")]
        [SerializeField, Min(0f)] private float m_Hold = 0.45f;
        [Tooltip("How fast it drains, in bar widths per second (at least; a big hit drains faster).")]
        [SerializeField, Min(0.01f)] private float m_DrainSpeed = 0.6f;

        private float m_Value = -1f;
        private float m_LastFill = -1f;
        private float m_DrainAt;

        /// <summary>Where the trail ends now, 0..1 (tests).</summary>
        public float Value => m_Value;

        private void OnEnable() => m_Value = m_LastFill = -1f;

        // LateUpdate: after the bar's view has set the fill this frame.
        private void LateUpdate()
        {
            float fill = m_Fill.fillAmount;
            if (m_LastFill >= 0f && fill < m_LastFill - 0.0001f)
                m_DrainAt = Time.unscaledTime + m_Hold;                       // hit after hit keeps the trail up
            m_LastFill = fill;
            if (m_Value < 0f || fill >= m_Value)
                m_Value = fill;                                               // first frame, or healed: no trail
            else if (Time.unscaledTime >= m_DrainAt)
                m_Value = Mathf.MoveTowards(m_Value, fill, Time.unscaledDeltaTime * Mathf.Max(m_DrainSpeed, (m_Value - fill) * 3f));
            Vector2 max = m_Trail.anchorMax;
            max.x = m_Value;
            m_Trail.anchorMax = max;
        }
    }
}
