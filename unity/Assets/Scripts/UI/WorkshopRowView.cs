using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// One thing in the workshop: icon, title, what it gives, level pips — "МАСТЕР N" once past the last level, the
    /// number bought for a supply — and the buy button with the price, greyed out when the scrap is not enough, or
    /// "НЕ НУЖНО" when a supply would do nothing now.
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
        [SerializeField] private Color m_PriceOk = Color.white;
        [SerializeField] private Color m_PriceTooHigh = new Color(1f, 0.45f, 0.4f, 1f);

        private float m_PunchStart = float.NegativeInfinity;

        public Button BuyButton => m_BuyButton;

        /// <param name="useful">False when buying would do nothing now (a supply: health or ammo already full...).</param>
        public void Bind(CarUpgrade upgrade, int level, int money, bool useful = true)
        {
            m_Icon.sprite = upgrade.Icon;
            m_Title.SetText(upgrade.Title);
            bool repeatable = upgrade.Repeatable;
            bool master = upgrade.HasMaster && level >= upgrade.RegularLevels;
            m_Description.SetText(upgrade.DescriptionAt(level));                // what the NEXT level gives
            for (int i = 0; i < m_Pips.Length; i++)
            {
                bool used = !repeatable && !master && i < upgrade.RegularLevels;
                m_Pips[i].gameObject.SetActive(used);
                if (used)
                    m_Pips[i].color = i < level ? m_PipOn : m_PipOff;
            }
            if (m_Count != null && !upgrade.ForAd)
            {
                int n = upgrade.MasterLevel(level);
                m_Count.gameObject.SetActive(repeatable || master);
                m_Count.SetText(master ? (n > 0 ? $"МАСТЕР {n}" : "МАСТЕР-УРОВЕНЬ ОТКРЫТ")
                    : level > 0 ? $"КУПЛЕНО: {level}" : string.Empty);
            }

            if (upgrade.ForAd)
            {
                BindAd(upgrade, level, useful);
                return;
            }
            if (m_AdIcon != null)
                m_AdIcon.SetActive(false);
            int cost = upgrade.CostAt(level);
            bool maxed = cost < 0 || level >= upgrade.MaxLevel;
            bool affordable = !maxed && useful && money >= cost;
            m_BuyButton.interactable = affordable;
            m_BuyPlate.color = affordable ? m_BuyColor : m_DisabledColor;
            m_PriceIcon.SetActive(!maxed && useful);
            m_Price.gameObject.SetActive(true);
            m_Price.SetText(maxed ? "МАКС." : !useful ? "НЕ НУЖНО" : cost.ToString());
            m_Price.color = maxed || !useful || affordable ? m_PriceOk : m_PriceTooHigh;
        }

        // Paid with a rewarded ad: the gold ad icon on the button; "ИСПОЛЬЗОВАНО" after the run's uses, "НЕ НУЖНО" when
        // it would do nothing now.
        private void BindAd(CarUpgrade upgrade, int used, bool useful)
        {
            bool spent = used >= upgrade.AdUsesPerRun;
            bool available = !spent && useful;
            if (m_Count != null)
            {
                m_Count.gameObject.SetActive(true);
                m_Count.SetText($"ОСТАЛОСЬ: {Mathf.Max(0, upgrade.AdUsesPerRun - used)}");
            }
            m_BuyButton.interactable = available;
            m_BuyPlate.color = available ? m_AdColor : m_DisabledColor;
            m_PriceIcon.SetActive(false);
            if (m_AdIcon != null)
                m_AdIcon.SetActive(available);
            m_Price.gameObject.SetActive(!available);
            m_Price.SetText(spent ? "ИСПОЛЬЗОВАНО" : "НЕ НУЖНО");
            m_Price.color = m_PriceOk;
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
