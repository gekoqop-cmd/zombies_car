using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 15. leaderboard
        /// <summary>
        /// The Yandex best-time leaderboard through the SDK stand-in (no SDK in the editor): a defeat sends nothing,
        /// a victory sends the run's time once; the victory screen's "ТАБЛИЦА ЛИДЕРОВ" opens the panel, which says
        /// there is nothing to show without the SDK and closes again; with stub entries it lists the top 10 and the
        /// player's own line under them.
        /// </summary>
        private static void Leaderboard()
        {
            GameOverScreen screen = null;
            LeaderboardPanel panel = null;
            At(1.0f, () =>
            {
                YandexSdk.SubmittedScores.Clear();
                YandexSdk.SubmittedBoards.Clear();
                YandexSdk.StubEntries = null;
                Find<LevelUpScreen>().enabled = false;
                screen = Find<GameOverScreen>();
                panel = Find<LeaderboardPanel>();
                s_Director.enabled = false;
                s_Car.TakeDamage(100000, Vector3.right);
            });
            At(3.4f, () =>
            {
                Check(s_Run.Outcome == RunOutcome.CarDestroyed && screen.IsShown, "a defeat");
                Check(YandexSdk.SubmittedScores.Count == 0, "a defeat sends nothing to the leaderboard");
                screen.Revive();
            });
            At(3.8f, () => s_Director.SkipTo(30, 0.3f));
            for (float t = 4.0f; t < 9f; t += 0.5f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                });
            At(5.6f, () =>
            {
                ZombieController boss = s_Director.Boss;
                Check(boss != null, "the last boss is out");
                boss?.TakeDamage(1000000, Vector3.right);
            });
            At(6.6f, () =>
            {
                int seconds = Mathf.RoundToInt(s_Run.PlayTime);
                Check(s_Run.Outcome == RunOutcome.Victory, "a victory");
                Check(YandexSdk.SubmittedScores.Count == 1 && YandexSdk.SubmittedScores[0] == seconds,
                    $"the victory sends its time once: {string.Join(", ", YandexSdk.SubmittedScores)} s (run {seconds} s)");
                Check(YandexSdk.SubmittedBoards.LastOrDefault() == DifficultyRules.Leaderboard(s_Run.Difficulty),
                    $"to the table of its difficulty ({s_Run.Difficulty} -> {YandexSdk.SubmittedBoards.LastOrDefault()})");
            });
            At(10.8f, () =>
            {
                Check(screen.IsShown && screen.Title == "ПОБЕДА!", "the victory screen");
                Button button = screen.GetComponentsInChildren<Button>(true).First(b => b.name == "LeaderboardButton");
                Check(button.gameObject.activeInHierarchy, "ТАБЛИЦА ЛИДЕРОВ on the victory screen");
                button.onClick.Invoke();
            });
            At(11.3f, () =>
            {
                Check(panel.IsOpen && panel.Current == LeaderboardPanel.State.Empty,
                    $"the panel opens and says there is nothing to show without the SDK ({panel.Current})");
                Shot("15_Leaderboard_Empty");
                panel.Close();
            });
            At(11.7f, () =>
            {
                Check(!panel.IsOpen && !panel.gameObject.activeSelf, "the panel closes");
                var entries = Enumerable.Range(1, 12).Select(i => new YandexSdk.LeaderboardEntry
                    { rank = i, name = i == 3 ? "" : $"Игрок {i}", avatar = "", score = 900 + i * 47 }).ToList();
                entries.Add(new YandexSdk.LeaderboardEntry { rank = 17, name = "Это я", avatar = "", score = 1850 });
                YandexSdk.StubEntries = new YandexSdk.LeaderboardEntries { available = true, entries = entries.ToArray(), userRank = 17 };
                panel.Open();
            });
            At(12.2f, () =>
            {
                Transform window = panel.transform.Find("Window");
                int rows = Enumerable.Range(1, 10).Count(i => window.Find($"Row{i}").gameObject.activeSelf);
                Check(panel.Current == LeaderboardPanel.State.Table && rows == 10, $"the top 10 ({rows} rows)");
                Check(window.Find("PlayerRow").gameObject.activeSelf, "the player's own line (17th) under them");
                Shot("15_Leaderboard_Table");
                panel.ShowTable(Difficulty.Hardcore);
                Check(panel.Shown == Difficulty.Hardcore && panel.Current == LeaderboardPanel.State.Table, "the ХАРДКОР tab shows its own table");
                panel.Close();
                YandexSdk.StubEntries = null;
                MetaProgress.TrySpend(screen.CoinsEarned);                    // the developer's own coins stay as they were
            });
            At(12.6f, Finish);
        }
    }
}
