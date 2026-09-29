using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.DevTools;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 10. developer cheats (Insert / F8)
        /// <summary>
        /// Presses every button of the cheat panel through its real onClick, one after another, and checks after each
        /// that the game still runs (no pause left behind, game time moves, nothing logged an error) and that the
        /// button did its job: upgrades +1 / max for free, the whole car maxed, waves 10 / 20 / 30 with their bosses,
        /// the next wave, scrap, ammo, healing, clearing the zombies, coins, the tutorial.
        /// </summary>
        private static void Cheats()
        {
            CheatPanel panel = null;
            CarUpgradeCatalog catalog = null;
            float gameTimeBefore = 0f;
            float t = 1.0f;

            // Level-up cards come after the kills: take a card whenever they are up, so the game goes on.
            for (float x = 1.2f; x < 40f; x += 0.4f)
                At(x, () =>
                {
                    LevelUpScreen levelUp = Find<LevelUpScreen>();
                    if (levelUp != null && levelUp.IsOpen)
                        levelUp.Pick(0);
                });

            void Press(string label)
            {
                Button button = panel.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>().text == label);
                Check(button != null, $"button '{label}' on the panel");
                gameTimeBefore = Time.time;
                button?.onClick.Invoke();
            }
            void Pressed(string label, System.Func<bool> ok, string what)
            {
                At(t += 0.05f, () => Press(label));
                At(t += 0.7f, () =>
                {
                    bool running = Time.timeScale > 0f || GamePause.Has(GamePause.Reason.LevelUp);
                    Check(running && Time.time > gameTimeBefore || GamePause.Has(GamePause.Reason.LevelUp),
                        $"'{label}': the game runs on (timeScale {Time.timeScale}, pauses {GamePause.Reasons})");
                    Check(ok(), $"'{label}': {what}");
                });
            }
            void PressedRow(string title, string label, System.Func<bool> ok, string what)
            {
                At(t += 0.05f, () =>
                {
                    Transform row = panel.GetComponentsInChildren<TMP_Text>(true).First(x => x.text.StartsWith(title)).transform.parent;
                    Button button = row.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<TMP_Text>().text == label);
                    button.onClick.Invoke();
                });
                At(t += 0.4f, () => Check(ok(), $"{title} '{label}': {what}"));
            }

            At(t, () =>
            {
                s_Director.enabled = false;
                panel = Object.FindAnyObjectByType<CheatPanel>();
                Check(panel != null, "the cheat panel is there in the editor");
                panel.SetOpen(true);
                Check(panel.IsOpen && !GamePause.IsPaused && Time.timeScale == 1f, "the panel is open and the game is NOT paused");
                catalog = AssetDatabase.LoadAssetAtPath<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
            });
            At(t += 0.3f, () => { });

            int moneyBefore = 0;
            At(t += 0.05f, () => moneyBefore = s_Run.Money);
            CarUpgrade Up(string id) => catalog.Upgrades.First(u => u.Id == id);
            PressedRow("БРОНЯ", "+1", () => s_Run.LevelOf(Up("armor")) == 1, "armour level 1");
            PressedRow("ШИПЫ", "МАКС", () => s_Run.LevelOf(Up("spikes")) == Up("spikes").MaxLevel && s_Car.Spikes == 32, "spikes maxed");
            PressedRow("РЕМОНТ", "СДЕЛАТЬ", () => s_Run.LevelOf(Up("repair")) == 1, "a repair");
            Pressed("ВСЯ МАШИНА НА МАКСИМУМ",
                () => catalog.Upgrades.Where(u => !u.Repeatable).All(u => s_Run.LevelOf(u) == u.MaxLevel) && s_Run.Money == moneyBefore &&
                      s_Car.Health == s_Car.MaxHealth, "every upgrade maxed for free, the car repaired");
            Pressed("+1000 МЕТАЛЛОЛОМА", () => s_Run.Money >= moneyBefore + 1000, "scrap +1000");
            At(t += 0.05f, () => { s_Weapon.Holstered = false; });
            Pressed("ПОЛНЫЕ ПАТРОНЫ", () => s_Weapon.IsFull, "magazine and reserve full");
            At(t += 0.05f, () => { s_PlayerHealth.TakeDamage(30, Vector3.right); s_Car.TakeDamage(50, Vector3.right); });
            Pressed("ВЫЛЕЧИТЬ ИГРОКА И МАШИНУ", () => s_Stats.Health == s_Stats.MaxHealth && s_Car.Health == s_Car.MaxHealth, "player and car healed");
            At(t += 0.05f, () =>
            {
                foreach (Vector3 offset in new[] { new Vector3(6f, 0f, 3f), new Vector3(-6f, 0f, 4f) })
                    s_Spawner.SpawnAt(Zombie("Walker"), s_Car.transform.position + offset);
            });
            int killsBefore = 0;
            At(t += 0.05f, () => killsBefore = s_Run.Kills);   // right after the spawn: both still rising from the ground
            Pressed("УБИТЬ ВСЕХ ЗОМБИ", () => !LiveZombies().Any() && s_Run.Kills == killsBefore + 2,
                "both zombies killed, also while rising, and counted as kills");
            At(t += 0.05f, () => s_Director.enabled = true);
            int levelBeforeSkip = 0;
            At(t += 0.05f, () =>
            {
                levelBeforeSkip = s_Stats.Level;
                s_Spawner.SpawnAt(Zombie("Walker"), s_Car.transform.position + new Vector3(5f, 0f, -4f));
            });
            foreach (int wave in new[] { 10, 20, 30 })
            {
                int w = wave;
                Pressed($"ВОЛНА {w}", () => s_Director.Wave == w && !LiveZombies().Any() && !GamePause.IsPaused &&
                                            s_Stats.Level == levelBeforeSkip,
                    $"countdown to wave {w}, the map cleared without rewards (no level-up pause)");
                At(t += 4.5f, () =>                                   // 3 s countdown + level-up cards from the kills
                {
                    Check(s_Director.Wave == w && s_Director.IsWaveRunning && s_Director.Boss != null,
                        $"wave {w} runs with its boss ({(s_Director.Boss != null ? s_Director.Boss.Config.DisplayName : "none")})");
                    Shot($"10_Cheats_Wave{w}");
                });
            }
            At(t += 0.05f, () => s_Director.SkipTo(12, 3f));
            At(t += 0.2f, () => { });
            Pressed("СЛЕДУЮЩАЯ ВОЛНА", () => s_Director.Wave == 12, "a break before wave 12: the countdown restarts to it");
            int coins = 0;
            At(t += 0.05f, () => coins = MetaProgress.Coins);
            Pressed("+1000 МОНЕТ ДЛЯ КЕЙСОВ", () => MetaProgress.Coins == coins + 1000, "coins +1000");
            At(t += 0.05f, () => MetaProgress.AddCoins(0));
            Pressed("ПОКАЗАТЬ ОБУЧЕНИЕ СНОВА",
                () => !MetaProgress.TutorialDone && Object.FindAnyObjectByType<TutorialDirector>().Running, "the tutorial runs again");
            At(t += 0.05f, () =>
            {
                // The screenshot helper puts overlay canvases in front of the camera by their sorting order: the
                // panel's (5000) would land behind it, so it is lowered for the picture only.
                Canvas canvas = panel.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = Camera.main.nearClipPlane + 0.1f;
                Shot("10_Cheats_Panel");
            });
            At(t += 0.05f, () =>
            {
                MetaProgress.TutorialDone = true;
                MetaProgress.TrySpend(1000);                           // leave the developer's coins as they were
                Press("ЗАКРЫТЬ");
            });
            At(t += 0.4f, () =>
            {
                Check(!panel.IsOpen && (Time.timeScale == 1f || GamePause.Reasons == GamePause.Reason.LevelUp),
                    $"the panel closes, the game runs (pauses: {GamePause.Reasons})");
                Shot("10_Cheats_End");
            });
            At(t += 0.2f, Finish);
        }
    }
}
