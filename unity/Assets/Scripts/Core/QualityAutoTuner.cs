using System.Collections;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// First run on a phone (no tier chosen yet): plays the first map on Medium, measures the frame rate for a few
    /// seconds and keeps Medium or drops to Low. The result is stored like a player's choice, so it runs only once;
    /// the player can still pick any tier (Ultra included) in the settings.
    /// </summary>
    public sealed class QualityAutoTuner : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float m_Warmup = 2.5f;
        [SerializeField, Min(0.5f)] private float m_Measure = 4f;
        [Tooltip("Below this average frame rate the phone gets Low.")]
        [SerializeField, Min(1f)] private float m_MinFps = 30f;

        public static bool IsNeeded => DeviceInfo.IsMobile && !SettingsService.HasStoredQuality;

        private IEnumerator Start()
        {
            if (!IsNeeded)
            {
                Destroy(this);
                yield break;
            }
            SettingsService.SetQuality(QualityTier.Medium);
            yield return new WaitForSecondsRealtime(m_Warmup);
            int frames = 0;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < m_Measure)
            {
                frames++;
                yield return null;
            }
            float fps = frames / (Time.unscaledTime - start);
            QualityTier tier = fps < m_MinFps ? QualityTier.Low : QualityTier.Medium;
            SettingsService.SetQuality(tier);
            Debug.Log($"[QualityAutoTuner] {fps:F0} fps on Medium -> {tier}.");
            Destroy(this);
        }
    }
}
