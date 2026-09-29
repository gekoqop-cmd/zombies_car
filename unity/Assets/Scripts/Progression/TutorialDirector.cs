using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Player;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The short tutorial of the player's first run: a few hints on the HUD, each gone as soon as the player does it
    /// (or after a few seconds): move, shoot, the axe, what to do between waves, the red barrels. Seen once per device
    /// (<see cref="MetaProgress.TutorialDone"/>); the texts follow the device (keyboard and mouse or touch controls).
    /// Timers run on game time, so a level-up card or the settings pause them.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private struct Hint
        {
            public string Desktop;
            public string Touch;
            public float Timeout;
            public int Done;       // 0 timeout only, 1 moved, 2 fired, 3 switched weapons
        }

        private static readonly Hint[] s_Hints =
        {
            new Hint { Desktop = "WASD — ДВИЖЕНИЕ", Touch = "ЛЕВЫЙ ДЖОЙСТИК — ДВИЖЕНИЕ", Timeout = 9f, Done = 1 },
            new Hint { Desktop = "МЫШЬ — ПРИЦЕЛ, ЛЕВАЯ КНОПКА — ОГОНЬ", Touch = "ПРАВЫЙ ДЖОЙСТИК — ПРИЦЕЛ И ОГОНЬ", Timeout = 9f, Done = 2 },
            new Hint { Desktop = "Q — ТОПОР. ПАТРОНОВ МАЛО, БЕРЕГИТЕ ИХ", Touch = "КНОПКА ОРУЖИЯ — ТОПОР. ПАТРОНОВ МАЛО", Timeout = 9f, Done = 3 },
            new Hint { Desktop = "ЗАЩИЩАЙТЕ МАШИНУ! МЕЖДУ ВОЛНАМИ: E У МАШИНЫ — ПАТРОНЫ, E У ГАРАЖА — МАСТЕРСКАЯ",
                Touch = "ЗАЩИЩАЙТЕ МАШИНУ! МЕЖДУ ВОЛНАМИ У МАШИНЫ — ПАТРОНЫ, У ГАРАЖА — МАСТЕРСКАЯ", Timeout = 7f },
            new Hint { Desktop = "КРАСНЫЕ БОЧКИ ВЗРЫВАЮТСЯ — СТРЕЛЯЙТЕ ПО НИМ, КОГДА РЯДОМ ЗОМБИ",
                Touch = "КРАСНЫЕ БОЧКИ ВЗРЫВАЮТСЯ — СТРЕЛЯЙТЕ ПО НИМ, КОГДА РЯДОМ ЗОМБИ", Timeout = 6f },
        };

        [SerializeField] private TutorialState m_State;
        [SerializeField] private PlayerInputProvider m_Input;
        [SerializeField] private WeaponState m_Weapons;
        [SerializeField] private RunState m_Run;
        [Tooltip("Seconds after the map starts before the first hint.")]
        [SerializeField, Min(0f)] private float m_StartDelay = 1f;
        [Tooltip("Seconds a hint stays after the player did it (so it is read).")]
        [SerializeField, Min(0f)] private float m_Linger = 0.8f;
        [Tooltip("Metres the player has to walk to finish the movement hint.")]
        [SerializeField, Min(0f)] private float m_MoveDistance = 2f;

        private int m_Index = -1;
        private float m_Time;
        private float m_DoneAt = -1f;
        private Vector3 m_Start;
        private WeaponSlot m_Weapon;

        /// <summary>The tutorial runs in this map (tests).</summary>
        public bool Running => m_Index >= 0 && m_Index < s_Hints.Length;

        private void Start()
        {
            if (m_State != null)
                m_State.Hide();
            if (MetaProgress.TutorialDone || m_State == null)
            {
                enabled = false;
                return;
            }
            m_Index = -1;
            m_Time = -m_StartDelay;
        }

        /// <summary>Starts the tutorial again even if it was seen (tests).</summary>
        public void Restart()
        {
            enabled = true;
            m_Index = -1;
            m_Time = 0f;
        }

        private void Update()
        {
            if (m_Run != null && m_Run.IsOver)
            {
                Finish();
                return;
            }
            m_Time += Time.deltaTime;
            if (m_Index < 0)
            {
                if (m_Time >= 0f)
                    Next();
                return;
            }
            Hint hint = s_Hints[m_Index];
            if (m_DoneAt < 0f && IsDone(hint))
                m_DoneAt = m_Time;
            if ((m_DoneAt >= 0f && m_Time - m_DoneAt >= m_Linger) || m_Time >= hint.Timeout)
                Next();
        }

        private bool IsDone(Hint hint)
        {
            IPlayerInput input = m_Input != null ? m_Input.Input : null;
            switch (hint.Done)
            {
                case 1:
                    Vector3 d = m_Input.transform.position - m_Start;
                    d.y = 0f;
                    return d.magnitude >= m_MoveDistance;
                case 2:
                    return input != null && (input.FireHeld || input.FirePressed || input.QuickTap);
                case 3:
                    return m_Weapons != null && m_Weapons.Current != m_Weapon;
                default:
                    return false;
            }
        }

        private void Next()
        {
            m_Index++;
            m_Time = 0f;
            m_DoneAt = -1f;
            if (m_Index >= s_Hints.Length)
            {
                Finish();
                return;
            }
            if (m_Input != null)
                m_Start = m_Input.transform.position;
            if (m_Weapons != null)
                m_Weapon = m_Weapons.Current;
            Hint hint = s_Hints[m_Index];
            m_State.Show(DeviceInfo.IsMobile ? hint.Touch : hint.Desktop, m_Index + 1, s_Hints.Length);
        }

        private void Finish()
        {
            MetaProgress.TutorialDone = true;
            m_Index = s_Hints.Length;
            if (m_State != null)
                m_State.Hide();
            enabled = false;
        }

        private void OnDisable()
        {
            if (m_State != null && Running)
                m_State.Hide();
        }
    }
}
