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

        public event Action Changed;

        public int InMagazine => m_InMagazine;
        public int Reserve => m_Reserve;
        public bool IsReloading => m_IsReloading;

        public void Set(int inMagazine, int reserve, bool isReloading)
        {
            if (inMagazine == m_InMagazine && reserve == m_Reserve && isReloading == m_IsReloading)
                return;
            m_InMagazine = inMagazine;
            m_Reserve = reserve;
            m_IsReloading = isReloading;
            Changed?.Invoke();
        }
    }
}
