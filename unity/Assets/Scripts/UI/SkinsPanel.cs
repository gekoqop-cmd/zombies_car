using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// "СКИНЫ" of the main menu, in tabs: ПЕРСОНАЖИ — the hero (the breathing portrait, "ВЫБРАН") and dark "?" cards
    /// that say "В РАЗРАБОТКЕ :)" in a bubble when tapped; ОРУЖИЕ, ТОПОР, ЭФФЕКТ ПУЛЬ — the collection of that slot, every
    /// skin owned or not, a tap puts an owned one on. New skins are counted on their tab and on the menu's button, and
    /// count as seen once their tab is left. Runs on unscaled time.
    /// </summary>
    public sealed class SkinsPanel : MonoBehaviour
    {
        public const int CharactersTab = 0;

        [SerializeField] private CosmeticCatalog m_Catalog;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Button m_Backdrop;

        [Header("Tabs: characters, gun, axe, bullets")]
        [SerializeField] private Button[] m_Tabs = System.Array.Empty<Button>();
        [SerializeField] private Image[] m_TabPlates = System.Array.Empty<Image>();
        [SerializeField] private GameObject[] m_TabBadges = System.Array.Empty<GameObject>();
        [SerializeField] private TMP_Text[] m_TabBadgeCounts = System.Array.Empty<TMP_Text>();
        [SerializeField] private Color m_TabOn = new Color(0.72f, 0.5f, 1f, 1f);
        [SerializeField] private Color m_TabOff = new Color(0.14f, 0.15f, 0.19f, 0.95f);
        [Tooltip("\"НОВЫХ\" count on the main menu's СКИНЫ button.")]
        [SerializeField] private GameObject m_MenuBadge;
        [SerializeField] private TMP_Text m_MenuBadgeCount;

        [Header("Characters")]
        [SerializeField] private GameObject m_CharactersPage;
        [SerializeField] private Button m_HeroCard;
        [Tooltip("The locked cards: a tap shows the \"in development\" bubble.")]
        [SerializeField] private Button[] m_LockedCards = System.Array.Empty<Button>();
        [SerializeField] private CanvasGroup m_Bubble;
        [SerializeField] private TMP_Text m_BubbleText;
        [SerializeField, Min(0.3f)] private float m_BubbleTime = 1.8f;

        [Header("Collections")]
        [SerializeField] private GameObject m_CollectionPage;
        [SerializeField] private RectTransform m_Grid;
        [SerializeField] private ItemTile m_TileTemplate;
        [Tooltip("Pictograms of the slots (rifle, axe, bullets), tinted by each skin's colour.")]
        [SerializeField] private Sprite[] m_SlotIcons = System.Array.Empty<Sprite>();

        private static readonly CosmeticSlot[] s_TabSlots = { CosmeticSlot.Gun, CosmeticSlot.Gun, CosmeticSlot.Axe, CosmeticSlot.Bullet };

        private readonly List<ItemTile> m_Tiles = new List<ItemTile>();
        private bool m_Open;
        private float m_OpenedAt;
        private int m_Tab;
        private float m_BubbleAt = float.NegativeInfinity;
        private RectTransform m_Tapped;

        public bool IsOpen => m_Open;
        public int CurrentTab => m_Tab;

        /// <summary>The bubble is showing (tests).</summary>
        public bool BubbleShown => Time.unscaledTime - m_BubbleAt < m_BubbleTime;

        public string BubbleText => m_BubbleText.text;

        /// <summary>The collection tiles shown now (tests).</summary>
        public IReadOnlyList<ItemTile> Tiles => m_Tiles;

        private void Awake()
        {
            m_CloseButton.onClick.AddListener(Close);
            m_Backdrop.onClick.AddListener(Close);
            m_HeroCard.onClick.AddListener(() => AudioManager.PlaySfx(Sound.UiClick));
            foreach (Button card in m_LockedCards)
            {
                Button c = card;
                c.onClick.AddListener(() => TapLocked(c));
            }
            for (int i = 0; i < m_Tabs.Length; i++)
            {
                int tab = i;
                m_Tabs[i].onClick.AddListener(() => ShowTab(tab));
            }
            m_TileTemplate.gameObject.SetActive(false);
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            m_Bubble.alpha = 0f;
        }

        private void OnEnable()
        {
            MetaProgress.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => MetaProgress.Changed -= Refresh;

        public void Open()
        {
            m_Open = true;
            m_OpenedAt = Time.unscaledTime;
            m_BubbleAt = float.NegativeInfinity;
            m_Group.blocksRaycasts = m_Group.interactable = true;
            ShowTab(CharactersTab, true);
            AudioManager.PlaySfx(Sound.UiOpen);
        }

        public void Close()
        {
            if (!m_Open)
                return;
            LeaveTab();
            m_Open = false;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            AudioManager.PlaySfx(Sound.UiClose);
        }

        /// <summary>Shows tab <paramref name="tab"/> (buttons, tests): the characters or a slot's collection.</summary>
        public void ShowTab(int tab) => ShowTab(tab, false);

        private void ShowTab(int tab, bool silent)
        {
            if (tab != m_Tab)
                LeaveTab();
            m_Tab = Mathf.Clamp(tab, 0, m_Tabs.Length - 1);
            for (int i = 0; i < m_TabPlates.Length; i++)
                m_TabPlates[i].color = i == m_Tab ? m_TabOn : m_TabOff;
            m_CharactersPage.SetActive(m_Tab == CharactersTab);
            m_CollectionPage.SetActive(m_Tab != CharactersTab);
            if (m_Tab != CharactersTab)
                FillCollection(s_TabSlots[m_Tab]);
            if (!silent)
                AudioManager.PlaySfx(Sound.UiClick);
        }

        // Leaving a collection: the new skins in it were seen.
        private void LeaveTab()
        {
            if (m_Tab != CharactersTab)
                MetaProgress.MarkSeen(m_Catalog.Items.Where(i => i.Slot == s_TabSlots[m_Tab]));
        }

        private Sprite SlotIcon(CosmeticSlot slot) => (int)slot < m_SlotIcons.Length ? m_SlotIcons[(int)slot] : null;

        private void Refresh()
        {
            int total = 0;
            for (int i = 1; i < m_Tabs.Length; i++)
            {
                int fresh = m_Catalog.Items.Count(item => item.Slot == s_TabSlots[i] && MetaProgress.IsNew(item));
                total += fresh;
                if (i < m_TabBadges.Length)
                {
                    m_TabBadges[i].SetActive(fresh > 0);
                    m_TabBadgeCounts[i].SetText(fresh.ToString());
                }
            }
            if (m_MenuBadge != null)
            {
                m_MenuBadge.SetActive(total > 0);
                m_MenuBadgeCount.SetText(total.ToString());
            }
            if (m_Open && m_Tab != CharactersTab)
                FillCollection(s_TabSlots[m_Tab]);
        }

        private void FillCollection(CosmeticSlot slot)
        {
            List<CosmeticItem> items = m_Catalog.Items.Where(i => i.Slot == slot).ToList();
            while (m_Tiles.Count < items.Count)
            {
                ItemTile tile = Instantiate(m_TileTemplate, m_Grid);
                tile.name = $"Tile{m_Tiles.Count + 1}";
                tile.gameObject.SetActive(true);
                m_Tiles.Add(tile);
            }
            CosmeticItem equipped = m_Catalog.Equipped(slot);
            for (int i = 0; i < m_Tiles.Count; i++)
            {
                ItemTile tile = m_Tiles[i];
                bool used = i < items.Count;
                tile.gameObject.SetActive(used);
                if (!used)
                    continue;
                CosmeticItem item = items[i];
                bool owned = MetaProgress.Owns(item);
                tile.Set(item, SlotIcon(slot), owned, item == equipped, MetaProgress.IsNew(item));
                tile.Button.onClick.RemoveAllListeners();
                tile.Button.interactable = owned;
                tile.Button.onClick.AddListener(() =>
                {
                    MetaProgress.Equip(item);
                    AudioManager.PlaySfx(Sound.UiBuy);
                });
            }
        }

        /// <summary>A "?" card was tapped (buttons, tests): the bubble pops up over it and the card shakes.</summary>
        public void TapLocked(Button card)
        {
            m_Tapped = (RectTransform)card.transform;
            m_BubbleAt = Time.unscaledTime;
            m_BubbleText.SetText("В РАЗРАБОТКЕ :)");
            m_Bubble.transform.position = m_Tapped.TransformPoint(new Vector3(0f, m_Tapped.rect.yMax, 0f));
            AudioManager.PlaySfx(Sound.UiDenied);
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.18f);
            if (!m_Open)
                return;
            float t = Time.unscaledTime - m_OpenedAt;
            m_Window.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, UiMotion.BackOut(Mathf.Clamp01(t / 0.3f)));

            float b = Time.unscaledTime - m_BubbleAt;
            bool shown = b < m_BubbleTime && m_Tab == CharactersTab;
            m_Bubble.alpha = shown ? Mathf.Clamp01(Mathf.Min(b / 0.1f, (m_BubbleTime - b) / 0.35f)) : 0f;
            m_Bubble.transform.localScale = Vector3.one * (shown ? Mathf.LerpUnclamped(0.6f, 1f, UiMotion.BackOut(Mathf.Clamp01(b / 0.25f))) : 1f);
            if (m_Tapped != null)
                m_Tapped.localRotation = Quaternion.Euler(0f, 0f, b < 0.35f ? Mathf.Sin(b * 60f) * 5f * (1f - b / 0.35f) : 0f);
        }
    }
}
