using TMPro;
using UnityEngine;
using ZombiCar.Car;

namespace ZombiCar.UI
{
    /// <summary>
    /// HUD warning "МАШИНУ АТАКУЮТ!" under the wave panel when a zombie hits the car (<see cref="CarState.Attacked"/>):
    /// pops in blinking red and white, holds, fades out. While the attack goes on it comes back after a cooldown, so
    /// it reminds the player without staying on screen. It gives way to the wave banner (same place on screen).
    /// </summary>
    public sealed class CarAttackAlert : MonoBehaviour
    {
        [SerializeField] private CarState m_Car;
        [SerializeField] private WaveBanner m_Banner;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField, Min(0f)] private float m_HoldTime = 1.3f;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.5f;
        [Tooltip("Seconds after the warning faded before it can show again.")]
        [SerializeField, Min(0f)] private float m_Cooldown = 4f;
        [SerializeField, Min(0f)] private float m_BlinkTime = 0.6f;
        [SerializeField] private Color m_Color = new Color(1f, 0.32f, 0.24f, 1f);
        [SerializeField] private Color m_BlinkColor = Color.white;

        private float m_ShownAt = float.NegativeInfinity;

        /// <summary>How many times the warning came up (tests).</summary>
        public int Shows { get; private set; }

        public float Alpha => m_Group.alpha;

        private void OnEnable()
        {
            m_Car.Attacked += OnAttacked;
            m_Group.alpha = 0f;
        }

        private void OnDisable() => m_Car.Attacked -= OnAttacked;

        private void OnAttacked(int damage)
        {
            if (Time.unscaledTime - m_ShownAt < m_HoldTime + m_FadeTime + m_Cooldown || m_Banner.IsShowing)
                return;
            m_ShownAt = Time.unscaledTime;
            Shows++;
        }

        private void Update()
        {
            if (m_Banner.IsShowing)
                m_ShownAt = float.NegativeInfinity;
            float age = Time.unscaledTime - m_ShownAt;
            float alpha = age < m_HoldTime ? 1f : 1f - Mathf.Clamp01((age - m_HoldTime) / m_FadeTime);
            m_Group.alpha = alpha;
            if (alpha <= 0f)
                return;
            m_Body.localScale = Vector3.one * (1f + UiMotion.Punch(age / 0.3f) * 0.25f);
            bool blink = age < m_BlinkTime && Mathf.Repeat(age, 0.2f) < 0.1f;
            m_Text.color = blink ? m_BlinkColor : m_Color;
        }
    }
}
