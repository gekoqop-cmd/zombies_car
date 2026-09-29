using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// End-of-run screen (HUD scene), shown when <see cref="RunState"/> gets its outcome: "ИГРА ОКОНЧЕНА" with the
    /// reason (car destroyed / player down; bosses killed) or "ПОБЕДА!" after the last wave, then the run's scrap
    /// earned, the level reached, zombies killed and the wave, counting up; "ЗАНОВО" plays the same map again, "В МЕНЮ" goes
    /// back to the main menu (both are a logical break for a fullscreen ad). Plays the victory or defeat stinger.
    /// </summary>
    public sealed class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private PlayerStats m_Stats;
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Reason;
        [SerializeField] private TMP_Text m_Money;
        [SerializeField] private TMP_Text m_Level;
        [SerializeField] private TMP_Text m_Kills;
        [SerializeField] private TMP_Text m_Wave;
        [Tooltip("Coins for the cases in the main menu earned by this run.")]
        [SerializeField] private TMP_Text m_Coins;
        [Tooltip("Coins per zombie killed, per wave reached and for a victory.")]
        [SerializeField, Min(0)] private int m_CoinsPerKill = 1;
        [SerializeField, Min(0)] private int m_CoinsPerWave = 10;
        [SerializeField, Min(0)] private int m_VictoryCoins = 300;
        [SerializeField] private Button m_RestartButton;
        [SerializeField] private Button m_MenuButton;
        [SerializeField] private Color m_DefeatColor = new Color(1f, 0.38f, 0.3f, 1f);
        [SerializeField] private Color m_VictoryColor = new Color(1f, 0.82f, 0.3f, 1f);
        [Tooltip("Real seconds after the end before the screen appears (the end is seen first).")]
        [SerializeField, Min(0f)] private float m_Delay = 1.2f;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.35f;
        [SerializeField, Min(0.01f)] private float m_CountTime = 1.1f;

        private float m_EndTime = float.PositiveInfinity;
        private bool m_Shown;
        private bool m_Leaving;
        private int m_MoneyTarget;
        private int m_KillsTarget;

        /// <summary>The screen is up (tests).</summary>
        public bool IsShown => m_Shown;

        public string Title => m_Title.text;

        private void Awake()
        {
            m_RestartButton.onClick.AddListener(Restart);
            m_MenuButton.onClick.AddListener(ToMenu);
            m_Root.alpha = 0f;
            m_Root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_Run.OutcomeChanged += OnOutcome;
            OnOutcome();
        }

        private void OnDisable() => m_Run.OutcomeChanged -= OnOutcome;

        private void OnOutcome()
        {
            if (!m_Run.IsOver)
            {
                m_EndTime = float.PositiveInfinity;
                m_Shown = false;
                return;
            }
            if (m_EndTime < float.PositiveInfinity)
                return;
            m_EndTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (!m_Shown && Time.unscaledTime >= m_EndTime + m_Delay)
                Show();
            if (!m_Shown)
                return;
            float t = Time.unscaledTime - m_EndTime - m_Delay;
            m_Root.alpha = Mathf.Clamp01(t / m_FadeTime);
            m_Window.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, UiMotion.BackOut(t / 0.4f, 1.6f));
            float count = UiMotion.CubicOut((t - 0.3f) / m_CountTime);
            m_Money.SetText(Mathf.RoundToInt(m_MoneyTarget * count).ToString());
            m_Kills.SetText(Mathf.RoundToInt(m_KillsTarget * count).ToString());
        }

        private void Show()
        {
            m_Shown = true;
            bool victory = m_Run.Outcome == RunOutcome.Victory;
            m_Title.SetText(victory ? "ПОБЕДА!" : "ИГРА ОКОНЧЕНА");
            m_Title.color = victory ? m_VictoryColor : m_DefeatColor;
            string reason = m_Run.Outcome switch
            {
                RunOutcome.CarDestroyed => "МАШИНА УНИЧТОЖЕНА",
                RunOutcome.PlayerDied => "ВЫ ПОГИБЛИ",
                _ => "ВСЕ ВОЛНЫ ОТБИТЫ, ГОРОД СПАСЁН!",
            };
            if (m_Run.BossKills > 0)
                reason += $"  •  БОССОВ ПОВЕРЖЕНО: {m_Run.BossKills}";
            m_Reason.SetText(reason);
            m_MoneyTarget = m_Run.MoneyEarned;
            m_KillsTarget = m_Run.Kills;
            m_Level.SetText(m_Stats != null ? m_Stats.Level.ToString() : "1");
            m_Wave.SetText(m_Run.Wave.ToString());
            m_Money.SetText("0");
            m_Kills.SetText("0");
            int coins = CoinsEarned;
            MetaProgress.AddCoins(coins);
            if (m_Coins != null)
                m_Coins.SetText($"+{coins} МОНЕТ ДЛЯ КЕЙСОВ  •  ВСЕГО {MetaProgress.Coins}");
            m_Root.gameObject.SetActive(true);
            m_Root.interactable = m_Root.blocksRaycasts = true;
            m_RestartButton.interactable = m_MenuButton.interactable = true;
            AudioManager.PlaySfx(victory ? Sound.Victory : Sound.Defeat);
        }

        /// <summary>Coins this run pays for the cases: kills, the wave reached (the first counts 0), a victory bonus.</summary>
        public int CoinsEarned => m_Run.Kills * m_CoinsPerKill + Mathf.Max(0, m_Run.Wave - 1) * m_CoinsPerWave +
                                  (m_Run.Outcome == RunOutcome.Victory ? m_VictoryCoins : 0);

        /// <summary>Plays the same map again (buttons, tests).</summary>
        public void Restart()
        {
            if (m_Leaving || m_Run.Map == null)
                return;
            m_Leaving = true;
            m_RestartButton.interactable = m_MenuButton.interactable = false;
            MapConfig map = m_Run.Map;
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMap(map));
        }

        /// <summary>Back to the main menu (buttons, tests).</summary>
        public void ToMenu()
        {
            if (m_Leaving)
                return;
            m_Leaving = true;
            m_RestartButton.interactable = m_MenuButton.interactable = false;
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMainMenu());
        }
    }
}
