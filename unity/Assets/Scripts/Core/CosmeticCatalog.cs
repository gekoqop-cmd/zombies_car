using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Every skin and case (Data/Cosmetics, filled by the editor's CosmeticsBuilder). A case drops a rarity by its
    /// chance (shown to the player on the case), then one skin of that rarity; a skin already owned pays back part of
    /// the price in coins.
    /// </summary>
    [CreateAssetMenu(fileName = "Cosmetics", menuName = "ZombiCar/Cosmetics")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        [SerializeField] private CosmeticItem[] m_Items = Array.Empty<CosmeticItem>();
        [SerializeField] private CaseDef[] m_Cases = Array.Empty<CaseDef>();
        [Tooltip("Chance of each rarity in a case, percent (Common, Rare, Epic, Legendary).")]
        [SerializeField] private float[] m_RarityChances = { 55f, 30f, 12f, 3f };
        [Tooltip("Share of the case price paid back for a skin already owned.")]
        [SerializeField, Range(0f, 1f)] private float m_DuplicateRefund = 0.4f;

        public IReadOnlyList<CosmeticItem> Items => m_Items;
        public IReadOnlyList<CaseDef> Cases => m_Cases;
        public float DuplicateRefund => m_DuplicateRefund;

        public float ChanceOf(Rarity rarity) => (int)rarity < m_RarityChances.Length ? m_RarityChances[(int)rarity] : 0f;

        public CosmeticItem Find(string id)
        {
            foreach (CosmeticItem item in m_Items)
            {
                if (item.Id == id)
                    return item;
            }
            return null;
        }

        public CosmeticItem DefaultFor(CosmeticSlot slot)
        {
            foreach (CosmeticItem item in m_Items)
            {
                if (item.Slot == slot && item.IsDefault)
                    return item;
            }
            return null;
        }

        /// <summary>The skin of <paramref name="slot"/> the player wears (the default one if nothing else is equipped).</summary>
        public CosmeticItem Equipped(CosmeticSlot slot) => Find(MetaProgress.EquippedId(slot)) ?? DefaultFor(slot);

        /// <summary>Skins a case of <paramref name="slot"/> can drop (everything but the default and the exclusive ones).</summary>
        public List<CosmeticItem> Droppable(CosmeticSlot slot)
        {
            var list = new List<CosmeticItem>();
            foreach (CosmeticItem item in m_Items)
            {
                if (item.Slot == slot && !item.IsDefault && !item.IsExclusive)
                    list.Add(item);
            }
            return list;
        }

        /// <summary>Rolls a case: a rarity by its chance (one the slot has), then a skin of it.</summary>
        public CosmeticItem Roll(CosmeticSlot slot)
        {
            List<CosmeticItem> pool = Droppable(slot);
            if (pool.Count == 0)
                return null;
            float total = 0f;
            for (int r = 0; r < m_RarityChances.Length; r++)
                total += pool.Exists(i => (int)i.Rarity == r) ? m_RarityChances[r] : 0f;
            float roll = UnityEngine.Random.value * total;
            Rarity rarity = Rarity.Common;
            for (int r = 0; r < m_RarityChances.Length; r++)
            {
                if (!pool.Exists(i => (int)i.Rarity == r))
                    continue;
                rarity = (Rarity)r;
                roll -= m_RarityChances[r];
                if (roll <= 0f)
                    break;
            }
            List<CosmeticItem> of = pool.FindAll(i => i.Rarity == rarity);
            return of[UnityEngine.Random.Range(0, of.Count)];
        }

        /// <summary>The legendary case (the daily bonus): every legendary skin a case can drop, of all slots.</summary>
        public List<CosmeticItem> LegendaryPool()
        {
            var list = new List<CosmeticItem>();
            foreach (CosmeticItem item in m_Items)
            {
                if (item.Rarity == Rarity.Legendary && !item.IsDefault && !item.IsExclusive)
                    list.Add(item);
            }
            return list;
        }

        /// <summary>The first exclusive skin of <paramref name="slot"/> (the 7th day's black bullets); null when none.</summary>
        public CosmeticItem Exclusive(CosmeticSlot slot)
        {
            foreach (CosmeticItem item in m_Items)
            {
                if (item.Slot == slot && item.IsExclusive)
                    return item;
            }
            return null;
        }

        public static string RarityName(Rarity rarity) => rarity switch
        {
            Rarity.Rare => "РЕДКОЕ",
            Rarity.Epic => "ЭПИЧЕСКОЕ",
            Rarity.Legendary => "ЛЕГЕНДАРНОЕ",
            _ => "ОБЫЧНОЕ",
        };

        public static Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Rare => new Color(0.3f, 0.6f, 1f, 1f),
            Rarity.Epic => new Color(0.72f, 0.36f, 1f, 1f),
            Rarity.Legendary => new Color(1f, 0.74f, 0.2f, 1f),
            _ => new Color(0.62f, 0.66f, 0.7f, 1f),
        };

#if UNITY_EDITOR
        public void EditorSet(CosmeticItem[] items, CaseDef[] cases)
        {
            m_Items = items;
            m_Cases = cases;
        }
#endif
    }
}
