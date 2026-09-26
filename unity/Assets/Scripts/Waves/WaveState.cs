using System;
using UnityEngine;

namespace ZombiCar.Waves
{
    /// <summary>Runtime wave number and countdown, shared with the HUD scene.</summary>
    [CreateAssetMenu(fileName = "WaveState", menuName = "ZombiCar/State/Wave State")]
    public sealed class WaveState : ScriptableObject
    {
        [NonSerialized] private int m_Wave;
        [NonSerialized] private int m_SecondsLeft;

        public event Action Changed;

        public int Wave => m_Wave;
        public int SecondsLeft => m_SecondsLeft;

        public void Set(int wave, int secondsLeft)
        {
            if (wave == m_Wave && secondsLeft == m_SecondsLeft)
                return;
            m_Wave = wave;
            m_SecondsLeft = Mathf.Max(0, secondsLeft);
            Changed?.Invoke();
        }
    }
}
