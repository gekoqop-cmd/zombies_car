using System;
using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Runtime ammo state shared between the weapon (map scene) and the HUD (UI_HUD scene) without cross-scene
    /// references. The weapon writes it, the HUD listens to <see cref="Changed"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "AmmoState", menuName = "ZombiCar/State/Ammo State")]
    public sealed class AmmoState : ScriptableObject
    {
        [NonSerialized] private int m_InMagazine;
        [NonSerialized] private int m_Reserve;
        [NonSerialized] private bool m_IsReloading;
        [NonSerialized] private float m_ReloadStartTime;
        [NonSerialized] private float m_ReloadDuration;

        public event Action Changed;

        public int InMagazine => m_InMagazine;
        public int Reserve => m_Reserve;
        public bool IsReloading => m_IsReloading;

        /// <summary>Reload progress 0..1 (1 when not reloading); read every frame by the HUD while reloading.</summary>
        public float ReloadProgress => m_IsReloading && m_ReloadDuration > 0f
            ? Mathf.Clamp01((Time.time - m_ReloadStartTime) / m_ReloadDuration)
            : 1f;

        public void Set(int inMagazine, int reserve, bool isReloading, float reloadDuration = 0f)
        {
            if (inMagazine == m_InMagazine && reserve == m_Reserve && isReloading == m_IsReloading)
                return;
            if (isReloading && !m_IsReloading)
            {
                m_ReloadStartTime = Time.time;
                m_ReloadDuration = reloadDuration;
            }
            m_InMagazine = inMagazine;
            m_Reserve = reserve;
            m_IsReloading = isReloading;
            Changed?.Invoke();
        }
    }
}
