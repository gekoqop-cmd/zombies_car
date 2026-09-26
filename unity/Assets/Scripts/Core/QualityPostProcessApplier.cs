using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombiCar.Core
{
    /// <summary>
    /// Scales post-processing with the quality tier: Low = no post-processing, Medium = bloom/vignette/colour,
    /// High = everything + depth of field and SMAA.
    /// </summary>
    public sealed class QualityPostProcessApplier : MonoBehaviour
    {
        [SerializeField] private Volume m_Volume;
        [SerializeField] private UniversalAdditionalCameraData m_CameraData;

        private DepthOfField m_DepthOfField;

        private void Awake()
        {
            // Volume.profile returns a per-scene copy, so toggling it never edits the shared asset.
            if (m_Volume != null)
                m_Volume.profile.TryGet(out m_DepthOfField);
        }

        private void OnEnable()
        {
            SettingsService.QualityChanged += Apply;
            Apply(SettingsService.Quality);
        }

        private void OnDisable() => SettingsService.QualityChanged -= Apply;

        private void Apply(QualityTier tier)
        {
            if (m_CameraData != null)
            {
                m_CameraData.renderPostProcessing = tier != QualityTier.Low;
                m_CameraData.antialiasing = tier == QualityTier.High
                    ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                    : tier == QualityTier.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }
            if (m_DepthOfField != null)
                m_DepthOfField.active = tier == QualityTier.High;
        }
    }
}
