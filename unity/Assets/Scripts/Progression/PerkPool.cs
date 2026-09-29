using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// One roguelike upgrade offered on the level-up cards: an icon, a title, what it does, the stat it changes and
    /// how often it can be taken.
    /// </summary>
    [Serializable]
    public sealed class Perk
    {
        [Tooltip("Stable key (the run counts stacks by it).")]
        [SerializeField] private string m_Id;
        [SerializeField] private string m_Title;
        [SerializeField, TextArea] private string m_Description;
        [SerializeField] private Sprite m_Icon;
        [Tooltip("Colour of the card's icon disc (the perk's group: gun, survival, axe, car, run).")]
        [SerializeField] private Color m_Color = Color.white;
        [SerializeField] private StatEffect m_Effect;
        [SerializeField] private float m_Value;
        [Tooltip("How many times it can be taken in one run (0 = no limit).")]
        [SerializeField, Min(0)] private int m_MaxStacks = 3;
        [Tooltip("Relative chance to be offered.")]
        [SerializeField, Min(0f)] private float m_Weight = 1f;
        [Tooltip("Every next time it is taken it gives this much more (0 = the same each time: an ability, not a number).")]
        [SerializeField] private float m_ValueStep;
        [Tooltip("The run's legendary card: never drawn at random, offered for sure once (PerkPool.LegendaryFromLevel), " +
                 "taken once and at its maximum at once; it may change a second stat too.")]
        [SerializeField] private bool m_Legendary;
        [SerializeField] private bool m_HasSecond;
        [SerializeField] private StatEffect m_Effect2;
        [SerializeField] private float m_Value2;

        public Perk(string id, string title, string description, Sprite icon, Color color, StatEffect effect, float value,
            int maxStacks, float weight, float valueStep = 0f)
        {
            m_ValueStep = valueStep;
            m_Id = id;
            m_Title = title;
            m_Description = description;
            m_Icon = icon;
            m_Color = color;
            m_Effect = effect;
            m_Value = value;
            m_MaxStacks = maxStacks;
            m_Weight = weight;
        }

        public string Id => m_Id;
        public string Title => m_Title;
        /// <summary>The text of the first time; {0} is the value, {1} the value in percent.</summary>
        public string Description => DescriptionAt(0);

        /// <summary>What taking it after <paramref name="stacks"/> times gives, in words.</summary>
        public string DescriptionAt(int stacks)
        {
            float value = ValueAt(stacks);
            string number = Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return string.Format(m_Description, number, Mathf.RoundToInt(value * 100f));
        }

        /// <summary>What taking it after <paramref name="stacks"/> times gives: the value grows by the step each time.</summary>
        public float ValueAt(int stacks) => m_Value + m_ValueStep * stacks;
        public Sprite Icon => m_Icon;
        public Color Color => m_Color;
        public StatEffect Effect => m_Effect;
        public float Value => m_Value;
        public int MaxStacks => m_MaxStacks;
        public float Weight => m_Weight;
        public bool Legendary => m_Legendary;
        public bool HasSecond => m_HasSecond;
        public StatEffect Effect2 => m_Effect2;
        public float Value2 => m_Value2;

        /// <summary>Makes this the legendary card (one stack, never drawn at random), with a second stat (builders).</summary>
        public Perk AsLegendary(StatEffect effect2, float value2)
        {
            m_Legendary = true;
            m_HasSecond = true;
            m_Effect2 = effect2;
            m_Value2 = value2;
            m_MaxStacks = 1;
            m_Weight = 0f;
            return this;
        }
    }

    /// <summary>All level-up cards; <see cref="Draw"/> deals a hand of different ones the run can still take.</summary>
    [CreateAssetMenu(fileName = "PerkPool", menuName = "ZombiCar/Perk Pool")]
    public sealed class PerkPool : ScriptableObject
    {
        [SerializeField] private Perk[] m_Perks = Array.Empty<Perk>();
        [Tooltip("From this player level on, every level-up hand holds the legendary card until the run takes it.")]
        [SerializeField, Min(1)] private int m_LegendaryFromLevel = 5;

        private readonly List<Perk> m_Candidates = new List<Perk>();

        public IReadOnlyList<Perk> Perks => m_Perks;
        public int LegendaryFromLevel => m_LegendaryFromLevel;

        /// <summary>The legendary card (null when the pool has none).</summary>
        public Perk Legendary => Array.Find(m_Perks, p => p != null && p.Legendary);

        /// <summary>
        /// The legendary card goes into <paramref name="hand"/> (in the middle) when the run has reached
        /// <see cref="LegendaryFromLevel"/> and has not taken it yet: sure to come once in every run.
        /// </summary>
        public void OfferLegendary(RunState run, int playerLevel, List<Perk> hand)
        {
            Perk legendary = Legendary;
            if (legendary == null || playerLevel < m_LegendaryFromLevel || run.StacksOf(legendary) > 0 || hand.Contains(legendary))
                return;
            if (hand.Count < 3)
                hand.Add(legendary);
            else
                hand[hand.Count / 2] = legendary;
        }

        /// <summary>
        /// Fills <paramref name="hand"/> with up to <paramref name="count"/> different perks, picked by weight among the
        /// ones not maxed out in <paramref name="run"/>.
        /// </summary>
        public void Draw(RunState run, int count, List<Perk> hand)
        {
            hand.Clear();
            m_Candidates.Clear();
            foreach (Perk perk in m_Perks)
            {
                if (perk != null && perk.Weight > 0f && (perk.MaxStacks == 0 || run.StacksOf(perk) < perk.MaxStacks))
                    m_Candidates.Add(perk);
            }
            while (hand.Count < count && m_Candidates.Count > 0)
            {
                float total = 0f;
                foreach (Perk perk in m_Candidates)
                    total += perk.Weight;
                float roll = UnityEngine.Random.value * total;
                int pick = m_Candidates.Count - 1;
                for (int i = 0; i < m_Candidates.Count; i++)
                {
                    roll -= m_Candidates[i].Weight;
                    if (roll <= 0f)
                    {
                        pick = i;
                        break;
                    }
                }
                hand.Add(m_Candidates[pick]);
                m_Candidates.RemoveAt(pick);
            }
        }

#if UNITY_EDITOR
        public void EditorSetPerks(Perk[] perks) => m_Perks = perks;
#endif
    }
}
