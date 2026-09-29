using System;
using System.Collections.Generic;
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
        private const string c_TutorialKey = "meta.tutorialDone";

        private static bool s_Loaded;
        private static int s_Coins;
        private static readonly HashSet<string> s_Owned = new HashSet<string>();
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

        /// <summary>The short tutorial of the first run was seen (or skipped).</summary>
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(c_TutorialKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(c_TutorialKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
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
            foreach (CosmeticSlot slot in (CosmeticSlot[])Enum.GetValues(typeof(CosmeticSlot)))
                PlayerPrefs.SetString(c_EquipKey + slot, s_Equipped.TryGetValue(slot, out string id) ? id : string.Empty);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
