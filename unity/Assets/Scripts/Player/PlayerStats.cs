using System;
using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Player health / level / experience shown by the HUD (health written by <see cref="PlayerHealth"/>, level and
    /// experience by <c>PlayerProgression</c>). The start values below are the defaults a map begins with:
    /// <see cref="ResetToDefaults"/> is called when a map starts. Level-up cards can raise the maximum health
    /// (<see cref="SetBonusMaxHealth"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "ZombiCar/State/Player Stats")]
    public sealed class PlayerStats : ScriptableObject
    {
        [SerializeField, Min(1)] private int m_MaxHealth = 100;
        [Tooltip("Health at the start of a map; the player starts at full health.")]
        [SerializeField, Min(0)] private int m_StartHealth = 100;
        [SerializeField, Min(1)] private int m_StartLevel = 1;
        [Tooltip("Experience towards the next level at the start, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float m_StartLevelProgress = 0f;

        [NonSerialized] private int m_Health;
        [NonSerialized] private int m_BonusMaxHealth;
        [NonSerialized] private int m_Level;
        [NonSerialized] private float m_LevelProgress;
        [NonSerialized] private int m_Xp;
        [NonSerialized] private int m_XpToNext;

        public event Action Changed;

        public int Health => m_Health;
        public int MaxHealth => m_MaxHealth + m_BonusMaxHealth;
        public int Level => m_Level;
        public float LevelProgress => m_LevelProgress;
        public int Xp => m_Xp;
        public int XpToNext => m_XpToNext;
        public int StartLevel => m_StartLevel;

        public void ResetToDefaults()
        {
            m_BonusMaxHealth = 0;
            m_Health = Mathf.Min(m_StartHealth, MaxHealth);
            m_Level = m_StartLevel;
            m_LevelProgress = m_StartLevelProgress;
            m_Xp = 0;
            m_XpToNext = 0;
            Changed?.Invoke();
        }

        public void SetHealth(int health)
        {
            m_Health = Mathf.Clamp(health, 0, MaxHealth);
            Changed?.Invoke();
        }

        /// <summary>Extra maximum health (cards); the health itself is not changed here.</summary>
        public void SetBonusMaxHealth(int bonus)
        {
            m_BonusMaxHealth = Mathf.Max(0, bonus);
            m_Health = Mathf.Min(m_Health, MaxHealth);
            Changed?.Invoke();
        }

        /// <summary>Level and the experience collected towards the next one.</summary>
        public void SetProgress(int level, int xp, int xpToNext)
        {
            m_Level = level;
            m_Xp = xp;
            m_XpToNext = xpToNext;
            m_LevelProgress = xpToNext > 0 ? Mathf.Clamp01((float)xp / xpToNext) : 0f;
            Changed?.Invoke();
        }
    }
}
