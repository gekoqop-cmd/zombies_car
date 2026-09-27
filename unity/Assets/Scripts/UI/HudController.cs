using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// HUD (UI_HUD scene): health, level, wave + timer, ammo with the reload progress and the settings button.
    /// Listens to the shared state assets, so it works over any map scene without direct references; every number
    /// on it comes from them (the scene builder fills the saved texts with <see cref="Refresh"/> too).
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private const int c_SecondsPerMinute = 60;

        [Header("State")]
        [SerializeField] private PlayerStats m_PlayerStats;
        [SerializeField] private WaveState m_WaveState;
        [SerializeField] private AmmoState m_AmmoState;

        [Header("Health / level")]
        [SerializeField] private Image m_HealthFill;
        [SerializeField] private TMP_Text m_HealthValue;
        [SerializeField] private Image m_LevelFill;
        [SerializeField] private TMP_Text m_LevelLabel;

        [Header("Wave")]
        [SerializeField] private TMP_Text m_WaveLabel;
        [SerializeField] private TMP_Text m_WaveTimer;

        [Header("Ammo")]
        [SerializeField] private TMP_Text m_AmmoValue;
        [SerializeField] private GameObject m_ReloadHint;
        [Tooltip("Filled image of the reload bar; advances every frame while reloading.")]
        [SerializeField] private Image m_ReloadFill;

        [Header("Settings")]
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private SettingsPanel m_SettingsPanel;

        [Header("Texts")]
        [SerializeField] private string m_LevelFormat = "УРОВЕНЬ {0}";
        [SerializeField] private string m_WaveFormat = "ВОЛНА {0}";
        [SerializeField] private string m_ValueFormat = "{0} / {1}";
        [SerializeField] private string m_TimerFormat = "{0:00}:{1:00}";

        private void OnEnable()
        {
            if (m_PlayerStats != null) m_PlayerStats.Changed += RefreshPlayer;
            if (m_WaveState != null) m_WaveState.Changed += RefreshWave;
            if (m_AmmoState != null) m_AmmoState.Changed += RefreshAmmo;
            if (m_SettingsButton != null) m_SettingsButton.onClick.AddListener(OpenSettings);
            Refresh();
        }

        /// <summary>Shows the current values of all state assets.</summary>
        public void Refresh()
        {
            RefreshPlayer();
            RefreshWave();
            RefreshAmmo();
        }

        private void Update()
        {
            if (m_ReloadFill != null && m_AmmoState != null && m_AmmoState.IsReloading)
                m_ReloadFill.fillAmount = m_AmmoState.ReloadProgress;
        }

        private void OnDisable()
        {
            if (m_PlayerStats != null) m_PlayerStats.Changed -= RefreshPlayer;
            if (m_WaveState != null) m_WaveState.Changed -= RefreshWave;
            if (m_AmmoState != null) m_AmmoState.Changed -= RefreshAmmo;
            if (m_SettingsButton != null) m_SettingsButton.onClick.RemoveListener(OpenSettings);
        }

        private void RefreshPlayer()
        {
            if (m_PlayerStats == null)
                return;
            m_HealthFill.fillAmount = (float)m_PlayerStats.Health / m_PlayerStats.MaxHealth;
            m_HealthValue.SetText(string.Format(m_ValueFormat, m_PlayerStats.Health, m_PlayerStats.MaxHealth));
            m_LevelFill.fillAmount = m_PlayerStats.LevelProgress;
            m_LevelLabel.SetText(string.Format(m_LevelFormat, m_PlayerStats.Level));
        }

        private void RefreshWave()
        {
            if (m_WaveState == null)
                return;
            m_WaveLabel.SetText(string.Format(m_WaveFormat, m_WaveState.Wave));
            int seconds = m_WaveState.SecondsLeft;
            m_WaveTimer.SetText(string.Format(m_TimerFormat, seconds / c_SecondsPerMinute, seconds % c_SecondsPerMinute));
        }

        private void RefreshAmmo()
        {
            if (m_AmmoState == null)
                return;
            m_AmmoValue.SetText(string.Format(m_ValueFormat, m_AmmoState.InMagazine, m_AmmoState.Reserve));
            if (m_ReloadHint != null)
                m_ReloadHint.SetActive(m_AmmoState.IsReloading);
            if (m_ReloadFill != null)
                m_ReloadFill.fillAmount = m_AmmoState.ReloadProgress;
        }

        private void OpenSettings()
        {
            if (m_SettingsPanel != null)
                m_SettingsPanel.Show();
        }
    }
}
