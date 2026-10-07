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
    /// <item>the best-time leaderboards (one per difficulty, DifficultyRules.Leaderboard): a victory's time sent, the top and the player's line read;</item>
    /// <item>cloud saves: the player's progress (<see cref="MetaProgress"/>) read at start and merged with this device's,
    /// then every saved change sent a moment later (and at once when the tab is hidden).</item>
    /// </list>
    /// Outside a WebGL build every call is a no-op and ads complete at once.
    /// </summary>
    public sealed class YandexSdk : MonoBehaviour
    {
        private const string c_ObjectName = "YandexSdk";   // must match Ya.receiver in the .jslib

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void YaInit();
        [DllImport("__Internal")] private static extern void YaLoadingReady();
        [DllImport("__Internal")] private static extern void YaVibrate(int milliseconds);
        [DllImport("__Internal")] private static extern void YaGameplay(int running);
        [DllImport("__Internal")] private static extern void YaShowFullscreenAd();
        [DllImport("__Internal")] private static extern void YaShowRewardedAd();
        [DllImport("__Internal")] private static extern void YaSubmitScore(string leaderboard, int score);
        [DllImport("__Internal")] private static extern void YaGetLeaderboardEntries(string leaderboard, int count);
        [DllImport("__Internal")] private static extern void YaCloudLoad();
        [DllImport("__Internal")] private static extern void YaCloudSave(string json);
        private static readonly bool s_Live = true;
#else
        private static void YaInit() { }
        private static void YaLoadingReady() { }
        private static void YaVibrate(int milliseconds) { }
        private static void YaGameplay(int running) { }
        private static void YaShowFullscreenAd() { }
        private static void YaShowRewardedAd() { }
        private static void YaSubmitScore(string leaderboard, int score) { }
        private static void YaGetLeaderboardEntries(string leaderboard, int count) { }
        private static void YaCloudLoad() { }
        private static void YaCloudSave(string json) { }
        private static readonly bool s_Live = false;
#endif

        /// <summary>
        /// Technical name of the leaderboard "Лучшее время прохождения" in the Yandex Games console: seconds of a
        /// victory, sorted ascending (fewer seconds = better). Only victories are sent. It is Normal's table; Easy and
        /// Hardcore have their own (<see cref="DifficultyRules.Leaderboard"/>).
        /// </summary>
        public const string c_BestTimeLeaderboard = "bestTimeNormal";

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

            /// <summary>The table this answer is for.</summary>
            [NonSerialized] public string Board;

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
            MetaProgress.Saved += OnProgressSaved;
            CloudReady = !s_Live;                         // no cloud to wait for outside Yandex Games
            YaInit();
        }

        private void OnDestroy()
        {
            GamePause.Changed -= Report;
            GameSession.Changed -= Report;
            MetaProgress.Saved -= OnProgressSaved;
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

        /// <summary>A short vibration on a phone (the browser's Vibration API; nothing where it is missing).</summary>
        public static void Vibrate(int milliseconds) => YaVibrate(milliseconds);

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

        /// <summary>Tests: the table each time of <see cref="SubmittedScores"/> was sent to.</summary>
        public static System.Collections.Generic.List<string> SubmittedBoards { get; } = new System.Collections.Generic.List<string>();

        /// <summary>
        /// A victory's time to <paramref name="board"/> (the difficulty's table). Only an authorized player on Yandex
        /// Games gets on it; otherwise (a guest, the editor, a local build) nothing is sent and nothing fails.
        /// </summary>
        public static void SubmitTimeScore(int seconds, string board = c_BestTimeLeaderboard)
        {
            if (seconds <= 0)
                return;
            SubmittedScores.Add(seconds);
            SubmittedBoards.Add(board);
            if (!s_Live || s_Instance == null || !IsAvailable)
            {
                Debug.Log($"[YandexSdk] Leaderboard '{board}': {seconds} s not sent (no SDK).");
                return;
            }
            YaSubmitScore(board, seconds);
        }

        /// <summary>
        /// The top <paramref name="count"/> of <paramref name="board"/> with the player's own line (the answer says
        /// which table it is for). Without the SDK (or on an error) <paramref name="callback"/> gets an unavailable,
        /// empty answer at once. One request at a time: a request for another table waits for the one going.
        /// </summary>
        public static void GetLeaderboardEntries(string board, int count, Action<LeaderboardEntries> callback)
        {
            if (callback == null)
                return;
            if (!s_Live || s_Instance == null || !IsAvailable)
            {
                LeaderboardEntries stub = Finish(StubEntries) ?? LeaderboardEntries.Unavailable;
                stub.Board = board;
                callback(stub);
                return;
            }
            YandexSdk sdk = s_Instance;
            if (sdk.m_EntriesDone == null)
            {
                sdk.m_EntriesBoard = board;
                sdk.m_EntriesDone = callback;
                YaGetLeaderboardEntries(board, Mathf.Clamp(count, 1, 20));
            }
            else if (sdk.m_EntriesBoard == board)
                sdk.m_EntriesDone += callback;
            else
            {
                sdk.m_NextBoard = board;                                   // the latest wish wins
                sdk.m_NextCount = count;
                sdk.m_NextDone = callback;
            }
        }

        private Action<LeaderboardEntries> m_EntriesDone;
        private string m_EntriesBoard;
        private Action<LeaderboardEntries> m_NextDone;
        private string m_NextBoard;
        private int m_NextCount;

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
            string board = m_EntriesBoard;
            m_EntriesDone = null;
            LeaderboardEntries answer = Finish(result) ?? LeaderboardEntries.Unavailable;
            answer.Board = board;
            done?.Invoke(answer);
            if (m_NextDone != null)
            {
                Action<LeaderboardEntries> next = m_NextDone;
                m_NextDone = null;
                GetLeaderboardEntries(m_NextBoard, m_NextCount, next);  // the table asked for while this one was coming
            }
        }

        private void OnScoreSubmitted(string ok) => Debug.Log($"[YandexSdk] Leaderboard score {(ok == "1" ? "sent" : "not sent")}.");

        // ---------------------------------------------------------------- cloud saves
        /// <summary>Seconds a saved change waits before it goes to the cloud (changes in a row go together).</summary>
        private const float c_CloudSaveDelay = 2f;

        /// <summary>
        /// The cloud save was read and merged — or there is none to wait for (outside Yandex Games, an SDK error): the
        /// Boot scene waits for this before the menu shows the coins.
        /// </summary>
        public static bool CloudReady { get; private set; }

        /// <summary>The progress is mirrored to the Yandex cloud (the SDK and the player are there).</summary>
        public static bool CloudEnabled { get; private set; }

        private float m_CloudSaveAt = -1f;

        private void OnProgressSaved()
        {
            if (CloudEnabled && m_CloudSaveAt < 0f)
                m_CloudSaveAt = Time.unscaledTime + c_CloudSaveDelay;
        }

        private void Update()
        {
            if (m_CloudSaveAt >= 0f && Time.unscaledTime >= m_CloudSaveAt)
                SendCloudSave();
        }

        // The tab is hidden or the page closes: whatever is waiting goes now.
        private void OnApplicationFocus(bool focus)
        {
            if (!focus && m_CloudSaveAt >= 0f)
                SendCloudSave();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && m_CloudSaveAt >= 0f)
                SendCloudSave();
        }

        private void SendCloudSave()
        {
            m_CloudSaveAt = -1f;
            if (CloudEnabled)
                YaCloudSave(MetaProgress.ExportJson());
        }

        // The cloud's copy ("" = nothing saved yet, "!" = no cloud): merged into this device's progress; when the two
        // differ, the merged progress goes back up at once.
        private void OnCloudLoaded(string payload)
        {
            CloudReady = true;
            if (payload == "!")
            {
                Debug.Log("[YandexSdk] cloud save unavailable: progress stays on this device.");
                return;
            }
            CloudEnabled = true;
            bool changed = MetaProgress.MergeCloud(payload);
            Debug.Log($"[YandexSdk] cloud save {(string.IsNullOrEmpty(payload) ? "empty" : "read")}{(changed ? ", merged in" : "")}.");
            if (MetaProgress.ExportJson() != payload)
                SendCloudSave();
        }

        private void OnCloudSaved(string ok)
        {
            if (ok != "1")
                Debug.LogWarning("[YandexSdk] cloud save failed; it is sent again with the next change.");
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
            {
                Language = language;
                Loc.SetPlatformLanguage(language);         // unless the player picked a language in the menu
            }
            Debug.Log($"[YandexSdk] ready, language {Language}");
            Report();
            YaCloudLoad();
        }

        private void OnSdkMissing(string _)
        {
            CloudReady = true;
            Debug.Log("[YandexSdk] SDK not available (not on Yandex Games).");
        }

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
