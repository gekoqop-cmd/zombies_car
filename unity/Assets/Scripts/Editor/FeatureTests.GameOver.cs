using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 4. game over
        /// <summary>
        /// The car destroyed ends the run with the game-over screen (reason, scrap earned, level, kills, wave, defeat
        /// stinger, game paused); "ЗАНОВО" starts the same map afresh; the player killed ends it too; "В МЕНЮ" goes to
        /// the main menu with the game running again.
        /// </summary>
        private static void GameOver()
        {
            GameOverScreen screen = null;
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;
                // Something to show: a few kills, some scrap and experience.
                for (int i = 0; i < 6; i++)
                    s_Spawner.SpawnAt(Zombie("Walker"), new Vector3(-6f + i * 2f, 0f, -8f));
            });
            At(2.2f, () =>
            {
                foreach (ZombieController z in LiveZombies().ToArray())
                    z.TakeDamage(1000, Vector3.right);
                s_Run.AddMoney(300);
                s_Progression.AddXp(200);
                s_Waves.StartWave(7);
                s_Car.TakeDamage(100000, Vector3.right);
            });
            At(2.5f, () =>
            {
                Check(s_Run.Outcome == RunOutcome.CarDestroyed, "the destroyed car ends the run");
                Check(!s_Director.enabled, "no more waves after the end");
            });
            At(3.1f, () => Shot("4_Car_Destroyed"));
            At(4.4f, () =>
            {
                screen = Find<GameOverScreen>();
                Check(screen != null && screen.IsShown && screen.Title == "ИГРА ОКОНЧЕНА", "game-over screen: ИГРА ОКОНЧЕНА");
                Check(GamePause.Has(GamePause.Reason.GameOver) && Time.timeScale == 0f, "the game is paused behind it");
                Check(Playing("Defeat"), "defeat stinger");
            });
            At(6.2f, () =>
            {
                Shot("4_GameOver_Car");
                Check(s_Run.MoneyEarned >= 300 && s_Run.Kills >= 6 && s_Stats.Level > 1 && s_Run.Wave == 7,
                    $"stats: scrap {s_Run.MoneyEarned}, kills {s_Run.Kills}, level {s_Stats.Level}, wave {s_Run.Wave}");
                screen.Restart();
            });
            At(10.0f, () =>
            {
                Check(SceneManager.GetActiveScene().name == SceneNames.c_Map01, "ЗАНОВО loads the map again");
                Rebind();
                screen = Find<GameOverScreen>();
                Check(s_Run.Outcome == RunOutcome.None && s_Run.Money == 0 && s_Run.Kills == 0 && s_Stats.Level == 1,
                    "a fresh run: no scrap, no kills, level 1");
                Check(!screen.IsShown && Time.timeScale == 1f && !GamePause.IsPaused, "the screen is gone and the game runs");
                Check(s_Car.Health == s_Car.MaxHealth, "the car is repaired");
                s_Director.enabled = false;
                s_PlayerHealth.TakeDamage(100000, Vector3.right);
            });
            At(10.35f, () => Shot("4_Player_Down"));
            At(12.2f, () =>
            {
                Check(s_Run.Outcome == RunOutcome.PlayerDied && screen.IsShown, "the player's death ends the run");
                Shot("4_GameOver_Player");
                screen.ToMenu();
            });
            At(15.5f, () =>
            {
                Check(SceneManager.GetActiveScene().name == SceneNames.c_MainMenu, "В МЕНЮ opens the main menu");
                Check(Time.timeScale == 1f && !GamePause.IsPaused, "nothing is paused in the menu");
            });
            At(15.7f, Finish);
        }

        // ================================================================== 4b. victory
        /// <summary>The last wave's boss and zombies dead: the victory screen.</summary>
        private static void Victory()
        {
            At(1.0f, () =>
            {
                Find<LevelUpScreen>().enabled = false;
                s_Director.SkipTo(30, 0.3f);
            });
            for (float t = 1.5f; t < 6f; t += 0.5f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                });
            At(2.6f, () =>
            {
                ZombieController boss = s_Director.Boss;
                Check(boss != null, "the last boss is out");
                if (boss != null)
                    boss.TakeDamage(1000000, Vector3.right);
                KillAll();
            });
            At(3.6f, KillAll);
            At(6.0f, () =>
            {
                GameOverScreen screen = Find<GameOverScreen>();
                Check(s_Run.Outcome == RunOutcome.Victory && s_Waves.Phase == WavePhase.Finished, "all 30 waves: victory");
                Check(screen.IsShown && screen.Title == "ПОБЕДА!", "victory screen: ПОБЕДА!");
                Check(Playing("Victory"), "victory fanfare");
            });
            At(7.5f, () => Shot("4_Victory"));
            At(7.7f, Finish);
        }
    }
}
