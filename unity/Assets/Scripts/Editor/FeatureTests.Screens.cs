using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 12. boss bar, boss card, revive
        /// <summary>
        /// A boss comes out: the card before the fight pauses the game; then the wave pill shows the boss's health
        /// and follows the damage, and turns back into the waves after the kill. A defeat offers the revive for an
        /// ad: the car and the player are back and the run goes on; the next defeat has no revive.
        /// </summary>
        private static void Screens()
        {
            BossIntroView intro = null;
            CanvasGroup bossBar = null;
            GameOverScreen screen = null;
            ZombieController boss = null;
            At(1.0f, () =>
            {
                Find<LevelUpScreen>().enabled = false;
                intro = Find<BossIntroView>();
                bossBar = Find<WaveProgressView>().transform.Find("BossBar").GetComponent<CanvasGroup>();
                // Narrow screens (Yandex fullscreen in a browser, a 4:3 tablet): the top row of the HUD must not overlap.
                Check(ShotSized("12_HUD_Narrow", 1528, 1040), "HUD at 1528x1040 (browser fullscreen): the top panels do not overlap");
                Check(ShotSized("12_HUD_4x3", 1440, 1080), "HUD at 4:3: the top panels do not overlap");
                Check(ShotSized("12_HUD_Wide", 2400, 1080), "HUD at 20:9: the top panels do not overlap");
                s_Director.SkipTo(10, 0.3f);
            });
            for (float t = 1.5f; t < 21.5f; t += 0.5f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                });
            At(2.4f, () =>
            {
                boss = s_Director.Boss;
                Check(boss != null && intro.IsOpen && GamePause.Has(GamePause.Reason.BossIntro) && Time.timeScale == 0f,
                    "the boss card comes up and the game waits");
            });
            At(15.4f, () =>
            {
                Check(intro.IsOpen && GamePause.Has(GamePause.Reason.BossIntro) && s_Waves.HasBoss,
                    "13 s later the card still waits: the fight starts only on В БОЙ!");
                Shot("12_Boss_Card");
                intro.Close();
            });
            At(15.6f, () => Check(!intro.IsOpen && !GamePause.IsPaused, "В БОЙ!: the fight begins"));
            // The boss rises out of the ground first (game time, which the card paused): hit it once it is up.
            At(17.4f, () =>
            {
                if (boss != null)
                    boss.TakeDamage(boss.MaxHealth / 2, Vector3.right);
            });
            At(18.0f, () =>
            {
                Check(bossBar.gameObject.activeInHierarchy && bossBar.alpha > 0.95f, "the wave pill shows the boss's health");
                Check(s_Waves.BossMaxHealth > 1 && s_Waves.BossHealth <= s_Waves.BossMaxHealth / 2 + 1,
                    $"the boss bar follows the damage ({s_Waves.BossHealth} / {s_Waves.BossMaxHealth})");
                Shot("12_Boss_Bar");
                if (boss != null)
                    boss.TakeDamage(1000000, Vector3.right);
            });
            At(19.8f, () =>
            {
                Check(!s_Waves.HasBoss && (!bossBar.gameObject.activeInHierarchy || bossBar.alpha < 0.05f), "after the kill the waves are back");
                s_Director.enabled = false;
                s_Car.TakeDamage(100000, Vector3.right);
            });
            At(22.0f, () =>
            {
                screen = Find<GameOverScreen>();
                Check(screen.IsShown && screen.CanRevive, "defeat: ВОСКРЕСНУТЬ ЗА РЕКЛАМУ is offered");
                Shot("12_Defeat");
                screen.Revive();                                              // outside Yandex the reward comes at once
            });
            At(22.6f, () =>
            {
                Check(s_Run.Outcome == RunOutcome.None && !screen.IsShown, "revived: the run goes on");
                Check(s_Car.Health == s_Car.MaxHealth && s_PlayerHealth.IsAlive, "the car and the player are back up");
                Check(s_Director.enabled && !GamePause.Has(GamePause.Reason.GameOver), "the waves and the game go on");
                s_PlayerHealth.TakeDamage(100000, Vector3.right);
            });
            At(24.6f, () =>
            {
                Check(s_Run.Outcome == RunOutcome.PlayerDied && screen.IsShown && !screen.CanRevive, "the revive is once per run");
                Shot("12_Defeat_NoRevive");
            });
            At(24.8f, Finish);
        }

        // ================================================================== 13. tutorial
        /// <summary>The intro cards (pause, Далее / Пропустить), then the live hint of the first zombie.</summary>
        private static void Tutorial()
        {
            TutorialCardsView cards = null;
            TutorialState state = AssetDatabase.LoadAssetAtPath<TutorialState>(DataAssetsBuilder.c_TutorialState);
            At(0.8f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;
                cards = Find<TutorialCardsView>();
                Find<TutorialDirector>().Restart();
            });
            At(1.2f, () =>
            {
                Check(cards.IsOpen && cards.Index == 0 && cards.Count == 6, $"the intro cards open on the first of {cards.Count}");
                Check(GamePause.Has(GamePause.Reason.Tutorial) && Time.timeScale == 0f, "the game waits behind them");
                Shot("13_Tutorial_Card1");
                cards.Next();
            });
            At(1.7f, () =>
            {
                Check(cards.Index == 1, "ДАЛЕЕ turns the card");
                Shot("13_Tutorial_Card2");
                cards.Next();
            });
            At(1.95f, () =>
            {
                Check(cards.Index == 2, "the weapon card");
                Shot("13_Tutorial_Card3_Weapon");
                cards.Next();
                cards.Next();
                cards.Next();
            });
            At(2.2f, () =>
            {
                Check(cards.Index == 5, "the last card");
                Shot("13_Tutorial_Card6");
                cards.Next();                                                 // ИГРАТЬ!
            });
            At(2.6f, () =>
            {
                Check(!cards.IsOpen && !GamePause.IsPaused && MetaProgress.TutorialDone, "the cards close and the game runs");
                Check(Find<TutorialDirector>().Running, "the live hints are on");
                s_Spawner.SpawnAt(Zombie("Walker"), s_Player.position + new Vector3(6f, 0f, 2f));
            });
            At(3.4f, () =>
            {
                Check(state.Visible && state.Text.Contains("ЗОМБИ"), $"live hint for the first zombie: '{state.Text}'");
                Shot("13_Live_Hint");
            });
            At(3.6f, () =>
            {
                MetaProgress.TutorialDone = MetaProgress.LiveHintsDone = true;
                Finish();
            });
        }
    }
}
