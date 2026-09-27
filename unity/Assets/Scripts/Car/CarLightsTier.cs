using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Car
{
    /// <summary>
    /// Real lights of the player's car (night map: low-beam headlights and a warm cabin light) scaled with the quality
    /// tier: Low keeps only the emissive headlight glass (no light sources), Medium the headlights without shadows,
    /// High adds the cabin light, Ultra gives the headlights soft shadows.
    /// </summary>
    public sealed class CarLightsTier : MonoBehaviour, IQualityTierListener
    {
        [SerializeField] private Light[] m_Headlights = System.Array.Empty<Light>();
        [SerializeField] private Light m_Cabin;

        private void OnEnable()
        {
            SettingsService.QualityChanged += ApplyQualityTier;
            ApplyQualityTier(SettingsService.Quality);
        }

        private void OnDisable() => SettingsService.QualityChanged -= ApplyQualityTier;

        public void ApplyQualityTier(QualityTier tier)
        {
            foreach (Light headlight in m_Headlights)
            {
                if (headlight == null)
                    continue;
                headlight.enabled = tier >= QualityTier.Medium;
                headlight.shadows = tier >= QualityTier.Ultra ? LightShadows.Soft : LightShadows.None;
            }
            if (m_Cabin != null)
                m_Cabin.enabled = tier >= QualityTier.High;
        }
    }
}
