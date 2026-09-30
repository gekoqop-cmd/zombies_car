using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// End-of-run screen (HUD scene), shown when <see cref="RunState"/> gets its outcome.
    /// Victory (after the finale, <see cref="VictoryFinaleView"/>): a medal and "ПОБЕДА!", then the lines of the run one
    /// by one — time (with the best time), zombies, bosses 3/3, level — and the coins step by step: for the zombies,
    /// for the waves, the scrap left exchanged ("ОСТАТОК ДЕТАЛЕЙ 2000 → +80"), the victory reward, and the total that
    /// counts up while coins fly into it; "К КЕЙСАМ" (the menu with the cases open), "ЗАНОВО", "В МЕНЮ".
    /// Defeat: "ИГРА ОКОНЧЕНА" with the reason, "ДОШЁЛ ДО ВОЛНЫ 17 ИЗ 30" over the wave line with the boss marks, how
    /// many waves were left to the next boss, the run's zombies / level / coins and the scrap exchanged; "ВОСКРЕСНУТЬ"
    /// with the ad icon once per run (a rewarded ad, then <see cref="RunState.Revive"/> and the screen goes away),
    /// "ЗАНОВО", "В МЕНЮ" (both a logical break for a fullscreen ad).
    /// Both: "×2" with the ad icon doubles the run's coins once (<see cref="DoubleCoins"/>), and the player's coin
    /// balance (coin icon + number) rolls up by what the run paid, a "+N" rising from it. Coins are paid into
    /// <see cref="MetaProgress"/> when the screen shows (only what a revived run had not been paid yet).
    /// </summary>
    public sealed class GameOverScreen : MonoBehaviour
    {
        private const int c_StatLines = 4;
        private const int c_CoinLines = 5;

        [SerializeField] private RunState m_Run;
        [SerializeField] private PlayerStats m_Stats;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Reason;
        [SerializeField] private Button m_RestartButton;
        [SerializeField] private Button m_MenuButton;

        [Header("Victory")]
        [SerializeField] private GameObject m_VictoryPanel;
        [SerializeField] private RectTransform m_Medal;
        [Tooltip("Time, zombies, bosses, level: shown one by one.")]
        [SerializeField] private CanvasGroup[] m_StatLines = new CanvasGroup[c_StatLines];
        [SerializeField] private TMP_Text m_TimeValue;
        [SerializeField] private TMP_Text m_KillsValue;
        [SerializeField] private TMP_Text m_BossesValue;
        [SerializeField] private TMP_Text m_LevelValue;
        [Tooltip("Coins for zombies, for waves, for the scrap left, the victory reward, the total.")]
        [SerializeField] private CanvasGroup[] m_CoinLines = new CanvasGroup[c_CoinLines];
        [SerializeField] private TMP_Text[] m_CoinValues = new TMP_Text[c_CoinLines];
        [Tooltip("Label of the scrap line: \"ОСТАТОК ДЕТАЛЕЙ 2000 →\".")]
        [SerializeField] private TMP_Text m_ScrapLabel;
        [Tooltip("Coin pictures that fly into the total.")]
        [SerializeField] private RectTransform[] m_FlyingCoins = System.Array.Empty<RectTransform>();
        [SerializeField] private Button m_CasesButton;
        [Tooltip("\"ТАБЛИЦА ЛИДЕРОВ\" (victory only: a defeat is not on the table).")]
        [SerializeField] private Button m_LeaderboardButton;
        [SerializeField] private LeaderboardPanel m_Leaderboard;

        [Header("Defeat")]
        [SerializeField] private GameObject m_DefeatPanel;
        [SerializeField] private TMP_Text m_Reached;
        [SerializeField] private Image m_WaveFill;
        [SerializeField] private RectTransform[] m_BossMarks = System.Array.Empty<RectTransform>();
        [SerializeField] private int[] m_BossWaves = { 10, 20, 30 };
        [SerializeField] private TMP_Text m_ToBoss;
        [Tooltip("Zombies, level, coins, the parts exchange: they pop in one after another.")]
        [SerializeField] private CanvasGroup[] m_DefeatTiles = System.Array.Empty<CanvasGroup>();
        [SerializeField] private TMP_Text m_DefeatKills;
        [SerializeField] private TMP_Text m_DefeatLevel;
        [SerializeField] private TMP_Text m_DefeatCoins;
        [Tooltip("The parts exchange: \"59 → +11\" between the parts and the coin icons.")]
        [SerializeField] private TMP_Text m_DefeatScrap;
        [Tooltip("\"ВОСКРЕСНУТЬ\" with the shared ad icon (the left bottom slot).")]
        [SerializeField] private Button m_ReviveButton;

        [Header("×2 coins and the balance")]
        [Tooltip("\"×2\" with the ad icon: doubles the run's coins for a rewarded ad, once a run.")]
        [SerializeField] private Button m_DoubleButton;
        [SerializeField] private TMP_Text m_DoubleLabel;
        [SerializeField] private GameObject m_DoubleIcon;
        [Tooltip("Where the ×2 button sits (window's top centre): by the total on a victory, right of the coin balance on a defeat.")]
        [SerializeField] private Vector2 m_DoubleVictoryPosition = new Vector2(380f, -636f);
        [SerializeField] private Vector2 m_DoubleDefeatPosition = new Vector2(140f, -726f);
        [SerializeField] private Vector2 m_DoubleVictorySize = new Vector2(240f, 84f);
        [SerializeField] private Vector2 m_DoubleDefeatSize = new Vector2(210f, 72f);
        [Tooltip("On a defeat the ×2 sits where the \"+N\" rises on a victory: there it rises left of the balance instead.")]
        [SerializeField] private Vector2 m_DefeatGainPosition = new Vector2(-370f, 176f);
        [Tooltip("On a defeat the balance moves left of centre: balance and ×2 together are centred.")]
        [SerializeField] private float m_DefeatBalanceX = -130f;
        [Tooltip("The player's coin balance (coin icon + number): counts up by what the run paid.")]
        [SerializeField] private TMP_Text m_Balance;
        [Tooltip("\"+N\" that rises from the balance as the coins come in.")]
        [SerializeField] private TMP_Text m_BalanceGain;

        [Header("Coins")]
        // Kept scarce: a case (100-150) takes a few runs, a full victory pays for about four.
        [Tooltip("Zombies killed per coin (the remainder is lost).")]
        [SerializeField, Min(1)] private int m_KillsPerCoin = 5;
        [Tooltip("Coins per wave reached.")]
        [SerializeField, Min(0)] private int m_CoinsPerWave = 2;
        [Tooltip("The reward for beating all 30 waves (on top of the kills and waves): about two cases.")]
        [SerializeField, Min(0)] private int m_VictoryCoins = 250;
        [Tooltip("Scrap left at the end is exchanged: this many parts give one coin (win or lose).")]
        [SerializeField, Min(1)] private int m_ScrapPerCoin = 50;

        [Header("Look")]
        [SerializeField] private Color m_DefeatColor = new Color(1f, 0.38f, 0.3f, 1f);
        [SerializeField] private Color m_VictoryColor = new Color(1f, 0.82f, 0.3f, 1f);
        [Tooltip("Real seconds after a defeat before the screen appears: almost at once, the death itself is not watched.")]
        [SerializeField, Min(0f)] private float m_Delay = 0.15f;
        [Tooltip("Real seconds after a victory: the finale plays first.")]
        [SerializeField, Min(0f)] private float m_VictoryDelay = 3.8f;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.35f;
        [SerializeField, Min(0.01f)] private float m_DefeatFadeTime = 0.12f;
        [Tooltip("Distance between the centres of the bottom buttons.")]
        [SerializeField, Min(0f)] private float m_ButtonStep = 390f;

        private float m_EndTime = float.PositiveInfinity;
        private bool m_Shown;
        private bool m_Victory;
        private bool m_Leaving;
        private int m_Kills, m_Level, m_Bosses, m_KillCoins, m_WaveCoins, m_ScrapCoins, m_Scrap, m_BonusCoins, m_Total;
        private float m_Time;
        private string m_TimeSuffix;
        private float m_Reach;
        private int m_LastStep = -1;
        private Vector2[] m_CoinHome;
        private int m_BalanceFrom, m_BalanceTo;
        private float m_BalanceAt = float.PositiveInfinity;           // unscaled time the balance starts counting
        private int m_TotalFrom;
        private float m_DoubledAt = float.NegativeInfinity;
        private Vector2 m_GainHome;
        private Vector2 m_GainStart;
        private Vector2 m_BalanceHome;
        private TextAlignmentOptions m_GainAlignment;
        private Vector2 m_DoubleSize;

        /// <summary>The ×2 button can be pressed (tests).</summary>
        public bool CanDouble => m_DoubleButton.gameObject.activeInHierarchy && m_DoubleButton.interactable;

        /// <summary>The balance number shown now (tests).</summary>
        public string BalanceText => m_Balance.text;

        /// <summary>The screen is up (tests).</summary>
        public bool IsShown => m_Shown;

        public string Title => m_Title.text;

        /// <summary>The revive button is offered (tests).</summary>
        public bool CanRevive => m_ReviveButton != null && m_ReviveButton.gameObject.activeInHierarchy && m_ReviveButton.interactable;

        private void Awake()
        {
            m_RestartButton.onClick.AddListener(Restart);
            m_MenuButton.onClick.AddListener(ToMenu);
            if (m_CasesButton != null)
                m_CasesButton.onClick.AddListener(ToCases);
            m_LeaderboardButton.onClick.AddListener(m_Leaderboard.Open);
            if (m_ReviveButton != null)
                m_ReviveButton.onClick.AddListener(Revive);
            m_DoubleButton.onClick.AddListener(DoubleCoins);
            m_GainHome = m_BalanceGain.rectTransform.anchoredPosition;
            m_GainAlignment = m_BalanceGain.alignment;
            m_BalanceHome = ((RectTransform)m_Balance.transform.parent).anchoredPosition;
            m_CoinHome = new Vector2[m_FlyingCoins.Length];
            for (int i = 0; i < m_FlyingCoins.Length; i++)
                m_CoinHome[i] = m_FlyingCoins[i].localPosition;
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
                // A fresh run or a revive: the screen goes away.
                m_EndTime = float.PositiveInfinity;
                m_Shown = false;
                m_Root.alpha = 0f;
                m_Root.interactable = m_Root.blocksRaycasts = false;
                m_Root.gameObject.SetActive(false);
                return;
            }
            if (m_EndTime < float.PositiveInfinity)
                return;
            m_EndTime = Time.unscaledTime;
        }

        private float Delay => m_Run.Outcome == RunOutcome.Victory ? m_VictoryDelay : m_Delay;

        private void Update()
        {
            if (!m_Shown && Time.unscaledTime >= m_EndTime + Delay)
                Show();
            if (!m_Shown)
                return;
            float t = Time.unscaledTime - m_EndTime - Delay;
            m_Root.alpha = Mathf.Clamp01(t / (m_Victory ? m_FadeTime : m_DefeatFadeTime));
            m_Window.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, UiMotion.BackOut(t / 0.4f, 1.6f));
            if (m_Victory)
                AnimateVictory(t);
            else
                AnimateDefeat(t);
            AnimateBalance();
        }

        private void Show()
        {
            m_Shown = true;
            m_Victory = m_Run.Outcome == RunOutcome.Victory;
            m_LastStep = -1;
            m_Title.SetText(m_Victory ? "ПОБЕДА!" : "ИГРА ОКОНЧЕНА");
            m_Title.color = m_Victory ? m_VictoryColor : m_DefeatColor;
            m_Reason.SetText(m_Run.Outcome switch
            {
                RunOutcome.CarDestroyed => "МАШИНА УНИЧТОЖЕНА",
                RunOutcome.PlayerDied => "ТЫ ПОГИБ",
                _ => "ВСЕ ВОЛНЫ ОТБИТЫ, ГОРОД СПАСЁН!",
            });
            m_Kills = m_Run.Kills;
            m_Level = m_Stats != null ? m_Stats.Level : 1;
            m_Bosses = m_Run.BossKills;
            m_KillCoins = m_Run.Kills / m_KillsPerCoin;
            m_WaveCoins = Mathf.Max(0, m_Run.Wave - 1) * m_CoinsPerWave;
            m_Scrap = m_Run.Money;
            m_ScrapCoins = ScrapCoins;
            m_BonusCoins = m_Victory ? m_VictoryCoins : 0;
            m_Total = m_TotalFrom = CoinsEarned;
            m_DoubledAt = float.NegativeInfinity;
            // Paid now; the balance counts it in once the coins are on screen (after the total on a victory).
            m_BalanceFrom = MetaProgress.Coins;
            MetaProgress.AddCoins(m_Total - m_Run.CoinsPaid);              // a revived run was paid for its first end
            m_Run.CoinsPaid = m_Total;
            m_BalanceTo = MetaProgress.Coins;
            m_BalanceAt = Time.unscaledTime + Delay + (m_Victory ? 1.8f + (c_CoinLines - 1) * 0.45f + 1f : 0.9f);
            m_Balance.SetText(m_BalanceFrom.ToString());
            m_BalanceGain.gameObject.SetActive(false);

            m_VictoryPanel.SetActive(m_Victory);
            m_DefeatPanel.SetActive(!m_Victory);
            // Both: the ad slot (cases / revive) on the left, again in the middle, menu on the right.
            PlaceButton(m_RestartButton, 0f);
            PlaceButton(m_MenuButton, m_ButtonStep);
            PlaceDoubleButton(m_Victory ? m_DoubleVictoryPosition : m_DoubleDefeatPosition,
                m_Victory ? m_DoubleVictorySize : m_DoubleDefeatSize);
            m_GainStart = m_Victory ? m_GainHome : m_DefeatGainPosition;
            m_BalanceGain.alignment = m_Victory ? m_GainAlignment : TextAlignmentOptions.Center;
            ((RectTransform)m_Balance.transform.parent).anchoredPosition =
                m_Victory ? m_BalanceHome : new Vector2(m_DefeatBalanceX, m_BalanceHome.y);
            ShowDoubleButton();
            if (m_Victory)
                SetupVictory();
            else
                SetupDefeat();
            m_Root.gameObject.SetActive(true);
            m_Root.interactable = m_Root.blocksRaycasts = true;
            m_RestartButton.interactable = m_MenuButton.interactable = true;
            if (m_CasesButton != null)
                m_CasesButton.interactable = true;
            if (!m_Victory)
                AudioManager.PlaySfx(Sound.Defeat);                             // the victory fanfare played in the finale
        }

        private static void PlaceButton(Button button, float x)
        {
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }

        /// <summary>
        /// Coins this run pays for the cases: kills, the wave reached (the first counts 0), the scrap left exchanged
        /// (<see cref="ScrapCoins"/>), a victory bonus — all of it twice after the ×2 for an ad.
        /// </summary>
        public int CoinsEarned => (m_Run.Kills / m_KillsPerCoin + Mathf.Max(0, m_Run.Wave - 1) * m_CoinsPerWave + ScrapCoins +
                                   (m_Run.Outcome == RunOutcome.Victory ? m_VictoryCoins : 0)) * (m_Run.CoinsDoubled ? 2 : 1);

        // ---------------------------------------------------------------- ×2 coins, the balance
        /// <summary>
        /// Doubles the run's coins for a rewarded ad (buttons, tests): only when the platform confirmed the reward —
        /// then the total counts up to twice, the extra goes to <see cref="MetaProgress"/> and the button says
        /// "ИСПОЛЬЗОВАНО"; a closed or failed video changes nothing and the button can be pressed again.
        /// </summary>
        public void DoubleCoins()
        {
            if (m_Leaving || !m_Shown || m_Run.CoinsDoubled)
                return;
            m_DoubleButton.interactable = false;
            YandexSdk.ShowRewardedAd(rewarded =>
            {
                if (this == null)
                    return;
                if (!rewarded || m_Run.CoinsDoubled)
                {
                    ShowDoubleButton();
                    return;
                }
                m_Run.CoinsDoubled = true;
                m_TotalFrom = m_Total;
                m_Total = CoinsEarned;
                m_BalanceFrom = ShownBalance();
                MetaProgress.AddCoins(m_Total - m_Run.CoinsPaid);
                m_Run.CoinsPaid = m_Total;
                m_BalanceTo = MetaProgress.Coins;
                m_DoubledAt = Time.unscaledTime;
                m_BalanceAt = Time.unscaledTime + 0.6f;
                AudioManager.PlaySfx(Sound.UiBuy);
                Debug.Log($"[GameOverScreen] Coins doubled for an ad: +{m_Total} in all.");
                ShowDoubleButton();
            });
        }

        private void PlaceDoubleButton(Vector2 position, Vector2 size)
        {
            var rt = (RectTransform)m_DoubleButton.transform;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            m_DoubleSize = size;
            float icon = size.y * 0.78f;
            var iconRt = (RectTransform)m_DoubleIcon.transform;
            iconRt.sizeDelta = new Vector2(icon, icon);
            iconRt.anchoredPosition = new Vector2(12f + icon * 0.5f, 0f);
        }

        // Ready: the ad icon on the left, "×2" in the rest. Used: no icon, "ИСПОЛЬЗОВАНО" centred over the whole plate.
        private void ShowDoubleButton()
        {
            bool used = m_Run.CoinsDoubled;
            m_DoubleButton.interactable = !used && !m_Leaving;
            m_DoubleIcon.SetActive(!used);
            float icon = used ? 0f : m_DoubleSize.y * 0.78f;
            RectTransform label = m_DoubleLabel.rectTransform;
            label.anchoredPosition = new Vector2(used ? 12f : 20f + icon, 0f);
            label.sizeDelta = new Vector2(m_DoubleSize.x - icon - (used ? 24f : 34f), m_DoubleSize.y);
            m_DoubleLabel.alignment = TextAlignmentOptions.Center;
            m_DoubleLabel.fontSizeMin = 14f;
            m_DoubleLabel.fontSizeMax = m_DoubleSize.y * (used ? 0.36f : 0.6f);
            m_DoubleLabel.SetText(used ? "ИСПОЛЬЗОВАНО" : "×2");
        }

        // The total after the ×2: counts from the first sum up to the doubled one.
        private int ShownTotal()
        {
            float k = UiMotion.CubicOut((Time.unscaledTime - m_DoubledAt) / 0.8f);
            return Mathf.RoundToInt(Mathf.Lerp(m_TotalFrom, m_Total, k));
        }

        private int ShownBalance()
        {
            float k = UiMotion.CubicOut((Time.unscaledTime - m_BalanceAt) / 1f);
            return Mathf.RoundToInt(Mathf.Lerp(m_BalanceFrom, m_BalanceTo, k));
        }

        // The balance rolls up by what came in, with "+N" rising from it and fading.
        private void AnimateBalance()
        {
            m_Balance.SetText(ShownBalance().ToString());
            float g = (Time.unscaledTime - m_BalanceAt) / 1.4f;
            bool gain = g >= 0f && g < 1f && m_BalanceTo > m_BalanceFrom;
            m_BalanceGain.gameObject.SetActive(gain);
            if (!gain)
                return;
            m_BalanceGain.SetText($"+{m_BalanceTo - m_BalanceFrom}");
            m_BalanceGain.rectTransform.anchoredPosition = m_GainStart + Vector2.up * (m_Victory ? 60f : 30f) * UiMotion.CubicOut(g);
            Color c = m_BalanceGain.color;
            c.a = 1f - g * g;
            m_BalanceGain.color = c;
        }

        /// <summary>The scrap left at the end exchanged for coins (<see cref="m_ScrapPerCoin"/> parts per coin).</summary>
        public int ScrapCoins => m_Run.Money / m_ScrapPerCoin;

        // ---------------------------------------------------------------- victory
        private void SetupVictory()
        {
            m_Time = m_Run.PlayTime;
            string map = m_Run.Map != null ? m_Run.Map.name : null;
            float best = map != null ? MetaProgress.BestTime(map) : 0f;
            m_TimeSuffix = best > 0f && Mathf.Abs(best - m_Time) < 0.5f ? "  РЕКОРД!" : best > 0f ? $"  (ЛУЧШЕЕ {MetaProgress.FormatTime(best)})" : string.Empty;
            foreach (CanvasGroup line in m_StatLines)
                line.alpha = 0f;
            foreach (CanvasGroup line in m_CoinLines)
                line.alpha = 0f;
            m_ScrapLabel.SetText($"ОСТАТОК ДЕТАЛЕЙ {m_Scrap} →");
            foreach (RectTransform coin in m_FlyingCoins)
                coin.gameObject.SetActive(false);
        }

        private void AnimateVictory(float t)
        {
            m_Medal.localScale = Vector3.one * UiMotion.BackOut((t - 0.1f) / 0.45f, 2.4f);
            m_Medal.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2f) * 4f);
            // The run's lines, one by one.
            for (int i = 0; i < m_StatLines.Length; i++)
            {
                float s = t - (0.45f + i * 0.3f);
                Reveal(m_StatLines[i], s, i);
                float k = UiMotion.CubicOut(s / 0.5f);
                switch (i)
                {
                    case 0: m_TimeValue.SetText(MetaProgress.FormatTime(m_Time * k) + (k >= 1f ? m_TimeSuffix : string.Empty)); break;
                    case 1: m_KillsValue.SetText(Mathf.RoundToInt(m_Kills * k).ToString()); break;
                    case 2: m_BossesValue.SetText($"{Mathf.RoundToInt(m_Bosses * k)} / {m_BossWaves.Length}"); break;
                    case 3: m_LevelValue.SetText(Mathf.RoundToInt(Mathf.Max(1, m_Level * k)).ToString()); break;
                }
            }
            // The coins, step by step, then the total with the coins flying into it.
            int[] values = { m_KillCoins, m_WaveCoins, m_ScrapCoins, m_BonusCoins, m_Total };
            for (int i = 0; i < m_CoinLines.Length; i++)
            {
                float s = t - (1.8f + i * 0.45f);
                Reveal(m_CoinLines[i], s, 10 + i);
                float k = UiMotion.CubicOut(s / (i == c_CoinLines - 1 ? 0.9f : 0.4f));
                bool total = i == c_CoinLines - 1;
                m_CoinValues[i].SetText(total && m_DoubledAt > 0f ? $"+{ShownTotal()}" : $"+{Mathf.RoundToInt(values[i] * k)}");
            }
            float fly = t - (1.8f + (c_CoinLines - 1) * 0.45f);
            RectTransform target = m_CoinValues[c_CoinLines - 1].rectTransform;
            for (int i = 0; i < m_FlyingCoins.Length; i++)
            {
                float c = (fly - i * 0.06f) / 0.5f;
                RectTransform coin = m_FlyingCoins[i];
                coin.gameObject.SetActive(c > 0f && c < 1f);
                if (c <= 0f || c >= 1f)
                    continue;
                Vector2 to = (Vector2)coin.parent.InverseTransformPoint(target.position);
                Vector2 from = m_CoinHome[i];
                float e = c * c;
                coin.localPosition = Vector2.Lerp(from, to, e) + Vector2.up * Mathf.Sin(c * Mathf.PI) * 120f;
                coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, e);
            }
            if (fly > 0.9f && m_LastStep < 99)
            {
                m_LastStep = 99;
                AudioManager.PlaySfx(Sound.UiBuy);
            }
        }

        // A line fades and slides in from the left; a click sounds as it lands.
        private void Reveal(CanvasGroup line, float s, int step)
        {
            float k = Mathf.Clamp01(s / 0.25f);
            line.alpha = k;
            var rt = (RectTransform)line.transform;
            rt.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, UiMotion.BackOut(k, 2f));
            if (s > 0f && step > m_LastStep && m_LastStep < 99)
            {
                m_LastStep = step;
                AudioManager.PlaySfx(Sound.UiClick);
            }
        }

        // ---------------------------------------------------------------- defeat
        private void SetupDefeat()
        {
            int count = m_Waves != null && m_Waves.WaveCount > 0 ? m_Waves.WaveCount : 30;
            int wave = Mathf.Clamp(m_Run.Wave, 1, count);
            m_Reached.SetText($"ДОШЁЛ ДО ВОЛНЫ {wave} ИЗ {count}");
            m_Reach = (float)wave / count;
            m_WaveFill.fillAmount = 0f;
            int next = -1;
            for (int i = 0; i < m_BossWaves.Length; i++)
            {
                // Bosses already beaten are dimmed; the first one still standing is the next.
                bool beaten = m_BossWaves[i] < wave || (m_BossWaves[i] == wave && m_Bosses > i);
                if (!beaten && next < 0)
                    next = m_BossWaves[i];
                var group = i < m_BossMarks.Length ? m_BossMarks[i].GetComponent<CanvasGroup>() : null;
                if (group != null)
                    group.alpha = beaten ? 0.4f : 1f;
            }
            int left = next - wave;
            m_ToBoss.SetText(next < 0 ? "ВСЕ БОССЫ ПОВЕРЖЕНЫ!"
                : left == 0 ? "ТЫ ДОБРАЛСЯ ДО БОССА!"
                : $"ДО БОССА: {left} {WaveWord(left)}");
            m_DefeatKills.SetText(m_Kills.ToString());
            m_DefeatLevel.SetText(m_Level.ToString());
            m_DefeatCoins.SetText($"+{m_Total}");
            m_DefeatScrap.SetText($"{m_Scrap} → +{m_ScrapCoins}");
            if (m_ReviveButton != null)
            {
                m_ReviveButton.gameObject.SetActive(m_Run.CanRevive);
                m_ReviveButton.interactable = m_Run.CanRevive;
            }
        }

        private static string WaveWord(int n)
        {
            int d = n % 10, h = n % 100;
            return d == 1 && h != 11 ? "ВОЛНА" : d >= 2 && d <= 4 && (h < 12 || h > 14) ? "ВОЛНЫ" : "ВОЛН";
        }

        private void AnimateDefeat(float t)
        {
            m_WaveFill.fillAmount = m_Reach * UiMotion.CubicOut((t - 0.3f) / 0.9f);
            if (m_DoubledAt > 0f)
                m_DefeatCoins.SetText($"+{ShownTotal()}");
            // The tiles pop in one by one while the wave line fills.
            for (int i = 0; i < m_DefeatTiles.Length; i++)
            {
                float k = Mathf.Clamp01((t - 0.35f - i * 0.12f) / 0.3f);
                m_DefeatTiles[i].alpha = k;
                m_DefeatTiles[i].transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, UiMotion.BackOut(k));
            }
            if (m_ReviveButton != null && m_ReviveButton.gameObject.activeSelf)
                m_ReviveButton.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f));
            if (CanDouble)
            {
                // Breathes, and every two seconds gives a little shake to catch the eye.
                float now = Time.unscaledTime;
                float shake = Mathf.Repeat(now, 2f) < 0.35f ? Mathf.Sin(now * 60f) * 6f * (1f - Mathf.Repeat(now, 2f) / 0.35f) : 0f;
                m_DoubleButton.transform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(now * 4f + 1f));
                m_DoubleButton.transform.localRotation = Quaternion.Euler(0f, 0f, shake);
            }
            else
            {
                m_DoubleButton.transform.localRotation = Quaternion.identity;
                m_DoubleButton.transform.localScale = Vector3.one;
            }
        }

        // ---------------------------------------------------------------- buttons
        /// <summary>Watches a rewarded ad and, when it paid out, takes the defeat back (buttons, tests).</summary>
        public void Revive()
        {
            if (m_Leaving || !m_Run.CanRevive)
                return;
            m_ReviveButton.interactable = false;
            YandexSdk.ShowRewardedAd(rewarded =>
            {
                if (rewarded && m_Run.Revive())
                    AudioManager.PlaySfx(Sound.UiBuy);
                else if (m_ReviveButton != null)
                    m_ReviveButton.interactable = m_Run.CanRevive;
            });
        }

        /// <summary>Plays the same map again (buttons, tests).</summary>
        public void Restart()
        {
            if (m_Leaving || m_Run.Map == null)
                return;
            Leave();
            MapConfig map = m_Run.Map;
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMap(map));
        }

        /// <summary>Back to the main menu (buttons, tests).</summary>
        public void ToMenu()
        {
            if (m_Leaving)
                return;
            Leave();
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMainMenu());
        }

        /// <summary>The main menu with the cases open (spend the reward).</summary>
        public void ToCases()
        {
            if (m_Leaving)
                return;
            MetaProgress.OpenCasesOnMenu = true;
            ToMenu();
        }

        private void Leave()
        {
            m_Leaving = true;
            m_RestartButton.interactable = m_MenuButton.interactable = false;
            if (m_CasesButton != null)
                m_CasesButton.interactable = false;
            if (m_ReviveButton != null)
                m_ReviveButton.interactable = false;
            m_DoubleButton.interactable = false;
        }
    }
}
