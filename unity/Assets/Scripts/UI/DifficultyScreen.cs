using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ВЫБЕРИ СЛОЖНОСТЬ" (main menu, after a map's "ИГРАТЬ"): three cards — ЛЁГКИЙ (the kind face, green), СРЕДНИЙ
    /// (orange), ХАРДКОР (the demon, red) — with what each changes; a card with a saved run says so. Picking one:
    /// <list type="bullet">
    /// <item>Easy / Normal with a checkpoint: "ЕСТЬ СОХРАНЕНИЕ" — go on from wave 11 / 21, or start over (the save goes);</item>
    /// <item>Hardcore the first time: "без сохранений — вы уверены?" (ДА / НАЗАД), then "ТОЧНО?)" with one big shaking
    /// ДА and no way back; never asked again (<see cref="MetaProgress.HardcoreConfirmed"/>);</item>
    /// <item>then the map loads on that difficulty (<see cref="RunLaunch"/>) after a logical-break fullscreen ad.</item>
    /// </list>
    /// "КАК РАБОТАЮТ СОХРАНЕНИЯ?" under the cards opens a short explanation for the player ("ПОНЯТНО" closes it).
    /// Runs on unscaled time.
    /// </summary>
    public sealed class DifficultyScreen : MonoBehaviour
    {
        [Serializable]
        public sealed class Card
        {
            public Button Button;
            public RectTransform Body;
            public Image Glow;
            public GameObject SaveBadge;
            public TMP_Text SaveText;
        }

        private enum Step { Hidden, Cards, Resume, Warn, Sure, Leaving, Help }

        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private TMP_Text m_Subtitle;
        [Tooltip("ЛЁГКИЙ, СРЕДНИЙ, ХАРДКОР (the Difficulty order).")]
        [SerializeField] private Card[] m_Cards = Array.Empty<Card>();
        [SerializeField] private Button m_BackButton;

        [Header("How the saves work")]
        [SerializeField, OptionalReference] private Button m_HelpButton;
        [SerializeField, OptionalReference] private CanvasGroup m_HelpPanel;
        [SerializeField, OptionalReference] private Button m_HelpCloseButton;

        [Header("A saved run")]
        [SerializeField] private CanvasGroup m_ResumePanel;
        [SerializeField] private TMP_Text m_ResumeText;
        [SerializeField] private Button m_ResumeButton;
        [SerializeField] private Button m_RestartButton;
        [SerializeField] private Button m_ResumeBackButton;

        [Header("Hardcore, the first time")]
        [SerializeField] private CanvasGroup m_WarnPanel;
        [SerializeField] private Button m_WarnYesButton;
        [SerializeField] private Button m_WarnBackButton;
        [SerializeField] private CanvasGroup m_SurePanel;
        [SerializeField] private Button m_SureYesButton;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.2f;

        private Step m_Step = Step.Hidden;
        private MapConfig m_Map;
        private Difficulty m_Chosen;
        private float m_StepAt;

        public bool IsOpen => m_Step != Step.Hidden;

        /// <summary>What the screen asks now (tests): "cards", "resume", "warn", "sure", "help", "leaving", "hidden".</summary>
        public string State => m_Step.ToString().ToLowerInvariant();

        /// <summary>The run chosen last, as it was launched (tests).</summary>
        public Difficulty Chosen => m_Chosen;

        private void Awake()
        {
            for (int i = 0; i < m_Cards.Length; i++)
            {
                var difficulty = (Difficulty)i;
                m_Cards[i].Button.onClick.AddListener(() => Choose(difficulty));
            }
            m_BackButton.onClick.AddListener(Close);
            m_ResumeButton.onClick.AddListener(() => Launch(m_Chosen, true));
            m_RestartButton.onClick.AddListener(StartOver);
            m_ResumeBackButton.onClick.AddListener(Back);
            m_WarnYesButton.onClick.AddListener(Warned);
            m_WarnBackButton.onClick.AddListener(Back);
            m_SureYesButton.onClick.AddListener(ConfirmHardcore);
            if (m_HelpButton != null)
                m_HelpButton.onClick.AddListener(ShowHelp);
            if (m_HelpCloseButton != null)
                m_HelpCloseButton.onClick.AddListener(Back);
            m_Group.alpha = 0f;
            Apply(true);
        }

        /// <summary>Opens for <paramref name="map"/> ("ГОРОД • ДЕНЬ" in <paramref name="title"/>).</summary>
        public void Open(MapConfig map, string title)
        {
            if (map == null)
                return;
            m_Map = map;
            gameObject.SetActive(true);
            m_Subtitle.SetText(title);
            for (int i = 0; i < m_Cards.Length; i++)
            {
                MetaProgress.Checkpoint checkpoint = MetaProgress.GetCheckpoint(map.name, (Difficulty)i);
                m_Cards[i].SaveBadge.SetActive(checkpoint != null);
                if (checkpoint != null)
                    m_Cards[i].SaveText.SetText($"СОХРАНЕНИЕ: ВОЛНА {checkpoint.wave}");
            }
            AudioManager.PlaySfx(Sound.UiOpen);
            Go(Step.Cards);
        }

        public void Close()
        {
            if (m_Step == Step.Leaving || m_Step == Step.Hidden)
                return;
            AudioManager.PlaySfx(Sound.UiClose);
            Go(Step.Hidden);
        }

        /// <summary>A card was picked (buttons, tests).</summary>
        public void Choose(Difficulty difficulty)
        {
            if (m_Step != Step.Cards)
                return;
            m_Chosen = difficulty;
            AudioManager.PlaySfx(Sound.UiClick);
            MetaProgress.Checkpoint checkpoint = MetaProgress.GetCheckpoint(m_Map.name, difficulty);
            if (checkpoint != null)
            {
                m_ResumeText.SetText($"{DifficultyRules.Title(difficulty)}  •  ВОЛНА {checkpoint.wave} ИЗ 30\n" +
                                     "Прокачка, карточки и детали сохранены");
                Go(Step.Resume);
            }
            else if (difficulty == Difficulty.Hardcore && !MetaProgress.HardcoreConfirmed)
                Go(Step.Warn);
            else
                Launch(difficulty, false);
        }

        /// <summary>"С НАЧАЛА": the saved run goes, a new one starts at wave 1 (buttons, tests).</summary>
        public void StartOver()
        {
            if (m_Step != Step.Resume)
                return;
            MetaProgress.ClearCheckpoint(m_Map.name, m_Chosen);
            Launch(m_Chosen, false);
        }

        /// <summary>"НАЗАД" of the saved-run question or the first warning, "ПОНЯТНО" of the saves' help: back to the cards.</summary>
        public void Back()
        {
            if (m_Step == Step.Resume || m_Step == Step.Warn || m_Step == Step.Help)
                Go(Step.Cards);
        }

        /// <summary>"КАК РАБОТАЮТ СОХРАНЕНИЯ?" (button, tests).</summary>
        public void ShowHelp()
        {
            if (m_Step == Step.Cards && m_HelpPanel != null)
                Go(Step.Help);
        }

        /// <summary>The warning's "ДА" (buttons, tests).</summary>
        public void Warned()
        {
            if (m_Step == Step.Warn)
                Go(Step.Sure);
        }

        /// <summary>"ТОЧНО?)" — "ДА": Hardcore it is, and the warning never comes again (buttons, tests).</summary>
        public void ConfirmHardcore()
        {
            if (m_Step != Step.Sure)
                return;
            MetaProgress.HardcoreConfirmed = true;
            Launch(Difficulty.Hardcore, false);
        }

        private void Launch(Difficulty difficulty, bool resume)
        {
            if (m_Step == Step.Leaving || m_Map == null)
                return;
            m_Chosen = difficulty;
            Go(Step.Leaving);
            AudioManager.PlaySfx(Sound.UiBuy);
            MetaProgress.LastDifficulty = difficulty;
            RunLaunch.Choose(difficulty, resume);
            MapConfig map = m_Map;
            // Between the menu and the map: a logical break for a fullscreen ad (the platform limits how often).
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMap(map));
        }

        private void Go(Step step)
        {
            if (step != m_Step && (step == Step.Resume || step == Step.Warn || step == Step.Sure || step == Step.Help))
                AudioManager.PlaySfx(step == Step.Sure ? Sound.BossRoar : Sound.UiOpen);
            m_Step = step;
            m_StepAt = Time.unscaledTime;
            Apply(false);
        }

        private void Apply(bool instant)
        {
            bool open = m_Step != Step.Hidden;
            bool cards = m_Step == Step.Cards;
            m_Group.blocksRaycasts = m_Group.interactable = open && m_Step != Step.Leaving;
            Show(m_ResumePanel, m_Step == Step.Resume, instant);
            Show(m_WarnPanel, m_Step == Step.Warn, instant);
            Show(m_SurePanel, m_Step == Step.Sure, instant);
            if (m_HelpPanel != null)
                Show(m_HelpPanel, m_Step == Step.Help, instant);
            foreach (Card card in m_Cards)
                card.Button.interactable = cards;
            m_BackButton.interactable = cards;
            if (m_HelpButton != null)
                m_HelpButton.interactable = cards;
        }

        private static void Show(CanvasGroup panel, bool on, bool instant)
        {
            panel.blocksRaycasts = panel.interactable = on;
            if (instant)
                panel.alpha = on ? 1f : 0f;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float step = Time.unscaledDeltaTime / m_FadeTime;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Step == Step.Hidden ? 0f : 1f, step);
            if (m_Step == Step.Hidden && m_Group.alpha <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            Fade(m_ResumePanel, m_Step == Step.Resume, step);
            Fade(m_WarnPanel, m_Step == Step.Warn, step);
            Fade(m_SurePanel, m_Step == Step.Sure, step);
            if (m_HelpPanel != null)
                Fade(m_HelpPanel, m_Step == Step.Help, step);
            // The cards pop in one after another and breathe; the demon's glow throbs.
            float since = now - m_StepAt;
            for (int i = 0; i < m_Cards.Length; i++)
            {
                Card card = m_Cards[i];
                float appear = m_Step == Step.Cards ? UiMotion.BackOut((since - i * 0.08f) / 0.35f, 2f) : 1f;
                float breathe = 1f + 0.015f * Mathf.Sin(now * 2.2f + i * 1.3f);
                card.Body.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, Mathf.Clamp(appear, 0f, 1.2f)) * breathe;
                Color glow = card.Glow.color;
                glow.a = i == (int)Difficulty.Hardcore ? 0.55f + 0.3f * Mathf.Sin(now * 5f) : 0.45f + 0.15f * Mathf.Sin(now * 2f + i);
                card.Glow.color = glow;
            }
            // "ТОЧНО?)": the one "ДА" shakes and swells to be pressed.
            if (m_Step == Step.Sure)
            {
                var yes = (RectTransform)m_SureYesButton.transform;
                float shake = Mathf.Sin(now * 38f) * 7f * (0.6f + 0.4f * Mathf.Sin(now * 3f));
                yes.localRotation = Quaternion.Euler(0f, 0f, shake);
                yes.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(now * 6f) + 0.15f * (1f - Mathf.Clamp01(since / 0.3f)));
            }
        }

        private static void Fade(CanvasGroup panel, bool on, float step) =>
            panel.alpha = Mathf.MoveTowards(panel.alpha, on ? 1f : 0f, step);
    }
}
