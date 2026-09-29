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
    }

    /// <summary>All level-up cards; <see cref="Draw"/> deals a hand of different ones the run can still take.</summary>
    [CreateAssetMenu(fileName = "PerkPool", menuName = "ZombiCar/Perk Pool")]
    public sealed class PerkPool : ScriptableObject
    {
        [SerializeField] private Perk[] m_Perks = Array.Empty<Perk>();

        private readonly List<Perk> m_Candidates = new List<Perk>();

        public IReadOnlyList<Perk> Perks => m_Perks;

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
