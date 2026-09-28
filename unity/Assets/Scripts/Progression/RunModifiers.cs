using System;

namespace ZombiCar.Progression
{
    /// <summary>The sum of every stat effect taken this run (level-up cards + workshop upgrades), one value per effect.</summary>
    public sealed class RunModifiers
    {
        private static readonly int s_Count = Enum.GetValues(typeof(StatEffect)).Length;

        private readonly float[] m_Values = new float[s_Count];

        public float this[StatEffect effect] => m_Values[(int)effect];

        public void Add(StatEffect effect, float value) => m_Values[(int)effect] += value;

        public void Clear() => Array.Clear(m_Values, 0, m_Values.Length);

        /// <summary>1 + the effect (for fraction effects: damage, fire rate, ...).</summary>
        public float Multiplier(StatEffect effect) => 1f + m_Values[(int)effect];

        /// <summary>The effect rounded to whole units (rounds, health points, pierce).</summary>
        public int Whole(StatEffect effect) => (int)Math.Round(m_Values[(int)effect]);
    }
}
