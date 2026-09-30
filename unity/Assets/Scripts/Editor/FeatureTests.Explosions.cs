using System.Linq;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Props;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 5. comic explosions
        /// <summary>
        /// Shooting a red barrel blows it up with the comic effect (burst, lettering, stars, smoke, ring, scorch),
        /// kills the zombies around it, shakes the screen and booms; a blast sets off a barrel next to it; exploded
        /// barrels are back after the wave.
        /// </summary>
        private static void Explosions()
        {
            ExplosiveProp barrel = null, second = null;
            ZombieController[] victims = null;
            int exploded = 0;
            float shake = 0f;
            bool boom = false;
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                ExplosiveProp.Exploded += _ => exploded++;
                // Every barrel that looks red explodes (the workshop set's red barrels too).
                var redBarrels = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name.StartsWith("Prop_Barrel") || t.name.StartsWith("Barrel_RedWhite")).ToArray();
                Check(redBarrels.Length > 0 && redBarrels.All(t => t.GetComponentInParent<ExplosiveProp>() != null),
                    $"all {redBarrels.Length} red barrels on the map explode; not: " +
                    string.Join(", ", redBarrels.Where(t => t.GetComponentInParent<ExplosiveProp>() == null).Select(t => $"{t.name} under {(t.parent ? t.parent.name : "-")}").Distinct()));
                var shaker = Object.FindAnyObjectByType<CameraShake>();
                Check(CameraShake.IsReady, $"screen shake on the game camera (component {(shaker != null ? "found" : "missing")}, " +
                    $"follow {(shaker != null && shaker.ComponentOwner != null && shaker.ComponentOwner.Follow != null)})");
                Check(ExplosiveProp.All.Count >= 5, $"{ExplosiveProp.All.Count} explosive barrels / canisters on the map");
                barrel = ExplosiveProp.All.OrderBy(b => Vector3.Distance(b.transform.position, new Vector3(9.5f, 0f, -7.5f))).First();
                Vector3 p = barrel.transform.position;
                foreach (Vector3 offset in new[] { new Vector3(1.8f, 0f, 1.2f), new Vector3(2.2f, 0f, -1.4f), new Vector3(3.2f, 0f, 0.2f), new Vector3(1.2f, 0f, 2.6f) })
                    s_Spawner.SpawnAt(Zombie("Walker"), p + offset);
                Teleport(p + new Vector3(-7f, 0f, 0f));
            });
            At(2.4f, () =>
            {
                victims = LiveZombies().Where(z => Vector3.Distance(z.transform.position, barrel.transform.position) < 4f).ToArray();
                Vector3 aim = barrel.transform.position - s_Player.position;
                aim.y = 0f;
                s_Bot.AimActive = true;
                s_Bot.AimDirection = aim.normalized;
                s_Bot.FireHeld = true;
            });
            for (float t = 2.45f; t < 5f; t += 0.02f)
                At(t, () =>
                {
                    if (!barrel.IsAlive && s_Bot.FireHeld)
                    {
                        s_Bot.FireHeld = false;
                        shake = CameraShake.LastKick;
                        boom = Playing("Explosion1") || Playing("Explosion2");
                        Time.timeScale = 0.35f;                              // slow motion: catch the comic effect
                        EditorApplicationDelay(() => Shot("5_Explosion"), 0.3f);
                        EditorApplicationDelay(() => Shot("5_Explosion_Late"), 1.3f);
                        EditorApplicationDelay(() => Time.timeScale = 1f, 1.4f);
                    }
                });
            At(6.2f, () =>
            {
                Check(!barrel.IsAlive && exploded >= 1, "shooting the barrel blew it up");
                Check(victims.Length >= 3 && victims.All(z => !z.IsAlive), $"the blast killed the {victims.Length} zombies around it");
                Check(Object.FindObjectsByType<ComicExplosion>(FindObjectsInactive.Exclude).Any(e => e.IsPlaying), "comic explosion on screen");
                Check(boom, "boom sound");
                // Chain: move another barrel next to a third one and blow the third up.
                ExplosiveProp[] rest = ExplosiveProp.All.Where(b => b.IsAlive).ToArray();
                ExplosiveProp first = rest[0];
                second = rest[1];
                second.transform.position = first.transform.position + new Vector3(2f, 0f, 0.5f);
                Physics.SyncTransforms();
                exploded = 0;
                first.Explode();
                Check(shake > 0.1f, $"a blast 7 m away shakes the screen (trauma {shake:F2})");
            });
            At(7.0f, () =>
            {
                Check(!second.IsAlive && exploded == 2, "a blast sets off the barrel next to it (chain)");
                s_Waves.EndWave(1, false);
            });
            At(7.5f, () =>
            {
                Check(barrel.IsAlive && second.IsAlive && ExplosiveProp.All.All(b => b.IsAlive), "exploded barrels are back after the wave");
            });
            At(7.7f, Finish);
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/> of real time (screenshots in slow motion).</summary>
        private static void EditorApplicationDelay(System.Action action, float seconds)
        {
            float due = Time.realtimeSinceStartup + seconds;
            void Tick()
            {
                if (Time.realtimeSinceStartup < due)
                    return;
                UnityEditor.EditorApplication.update -= Tick;
                action();
            }
            UnityEditor.EditorApplication.update += Tick;
        }
    }
}
