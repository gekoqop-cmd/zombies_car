using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// What the player keeps between runs: coins earned at the end of every run, the skins won from cases and the one
    /// equipped in each slot, the tutorial flags, the map records, the difficulty, the checkpoints of Easy / Normal runs,
    /// the achievements, the daily bonus row and the touch controls' layout. Saved at once on this device (PlayerPrefs) and
    /// mirrored to the Yandex Games cloud (<see cref="CloudSave"/>): <see cref="ExportJson"/> is the whole state in one
    /// snapshot, <see cref="MergeCloud"/> takes the cloud's copy in at start. Every saved change raises the revision
    /// (<see cref="Revision"/>) that decides which copy's coins and equipped skins are the newer.
    /// </summary>
    public static class MetaProgress
    {
        private const string c_CoinsKey = "meta.coins";
        private const string c_OwnedKey = "meta.owned";
        private const string c_EquipKey = "meta.equip.";
        private const string c_SeenKey = "meta.seen";
        private const string c_TutorialKey = "meta.tutorialDone";
        private const string c_LiveHintsKey = "meta.liveHintsDone";
        private const string c_BeatenKey = "meta.beaten.";
        private const string c_BestTimeKey = "meta.bestTime.";
        private const string c_MapsKey = "meta.maps";
        private const string c_RevisionKey = "meta.revision";

        private static bool s_Loaded;
        private static int s_Coins;
        private static readonly HashSet<string> s_Owned = new HashSet<string>();
        private static readonly HashSet<string> s_Seen = new HashSet<string>();
        private static readonly Dictionary<CosmeticSlot, string> s_Equipped = new Dictionary<CosmeticSlot, string>();

        /// <summary>Coins, the collection or an equipped skin changed.</summary>
        public static event Action Changed;

        /// <summary>Something saved changed (the cloud copy has to follow).</summary>
        public static event Action Saved;

        public static int Coins
        {
            get
            {
                Load();
                return s_Coins;
            }
        }

        /// <summary>How many times the progress was saved (on any device, after a merge): the newer copy wins coins.</summary>
        public static int Revision => PlayerPrefs.GetInt(c_RevisionKey, 0);

        /// <summary>The intro cards of the tutorial were seen (or skipped).</summary>
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(c_TutorialKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(c_TutorialKey, value ? 1 : 0);
                Commit();
            }
        }

        /// <summary>The live hints of the first run (first zombie, no ammo, workshop, first card) were all shown.</summary>
        public static bool LiveHintsDone
        {
            get => PlayerPrefs.GetInt(c_LiveHintsKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(c_LiveHintsKey, value ? 1 : 0);
                Commit();
            }
        }

        private const string c_ControlsKey = "meta.controls";
        private const string c_ControlsScaleKey = "meta.controlsScale";

        /// <summary>
        /// The touch controls the player moved, "id=x,y;id=x,y" (area fractions; empty = all at their designed places).
        /// Kept with the progress, not only on the device: a browser in an iframe may drop its local storage.
        /// </summary>
        public static string ControlsLayout
        {
            get => PlayerPrefs.GetString(c_ControlsKey, string.Empty);
            set
            {
                if ((value ?? string.Empty) == ControlsLayout)
                    return;
                PlayerPrefs.SetString(c_ControlsKey, value ?? string.Empty);
                Commit();
            }
        }

        /// <summary>The touch controls' size the player chose (0 = never chosen).</summary>
        public static float ControlsScale
        {
            get => PlayerPrefs.GetFloat(c_ControlsScaleKey, 0f);
            set
            {
                if (Mathf.Approximately(value, ControlsScale))
                    return;
                PlayerPrefs.SetFloat(c_ControlsScaleKey, value);
                Commit();
            }
        }

        // ---------------------------------------------------------------- difficulty, checkpoints
        private const string c_DifficultyKey = "meta.difficulty";
        private const string c_HardcoreOkKey = "meta.hardcoreConfirmed";
        private const string c_CheckpointKey = "meta.checkpoint.";

        /// <summary>The difficulty chosen last (the difficulty screen starts on it).</summary>
        public static Difficulty LastDifficulty
        {
            get => DifficultyRules.Clamp(PlayerPrefs.GetInt(c_DifficultyKey, (int)Difficulty.Normal));
            set
            {
                if (value == LastDifficulty && PlayerPrefs.HasKey(c_DifficultyKey))
                    return;
                PlayerPrefs.SetInt(c_DifficultyKey, (int)value);
                Commit();
            }
        }

        /// <summary>The player went through the "no saves — sure?" warning of Hardcore once (it is not asked again).</summary>
        public static bool HardcoreConfirmed
        {
            get => PlayerPrefs.GetInt(c_HardcoreOkKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(c_HardcoreOkKey, value ? 1 : 0);
                Commit();
            }
        }

        /// <summary>
        /// A run saved after a boss (Easy and Normal): the wave it goes on from and what the run had then — the cards
        /// and workshop levels ("perk:id=2", "car:id=3"), scrap, kits, kills, the player's level, the time played.
        /// </summary>
        [Serializable]
        public sealed class Checkpoint
        {
            public string map;
            public int difficulty;
            public int wave;
            public string[] levels = Array.Empty<string>();
            public int money, medkits, kills, bossKills, rerolls, playerLevel, playerXp, levelUps;
            public float playTime;
            public bool revived;
        }

        private static string CheckpointKey(string map, Difficulty d) => $"{c_CheckpointKey}{map}.{(int)d}";

        /// <summary>The saved run of <paramref name="map"/> on <paramref name="difficulty"/> (null when there is none).</summary>
        public static Checkpoint GetCheckpoint(string map, Difficulty difficulty)
        {
            if (string.IsNullOrEmpty(map) || !DifficultyRules.HasCheckpoints(difficulty))
                return null;
            string json = PlayerPrefs.GetString(CheckpointKey(map, difficulty), string.Empty);
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                Checkpoint cp = JsonUtility.FromJson<Checkpoint>(json);
                return cp != null && cp.wave > 1 ? cp : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || string.IsNullOrEmpty(checkpoint.map))
                return;
            RememberMap(checkpoint.map);
            PlayerPrefs.SetString(CheckpointKey(checkpoint.map, DifficultyRules.Clamp(checkpoint.difficulty)), JsonUtility.ToJson(checkpoint));
            Commit();
        }

        public static void ClearCheckpoint(string map, Difficulty difficulty)
        {
            string key = CheckpointKey(map, difficulty);
            if (string.IsNullOrEmpty(map) || !PlayerPrefs.HasKey(key))
                return;
            PlayerPrefs.DeleteKey(key);
            Commit();
        }

        /// <summary>Every saved run (all maps and difficulties).</summary>
        public static IEnumerable<Checkpoint> AllCheckpoints()
        {
            foreach (string map in KnownMaps())
                for (int d = 0; d < DifficultyRules.Count; d++)
                {
                    Checkpoint cp = GetCheckpoint(map, (Difficulty)d);
                    if (cp != null)
                        yield return cp;
                }
        }

        // ---------------------------------------------------------------- achievements
        private const string c_AchievedKey = "meta.achieved";
        private const string c_ClaimedKey = "meta.claimed";

        /// <summary>"БЕЗУМЕЦ!": all 30 waves on Hardcore.</summary>
        public const string MadmanAchievement = "madman";

        private static IEnumerable<string> List(string key) =>
            PlayerPrefs.GetString(key, string.Empty).Split(',').Where(s => !string.IsNullOrEmpty(s));

        private static void AddToList(string key, string id)
        {
            string[] items = List(key).ToArray();
            if (items.Contains(id))
                return;
            PlayerPrefs.SetString(key, string.Join(",", items.Append(id)));
        }

        public static bool IsAchieved(string id) => List(c_AchievedKey).Contains(id);
        public static bool IsClaimed(string id) => List(c_ClaimedKey).Contains(id);

        /// <summary>Unlocks an achievement; true when it is new (the notification shows).</summary>
        public static bool Achieve(string id)
        {
            if (string.IsNullOrEmpty(id) || IsAchieved(id))
                return false;
            AddToList(c_AchievedKey, id);
            Commit();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes an unlocked achievement's reward once; false when it is locked or was taken.</summary>
        public static bool ClaimAchievement(string id, int coins)
        {
            if (!IsAchieved(id) || IsClaimed(id))
                return false;
            AddToList(c_ClaimedKey, id);
            AddCoins(coins);                                               // saves and raises Changed
            return true;
        }

        /// <summary>An achievement waits to be claimed (the menu's button shows a mark).</summary>
        public static bool HasUnclaimedAchievement => List(c_AchievedKey).Any(id => !IsClaimed(id));

        /// <summary>Locks an achievement again, its reward untaken (tests, cheats).</summary>
        public static void ForgetAchievement(string id)
        {
            PlayerPrefs.SetString(c_AchievedKey, string.Join(",", List(c_AchievedKey).Where(s => s != id)));
            PlayerPrefs.SetString(c_ClaimedKey, string.Join(",", List(c_ClaimedKey).Where(s => s != id)));
            Commit();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- daily bonus
        private const string c_DailyDayKey = "meta.daily.day";
        private const string c_DailyDateKey = "meta.daily.date";

        /// <summary>The day of the 7-day row the next bonus is (0..6); a missed day does not reset it.</summary>
        public static int DailyDay => Mathf.Clamp(PlayerPrefs.GetInt(c_DailyDayKey, 0), 0, 6);

        /// <summary>The day (days since 2000-01-01, local) the last bonus was taken; -1 = never.</summary>
        public static int DailyLastDate => PlayerPrefs.GetInt(c_DailyDateKey, -1);

        public static int Today => (DateTime.Now.Date - new DateTime(2000, 1, 1)).Days;

        /// <summary>Today's bonus can be taken.</summary>
        public static bool DailyReady => DailyLastDate != Today;

        /// <summary>Puts the daily row on <paramref name="day"/>, last taken on <paramref name="lastDate"/> (tests, cheats).</summary>
        public static void SetDaily(int day, int lastDate)
        {
            PlayerPrefs.SetInt(c_DailyDayKey, Mathf.Clamp(day, 0, 6));
            PlayerPrefs.SetInt(c_DailyDateKey, lastDate);
            Commit();
            Changed?.Invoke();
        }

        /// <summary>Marks today's bonus taken: the row moves to the next day (after the 7th, back to the 1st).</summary>
        public static void TakeDaily()
        {
            PlayerPrefs.SetInt(c_DailyDayKey, (DailyDay + 1) % 7);
            PlayerPrefs.SetInt(c_DailyDateKey, Today);
            Commit();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- free cases (the daily bonus)
        private const string c_FreeCasesKey = "meta.freecases";

        /// <summary>Cases won for free (the daily bonus) waiting on the cases screen: any case opens with one.</summary>
        public static int FreeCases => Mathf.Max(0, PlayerPrefs.GetInt(c_FreeCasesKey, 0));

        public static void AddFreeCase()
        {
            PlayerPrefs.SetInt(c_FreeCasesKey, FreeCases + 1);
            Commit();
            Changed?.Invoke();
        }

        /// <summary>Uses up one free case; false when there is none.</summary>
        public static bool TryUseFreeCase()
        {
            if (FreeCases <= 0)
                return false;
            PlayerPrefs.SetInt(c_FreeCasesKey, FreeCases - 1);
            Commit();
            Changed?.Invoke();
            return true;
        }

        /// <summary>All waves of <paramref name="map"/> (a MapConfig name) were beaten at least once.</summary>
        public static bool IsBeaten(string map) => PlayerPrefs.GetInt(c_BeatenKey + map, 0) == 1;

        /// <summary>Fastest victory on <paramref name="map"/>, game seconds; 0 when it was never beaten.</summary>
        public static float BestTime(string map) => PlayerPrefs.GetFloat(c_BestTimeKey + map, 0f);

        /// <summary>Saves a victory on <paramref name="map"/>; true when <paramref name="seconds"/> is a new best time.</summary>
        public static bool RecordVictory(string map, float seconds)
        {
            if (string.IsNullOrEmpty(map))
                return false;
            float best = BestTime(map);
            bool record = best <= 0f || seconds < best;
            RememberMap(map);
            PlayerPrefs.SetInt(c_BeatenKey + map, 1);
            if (record)
                PlayerPrefs.SetFloat(c_BestTimeKey + map, seconds);
            Commit();
            Changed?.Invoke();
            return record;
        }

        /// <summary>A map's bests over all its runs (the map records window).</summary>
        public readonly struct MapRecords
        {
            public readonly int Runs, Wins, BestWave, BestKills, BestBosses;
            public readonly float BestTime;

            public MapRecords(int runs, int wins, int bestWave, int bestKills, int bestBosses, float bestTime)
            {
                Runs = runs;
                Wins = wins;
                BestWave = bestWave;
                BestKills = bestKills;
                BestBosses = bestBosses;
                BestTime = bestTime;
            }
        }

        private const string c_RecordKey = "meta.record.";
        private static readonly string[] s_RecordNames = { "runs", "wins", "wave", "kills", "bosses" };

        private static int GetRecord(string map, string what) => PlayerPrefs.GetInt($"{c_RecordKey}{what}.{map}", 0);

        private static void KeepMax(string map, string what, int value)
        {
            if (value > GetRecord(map, what))
                PlayerPrefs.SetInt($"{c_RecordKey}{what}.{map}", value);
        }

        public static MapRecords Records(string map) => new MapRecords(GetRecord(map, "runs"), GetRecord(map, "wins"),
            GetRecord(map, "wave"), GetRecord(map, "kills"), GetRecord(map, "bosses"), BestTime(map));

        /// <summary>
        /// Counts a finished run on <paramref name="map"/> and keeps its bests (wave reached, zombies, bosses). A revived
        /// run that ends again is counted again only in its bests (<paramref name="firstEnd"/> false).
        /// </summary>
        public static void RecordRun(string map, int wave, int kills, int bosses, bool victory, bool firstEnd)
        {
            if (string.IsNullOrEmpty(map))
                return;
            RememberMap(map);
            if (firstEnd)
                PlayerPrefs.SetInt($"{c_RecordKey}runs.{map}", GetRecord(map, "runs") + 1);
            if (victory)
                PlayerPrefs.SetInt($"{c_RecordKey}wins.{map}", GetRecord(map, "wins") + 1);
            KeepMax(map, "wave", wave);
            KeepMax(map, "kills", kills);
            KeepMax(map, "bosses", bosses);
            Commit();
            Changed?.Invoke();
        }

        /// <summary>The menu opens the cases screen when it loads (the victory screen's "К КЕЙСАМ"); not saved.</summary>
        public static bool OpenCasesOnMenu { get; set; }

        /// <summary>"12:34" (or "1:02:03") for game seconds.</summary>
        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0)
                return;
            Load();
            s_Coins += amount;
            Save();
        }

        public static bool TrySpend(int amount)
        {
            Load();
            if (amount < 0 || amount > s_Coins)
                return false;
            s_Coins -= amount;
            Save();
            return true;
        }

        public static bool Owns(CosmeticItem item)
        {
            Load();
            return item != null && (item.IsDefault || s_Owned.Contains(item.Id));
        }

        /// <summary>Adds a skin to the collection; false when it was already there.</summary>
        public static bool Grant(CosmeticItem item)
        {
            Load();
            if (item == null || item.IsDefault || !s_Owned.Add(item.Id))
                return false;
            Save();
            return true;
        }

        /// <summary>An owned skin the player has not looked at in the collection yet ("НОВЫЙ").</summary>
        public static bool IsNew(CosmeticItem item)
        {
            Load();
            return item != null && !item.IsDefault && s_Owned.Contains(item.Id) && !s_Seen.Contains(item.Id);
        }

        /// <summary>The skins were seen in the collection: no longer "НОВЫЙ".</summary>
        public static void MarkSeen(IEnumerable<CosmeticItem> items)
        {
            Load();
            bool changed = false;
            foreach (CosmeticItem item in items)
                changed |= item != null && s_Owned.Contains(item.Id) && s_Seen.Add(item.Id);
            if (changed)
                Save();
        }

        public static string EquippedId(CosmeticSlot slot)
        {
            Load();
            return s_Equipped.TryGetValue(slot, out string id) ? id : null;
        }

        public static void Equip(CosmeticItem item)
        {
            if (item == null || !Owns(item))
                return;
            s_Equipped[item.Slot] = item.Id;
            Save();
        }

        /// <summary>Everything back to a new player (tests).</summary>
        public static void ResetAll()
        {
            s_Loaded = true;
            s_Coins = 0;
            s_Owned.Clear();
            s_Seen.Clear();
            s_Equipped.Clear();
            PlayerPrefs.DeleteKey(c_FreeCasesKey);
            Save();
        }

        /// <summary>Reads the saved state again (after it was changed outside, e.g. by editor tools).</summary>
        public static void Reload()
        {
            s_Loaded = false;
            Load();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- the cloud copy
        /// <summary>The whole progress in one piece (the cloud save), JSON.</summary>
        [Serializable]
        public sealed class Snapshot
        {
            public int version = 1;
            public int revision;
            public int coins;
            public string[] owned = Array.Empty<string>();
            public string[] seen = Array.Empty<string>();
            public string[] equipped = Array.Empty<string>();          // "Slot:id"
            public bool tutorialDone;
            public bool liveHintsDone;
            public MapSnapshot[] maps = Array.Empty<MapSnapshot>();
            public string controls = string.Empty;
            public float controlsScale;
            public int difficulty = (int)Difficulty.Normal;
            public bool hardcoreConfirmed;
            public Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
            public string[] achieved = Array.Empty<string>();
            public string[] claimed = Array.Empty<string>();
            public int dailyDay;
            public int dailyDate = -1;
            public int freeCases;
        }

        [Serializable]
        public sealed class MapSnapshot
        {
            public string map;
            public bool beaten;
            public float bestTime;
            public int runs, wins, wave, kills, bosses;
        }

        /// <summary>The progress as it is saved now (what goes to the cloud).</summary>
        public static Snapshot Export()
        {
            Load();
            return new Snapshot
            {
                revision = Revision,
                coins = s_Coins,
                owned = s_Owned.OrderBy(id => id).ToArray(),
                seen = s_Seen.OrderBy(id => id).ToArray(),
                equipped = s_Equipped.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}").ToArray(),
                tutorialDone = TutorialDone,
                liveHintsDone = LiveHintsDone,
                maps = KnownMaps().Select(m => new MapSnapshot
                {
                    map = m, beaten = IsBeaten(m), bestTime = BestTime(m), runs = GetRecord(m, "runs"), wins = GetRecord(m, "wins"),
                    wave = GetRecord(m, "wave"), kills = GetRecord(m, "kills"), bosses = GetRecord(m, "bosses"),
                }).ToArray(),
                controls = ControlsLayout,
                controlsScale = ControlsScale,
                difficulty = (int)LastDifficulty,
                hardcoreConfirmed = HardcoreConfirmed,
                checkpoints = AllCheckpoints().ToArray(),
                achieved = List(c_AchievedKey).OrderBy(id => id).ToArray(),
                claimed = List(c_ClaimedKey).OrderBy(id => id).ToArray(),
                dailyDay = DailyDay,
                dailyDate = DailyLastDate,
                freeCases = FreeCases,
            };
        }

        public static string ExportJson() => JsonUtility.ToJson(Export());

        /// <summary>
        /// Takes the cloud's copy in: skins, "seen" marks, the tutorial and every map's bests are joined (nothing won on
        /// either device is lost); coins and equipped skins come from the newer copy (the higher revision; the richer
        /// one on a tie). Returns true when the local progress changed. A null / broken <paramref name="json"/> changes
        /// nothing.
        /// </summary>
        public static bool MergeCloud(string json)
        {
            Snapshot cloud = Parse(json);
            if (cloud == null)
                return false;
            string before = ExportJson();
            Snapshot local = Export();
            bool cloudNewer = cloud.revision > local.revision || (cloud.revision == local.revision && cloud.coins > local.coins);

            Load();
            if (cloudNewer)
            {
                s_Coins = Mathf.Max(0, cloud.coins);
                s_Equipped.Clear();
            }
            foreach (string id in cloud.owned ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id))
                    s_Owned.Add(id);
            foreach (string id in cloud.seen ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id))
                    s_Seen.Add(id);
            foreach (string pair in cloud.equipped ?? Array.Empty<string>())
            {
                int colon = pair.IndexOf(':');
                if (colon <= 0 || !Enum.TryParse(pair.Substring(0, colon), out CosmeticSlot slot))
                    continue;
                string id = pair.Substring(colon + 1);
                if (s_Owned.Contains(id) && (cloudNewer || !s_Equipped.ContainsKey(slot)))
                    s_Equipped[slot] = id;
            }
            if (cloud.tutorialDone)
                PlayerPrefs.SetInt(c_TutorialKey, 1);
            if (cloud.liveHintsDone)
                PlayerPrefs.SetInt(c_LiveHintsKey, 1);
            foreach (MapSnapshot m in cloud.maps ?? Array.Empty<MapSnapshot>())
            {
                if (m == null || string.IsNullOrEmpty(m.map))
                    continue;
                RememberMap(m.map);
                if (m.beaten)
                    PlayerPrefs.SetInt(c_BeatenKey + m.map, 1);
                float best = BestTime(m.map);
                if (m.bestTime > 0f && (best <= 0f || m.bestTime < best))
                    PlayerPrefs.SetFloat(c_BestTimeKey + m.map, m.bestTime);
                int[] values = { m.runs, m.wins, m.wave, m.kills, m.bosses };
                for (int i = 0; i < s_RecordNames.Length; i++)
                    KeepMax(m.map, s_RecordNames[i], values[i]);
            }
            // The touch controls: the newer copy's, or the cloud's where this device has none.
            if (!string.IsNullOrEmpty(cloud.controls) && (cloudNewer || string.IsNullOrEmpty(local.controls)))
                PlayerPrefs.SetString(c_ControlsKey, cloud.controls);
            if (cloud.controlsScale > 0f && (cloudNewer || local.controlsScale <= 0f))
                PlayerPrefs.SetFloat(c_ControlsScaleKey, cloud.controlsScale);
            // Difficulty, checkpoints, the daily row and its free cases: the newer copy's (a checkpoint only this device has stays).
            if (cloudNewer)
            {
                PlayerPrefs.SetInt(c_DifficultyKey, (int)DifficultyRules.Clamp(cloud.difficulty));
                PlayerPrefs.SetInt(c_DailyDayKey, Mathf.Clamp(cloud.dailyDay, 0, 6));
                PlayerPrefs.SetInt(c_DailyDateKey, cloud.dailyDate);
                PlayerPrefs.SetInt(c_FreeCasesKey, Mathf.Max(0, cloud.freeCases));
            }
            foreach (Checkpoint cp in cloud.checkpoints ?? Array.Empty<Checkpoint>())
            {
                if (cp == null || string.IsNullOrEmpty(cp.map) || cp.wave <= 1)
                    continue;
                Difficulty d = DifficultyRules.Clamp(cp.difficulty);
                if (cloudNewer || GetCheckpoint(cp.map, d) == null)
                {
                    RememberMap(cp.map);
                    PlayerPrefs.SetString(CheckpointKey(cp.map, d), JsonUtility.ToJson(cp));
                }
            }
            // Achievements and the warning seen: joined (nothing is lost on either device).
            if (cloud.hardcoreConfirmed)
                PlayerPrefs.SetInt(c_HardcoreOkKey, 1);
            foreach (string id in cloud.achieved ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id))
                    AddToList(c_AchievedKey, id);
            foreach (string id in cloud.claimed ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id))
                    AddToList(c_ClaimedKey, id);
            PlayerPrefs.SetInt(c_RevisionKey, Mathf.Max(local.revision, cloud.revision));
            WriteCollection();
            PlayerPrefs.Save();
            bool changed = ExportJson() != before;
            if (changed)
                Changed?.Invoke();
            return changed;
        }

        private static Snapshot Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                Snapshot s = JsonUtility.FromJson<Snapshot>(json);
                return s != null && s.version >= 1 ? s : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MetaProgress] cloud save not read: {e.Message}");
                return null;
            }
        }

        private static IEnumerable<string> KnownMaps() =>
            PlayerPrefs.GetString(c_MapsKey, string.Empty).Split(',').Where(m => !string.IsNullOrEmpty(m)).Distinct().OrderBy(m => m);

        private static void RememberMap(string map)
        {
            string[] maps = KnownMaps().ToArray();
            if (!maps.Contains(map))
                PlayerPrefs.SetString(c_MapsKey, string.Join(",", maps.Append(map)));
        }

        // ---------------------------------------------------------------- local storage
        private static void Load()
        {
            if (s_Loaded)
                return;
            s_Loaded = true;
            s_Coins = Mathf.Max(0, PlayerPrefs.GetInt(c_CoinsKey, 0));
            s_Owned.Clear();
            foreach (string id in PlayerPrefs.GetString(c_OwnedKey, string.Empty).Split(','))
            {
                if (!string.IsNullOrEmpty(id))
                    s_Owned.Add(id);
            }
            // Skins won before the "new" marks existed count as seen.
            s_Seen.Clear();
            string seen = PlayerPrefs.GetString(c_SeenKey, null);
            foreach (string id in seen == null ? s_Owned.ToArray() : seen.Split(','))
            {
                if (!string.IsNullOrEmpty(id))
                    s_Seen.Add(id);
            }
            s_Equipped.Clear();
            foreach (CosmeticSlot slot in (CosmeticSlot[])Enum.GetValues(typeof(CosmeticSlot)))
            {
                string id = PlayerPrefs.GetString(c_EquipKey + slot, string.Empty);
                if (!string.IsNullOrEmpty(id))
                    s_Equipped[slot] = id;
            }
        }

        private static void WriteCollection()
        {
            PlayerPrefs.SetInt(c_CoinsKey, s_Coins);
            PlayerPrefs.SetString(c_OwnedKey, string.Join(",", s_Owned));
            PlayerPrefs.SetString(c_SeenKey, string.Join(",", s_Seen));
            foreach (CosmeticSlot slot in (CosmeticSlot[])Enum.GetValues(typeof(CosmeticSlot)))
                PlayerPrefs.SetString(c_EquipKey + slot, s_Equipped.TryGetValue(slot, out string id) ? id : string.Empty);
        }

        private static void Save()
        {
            WriteCollection();
            Commit();
            Changed?.Invoke();
        }

        // Every saved change: a new revision, written to disk now, and the cloud copy follows (CloudSave).
        private static void Commit()
        {
            PlayerPrefs.SetInt(c_RevisionKey, Revision + 1);
            PlayerPrefs.Save();
            Saved?.Invoke();
        }
    }
}
