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
        /// Where the scrap goes: upgrades up to their last level (then MAX, nothing more to buy), supplies bought
        /// for an ad and "НЕ НУЖНО" when useless (first-aid kit, repair, the eight mines that
        /// blow up a zombie), the level-up reroll (for an ad, no scrap), and the scrap left exchanged for coins at the end
        /// (2000 → +40).
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
                Check(workshop.IsOpen && catalog.Supplies.Length == 3, $"the workshop sells {catalog.Supplies.Length} supplies");
                // Every upgrade and supply sounds different, and every one of those sounds has a clip.
                var items = catalog.Upgrades.Concat(catalog.Supplies).ToArray();
                var sounds = items.Select(WorkshopScreen.BoughtSound).ToArray();
                var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(AudioBuilder.c_SoundBank);
                Check(sounds.Distinct().Count() == items.Length && !sounds.Contains(Sound.UiBuy) && sounds.All(s => bank.Get(s) != null),
                    $"{items.Length} workshop items, {sounds.Distinct().Count()} own sounds with clips");
                // Growing gains: every level gives more than the one before, the tile shows the next one.
                CarUpgrade spikes = catalog.Upgrades.First(u => u.Id == "spikes");
                Check(Enumerable.Range(0, 6).Select(spikes.ValueAt).SequenceEqual(new[] { 8f, 10f, 12f, 14f, 16f, 18f }) &&
                      spikes.MaxLevel == 6 && spikes.CostAt(6) < 0, "spikes +8 ... +18 over 6 levels, then the top");
                int total = catalog.Upgrades.Sum(u => Enumerable.Range(0, u.RegularLevels).Sum(u.CostAt));
                Check(total >= 14000, $"the whole workshop costs {total} scrap: deep enough for the whole run");
                Check(armor.DescriptionAt(0).Contains("4%") && armor.DescriptionAt(1).Contains("4.5%"), $"the tile shows the next gain: '{armor.DescriptionAt(1)}'");
                for (int i = 0; i < armor.RegularLevels; i++)
                    workshop.Buy(0);
                Check(s_Run.LevelOf(armor) == 8 && Mathf.Abs(s_Car.Armor - 0.46f) < 0.001f, $"armour maxed (4 + 4.5 + ... + 7.5%): {s_Car.Armor:P1}");
                money = s_Run.Money;
                Check(!workshop.Buy(0) && s_Run.Money == money && s_Run.LevelOf(armor) == 8 && armor.CostAt(8) < 0,
                    "no level past the last: no master level, the scrap stays");
                var row = workshop.GetComponentsInChildren<WorkshopRowView>(true).First(v => v.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == "БРОНЯ"));
                var texts = row.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();
                Check(texts.Contains("MAX") && !texts.Any(t => t.Contains("МАСТЕР")), $"the maxed row says MAX: [{string.Join(" | ", texts)}]");
            });
            At(1.9f, () => Shot("14_Workshop_Max"));
            At(2.1f, () =>
            {
                CarUpgrade medkit = catalog.Supplies[0], repair = catalog.Supplies[1], mines = catalog.Supplies[2];
                Check(catalog.Supplies.Length == 3 && medkit.ForAd && repair.ForAd && mines.ForAd &&
                      catalog.Supplies.All(u => u.Effect != StatEffect.RefillAmmo),
                    "kit, repair and mines, all for an ad (no ammo box); the mines last");
                // First-aid kits (for an ad, the stand-in rewards at once): into the inventory, up to 5, whatever the health.
                money = s_Run.Money;
                for (int i = 0; i < RunState.MaxMedkits; i++)
                    workshop.BuySupply(0);
                Check(s_Run.Medkits == RunState.MaxMedkits && s_Run.Money == money, $"5 first-aid kits in the inventory, no scrap spent ({s_Run.Medkits})");
                Check(!workshop.BuySupply(0), "a 6th kit: НЕ НУЖНО (the inventory is full)");
                Check(mines.AdUsesPerRun == 4 && workshop.BuySupply(2) && s_Run.ArmedMines == 8 && s_Run.Money == money,
                    $"mines for an ad (4 a run): all 8 round the car at once ({s_Run.ArmedMines}), no scrap");
                Check(!workshop.BuySupply(2) && s_Run.LevelOf(mines) == 1, "more mines with all 8 armed: НЕ НУЖНО");
                Check(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                        .Any(tr => tr.name == "Light" && tr.parent != null && tr.parent.name.StartsWith("Mine")),
                    "the mines are plain models: no light on them");
                CloseShot("14_Mines", s_Car.transform.position, 11f);

                // The repair: only for an ad, back to full, 3 a run.
                Check(repair.ForAd && repair.AdUsesPerRun == 3, "the repair is for an ad, 3 times a run");
                Check(!workshop.BuySupply(1), "repair of an intact car: НЕ НУЖНО");
                money = s_Run.Money;
                for (int i = 0; i < 3; i++)
                {
                    s_Car.TakeDamage(300, Vector3.right);
                    int before = s_Car.Health;
                    Check(workshop.BuySupply(1) && s_Car.Health == s_Car.MaxHealth, $"ad repair {i + 1}: {before} → {s_Car.Health} (full)");
                }
                Check(s_Run.Money == money, "the repairs cost no scrap");
                s_Car.TakeDamage(300, Vector3.right);
                Check(!workshop.BuySupply(1) && s_Run.LevelOf(repair) == 3, "a 4th repair: ИСПОЛЬЗОВАНО");
            });
            At(2.5f, () => Shot("14_Workshop_Supplies"));
            At(2.7f, () =>
            {
                workshop.Close();
                Vector3 car = s_Car.transform.position;
                s_Spawner.SpawnAt(Zombie("Walker"), car + new Vector3(2.55f, 0f, 2.55f));
            });
            At(2.9f, () =>
            {
                // The kits are used in the game (never behind a pause): 3 / the HUD slot.
                s_Run.RequestMedkit();
                Check(s_Run.Medkits == RunState.MaxMedkits, "a kit at full health is not used");
                s_PlayerHealth.TakeDamage(40, Vector3.right);
                Object.FindAnyObjectByType<MedkitSlotView>().Use();
                Check(s_Run.Medkits == RunState.MaxMedkits - 1 && s_Stats.Health == s_Stats.MaxHealth, "the HUD kit (3) heals to full: 4 left");
            });
            At(6.5f, () =>
            {
                Check(s_Run.ArmedMines == 7, $"a zombie stepped on a mine: {s_Run.ArmedMines} left");
                // The next ad fills the free place only (no mine on top of another): 8 again, 2 of 4 used.
                s_Run.SetWorkshopOpen(true);
                Check(workshop.BuySupply(2) && s_Run.ArmedMines == 8 && s_Run.LevelOf(catalog.Supplies[2]) == 2,
                    $"the second ad refills the empty place: {s_Run.ArmedMines} mines");
                var spots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(tr => tr.name.StartsWith("Mine") && tr.gameObject.activeInHierarchy && tr.parent == null)
                    .Select(tr => new Vector2(Mathf.Round(tr.position.x * 10f), Mathf.Round(tr.position.z * 10f))).ToArray();
                Check(spots.Length == 8 && spots.Distinct().Count() == 8, $"every mine on its own place ({spots.Distinct().Count()} / {spots.Length})");
                s_Run.SetWorkshopOpen(false);
                s_Progression.AddXp(35);
            });
            At(7.3f, () =>
            {
                Check(levelUp.IsOpen, "level-up cards for the reroll");
                money = s_Run.Money;
                int rerolls = s_Run.Rerolls;
                // For a rewarded ad (the stand-in rewards at once): a new hand, no parts spent.
                Check(levelUp.Reroll() && s_Run.Rerolls == rerolls + 1 && s_Run.Money == money, "ПОМЕНЯТЬ for an ad: a new hand, parts kept");
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
                Check(screen.IsShown && screen.ScrapCoins == 40, $"the end exchanges the parts: 2000 → +{screen.ScrapCoins}");
                var line = screen.transform.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.name == "Scrap");
                Check(line != null && line.text.Contains("2000 → +40"), $"the defeat screen says so: '{line?.text}'");
                Check(screen.CoinsEarned >= 40, $"the parts are in the coins earned ({screen.CoinsEarned})");
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
