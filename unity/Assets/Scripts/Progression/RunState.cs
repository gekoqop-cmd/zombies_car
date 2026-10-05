using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Progression
{
    /// <summary>What a pickup on the ground gives.</summary>
    public enum LootKind
    {
        Scrap,
        Ammo,
    }

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
        [NonSerialized] private bool m_Revived;
        [NonSerialized] private int m_Rerolls;
        [NonSerialized] private int m_ArmedMines;
        [NonSerialized] private int m_Medkits;
        [NonSerialized] private float m_StartTime;
        [NonSerialized] private float m_PlayTime = -1f;
        [NonSerialized] private int m_MoneyInFlight;
        [NonSerialized] private Difficulty m_Difficulty = Difficulty.Normal;

        /// <summary>Scrap, kills or the wave changed.</summary>
        public event Action Changed;

        /// <summary>A pickup was taken on the map: what, how much, where (the HUD flies its icon to the counter).</summary>
        public event Action<LootKind, int, Vector3> LootCollected;

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

        /// <summary>The scrap the counter shows: picked-up scrap counts once its icon has flown into the counter.</summary>
        public int MoneyShown => Mathf.Max(0, m_Money - m_MoneyInFlight);

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

        /// <summary>How hard this run is (chosen in the menu, <see cref="RunLaunch"/>).</summary>
        public Difficulty Difficulty => m_Difficulty;

        // Where this run stands since its last checkpoint: the coins at its end count only what came after (a run
        // that goes on from a checkpoint again and again never pays twice for the waves before it).
        /// <summary>Kills when the run started or went on from its checkpoint.</summary>
        public int BaseKills { get; private set; }
        /// <summary>The wave the run started on (1) or went on from (11 / 21).</summary>
        public int BaseWave { get; private set; } = 1;
        /// <summary>Scrap the run had when it went on from its checkpoint.</summary>
        public int BaseMoney { get; private set; }

        /// <summary>A boss stage was cleared on Easy / Normal: the run is saved to go on from <c>wave</c>; coins paid now.</summary>
        public event Action<int, int> StageCleared;

        /// <summary>Raises <see cref="StageCleared"/> (the map's checkpoint keeper; the HUD shows the stage screen).</summary>
        public void ClearStage(int nextWave, int coins) => StageCleared?.Invoke(nextWave, coins);

        /// <summary>The run's progress from here on is counted from now (a checkpoint was saved).</summary>
        public void SetBase(int wave, int money)
        {
            BaseKills = m_Kills;
            BaseWave = wave;
            BaseMoney = money;
            CoinsPaid = 0;
        }

        public RunModifiers Modifiers => m_Modifiers ??= new RunModifiers();

        /// <summary>Game-time seconds of the run (pauses excluded); frozen when it ended.</summary>
        public float PlayTime => m_PlayTime >= 0f ? m_PlayTime : Time.time - m_StartTime;

        /// <summary>Coins already paid into the menu for this run (a revived run is only paid the difference at its end).</summary>
        public int CoinsPaid { get; set; }

        /// <summary>The end screen's coins were doubled for a rewarded ad (once a run).</summary>
        public bool CoinsDoubled { get; set; }

        /// <summary>An achievement this run unlocked (the HUD announces it on the victory screen); null when none.</summary>
        public string NewAchievement { get; set; }

        /// <summary>A defeat can still be undone once this run (the revive for an ad).</summary>
        public bool CanRevive => !m_Revived && (m_Outcome == RunOutcome.CarDestroyed || m_Outcome == RunOutcome.PlayerDied);

        /// <summary>This run was revived once already (its end was counted before).</summary>
        public bool WasRevived => m_Revived;

        /// <summary>The run was taken back from a defeat (the map puts the player and the car back up).</summary>
        public event Action Revived;

        private Dictionary<string, int> Levels => m_Levels ??= new Dictionary<string, int>();

        /// <summary>A fresh run on <paramref name="map"/>: no scrap, no kills, no upgrades.</summary>
        public void BeginRun(MapConfig map, Difficulty difficulty = Difficulty.Normal)
        {
            m_Map = map;
            m_Difficulty = difficulty;
            BaseKills = 0;
            BaseWave = 1;
            BaseMoney = 0;
            m_Money = m_MoneyEarned = m_Kills = m_BossKills = 0;
            m_Wave = 1;
            m_PendingLevelUps = 0;
            m_WorkshopOpen = false;
            m_Outcome = RunOutcome.None;
            m_Revived = false;
            m_Rerolls = m_ArmedMines = m_Medkits = 0;
            m_MoneyInFlight = 0;
            NewAchievement = null;
            Streak = BestStreak = 0;
            CoinsPaid = 0;
            CoinsDoubled = false;
            m_StartTime = Time.time;
            m_PlayTime = -1f;
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

        /// <summary>
        /// Scrap picked up at <paramref name="world"/>: it is the run's at once, the counter shows it when the HUD's
        /// flying icon lands (<see cref="LootLanded"/>).
        /// </summary>
        public void AddLootMoney(int amount, Vector3 world)
        {
            if (amount <= 0)
                return;
            if (LootCollected != null)
            {
                m_MoneyInFlight += amount;
                LootCollected.Invoke(LootKind.Scrap, amount, world);
            }
            AddMoney(amount);
        }

        /// <summary>A pickup other than scrap was taken at <paramref name="world"/> (the HUD shows it flying in).</summary>
        public void ReportLoot(LootKind kind, int amount, Vector3 world) => LootCollected?.Invoke(kind, amount, world);

        /// <summary>A flying scrap icon reached the counter: the counter takes its amount.</summary>
        public void LootLanded(int amount)
        {
            if (amount <= 0 || m_MoneyInFlight <= 0)
                return;
            m_MoneyInFlight = Mathf.Max(0, m_MoneyInFlight - amount);
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

        // ---------------------------------------------------------------- kill streak
        /// <summary>Kills in a row, each within a few seconds of the last, without the player getting hurt.</summary>
        public int Streak { get; private set; }

        /// <summary>The longest streak of this run.</summary>
        public int BestStreak { get; private set; }

        /// <summary>The streak grew or broke.</summary>
        public event Action StreakChanged;

        /// <summary>Scrap per kill for a streak: ×1.25 from 10 kills, ×1.5 from 25, ×2 from 50.</summary>
        public static float StreakMultiplierFor(int streak) => streak >= 50 ? 2f : streak >= 25 ? 1.5f : streak >= 10 ? 1.25f : 1f;

        public float StreakMultiplier => StreakMultiplierFor(Streak);

        /// <summary>A kill: the streak goes on when <paramref name="chained"/> (in time), else starts again at 1.</summary>
        public void AddStreak(bool chained)
        {
            Streak = chained ? Streak + 1 : 1;
            BestStreak = Mathf.Max(BestStreak, Streak);
            StreakChanged?.Invoke();
        }

        public void BreakStreak()
        {
            if (Streak == 0)
                return;
            Streak = 0;
            StreakChanged?.Invoke();
        }

        // ---------------------------------------------------------------- cards and upgrades
        public int StacksOf(Perk perk) => Levels.TryGetValue("perk:" + perk.Id, out int n) ? n : 0;

        public int LevelOf(CarUpgrade upgrade) => Levels.TryGetValue("car:" + upgrade.Id, out int n) ? n : 0;

        /// <summary>Takes a level-up card.</summary>
        public void TakePerk(Perk perk)
        {
            string key = "perk:" + perk.Id;
            int stacks = StacksOf(perk);
            Levels[key] = stacks + 1;
            Apply(perk.Effect, perk.ValueAt(stacks));
            if (perk.HasSecond)
                Apply(perk.Effect2, perk.Value2);
        }

        /// <summary>A worn legendary skin's bonus for this run (map start).</summary>
        public void AddSkinBonus(StatEffect effect, float value) => Apply(effect, value);

        /// <summary>Buys the next level of a car upgrade if there is one and the scrap is enough.</summary>
        public bool TryBuy(CarUpgrade upgrade)
        {
            int level = LevelOf(upgrade);
            int cost = upgrade.CostAt(level);
            if (cost < 0 || level >= upgrade.MaxLevel || !TrySpend(cost))
                return false;
            Levels["car:" + upgrade.Id] = level + 1;
            Apply(upgrade.Effect, upgrade.ValueAt(level));
            return true;
        }

        // ---------------------------------------------------------------- level-up rerolls, mines
        /// <summary>Hands of level-up cards dealt again this run.</summary>
        public int Rerolls => m_Rerolls;

        /// <summary>Counts one reroll of the level-up cards, paying <paramref name="cost"/> scrap (0 for an ad); false when it is not enough.</summary>
        public bool TryReroll(int cost)
        {
            if (!TrySpend(cost))
                return false;
            m_Rerolls++;
            return true;
        }

        /// <summary>Mines lying around the car now (written by the map, read by the workshop).</summary>
        public int ArmedMines => m_ArmedMines;

        public void SetArmedMines(int count)
        {
            if (count == m_ArmedMines)
                return;
            m_ArmedMines = count;
            Changed?.Invoke();
        }

        /// <summary>The next level of a car upgrade for free (developer cheats); false when it is maxed out.</summary>
        public bool GrantUpgrade(CarUpgrade upgrade)
        {
            int level = LevelOf(upgrade);
            if (level >= upgrade.MaxLevel)
                return false;
            Levels["car:" + upgrade.Id] = level + 1;
            Apply(upgrade.Effect, upgrade.ValueAt(level));
            return true;
        }

        // ---------------------------------------------------------------- first-aid kits in the inventory
        public const int MaxMedkits = 5;

        /// <summary>First-aid kits carried (bought in the workshop, used with 3 or the HUD slot).</summary>
        public int Medkits => m_Medkits;

        /// <summary>A kit used from the inventory is asked for (the HUD slot, the key): the player heals if it helps.</summary>
        public event Action MedkitRequested;

        public void RequestMedkit() => MedkitRequested?.Invoke();

        /// <summary>Takes one kit out of the inventory; false when there is none.</summary>
        public bool ConsumeMedkit()
        {
            if (m_Medkits <= 0)
                return false;
            m_Medkits--;
            Changed?.Invoke();
            return true;
        }

        private static bool IsInstant(StatEffect effect) => effect == StatEffect.StoreMedkit || effect == StatEffect.HealPlayerFull ||
                                                           effect == StatEffect.RepairCar ||
                                                           effect == StatEffect.RefillAmmo || effect == StatEffect.PlaceMines;

        private void Apply(StatEffect effect, float value)
        {
            if (effect == StatEffect.StoreMedkit)
            {
                m_Medkits = Mathf.Min(MaxMedkits, m_Medkits + Mathf.Max(1, Mathf.RoundToInt(value)));
                Changed?.Invoke();
            }
            if (!IsInstant(effect))
            {
                Modifiers.Add(effect, value);
                ModifiersChanged?.Invoke();
            }
            EffectApplied?.Invoke(effect, value);
        }

        // ---------------------------------------------------------------- checkpoints
        /// <summary>The run as a checkpoint to go on from <paramref name="wave"/> (scrap: <paramref name="money"/>).</summary>
        public MetaProgress.Checkpoint ExportCheckpoint(int wave, int money, int playerLevel, int playerXp) => new MetaProgress.Checkpoint
        {
            map = m_Map != null ? m_Map.name : null,
            difficulty = (int)m_Difficulty,
            wave = wave,
            levels = Levels.Where(p => p.Value > 0).OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}").ToArray(),
            money = money,
            medkits = m_Medkits,
            kills = m_Kills,
            bossKills = m_BossKills,
            rerolls = m_Rerolls,
            playerLevel = playerLevel,
            playerXp = playerXp,
            levelUps = m_PendingLevelUps,
            playTime = PlayTime,
            revived = m_Revived,
        };

        /// <summary>
        /// Puts a saved run back on a fresh one: its cards and workshop levels (their effects given again), the ad
        /// supplies used, scrap, kits, kills, the time played; the coins from here on count from it.
        /// </summary>
        public void RestoreCheckpoint(MetaProgress.Checkpoint checkpoint, PerkPool pool, CarUpgradeCatalog catalog)
        {
            if (checkpoint == null)
                return;
            foreach (string entry in checkpoint.levels ?? Array.Empty<string>())
            {
                int eq = entry.LastIndexOf('=');
                if (eq <= 0 || !int.TryParse(entry.Substring(eq + 1), out int n) || n <= 0)
                    continue;
                string key = entry.Substring(0, eq);
                if (key.StartsWith("perk:"))
                {
                    string id = key.Substring(5);
                    Perk perk = pool != null ? pool.Perks.FirstOrDefault(p => p != null && p.Id == id) : null;
                    for (int i = 0; perk != null && i < n && (perk.MaxStacks == 0 || StacksOf(perk) < perk.MaxStacks); i++)
                        TakePerk(perk);
                }
                else if (key.StartsWith("car:"))
                {
                    string id = key.Substring(4);
                    CarUpgrade upgrade = catalog != null ? catalog.Upgrades.FirstOrDefault(u => u.Id == id) : null;
                    if (upgrade == null)
                        Levels[key] = n;                                   // an ad supply: its uses this run
                    for (int i = 0; upgrade != null && i < n; i++)
                        GrantUpgrade(upgrade);
                }
            }
            m_Money = m_MoneyEarned = Mathf.Max(0, checkpoint.money);
            m_Kills = Mathf.Max(0, checkpoint.kills);
            m_BossKills = Mathf.Max(0, checkpoint.bossKills);
            m_Medkits = Mathf.Clamp(checkpoint.medkits, 0, MaxMedkits);
            m_Rerolls = Mathf.Max(0, checkpoint.rerolls);
            m_Revived = checkpoint.revived;
            m_Wave = Mathf.Max(1, checkpoint.wave);
            m_StartTime = Time.time - Mathf.Max(0f, checkpoint.playTime);
            m_PlayTime = -1f;
            SetBase(m_Wave, m_Money);
            Changed?.Invoke();
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
            m_PlayTime = Time.time - m_StartTime;
            if (m_WorkshopOpen)
            {
                m_WorkshopOpen = false;
                WorkshopChanged?.Invoke();
            }
            OutcomeChanged?.Invoke();
        }

        /// <summary>Takes back a defeat once per run (after the rewarded ad); false when it cannot.</summary>
        public bool Revive()
        {
            if (!CanRevive)
                return false;
            m_Revived = true;
            m_Outcome = RunOutcome.None;
            m_StartTime = Time.time - m_PlayTime;                    // the clock goes on from where it stopped
            m_PlayTime = -1f;
            OutcomeChanged?.Invoke();
            Revived?.Invoke();
            return true;
        }
    }
}
