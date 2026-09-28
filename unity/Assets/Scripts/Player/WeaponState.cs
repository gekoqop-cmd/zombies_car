using System;
using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>The weapon in hand, shared with the HUD scene (weapon slots, the touch weapon button).</summary>
    [CreateAssetMenu(fileName = "WeaponState", menuName = "ZombiCar/State/Weapon State")]
    public sealed class WeaponState : ScriptableObject
    {
        [NonSerialized] private WeaponSlot m_Current;
        [NonSerialized] private bool m_Automatic;

        public event Action Changed;

        public WeaponSlot Current => m_Current;

        /// <summary>The axe came out by itself because the gun ran dry.</summary>
        public bool Automatic => m_Automatic;

        public void Set(WeaponSlot slot, bool automatic)
        {
            m_Current = slot;
            m_Automatic = automatic;
            Changed?.Invoke();
        }
    }
}
