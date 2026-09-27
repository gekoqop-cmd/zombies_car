using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// Main menu: background with logo and the Play / Settings buttons. Play darkens the background and brings in the
    /// map selection (<see cref="MapSelectScreen"/>, which loads the chosen map); its Back button returns. Settings
    /// opens the shared <see cref="SettingsPanel"/> pop-up. No Exit button (not allowed on Yandex Games).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button m_PlayButton;
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private Button m_BackButton;
        [SerializeField] private CanvasGroup m_MainScreen;
        [SerializeField] private CanvasGroup m_MapScreen;
        [Tooltip("Black overlay over the background; its alpha rises while the map selection is open.")]
        [SerializeField] private CanvasGroup m_Dim;
        [SerializeField, Range(0f, 1f)] private float m_DimAlpha = 0.65f;
        [SerializeField] private SettingsPanel m_Settings;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.3f;

        private bool m_MapOpen;

        public bool IsMapSelectionOpen => m_MapOpen;
        public SettingsPanel Settings => m_Settings;

        private void Awake()
        {
            SettingsService.EnsureLoaded();
            m_PlayButton.onClick.AddListener(() => ShowMapSelection(true));
            m_SettingsButton.onClick.AddListener(() => m_Settings.Show());
            if (m_BackButton != null)
                m_BackButton.onClick.AddListener(() => ShowMapSelection(false));
            Apply(1f, true);
        }

        // The menu is the first screen the player can use: the game is loaded for the platform.
        private void Start() => YandexSdk.NotifyGameReady();

        public void ShowMapSelection(bool open)
        {
            m_MapOpen = open;
            m_MapScreen.gameObject.SetActive(true);
        }

        /// <summary>Jumps to the current state without the fade (tools, screenshots).</summary>
        public void Snap() => Apply(1f, true);

        private void Update() => Apply(Time.unscaledDeltaTime / m_FadeTime, false);

        private void Apply(float step, bool instant)
        {
            float map = m_MapOpen ? 1f : 0f;
            float Move(float from, float to) => instant ? to : Mathf.MoveTowards(from, to, step);
            m_MapScreen.alpha = Move(m_MapScreen.alpha, map);
            m_MainScreen.alpha = Move(m_MainScreen.alpha, 1f - map);
            m_Dim.alpha = Move(m_Dim.alpha, map * m_DimAlpha);
            m_MapScreen.interactable = m_MapScreen.blocksRaycasts = m_MapOpen;
            m_MainScreen.interactable = m_MainScreen.blocksRaycasts = !m_MapOpen;
            if (!m_MapOpen && m_MapScreen.alpha <= 0f)
                m_MapScreen.gameObject.SetActive(false);
        }
    }
}
