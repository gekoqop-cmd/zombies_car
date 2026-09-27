using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// Settings pop-up (main menu and HUD gear button): master / music / effects volume sliders (the mixer groups via
    /// <see cref="SettingsService"/>), quality tier (Low / Medium / High / Ultra; Ultra hidden on mobile,
    /// <see cref="SettingsService.IsUltraAllowed"/>), Back and — in the game only — "To menu", which leaves the map for
    /// the main menu. Fades in and out (unscaled time).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Header("Volume")]
        [SerializeField] private Slider m_MasterSlider;
        [SerializeField] private Slider m_MusicSlider;
        [SerializeField] private Slider m_SfxSlider;

        [Header("Quality")]
        [SerializeField] private Button m_LowButton;
        [SerializeField] private Button m_MediumButton;
        [SerializeField] private Button m_HighButton;
        [SerializeField] private Button m_UltraButton;
        [SerializeField] private Color m_NormalColor = new Color(0.16f, 0.17f, 0.2f, 0.95f);
        [SerializeField] private Color m_SelectedColor = new Color(0.9f, 0.45f, 0.12f, 1f);

        [Header("Touch controls (mobile only)")]
        [SerializeField, OptionalReference] private GameObject m_MobileSection;
        [SerializeField, OptionalReference] private Slider m_ControlsSizeSlider;
        [Tooltip("Opens the controls' edit mode (only where the controls exist: the HUD).")]
        [SerializeField, OptionalReference] private Button m_EditControlsButton;
        [Tooltip("The window; it is this much shorter when the touch-controls section is hidden.")]
        [SerializeField] private RectTransform m_Window;
        [SerializeField, Min(0f)] private float m_MobileSectionHeight;

        [SerializeField] private Button m_CloseButton;
        [Tooltip("Leaves the map for the main menu (only the HUD's pop-up has it).")]
        [SerializeField, OptionalReference] private Button m_MenuButton;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.15f;

        private CanvasGroup m_Group;
        private float m_Target;
        private bool m_Initialised;

        private void Awake() => Init();

        private void Init()
        {
            if (m_Initialised)
                return;
            m_Initialised = true;
            m_Group = GetComponent<CanvasGroup>();
            SettingsService.EnsureLoaded();
            m_LowButton.onClick.AddListener(() => Select(QualityTier.Low));
            m_MediumButton.onClick.AddListener(() => Select(QualityTier.Medium));
            m_HighButton.onClick.AddListener(() => Select(QualityTier.High));
            if (m_UltraButton != null)
            {
                m_UltraButton.gameObject.SetActive(SettingsService.IsUltraAllowed);
                m_UltraButton.onClick.AddListener(() => Select(QualityTier.Ultra));
            }
            m_CloseButton.onClick.AddListener(Hide);
            if (m_MenuButton != null)
                m_MenuButton.onClick.AddListener(ToMenu);
            bool mobile = DeviceInfo.IsMobile && m_MobileSection != null;
            if (m_MobileSection != null)
                m_MobileSection.SetActive(mobile);
            if (!mobile && m_Window != null)
                m_Window.sizeDelta -= new Vector2(0f, m_MobileSectionHeight);
            if (m_ControlsSizeSlider != null)
            {
                m_ControlsSizeSlider.minValue = SettingsService.c_MinControlsScale;
                m_ControlsSizeSlider.maxValue = SettingsService.c_MaxControlsScale;
                m_ControlsSizeSlider.onValueChanged.AddListener(SettingsService.SetControlsScale);
            }
            if (m_EditControlsButton != null)
                m_EditControlsButton.onClick.AddListener(EditControls);
            m_MasterSlider.onValueChanged.AddListener(SettingsService.SetMasterVolume);
            m_MusicSlider.onValueChanged.AddListener(SettingsService.SetMusicVolume);
            m_SfxSlider.onValueChanged.AddListener(SettingsService.SetSfxVolume);
        }

        public bool IsOpen => gameObject.activeSelf && m_Target > 0f;

        public void Show()
        {
            Init();
            gameObject.SetActive(true);
            m_MasterSlider.SetValueWithoutNotify(SettingsService.MasterVolume);
            m_MusicSlider.SetValueWithoutNotify(SettingsService.MusicVolume);
            m_SfxSlider.SetValueWithoutNotify(SettingsService.SfxVolume);
            if (m_ControlsSizeSlider != null)
                m_ControlsSizeSlider.SetValueWithoutNotify(SettingsService.ControlsScale);
            if (m_EditControlsButton != null)
                m_EditControlsButton.gameObject.SetActive(MobileControlsLayout.Instance != null);
            Highlight(SettingsService.Quality);
            m_Target = 1f;
            m_Group.interactable = m_Group.blocksRaycasts = true;
        }

        public void Hide()
        {
            m_Target = 0f;
            if (m_Group != null)
                m_Group.interactable = m_Group.blocksRaycasts = false;
        }

        /// <summary>Shows or hides at once (tools, screenshots).</summary>
        public void Snap()
        {
            if (m_Group != null)
                m_Group.alpha = m_Target;
            if (m_Target <= 0f)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Target, Time.unscaledDeltaTime / m_FadeTime);
            if (m_Target <= 0f && m_Group.alpha <= 0f)
                gameObject.SetActive(false);
        }

        private void EditControls()
        {
            Hide();
            Snap();
            if (MobileControlsLayout.Instance != null)
                MobileControlsLayout.Instance.BeginEdit();
        }

        private void ToMenu()
        {
            m_MenuButton.interactable = false;
            SceneLoader.EnsureExists().LoadMainMenu();
        }

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
            Paint(m_UltraButton, tier == QualityTier.Ultra);
        }

        private void Paint(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = selected ? m_SelectedColor : m_NormalColor;
        }
    }
}
