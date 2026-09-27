using System;
using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Player health / level shown by the HUD (written by <see cref="PlayerHealth"/>). The start values below are the
    /// defaults a map begins with: <see cref="ResetToDefaults"/> is called when a map starts.
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
        [NonSerialized] private int m_Level;
        [NonSerialized] private float m_LevelProgress;

        public event Action Changed;

        public int Health => m_Health;
        public int MaxHealth => m_MaxHealth;
        public int Level => m_Level;
        public float LevelProgress => m_LevelProgress;

        public void ResetToDefaults()
        {
            m_Health = Mathf.Min(m_StartHealth, m_MaxHealth);
            m_Level = m_StartLevel;
            m_LevelProgress = m_StartLevelProgress;
            Changed?.Invoke();
        }

        public void SetHealth(int health)
        {
            m_Health = Mathf.Clamp(health, 0, m_MaxHealth);
            Changed?.Invoke();
        }
    }
}
