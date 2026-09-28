using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Player;

namespace ZombiCar.UI
{
    /// <summary>The touch weapon button shows the weapon it switches to (the axe while the gun is in hand, and back).</summary>
    public sealed class WeaponButtonView : MonoBehaviour
    {
        [SerializeField] private WeaponState m_State;
        [SerializeField] private Image m_Icon;
        [SerializeField] private Sprite m_GunIcon;
        [SerializeField] private Sprite m_AxeIcon;

        private void OnEnable()
        {
            m_State.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => m_State.Changed -= Refresh;

        private void Refresh() => m_Icon.sprite = m_State.Current == WeaponSlot.Axe ? m_GunIcon : m_AxeIcon;
    }
}
