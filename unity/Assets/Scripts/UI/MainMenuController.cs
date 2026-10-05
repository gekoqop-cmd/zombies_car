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
        [Tooltip("Cases of rifle / axe / bullet skins (optional).")]
        [SerializeField] private Button m_CasesButton;
        [SerializeField] private CasesScreen m_Cases;
        [Tooltip("Coins the player has, next to the cases button.")]
        [SerializeField] private TMPro.TMP_Text m_CoinsLabel;
        [Tooltip("\"ТАБЛИЦА ЛИДЕРОВ\": the Yandex Games best-time table.")]
        [SerializeField] private Button m_LeaderboardButton;
        [SerializeField] private LeaderboardPanel m_Leaderboard;
        [Tooltip("\"СКИНЫ\": the characters and the skin collections.")]
        [SerializeField] private Button m_SkinsButton;
        [SerializeField] private SkinsPanel m_Skins;
        [Tooltip("\"ДОСТИЖЕНИЯ\": the achievements and their rewards; the mark shows when one waits to be taken.")]
        [SerializeField, OptionalReference] private Button m_AchievementsButton;
        [SerializeField, OptionalReference] private AchievementsPanel m_Achievements;
        [SerializeField, OptionalReference] private GameObject m_AchievementsMark;
        [Tooltip("\"ЕЖЕДНЕВНЫЕ БОНУСЫ\": the 7-day row; the mark shows while today's bonus waits.")]
        [SerializeField, OptionalReference] private Button m_DailyButton;
        [SerializeField, OptionalReference] private DailyBonusPanel m_Daily;
        [SerializeField, OptionalReference] private GameObject m_DailyMark;
        [Tooltip("\"!\" on КЕЙСЫ while a free case (the daily bonus) waits.")]
        [SerializeField, OptionalReference] private GameObject m_CasesMark;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.3f;
        [Tooltip("Calm menu track (cross-fades from the map's music when coming back).")]
        [SerializeField] private AudioClip m_Music;

        private bool m_MapOpen;
        private float m_MarksAt;

        public bool IsMapSelectionOpen => m_MapOpen;
        public SettingsPanel Settings => m_Settings;
        public CasesScreen Cases => m_Cases;
        public LeaderboardPanel Leaderboard => m_Leaderboard;
        public SkinsPanel Skins => m_Skins;
        public AchievementsPanel Achievements => m_Achievements;
        public DailyBonusPanel Daily => m_Daily;

        private void Awake()
        {
            SettingsService.EnsureLoaded();
            m_PlayButton.onClick.AddListener(() => ShowMapSelection(true));
            m_SettingsButton.onClick.AddListener(() => m_Settings.Show());
            if (m_BackButton != null)
                m_BackButton.onClick.AddListener(() => ShowMapSelection(false));
            if (m_CasesButton != null && m_Cases != null)
                m_CasesButton.onClick.AddListener(m_Cases.Show);
            m_LeaderboardButton.onClick.AddListener(m_Leaderboard.Open);
            m_SkinsButton.onClick.AddListener(m_Skins.Open);
            if (m_AchievementsButton != null && m_Achievements != null)
                m_AchievementsButton.onClick.AddListener(m_Achievements.Open);
            if (m_DailyButton != null && m_Daily != null)
                m_DailyButton.onClick.AddListener(m_Daily.Open);
            Apply(1f, true);
        }

        // The marks on "ДОСТИЖЕНИЯ", "ЕЖЕДНЕВНЫЕ БОНУСЫ" and "КЕЙСЫ" (checked once a second: the day can turn while the menu is open).
        private void ShowMarks()
        {
            if (m_AchievementsMark != null)
                m_AchievementsMark.SetActive(MetaProgress.HasUnclaimedAchievement);
            if (m_DailyMark != null)
                m_DailyMark.SetActive(MetaProgress.DailyReady);
            if (m_CasesMark != null)
                m_CasesMark.SetActive(MetaProgress.FreeCases > 0);
        }

        private void OnEnable()
        {
            MetaProgress.Changed += ShowCoins;
            ShowCoins();
        }

        private void OnDisable() => MetaProgress.Changed -= ShowCoins;

        private void ShowCoins()
        {
            if (m_CoinsLabel != null)
                m_CoinsLabel.SetText(MetaProgress.Coins.ToString());
        }

        // The menu is the first screen the player can use: the game is loaded for the platform.
        private void Start()
        {
            YandexSdk.NotifyGameReady();
            AudioManager.PlayMusic(m_Music);
            if (MetaProgress.OpenCasesOnMenu && m_Cases != null)
            {
                MetaProgress.OpenCasesOnMenu = false;                     // the victory screen's "К КЕЙСАМ"
                m_Cases.Show();
            }
        }

        public void ShowMapSelection(bool open)
        {
            if (open != m_MapOpen)
                AudioManager.PlaySfx(open ? Sound.UiOpen : Sound.UiClose);
            m_MapOpen = open;
            m_MapScreen.gameObject.SetActive(true);
        }

        /// <summary>Jumps to the current state without the fade (tools, screenshots).</summary>
        public void Snap() => Apply(1f, true);

        private void Update()
        {
            Apply(Time.unscaledDeltaTime / m_FadeTime, false);
            if (Time.unscaledTime >= m_MarksAt)
            {
                m_MarksAt = Time.unscaledTime + 1f;
                ShowMarks();
            }
            // The marks breathe to be seen.
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            if (m_DailyMark != null && m_DailyMark.activeSelf)
                m_DailyMark.transform.localScale = Vector3.one * pulse;
            if (m_CasesMark != null && m_CasesMark.activeSelf)
                m_CasesMark.transform.localScale = Vector3.one * pulse;
            if (m_AchievementsMark != null && m_AchievementsMark.activeSelf)
                m_AchievementsMark.transform.localScale = Vector3.one * pulse;
        }

        private void Apply(float step, bool instant)
        {
            float map = m_MapOpen ? 1f : 0f;
            bool cases = m_Cases != null && m_Cases.IsOpen;              // the cases screen hides the logo and buttons
            float Move(float from, float to) => instant ? to : Mathf.MoveTowards(from, to, step);
            m_MapScreen.alpha = Move(m_MapScreen.alpha, map);
            m_MainScreen.alpha = Move(m_MainScreen.alpha, cases ? 0f : 1f - map);
            m_Dim.alpha = Move(m_Dim.alpha, map * m_DimAlpha);
            m_MapScreen.interactable = m_MapScreen.blocksRaycasts = m_MapOpen;
            m_MainScreen.interactable = m_MainScreen.blocksRaycasts = !m_MapOpen && !cases;
            if (!m_MapOpen && m_MapScreen.alpha <= 0f)
                m_MapScreen.gameObject.SetActive(false);
        }
    }
}
