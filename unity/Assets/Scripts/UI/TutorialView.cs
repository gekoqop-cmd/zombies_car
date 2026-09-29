using TMPro;
using UnityEngine;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The tutorial hint on the HUD (<see cref="TutorialState"/>): a pill above the interaction prompt with the step
    /// ("ОБУЧЕНИЕ 2/5") and the hint, popping in on every new step and fading out at the end.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        [SerializeField] private TutorialState m_State;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private TMP_Text m_Step;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField, Min(0.01f)] private float m_Fade = 0.25f;

        private float m_ShownAt = float.NegativeInfinity;

        private void OnEnable()
        {
            m_State.Changed += OnChanged;
            m_Group.alpha = 0f;
            OnChanged();
        }

        private void OnDisable() => m_State.Changed -= OnChanged;

        private void OnChanged()
        {
            if (!m_State.Visible)
                return;
            m_Text.SetText(m_State.Text);
            m_Step.SetText($"ОБУЧЕНИЕ {m_State.Step}/{m_State.Steps}");
            m_ShownAt = Time.unscaledTime;
        }

        private void Update()
        {
            float target = m_State.Visible ? 1f : 0f;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, target, Time.unscaledDeltaTime / m_Fade);
            float pop = UiMotion.BackOut((Time.unscaledTime - m_ShownAt) / 0.3f, 2.2f);
            m_Body.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, pop);
        }
    }
}
