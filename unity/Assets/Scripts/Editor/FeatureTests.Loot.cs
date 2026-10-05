using System.Linq;
using UnityEngine;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.Props;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 18. loot on the ground
        /// <summary>
        /// A boss fight sheds ammo boxes (one per 10% of the boss's health lost), taken by walking over them (the
        /// reserve goes up); a boss that dies before the last wave scatters its scrap in pieces, picked up by walking
        /// over them: each piece flies into the player and its icon on to the HUD counter, which counts it when it
        /// lands; the pieces left fly to the player by themselves when the next wave starts — nothing is lost.
        /// </summary>
        private static void Loot()
        {
            LootDropper loot = null;
            LootFlyView fly = null;
            ZombieController boss = null;
            int reserveBefore = 0, scrapTaken = 0, piecesTaken = 0;
            System.Action<LootKind, int, Vector3> onLoot = (kind, amount, _) =>
            {
                if (kind != LootKind.Scrap)
                    return;
                scrapTaken += amount;
                piecesTaken++;
            };
            for (float t = 1.5f; t < 16f; t += 0.1f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                });

            At(1.0f, () =>
            {
                Find<LevelUpScreen>().enabled = false;
                loot = Find<LootDropper>();
                fly = Find<LootFlyView>();
                Check(loot != null && fly != null, "the map drops loot, the HUD flies it to the counters");
                s_Run.LootCollected += onLoot;
                s_Director.SkipTo(10, 0.3f);
            });
            At(2.6f, () =>
            {
                boss = s_Director.Boss;
                Check(boss != null, "wave 10: the boss is out");
                Teleport(boss.transform.position + new Vector3(-6f, 0f, -4f));
                boss.TakeDamage(Mathf.CeilToInt(boss.MaxHealth * 0.25f), Vector3.right);
            });
            At(2.8f, () => Check(loot.CountOf(LootKind.Ammo) == 2, $"a quarter of its health lost: 2 ammo boxes ({loot.CountOf(LootKind.Ammo)})"));
            At(3.6f, () =>
            {
                Shot("18_Loot_AmmoBoxes");
                SetAmmo(5, 0);
                reserveBefore = s_Weapon.Reserve;
                Teleport(loot.PositionsOf(LootKind.Ammo).First());
            });
            At(4.4f, () =>
            {
                Check(s_Weapon.Reserve > reserveBefore && loot.CountOf(LootKind.Ammo) < 2,
                    $"walking over a box: reserve {reserveBefore} -> {s_Weapon.Reserve}, {loot.CountOf(LootKind.Ammo)} left");
                boss.TakeDamage(100000, Vector3.right);
            });
            At(5.6f, () =>
            {
                // The player stands close to the boss: a piece landing by them is taken at once.
                Check(loot.CountOf(LootKind.Scrap) + piecesTaken == 12,
                    $"the boss's scrap lies round it in 12 pieces ({loot.CountOf(LootKind.Scrap)} lying, {piecesTaken} taken)");
                Shot("18_Loot_BossScrap");
                Teleport(loot.PositionsOf(LootKind.Scrap).First());
            });
            At(5.95f, () =>
            {
                Check(scrapTaken > 0 && fly.Flying > 0 && s_Run.MoneyShown < s_Run.Money,
                    $"a piece taken: its icon flies to the counter ({fly.Flying} flying, shown {s_Run.MoneyShown} of {s_Run.Money})");
                Shot("18_Loot_Flying");
            });
            At(7.0f, () =>
            {
                Check(fly.Flying == 0 && s_Run.MoneyShown == s_Run.Money, "landed: the counter shows it");
                KillAll();
            });
            At(7.6f, KillAll);
            At(8.2f, () =>
            {
                Check(loot.CountOf(LootKind.Scrap) > 0, $"pieces still lie there in the break ({loot.CountOf(LootKind.Scrap)})");
                s_Director.SkipTo(11, 0.3f);
            });
            At(10.0f, () =>
            {
                Check(loot.ActiveCount == 0, "the next wave: what was left flew to the player by itself");
                Check(scrapTaken == 150, $"all of the boss's 150 scrap was taken ({scrapTaken})");
                s_Run.LootCollected -= onLoot;
            });
            At(10.3f, Finish);
        }
    }
}
