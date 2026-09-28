using System;
using UnityEngine;

namespace ZombiCar.Car
{
    /// <summary>
    /// The car's durability shared with the HUD scene (workshop status, the "car under attack" warning) without
    /// cross-scene references. Written by <see cref="CarHealth"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "CarState", menuName = "ZombiCar/State/Car State")]
    public sealed class CarState : ScriptableObject
    {
        [NonSerialized] private int m_Health;
        [NonSerialized] private int m_MaxHealth;
        [NonSerialized] private float m_Armor;
        [NonSerialized] private int m_Spikes;

        /// <summary>Durability or a car stat changed.</summary>
        public event Action Changed;

        /// <summary>A zombie (or boss) hit the car: the damage taken.</summary>
        public event Action<int> Attacked;

        public int Health => m_Health;
        public int MaxHealth => m_MaxHealth;
        public float Armor => m_Armor;
        public int Spikes => m_Spikes;

        public void Set(int health, int maxHealth, float armor, int spikes)
        {
            if (health == m_Health && maxHealth == m_MaxHealth && Mathf.Approximately(armor, m_Armor) && spikes == m_Spikes)
                return;
            m_Health = health;
            m_MaxHealth = maxHealth;
            m_Armor = armor;
            m_Spikes = spikes;
            Changed?.Invoke();
        }

        public void RaiseAttacked(int damage) => Attacked?.Invoke(damage);
    }
}
