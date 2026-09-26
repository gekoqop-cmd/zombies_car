using UnityEngine;
using UnityEngine.UI;

namespace ZombiCar.Car
{
    /// <summary>
    /// World-space durability bar floating above the player's car (built by SceneBuilder next to it), fading from
    /// green to red with <see cref="CarHealth"/>. Its anchor carries a YAxisBillboard, same as the character sprites.
    /// </summary>
    public sealed class CarHealthBar : MonoBehaviour
    {
        private static readonly Color s_FullColor = new Color(0.32f, 0.78f, 0.28f, 1f);
        private static readonly Color s_EmptyColor = new Color(0.82f, 0.18f, 0.14f, 1f);

        [SerializeField] private CarHealth m_Car;
        [SerializeField] private Image m_Fill;

        private void OnEnable()
        {
            if (m_Car != null)
                m_Car.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (m_Car != null)
                m_Car.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (m_Car == null || m_Fill == null)
                return;
            float fraction = (float)m_Car.Health / m_Car.MaxHealth;
            m_Fill.fillAmount = fraction;
            m_Fill.color = Color.Lerp(s_EmptyColor, s_FullColor, fraction);
        }
    }
}
