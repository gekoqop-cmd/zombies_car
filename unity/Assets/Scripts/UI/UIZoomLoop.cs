using UnityEngine;

namespace ZombiCar.UI
{
    /// <summary>Slow breathing zoom of a background: 100% → MaxScale → 100% over one period (unscaled time).</summary>
    public sealed class UIZoomLoop : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float m_MaxScale = 1.05f;
        [SerializeField, Min(0.1f)] private float m_Period = 12f;

        private void Update()
        {
            float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / m_Period);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, m_MaxScale, k);
        }
    }
}
