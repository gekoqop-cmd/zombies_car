using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// The game's side of the Yandex Games SDK (plugin: Plugins/WebGL/YandexSdk.jslib; the SDK itself is loaded by the
    /// WebGL template). Created automatically before the first scene and kept for the whole session:
    /// <list type="bullet">
    /// <item>LoadingAPI.ready — once, when the main menu is shown (<see cref="NotifyGameReady"/>);</item>
    /// <item>GameplayAPI.start / stop — gameplay runs while a map is open and nothing pauses it (<see cref="GamePause"/>);</item>
    /// <item>game_api_pause / resume — pause the game and the sound while the platform asks;</item>
    /// <item>fullscreen ads at logical breaks (<see cref="ShowFullscreenAd"/>), with the game paused and muted meanwhile;</item>
    /// <item>the player's language (<see cref="Language"/>).</item>
    /// </list>
    /// Outside a WebGL build every call is a no-op and ads complete at once.
    /// </summary>
    public sealed class YandexSdk : MonoBehaviour
    {
        private const string c_ObjectName = "YandexSdk";   // must match Ya.receiver in the .jslib

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void YaInit();
        [DllImport("__Internal")] private static extern void YaLoadingReady();
        [DllImport("__Internal")] private static extern void YaGameplay(int running);
        [DllImport("__Internal")] private static extern void YaShowFullscreenAd();
        private static readonly bool s_Live = true;
#else
        private static void YaInit() { }
        private static void YaLoadingReady() { }
        private static void YaGameplay(int running) { }
        private static void YaShowFullscreenAd() { }
        private static readonly bool s_Live = false;
#endif

        private static YandexSdk s_Instance;
        private bool m_ReadySent;
        private bool m_Gameplay;
        private bool m_GameplayReported;
        private Action m_AdDone;

        /// <summary>The player's language from the SDK ("ru", "en", …); "ru" until the SDK answers.</summary>
        public static string Language { get; private set; } = "ru";

        /// <summary>The SDK is loaded and answered (false outside Yandex Games).</summary>
        public static bool IsAvailable { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (s_Instance != null)
                return;
            var go = new GameObject(c_ObjectName);
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<YandexSdk>();
        }

        private void Awake()
        {
            GamePause.Changed += Report;
            GameSession.Changed += Report;
            YaInit();
        }

        private void OnDestroy()
        {
            GamePause.Changed -= Report;
            GameSession.Changed -= Report;
            if (s_Instance == this)
                s_Instance = null;
        }

        /// <summary>The first screen the player can use is up: LoadingAPI.ready (sent once).</summary>
        public static void NotifyGameReady()
        {
            if (s_Instance == null || s_Instance.m_ReadySent)
                return;
            s_Instance.m_ReadySent = true;
            YaLoadingReady();
            Debug.Log("[YandexSdk] LoadingAPI.ready");
        }

        /// <summary>
        /// Shows a fullscreen ad at a logical break (between menu and map), then calls <paramref name="done"/>.
        /// The platform decides whether an ad is actually shown (frequency limits); the game waits either way.
        /// </summary>
        public static void ShowFullscreenAd(Action done)
        {
            if (!s_Live || s_Instance == null)
            {
                done?.Invoke();
                return;
            }
            if (s_Instance.m_AdDone != null)
                return;                                   // one ad at a time
            s_Instance.m_AdDone = done ?? (() => { });
            YaShowFullscreenAd();
        }

        // Gameplay for the platform = a map is open and nothing pauses it.
        private void Report()
        {
            bool gameplay = GameSession.InMap && !GamePause.IsPaused;
            if (m_GameplayReported && gameplay == m_Gameplay)
                return;
            m_Gameplay = gameplay;
            m_GameplayReported = true;
            YaGameplay(gameplay ? 1 : 0);
        }

        // ---------------------------------------------------------------- calls from the .jslib (SendMessage)
        private void OnSdkReady(string language)
        {
            IsAvailable = true;
            if (!string.IsNullOrEmpty(language))
                Language = language;
            Debug.Log($"[YandexSdk] ready, language {Language}");
            Report();
        }

        private void OnSdkMissing(string _) => Debug.Log("[YandexSdk] SDK not available (not on Yandex Games).");

        private void OnPlatformPause(string _) => GamePause.Set(GamePause.Reason.Platform, true);

        private void OnPlatformResume(string _) => GamePause.Set(GamePause.Reason.Platform, false);

        private void OnAdOpen(string _) => GamePause.Set(GamePause.Reason.Ad, true);

        private void OnAdClose(string _)
        {
            GamePause.Set(GamePause.Reason.Ad, false);
            Action done = m_AdDone;
            m_AdDone = null;
            done?.Invoke();
        }
    }
}
