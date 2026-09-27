using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Bullet impact feedback on every tier: sparks and a ricochet sound where a bullet hits scenery or the car
    /// (hits on zombies have their own flash). The Ultra tier doubles the impact sparks and adds a burst of sparks
    /// from the muzzle on every shot. The particle systems are pre-built with no emission of their own: they are
    /// moved to the event and emitted.
    /// </summary>
    public sealed class ShotEffects : MonoBehaviour, IQualityTierListener
    {
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private ParticleSystem m_MuzzleSparks;
        [SerializeField] private ParticleSystem m_ImpactSparks;
        [SerializeField] private AudioClip m_ImpactSound;
        [SerializeField, Range(0f, 1f)] private float m_ImpactVolume = 0.55f;
        [SerializeField, Min(0)] private int m_MuzzleSparkCount = 10;
        [Tooltip("Sparks per impact below Ultra; Ultra emits twice as many.")]
        [SerializeField, Min(0)] private int m_ImpactSparkCount = 8;
        [SerializeField, Range(0f, 0.5f)] private float m_ImpactPitchJitter = 0.12f;

        private bool m_Ultra;

        private void OnEnable()
        {
            SettingsService.QualityChanged += ApplyQualityTier;
            ApplyQualityTier(SettingsService.Quality);
            Bullet.Impacted += OnImpact;
            if (m_Weapon != null)
                m_Weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            SettingsService.QualityChanged -= ApplyQualityTier;
            Bullet.Impacted -= OnImpact;
            if (m_Weapon != null)
                m_Weapon.Fired -= OnFired;
        }

        public void ApplyQualityTier(QualityTier tier) => m_Ultra = tier >= QualityTier.Ultra;

        private void OnFired()
        {
            if (!m_Ultra || m_MuzzleSparks == null || m_Weapon.Muzzle == null)
                return;
            m_MuzzleSparks.transform.SetPositionAndRotation(m_Weapon.Muzzle.position,
                Quaternion.LookRotation(m_Weapon.ShotDirection, Vector3.up));
            m_MuzzleSparks.Emit(m_MuzzleSparkCount);
        }

        private void OnImpact(Vector3 point, Vector3 normal, bool enemy)
        {
            if (enemy)
                return;
            if (m_ImpactSparks != null)
            {
                m_ImpactSparks.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal.sqrMagnitude > 0f ? normal : Vector3.up));
                m_ImpactSparks.Emit(m_Ultra ? m_ImpactSparkCount * 2 : m_ImpactSparkCount);
            }
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(m_ImpactSound, m_ImpactVolume, m_ImpactPitchJitter);
        }
    }
}
