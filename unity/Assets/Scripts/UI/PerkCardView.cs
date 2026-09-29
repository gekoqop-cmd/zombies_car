using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// One level-up card: icon on a disc in the perk's colour, title, what it does, "НОВОЕ" and a pulsing glow when
    /// the run has not taken it yet, and the number key that picks it on desktop. <see cref="LevelUpScreen"/> drives
    /// its appear / pick animation.
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

        private bool m_New;

        public Button Button => m_Button;

        public void Bind(Perk perk, int stacks, int index, bool showKey)
        {
            m_Icon.sprite = perk.Icon;
            m_IconDisc.color = perk.Color;
            m_Title.SetText(perk.Title);
            m_Description.SetText(perk.DescriptionAt(stacks));                  // what taking it NOW gives
            // Only a card the run has not taken yet is marked ("НОВОЕ" and a glow); levels are on the HUD.
            m_New = stacks == 0;
            m_Level.gameObject.SetActive(m_New);
            m_Level.SetText("НОВОЕ");
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
            m_Group.alpha = Mathf.Clamp01(appear * 3f) * (1f - fade);
            if (m_Glow != null)
            {
                Color c = m_Glow.color;
                float newGlow = m_New ? (0.2f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f)) * Mathf.Clamp01(appear) * (1f - fade) : 0f;
                c.a = picked > 0f ? (1f - picked) * 0.9f : newGlow;
                m_Glow.color = c;
            }
        }
    }
}
