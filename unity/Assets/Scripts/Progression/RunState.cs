using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Progression
{
    /// <summary>How a run ended.</summary>
    public enum RunOutcome
    {
        None,
        CarDestroyed,
        PlayerDied,
        Victory,
    }

    /// <summary>
    /// Everything one run on a map collects, shared between the map scene (which writes it) and the HUD scene (which
    /// shows it) without cross-scene references: scrap (the currency), kills, the wave reached, the stat effects of the
    /// level-up cards and workshop upgrades taken (<see cref="Modifiers"/>), level-ups waiting for a card, the
    /// workshop being open and how the run ended. Reset by <see cref="MapBootstrap"/> when a map starts.
    /// </summary>
    [CreateAssetMenu(fileName = "RunState", menuName = "ZombiCar/State/Run State")]
    public sealed class RunState : ScriptableObject
    {
        [NonSerialized] private int m_Money;
        [NonSerialized] private int m_MoneyEarned;
        [NonSerialized] private int m_Kills;
        [NonSerialized] private int m_BossKills;
        [NonSerialized] private int m_Wave;
        [NonSerialized] private int m_PendingLevelUps;
        [NonSerialized] private bool m_WorkshopOpen;
        [NonSerialized] private RunOutcome m_Outcome;
        [NonSerialized] private MapConfig m_Map;
        [NonSerialized] private RunModifiers m_Modifiers;
        [NonSerialized] private Dictionary<string, int> m_Levels;

        /// <summary>Scrap, kills or the wave changed.</summary>
        public event Action Changed;

        /// <summary>A stat effect was added (<see cref="Modifiers"/> changed).</summary>
        public event Action ModifiersChanged;

        /// <summary>One card / purchase was applied: the effect and its value (instant effects act on this).</summary>
        public event Action<StatEffect, float> EffectApplied;

        /// <summary>The number of level-ups waiting for a card changed.</summary>
        public event Action LevelUpsChanged;

        /// <summary>The workshop was opened or closed.</summary>
        public event Action WorkshopChanged;

        /// <summary>The run ended (<see cref="Outcome"/>).</summary>
        public event Action OutcomeChanged;

        public int Money => m_Money;
        public int MoneyEarned => m_MoneyEarned;
        public int Kills => m_Kills;
        public int BossKills => m_BossKills;
        public int Wave => m_Wave;
        public int PendingLevelUps => m_PendingLevelUps;
        public bool WorkshopOpen => m_WorkshopOpen;
        public RunOutcome Outcome => m_Outcome;
        public bool IsOver => m_Outcome != RunOutcome.None;

        /// <summary>The map of this run (restart loads it again).</summary>
        public MapConfig Map => m_Map;

        public RunModifiers Modifiers => m_Modifiers ??= new RunModifiers();

        private Dictionary<string, int> Levels => m_Levels ??= new Dictionary<string, int>();

        /// <summary>A fresh run on <paramref name="map"/>: no scrap, no kills, no upgrades.</summary>
        public void BeginRun(MapConfig map)
        {
            m_Map = map;
            m_Money = m_MoneyEarned = m_Kills = m_BossKills = 0;
            m_Wave = 1;
            m_PendingLevelUps = 0;
            m_WorkshopOpen = false;
            m_Outcome = RunOutcome.None;
            Modifiers.Clear();
            Levels.Clear();
            Changed?.Invoke();
            ModifiersChanged?.Invoke();
            LevelUpsChanged?.Invoke();
            WorkshopChanged?.Invoke();
            OutcomeChanged?.Invoke();
        }

        // ---------------------------------------------------------------- scrap, kills, wave
        public void AddMoney(int amount)
        {
            if (amount <= 0)
                return;
            m_Money += amount;
            m_MoneyEarned += amount;
            Changed?.Invoke();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > m_Money)
                return false;
            m_Money -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddKill(bool boss)
        {
            m_Kills++;
            if (boss)
                m_BossKills++;
            Changed?.Invoke();
        }

        public void SetWave(int wave)
        {
            if (wave == m_Wave)
                return;
            m_Wave = wave;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- cards and upgrades
        public int StacksOf(Perk perk) => Levels.TryGetValue("perk:" + perk.Id, out int n) ? n : 0;

        public int LevelOf(CarUpgrade upgrade) => Levels.TryGetValue("car:" + upgrade.Id, out int n) ? n : 0;

        /// <summary>Takes a level-up card.</summary>
        public void TakePerk(Perk perk)
        {
            string key = "perk:" + perk.Id;
            Levels[key] = StacksOf(perk) + 1;
            Apply(perk.Effect, perk.Value);
        }

        /// <summary>Buys the next level of a car upgrade if there is one and the scrap is enough.</summary>
        public bool TryBuy(CarUpgrade upgrade)
        {
            int level = LevelOf(upgrade);
            int cost = upgrade.CostAt(level);
            if (cost < 0 || level >= upgrade.MaxLevel || !TrySpend(cost))
                return false;
            Levels["car:" + upgrade.Id] = level + 1;
            Apply(upgrade.Effect, upgrade.ValuePerLevel);
            return true;
        }

        private void Apply(StatEffect effect, float value)
        {
            if (effect != StatEffect.HealPlayerFull && effect != StatEffect.RepairCar)
            {
                Modifiers.Add(effect, value);
                ModifiersChanged?.Invoke();
            }
            EffectApplied?.Invoke(effect, value);
        }

        // ---------------------------------------------------------------- level-ups, workshop, end
        public void QueueLevelUps(int count)
        {
            if (count <= 0)
                return;
            m_PendingLevelUps += count;
            LevelUpsChanged?.Invoke();
        }

        /// <summary>One waiting level-up got its card.</summary>
        public void ConsumeLevelUp()
        {
            if (m_PendingLevelUps <= 0)
                return;
            m_PendingLevelUps--;
            LevelUpsChanged?.Invoke();
        }

        public void SetWorkshopOpen(bool open)
        {
            if (open == m_WorkshopOpen)
                return;
            m_WorkshopOpen = open;
            WorkshopChanged?.Invoke();
        }

        /// <summary>Ends the run once (the first outcome wins).</summary>
        public void End(RunOutcome outcome)
        {
            if (m_Outcome != RunOutcome.None || outcome == RunOutcome.None)
                return;
            m_Outcome = outcome;
            if (m_WorkshopOpen)
            {
                m_WorkshopOpen = false;
                WorkshopChanged?.Invoke();
            }
            OutcomeChanged?.Invoke();
        }
    }
}
