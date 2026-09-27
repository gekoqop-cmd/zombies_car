using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Keeps extra scene content (Ultra lights, denser foliage, reflection probe, ambient dust) switched off below
    /// <see cref="m_MinTier"/>, so the lower tiers render exactly as before.
    /// </summary>
    public sealed class QualityTierGate : MonoBehaviour, IQualityTierListener
    {
        [SerializeField] private QualityTier m_MinTier = QualityTier.Ultra;
        [SerializeField] private GameObject[] m_Targets = System.Array.Empty<GameObject>();

        private void OnEnable()
        {
            SettingsService.QualityChanged += ApplyQualityTier;
            ApplyQualityTier(SettingsService.Quality);
        }

        private void OnDisable() => SettingsService.QualityChanged -= ApplyQualityTier;

        public void ApplyQualityTier(QualityTier tier)
        {
            bool active = tier >= m_MinTier;
            foreach (GameObject target in m_Targets)
            {
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
            }
        }
    }
}
