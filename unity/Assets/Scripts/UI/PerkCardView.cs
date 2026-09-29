using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// One level-up card: icon on a disc in the perk's colour, title, what it does, "НОВОЕ" and a pulsing glow when
    /// the run has not taken it yet, and the number key that picks it on desktop. The legendary card is dressed up: a
    /// gold halo, rays turning behind the icon, a shine sweeping over it, it floats and sways, the title in gold and
    /// "ЛЕГЕНДАРНАЯ • СРАЗУ МАКС.". <see cref="LevelUpScreen"/> drives its appear / pick animation.
    /// </summary>
    public sealed class PerkCardView : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [Tooltip("The animated part (scale / tilt); the button's hit area stays put.")]
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private Image m_IconDisc;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Description;
        [SerializeField] private TMP_Text m_Level;
        [SerializeField] private TMP_Text m_KeyHint;
        [SerializeField] private Image m_Glow;
        [Header("Legendary")]
        [SerializeField] private Image m_LegendaryHalo;
        [SerializeField] private Image m_LegendaryRays;
        [SerializeField] private GameObject m_LegendaryShineClip;
        [SerializeField] private RectTransform m_LegendaryShine;
        [SerializeField] private Color m_LegendaryColor = new Color(1f, 0.8f, 0.25f, 1f);

        private bool m_New;
        private bool m_Legendary;
        private Color m_TitleColor = Color.white;
        private Color m_LevelColor = Color.white;
        private bool m_ColorsKept;

        public Button Button => m_Button;

        /// <summary>This card shows the legendary one (tests).</summary>
        public bool IsLegendary => m_Legendary;

        public void Bind(Perk perk, int stacks, int index, bool showKey)
        {
            if (!m_ColorsKept)
            {
                m_TitleColor = m_Title.color;
                m_LevelColor = m_Level.color;
                m_ColorsKept = true;
            }
            m_Legendary = perk.Legendary;
            m_Icon.sprite = perk.Icon;
            m_IconDisc.color = perk.Color;
            m_Title.SetText(perk.Title);
            m_Description.SetText(perk.DescriptionAt(stacks));                  // what taking it NOW gives
            // Only a card the run has not taken yet is marked ("НОВОЕ" and a glow); levels are on the HUD.
            m_New = stacks == 0;
            m_Level.gameObject.SetActive(m_New || m_Legendary);
            m_Level.SetText(m_Legendary ? "ЛЕГЕНДАРНАЯ • СРАЗУ МАКС." : "НОВОЕ");
            m_Level.color = m_Legendary ? m_LegendaryColor : m_LevelColor;
            m_Title.color = m_Legendary ? m_LegendaryColor : m_TitleColor;
            m_LegendaryHalo.gameObject.SetActive(m_Legendary);
            m_LegendaryRays.gameObject.SetActive(m_Legendary);
            m_LegendaryShineClip.SetActive(m_Legendary);
            m_Body.anchoredPosition = Vector2.zero;
            m_KeyHint.gameObject.SetActive(showKey);
            m_KeyHint.SetText((index + 1).ToString());
            if (m_Glow != null)
            {
                Color c = perk.Color;
                c.a = 0f;
                m_Glow.color = c;
            }
        }

        /// <summary>
        /// <paramref name="appear"/> 0..1 brings the card in (pop); <paramref name="picked"/> 0..1 plays the chosen
        /// card's punch and glow; <paramref name="fade"/> 0..1 fades the cards that were not chosen.
        /// </summary>
        public void Animate(float appear, float picked, float fade)
        {
            float pop = UiMotion.BackOut(appear, 2.2f);
            float punch = UiMotion.Punch(picked) * 0.35f;
            m_Body.localScale = Vector3.one * (Mathf.Lerp(0.55f, 1f, pop) + punch);
            m_Body.localRotation = Quaternion.Euler(0f, 0f, (1f - pop) * 8f);
            if (m_Legendary)
                AnimateLegendary(appear, fade);
            m_Group.alpha = Mathf.Clamp01(appear * 3f) * (1f - fade);
            if (m_Glow != null)
            {
                Color c = m_Glow.color;
                float pulse = m_Legendary ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0.2f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f);
                float newGlow = m_New || m_Legendary ? pulse * Mathf.Clamp01(appear) * (1f - fade) : 0f;
                c.a = picked > 0f ? (1f - picked) * 0.9f : newGlow;
                m_Glow.color = c;
            }
        }

        // Floats and sways, the halo breathes, the rays turn, the shine sweeps across every 1.6 s.
        private void AnimateLegendary(float appear, float fade)
        {
            float t = Time.unscaledTime;
            m_Body.anchoredPosition = new Vector2(0f, 10f * Mathf.Sin(t * 2.2f));
            m_Body.localRotation *= Quaternion.Euler(0f, 0f, 1.6f * Mathf.Sin(t * 1.7f));
            float visible = Mathf.Clamp01(appear) * (1f - fade);
            Color halo = m_LegendaryColor;
            halo.a = (0.65f + 0.35f * Mathf.Sin(t * 5f)) * visible;
            m_LegendaryHalo.color = halo;
            m_LegendaryRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -t * 30f);
            Color rays = m_LegendaryColor;
            rays.a = 0.8f * visible;
            m_LegendaryRays.color = rays;
            float sweep = Mathf.Repeat(t / 1.6f, 1f);
            float width = ((RectTransform)m_LegendaryShine.parent).rect.width;
            m_LegendaryShine.anchoredPosition = new Vector2(Mathf.Lerp(-width, width, sweep), 0f);
        }
    }
}
