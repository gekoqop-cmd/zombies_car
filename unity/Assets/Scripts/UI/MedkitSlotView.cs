using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The first-aid kits carried (HUD, next to the weapon slots): the kit with how many there are; 3 on the keyboard or
    /// a click / tap uses one (<see cref="RunState.RequestMedkit"/>: the player heals to full if hurt). Dimmed when empty,
    /// pops when a kit comes in or is used.
    /// </summary>
    public sealed class MedkitSlotView : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_Plate;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_Count;
        [SerializeField] private Color m_Full = new Color(0.86f, 0.27f, 0.25f, 0.95f);
        [SerializeField] private Color m_Empty = new Color(0.12f, 0.13f, 0.16f, 0.8f);

        private int m_Shown = -1;
        private float m_PopAt = float.NegativeInfinity;
        private int m_PausedFrame = -1;

        private void Awake() => m_Button.onClick.AddListener(Use);

        private void OnEnable()
        {
            m_Run.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => m_Run.Changed -= Refresh;

        /// <summary>Uses a kit (click, key, tests).</summary>
        public void Use() => m_Run.RequestMedkit();

        private void Refresh()
        {
            int n = m_Run.Medkits;
            if (n == m_Shown)
                return;
            if (m_Shown >= 0)
                m_PopAt = Time.unscaledTime;
            m_Shown = n;
            m_Count.SetText($"{n}/{RunState.MaxMedkits}");
            m_Plate.color = n > 0 ? m_Full : m_Empty;
            m_Icon.color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        private void Update()
        {
            Keyboard keys = Keyboard.current;
            // 3 also picks the third level-up card: a press that closed a pause this frame is not a kit.
            if (GamePause.IsPaused)
                m_PausedFrame = Time.frameCount;
            else if (keys != null && keys.digit3Key.wasPressedThisFrame && Time.frameCount - m_PausedFrame > 1)
                Use();
            float t = (Time.unscaledTime - m_PopAt) / 0.3f;
            m_Icon.transform.localScale = Vector3.one * (1f + UiMotion.Punch(t) * 0.35f);   // the icon: the slot itself may be scaled by the touch layout
        }
    }
}
