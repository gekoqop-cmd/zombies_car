using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The tutorial's intro cards (HUD and main menu), shown while <see cref="TutorialState.CardsOpen"/>: a card with a
    /// picture (one or two icons and an optional caption), a title and a short text; "Далее" turns to the next card
    /// ("Играть" on the last), "Пропустить" closes them; dots at the bottom show where the player is. Cards with a
    /// touch text show it on touch screens. Enter / Space turn the page, Esc skips. Runs on unscaled time (the map
    /// waits behind it, <see cref="TutorialDirector"/>).
    /// </summary>
    public sealed class TutorialCardsView : MonoBehaviour
    {
        [Serializable]
        public sealed class Card
        {
            public string Title;
            [TextArea] public string Text;
            [Tooltip("Text on touch screens (empty = the same as on PC).")]
            [TextArea] public string TouchText;
            public Sprite Icon;
            [Tooltip("Second icon beside the first (optional).")]
            [OptionalReference] public Sprite Icon2;
            [Tooltip("Icon on touch screens (empty = the same).")]
            [OptionalReference] public Sprite TouchIcon;
            public string Caption;
            public string TouchCaption;
            public Color Accent = new Color(0.93f, 0.47f, 0.13f, 1f);
        }

        [SerializeField] private TutorialState m_State;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Card;
        [SerializeField] private Image m_PictureBack;
        [SerializeField] private Image m_Icon;
        [SerializeField] private Image m_Icon2;
        [SerializeField] private TMP_Text m_Caption;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField] private TMP_Text m_Counter;
        [SerializeField] private Button m_NextButton;
        [SerializeField] private TMP_Text m_NextLabel;
        [SerializeField] private Button m_SkipButton;
        [SerializeField] private Image[] m_Dots = Array.Empty<Image>();
        [SerializeField] private Card[] m_Cards = Array.Empty<Card>();
        [SerializeField] private Color m_DotIdle = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private string m_NextText = "ДАЛЕЕ";
        [SerializeField] private string m_LastText = "ИГРАТЬ!";
        [SerializeField, Min(0.01f)] private float m_Fade = 0.2f;

        private int m_Index;
        private bool m_Open;
        private float m_TurnedAt = float.NegativeInfinity;
        private float m_Direction = 1f;
        private float m_Icon1X;

        public bool IsOpen => m_Open;
        public int Index => m_Index;
        public int Count => m_Cards.Length;

        private void Awake()
        {
            m_NextButton.onClick.AddListener(Next);
            m_SkipButton.onClick.AddListener(Close);
            m_Icon1X = m_Icon.rectTransform.anchoredPosition.x;
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
        }

        private void OnEnable()
        {
            m_State.Changed += OnChanged;
            OnChanged();
        }

        private void OnDisable()
        {
            m_State.Changed -= OnChanged;
            if (m_Open)
            {
                m_Open = false;
                m_State.CloseCards();
            }
        }

        private void OnChanged()
        {
            if (m_State.CardsOpen && !m_Open)
                Open();
            else if (!m_State.CardsOpen && m_Open)
                Hide();
        }

        private void Open()
        {
            m_Open = true;
            m_Index = 0;
            m_Direction = 1f;
            m_Group.blocksRaycasts = m_Group.interactable = true;
            AudioManager.PlaySfx(Sound.UiOpen);
            ShowCard();
        }

        /// <summary>Next card, or closes after the last one.</summary>
        public void Next()
        {
            if (!m_Open)
                return;
            if (m_Index >= m_Cards.Length - 1)
            {
                Close();
                return;
            }
            m_Index++;
            m_Direction = 1f;
            AudioManager.PlaySfx(Sound.UiClick);
            ShowCard();
        }

        /// <summary>Skip / done: the cards close and the game goes on.</summary>
        public void Close()
        {
            if (!m_Open)
                return;
            Hide();
            m_State.CloseCards();
        }

        private void Hide()
        {
            m_Open = false;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            AudioManager.PlaySfx(Sound.UiClose);
        }

        private void ShowCard()
        {
            if (m_Cards.Length == 0)
                return;
            Card card = m_Cards[m_Index];
            bool touch = DeviceInfo.IsMobile;
            m_Title.SetText(card.Title);
            m_Title.color = card.Accent;
            m_Text.SetText(touch && !string.IsNullOrEmpty(card.TouchText) ? card.TouchText : card.Text);
            Sprite icon = touch && card.TouchIcon != null ? card.TouchIcon : card.Icon;
            m_Icon.sprite = icon;
            m_Icon.enabled = icon != null;
            bool two = card.Icon2 != null && !(touch && card.TouchIcon != null);
            m_Icon2.sprite = card.Icon2;
            m_Icon2.gameObject.SetActive(two);
            RectTransform i1 = m_Icon.rectTransform;
            i1.anchoredPosition = new Vector2(two ? m_Icon1X : 0f, i1.anchoredPosition.y);
            string caption = touch && !string.IsNullOrEmpty(card.TouchCaption) ? card.TouchCaption : card.Caption;
            m_Caption.SetText(caption ?? string.Empty);
            m_Caption.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            Color back = card.Accent * 0.35f;
            back.a = 0.9f;
            m_PictureBack.color = back;
            m_Counter.SetText($"{m_Index + 1} / {m_Cards.Length}");
            m_NextLabel.SetText(m_Index >= m_Cards.Length - 1 ? m_LastText : m_NextText);
            for (int i = 0; i < m_Dots.Length; i++)
            {
                m_Dots[i].gameObject.SetActive(i < m_Cards.Length);
                m_Dots[i].color = i == m_Index ? card.Accent : m_DotIdle;
                m_Dots[i].rectTransform.localScale = Vector3.one * (i == m_Index ? 1.3f : 1f);
            }
            m_TurnedAt = Time.unscaledTime;
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / m_Fade);
            if (!m_Open)
                return;
            // The card slides in from the side it comes from and pops a little.
            float t = Mathf.Clamp01((Time.unscaledTime - m_TurnedAt) / 0.3f);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            m_Card.anchoredPosition = new Vector2((1f - ease) * 120f * m_Direction, m_Card.anchoredPosition.y);
            m_Card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, UiMotion.BackOut(t, 2f));
            Keyboard keys = Keyboard.current;
            if (keys == null)
                return;
            if (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                Next();
            else if (keys.escapeKey.wasPressedThisFrame)
                Close();
        }
    }
}
