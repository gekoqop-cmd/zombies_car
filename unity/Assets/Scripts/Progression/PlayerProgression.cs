using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The run's economy on a map: every kill gives experience and scrap (the zombie type's
    /// <see cref="ZombieConfig.Xp"/> / <see cref="ZombieConfig.Reward"/>; a boss's scrap lies round it to be picked
    /// up, <see cref="Props.LootDropper"/>), every cleared wave a bonus
    /// (<see cref="ProgressionConfig"/>); cards can raise both. Experience fills the player's level
    /// (<see cref="PlayerStats"/>); each level-up is queued in <see cref="RunState"/> for a card.
    /// </summary>
    public sealed class PlayerProgression : MonoBehaviour
    {
        [SerializeField] private ProgressionConfig m_Config;
        [SerializeField] private RunState m_Run;
        [SerializeField] private PlayerStats m_Stats;
        [SerializeField] private ZombieSpawner m_Spawner;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private PlayerHealth m_Player;
        [Tooltip("Scatters a boss's scrap round it to be picked up (paid at once when missing, or on the last wave).")]
        [SerializeField, OptionalReference] private Props.LootDropper m_Loot;
        [Tooltip("Seconds between two kills that keep a kill streak going (getting hurt breaks it).")]
        [SerializeField, Min(0.5f)] private float m_StreakWindow = 3f;

        private int m_Level;
        private int m_Xp;
        private float m_LastKill = float.NegativeInfinity;

        public int Level => m_Level;

        private void OnEnable()
        {
            if (m_Player != null)
                m_Player.Damaged += OnPlayerHurt;
            if (m_Spawner != null)
                m_Spawner.ZombieDied += OnZombieDied;
            if (m_Waves != null)
            {
                m_Waves.WaveEnded += OnWaveEnded;
                m_Waves.WaveStarted += OnWaveStarted;
            }
        }

        private void OnDisable()
        {
            if (m_Player != null)
                m_Player.Damaged -= OnPlayerHurt;
            if (m_Spawner != null)
                m_Spawner.ZombieDied -= OnZombieDied;
            if (m_Waves != null)
            {
                m_Waves.WaveEnded -= OnWaveEnded;
                m_Waves.WaveStarted -= OnWaveStarted;
            }
        }

        private void OnWaveStarted(int wave) => m_Run.SetWave(wave);

        private void OnPlayerHurt(int damage) => m_Run.BreakStreak();

        // A streak runs out when no kill comes in time.
        private void Update()
        {
            if (m_Run.Streak > 0 && Time.time - m_LastKill > m_StreakWindow)
                m_Run.BreakStreak();
        }

        private void Start()
        {
            m_Level = m_Stats.StartLevel;
            m_Xp = 0;
            Publish();
        }

        private void OnZombieDied(ZombieController zombie)
        {
            if (m_Run.IsOver)
                return;
            ZombieConfig config = zombie.Config;
            m_Run.AddKill(config.IsBoss);
            if (config.IsBoss)
                GameFeel.BossDown();
            m_Run.AddStreak(Time.time - m_LastKill <= m_StreakWindow);
            m_LastKill = Time.time;
            int reward = Scaled(config.Reward, StatEffect.MoneyGain);
            if (!config.IsBoss)
                reward = Mathf.RoundToInt(reward * m_Run.StreakMultiplier);       // a streak pays more per kill
            if (!config.IsBoss || m_Loot == null || !m_Loot.Scatter(zombie.transform.position, reward))
                m_Run.AddMoney(reward);
            AddXp(Scaled(config.Xp, StatEffect.XpGain));
            int heal = m_Run.Modifiers.Whole(StatEffect.KillHeal);
            if (heal > 0 && m_Player != null)
                m_Player.Heal(heal);
        }

        private void OnWaveEnded(int wave)
        {
            if (m_Run.IsOver)
                return;
            m_Run.AddMoney(Scaled(m_Config.WaveMoney(wave), StatEffect.MoneyGain));
            AddXp(Scaled(m_Config.WaveXp(wave), StatEffect.XpGain));
        }

        // The cards' bonus, and for scrap the run's difficulty (more on Hardcore, less on Easy).
        private int Scaled(int amount, StatEffect bonus) =>
            amount <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(amount * m_Run.Modifiers.Multiplier(bonus) *
                                                            (bonus == StatEffect.MoneyGain ? DifficultyRules.Scrap(m_Run.Difficulty) : 1f)));

        /// <summary>A run going on from its checkpoint: the player's level and experience back as they were.</summary>
        public void Restore(int level, int xp)
        {
            m_Level = Mathf.Max(m_Stats.StartLevel, level);
            m_Xp = Mathf.Max(0, xp);
            Publish();
        }

        public int Xp => m_Xp;

        /// <summary>Adds experience; every level reached is queued for a card.</summary>
        public void AddXp(int amount)
        {
            if (amount <= 0)
                return;
            m_Xp += amount;
            int gained = 0;
            for (int need = m_Config.XpToNext(m_Level); m_Xp >= need; need = m_Config.XpToNext(m_Level))
            {
                m_Xp -= need;
                m_Level++;
                gained++;
            }
            Publish();
            if (gained > 0)
            {
                Debug.Log($"[PlayerProgression] Level {m_Level} (+{gained}).");
                m_Run.QueueLevelUps(gained);
            }
        }

        private void Publish() => m_Stats.SetProgress(m_Level, m_Xp, m_Config.XpToNext(m_Level));
    }
}
