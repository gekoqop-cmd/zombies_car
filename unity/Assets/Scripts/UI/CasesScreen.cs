using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>The buttons and texts of one case on the cases screen.</summary>
    [Serializable]
    public sealed class CaseCardRefs
    {
        public TMP_Text Title;
        public TMP_Text Chances;
        public Image Icon;
        public Button OpenButton;
        public TMP_Text OpenLabel;
        public Button AdButton;
        [Tooltip("The legendary skin's run bonus, on its own plate between the open and the ad buttons.")]
        public TMP_Text Bonus;
        [Tooltip("The bonus plate's frame: it breathes so the plate catches the eye.")]
        public Image BonusGlow;
    }

    /// <summary>
    /// Main menu cases (<see cref="CosmeticCatalog"/>): a case of rifle, axe or bullet skins opens for coins (earned at
    /// the end of every run) or for a rewarded video. The drop chances of every rarity are written on the case. Opening
    /// spins a roulette strip ("ПРОПУСТИТЬ" under it) that stops on the prize, then the prize alone comes out
    /// big (<see cref="SkinRevealView"/>); a new skin goes to the collection (and can be put on at once),
    /// a repeat pays part of the price back. The collections themselves are in the menu's "СКИНЫ" (<see cref="SkinsPanel"/>).
    /// Runs on unscaled time.
    /// </summary>
    public sealed class CasesScreen : MonoBehaviour
    {
        private const int c_StripTiles = 42;
        private const int c_PrizeIndex = 36;

        [SerializeField] private CosmeticCatalog m_Catalog;
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private TMP_Text m_Coins;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private CaseCardRefs[] m_Cards = Array.Empty<CaseCardRefs>();
        [Tooltip("Pictograms of the slots (rifle, axe, bullets), tinted by each skin's colour.")]
        [SerializeField] private Sprite[] m_SlotIcons = Array.Empty<Sprite>();

        [Header("Roulette")]
        [SerializeField] private CanvasGroup m_Roulette;
        [Tooltip("The strip's window and marker: gone once the prize is revealed.")]
        [SerializeField] private GameObject m_Wheel;
        [SerializeField] private Button m_SkipButton;
        [SerializeField] private SkinRevealView m_Reveal;
        [SerializeField] private RectTransform m_Strip;
        [SerializeField] private ItemTile m_StripTemplate;
        [SerializeField, Min(1f)] private float m_TileStep = 236f;
        [SerializeField, Min(0.5f)] private float m_SpinTime = 4.2f;
        [SerializeField] private GameObject m_ResultPanel;
        [SerializeField] private TMP_Text m_ResultRarity;
        [SerializeField] private TMP_Text m_ResultName;
        [SerializeField] private TMP_Text m_ResultNote;
        [SerializeField] private Button m_EquipButton;
        [SerializeField] private Button m_OkButton;

        private readonly List<ItemTile> m_StripTiles = new List<ItemTile>();
        private bool m_Open;
        private bool m_Spinning;
        private float m_SpinStart;
        private float m_SpinDistance;
        private int m_LastTick;
        private CosmeticItem m_Prize;
        private bool m_PrizeNew;
        private float m_CoinsFlashUntil;

        public bool IsOpen => m_Open;
        public bool IsSpinning => m_Spinning;
        public CosmeticItem LastPrize => m_Prize;

        /// <summary>The screen closed (the main menu shows its buttons again).</summary>
        public event Action Closed;

        private void Awake()
        {
            m_CloseButton.onClick.AddListener(Hide);
            for (int i = 0; i < m_Cards.Length && i < m_Catalog.Cases.Count; i++)
            {
                CaseDef def = m_Catalog.Cases[i];
                CaseCardRefs card = m_Cards[i];
                card.Title.SetText(def.Title);
                card.Chances.SetText(ChancesText());
                card.Icon.sprite = SlotIcon(def.Slot);
                card.OpenLabel.SetText($"ОТКРЫТЬ ЗА {def.Price}");
                card.OpenButton.onClick.AddListener(() => BuyCase(def));
                card.AdButton.onClick.AddListener(() => AdCase(def));
            }
            m_OkButton.onClick.AddListener(CloseRoulette);
            m_SkipButton.onClick.AddListener(SkipSpin);
            m_EquipButton.onClick.AddListener(() =>
            {
                MetaProgress.Equip(m_Prize);
                AudioManager.PlaySfx(Sound.UiBuy);
                CloseRoulette();
            });
            m_StripTemplate.gameObject.SetActive(false);
            ShowGroup(m_Roulette, false);
            m_Root.alpha = 0f;
            m_Root.gameObject.SetActive(false);
        }

        private void OnEnable() => MetaProgress.Changed += Refresh;
        private void OnDisable() => MetaProgress.Changed -= Refresh;

        public void Show()
        {
            m_Open = true;
            m_Root.gameObject.SetActive(true);
            m_Root.interactable = m_Root.blocksRaycasts = true;
            AudioManager.PlaySfx(Sound.UiOpen);
            Refresh();
        }

        public void Hide()
        {
            if (m_Spinning)
                return;
            m_Open = false;
            ShowGroup(m_Roulette, false);
            SetCasesVisible(true);
            m_Root.interactable = m_Root.blocksRaycasts = false;
            AudioManager.PlaySfx(Sound.UiClose);
            Closed?.Invoke();
        }

        private string ChancesText()
        {
            var parts = new List<string> { "<size=80%>ШАНСЫ ВЫПАДЕНИЯ:</size>" };
            foreach (Rarity r in (Rarity[])Enum.GetValues(typeof(Rarity)))
            {
                string hex = ColorUtility.ToHtmlStringRGB(CosmeticCatalog.RarityColor(r));
                parts.Add($"<color=#{hex}>{CosmeticCatalog.RarityName(r)} {m_Catalog.ChanceOf(r):0.#}%</color>");
            }
            return string.Join("\n", parts);
        }

        // That the case's legendary skin gives a bonus in a run; what bonus stays a secret ("???") until it is won.
        private string BonusText(CosmeticSlot slot)
        {
            foreach (CosmeticItem item in m_Catalog.Items)
            {
                if (item.Slot == slot && item.Rarity == Rarity.Legendary && item.HasBonus)
                    return $"ЛЕГЕНДАРНЫЙ СКИН ДАЁТ БОНУС:\n<size=120%>{(MetaProgress.Owns(item) ? item.BonusText.ToUpperInvariant() : "???")}</size>";
            }
            return string.Empty;
        }

        private Sprite SlotIcon(CosmeticSlot slot) => (int)slot < m_SlotIcons.Length ? m_SlotIcons[(int)slot] : null;

        private void Refresh()
        {
            // Nothing may give the prize away before the roulette stops: the balance (a repeat's refund) waits.
            if (m_Spinning)
                return;
            m_Coins.SetText(MetaProgress.Coins.ToString());
            for (int i = 0; i < m_Cards.Length && i < m_Catalog.Cases.Count; i++)
            {
                if (m_Cards[i].Bonus != null)
                    m_Cards[i].Bonus.SetText(BonusText(m_Catalog.Cases[i].Slot));
            }
        }

        // ---------------------------------------------------------------- opening
        /// <summary>Opens a case for its price in coins (buttons, tests); false when the coins are not enough.</summary>
        public bool BuyCase(CaseDef def)
        {
            if (m_Spinning)
                return false;
            if (!MetaProgress.TrySpend(def.Price))
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                m_CoinsFlashUntil = Time.unscaledTime + 0.6f;
                return false;
            }
            Spin(def);
            return true;
        }

        /// <summary>Opens a case for a rewarded video: only when the platform confirms the reward.</summary>
        public void AdCase(CaseDef def)
        {
            if (m_Spinning)
                return;
            YandexSdk.ShowRewardedAd(rewarded =>
            {
                if (rewarded && this != null && m_Open)
                    Spin(def);
            });
        }

        private void Spin(CaseDef def)
        {
            m_Prize = m_Catalog.Roll(def.Slot);
            if (m_Prize == null)
                return;
            m_PrizeNew = MetaProgress.Grant(m_Prize);
            if (!m_PrizeNew)
                MetaProgress.AddCoins(Mathf.RoundToInt(def.Price * m_Catalog.DuplicateRefund));

            // The strip: random skins of the case, the prize at c_PrizeIndex.
            EnsureTiles(m_StripTiles, m_StripTemplate, m_Strip, c_StripTiles);
            for (int i = 0; i < c_StripTiles; i++)
            {
                CosmeticItem item = i == c_PrizeIndex ? m_Prize : m_Catalog.Roll(def.Slot);
                ItemTile tile = m_StripTiles[i];
                tile.Set(item, SlotIcon(def.Slot), true, false, bonusKnown: false);     // the strip keeps the secret
                ((RectTransform)tile.transform).anchoredPosition = new Vector2(i * m_TileStep, 0f);
            }
            // Stop somewhere on the prize tile, not always dead centre.
            m_SpinDistance = c_PrizeIndex * m_TileStep + UnityEngine.Random.Range(-0.38f, 0.38f) * m_TileStep;
            m_Strip.anchoredPosition = Vector2.zero;
            m_SpinStart = Time.unscaledTime;
            m_LastTick = 0;
            m_Spinning = true;
            m_ResultPanel.SetActive(false);
            m_Reveal.Hide();
            m_Wheel.SetActive(true);
            m_SkipButton.gameObject.SetActive(true);
            SetCasesVisible(false);
            ShowGroup(m_Roulette, true);
            AudioManager.PlaySfx(Sound.UiOpen);
        }

        /// <summary>"ПРОПУСТИТЬ": the strip jumps to its stop and the prize comes out at once.</summary>
        public void SkipSpin()
        {
            if (!m_Spinning)
                return;
            m_Strip.anchoredPosition = new Vector2(-m_SpinDistance, 0f);
            FinishSpin();
        }

        // The case cards (with their "НОВЫХ" counters) stay out of sight behind the roulette.
        private void SetCasesVisible(bool visible)
        {
            foreach (CaseCardRefs card in m_Cards)
                card.Title.transform.parent.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (m_Open || m_Root.alpha > 0f)
            {
                m_Root.alpha = Mathf.MoveTowards(m_Root.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
                if (!m_Open && m_Root.alpha <= 0f)
                    m_Root.gameObject.SetActive(false);
            }
            foreach (CaseCardRefs card in m_Cards)
            {
                if (card.BonusGlow == null)
                    continue;
                Color glow = card.BonusGlow.color;
                glow.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                card.BonusGlow.color = glow;
            }
            if (m_Coins != null)
                m_Coins.color = Time.unscaledTime < m_CoinsFlashUntil ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(1f, 0.85f, 0.4f, 1f);
            if (!m_Spinning)
                return;
            Keyboard keys = Keyboard.current;
            if (keys != null && Time.unscaledTime - m_SpinStart > 0.3f &&
                (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
            {
                SkipSpin();
                return;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - m_SpinStart) / m_SpinTime);
            float eased = 1f - Mathf.Pow(1f - t, 4f);                       // fast start, long slow stop
            float x = eased * m_SpinDistance;
            m_Strip.anchoredPosition = new Vector2(-x, 0f);
            int passed = Mathf.FloorToInt((x + m_TileStep * 0.5f) / m_TileStep);
            if (passed != m_LastTick)
            {
                m_LastTick = passed;
                AudioManager.PlaySfx(Sound.UiClick);
            }
            if (t >= 1f)
                FinishSpin();
        }

        /// <summary>Shows <paramref name="item"/> as the prize at once, no roulette (tests, cheats).</summary>
        public void ShowPrize(CosmeticItem item)
        {
            if (m_Spinning || item == null)
                return;
            m_Prize = item;
            m_PrizeNew = MetaProgress.Grant(item);
            SetCasesVisible(false);
            ShowGroup(m_Roulette, true);
            FinishSpin();
        }

        private void FinishSpin()
        {
            m_Spinning = false;
            m_Wheel.SetActive(false);
            m_SkipButton.gameObject.SetActive(false);
            m_Reveal.Show(m_Prize, SlotIcon(m_Prize.Slot));
            if (m_Prize.Rarity != Rarity.Legendary)                    // a legendary one has its own fanfare (the reveal)
                AudioManager.PlaySfx(m_Prize.Rarity == Rarity.Epic ? Sound.LevelUp : Sound.CardPick);
            m_ResultRarity.SetText(CosmeticCatalog.RarityName(m_Prize.Rarity));
            m_ResultRarity.color = CosmeticCatalog.RarityColor(m_Prize.Rarity);
            m_ResultName.SetText(m_Prize.Title);
            m_ResultName.color = m_Prize.Color;
            int refund = 0;
            foreach (CaseDef def in m_Catalog.Cases)
            {
                if (def.Slot == m_Prize.Slot)
                    refund = Mathf.RoundToInt(def.Price * m_Catalog.DuplicateRefund);
            }
            string note = m_PrizeNew ? "НОВЫЙ СКИН В КОЛЛЕКЦИИ!" : $"УЖЕ ЕСТЬ: +{refund} МОНЕТ";
            if (m_Prize.HasBonus)
            {
                string hex = ColorUtility.ToHtmlStringRGB(CosmeticCatalog.RarityColor(Rarity.Legendary));
                note += $"  <color=#{hex}>БОНУС В ЗАБЕГЕ: {m_Prize.BonusText.ToUpperInvariant()}</color>";
            }
            m_ResultNote.SetText(note);
            bool equipped = MetaProgress.EquippedId(m_Prize.Slot) == m_Prize.Id;
            m_EquipButton.gameObject.SetActive(!equipped);
            m_ResultPanel.SetActive(true);
            Refresh();
        }

        private void CloseRoulette()
        {
            if (m_Spinning)
                return;
            ShowGroup(m_Roulette, false);
            SetCasesVisible(true);
            Refresh();
        }

        // ---------------------------------------------------------------- helpers
        private static void EnsureTiles(List<ItemTile> tiles, ItemTile template, RectTransform parent, int count)
        {
            while (tiles.Count < count)
            {
                ItemTile tile = Instantiate(template, parent);
                tile.name = $"Tile{tiles.Count + 1}";
                tile.gameObject.SetActive(true);
                tiles.Add(tile);
            }
        }

        private static void ShowGroup(CanvasGroup group, bool show)
        {
            group.alpha = show ? 1f : 0f;
            group.interactable = group.blocksRaycasts = show;
            group.gameObject.SetActive(show);
        }
    }
}
