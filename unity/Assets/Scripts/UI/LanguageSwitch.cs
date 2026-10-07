using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// The main menu's RU / EN switch (top left corner): the chosen language is highlighted; a click on the other one
    /// switches every text at once and is remembered (<see cref="Loc.Choose"/>).
    /// </summary>
    public sealed class LanguageSwitch : MonoBehaviour
    {
        [SerializeField] private Button m_Russian;
        [SerializeField] private Button m_English;
        [Tooltip("Tints over the dark button plate: the chosen language bright, the other dimmed.")]
        [SerializeField] private Color m_Selected = new Color(1.6f, 1.25f, 0.75f, 1f);
        [SerializeField] private Color m_Normal = new Color(0.45f, 0.45f, 0.5f, 0.75f);

        private void Awake()
        {
            m_Russian.onClick.AddListener(() => Pick(GameLanguage.Russian));
            m_English.onClick.AddListener(() => Pick(GameLanguage.English));
        }

        private void OnEnable()
        {
            Loc.Changed += Show;
            Show();
        }

        private void OnDisable() => Loc.Changed -= Show;

        private void Pick(GameLanguage language)
        {
            if (language == Loc.Language)
                return;
            AudioManager.PlaySfx(Sound.UiClick);
            Loc.Choose(language);
        }

        private void Show()
        {
            Loc.EnsureLoaded();
            Paint(m_Russian, !Loc.IsEnglish);
            Paint(m_English, Loc.IsEnglish);
        }

        private void Paint(Button button, bool selected)
        {
            if (button == null)
                return;
            if (button.targetGraphic != null)
                button.targetGraphic.color = selected ? m_Selected : m_Normal;
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null)
                label.alpha = selected ? 1f : 0.55f;
        }
    }
}
