using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ЭТАП 1 ИЗ 3 ПРОЙДЕН!" (HUD, Easy and Normal): after the boss of wave 10 or 20 the run is saved
    /// (<see cref="RunState.StageCleared"/>); the game waits while this shows the stage's coins and "ПРОГРЕСС СОХРАНЁН"
    /// with "ПРОДОЛЖИТЬ" (the break goes on, the next stage follows) and "В МЕНЮ" (a logical break for a fullscreen ad;
    /// the run goes on from wave 11 / 21 next time). It waits for the level-up cards to be closed first.
    /// </summary>
    public sealed class StageClearScreen : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Coins;
        [SerializeField] private TMP_Text m_Next;
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_MenuButton;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.25f;

        private int m_Waiting = -1;             // the next wave of a stage cleared, until the screen can show
        private int m_WaitingCoins;
        private bool m_Open;
        private bool m_Leaving;
        private float m_OpenedAt;

        public bool IsOpen => m_Open;

        /// <summary>Feature tests that time the bosses: the run is still saved, the window never comes.</summary>
        public static bool Suppressed { get; set; }

        private void Awake()
        {
            m_ContinueButton.onClick.AddListener(Continue);
            m_MenuButton.onClick.AddListener(ToMenu);
            m_Root.alpha = 0f;
            m_Root.gameObject.SetActive(false);
        }

        private void OnEnable() => m_Run.StageCleared += OnStageCleared;

        private void OnDisable()
        {
            m_Run.StageCleared -= OnStageCleared;
            if (m_Open)
                GamePause.Set(GamePause.Reason.Stage, false);
        }

        private void OnStageCleared(int nextWave, int coins)
        {
            if (Suppressed)
                return;
            m_Waiting = nextWave;
            m_WaitingCoins = coins;
        }

        private void Update()
        {
            if (m_Waiting > 0 && !m_Open && !m_Run.IsOver && !GamePause.Has(GamePause.Reason.LevelUp))
                Open(m_Waiting, m_WaitingCoins);
            m_Root.alpha = Mathf.MoveTowards(m_Root.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / m_FadeTime);
            if (m_Open)
                m_Window.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, UiMotion.BackOut((Time.unscaledTime - m_OpenedAt) / 0.4f, 1.8f));
            else if (m_Root.gameObject.activeSelf && m_Root.alpha <= 0f)
                m_Root.gameObject.SetActive(false);
        }

        private void Open(int nextWave, int coins)
        {
            m_Waiting = -1;
            m_Open = true;
            m_OpenedAt = Time.unscaledTime;
            int stage = System.Array.IndexOf(DifficultyRules.CheckpointWaves, nextWave) + 1;
            m_Title.SetText($"ЭТАП {stage} ИЗ {DifficultyRules.CheckpointWaves.Length + 1} ПРОЙДЕН!");
            m_Coins.SetText($"+{coins}");
            m_Next.SetText($"ПРОГРЕСС СОХРАНЁН • ДАЛЬШЕ ВОЛНЫ {nextWave}–{nextWave + 9}");
            m_Root.gameObject.SetActive(true);
            m_Root.interactable = m_Root.blocksRaycasts = true;
            m_ContinueButton.interactable = m_MenuButton.interactable = true;
            GamePause.Set(GamePause.Reason.Stage, true);
            AudioManager.PlaySfx(Sound.LegendaryDrop);
        }

        /// <summary>Back to the game: the break goes on (buttons, tests).</summary>
        public void Continue()
        {
            if (!m_Open || m_Leaving)
                return;
            m_Open = false;
            m_Root.interactable = m_Root.blocksRaycasts = false;
            GamePause.Set(GamePause.Reason.Stage, false);
            AudioManager.PlaySfx(Sound.UiClick);
        }

        /// <summary>To the main menu: the run goes on from its checkpoint next time (buttons, tests).</summary>
        public void ToMenu()
        {
            if (!m_Open || m_Leaving)
                return;
            m_Leaving = true;
            m_ContinueButton.interactable = m_MenuButton.interactable = false;
            AudioManager.PlaySfx(Sound.UiClick);
            YandexSdk.ShowFullscreenAd(() => SceneLoader.EnsureExists().LoadMainMenu());
        }
    }
}
