using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// Roguelike level-up choice (HUD scene): whenever the run has a level-up waiting (<see cref="RunState"/>), the
    /// game pauses and three random cards from the <see cref="PerkPool"/> fly in; a click (or 1 / 2 / 3 on the
    /// keyboard) takes one. From <see cref="PerkPool.LegendaryFromLevel"/> on, the run's legendary card is in every hand
    /// until it is taken (<see cref="PerkPool.OfferLegendary"/>); the legendary card for an ad is taken only once the
    /// rewarded ad is watched. "ПОМЕНЯТЬ" deals a new hand for a rewarded ad.
    /// Several level-ups in a row deal a new hand each; the game goes on after the last one.
    /// Never shown once the run is over.
    /// </summary>
    public sealed class LevelUpScreen : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private PerkPool m_Pool;
        [SerializeField] private ProgressionConfig m_Config;
        [SerializeField] private PlayerStats m_Stats;
        [Tooltip("The screen (dim, title, cards); this component's object stays active to hear the run.")]
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Subtitle;
        [SerializeField] private PerkCardView[] m_Cards = System.Array.Empty<PerkCardView>();
        [Header("Reroll")]
        [Tooltip("\"ПОМЕНЯТЬ\" with the ad icon: deals a new hand for a rewarded ad.")]
        [SerializeField] private UnityEngine.UI.Button m_RerollButton;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.2f;
        [SerializeField, Min(0.01f)] private float m_CardTime = 0.35f;
        [SerializeField, Min(0f)] private float m_CardStagger = 0.09f;
        [SerializeField, Min(0.01f)] private float m_PickTime = 0.35f;

        private readonly List<Perk> m_Hand = new List<Perk>();
        private bool m_Open;
        private float m_DealTime;
        private int m_Picked = -1;
        private float m_PickStart;
        private bool m_WaitingForAd;

        /// <summary>The cards are on screen (tests).</summary>
        public bool IsOpen => m_Open;
        public IReadOnlyList<Perk> Hand => m_Hand;

        /// <summary>Raised when a hand is dealt (sound).</summary>
        public event System.Action Dealt;

        /// <summary>Raised when a card is taken (sound).</summary>
        public event System.Action<Perk> Picked;

        private void Awake()
        {
            for (int i = 0; i < m_Cards.Length; i++)
            {
                int index = i;
                m_Cards[i].Button.onClick.AddListener(() => Pick(index));
            }
            if (m_RerollButton != null)
                m_RerollButton.onClick.AddListener(() => Reroll());
            m_Root.alpha = 0f;
            m_Root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_Run.LevelUpsChanged += TryOpen;
            m_Run.OutcomeChanged += OnOutcome;
            m_Run.Changed += RefreshReroll;
        }

        private void OnDisable()
        {
            m_Run.LevelUpsChanged -= TryOpen;
            m_Run.OutcomeChanged -= OnOutcome;
            m_Run.Changed -= RefreshReroll;
            if (m_Open)
                GamePause.Set(GamePause.Reason.LevelUp, false);
        }

        private void Start() => TryOpen();

        private void TryOpen()
        {
            if (m_Open || m_Run.PendingLevelUps <= 0 || m_Run.IsOver)
                return;
            Deal();
        }

        private void Deal()
        {
            m_Pool.Draw(m_Run, Mathf.Min(m_Config.CardsPerLevelUp, m_Cards.Length), m_Hand);
            m_Pool.OfferLegendary(m_Run, m_Stats != null ? m_Stats.Level : 1, m_Hand);
            if (m_Hand.Count == 0)
            {
                while (m_Run.PendingLevelUps > 0)            // every perk is maxed out: nothing to offer
                    m_Run.ConsumeLevelUp();
                Close();
                return;
            }
            bool keys = !DeviceInfo.IsMobile;
            for (int i = 0; i < m_Cards.Length; i++)
            {
                bool used = i < m_Hand.Count;
                m_Cards[i].gameObject.SetActive(used);
                if (used)
                    m_Cards[i].Bind(m_Hand[i], m_Run.StacksOf(m_Hand[i]), i, keys);
            }
            m_Title.SetText("НОВЫЙ УРОВЕНЬ!");
            m_Subtitle.SetText("ВЫБЕРИТЕ УЛУЧШЕНИЕ");
            m_Picked = -1;
            m_DealTime = Time.unscaledTime;
            if (!m_Open)
            {
                m_Open = true;
                m_Root.gameObject.SetActive(true);
                m_Root.interactable = m_Root.blocksRaycasts = true;
                GamePause.Set(GamePause.Reason.LevelUp, true);
            }
            Animate();
            RefreshReroll();
            AudioManager.PlaySfx(Sound.LevelUp);
            Dealt?.Invoke();
        }

        /// <summary>
        /// A new hand of cards for a rewarded ad (button, tests): dealt when the platform confirms the reward (a closed or
        /// failed video changes nothing); false when a new hand cannot be asked for now.
        /// </summary>
        public bool Reroll()
        {
            if (!m_Open || m_Picked >= 0 || m_WaitingForAd)
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                return false;
            }
            m_WaitingForAd = true;
            RefreshReroll();
            YandexSdk.ShowRewardedAd(rewarded =>
            {
                m_WaitingForAd = false;
                if (this == null)
                    return;
                if (rewarded && m_Open && m_Picked < 0 && m_Run.TryReroll(0))
                {
                    Debug.Log($"[LevelUpScreen] Rerolled for an ad (#{m_Run.Rerolls}).");
                    Deal();
                }
                else
                    RefreshReroll();
            });
            return true;
        }

        private void RefreshReroll()
        {
            if (m_RerollButton != null)
                m_RerollButton.interactable = m_Open && m_Picked < 0 && !m_WaitingForAd;
        }

        /// <summary>
        /// Takes the card at <paramref name="index"/> (clicks, number keys, tests); the card for an ad is taken when the
        /// platform confirms the reward (a closed or failed video leaves the hand as it is).
        /// </summary>
        public void Pick(int index)
        {
            if (!m_Open || m_Picked >= 0 || m_WaitingForAd || index < 0 || index >= m_Hand.Count)
                return;
            if (Time.unscaledTime - m_DealTime < m_CardTime * 0.5f)
                return;                                            // a click still meant for the previous hand
            if (m_Hand[index].ForAd)
            {
                Perk wanted = m_Hand[index];
                m_WaitingForAd = true;
                RefreshReroll();
                YandexSdk.ShowRewardedAd(rewarded =>
                {
                    m_WaitingForAd = false;
                    if (this == null)
                        return;
                    if (rewarded && m_Open && m_Picked < 0 && index < m_Hand.Count && m_Hand[index] == wanted)
                        Choose(index);
                    else
                        RefreshReroll();
                });
                return;
            }
            Choose(index);
        }

        private void Choose(int index)
        {
            m_Picked = index;
            m_PickStart = Time.unscaledTime;
            RefreshReroll();
            AudioManager.PlaySfx(m_Hand[index].Legendary ? Sound.LegendaryDrop : Sound.CardPick);
            Picked?.Invoke(m_Hand[index]);
        }

        private void Update()
        {
            if (!m_Open)
            {
                if (m_Root.gameObject.activeSelf)
                {
                    m_Root.alpha = Mathf.MoveTowards(m_Root.alpha, 0f, Time.unscaledDeltaTime / m_FadeTime);
                    if (m_Root.alpha <= 0f)
                        m_Root.gameObject.SetActive(false);
                }
                return;
            }
            m_Root.alpha = Mathf.MoveTowards(m_Root.alpha, 1f, Time.unscaledDeltaTime / m_FadeTime);
            ReadKeys();
            Animate();
            if (m_Picked >= 0 && Time.unscaledTime - m_PickStart >= m_PickTime)
                Apply();
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || m_Picked >= 0)
                return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Pick(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Pick(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Pick(2);
        }

        private void Animate()
        {
            float now = Time.unscaledTime;
            float pick = m_Picked >= 0 ? Mathf.Clamp01((now - m_PickStart) / m_PickTime) : 0f;
            for (int i = 0; i < m_Hand.Count && i < m_Cards.Length; i++)
            {
                float appear = Mathf.Clamp01((now - m_DealTime - i * m_CardStagger) / m_CardTime);
                bool chosen = i == m_Picked;
                m_Cards[i].Animate(appear, chosen ? pick : 0f, m_Picked >= 0 && !chosen ? pick : 0f);
            }
        }

        private void Apply()
        {
            Perk perk = m_Hand[m_Picked];
            m_Picked = -1;
            m_Run.TakePerk(perk);
            m_Run.ConsumeLevelUp();
            Debug.Log($"[LevelUpScreen] Took {perk.Id} ({perk.Title}), stacks {m_Run.StacksOf(perk)}.");
            if (m_Run.PendingLevelUps > 0 && !m_Run.IsOver)
                Deal();
            else
                Close();
        }

        private void Close()
        {
            if (!m_Open)
                return;
            m_Open = false;
            m_Hand.Clear();
            m_Root.interactable = m_Root.blocksRaycasts = false;
            GamePause.Set(GamePause.Reason.LevelUp, false);
        }

        private void OnOutcome()
        {
            if (m_Run.IsOver)
                Close();
        }
    }
}
