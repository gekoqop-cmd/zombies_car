using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// One car upgrade in the workshop: icon, title, what a level gives, level pips (or the number of repairs for a
    /// repeatable service) and the buy button with the price, greyed out when the scrap is not enough or the upgrade
    /// is maxed out.
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
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private Color m_PipOn = new Color(1f, 0.66f, 0.2f, 1f);
        [SerializeField] private Color m_PipOff = new Color(0.25f, 0.26f, 0.3f, 1f);
        [SerializeField] private Color m_BuyColor = new Color(0.93f, 0.47f, 0.13f, 1f);
        [SerializeField] private Color m_DisabledColor = new Color(0.33f, 0.33f, 0.36f, 1f);
        [SerializeField] private Color m_PriceOk = Color.white;
        [SerializeField] private Color m_PriceTooHigh = new Color(1f, 0.45f, 0.4f, 1f);

        private float m_PunchStart = float.NegativeInfinity;

        public Button BuyButton => m_BuyButton;

        public void Bind(CarUpgrade upgrade, int level, int money)
        {
            m_Icon.sprite = upgrade.Icon;
            m_Title.SetText(upgrade.Title);
            m_Description.SetText(upgrade.Description);
            bool repeatable = upgrade.Repeatable;
            for (int i = 0; i < m_Pips.Length; i++)
            {
                bool used = !repeatable && i < upgrade.MaxLevel;
                m_Pips[i].gameObject.SetActive(used);
                if (used)
                    m_Pips[i].color = i < level ? m_PipOn : m_PipOff;
            }
            if (m_Count != null)
            {
                m_Count.gameObject.SetActive(repeatable);
                m_Count.SetText(level > 0 ? $"СДЕЛАНО: {level}" : string.Empty);
            }

            int cost = upgrade.CostAt(level);
            bool maxed = cost < 0 || level >= upgrade.MaxLevel;
            bool affordable = !maxed && money >= cost;
            m_BuyButton.interactable = affordable;
            m_BuyPlate.color = affordable ? m_BuyColor : m_DisabledColor;
            m_PriceIcon.SetActive(!maxed);
            m_Price.SetText(maxed ? "МАКС." : cost.ToString());
            m_Price.color = maxed || affordable ? m_PriceOk : m_PriceTooHigh;
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
