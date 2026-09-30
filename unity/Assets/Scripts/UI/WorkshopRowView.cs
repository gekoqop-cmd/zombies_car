using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// One thing in the workshop: icon, title, what it gives, level pips, the
    /// number bought for a supply — and the buy button with the price, greyed out when the scrap is not enough, or
    /// "НЕ НУЖНО" when a supply would do nothing now; a maxed upgrade gets a gold "MAX" plate. A supply for a rewarded
    /// ad looks like the others, with the gold ad button. The price sits right of the scrap icon; a word alone
    /// ("MAX", "НЕ НУЖНО", "ИСПОЛЬЗОВАНО") is centred on the button.
    /// </summary>
    public sealed class WorkshopRowView : MonoBehaviour
    {
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Description;
        [SerializeField] private Image[] m_Pips = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text m_Count;
        [SerializeField] private Button m_BuyButton;
        [SerializeField] private Image m_BuyPlate;
        [SerializeField] private TMP_Text m_Price;
        [SerializeField] private GameObject m_PriceIcon;
        [Tooltip("The gold ad icon on the button (a supply paid with a rewarded ad).")]
        [SerializeField, Core.OptionalReference] private GameObject m_AdIcon;
        [SerializeField] private Color m_AdColor = new Color(0.95f, 0.72f, 0.2f, 1f);
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private Color m_PipOn = new Color(1f, 0.66f, 0.2f, 1f);
        [SerializeField] private Color m_PipOff = new Color(0.25f, 0.26f, 0.3f, 1f);
        [SerializeField] private Color m_BuyColor = new Color(0.93f, 0.47f, 0.13f, 1f);
        [SerializeField] private Color m_DisabledColor = new Color(0.33f, 0.33f, 0.36f, 1f);
        [Tooltip("The plate of a maxed upgrade (\"MAX\").")]
        [SerializeField] private Color m_MaxColor = new Color(0.95f, 0.72f, 0.2f, 1f);
        [Tooltip("Left margin of the price when the scrap icon is shown (the number stays right of it).")]
        [SerializeField] private float m_IconMargin = 58f;
        [SerializeField] private Color m_PriceOk = Color.white;
        [SerializeField] private Color m_PriceTooHigh = new Color(1f, 0.45f, 0.4f, 1f);

        private float m_PunchStart = float.NegativeInfinity;

        public Button BuyButton => m_BuyButton;

        /// <param name="useful">False when buying would do nothing now (a supply: health or ammo already full...).</param>
        /// <param name="count">An ad supply's line under the text ("В ЗАПАСЕ: 2/5"); null = the uses left.</param>
        public void Bind(CarUpgrade upgrade, int level, int money, bool useful = true, string count = null)
        {
            m_Icon.sprite = upgrade.Icon;
            m_Title.SetText(upgrade.Title);
            bool repeatable = upgrade.Repeatable;
            bool top = !repeatable && level >= upgrade.RegularLevels;
            m_Description.SetText(top ? "Прокачано до максимума" : upgrade.DescriptionAt(level));   // what the NEXT level gives
            for (int i = 0; i < m_Pips.Length; i++)
            {
                bool used = !repeatable && i < upgrade.RegularLevels;
                m_Pips[i].gameObject.SetActive(used);
                if (used)
                    m_Pips[i].color = i < level ? m_PipOn : m_PipOff;
            }
            if (m_Count != null && !upgrade.ForAd)
            {
                m_Count.gameObject.SetActive(repeatable);
                m_Count.SetText(level > 0 ? $"КУПЛЕНО: {level}" : string.Empty);
            }

            if (upgrade.ForAd)
            {
                BindAd(upgrade, level, useful, count);
                return;
            }
            if (m_AdIcon != null)
                m_AdIcon.SetActive(false);
            int cost = upgrade.CostAt(level);
            bool maxed = cost < 0 || level >= upgrade.MaxLevel;
            bool affordable = !maxed && useful && money >= cost;
            m_BuyButton.interactable = affordable;
            m_BuyPlate.color = maxed ? m_MaxColor : affordable ? m_BuyColor : m_DisabledColor;
            SetPriceLayout(!maxed && useful);
            m_Price.gameObject.SetActive(true);
            m_Price.SetText(maxed ? "MAX" : !useful ? "НЕ НУЖНО" : cost.ToString());
            m_Price.color = maxed || !useful || affordable ? m_PriceOk : m_PriceTooHigh;
        }

        // Paid with a rewarded ad: the gold ad icon on the button; "ИСПОЛЬЗОВАНО" after the run's uses, "НЕ НУЖНО" when
        // it would do nothing now.
        private void BindAd(CarUpgrade upgrade, int used, bool useful, string count)
        {
            bool spent = used >= upgrade.AdUsesPerRun;
            bool available = !spent && useful;
            if (m_Count != null)
            {
                // Unlimited ones (as many as wanted) show no count unless the screen gives one.
                bool limited = upgrade.AdUsesPerRun < CarUpgrade.UnlimitedAdUses;
                string text = count ?? (limited ? $"ОСТАЛОСЬ: {Mathf.Max(0, upgrade.AdUsesPerRun - used)}" : string.Empty);
                m_Count.gameObject.SetActive(text.Length > 0);
                m_Count.SetText(text);
            }
            m_BuyButton.interactable = available;
            m_BuyPlate.color = available ? m_AdColor : m_DisabledColor;
            SetPriceLayout(false);
            if (m_AdIcon != null)
                m_AdIcon.SetActive(available);
            m_Price.gameObject.SetActive(!available);
            m_Price.SetText(spent ? "ИСПОЛЬЗОВАНО" : "НЕ НУЖНО");
            m_Price.color = m_PriceOk;
        }

        // A price next to the scrap icon (right-aligned after it), or a word alone in the middle of the button.
        private void SetPriceLayout(bool withIcon)
        {
            m_PriceIcon.SetActive(withIcon);
            m_Price.alignment = withIcon ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
            m_Price.margin = withIcon ? new Vector4(m_IconMargin, 0f, 14f, 0f) : new Vector4(10f, 0f, 10f, 0f);
        }

        /// <summary>A short pop after a purchase.</summary>
        public void Punch() => m_PunchStart = Time.unscaledTime;

        private void Update()
        {
            if (m_Body == null)
                return;
            float t = (Time.unscaledTime - m_PunchStart) / 0.3f;
            m_Body.localScale = Vector3.one * (1f + UiMotion.Punch(t) * 0.12f);
        }
    }
}
