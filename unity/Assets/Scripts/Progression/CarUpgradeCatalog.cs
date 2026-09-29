using System;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// One thing sold in the workshop: a car upgrade with a price per level — and after its last level endless
    /// master levels (a small bonus each, the price growing ×<see cref="CarUpgradeCatalog.MasterCostGrowth"/> every
    /// time) — or a repeatable supply (first-aid kit, repair, ammo, mines) whose price grows with every purchase in
    /// the run.
    /// </summary>
    [Serializable]
    public sealed class CarUpgrade
    {
        [SerializeField] private string m_Id;
        [SerializeField] private string m_Title;
        [Tooltip("What one level gives; {0} is replaced by the value per level, {1} by it in percent.")]
        [SerializeField] private string m_Description;
        [SerializeField] private Sprite m_Icon;
        [SerializeField] private StatEffect m_Effect;
        [SerializeField] private float m_ValuePerLevel;
        [Tooltip("Price of each level (its length is the number of levels). A repeatable one uses the first price.")]
        [SerializeField] private int[] m_Costs = Array.Empty<int>();
        [Tooltip("Can be bought again and again (a supply, e.g. a repair).")]
        [SerializeField] private bool m_Repeatable;
        [Tooltip("Repeatable: the price grows by this much with every purchase ...")]
        [SerializeField, Min(0)] private int m_RepeatCostStep;
        [Tooltip("... up to this price (0 = no limit).")]
        [SerializeField, Min(0)] private int m_RepeatCostMax;
        [Tooltip("Upgrades: what one master level gives after the last level (about +2% of the stat; 0 = no master levels).")]
        [SerializeField] private float m_MasterValue;
        [Tooltip("What a master level gives; {0} is replaced by the master value, {1} by it in percent.")]
        [SerializeField] private string m_MasterDescription;
        [Tooltip("Every next level gives this much more than the one before (e.g. spikes +8, +10, +12, +14).")]
        [SerializeField] private float m_ValueStep;
        [Tooltip("The same growth for the master levels.")]
        [SerializeField] private float m_MasterStep;
        [Tooltip("Paid with a rewarded ad, not scrap (e.g. the repair); this many times per run.")]
        [SerializeField, Min(0)] private int m_AdUsesPerRun;

        public CarUpgrade(string id, string title, string description, Sprite icon, StatEffect effect, float valuePerLevel,
            int[] costs, bool repeatable = false, int repeatCostStep = 0, int repeatCostMax = 0,
            float masterValue = 0f, string masterDescription = null, float valueStep = 0f, float masterStep = 0f, int adUsesPerRun = 0)
        {
            m_ValueStep = valueStep;
            m_MasterStep = masterStep;
            m_AdUsesPerRun = adUsesPerRun;
            m_Id = id;
            m_Title = title;
            m_Description = description;
            m_Icon = icon;
            m_Effect = effect;
            m_ValuePerLevel = valuePerLevel;
            m_Costs = costs;
            m_Repeatable = repeatable;
            m_RepeatCostStep = repeatCostStep;
            m_RepeatCostMax = repeatCostMax;
            m_MasterValue = masterValue;
            m_MasterDescription = masterDescription;
        }

        public string Id => m_Id;
        public string Title => m_Title;
        public Sprite Icon => m_Icon;
        public StatEffect Effect => m_Effect;
        public float ValuePerLevel => m_ValuePerLevel;
        public bool Repeatable => m_Repeatable;
        public bool HasMaster => !m_Repeatable && m_MasterValue > 0f;

        /// <summary>The ordinary levels (the pips); master levels come after them.</summary>
        public int RegularLevels => m_Repeatable ? 0 : m_Costs.Length;

        public int MaxLevel => m_Repeatable || HasMaster ? int.MaxValue : m_Costs.Length;

        /// <summary>Paid with a rewarded ad (never with scrap): this many times per run; 0 = bought for scrap.</summary>
        public int AdUsesPerRun => m_AdUsesPerRun;
        public bool ForAd => m_AdUsesPerRun > 0;

        /// <summary>What the first level gives, in words.</summary>
        public string Description => DescriptionAt(0);

        /// <summary>What purchase number <paramref name="level"/> + 1 gives, in words (the ordinary or the master text).</summary>
        public string DescriptionAt(int level)
        {
            float value = ValueAt(level);
            bool master = HasMaster && level >= m_Costs.Length;
            return string.Format(master ? m_MasterDescription ?? string.Empty : m_Description, Format(value),
                Mathf.RoundToInt(value * 100f));
        }

        private static string Format(float value) => Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Master levels bought after <paramref name="level"/> purchases (0 while on the ordinary ones).</summary>
        public int MasterLevel(int level) => HasMaster ? Mathf.Max(0, level - m_Costs.Length) : 0;

        /// <summary>
        /// What purchase number <paramref name="level"/> + 1 gives: it grows with every level (the step), and so do
        /// the master levels after the last one.
        /// </summary>
        public float ValueAt(int level)
        {
            if (m_Repeatable)
                return m_ValuePerLevel;
            if (HasMaster && level >= m_Costs.Length)
                return m_MasterValue + m_MasterStep * (level - m_Costs.Length);
            return m_ValuePerLevel + m_ValueStep * level;
        }

        /// <summary>The total a stat got from <paramref name="levels"/> purchases.</summary>
        public float TotalAt(int levels)
        {
            float total = 0f;
            for (int i = 0; i < levels; i++)
                total += ValueAt(i);
            return total;
        }

        /// <summary>Price of the next purchase after <paramref name="level"/> purchases (-1 when maxed out).</summary>
        public int CostAt(int level)
        {
            if (m_Costs.Length == 0)
                return -1;
            if (m_Repeatable)
            {
                int cost = m_Costs[0] + m_RepeatCostStep * level;
                return m_RepeatCostMax > 0 ? Mathf.Min(cost, Mathf.Max(m_Costs[0], m_RepeatCostMax)) : cost;
            }
            if (level < m_Costs.Length)
                return m_Costs[level];
            if (!HasMaster)
                return -1;
            // Master level n (1, 2, ...): the last level's price × growth^n.
            int n = level - m_Costs.Length + 1;
            return Mathf.RoundToInt(m_Costs[m_Costs.Length - 1] * Mathf.Pow(CarUpgradeCatalog.MasterCostGrowth, n));
        }
    }

    /// <summary>What the workshop sells: the car upgrades and the supplies, in the order shown.</summary>
    [CreateAssetMenu(fileName = "CarUpgrades", menuName = "ZombiCar/Car Upgrade Catalog")]
    public sealed class CarUpgradeCatalog : ScriptableObject
    {
        /// <summary>Master level n costs the last level's price × this^n.</summary>
        public const float MasterCostGrowth = 1.35f;

        [SerializeField] private CarUpgrade[] m_Upgrades = Array.Empty<CarUpgrade>();
        [Tooltip("Bought again and again between waves: first-aid kit, repair, ammo, mines.")]
        [SerializeField] private CarUpgrade[] m_Supplies = Array.Empty<CarUpgrade>();

        public CarUpgrade[] Upgrades => m_Upgrades;
        public CarUpgrade[] Supplies => m_Supplies;

#if UNITY_EDITOR
        public void EditorSetUpgrades(CarUpgrade[] upgrades, CarUpgrade[] supplies)
        {
            m_Upgrades = upgrades;
            m_Supplies = supplies;
        }
#endif
    }
}
