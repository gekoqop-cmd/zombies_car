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
