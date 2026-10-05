using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The tutorial of the first run. First the intro cards (the HUD's <c>TutorialCardsView</c>: goal, controls,
    /// weapons, the car, level-up cards, bosses; the game waits while they are open), then live hints on the HUD at the
    /// moment they matter: the first zombie, the gun running dry, the workshop opening after the first wave, the first
    /// level-up card. Every live hint shows once and goes as soon as the player did it (or after a few seconds). The
    /// cards are seen once per device (<see cref="MetaProgress.TutorialDone"/>) and the live hints only in the run that
    /// showed them; the settings' "Обучение" button replays both. Texts follow the device (keyboard and mouse or touch
    /// controls).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private enum Live { FirstZombie, NoAmmo, Workshop, FirstCard, Count }

        private struct Hint
        {
            public string Desktop;
            public string Touch;
            public float Timeout;      // seconds of game time; 0 = until done
        }

        private static readonly Hint[] s_Hints =
        {
            new Hint { Desktop = "ЗОМБИ! ЦЕЛЬСЯ МЫШЬЮ, ЛЕВАЯ КНОПКА — ОГОНЬ",
                Touch = "ЗОМБИ! ТЯНИ ПРАВЫЙ ДЖОЙСТИК — ПРИЦЕЛ И ОГОНЬ", Timeout = 7f },
            new Hint { Desktop = "ПАТРОНЫ КОНЧИЛИСЬ! ТОПОР — КЛАВИША 2. ПАТРОНЫ — У МАШИНЫ (E)",
                Touch = "ПАТРОНЫ КОНЧИЛИСЬ! БЕЙ ТОПОРОМ. ПАТРОНЫ — У МАШИНЫ", Timeout = 8f },
            new Hint { Desktop = "МАСТЕРСКАЯ ОТКРЫТА! У ГАРАЖА НАЖМИ E — УЛУЧШИ МАШИНУ ЗА МЕТАЛЛОЛОМ",
                Touch = "МАСТЕРСКАЯ ОТКРЫТА! ПОДОЙДИ К ГАРАЖУ — УЛУЧШИ МАШИНУ ЗА МЕТАЛЛОЛОМ", Timeout = 9f },
            new Hint { Desktop = "ВЫБЕРИ УЛУЧШЕНИЕ. «НОВОЕ» — ЕЩЁ НЕ БРАЛ, ВЗЯТЫЕ ВИДНЫ СЛЕВА",
                Touch = "ВЫБЕРИ УЛУЧШЕНИЕ. «НОВОЕ» — ЕЩЁ НЕ БРАЛ, ВЗЯТЫЕ ВИДНЫ СЛЕВА", Timeout = 0f },
        };

        [SerializeField] private TutorialState m_State;
        [SerializeField] private PlayerInputProvider m_Input;
        [SerializeField] private RunState m_Run;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private AmmoState m_Ammo;
        [SerializeField] private ZombieSpawner m_Spawner;
        [Tooltip("Seconds a hint stays after the player did it (so it is read).")]
        [SerializeField, Min(0f)] private float m_Linger = 0.8f;

        private bool m_Live;
        private int m_Shown;                  // bit per Live hint
        private int m_Current = -1;
        private float m_Time;
        private float m_DoneAt = -1f;
        private bool m_WaveCleared;

        /// <summary>The tutorial runs in this map: the cards are open or live hints are still to come (tests).</summary>
        public bool Running => m_State != null && (m_State.CardsOpen || m_Live);

        private void Start()
        {
            if (m_State == null)
            {
                enabled = false;
                return;
            }
            m_State.Hide();
            m_State.CardsClosed += OnCardsClosed;
            if (m_Waves != null)
                m_Waves.WaveEnded += OnWaveEnded;
            // Live hints come in one run only (the first one, or the one after the settings' replay): a later run never
            // shows them again, even when a hint never came up in it (the gun never ran dry).
            if (!MetaProgress.TutorialDone)
                OpenCards();
            else
            {
                m_Live = !MetaProgress.LiveHintsDone;
                MetaProgress.LiveHintsDone = true;
            }
        }

        private void OnDestroy()
        {
            if (m_State != null)
            {
                m_State.CardsClosed -= OnCardsClosed;
                m_State.CloseCards();
                m_State.Hide();
            }
            if (m_Waves != null)
                m_Waves.WaveEnded -= OnWaveEnded;
            GamePause.Set(GamePause.Reason.Tutorial, false);
        }

        /// <summary>Starts the tutorial again from the cards even if it was seen (cheats, tests).</summary>
        public void Restart()
        {
            MetaProgress.TutorialDone = false;
            MetaProgress.LiveHintsDone = false;
            OpenCards();
        }

        private void OpenCards()
        {
            m_Live = false;
            m_Current = -1;
            m_State.OpenCards();
            GamePause.Set(GamePause.Reason.Tutorial, true);
        }

        // Also after the settings' "Обучение" (it clears LiveHintsDone first): the live hints start over.
        private void OnCardsClosed()
        {
            MetaProgress.TutorialDone = true;
            m_Live = !MetaProgress.LiveHintsDone;
            MetaProgress.LiveHintsDone = true;
            m_Shown = 0;
            m_Current = -1;
        }

        private void OnWaveEnded(int wave) => m_WaveCleared = true;

        private void Update()
        {
            bool over = m_Run != null && m_Run.IsOver;
            // The game waits while the cards are open (opened here or by the settings' button).
            GamePause.Set(GamePause.Reason.Tutorial, m_State.CardsOpen && !over);
            if (!m_Live || m_State.CardsOpen)
                return;
            if (over)
            {
                m_Current = -1;
                m_State.Hide();
                return;
            }
            // The first level-up card takes over whatever hint is on.
            if (GamePause.Has(GamePause.Reason.LevelUp) && !Shown(Live.FirstCard) && m_Current != (int)Live.FirstCard)
                Begin(Live.FirstCard);
            if (m_Current >= 0)
            {
                Tick();
                return;
            }
            if (!Shown(Live.NoAmmo) && m_Ammo != null && m_Ammo.InMagazine + m_Ammo.Reserve == 0)
                Begin(Live.NoAmmo);
            else if (!Shown(Live.FirstZombie) && m_Spawner != null && m_Spawner.Alive > 0)
                Begin(Live.FirstZombie);
            else if (!Shown(Live.Workshop) && m_WaveCleared && m_Waves != null && !m_Waves.IsWaveRunning)
                Begin(Live.Workshop);
            else if (m_Shown == (1 << (int)Live.Count) - 1)
            {
                MetaProgress.LiveHintsDone = true;
                m_Live = false;
            }
        }

        private bool Shown(Live hint) => (m_Shown & (1 << (int)hint)) != 0;

        private void Begin(Live hint)
        {
            m_Current = (int)hint;
            m_Shown |= 1 << m_Current;
            m_Time = 0f;
            m_DoneAt = -1f;
            Hint h = s_Hints[m_Current];
            m_State.Show(DeviceInfo.IsMobile ? h.Touch : h.Desktop);
        }

        private void Tick()
        {
            m_Time += Time.deltaTime;
            Hint hint = s_Hints[m_Current];
            if (m_DoneAt < 0f && IsDone((Live)m_Current))
                m_DoneAt = m_Time;
            bool lingered = m_DoneAt >= 0f && (m_Time - m_DoneAt >= m_Linger || m_Current == (int)Live.FirstCard);
            if (lingered || (hint.Timeout > 0f && m_Time >= hint.Timeout))
            {
                m_Current = -1;
                m_State.Hide();
            }
        }

        private bool IsDone(Live hint)
        {
            IPlayerInput input = m_Input != null ? m_Input.Input : null;
            switch (hint)
            {
                case Live.FirstZombie:
                    return input != null && (input.FireHeld || input.FirePressed || input.QuickTap);
                case Live.NoAmmo:
                    return m_Ammo == null || m_Ammo.InMagazine + m_Ammo.Reserve > 0;
                case Live.Workshop:
                    return m_Waves == null || m_Waves.IsWaveRunning || (m_Run != null && m_Run.WorkshopOpen);
                case Live.FirstCard:
                    return !GamePause.Has(GamePause.Reason.LevelUp);
                default:
                    return true;
            }
        }
    }
}
