using UnityEngine;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The run's economy on a map: every kill gives experience and scrap (the zombie type's
    /// <see cref="ZombieConfig.Xp"/> / <see cref="ZombieConfig.Reward"/>), every cleared wave a bonus
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

        private int m_Level;
        private int m_Xp;

        public int Level => m_Level;

        private void OnEnable()
        {
            if (m_Spawner != null)
                m_Spawner.ZombieDied += OnZombieDied;
            if (m_Waves != null)
                m_Waves.WaveEnded += OnWaveEnded;
        }

        private void OnDisable()
        {
            if (m_Spawner != null)
                m_Spawner.ZombieDied -= OnZombieDied;
            if (m_Waves != null)
                m_Waves.WaveEnded -= OnWaveEnded;
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
            m_Run.AddMoney(Scaled(config.Reward, StatEffect.MoneyGain));
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

        private int Scaled(int amount, StatEffect bonus) =>
            amount <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(amount * m_Run.Modifiers.Multiplier(bonus)));

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
