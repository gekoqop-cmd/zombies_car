using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>What a cosmetic changes.</summary>
    public enum CosmeticSlot
    {
        Gun,       // colour of the rifle on the player sprite
        Axe,       // colour of the axe blade and its slash
        Bullet,    // tracer colour, size and trail
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>One skin: a look for the rifle, the axe or the bullets. Won from cases in the main menu.</summary>
    [Serializable]
    public sealed class CosmeticItem
    {
        [Tooltip("Stable key (saved in the player's collection).")]
        [SerializeField] private string m_Id;
        [SerializeField] private string m_Title;
        [SerializeField] private CosmeticSlot m_Slot;
        [SerializeField] private Rarity m_Rarity;
        [Tooltip("Main colour: rifle / blade tint, tracer colour.")]
        [SerializeField] private Color m_Color = Color.white;
        [Tooltip("Second colour: the axe's slash, the bullet trail.")]
        [SerializeField] private Color m_Color2 = Color.white;
        [Tooltip("Bullets: tracer size multiplier.")]
        [SerializeField, Min(0.1f)] private float m_Size = 1f;
        [Tooltip("Bullets: a glowing trail behind the tracer.")]
        [SerializeField] private bool m_Trail;
        [Tooltip("The default look every player has (never in a case).")]
        [SerializeField] private bool m_Default;
        [Tooltip("Legendary skins: a bonus for every run while the skin is on (shown in the skins, the cases, the prize).")]
        [SerializeField] private bool m_HasBonus;
        [SerializeField] private ZombiCar.Progression.StatEffect m_Bonus;
        [SerializeField] private float m_BonusValue;
        [Tooltip("The bonus in words: \"+15% к скорострельности\".")]
        [SerializeField] private string m_BonusText;
        [Tooltip("Never in a case: a reward of its own (the 7th day's daily bonus).")]
        [SerializeField] private bool m_Exclusive;
        [Tooltip("Bullets: a glowing outline in the second colour around the tracer.")]
        [SerializeField] private bool m_Outline;

        public CosmeticItem(string id, string title, CosmeticSlot slot, Rarity rarity, Color color, Color color2,
            float size = 1f, bool trail = false, bool isDefault = false)
        {
            m_Id = id;
            m_Title = title;
            m_Slot = slot;
            m_Rarity = rarity;
            m_Color = color;
            m_Color2 = color2;
            m_Size = size;
            m_Trail = trail;
            m_Default = isDefault;
        }

        public string Id => m_Id;
        public string Title => m_Title;
        public CosmeticSlot Slot => m_Slot;
        public Rarity Rarity => m_Rarity;
        public Color Color => m_Color;
        public Color Color2 => m_Color2;
        public float Size => m_Size;
        public bool Trail => m_Trail;
        public bool IsDefault => m_Default;
        public bool HasBonus => m_HasBonus;
        public ZombiCar.Progression.StatEffect Bonus => m_Bonus;
        public float BonusValue => m_BonusValue;
        public string BonusText => m_BonusText;
        public bool IsExclusive => m_Exclusive;
        public bool Outline => m_Outline;

        /// <summary>Gives the skin a bonus for the runs it is worn in (builders).</summary>
        public CosmeticItem WithBonus(ZombiCar.Progression.StatEffect effect, float value, string text)
        {
            m_HasBonus = true;
            m_Bonus = effect;
            m_BonusValue = value;
            m_BonusText = text;
            return this;
        }

        /// <summary>Bullets: a glowing outline around the tracer (builders).</summary>
        public CosmeticItem WithOutline()
        {
            m_Outline = true;
            return this;
        }

        /// <summary>Never dropped by a case (builders).</summary>
        public CosmeticItem AsExclusive()
        {
            m_Exclusive = true;
            return this;
        }
    }

    /// <summary>A case in the main menu: skins of one slot for coins or a rewarded video.</summary>
    [Serializable]
    public sealed class CaseDef
    {
        [SerializeField] private string m_Title;
        [SerializeField] private CosmeticSlot m_Slot;
        [SerializeField, Min(0)] private int m_Price;

        public CaseDef(string title, CosmeticSlot slot, int price)
        {
            m_Title = title;
            m_Slot = slot;
            m_Price = price;
        }

        public string Title => m_Title;
        public CosmeticSlot Slot => m_Slot;
        public int Price => m_Price;
    }
}
