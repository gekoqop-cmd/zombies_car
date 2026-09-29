using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZombiCar.Player;

namespace ZombiCar.UI
{
    /// <summary>
    /// HUD weapon slots in a column: the gun on top, the axe under it, the one in hand lit orange and a little bigger.
    /// A click / tap on a slot takes that weapon. The PC set (bottom right, with the 1 / 2 key numbers) shows on
    /// desktop only; the touch set (next to the fire stick, no numbers, movable with the other controls) on touch
    /// screens only. When the gun ran dry and the axe came out by itself, the notice says so.
    /// </summary>
    public sealed class WeaponSlotsView : MonoBehaviour
    {
        [SerializeField] private WeaponState m_State;
        [Tooltip("The touch set (shown on touch screens); otherwise the PC set (shown on desktop).")]
        [SerializeField] private bool m_Mobile;
        [SerializeField] private Image m_GunPlate;
        [SerializeField] private Image m_AxePlate;
        [Tooltip("Optional: 'out of ammo, axe' notice beside the slots.")]
        [SerializeField, Core.OptionalReference] private TMP_Text m_Hint;
        [SerializeField, Core.OptionalReference] private MobileControlsLayout m_Layout;
        [SerializeField] private Color m_Active = new Color(0.93f, 0.47f, 0.13f, 1f);
        [SerializeField] private Color m_Idle = new Color(0.12f, 0.13f, 0.16f, 0.85f);
        [SerializeField] private string m_EmptyHint = "ПАТРОНЫ КОНЧИЛИСЬ: ТОПОР";

        private void Awake()
        {
            AddTap(m_GunPlate, WeaponSlot.Gun);
            AddTap(m_AxePlate, WeaponSlot.Axe);
        }

        private void OnEnable()
        {
            if (Core.DeviceInfo.IsMobile != m_Mobile)
            {
                gameObject.SetActive(false);
                return;
            }
            m_State.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => m_State.Changed -= Refresh;

        // Dragging the controls around tints them; the lit slot is put back right after.
        private void LateUpdate()
        {
            if (m_Layout != null && m_Layout.IsEditing)
                Refresh();
        }

        private void Refresh()
        {
            bool axe = m_State.Current == WeaponSlot.Axe;
            m_GunPlate.color = axe ? m_Idle : m_Active;
            m_AxePlate.color = axe ? m_Active : m_Idle;
            m_GunPlate.rectTransform.localScale = Vector3.one * (axe ? 0.9f : 1f);
            m_AxePlate.rectTransform.localScale = Vector3.one * (axe ? 1f : 0.9f);
            if (m_Hint != null)
            {
                bool empty = axe && m_State.Automatic;
                m_Hint.gameObject.SetActive(empty);
                m_Hint.SetText(m_EmptyHint);
            }
        }

        private void AddTap(Image plate, WeaponSlot slot)
        {
            plate.raycastTarget = true;
            var tap = plate.gameObject.AddComponent<Tap>();
            tap.Owner = this;
            tap.Slot = slot;
        }

        private void Pick(WeaponSlot slot)
        {
            if (m_Layout != null && m_Layout.IsEditing)
                return;
            m_State.Request(slot);
        }

        // Fires on press (like the touch buttons), so a tap while running is not lost.
        private sealed class Tap : MonoBehaviour, IPointerDownHandler
        {
            public WeaponSlotsView Owner;
            public WeaponSlot Slot;

            public void OnPointerDown(PointerEventData eventData) => Owner.Pick(Slot);
        }
    }
}
