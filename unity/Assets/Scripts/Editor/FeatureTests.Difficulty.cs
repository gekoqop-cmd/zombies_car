using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 19. difficulty, checkpoints, achievement
        /// <summary>
        /// A map opened directly plays Normal (the banner says so). The boss of wave 10 cleared on Normal saves the run
        /// (cards, workshop levels, scrap) to go on from wave 11, pays the stage's coins and shows "ЭТАП 1 ИЗ 3
        /// ПРОЙДЕН!"; going on from the checkpoint brings all of it back at wave 11. Hardcore: tougher and more zombies,
        /// no checkpoint after a boss; its victory unlocks "БЕЗУМЕЦ!" with the notification over the victory screen.
        /// In the menu: the difficulty cards, the saved-run question, the two Hardcore warnings, the achievements and
        /// the daily bonus. Everything the scenario changed in the progress is put back.
        /// </summary>
        private static void DifficultyAndCheckpoints()
        {
            string map = null;
            Perk damage = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool).Perks.First(p => p.Id == "damage");
            CarUpgradeCatalog catalog = AssetDatabase.LoadAssetAtPath<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
            CarUpgrade armor = catalog.Upgrades.First(u => u.Id == "armor");
            int coinsAtStart = 0, coinsBeforeStage = 0, dailyDay = 0, dailyDate = -1;
            bool hardcoreWas = false;
            Core.Difficulty lastWas = Core.Difficulty.Normal;
            MetaProgress.Checkpoint saved = null;
            StageClearScreen stage = null;

            void KeepAlive()
            {
                s_PlayerHealth.Heal(1000);
                s_Car.Repair(1000);
            }

            void Cleanup()
            {
                if (map != null)
                    foreach (Core.Difficulty d in new[] { Core.Difficulty.Easy, Core.Difficulty.Normal, Core.Difficulty.Hardcore })
                        MetaProgress.ClearCheckpoint(map, d);
                MetaProgress.ForgetAchievement(MetaProgress.MadmanAchievement);
                MetaProgress.HardcoreConfirmed = hardcoreWas;
                MetaProgress.LastDifficulty = lastWas;
                MetaProgress.SetDaily(dailyDay, dailyDate);
                int extra = MetaProgress.Coins - coinsAtStart;
                if (extra > 0)
                    MetaProgress.TrySpend(extra);                              // the developer's own coins stay as they were
            }

            // ---- Normal: the stage after the first boss is saved.
            At(1.0f, () =>
            {
                map = s_Run.Map.name;
                coinsAtStart = MetaProgress.Coins;
                hardcoreWas = MetaProgress.HardcoreConfirmed;
                lastWas = MetaProgress.LastDifficulty;
                dailyDay = MetaProgress.DailyDay;
                dailyDate = MetaProgress.DailyLastDate;
                foreach (Core.Difficulty d in new[] { Core.Difficulty.Easy, Core.Difficulty.Normal, Core.Difficulty.Hardcore })
                    MetaProgress.ClearCheckpoint(map, d);
                Find<LevelUpScreen>().enabled = false;
                Check(s_Run.Difficulty == Core.Difficulty.Normal && Mathf.Approximately(Find<ZombieSpawner>().HealthScale, 1f),
                    "a map opened directly plays Normal (zombies as tuned)");
                s_Run.TakePerk(damage);
                s_Run.TakePerk(damage);
                s_Run.AddMoney(500);
                s_Run.TryBuy(armor);
                s_Run.TryBuy(armor);
                stage = Find<StageClearScreen>();
            });
            At(2.2f, () =>
            {
                ModeBanner banner = Find<ModeBanner>();
                Check(banner.Shown && banner.Title == "РЕЖИМ: СРЕДНИЙ", $"the difficulty banner before the waves: \"{banner.Title}\"");
                Shot("19_ModeBanner_Normal");
                coinsBeforeStage = MetaProgress.Coins;
                s_Director.SkipTo(10, 0.3f);
            });
            for (float t = 2.4f; t < 8f; t += 0.2f)
                At(t, KeepAlive);
            At(3.6f, () => s_Director.Boss?.TakeDamage(1000000, Vector3.right));
            for (float t = 4.0f; t < 6.6f; t += 0.3f)
                At(t, KillAll);                                            // a zombie still rising is caught the next time
            At(7.0f, () =>
            {
                saved = MetaProgress.GetCheckpoint(map, Core.Difficulty.Normal);
                Check(saved != null && saved.wave == 11, $"the boss of wave 10 cleared: the run is saved to go on from wave {saved?.wave}");
                Check(saved != null && saved.levels.Contains("perk:damage=2") && saved.levels.Contains("car:armor=2"),
                    $"with its cards and workshop levels ({string.Join(" ", saved?.levels ?? new string[0])})");
                Check(MetaProgress.Coins > coinsBeforeStage, $"the stage's coins are paid at once ({coinsBeforeStage} -> {MetaProgress.Coins})");
                Check(stage.IsOpen && GamePause.IsPaused, "\"ЭТАП 1 ИЗ 3 ПРОЙДЕН!\" and the game waits");
            });
            At(7.4f, () =>
            {
                Shot("19_Stage_Clear");
                stage.Continue();
            });
            At(7.8f, () =>
            {
                Check(!stage.IsOpen && !GamePause.Has(GamePause.Reason.Stage), "ПРОДОЛЖИТЬ: the break goes on");
                RunLaunch.Choose(Core.Difficulty.Normal, true);
                SceneLoader.EnsureExists().LoadMap(s_Run.Map);
            });

            // ---- going on from the checkpoint
            At(10.8f, () =>
            {
                Rebind();
                Check(Find<RunSetup>().Resumed && s_Run.Wave == 11 && s_Waves.Wave == 11,
                    $"the run goes on from its checkpoint: wave {s_Run.Wave} (HUD {s_Waves.Wave})");
                Check(s_Run.StacksOf(damage) == 2 && s_Run.LevelOf(armor) == 2 && s_Run.Money == saved.money && s_Car.Armor > 0f,
                    $"cards, workshop levels and scrap back (damage {s_Run.StacksOf(damage)}, armour {s_Run.LevelOf(armor)}, scrap {s_Run.Money})");
                Check(s_Run.BaseWave == 11, "the coins at its end count from wave 11");
                Shot("19_Resumed");
                RunLaunch.Choose(Core.Difficulty.Hardcore, false);
                SceneLoader.EnsureExists().LoadMap(s_Run.Map);
            });

            // ---- Hardcore: tougher, no checkpoints, the achievement
            At(13.8f, () =>
            {
                Rebind();
                Find<LevelUpScreen>().enabled = false;
                var spawner = Find<ZombieSpawner>();
                Check(s_Run.Difficulty == Core.Difficulty.Hardcore && spawner.HealthScale > 1.3f && spawner.DamageScale > 1.2f &&
                      s_Director.CountScale > 1.2f, $"Hardcore: zombies x{spawner.HealthScale} health, x{spawner.DamageScale} damage, x{s_Director.CountScale} more");
                Check(s_Waves.Wave == 1 && !Find<RunSetup>().Resumed, "Hardcore starts at wave 1");
                MetaProgress.ForgetAchievement(MetaProgress.MadmanAchievement);
            });
            At(14.8f, () =>
            {
                ModeBanner banner = Find<ModeBanner>();
                Check(banner.Shown && banner.Title == "РЕЖИМ: ХАРДКОР", $"the banner: \"{banner.Title}\"");
                Shot("19_ModeBanner_Hardcore");
                s_Director.SkipTo(10, 0.3f);
            });
            for (float t = 15.0f; t < 24f; t += 0.2f)
                At(t, KeepAlive);
            At(16.2f, () => s_Director.Boss?.TakeDamage(1000000, Vector3.right));
            for (float t = 16.6f; t < 18.5f; t += 0.3f)
                At(t, KillAll);
            At(18.8f, () =>
            {
                Check(MetaProgress.GetCheckpoint(map, Core.Difficulty.Hardcore) == null && !Find<StageClearScreen>().IsOpen,
                    "Hardcore saves nothing after a boss");
                s_Director.SkipTo(30, 0.3f);
            });
            At(20.2f, () => s_Director.Boss?.TakeDamage(1000000, Vector3.right));
            At(21.0f, () =>
            {
                Check(s_Run.Outcome == RunOutcome.Victory && MetaProgress.IsAchieved(MetaProgress.MadmanAchievement) &&
                      s_Run.NewAchievement == MetaProgress.MadmanAchievement, "a Hardcore victory: \"БЕЗУМЕЦ!\" is unlocked");
            });
            At(27.4f, () =>
            {
                Check(Find<AchievementToast>().Shown, "the notification slides in over the victory screen");
                Shot("19_Achievement_Toast");
                MetaProgress.LastDifficulty = Core.Difficulty.Hardcore;
                SceneLoader.EnsureExists().LoadMainMenu();
            });

            // ---- the menu: achievements, the daily bonus, the difficulty choice
            AchievementsPanel achievements = null;
            DifficultyScreen screen = null;
            At(30.4f, () =>
            {
                Check(SceneManager.GetActiveScene().name == SceneNames.c_MainMenu, "back in the main menu");
                var menu = Find<MainMenuController>();
                achievements = menu.Achievements;
                Check(menu.transform.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Mark" && t.gameObject.activeInHierarchy &&
                      t.parent.name == "AchievementsButton"), "ДОСТИЖЕНИЯ carries a mark: a reward waits");
                achievements.Open();
            });
            At(31.0f, () =>
            {
                Shot("19_Achievements_Unlocked");
                int coins = MetaProgress.Coins;
                Check(achievements.Claim(0) && MetaProgress.Coins == coins + 676, $"the glowing reward is taken: +676 ({coins} -> {MetaProgress.Coins})");
                Check(!achievements.Claim(0), "once only");
                MetaProgress.ForgetAchievement(MetaProgress.MadmanAchievement);
                achievements.Close();
                achievements.Open();
            });
            At(31.6f, () =>
            {
                Shot("19_Achievements_Locked");
                Check(!achievements.Claim(0), "a locked achievement gives nothing (its reward is only shown)");
                achievements.Close();
                MetaProgress.SetDaily(0, -1);
                Find<MainMenuController>().Daily.Open();
            });
            At(32.2f, () =>
            {
                Shot("19_Daily_Ready");
                int free = MetaProgress.FreeCases;
                DailyBonusPanel daily = Find<MainMenuController>().Daily;
                Check(DailyBonusPanel.Days[0].Kind == DailyBonusPanel.Kind.Case && daily.Take() && MetaProgress.FreeCases == free + 1 &&
                      !MetaProgress.DailyReady && MetaProgress.DailyDay == 1, "day 1 taken: a free case, the next day waits for tomorrow");
                Check(Find<MainMenuController>().Cases.IsOpen, "the free case waits on the cases screen (it opens)");
                Check(!daily.Take(), "one bonus a day");
                MetaProgress.SetDaily(3, MetaProgress.Today - 4);                // days missed: the row waits, it does not reset
                Check(MetaProgress.DailyReady && MetaProgress.DailyDay == 3, "missed days do not reset the row");
            });
            At(32.8f, () =>
            {
                Shot("19_Cases_FreeCase");
                Find<MainMenuController>().Cases.Hide();
            });
            At(33.6f, () =>
            {
                Check(Find<MainMenuController>().transform.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Mark" &&
                      t.gameObject.activeSelf && t.parent.name == "CasesButton"), "КЕЙСЫ carries a mark while the free case waits");
                MetaProgress.TryUseFreeCase();                                   // the developer's own collection stays as it was
            });
            At(33.8f, () =>
            {
                MetaProgress.HardcoreConfirmed = false;
                MetaProgress.SaveCheckpoint(new MetaProgress.Checkpoint { map = map, difficulty = (int)Core.Difficulty.Easy, wave = 11 });
                screen = Find<DifficultyScreen>();
                Find<MainMenuController>().ShowMapSelection(true);
                screen.Open(AssetDatabase.LoadAssetAtPath<MapConfig>(DataAssetsBuilder.c_MapConfig), "ГОРОД  •  ДЕНЬ");
            });
            At(34.2f, () =>
            {
                Check(screen.IsOpen && screen.State == "cards", "ИГРАТЬ: the three difficulty cards");
                Shot("19_Difficulty_Cards");
                screen.Choose(Core.Difficulty.Easy);
            });
            At(34.7f, () =>
            {
                Check(screen.State == "resume", "Easy with a saved run: go on from it or start over");
                Shot("19_Difficulty_Resume");
                screen.Back();
                screen.Choose(Core.Difficulty.Hardcore);
            });
            At(35.2f, () =>
            {
                Check(screen.State == "warn", "Hardcore the first time: no saves — sure?");
                Shot("19_Difficulty_Warn");
                screen.Warned();
            });
            At(35.7f, () =>
            {
                Check(screen.State == "sure", "then \"ТОЧНО?)\" with one big ДА");
                Shot("19_Difficulty_Sure");
                screen.ConfirmHardcore();
            });
            At(39.2f, () =>
            {
                Check(SceneManager.GetActiveScene().name == SceneNames.c_Map01, "ДА: the map loads");
                Rebind();
                Check(s_Run.Difficulty == Core.Difficulty.Hardcore && MetaProgress.HardcoreConfirmed, "on Hardcore, the warning never asked again");
                Cleanup();
            });
            At(39.5f, Finish);
        }
    }
}
