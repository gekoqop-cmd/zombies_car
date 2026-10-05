using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// The one owner of <see cref="Time.timeScale"/> and <see cref="AudioListener.pause"/>. Anything that must stop the
    /// game adds its reason (settings pop-up, controls editor, level-up cards, the workshop, the game-over screen, the
    /// platform's pause, an ad, the tab losing focus); the
    /// game runs again when no reason is left. Yandex Games rules: sound off without focus and during ads, gameplay
    /// paused during ads and platform overlays (<c>YandexSdk</c> reports it with GameplayAPI start / stop).
    /// </summary>
    public static class GamePause
    {
        [Flags]
        public enum Reason
        {
            None = 0,
            Settings = 1 << 0,
            ControlsLayout = 1 << 1,
            Platform = 1 << 2,
            Ad = 1 << 3,
            Focus = 1 << 4,
            LevelUp = 1 << 5,
            Workshop = 1 << 6,
            GameOver = 1 << 7,
            Tutorial = 1 << 8,
            BossIntro = 1 << 9,
            Stage = 1 << 10,
        }

        // Reasons that also silence the sound (the menus keep it).
        private const Reason c_Mute = Reason.Platform | Reason.Ad | Reason.Focus;
        // Reasons that belong to one map session; cleared when a scene is loaded.
        private const Reason c_Session = Reason.Settings | Reason.ControlsLayout | Reason.LevelUp | Reason.Workshop | Reason.GameOver |
                                          Reason.Tutorial | Reason.BossIntro | Reason.Stage;

        private static Reason s_Reasons;
        private static float s_SlowMotion = 1f;

        /// <summary>Raised when the set of reasons changes.</summary>
        public static event Action Changed;

        public static bool IsPaused => s_Reasons != Reason.None;
        public static Reason Reasons => s_Reasons;

        /// <summary>Frame of the last change: input that closed a pop-up this frame must not act in the game too.</summary>
        public static int ChangedFrame { get; private set; } = -1;

        public static bool Has(Reason reason) => (s_Reasons & reason) != 0;

        public static void Set(Reason reason, bool active)
        {
            Reason next = active ? s_Reasons | reason : s_Reasons & ~reason;
            if (next == s_Reasons)
                return;
            s_Reasons = next;
            ChangedFrame = Time.frameCount;
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Drops the pauses of the scene being left (its pop-ups are gone with it).</summary>
        public static void ClearSession()
        {
            s_SlowMotion = 1f;
            Set(c_Session, false);
            Apply();
        }

        /// <summary>The game speed while nothing pauses it.</summary>
        public static float SlowMotion => s_SlowMotion;

        /// <summary>Game speed while nothing pauses it (1 = normal; the victory finale slows it down).</summary>
        public static void SetSlowMotion(float scale)
        {
            s_SlowMotion = Mathf.Clamp(scale, 0.05f, 1f);
            Apply();
        }

        private static void Apply()
        {
            Time.timeScale = s_Reasons == Reason.None ? s_SlowMotion : 0f;
            AudioListener.pause = (s_Reasons & c_Mute) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Reasons = Reason.None;
            s_SlowMotion = 1f;
            Changed = null;
        }
    }
}
