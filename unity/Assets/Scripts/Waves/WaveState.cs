using System;
using UnityEngine;

namespace ZombiCar.Waves
{
    /// <summary>Where the waves are: counting down to the next wave, a wave running, or all waves done.</summary>
    public enum WavePhase
    {
        Countdown,
        Running,
        Finished,
    }

    /// <summary>
    /// Runtime wave number, countdown and phase, shared with the HUD scene; also raises the start and the end of
    /// every wave (banners, sounds, rewards, the car's between-waves services).
    /// </summary>
    [CreateAssetMenu(fileName = "WaveState", menuName = "ZombiCar/State/Wave State")]
    public sealed class WaveState : ScriptableObject
    {
        [NonSerialized] private int m_Wave;
        [NonSerialized] private int m_SecondsLeft;
        [NonSerialized] private WavePhase m_Phase;
        [NonSerialized] private int m_WaveCount;
        [NonSerialized] private string m_BossName;
        [NonSerialized] private int m_BossHealth;
        [NonSerialized] private int m_BossMaxHealth;
        [NonSerialized] private bool m_BossEnraged;

        /// <summary>Wave number or countdown changed (at most once per second).</summary>
        public event Action Changed;

        /// <summary>A wave started (its number).</summary>
        public event Action<int> WaveStarted;

        /// <summary>A wave was cleared or its time ran out (its number).</summary>
        public event Action<int> WaveEnded;

        /// <summary>A boss came out (its name).</summary>
        public event Action<string> BossAppeared;

        /// <summary>"ПРОПУСТИТЬ ПЕРЕРЫВ": the player wants the next wave now (the HUD asks, the map's director starts it).</summary>
        public event Action SkipBreakRequested;

        public void RequestSkipBreak()
        {
            if (m_Phase == WavePhase.Countdown)
                SkipBreakRequested?.Invoke();
        }

        public int Wave => m_Wave;
        public int SecondsLeft => m_SecondsLeft;
        public WavePhase Phase => m_Phase;
        public int WaveCount => m_WaveCount;

        /// <summary>The boss of the running wave (null when it has none or it is dead).</summary>
        public string BossName => m_BossName;

        /// <summary>Health of the boss of the running wave (the HUD's boss bar polls it every frame).</summary>
        public int BossHealth => m_BossHealth;
        public int BossMaxHealth => m_BossMaxHealth;

        /// <summary>The boss is in its second (enraged) phase.</summary>
        public bool BossEnraged => m_BossEnraged;

        public bool HasBoss => !string.IsNullOrEmpty(m_BossName);

        public bool IsWaveRunning => m_Phase == WavePhase.Running;

        /// <summary>The boss's health this frame (no event: it changes too often).</summary>
        public void SetBossHealth(int health, int maxHealth, bool enraged)
        {
            m_BossHealth = Mathf.Max(0, health);
            m_BossMaxHealth = Mathf.Max(1, maxHealth);
            m_BossEnraged = enraged;
        }

        public void Set(int wave, int secondsLeft)
        {
            if (wave == m_Wave && secondsLeft == m_SecondsLeft)
                return;
            m_Wave = wave;
            m_SecondsLeft = Mathf.Max(0, secondsLeft);
            Changed?.Invoke();
        }

        /// <summary>Back to the state before the first wave (a map starts).</summary>
        public void Begin(int waveCount)
        {
            m_WaveCount = waveCount;
            m_Phase = WavePhase.Countdown;
            m_BossName = null;
        }

        public void StartWave(int wave)
        {
            m_Phase = WavePhase.Running;
            m_Wave = wave;
            Changed?.Invoke();
            WaveStarted?.Invoke(wave);
        }

        public void EndWave(int wave, bool last)
        {
            m_Phase = last ? WavePhase.Finished : WavePhase.Countdown;
            m_BossName = null;
            Changed?.Invoke();
            WaveEnded?.Invoke(wave);
        }

        public void SetBoss(string name)
        {
            m_BossName = name;
            Changed?.Invoke();
            if (!string.IsNullOrEmpty(name))
                BossAppeared?.Invoke(name);
        }
    }
}
