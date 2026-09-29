using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// The screen before every boss fight (HUD): the game waits, "БОСС · ВОЛНА N" and the boss's name come down, then three
    /// dark ability cards flip in one by one — a picture of the move (the boss's frame over its effect) and what it does.
    /// The game waits until "В БОЙ!" is pressed (or Enter / Space): the fight never starts on its own. Bosses are
    /// matched by their display name. Runs on unscaled time.
    /// </summary>
    public sealed class BossIntroView : MonoBehaviour
    {
        [Serializable]
        public sealed class Ability
        {
            public string Title;
            [TextArea] public string Text;
            public Sprite Pose;
            public Texture Fx;
            public Color FxColor = Color.white;
            public bool SecondPhase;
        }

        [Serializable]
        public sealed class Entry
        {
            public string Name;
            public Color Accent = new Color(0.86f, 0.18f, 0.16f, 1f);
            public Ability[] Abilities = Array.Empty<Ability>();
        }

        [Serializable]
        public sealed class Card
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Frame;
            public Image PictureBack;
            public RawImage Fx;
            public Image Pose;
            public TMP_Text Title;
            public TMP_Text Text;
            public GameObject PhaseBadge;
        }

        [SerializeField] private WaveState m_Waves;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Head;
        [SerializeField] private TMP_Text m_Header;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private Card[] m_Cards = Array.Empty<Card>();
        [SerializeField] private Button m_FightButton;
        [SerializeField] private Entry[] m_Bosses = Array.Empty<Entry>();
        [Tooltip("Real seconds before Enter / Space may start the fight (a key held from the fight is not taken as \"В БОЙ!\").")]
        [SerializeField, Min(0f)] private float m_KeyDelay = 1f;

        private const float c_CardDelay = 0.35f;
        private const float c_CardStep = 0.18f;
        private const float c_CardFlip = 0.4f;

        private bool m_Open;
        private float m_OpenedAt;

        /// <summary>No cards (feature tests and tools drive the bosses themselves).</summary>
        public static bool Suppressed { get; set; }

        public bool IsOpen => m_Open;

        private void Awake()
        {
            m_FightButton.onClick.AddListener(Close);
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
        }

        private void OnEnable() => m_Waves.BossAppeared += OnBoss;

        private void OnDisable()
        {
            m_Waves.BossAppeared -= OnBoss;
            if (m_Open)
                Close();
        }

        private void OnBoss(string name)
        {
            if (Suppressed)
                return;
            Entry entry = Array.Find(m_Bosses, e => e.Name == name);
            Color accent = entry != null ? entry.Accent : Color.red;
            m_Header.SetText($"БОСС  •  ВОЛНА {m_Waves.Wave}");
            m_Title.SetText(name);
            m_Title.color = accent;
            for (int i = 0; i < m_Cards.Length; i++)
            {
                Card card = m_Cards[i];
                Ability ability = entry != null && i < entry.Abilities.Length ? entry.Abilities[i] : null;
                card.Root.gameObject.SetActive(ability != null);
                if (ability == null)
                    continue;
                card.Frame.color = accent;
                Color back = accent * 0.35f;
                back.a = 1f;
                card.PictureBack.color = back;
                card.Pose.sprite = ability.Pose;
                card.Pose.enabled = ability.Pose != null;
                card.Fx.texture = ability.Fx;
                card.Fx.color = ability.FxColor;
                card.Fx.enabled = ability.Fx != null;
                card.Title.SetText(ability.Title);
                card.Title.color = accent;
                card.Text.SetText(ability.Text);
                card.PhaseBadge.SetActive(ability.SecondPhase);
                card.Group.alpha = 0f;
            }
            m_Open = true;
            m_OpenedAt = Time.unscaledTime;
            m_Group.blocksRaycasts = m_Group.interactable = true;
            GamePause.Set(GamePause.Reason.BossIntro, true);
            AudioManager.PlaySfx(Sound.BossRoar);
        }

        /// <summary>The fight begins (button, keys, tests).</summary>
        public void Close()
        {
            if (!m_Open)
                return;
            m_Open = false;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            GamePause.Set(GamePause.Reason.BossIntro, false);
            AudioManager.PlaySfx(Sound.UiClick);
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            if (!m_Open)
                return;
            float t = Time.unscaledTime - m_OpenedAt;
            // The name drops from above with a thump.
            m_Head.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, UiMotion.BackOut(Mathf.Clamp01(t / 0.3f)));
            for (int i = 0; i < m_Cards.Length; i++)
                AnimateCard(m_Cards[i], t - c_CardDelay - i * c_CardStep, i);

            Keyboard keys = Keyboard.current;
            // The boss waits for the player: only "В БОЙ!" (or Enter / Space) starts the fight, never a timer.
            if (keys != null && t > m_KeyDelay && (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame))
                Close();
        }

        /// <summary>Flips in around its vertical axis (a card turned face up), then the move plays: the effect spins, the boss sways.</summary>
        private static void AnimateCard(Card card, float t, int index)
        {
            float k = Mathf.Clamp01(t / c_CardFlip);
            card.Group.alpha = Mathf.Clamp01(k * 3f);
            card.Root.localScale = new Vector3(Mathf.Max(0.001f, UiMotion.BackOut(k)), 1f, 1f);
            card.Root.localRotation = Quaternion.Euler(0f, 0f, (1f - UiMotion.CubicOut(k)) * (index - 1) * 8f);
            float live = Mathf.Max(0f, t);
            card.Fx.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -live * 25f);
            card.Fx.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(live * 4f + index));
            card.Pose.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f * Mathf.Sin(live * 2.2f + index));
        }
    }
}
