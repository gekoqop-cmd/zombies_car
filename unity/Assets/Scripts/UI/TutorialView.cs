using TMPro;
using UnityEngine;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The tutorial's live hint on the HUD (<see cref="TutorialState"/>): a pill above the interaction prompt with
    /// "ПОДСКАЗКА" (or a numbered step) and the hint, popping in on every new hint and fading out at the end. It sits
    /// above the level-up cards, so the first card's hint reads over them.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        [SerializeField] private TutorialState m_State;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private TMP_Text m_Step;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField, Min(0.01f)] private float m_Fade = 0.25f;

        [Tooltip("Height above the bottom while the level-up cards are open (under them).")]
        [SerializeField] private float m_OverCardsY = 30f;

        private float m_ShownAt = float.NegativeInfinity;
        private float m_DefaultY = float.NaN;

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
            m_Step.SetText(m_State.Steps > 0 ? $"ОБУЧЕНИЕ {m_State.Step}/{m_State.Steps}" : "ПОДСКАЗКА");
            m_ShownAt = Time.unscaledTime;
        }

        private void Update()
        {
            float target = m_State.Visible ? 1f : 0f;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, target, Time.unscaledDeltaTime / m_Fade);
            float pop = UiMotion.BackOut((Time.unscaledTime - m_ShownAt) / 0.3f, 2.2f);
            m_Body.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, pop);
            var rt = (RectTransform)transform;
            if (float.IsNaN(m_DefaultY))
                m_DefaultY = rt.anchoredPosition.y;
            bool cards = Core.GamePause.Has(Core.GamePause.Reason.LevelUp);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, cards ? m_OverCardsY : m_DefaultY);
        }
    }
}
