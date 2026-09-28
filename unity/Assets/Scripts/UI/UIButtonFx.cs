using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// Hover / press feedback of a button: eases its scale up on hover and down while pressed (unscaled time, so it
    /// also works in pause menus) and clicks on press. Disabled (non-interactable) buttons do not react.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(1f)] private float m_HoverScale = 1.06f;
        [SerializeField, Range(0.5f, 1f)] private float m_PressScale = 0.94f;
        [Tooltip("Seconds to settle on the new scale.")]
        [SerializeField, Min(0.01f)] private float m_Smoothing = 0.08f;

        private Selectable m_Selectable;
        private Vector3 m_BaseScale;
        private bool m_Hover;
        private bool m_Pressed;

        private void Awake()
        {
            m_Selectable = GetComponent<Selectable>();
            m_BaseScale = transform.localScale;
        }

        private void OnDisable()
        {
            m_Hover = m_Pressed = false;
            transform.localScale = m_BaseScale;
        }

        public void OnPointerEnter(PointerEventData eventData) => m_Hover = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Hover = false;
            m_Pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_Pressed = true;
            if (m_Selectable == null || m_Selectable.IsInteractable())
                AudioManager.PlaySfx(Sound.UiClick);
        }

        public void OnPointerUp(PointerEventData eventData) => m_Pressed = false;

        private void Update()
        {
            bool active = m_Selectable == null || m_Selectable.IsInteractable();
            float target = !active ? 1f : m_Pressed ? m_PressScale : m_Hover ? m_HoverScale : 1f;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / m_Smoothing);
            transform.localScale = Vector3.Lerp(transform.localScale, m_BaseScale * target, k);
        }
    }
}
