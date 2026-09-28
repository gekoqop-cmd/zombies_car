using UnityEngine;
using UnityEngine.EventSystems;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// On-screen action button of the mobile controls (reload; interact: workshop, ammo at the car). Fires on press.
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler
    {
        public enum Action { Reload, Interact }

        [SerializeField] private Action m_Action;
        [SerializeField] private MobileControlsLayout m_Layout;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_Layout != null && m_Layout.IsEditing)
                return;
            switch (m_Action)
            {
                case Action.Reload:
                    MobileControls.ReloadFrame = Time.frameCount;
                    break;
                case Action.Interact:
                    MobileControls.InteractFrame = Time.frameCount;
                    break;
            }
        }
    }
}
