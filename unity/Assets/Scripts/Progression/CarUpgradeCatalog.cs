using System;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// One thing sold in the workshop: a car upgrade with a price per level up to its top (МАКС), or a repeatable
    /// supply (first-aid kit, repair, mines) bought again and again in the run.
    /// </summary>
    [Serializable]
    public sealed class CarUpgrade
    {
        [SerializeField] private string m_Id;
        [SerializeField] private string m_Title;
        [Tooltip("What one level gives; {0} is replaced by the value per level, {1} by it in percent (4.5 for 0.045).")]
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
        [Tooltip("Every next level gives this much more than the one before (e.g. spikes +8, +10, +12, +14).")]
        [SerializeField] private float m_ValueStep;
        [Tooltip("Paid with a rewarded ad, not scrap (e.g. the repair); this many times per run.")]
        [SerializeField, Min(0)] private int m_AdUsesPerRun;

        public CarUpgrade(string id, string title, string description, Sprite icon, StatEffect effect, float valuePerLevel,
            int[] costs, bool repeatable = false, int repeatCostStep = 0, int repeatCostMax = 0,
            float valueStep = 0f, int adUsesPerRun = 0)
        {
            m_ValueStep = valueStep;
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
        }

        public string Id => m_Id;
        public string Title => m_Title;
        public Sprite Icon => m_Icon;
        public StatEffect Effect => m_Effect;
        public float ValuePerLevel => m_ValuePerLevel;
        public bool Repeatable => m_Repeatable;

        /// <summary>The levels (the pips); none for a repeatable supply.</summary>
        public int RegularLevels => m_Repeatable ? 0 : m_Costs.Length;

        public int MaxLevel => m_Repeatable ? int.MaxValue : m_Costs.Length;

        /// <summary>Paid with a rewarded ad (never with scrap): this many times per run; 0 = bought for scrap.</summary>
        public int AdUsesPerRun => m_AdUsesPerRun;

        /// <summary>An ad supply with this many uses (or more) can be taken as often as wanted: no count is shown.</summary>
        public const int UnlimitedAdUses = 999;
        public bool ForAd => m_AdUsesPerRun > 0;

        /// <summary>What the first level gives, in words.</summary>
        public string Description => DescriptionAt(0);

        /// <summary>What purchase number <paramref name="level"/> + 1 gives, in words.</summary>
        public string DescriptionAt(int level)
        {
            float value = ValueAt(level);
            return string.Format(m_Description, Format(value), Format(Mathf.Round(value * 1000f) / 10f));
        }

        private static string Format(float value) => Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// What purchase number <paramref name="level"/> + 1 gives: it grows with every level (the step).
        /// </summary>
        public float ValueAt(int level)
        {
            if (m_Repeatable)
                return m_ValuePerLevel;
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
            return level < m_Costs.Length ? m_Costs[level] : -1;
        }
    }

    /// <summary>What the workshop sells: the car upgrades and the supplies, in the order shown.</summary>
    [CreateAssetMenu(fileName = "CarUpgrades", menuName = "ZombiCar/Car Upgrade Catalog")]
    public sealed class CarUpgradeCatalog : ScriptableObject
    {
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
