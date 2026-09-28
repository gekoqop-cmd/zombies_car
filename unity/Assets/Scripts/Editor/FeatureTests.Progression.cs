using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        private static void RegisterScenarios()
        {
            Register("progression", Progression);
        }

        // ================================================================== 1. workshop, levels, cards
        /// <summary>
        /// Workshop prompt and window (between waves only), car upgrades bought and applied (armour, hull, spikes,
        /// discharge, repair), level-ups with three cards and the pause, several level-ups in a row, experience and
        /// scrap for kills and cleared waves.
        /// </summary>
        private static void Progression()
        {
            var workshopZone = Vector3.zero;
            WorkshopScreen workshop = null;
            LevelUpScreen levelUp = null;
            int money = 0;
            string perkId = null;
            int perkStacks = 0;

            At(1.0f, () =>
            {
                workshop = Find<WorkshopScreen>();
                levelUp = Find<LevelUpScreen>();
                Check(workshop != null && levelUp != null, "HUD has the workshop and level-up screens");
                var pool = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool);
                var catalog = AssetDatabase.LoadAssetAtPath<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
                Check(pool.Perks.Count >= 15, $"card pool has {pool.Perks.Count} cards (>= 15)");
                Check(catalog.Upgrades.Length >= 5, $"workshop sells {catalog.Upgrades.Length} car upgrades");
                Check(s_Run.Money == 0 && s_Stats.Level == 1 && s_Run.Kills == 0, "a fresh run: no scrap, level 1, no kills");
                s_Director.enabled = false;                                   // the scenario controls the waves
                workshopZone = Find<WorkshopZone>().transform.position;
                Teleport(workshopZone + new Vector3(0.4f, 0f, -0.6f));
            });
            At(1.6f, () =>
            {
                Check(s_Interaction.Visible && s_Interaction.Available && s_Interaction.Text.Contains("МАСТЕРСКАЯ"),
                    $"workshop prompt shown near the garage: '{s_Interaction.Text}'");
                Shot("1_Workshop_Prompt");
                Press();
            });
            At(2.2f, () =>
            {
                Check(s_Run.WorkshopOpen && workshop.IsOpen, "E opens the workshop");
                Check(GamePause.Has(GamePause.Reason.Workshop) && Time.timeScale == 0f, "the game is paused in the workshop");
                s_Run.AddMoney(700);
            });
            At(2.8f, () => Shot("1_Workshop_Open"));
            At(3.0f, () =>
            {
                int carMax = s_Car.MaxHealth;
                Check(workshop.Buy(0), "bought armour");
                Check(Mathf.Approximately(s_Car.Armor, 0.08f), $"car armour {s_Car.Armor:P0}");
                Check(workshop.Buy(1), "bought the reinforced hull");
                Check(s_Car.MaxHealth == carMax + 60 && s_Car.Health == s_Car.MaxHealth, $"car durability {s_Car.Health}/{s_Car.MaxHealth}");
                Check(workshop.Buy(2), "bought spikes");
                Check(s_Car.Spikes == 8, $"spikes {s_Car.Spikes}");
                Check(workshop.Buy(3), "bought the electric discharge");
                money = s_Run.Money;
                Check(money == 700 - 60 - 50 - 70 - 150, $"scrap left {money}");
                Check(workshop.Buy(5), "bought a repair");
                Check(workshop.Buy(3), "bought the discharge's second level");
                Check(!workshop.Buy(3), $"the third discharge level (450) is refused with {s_Run.Money} scrap");
            });
            At(3.4f, () => Shot("1_Workshop_Bought"));
            At(3.5f, () =>
            {
                workshop.Close();
            });
            At(3.8f, () =>
            {
                Check(!s_Run.WorkshopOpen && !GamePause.IsPaused && Time.timeScale == 1f, "closing the workshop resumes the game");
                Teleport(new Vector3(-1.5f, 0f, -3f));
                s_Progression.AddXp(35);
            });
            At(4.6f, () =>
            {
                Check(s_Stats.Level == 2, $"35 XP: level {s_Stats.Level}");
                Check(levelUp.IsOpen && levelUp.Hand.Count == 3, $"level-up shows {levelUp.Hand.Count} cards");
                Check(levelUp.Hand.Select(p => p.Id).Distinct().Count() == levelUp.Hand.Count, "the three cards are different");
                Check(GamePause.Has(GamePause.Reason.LevelUp) && Time.timeScale == 0f, "the game is paused while choosing");
                Shot("1_LevelUp_Cards");
                perkId = levelUp.Hand[0].Id;
                perkStacks = s_Run.StacksOf(levelUp.Hand[0]);
                levelUp.Pick(0);
            });
            At(5.4f, () =>
            {
                Perk taken = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool).Perks.First(p => p.Id == perkId);
                Check(s_Run.StacksOf(taken) == perkStacks + 1, $"card '{perkId}' taken");
                Check(!levelUp.IsOpen && !GamePause.IsPaused, "the game goes on after the card");
                s_Progression.AddXp(250);
            });
            At(6.1f, () =>
            {
                Check(s_Run.PendingLevelUps >= 2 && levelUp.IsOpen, $"several level-ups at once: {s_Run.PendingLevelUps} waiting");
                Shot("1_LevelUp_Second");
                levelUp.Pick(1);
            });
            for (int i = 0; i < 4; i++)
                At(6.9f + i * 0.8f, () => { if (levelUp.IsOpen) levelUp.Pick(2); });
            At(10.2f, () =>
            {
                Check(s_Run.PendingLevelUps == 0 && !levelUp.IsOpen && !GamePause.IsPaused,
                    $"all level-ups chosen, level {s_Stats.Level}");
                Vector3 car = s_Car.transform.position;
                foreach (Vector3 offset in new[] { new Vector3(2.6f, 0f, 1.5f), new Vector3(-2.6f, 0f, -1.5f), new Vector3(0.5f, 0f, 3.6f) })
                    s_Spawner.SpawnAt(Zombie("Walker"), car + offset);
                s_Car.Repair(1000);
            });
            int carBefore = 0;
            At(10.3f, () => carBefore = s_Car.Health);
            At(14.0f, () =>
            {
                Vector3 car = s_Car.transform.position;
                s_Spawner.SpawnAt(Zombie("Worker"), car + new Vector3(3f, 0f, 0f));
                s_Spawner.SpawnAt(Zombie("Worker"), car + new Vector3(-3f, 0f, 0.5f));
            });
            At(15.5f, () =>
            {
                ZombieController[] zombies = LiveZombies().ToArray();
                Check(s_Car.Health < carBefore, $"zombies hit the car: {carBefore} -> {s_Car.Health}");
                bool spiked = zombies.Any(z => z.Health < z.MaxHealth) || s_Spawner.Killed > 0;
                Check(spiked, "the car's spikes hurt the zombies hitting it");
                Shot("1_Car_Attacked");
            });
            int killsBefore = 0, moneyBefore = 0;
            At(15.6f, () =>
            {
                ZombieController[] zombies = LiveZombies().ToArray();
                int[] before = zombies.Select(z => z.Health).ToArray();
                Time.timeScale = 0.1f;                                           // slow motion: catch the ring
                int zapped = Find<CarShockField>().Pulse(20);
                Check(zapped > 0, $"the discharge zapped {zapped} zombies");
                Check(zombies.Where((z, i) => !z.IsAlive || z.Health <= before[i] - 20).Any(), "the discharge dealt 20 damage");
            });
            At(15.7f, () =>
            {
                Shot("1_Shock_Ring");
                Time.timeScale = 1f;
            });
            ZombieController victim = null;
            At(15.8f, () =>
            {
                victim = s_Spawner.SpawnAt(Zombie("Walker"), new Vector3(8f, 0f, -8f));
                Teleport(workshopZone + new Vector3(0.4f, 0f, -0.6f));
                s_Waves.StartWave(2);                                           // a wave running
            });
            At(16.4f, () =>
            {
                Check(s_Interaction.Visible && !s_Interaction.Available && s_Interaction.Text.Contains("ПОСЛЕ ВОЛНЫ"),
                    $"during a wave the workshop is closed: '{s_Interaction.Text}'");
                Press();
            });
            At(16.8f, () =>
            {
                Check(!s_Run.WorkshopOpen, "E does not open the workshop during a wave");
                Shot("1_Workshop_Closed_DuringWave");
                moneyBefore = s_Run.Money;
                s_Waves.EndWave(2, false);
            });
            At(17.0f, () =>
            {
                Check(s_Run.Money > moneyBefore, $"a cleared wave pays a bonus: {moneyBefore} -> {s_Run.Money}");
                Check(s_Interaction.Available, "after the wave the workshop opens again");
                killsBefore = s_Run.Kills;
                moneyBefore = s_Run.Money;
                int xpBefore = s_Stats.Xp + s_Stats.Level * 100000;
                victim.TakeDamage(999, Vector3.right);
                Check(s_Run.Kills == killsBefore + 1, $"a kill counts: {s_Run.Kills}");
                Check(s_Run.Money > moneyBefore, $"a kill pays scrap: {moneyBefore} -> {s_Run.Money}");
                Check(s_Stats.Xp + s_Stats.Level * 100000 > xpBefore, "a kill gives experience");
            });
            At(17.2f, Finish);
        }
    }
}
