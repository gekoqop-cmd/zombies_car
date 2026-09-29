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
    /// <item>the player's language (<see cref="Language"/>);</item>
    /// <item>the "best_time" leaderboard: a victory's time sent, the top and the player's line read.</item>
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
        [DllImport("__Internal")] private static extern void YaShowRewardedAd();
        [DllImport("__Internal")] private static extern void YaSubmitScore(string leaderboard, int score);
        [DllImport("__Internal")] private static extern void YaGetLeaderboardEntries(string leaderboard, int count);
        private static readonly bool s_Live = true;
#else
        private static void YaInit() { }
        private static void YaLoadingReady() { }
        private static void YaGameplay(int running) { }
        private static void YaShowFullscreenAd() { }
        private static void YaShowRewardedAd() { }
        private static void YaSubmitScore(string leaderboard, int score) { }
        private static void YaGetLeaderboardEntries(string leaderboard, int count) { }
        private static readonly bool s_Live = false;
#endif

        /// <summary>
        /// Technical name of the leaderboard "Лучшее время прохождения" in the Yandex Games console: seconds of a
        /// victory, sorted ascending (fewer seconds = better). Only victories are sent.
        /// </summary>
        public const string c_BestTimeLeaderboard = "best_time";

        // ---------------------------------------------------------------- leaderboard
        /// <summary>One line of the leaderboard.</summary>
        [Serializable]
        public sealed class LeaderboardEntry
        {
            public int rank;
            public string name;
            public string avatar;
            public int score;                                       // seconds
        }

        /// <summary>The top of the leaderboard and the player's own line (null when they are not on it).</summary>
        [Serializable]
        public sealed class LeaderboardEntries
        {
            public bool available;
            public LeaderboardEntry[] entries = Array.Empty<LeaderboardEntry>();
            public int userRank;

            [NonSerialized] public LeaderboardEntry Player;

            /// <summary>Nothing to show: the SDK is not there (editor, local build) or it answered with an error.</summary>
            public static LeaderboardEntries Unavailable => new LeaderboardEntries { available = false };
        }

        /// <summary>
        /// Every time sent with <see cref="SubmitTimeScore"/> (also outside Yandex Games, where nothing leaves the
        /// game): the tests' stand-in for the SDK.
        /// </summary>
        public static System.Collections.Generic.List<int> SubmittedScores { get; } = new System.Collections.Generic.List<int>();

        /// <summary>Tests: what <see cref="GetLeaderboardEntries"/> answers outside Yandex Games (null = unavailable).</summary>
        public static LeaderboardEntries StubEntries { get; set; }

        private static YandexSdk s_Instance;
        private bool m_ReadySent;
        private bool m_Gameplay;
        private bool m_GameplayReported;
        private Action m_AdDone;
        private Action<bool> m_RewardedDone;
        private bool m_Rewarded;

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

        /// <summary>
        /// Shows a rewarded video (the button says so); <paramref name="done"/> gets true only when the platform
        /// confirmed the reward (onRewarded), after the video is closed. Outside Yandex Games the reward is granted at
        /// once (editor and local tests).
        /// </summary>
        public static void ShowRewardedAd(Action<bool> done)
        {
            if (!s_Live || s_Instance == null)
            {
                done?.Invoke(true);
                return;
            }
            if (s_Instance.m_RewardedDone != null || s_Instance.m_AdDone != null)
                return;                                   // one ad at a time
            s_Instance.m_Rewarded = false;
            s_Instance.m_RewardedDone = done ?? (_ => { });
            YaShowRewardedAd();
        }

        /// <summary>
        /// A victory's time to the <see cref="c_BestTimeLeaderboard"/> leaderboard. Only an authorized player on Yandex
        /// Games gets on it; otherwise (a guest, the editor, a local build) nothing is sent and nothing fails.
        /// </summary>
        public static void SubmitTimeScore(int seconds)
        {
            if (seconds <= 0)
                return;
            SubmittedScores.Add(seconds);
            if (!s_Live || s_Instance == null || !IsAvailable)
            {
                Debug.Log($"[YandexSdk] Leaderboard '{c_BestTimeLeaderboard}': {seconds} s not sent (no SDK).");
                return;
            }
            YaSubmitScore(c_BestTimeLeaderboard, seconds);
        }

        /// <summary>
        /// The top <paramref name="count"/> of the <see cref="c_BestTimeLeaderboard"/> leaderboard with the player's own
        /// line. Without the SDK (or on an error) <paramref name="callback"/> gets an unavailable, empty answer at once.
        /// </summary>
        public static void GetLeaderboardEntries(int count, Action<LeaderboardEntries> callback)
        {
            if (callback == null)
                return;
            if (!s_Live || s_Instance == null || !IsAvailable)
            {
                callback(Finish(StubEntries) ?? LeaderboardEntries.Unavailable);
                return;
            }
            s_Instance.m_EntriesDone += callback;
            if (s_Instance.m_EntriesDone.GetInvocationList().Length == 1)       // one request at a time
                YaGetLeaderboardEntries(c_BestTimeLeaderboard, Mathf.Clamp(count, 1, 20));
        }

        private Action<LeaderboardEntries> m_EntriesDone;

        // The player's line: the entry at userRank.
        private static LeaderboardEntries Finish(LeaderboardEntries result)
        {
            if (result == null)
                return null;
            result.entries ??= Array.Empty<LeaderboardEntry>();
            result.Player = result.userRank > 0 ? Array.Find(result.entries, e => e.rank == result.userRank) : null;
            return result;
        }

        private void OnLeaderboardEntries(string json)
        {
            LeaderboardEntries result = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    result = JsonUtility.FromJson<LeaderboardEntries>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[YandexSdk] leaderboard answer: {e.Message}");
                }
            }
            Action<LeaderboardEntries> done = m_EntriesDone;
            m_EntriesDone = null;
            done?.Invoke(Finish(result) ?? LeaderboardEntries.Unavailable);
        }

        private void OnScoreSubmitted(string ok) => Debug.Log($"[YandexSdk] Leaderboard score {(ok == "1" ? "sent" : "not sent")}.");

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

        private void OnRewarded(string _) => m_Rewarded = true;

        private void OnRewardedClose(string _)
        {
            GamePause.Set(GamePause.Reason.Ad, false);
            Action<bool> done = m_RewardedDone;
            m_RewardedDone = null;
            done?.Invoke(m_Rewarded);
        }

        private void OnAdClose(string _)
        {
            GamePause.Set(GamePause.Reason.Ad, false);
            Action done = m_AdDone;
            m_AdDone = null;
            done?.Invoke();
        }
    }
}
