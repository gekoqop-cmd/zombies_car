using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// What the player keeps between runs, saved on this device (PlayerPrefs, like the settings): coins earned at the end
    /// of every run, the skins won from cases and the one equipped in each slot. Moving it to Yandex cloud saves later
    /// only has to replace <see cref="Load"/> / <see cref="Save"/>.
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

        private static bool s_Loaded;
        private static int s_Coins;
        private static readonly HashSet<string> s_Owned = new HashSet<string>();
        private static readonly HashSet<string> s_Seen = new HashSet<string>();
        private static readonly Dictionary<CosmeticSlot, string> s_Equipped = new Dictionary<CosmeticSlot, string>();

        /// <summary>Coins, the collection or an equipped skin changed.</summary>
        public static event Action Changed;

        public static int Coins
        {
            get
            {
                Load();
                return s_Coins;
            }
        }

        /// <summary>The intro cards of the tutorial were seen (or skipped).</summary>
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(c_TutorialKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(c_TutorialKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The live hints of the first run (first zombie, no ammo, workshop, first card) were all shown.</summary>
        public static bool LiveHintsDone
        {
            get => PlayerPrefs.GetInt(c_LiveHintsKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(c_LiveHintsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
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
            PlayerPrefs.SetInt(c_BeatenKey + map, 1);
            if (record)
                PlayerPrefs.SetFloat(c_BestTimeKey + map, seconds);
            PlayerPrefs.Save();
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
            if (firstEnd)
                PlayerPrefs.SetInt($"{c_RecordKey}runs.{map}", GetRecord(map, "runs") + 1);
            if (victory)
                PlayerPrefs.SetInt($"{c_RecordKey}wins.{map}", GetRecord(map, "wins") + 1);
            KeepMax(map, "wave", wave);
            KeepMax(map, "kills", kills);
            KeepMax(map, "bosses", bosses);
            PlayerPrefs.Save();
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
            Save();
        }

        /// <summary>Reads the saved state again (after it was changed outside, e.g. by editor tools).</summary>
        public static void Reload()
        {
            s_Loaded = false;
            Load();
            Changed?.Invoke();
        }

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

        private static void Save()
        {
            PlayerPrefs.SetInt(c_CoinsKey, s_Coins);
            PlayerPrefs.SetString(c_OwnedKey, string.Join(",", s_Owned));
            PlayerPrefs.SetString(c_SeenKey, string.Join(",", s_Seen));
            foreach (CosmeticSlot slot in (CosmeticSlot[])Enum.GetValues(typeof(CosmeticSlot)))
                PlayerPrefs.SetString(c_EquipKey + slot, s_Equipped.TryGetValue(slot, out string id) ? id : string.Empty);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
