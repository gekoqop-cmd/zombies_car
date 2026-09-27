using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Loads, applies and saves user settings (quality tier; master, music and effects volume).
    /// Loaded once by the Boot scene; scenes started directly in the Editor call <see cref="EnsureLoaded"/>.
    /// </summary>
    public static class SettingsService
    {
        private const string c_QualityKey = "settings.quality";
        private const string c_VolumeKey = "settings.volume";
        private const string c_MusicVolumeKey = "settings.volume.music";
        private const string c_SfxVolumeKey = "settings.volume.sfx";
        private const QualityTier c_FallbackDesktopQuality = QualityTier.Medium;
        private const QualityTier c_FallbackMobileQuality = QualityTier.Low;
        private const float c_FallbackVolume = 1f;
        // Ultra forces 16x anisotropic filtering on every texture; the other tiers keep Unity's defaults (9..16,
        // used only by textures that ask for anisotropy themselves).
        private const int c_UltraAnisotropy = 16;
        private const int c_DefaultForcedAnisotropy = 9;
        private const int c_MaxAnisotropy = 16;

        private static bool s_Loaded;

        public static event Action<QualityTier> QualityChanged;

        /// <summary>Master volume 0..1 changed (applied to the mixer by <see cref="AudioManager"/>).</summary>
        public static event Action<float> VolumeChanged;

        /// <summary>Any of the three volumes changed (master, music, effects).</summary>
        public static event Action VolumesChanged;

        public static QualityTier Quality { get; private set; } = c_FallbackDesktopQuality;
        public static float MasterVolume { get; private set; } = c_FallbackVolume;
        public static float MusicVolume { get; private set; } = c_FallbackVolume;
        public static float SfxVolume { get; private set; } = c_FallbackVolume;

        public static void Load(GameSettings defaults)
        {
            QualityTier defaultTier = Application.isMobilePlatform
                ? (defaults != null ? defaults.MobileDefaultQuality : c_FallbackMobileQuality)
                : (defaults != null ? defaults.DesktopDefaultQuality : c_FallbackDesktopQuality);
            float defaultVolume = defaults != null ? defaults.DefaultMasterVolume : c_FallbackVolume;

            int maxTier = Mathf.Max(0, QualitySettings.names.Length - 1);
            Quality = Allowed((QualityTier)Mathf.Clamp(PlayerPrefs.GetInt(c_QualityKey, (int)defaultTier), 0, maxTier));
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(c_VolumeKey, defaultVolume));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(c_MusicVolumeKey, c_FallbackVolume));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(c_SfxVolumeKey, c_FallbackVolume));

            // Let the browser drive the frame pacing on the Web (requestAnimationFrame).
            Application.targetFrameRate = -1;
            ApplyQuality(Quality);
            s_Loaded = true;
            VolumeChanged?.Invoke(MasterVolume);
            VolumesChanged?.Invoke();
            QualityChanged?.Invoke(Quality);   // components enabled before the load pick up the stored tier
        }

        public static void EnsureLoaded()
        {
            if (!s_Loaded)
                Load(null);
        }

        /// <summary>Ultra is for powerful PCs: it is not offered on mobile and a stored Ultra falls back to High there.</summary>
        public static bool IsUltraAllowed => !Application.isMobilePlatform;

        public static void SetQuality(QualityTier tier)
        {
            tier = Allowed(tier);
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
            PlayerPrefs.SetFloat(c_VolumeKey, MasterVolume);
            PlayerPrefs.Save();
            VolumeChanged?.Invoke(MasterVolume);
            VolumesChanged?.Invoke();
        }

        public static void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(c_MusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
            VolumesChanged?.Invoke();
        }

        public static void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(c_SfxVolumeKey, SfxVolume);
            PlayerPrefs.Save();
            VolumesChanged?.Invoke();
        }

        /// <summary>
        /// Switches the Unity quality level (URP asset, shadows, textures) and the global texture filtering.
        /// Public so editor tools can preview a tier without Play mode.
        /// </summary>
        public static void ApplyQuality(QualityTier tier)
        {
            if ((int)tier < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel((int)tier, true);
            Texture.SetGlobalAnisotropicFilteringLimits(tier == QualityTier.Ultra ? c_UltraAnisotropy : c_DefaultForcedAnisotropy,
                c_MaxAnisotropy);
        }

        private static QualityTier Allowed(QualityTier tier) =>
            tier == QualityTier.Ultra && !IsUltraAllowed ? QualityTier.High : tier;
    }
}
