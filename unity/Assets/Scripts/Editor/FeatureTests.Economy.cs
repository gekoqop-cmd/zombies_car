using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 14. parts economy
        /// <summary>
        /// Where the scrap goes: a master level after an upgrade's last level (+2%, price ×1.35 each), supplies bought
        /// again and again with growing prices and "НЕ НУЖНО" when useless (first-aid kit, repair, ammo, mines that
        /// blow up a zombie), the level-up reroll (50 → 100 → 150), and the scrap left exchanged for coins at the end
        /// (2000 → +400).
        /// </summary>
        private static void Economy()
        {
            WorkshopScreen workshop = null;
            LevelUpScreen levelUp = null;
            CarUpgradeCatalog catalog = null;
            CarUpgrade armor = null;
            GameOverScreen screen = null;
            int money = 0;
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                workshop = Find<WorkshopScreen>();
                levelUp = Find<LevelUpScreen>();
                screen = Find<GameOverScreen>();
                catalog = AssetDatabase.LoadAssetAtPath<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
                armor = catalog.Upgrades.First(u => u.Id == "armor");
                s_Run.AddMoney(20000);
                s_Run.SetWorkshopOpen(true);
            });
            At(1.5f, () =>
            {
                Check(workshop.IsOpen && catalog.Supplies.Length == 4, $"the workshop sells {catalog.Supplies.Length} supplies");
                // Every upgrade and supply sounds different, and every one of those sounds has a clip.
                var items = catalog.Upgrades.Concat(catalog.Supplies).ToArray();
                var sounds = items.Select(WorkshopScreen.BoughtSound).ToArray();
                var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(AudioBuilder.c_SoundBank);
                Check(sounds.Distinct().Count() == items.Length && !sounds.Contains(Sound.UiBuy) && sounds.All(s => bank.Get(s) != null),
                    $"{items.Length} workshop items, {sounds.Distinct().Count()} own sounds with clips");
                // Growing gains: every level gives more than the one before, the tile shows the next one.
                CarUpgrade spikes = catalog.Upgrades.First(u => u.Id == "spikes");
                Check(Enumerable.Range(0, 4).Select(spikes.ValueAt).SequenceEqual(new[] { 8f, 10f, 12f, 14f }) &&
                      spikes.ValueAt(4) == 2f && spikes.ValueAt(5) == 3f, "spikes +8, +10, +12, +14, then master +2, +3");
                Check(armor.DescriptionAt(0).Contains("6%") && armor.DescriptionAt(1).Contains("7%"), $"the tile shows the next gain: '{armor.DescriptionAt(1)}'");
                for (int i = 0; i < armor.RegularLevels; i++)
                    workshop.Buy(0);
                Check(s_Run.LevelOf(armor) == 5 && Mathf.Approximately(s_Car.Armor, 0.4f), $"armour maxed (6+7+8+9+10%): {s_Car.Armor:P0}");
                int master1 = armor.CostAt(5);
                Check(master1 == Mathf.RoundToInt(600 * 1.35f), $"master level 1 costs 600 × 1.35 = {master1}");
                money = s_Run.Money;
                Check(workshop.Buy(0) && s_Run.Money == money - master1, "master level 1 bought");
                Check(Mathf.Approximately(s_Car.Armor, 0.415f), $"master level 1: armour +1.5% ({s_Car.Armor:P1})");
                int master2 = armor.CostAt(6);
                Check(master2 == Mathf.RoundToInt(600 * 1.35f * 1.35f) && armor.MasterLevel(6) == 1 && Mathf.Approximately(armor.ValueAt(6), 0.02f),
                    $"master level 2 costs {master2} and gives +2%");
                Check(workshop.Buy(0) && armor.MasterLevel(s_Run.LevelOf(armor)) == 2, "master level 2 bought: МАСТЕР 2");
            });
            At(1.9f, () => Shot("14_Workshop_Master"));
            At(2.1f, () =>
            {
                CarUpgrade medkit = catalog.Supplies[0], ammo = catalog.Supplies[1], repair = catalog.Supplies[3];
                Check(!workshop.BuySupply(0), "first-aid kit with full health: НЕ НУЖНО");
                s_PlayerHealth.TakeDamage(40, Vector3.right);
                Check(medkit.CostAt(s_Run.LevelOf(medkit)) == 60 && workshop.BuySupply(0) && s_Stats.Health == s_Stats.MaxHealth,
                    "first-aid kit (60): full health");
                s_PlayerHealth.TakeDamage(40, Vector3.right);
                Check(medkit.CostAt(s_Run.LevelOf(medkit)) == 80 && workshop.BuySupply(0), "the second kit costs 80 (+20)");
                s_Weapon.Refill();
                Check(!workshop.BuySupply(1), "ammo box with a full gun: НЕ НУЖНО");
                Check(ammo.CostAt(0) == 40, "ammo box costs 40");
                Check(workshop.BuySupply(2) && s_Run.ArmedMines == 4, "mines (120): 4 around the car");
                var mines = s_Car.GetComponent<CarMines>();
                Check(mines != null && !Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                        .Any(tr => tr.name == "Light" && tr.parent != null && tr.parent.name.StartsWith("Mine")),
                    "the mines are plain models: no light on them");
                CloseShot("14_Mines", s_Car.transform.position, 11f);
                Check(!workshop.BuySupply(2), "more mines while 4 are armed: НЕ НУЖНО");

                // The repair: only for an ad (the editor's stand-in rewards at once), +200 up to the maximum, 3 a run.
                Check(repair.ForAd && repair.AdUsesPerRun == 3, "the repair is for an ad, 3 times a run");
                Check(!workshop.BuySupply(3), "repair of an intact car: НЕ НУЖНО");
                money = s_Run.Money;
                for (int i = 0; i < 3; i++)
                {
                    s_Car.TakeDamage(300, Vector3.right);
                    int before = s_Car.Health;
                    Check(workshop.BuySupply(3) && s_Car.Health == Mathf.Min(before + 200, s_Car.MaxHealth),
                        $"ad repair {i + 1}: {before} → {s_Car.Health} (+200, capped at {s_Car.MaxHealth})");
                }
                Check(s_Run.Money == money, "the repairs cost no scrap");
                s_Car.TakeDamage(300, Vector3.right);
                Check(!workshop.BuySupply(3) && s_Run.LevelOf(repair) == 3, "a 4th repair: ИСПОЛЬЗОВАНО");
            });
            At(2.5f, () => Shot("14_Workshop_Supplies"));
            At(2.7f, () =>
            {
                workshop.Close();
                Vector3 car = s_Car.transform.position;
                s_Spawner.SpawnAt(Zombie("Walker"), car + new Vector3(2.55f, 0f, 2.55f));
            });
            At(6.5f, () =>
            {
                Check(s_Run.ArmedMines == 3, $"a zombie stepped on a mine: {s_Run.ArmedMines} left");
                s_Progression.AddXp(35);
            });
            At(7.3f, () =>
            {
                Check(levelUp.IsOpen, "level-up cards for the reroll");
                money = s_Run.Money;
                int rerolls = s_Run.Rerolls;
                // For a rewarded ad (the stand-in rewards at once): a new hand, no parts spent.
                Check(levelUp.Reroll() && s_Run.Rerolls == rerolls + 1 && s_Run.Money == money, "ПЕРЕБРОСИТЬ for an ad: a new hand, parts kept");
            });
            At(7.8f, () => Check(levelUp.Reroll() && s_Run.Money == money, "and again: every reroll is an ad, never parts"));
            At(8.2f, () => Shot("14_LevelUp_Reroll"));
            At(8.4f, () => levelUp.Pick(0));
            At(9.2f, () =>
            {
                s_Run.TrySpend(s_Run.Money - 2000);
                Check(s_Run.Money == 2000, "2000 parts left");
                s_Car.TakeDamage(100000, Vector3.right);
            });
            At(11.4f, () =>
            {
                Check(screen.IsShown && screen.ScrapCoins == 400, $"the end exchanges the parts: 2000 → +{screen.ScrapCoins}");
                var line = screen.transform.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.name == "Scrap");
                Check(line != null && line.text.Contains("2000 → +400"), $"the defeat screen says so: '{line?.text}'");
                Check(screen.CoinsEarned >= 400, $"the parts are in the coins earned ({screen.CoinsEarned})");
                Shot("14_Scrap_Exchange");
                // ×2 for an ad (the stand-in rewards at once): the run's coins twice, the extra paid in, once a run.
                int total = screen.CoinsEarned;
                int balance = MetaProgress.Coins;
                Check(screen.CanDouble, "×2 is offered");
                screen.DoubleCoins();
                Check(screen.CoinsEarned == total * 2 && MetaProgress.Coins == balance + total,
                    $"×2: {total} → {screen.CoinsEarned}, the balance {balance} → {MetaProgress.Coins}");
                Check(!screen.CanDouble, "×2 is used up (ИСПОЛЬЗОВАНО)");
                screen.DoubleCoins();
                Check(MetaProgress.Coins == balance + total, "no second ×2");
            });
            At(13.4f, () =>
            {
                Check(screen.BalanceText == MetaProgress.Coins.ToString(), $"the balance rolled up to {screen.BalanceText}");
                Shot("14_Coins_Doubled");
                MetaProgress.TrySpend(screen.CoinsEarned);                    // the developer's own coins stay as they were
            });
            At(13.6f, Finish);
        }
    }
}
