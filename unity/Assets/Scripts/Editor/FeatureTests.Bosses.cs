using System.Linq;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.UI;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 3. bosses
        /// <summary>
        /// Waves 10 / 20 / 30 bring their bosses: announced (banner, boss music, HUD timer), with a health plate; the
        /// brood mother summons minions, the juggernaut spits acid and charges, the reaper enrages at half health and
        /// slams; a boss wave ends only when the boss is dead, and the kill pays its reward.
        /// </summary>
        private static void Bosses()
        {
            ZombieController boss = null;
            BossBrain brain = null;
            int moneyBefore = 0;
            // The player and the car are kept alive: the scenario looks at the bosses.
            for (float t = 1.5f; t < 60f; t += 0.1f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                    PickCards();
                });

            At(1.0f, () =>
            {
                // Level-up cards (checked by the progression scenario) would pause the game after every boss kill.
                Find<LevelUpScreen>().enabled = false;
                s_Director.SkipTo(10, 0.3f);
            });
            At(2.6f, () =>
            {
                boss = s_Director.Boss;
                brain = boss != null ? boss.GetComponent<BossBrain>() : null;
                Check(boss != null && boss.Config.IsBoss && boss.Config.DisplayName == "МАТКА", "wave 10: the brood mother comes out");
                Check(s_Waves.BossName == "МАТКА" && Find<WaveBanner>().LastTitle == "БОСС!", "boss banner and HUD name");
                Check(AudioManager.Instance.CurrentMusic != null && AudioManager.Instance.CurrentMusic.name == "Music_Boss", "boss music");
                Check(boss != null && boss.GetComponentInChildren<BossHealthPlate>() != null, "health plate over the boss");
                Teleport(boss.transform.position + new Vector3(-5f, 0f, -3f));
            });
            At(3.4f, () => Shot("3_Boss1_Arrives"));
            At(6.4f, () => Shot("3_Boss1_Summon"));
            At(8.0f, () =>
            {
                Check(brain.MinionsAlive > 0, $"the brood mother summoned minions ({brain.MinionsAlive} alive)");
                moneyBefore = s_Run.Money;
                boss.TakeDamage(100000, Vector3.right);
            });
            At(9.0f, () =>
            {
                Check(!boss.IsAlive && s_Director.Boss == null && string.IsNullOrEmpty(s_Waves.BossName), "the boss is dead");
                Check(s_Run.Money >= moneyBefore + 150 && s_Run.BossKills == 1, $"boss reward: {moneyBefore} -> {s_Run.Money}");
                Check(s_Director.IsWaveRunning, "the boss wave goes on while zombies are left");
                KillAll();
            });
            At(10.0f, KillAll);
            At(10.5f, () =>
            {
                Check(AudioManager.Instance.CurrentMusic.name == "Music_Day", "the map's music is back");
                Check(!s_Director.IsWaveRunning, $"wave 10 is over once every zombie is dead (alive {s_Spawner.Alive})");
                s_Director.SkipTo(20, 0.3f);
            });

            // Juggernaut: acid volleys and charges.
            int hurt = 0;
            At(12.0f, () =>
            {
                boss = s_Director.Boss;
                brain = boss != null ? boss.GetComponent<BossBrain>() : null;
                Check(boss != null && boss.Config.DisplayName == "ТАРАН", "wave 20: the juggernaut comes out");
                Teleport(boss.transform.position + new Vector3(-8f, 0f, 0f));
                s_PlayerHealth.Damaged += _ => hurt++;
            });
            bool sawCharge = false, sawGlob = false;
            for (float t = 12.2f; t < 26f; t += 0.1f)
                At(t, () =>
                {
                    if (brain == null)
                        return;
                    sawCharge |= brain.CurrentAbility == "Charge" || brain.CurrentAbility == "Dash";
                    sawGlob |= Object.FindObjectsByType<AcidGlob>(FindObjectsInactive.Exclude).Length > 0;
                    // Stay in charge range, away from the boss's melee.
                    Vector3 away = s_Player.position - boss.transform.position;
                    away.y = 0f;
                    if (away.magnitude < 5f)
                        Teleport(boss.transform.position + away.normalized * 8f);
                });
            At(15.3f, () => Shot("3_Boss2_Telegraph"));
            At(26.0f, () =>
            {
                Check(sawGlob, "the juggernaut spat acid globs");
                Check(sawCharge, "the juggernaut charged");
                Check(hurt > 0, $"its attacks hurt the player ({hurt} hits)");
                boss.TakeDamage(100000, Vector3.right);
                KillAll();
            });
            At(27.0f, KillAll);
            At(27.5f, () => s_Director.SkipTo(30, 0.3f));

            // Reaper: everything, then the second phase with the slam.
            At(29.3f, () =>
            {
                boss = s_Director.Boss;
                brain = boss != null ? boss.GetComponent<BossBrain>() : null;
                Check(boss != null && boss.Config.DisplayName == "ЖНЕЦ" && s_Waves.WaveCount == 30, "wave 30: the reaper comes out (last wave)");
                Check(brain.Config.Summon && brain.Config.Spit && brain.Config.Charge && brain.Config.Enrage && brain.Config.Slam,
                    "the reaper has every ability plus the second phase");
                Teleport(boss.transform.position + new Vector3(-3.5f, 0f, -2f));
                boss.TakeDamage(Mathf.CeilToInt(boss.MaxHealth * 0.55f), Vector3.right);
            });
            At(29.9f, () =>
            {
                Check(brain.IsEnraged && boss.Animator.ScaleMultiplier > 1f && boss.SpeedScale > 1f,
                    $"below half health it enrages (speed x{boss.SpeedScale:F2})");
                Shot("3_Boss3_Enraged");
            });
            bool sawSlam = false;
            for (float t = 30.0f; t < 45f; t += 0.1f)
                At(t, () =>
                {
                    sawSlam |= brain.CurrentAbility == "Slam" || brain.CurrentAbility == "Shockwave";
                    Vector3 d = s_Player.position - boss.transform.position;
                    d.y = 0f;
                    if (d.magnitude > 4f || d.magnitude < 1.5f)
                        Teleport(boss.transform.position + new Vector3(-3f, 0f, -1.5f));
                });
            At(33.5f, () => Shot("3_Boss3_Fight"));
            At(45.0f, () =>
            {
                Check(sawSlam, "the enraged reaper slams the ground");
                boss.TakeDamage(100000, Vector3.right);
                KillAll();
            });
            At(45.9f, KillAll);
            At(46.5f, () =>
            {
                Check(s_Run.BossKills == 3, $"three bosses killed ({s_Run.BossKills})");
                Check(s_Director.IsFinished && s_Waves.Phase == WavePhase.Finished, "wave 30 cleared: all waves done");
            });
            At(47.0f, Finish);
        }

        /// <summary>Takes the first card of any level-up hand on screen (boss kills give several levels).</summary>
        private static void PickCards()
        {
            LevelUpScreen screen = Find<LevelUpScreen>();
            if (screen != null && screen.IsOpen)
                screen.Pick(0);
        }

        private static void KillAll()
        {
            foreach (ZombieController z in LiveZombies().ToArray())
                z.TakeDamage(100000, Vector3.right);
            s_Director.GetType().GetField("m_ToSpawn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(s_Director, 0);
        }
    }
}
