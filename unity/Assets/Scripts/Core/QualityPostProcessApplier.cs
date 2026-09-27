using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombiCar.Core
{
    /// <summary>
    /// Scales post-processing with the quality tier: Low = no post-processing, Medium = bloom/vignette/colour,
    /// High = everything + depth of field and SMAA, Ultra = High + the Ultra volume (high-quality bloom, bokeh depth
    /// of field, lift/gamma/gain grade, white balance, light chromatic aberration and film grain).
    /// </summary>
    public sealed class QualityPostProcessApplier : MonoBehaviour, IQualityTierListener
    {
        [SerializeField] private Volume m_Volume;
        [SerializeField] private UniversalAdditionalCameraData m_CameraData;
        [Tooltip("Extra global volume with a higher priority, active only on the Ultra tier.")]
        [SerializeField] private Volume m_UltraVolume;

        private DepthOfField m_DepthOfField;
        private bool m_Resolved;

        private void Awake() => Resolve();

        private void OnEnable()
        {
            SettingsService.QualityChanged += ApplyQualityTier;
            ApplyQualityTier(SettingsService.Quality);
        }

        private void OnDisable() => SettingsService.QualityChanged -= ApplyQualityTier;

        public void ApplyQualityTier(QualityTier tier)
        {
            Resolve();
            if (m_CameraData != null)
            {
                m_CameraData.renderPostProcessing = tier != QualityTier.Low;
                m_CameraData.antialiasing = tier >= QualityTier.High
                    ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                    : tier == QualityTier.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }
            if (m_DepthOfField != null)
                m_DepthOfField.active = tier >= QualityTier.High;
            if (m_UltraVolume != null)
                m_UltraVolume.gameObject.SetActive(tier == QualityTier.Ultra);
        }

        private void Resolve()
        {
            if (m_Resolved)
                return;
            m_Resolved = true;
            // Volume.profile returns a per-scene copy, so toggling it never edits the shared asset.
            if (m_Volume != null)
                m_Volume.profile.TryGet(out m_DepthOfField);
        }
    }
}
