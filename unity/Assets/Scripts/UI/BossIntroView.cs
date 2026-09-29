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
    /// The card that comes before every boss fight (HUD): when a boss comes out the game waits, and a card shows its
    /// portrait, "БОСС · ВОЛНА N", its name and how to beat it; "В БОЙ!" (or Enter / Space, or a few seconds) lets the
    /// fight begin. Bosses are matched by their display name. Runs on unscaled time.
    /// </summary>
    public sealed class BossIntroView : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string Name;
            public Sprite Portrait;
            [TextArea] public string Tip;
            public Color Accent = new Color(0.86f, 0.18f, 0.16f, 1f);
        }

        [SerializeField] private WaveState m_Waves;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Card;
        [SerializeField] private Image m_PortraitBack;
        [SerializeField] private Image m_Portrait;
        [SerializeField] private TMP_Text m_Header;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Tip;
        [SerializeField] private Button m_FightButton;
        [SerializeField] private Entry[] m_Bosses = Array.Empty<Entry>();
        [Tooltip("Real seconds before the fight starts by itself.")]
        [SerializeField, Min(1f)] private float m_AutoClose = 8f;

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
            m_Header.SetText($"БОСС  •  ВОЛНА {m_Waves.Wave}");
            m_Title.SetText(name);
            m_Title.color = entry != null ? entry.Accent : Color.white;
            m_Tip.SetText(entry != null ? entry.Tip : string.Empty);
            m_Portrait.sprite = entry != null ? entry.Portrait : null;
            m_Portrait.enabled = m_Portrait.sprite != null;
            Color back = (entry != null ? entry.Accent : Color.red) * 0.4f;
            back.a = 0.95f;
            m_PortraitBack.color = back;
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
            // Slides in from the right with a thump, the portrait breathes.
            m_Card.anchoredPosition = new Vector2((1f - UiMotion.CubicOut(t / 0.35f)) * 500f, m_Card.anchoredPosition.y);
            m_Portrait.rectTransform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 3f));
            Keyboard keys = Keyboard.current;
            bool key = keys != null && t > 0.4f &&
                       (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame);
            if (key || t >= m_AutoClose)
                Close();
        }
    }
}
