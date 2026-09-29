using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// One skin as a tile (case roulette strip, collection grid): rarity-coloured plate, the slot's pictogram in the
    /// skin's colour, the name; dimmed with a question mark while not owned, outlined when equipped.
    /// </summary>
    public sealed class ItemTile : MonoBehaviour
    {
        [SerializeField] private Image m_Plate;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_Title;          // not "m_Name": that is the object's own name
        [SerializeField] private Image m_Equipped;
        [SerializeField] private TMP_Text m_Locked;
        [SerializeField] private Button m_Button;

        public Button Button => m_Button;
        public CosmeticItem Item { get; private set; }

        public void Set(CosmeticItem item, Sprite icon, bool owned, bool equipped)
        {
            Item = item;
            Color rarity = CosmeticCatalog.RarityColor(item.Rarity);
            m_Plate.color = owned ? rarity * new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.14f, 0.15f, 0.18f, 0.95f);
            m_Icon.sprite = icon;
            m_Icon.color = owned ? item.Color : new Color(0.3f, 0.3f, 0.33f, 1f);
            m_Title.SetText(item.Title);
            m_Title.color = owned ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f);
            if (m_Equipped != null)
                m_Equipped.enabled = equipped;
            if (m_Locked != null)
                m_Locked.gameObject.SetActive(!owned);
        }
    }
}
