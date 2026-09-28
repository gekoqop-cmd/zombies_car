using System;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// One upgrade of the car bought in the workshop: levels with a price each (or a repeatable service such as a
    /// repair whose price grows with every use).
    /// </summary>
    [Serializable]
    public sealed class CarUpgrade
    {
        [SerializeField] private string m_Id;
        [SerializeField] private string m_Title;
        [Tooltip("What one level gives; {0} is replaced by the value per level.")]
        [SerializeField] private string m_Description;
        [SerializeField] private Sprite m_Icon;
        [SerializeField] private StatEffect m_Effect;
        [SerializeField] private float m_ValuePerLevel;
        [Tooltip("Price of each level (its length is the number of levels). A repeatable upgrade uses the first price.")]
        [SerializeField] private int[] m_Costs = Array.Empty<int>();
        [Tooltip("Can be bought again and again (a service, e.g. a repair).")]
        [SerializeField] private bool m_Repeatable;
        [Tooltip("Repeatable: the price grows by this much with every purchase ...")]
        [SerializeField, Min(0)] private int m_RepeatCostStep;
        [Tooltip("... up to this price.")]
        [SerializeField, Min(0)] private int m_RepeatCostMax;

        public CarUpgrade(string id, string title, string description, Sprite icon, StatEffect effect, float valuePerLevel,
            int[] costs, bool repeatable = false, int repeatCostStep = 0, int repeatCostMax = 0)
        {
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
        public int MaxLevel => m_Repeatable ? int.MaxValue : m_Costs.Length;

        public string Description => string.Format(m_Description, m_ValuePerLevel, Mathf.RoundToInt(m_ValuePerLevel * 100f));

        /// <summary>Price of the next purchase after <paramref name="level"/> purchases (-1 when maxed out).</summary>
        public int CostAt(int level)
        {
            if (m_Costs.Length == 0)
                return -1;
            if (m_Repeatable)
                return Mathf.Min(m_Costs[0] + m_RepeatCostStep * level, Mathf.Max(m_Costs[0], m_RepeatCostMax));
            return level < m_Costs.Length ? m_Costs[level] : -1;
        }
    }

    /// <summary>What the workshop sells (car upgrades), in the order shown.</summary>
    [CreateAssetMenu(fileName = "CarUpgrades", menuName = "ZombiCar/Car Upgrade Catalog")]
    public sealed class CarUpgradeCatalog : ScriptableObject
    {
        [SerializeField] private CarUpgrade[] m_Upgrades = Array.Empty<CarUpgrade>();

        public CarUpgrade[] Upgrades => m_Upgrades;

#if UNITY_EDITOR
        public void EditorSetUpgrades(CarUpgrade[] upgrades) => m_Upgrades = upgrades;
#endif
    }
}
