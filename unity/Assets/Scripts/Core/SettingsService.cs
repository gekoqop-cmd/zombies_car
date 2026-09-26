using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Loads, applies and saves user settings (quality tier, master volume).
    /// Loaded once by the Boot scene; scenes started directly in the Editor call <see cref="EnsureLoaded"/>.
    /// </summary>
    public static class SettingsService
    {
        private const string c_QualityKey = "settings.quality";
        private const string c_VolumeKey = "settings.volume";
        private const QualityTier c_FallbackDesktopQuality = QualityTier.Medium;
        private const QualityTier c_FallbackMobileQuality = QualityTier.Low;
        private const float c_FallbackVolume = 1f;

        private static bool s_Loaded;

        public static event Action<QualityTier> QualityChanged;

        public static QualityTier Quality { get; private set; } = c_FallbackDesktopQuality;
        public static float MasterVolume { get; private set; } = c_FallbackVolume;

        public static void Load(GameSettings defaults)
        {
            QualityTier defaultTier = Application.isMobilePlatform
                ? (defaults != null ? defaults.MobileDefaultQuality : c_FallbackMobileQuality)
                : (defaults != null ? defaults.DesktopDefaultQuality : c_FallbackDesktopQuality);
            float defaultVolume = defaults != null ? defaults.DefaultMasterVolume : c_FallbackVolume;

            int maxTier = Mathf.Max(0, QualitySettings.names.Length - 1);
            Quality = (QualityTier)Mathf.Clamp(PlayerPrefs.GetInt(c_QualityKey, (int)defaultTier), 0, maxTier);
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(c_VolumeKey, defaultVolume));

            // Let the browser drive the frame pacing on the Web (requestAnimationFrame).
            Application.targetFrameRate = -1;
            ApplyQuality(Quality);
            AudioListener.volume = MasterVolume;
            s_Loaded = true;
            QualityChanged?.Invoke(Quality);   // components enabled before the load pick up the stored tier
        }

        public static void EnsureLoaded()
        {
            if (!s_Loaded)
                Load(null);
        }

        public static void SetQuality(QualityTier tier)
        {
            if (s_Loaded && tier == Quality)
                return;
            Quality = tier;
            PlayerPrefs.SetInt(c_QualityKey, (int)tier);
            PlayerPrefs.Save();
            ApplyQuality(tier);
            QualityChanged?.Invoke(tier);
        }

        public static void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(c_VolumeKey, MasterVolume);
            PlayerPrefs.Save();
        }

        private static void ApplyQuality(QualityTier tier)
        {
            if ((int)tier < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel((int)tier, true);
        }
    }
}
