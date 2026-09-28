using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Player;

namespace ZombiCar.UI
{
    /// <summary>
    /// HUD weapon slots (desktop, bottom right): the gun [1] and the axe [2], the one in hand lit and a little bigger,
    /// with the switch hint; when the gun ran dry and the axe came out by itself it says so.
    /// </summary>
    public sealed class WeaponSlotsView : MonoBehaviour
    {
        [SerializeField] private WeaponState m_State;
        [SerializeField] private Image m_GunPlate;
        [SerializeField] private Image m_AxePlate;
        [SerializeField] private TMP_Text m_Hint;
        [SerializeField] private Color m_Active = new Color(0.93f, 0.47f, 0.13f, 1f);
        [SerializeField] private Color m_Idle = new Color(0.12f, 0.13f, 0.16f, 0.85f);
        [SerializeField] private string m_SwitchHint = "Q: СМЕНИТЬ ОРУЖИЕ";
        [SerializeField] private string m_EmptyHint = "ПАТРОНЫ КОНЧИЛИСЬ: ТОПОР";

        private void OnEnable()
        {
            if (Core.DeviceInfo.IsMobile)
            {
                gameObject.SetActive(false);                           // touch screens use the weapon button
                return;
            }
            m_State.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => m_State.Changed -= Refresh;

        private void Refresh()
        {
            bool axe = m_State.Current == WeaponSlot.Axe;
            m_GunPlate.color = axe ? m_Idle : m_Active;
            m_AxePlate.color = axe ? m_Active : m_Idle;
            m_GunPlate.rectTransform.localScale = Vector3.one * (axe ? 0.9f : 1f);
            m_AxePlate.rectTransform.localScale = Vector3.one * (axe ? 1f : 0.9f);
            m_Hint.SetText(axe && m_State.Automatic ? m_EmptyHint : m_SwitchHint);
        }
    }
}
