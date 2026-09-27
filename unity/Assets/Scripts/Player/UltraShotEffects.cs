using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Ultra tier only: a burst of sparks from the muzzle on every shot and sparks where a bullet hits.
    /// The particle systems are pre-built (no emission of their own) and simply moved to the event and emitted.
    /// </summary>
    public sealed class UltraShotEffects : MonoBehaviour, IQualityTierListener
    {
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private ParticleSystem m_MuzzleSparks;
        [SerializeField] private ParticleSystem m_ImpactSparks;
        [SerializeField, Min(0)] private int m_MuzzleSparkCount = 10;
        [SerializeField, Min(0)] private int m_ImpactSparkCount = 14;

        private bool m_Enabled;

        private void OnEnable()
        {
            SettingsService.QualityChanged += ApplyQualityTier;
            ApplyQualityTier(SettingsService.Quality);
        }

        private void OnDisable()
        {
            SettingsService.QualityChanged -= ApplyQualityTier;
            SetListening(false);
        }

        public void ApplyQualityTier(QualityTier tier) => SetListening(tier >= QualityTier.Ultra && isActiveAndEnabled);

        private void SetListening(bool on)
        {
            if (on == m_Enabled)
                return;
            m_Enabled = on;
            if (on)
            {
                if (m_Weapon != null)
                    m_Weapon.Fired += OnFired;
                Bullet.Impacted += OnImpact;
            }
            else
            {
                if (m_Weapon != null)
                    m_Weapon.Fired -= OnFired;
                Bullet.Impacted -= OnImpact;
            }
        }

        private void OnFired()
        {
            if (m_MuzzleSparks == null || m_Weapon.Muzzle == null)
                return;
            m_MuzzleSparks.transform.SetPositionAndRotation(m_Weapon.Muzzle.position,
                Quaternion.LookRotation(m_Weapon.ShotDirection, Vector3.up));
            m_MuzzleSparks.Emit(m_MuzzleSparkCount);
        }

        private void OnImpact(Vector3 point, Vector3 normal)
        {
            if (m_ImpactSparks == null)
                return;
            m_ImpactSparks.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal.sqrMagnitude > 0f ? normal : Vector3.up));
            m_ImpactSparks.Emit(m_ImpactSparkCount);
        }
    }
}
