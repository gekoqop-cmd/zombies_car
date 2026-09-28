using UnityEngine;

namespace ZombiCar.UI
{
    /// <summary>Easing curves for the pop-up animations (all driven by unscaled time: they run while the game is paused).</summary>
    public static class UiMotion
    {
        /// <summary>0..1 → 0..1 with a small overshoot at the end (a "pop").</summary>
        public static float BackOut(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        public static float CubicOut(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>A bump 0 → 1 → 0 over t = 0..1 (punch scales).</summary>
        public static float Punch(float t) => t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * (1f - t);
    }
}
