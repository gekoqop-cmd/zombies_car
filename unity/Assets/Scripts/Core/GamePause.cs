using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// The one owner of <see cref="Time.timeScale"/> and <see cref="AudioListener.pause"/>. Anything that must stop the
    /// game adds its reason (settings pop-up, controls editor, the platform's pause, an ad, the tab losing focus); the
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
        }

        // Reasons that also silence the sound (the menus keep it).
        private const Reason c_Mute = Reason.Platform | Reason.Ad | Reason.Focus;
        // Reasons that belong to one map session; cleared when a scene is loaded.
        private const Reason c_Session = Reason.Settings | Reason.ControlsLayout;

        private static Reason s_Reasons;

        /// <summary>Raised when the set of reasons changes.</summary>
        public static event Action Changed;

        public static bool IsPaused => s_Reasons != Reason.None;
        public static Reason Reasons => s_Reasons;

        public static void Set(Reason reason, bool active)
        {
            Reason next = active ? s_Reasons | reason : s_Reasons & ~reason;
            if (next == s_Reasons)
                return;
            s_Reasons = next;
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Drops the pauses of the scene being left (its pop-ups are gone with it).</summary>
        public static void ClearSession() => Set(c_Session, false);

        private static void Apply()
        {
            Time.timeScale = s_Reasons == Reason.None ? 1f : 0f;
            AudioListener.pause = (s_Reasons & c_Mute) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Reasons = Reason.None;
            Changed = null;
        }
    }
}
