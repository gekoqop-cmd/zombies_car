using System;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Props;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The run's difficulty and checkpoints on a map. At the start it sets the zombies' health, damage and numbers for
    /// the difficulty, and a run that goes on from its checkpoint (<see cref="RunLaunch.Resume"/>) gets back what it
    /// had — cards, workshop levels, scrap, kits, kills, level, time — with full health, a repaired car and a full gun,
    /// counting down to wave 11 or 21. On Easy and Normal, a cleared boss wave (10, 20) saves the checkpoint (the scrap
    /// still lying round counted in), pays the stage's coins and tells the HUD (<see cref="RunState.StageCleared"/>).
    /// </summary>
    [DefaultExecutionOrder(100)]                                     // after the director and the progression started
    public sealed class RunSetup : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private WaveDirector m_Director;
        [SerializeField] private ZombieSpawner m_Spawner;
        [SerializeField] private PlayerProgression m_Progression;
        [SerializeField] private PerkPool m_Pool;
        [SerializeField] private CarUpgradeCatalog m_Catalog;
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private PlayerHealth m_Player;
        [SerializeField] private CarHealth m_Car;
        [SerializeField, OptionalReference] private LootDropper m_Loot;

        private int m_PendingStage = -1;

        /// <summary>This run went on from its checkpoint (tests).</summary>
        public bool Resumed { get; private set; }

        private void OnEnable() => m_Waves.WaveEnded += OnWaveEnded;

        private void OnDisable() => m_Waves.WaveEnded -= OnWaveEnded;

        private void Start()
        {
            Difficulty difficulty = m_Run.Difficulty;
            m_Spawner.HealthScale = DifficultyRules.ZombieHealth(difficulty);
            m_Spawner.DamageScale = DifficultyRules.ZombieDamage(difficulty);
            m_Director.CountScale = DifficultyRules.ZombieCount(difficulty);
            MetaProgress.Checkpoint checkpoint = RunLaunch.Resume && m_Run.Map != null
                ? MetaProgress.GetCheckpoint(m_Run.Map.name, difficulty)
                : null;
            RunLaunch.Resume = false;                                    // "ЗАНОВО" after a defeat asks again
            if (checkpoint != null)
                Resume(checkpoint);
            Debug.Log($"[RunSetup] {difficulty}{(Resumed ? $", from the checkpoint at wave {checkpoint.wave}" : string.Empty)}.");
        }

        /// <summary>Puts a saved run back (the start; tests).</summary>
        public void Resume(MetaProgress.Checkpoint checkpoint)
        {
            m_Run.RestoreCheckpoint(checkpoint, m_Pool, m_Catalog);
            m_Progression.Restore(checkpoint.playerLevel, checkpoint.playerXp);
            m_Run.QueueLevelUps(checkpoint.levelUps);
            m_Director.StartFrom(checkpoint.wave);
            m_Weapon.Refill();
            m_Player.Heal(100000);
            m_Car.Repair(100000);
            Resumed = true;
        }

        // The boss waves' end: saved a moment later, once the wave's scrap and experience are in.
        private void OnWaveEnded(int wave)
        {
            if (!m_Run.IsOver && m_Run.Map != null && DifficultyRules.HasCheckpoints(m_Run.Difficulty) &&
                Array.IndexOf(DifficultyRules.CheckpointWaves, wave + 1) >= 0)
                m_PendingStage = wave + 1;
        }

        private void LateUpdate()
        {
            if (m_PendingStage < 0)
                return;
            int next = m_PendingStage;
            m_PendingStage = -1;
            if (!m_Run.IsOver)
                SaveStage(next);
        }

        /// <summary>Saves the checkpoint to go on from <paramref name="next"/> and pays the stage's coins (tests).</summary>
        public void SaveStage(int next)
        {
            int coins = CoinRules.Stage(m_Run, next);
            MetaProgress.AddCoins(coins);
            int money = m_Run.Money + (m_Loot != null ? m_Loot.PendingScrap : 0);
            MetaProgress.SaveCheckpoint(m_Run.ExportCheckpoint(next, money, m_Progression.Level, m_Progression.Xp));
            m_Run.SetBase(next, money);
            Debug.Log($"[RunSetup] Checkpoint at wave {next} ({m_Run.Difficulty}): {money} scrap, +{coins} coins.");
            m_Run.ClearStage(next, coins);
        }
    }
}
