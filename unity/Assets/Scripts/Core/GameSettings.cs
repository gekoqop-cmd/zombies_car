using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>Default user settings; the saved values (PlayerPrefs) override them.</summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "ZombiCar/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [SerializeField] private QualityTier m_DesktopDefaultQuality = QualityTier.Medium;
        [SerializeField] private QualityTier m_MobileDefaultQuality = QualityTier.Low;
        [SerializeField, Range(0f, 1f)] private float m_DefaultMasterVolume = 1f;

        public QualityTier DesktopDefaultQuality => m_DesktopDefaultQuality;
        public QualityTier MobileDefaultQuality => m_MobileDefaultQuality;
        public float DefaultMasterVolume => m_DefaultMasterVolume;
    }
}
