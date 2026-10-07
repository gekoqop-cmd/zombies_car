using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// Settings pop-up (main menu and HUD gear button): music / effects volume sliders (the mixer groups via
    /// <see cref="SettingsService"/>), quality tier (Low / Medium / High / Ultra; Ultra hidden on mobile,
    /// <see cref="SettingsService.IsUltraAllowed"/>), on a phone only "ТРЯСКА И ВИБРАЦИЯ" on / off and the touch
    /// controls, Back and — in the game only — "To menu", which leaves the map for the main menu. Fades in and out
    /// (unscaled time).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Header("Volume")]
        [SerializeField] private Slider m_MusicSlider;
        [SerializeField] private Slider m_SfxSlider;

        [Header("Shake and vibration")]
        [Tooltip("\"ВКЛ\" / \"ВЫКЛ\": screen shake, hit stops and vibration (SettingsService.ShakeEnabled).")]
        [SerializeField, OptionalReference] private Button m_ShakeButton;
        [SerializeField, OptionalReference] private TMPro.TMP_Text m_ShakeLabel;

        [Header("Quality")]
        [SerializeField] private Button m_LowButton;
        [SerializeField] private Button m_MediumButton;
        [SerializeField] private Button m_HighButton;
        [SerializeField] private Button m_UltraButton;
        [SerializeField] private Color m_NormalColor = new Color(0.16f, 0.17f, 0.2f, 0.95f);
        [SerializeField] private Color m_SelectedColor = new Color(0.9f, 0.45f, 0.12f, 1f);

        [Header("Phone only: shake switch, touch controls")]
        [SerializeField, OptionalReference] private GameObject m_MobileSection;
        [SerializeField, OptionalReference] private Slider m_ControlsSizeSlider;
        [Tooltip("Opens the controls' edit mode (only where the controls exist: the HUD).")]
        [SerializeField, OptionalReference] private Button m_EditControlsButton;
        [Tooltip("The window; it is this much shorter when the phone-only section is hidden.")]
        [SerializeField] private RectTransform m_Window;

        [SerializeField] private Button m_CloseButton;
        [Tooltip("Leaves the map for the main menu (only the HUD's pop-up has it).")]
        [SerializeField, OptionalReference] private Button m_MenuButton;
        [Tooltip("The coin balance (coin icon + number): the in-game pause only.")]
        [SerializeField, OptionalReference] private TMPro.TMP_Text m_Coins;
        [Tooltip("Replays the tutorial: its intro cards now, its live hints in the game.")]
        [SerializeField, OptionalReference] private Button m_TutorialButton;
        [SerializeField, OptionalReference] private Progression.TutorialState m_Tutorial;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.15f;

        private CanvasGroup m_Group;
        private float m_Target;
        private bool m_Initialised;
        private float m_ScaleAtShow;

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
            if (m_TutorialButton != null)
                m_TutorialButton.onClick.AddListener(ReplayTutorial);
            bool mobile = DeviceInfo.IsMobile && m_MobileSection != null;
            // The shake switch is for every device: out of the phone-only part, into the window itself.
            if (m_ShakeButton != null && m_Window != null && m_ShakeButton.transform.parent != null)
                m_ShakeButton.transform.parent.SetParent(m_Window, false);
            if (m_MobileSection != null)
                m_MobileSection.SetActive(mobile);
            Layout(mobile);
            if (m_ControlsSizeSlider != null)
            {
                m_ControlsSizeSlider.minValue = SettingsService.c_MinControlsScale;
                m_ControlsSizeSlider.maxValue = SettingsService.c_MaxControlsScale;
                m_ControlsSizeSlider.onValueChanged.AddListener(SettingsService.SetControlsScale);
            }
            if (m_EditControlsButton != null)
                m_EditControlsButton.onClick.AddListener(EditControls);
            m_MusicSlider.onValueChanged.AddListener(SettingsService.SetMusicVolume);
            if (m_ShakeButton != null)
                m_ShakeButton.onClick.AddListener(ToggleShake);
            m_SfxSlider.onValueChanged.AddListener(SettingsService.SetSfxVolume);
        }

        // Rows from the top of the window (centres, px): the quality caption clear of the tier buttons below it, the
        // shake row, the touch controls row on a phone, then the bottom buttons with room above them.
        private const float c_QualityLabelY = -318f;
        private const float c_TiersY = -412f;
        private const float c_ShakeY = -572f;
        private const float c_TouchY = -672f;
        private const float c_BottomRoom = 245f;          // the bottom buttons (40 + 110) and a gap above them

        private void Layout(bool mobile)
        {
            if (m_Window == null)
                return;
            void Place(string path, float y)
            {
                if (m_Window.Find(path) is RectTransform rt)
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            }
            Place("QualityLabel", c_QualityLabelY);
            Place("TierButtons", c_TiersY);
            Place("Shake", c_ShakeY);
            Place("PhoneOnly/TouchControls", c_TouchY);
            float lowest = mobile ? c_TouchY - 45f : c_ShakeY - 42f;
            m_Window.sizeDelta = new Vector2(m_Window.sizeDelta.x, -lowest + c_BottomRoom);
        }

        public bool IsOpen => gameObject.activeSelf && m_Target > 0f;

        private void OnDisable()
        {
            if (m_Target > 0f)
                GamePause.Set(GamePause.Reason.Settings, false);
        }

        public void Show()
        {
            Init();
            gameObject.SetActive(true);
            m_MusicSlider.SetValueWithoutNotify(SettingsService.MusicVolume);
            m_SfxSlider.SetValueWithoutNotify(SettingsService.SfxVolume);
            if (m_ControlsSizeSlider != null)
                m_ControlsSizeSlider.SetValueWithoutNotify(SettingsService.ControlsScale);
            m_ScaleAtShow = SettingsService.ControlsScale;
            ShowShake();
            if (m_Coins != null)
                m_Coins.SetText(MetaProgress.Coins.ToString());
            if (m_EditControlsButton != null)
                m_EditControlsButton.gameObject.SetActive(MobileControlsLayout.Instance != null);
            Highlight(SettingsService.Quality);
            if (m_Target <= 0f)
                AudioManager.PlaySfx(Sound.UiOpen);
            m_Target = 1f;
            m_Group.interactable = m_Group.blocksRaycasts = true;
            GamePause.Set(GamePause.Reason.Settings, true);        // the game waits while the settings are open
        }

        public void Hide()
        {
            // A new size of the touch controls is kept with the progress (it survives a cleared browser storage).
            if (m_Target > 0f && !Mathf.Approximately(m_ScaleAtShow, SettingsService.ControlsScale))
                MetaProgress.ControlsScale = SettingsService.ControlsScale;
            if (m_Target > 0f)
                AudioManager.PlaySfx(Sound.UiClose);
            m_Target = 0f;
            if (m_Group != null)
                m_Group.interactable = m_Group.blocksRaycasts = false;
            GamePause.Set(GamePause.Reason.Settings, false);
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

        /// <summary>Turns the shake, hit stops and vibration on or off (button, tests).</summary>
        public void ToggleShake()
        {
            SettingsService.SetShakeEnabled(!SettingsService.ShakeEnabled);
            AudioManager.PlaySfx(Sound.UiClick);
            ShowShake();
        }

        private void ShowShake()
        {
            if (m_ShakeLabel != null)
                m_ShakeLabel.SetText(SettingsService.ShakeEnabled ? "ВКЛ" : "ВЫКЛ");
            if (m_ShakeButton != null && m_ShakeButton.targetGraphic != null)
                m_ShakeButton.targetGraphic.color = SettingsService.ShakeEnabled ? m_SelectedColor : m_NormalColor;
        }

        private void EditControls()
        {
            Hide();
            Snap();
            if (MobileControlsLayout.Instance != null)
                MobileControlsLayout.Instance.BeginEdit();
        }

        private void ReplayTutorial()
        {
            if (m_Tutorial == null)
                return;
            Hide();
            Snap();
            MetaProgress.LiveHintsDone = false;                    // the live hints come again (now or in the next run)
            m_Tutorial.OpenCards();
        }

        private void ToMenu()
        {
            m_MenuButton.interactable = false;
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMainMenu());
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
