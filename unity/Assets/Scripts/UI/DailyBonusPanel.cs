using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ЕЖЕДНЕВНЫЕ БОНУСЫ" (main menu): a row of 7 days, one bonus a day. Day 1 gives a free case (it waits on the
    /// cases screen, any case opens with it: <see cref="MetaProgress.FreeCases"/>), days 2-4 and 6 coins, day 5 opens the
    /// legendary case (a gold roulette of every legendary skin, a new one while any is missing), day 7 gives the black
    /// bullets, an exclusive skin no case drops (coins once it is owned); then the row starts again. A missed day does not reset the row: it waits on the same day (<see cref="MetaProgress.DailyDay"/>).
    /// Taken days show a tick, today's tile glows with "ЗАБРАТЬ", the next ones wait. Runs on unscaled time.
    /// </summary>
    public sealed class DailyBonusPanel : MonoBehaviour
    {
        public enum Kind { Coins, Case, LegendaryCase, Exclusive }

        public readonly struct Reward
        {
            public readonly Kind Kind;
            public readonly int Coins;

            public Reward(Kind kind, int coins)
            {
                Kind = kind;
                Coins = coins;
            }
        }

        /// <summary>The 7 days' bonuses.</summary>
        public static readonly Reward[] Days =
        {
            new Reward(Kind.Case, 0), new Reward(Kind.Coins, 35), new Reward(Kind.Coins, 50), new Reward(Kind.Coins, 70),
            new Reward(Kind.LegendaryCase, 0), new Reward(Kind.Coins, 120), new Reward(Kind.Exclusive, 0),
        };

        /// <summary>Coins on day 7 once the black bullets are owned.</summary>
        public const int ExclusiveOwnedCoins = 300;

        [Serializable]
        public sealed class Tile
        {
            public RectTransform Root;
            public Image Plate;
            public Image Glow;
            public TMP_Text Day;
            public TMP_Text Amount;
            public GameObject Tick;
        }

        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private Tile[] m_Tiles = Array.Empty<Tile>();
        [SerializeField] private Button m_TakeButton;
        [SerializeField] private TMP_Text m_TakeLabel;
        [SerializeField] private TMP_Text m_Status;
        [SerializeField] private Button m_CloseButton;
        [Tooltip("The free case, the legendary case and the black bullets come out on the cases screen.")]
        [SerializeField] private CasesScreen m_Cases;
        [SerializeField] private Color m_TakenColor = new Color(0.18f, 0.32f, 0.16f, 0.95f);
        [SerializeField] private Color m_TodayColor = new Color(0.55f, 0.36f, 0.08f, 0.97f);
        [SerializeField] private Color m_LaterColor = new Color(0.1f, 0.1f, 0.12f, 0.92f);

        private bool m_Open;
        private float m_OpenedAt;

        public bool IsOpen => m_Open;

        private void Awake()
        {
            m_CloseButton.onClick.AddListener(Close);
            m_TakeButton.onClick.AddListener(() => Take());
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            m_Open = true;
            m_OpenedAt = Time.unscaledTime;
            m_Group.blocksRaycasts = m_Group.interactable = true;
            AudioManager.PlaySfx(Sound.UiOpen);
            Refresh();
        }

        public void Close()
        {
            if (!m_Open)
                return;
            m_Open = false;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            AudioManager.PlaySfx(Sound.UiClose);
        }

        /// <summary>Takes today's bonus (button, tests); false when it was taken today already.</summary>
        public bool Take()
        {
            if (!MetaProgress.DailyReady)
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                return false;
            }
            Reward reward = Days[MetaProgress.DailyDay];
            MetaProgress.TakeDaily();
            switch (reward.Kind)
            {
                case Kind.Coins:
                    MetaProgress.AddCoins(reward.Coins);
                    AudioManager.PlaySfx(Sound.UiBuy);
                    Refresh();
                    break;
                case Kind.Case:
                    // The case waits on the cases screen: it opens there, the player picks which one.
                    MetaProgress.AddFreeCase();
                    AudioManager.PlaySfx(Sound.UiBuy);
                    Close();
                    if (m_Cases != null)
                        m_Cases.Show();
                    break;
                case Kind.LegendaryCase:
                    Close();
                    if (m_Cases != null)
                        m_Cases.OpenLegendaryCase();
                    break;
                case Kind.Exclusive:
                    CosmeticItem item = m_Cases != null ? m_Cases.Catalog.Exclusive(CosmeticSlot.Bullet) : null;
                    if (item == null || MetaProgress.Owns(item))
                    {
                        MetaProgress.AddCoins(ExclusiveOwnedCoins);
                        AudioManager.PlaySfx(Sound.UiBuy);
                        Refresh();
                        break;
                    }
                    Close();
                    m_Cases.GiveExclusive(item);
                    break;
            }
            return true;
        }

        public static string Describe(Reward reward) => reward.Kind switch
        {
            Kind.Case => "КЕЙС",
            Kind.LegendaryCase => "ЛЕГЕНДАРНЫЙ КЕЙС",
            Kind.Exclusive => "ЧЁРНЫЕ ПУЛИ",
            _ => reward.Coins.ToString(),
        };

        private void Refresh()
        {
            int today = MetaProgress.DailyDay;
            bool ready = MetaProgress.DailyReady;
            for (int i = 0; i < m_Tiles.Length && i < Days.Length; i++)
            {
                Tile tile = m_Tiles[i];
                // Before today's index: taken in this row; today's index once taken today: the next day waits on it.
                bool taken = i < today;
                tile.Tick.SetActive(taken);
                tile.Plate.color = taken ? m_TakenColor : i == today && ready ? m_TodayColor : m_LaterColor;
                tile.Day.SetText($"ДЕНЬ {i + 1}");
                tile.Amount.SetText(Describe(Days[i]));
            }
            m_TakeButton.interactable = ready;
            m_TakeLabel.SetText(ready ? "ЗАБРАТЬ" : "ЗАВТРА");
            m_Status.SetText(ready ? $"СЕГОДНЯ — ДЕНЬ {today + 1}" : "БОНУС ЗА СЕГОДНЯ ПОЛУЧЕН. ЗАХОДИ ЗАВТРА!");
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            m_Window.localScale = Vector3.one * (m_Open ? Mathf.Lerp(0.9f, 1f, UiMotion.BackOut((Time.unscaledTime - m_OpenedAt) / 0.3f, 1.6f)) : 1f);
            if (!m_Open && m_Group.alpha <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            float now = Time.unscaledTime;
            int today = MetaProgress.DailyDay;
            bool ready = MetaProgress.DailyReady;
            for (int i = 0; i < m_Tiles.Length; i++)
            {
                bool glowing = ready && i == today;
                Color glow = m_Tiles[i].Glow.color;
                glow.a = glowing ? 0.5f + 0.4f * Mathf.Sin(now * 4.5f) : 0f;
                m_Tiles[i].Glow.color = glow;
                m_Tiles[i].Root.localScale = Vector3.one * (glowing ? 1.06f + 0.04f * Mathf.Sin(now * 4.5f) : 1f);
            }
            m_TakeButton.transform.localScale = Vector3.one * (ready ? 1f + 0.05f * Mathf.Sin(now * 5f) : 1f);
        }
    }
}
