using System.Collections;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// How hits feel: a short hit stop (the game nearly freezes for a few hundredths of a second so a blow has weight),
    /// a camera shake (<see cref="CameraShake"/>) and a vibration on phones. All of it goes off with the settings'
    /// "ТРЯСКА И ВИБРАЦИЯ" (<see cref="SettingsService.ShakeEnabled"/>). A hit stop never cuts into a pause or a slow
    /// motion of its own (the victory finale).
    /// </summary>
    public static class GameFeel
    {
        private const float c_StopSpeed = 0.06f;

        private sealed class Runner : MonoBehaviour
        {
            private float m_Until;
            private bool m_Running;

            public void Stop(float seconds)
            {
                m_Until = Mathf.Max(m_Until, Time.unscaledTime + seconds);
                if (m_Running)
                    return;
                m_Running = true;
                GamePause.SetSlowMotion(c_StopSpeed);
                StartCoroutine(Release());
            }

            private IEnumerator Release()
            {
                while (Time.unscaledTime < m_Until)
                    yield return null;
                m_Running = false;
                if (Mathf.Approximately(GamePause.SlowMotion, c_StopSpeed))   // a finale's slow motion is left alone
                    GamePause.SetSlowMotion(1f);
            }
        }

        private static Runner s_Runner;

        /// <summary>The last hit stop's length, real seconds (tests).</summary>
        public static float LastStop { get; private set; }

        /// <summary>The last vibration asked for, milliseconds (tests: the browser may not vibrate at all).</summary>
        public static int LastVibration { get; private set; }

        public static void HitStop(float seconds)
        {
            if (!SettingsService.ShakeEnabled || seconds <= 0f || GamePause.IsPaused ||
                (GamePause.SlowMotion < 1f && !Mathf.Approximately(GamePause.SlowMotion, c_StopSpeed)))
                return;
            if (s_Runner == null)
            {
                var go = new GameObject("[GameFeel]");
                Object.DontDestroyOnLoad(go);
                s_Runner = go.AddComponent<Runner>();
            }
            LastStop = seconds;
            s_Runner.Stop(seconds);
        }

        public static void Vibrate(int milliseconds)
        {
            if (!SettingsService.ShakeEnabled || !DeviceInfo.IsMobile || milliseconds <= 0)
                return;
            LastVibration = milliseconds;
            YandexSdk.Vibrate(milliseconds);
        }

        /// <summary>The player took <paramref name="damage"/>: the bigger the blow, the harder it kicks.</summary>
        public static void Hurt(int damage)
        {
            CameraShake.Add(Mathf.Clamp(0.08f + damage * 0.008f, 0.08f, 0.4f));
            Vibrate(Mathf.Clamp(25 + damage * 2, 30, 120));
            if (damage >= 12)
                HitStop(0.05f);
        }

        /// <summary>A blow of the axe landed.</summary>
        public static void Impact()
        {
            Vibrate(20);
            HitStop(0.035f);
        }

        /// <summary>A boss went down.</summary>
        public static void BossDown()
        {
            CameraShake.Add(0.5f);
            Vibrate(150);
            HitStop(0.14f);
        }

        /// <summary>Something blew up near the player.</summary>
        public static void Blast(float strength) => Vibrate(Mathf.RoundToInt(Mathf.Clamp(40f * strength, 25f, 90f)));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Runner = null;
            LastStop = 0f;
            LastVibration = 0;
        }
    }
}
