using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>Settings window: quality tier (Low / Medium / High) and close. Highlights the active tier.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Button m_LowButton;
        [SerializeField] private Button m_MediumButton;
        [SerializeField] private Button m_HighButton;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Color m_NormalColor = new Color(0.16f, 0.17f, 0.2f, 0.95f);
        [SerializeField] private Color m_SelectedColor = new Color(0.9f, 0.45f, 0.12f, 1f);

        private void Awake()
        {
            m_LowButton.onClick.AddListener(() => Select(QualityTier.Low));
            m_MediumButton.onClick.AddListener(() => Select(QualityTier.Medium));
            m_HighButton.onClick.AddListener(() => Select(QualityTier.High));
            m_CloseButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Highlight(SettingsService.Quality);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Select(QualityTier tier)
        {
            SettingsService.SetQuality(tier);
            Highlight(tier);
        }

        private void Highlight(QualityTier tier)
        {
            Paint(m_LowButton, tier == QualityTier.Low);
            Paint(m_MediumButton, tier == QualityTier.Medium);
            Paint(m_HighButton, tier == QualityTier.High);
        }

        private void Paint(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = selected ? m_SelectedColor : m_NormalColor;
        }
    }
}
