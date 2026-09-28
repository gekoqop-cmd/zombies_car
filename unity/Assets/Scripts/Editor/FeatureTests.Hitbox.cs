using UnityEditor;
using UnityEngine;
using ZombiCar.Characters;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 9. larger zombie hit box for shots
        /// <summary>
        /// Zombies carry a larger trigger that only bullets use: a shot passing 0.6 m from a walker's centre (outside
        /// its body) now hits, one passing 0.9 m away still misses; the body collider, the agent radius and the attack
        /// range are unchanged; the hit box is off while the zombie rises; the axe (area damage) ignores it.
        /// </summary>
        private static void Hitbox()
        {
            ZombieController zombie = null;
            CapsuleCollider body = null, hitbox = null;
            int before = 0, shots = 0;
            bool frozen = false;
            Vector3 start = Vector3.zero;

            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;
                start = s_Car.transform.position + new Vector3(0f, 0f, -6f);
                Teleport(start);
                // Fire is held until the gun actually fires once (steps can share one slow batch frame).
                s_Weapon.Fired += () =>
                {
                    shots++;
                    s_Bot.FireHeld = false;
                };
                zombie = s_Spawner.SpawnAt(Zombie("Walker"), start + new Vector3(6f, 0f, 0.6f));
                body = zombie.GetComponent<CapsuleCollider>();
                hitbox = zombie.transform.Find("ShotHitbox").GetComponent<CapsuleCollider>();
                ZombieConfig walker = Zombie("Walker");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EditorPaths.c_PrefabsEnemies}/Zombie_Walker.prefab");
                Check(hitbox.isTrigger && hitbox.radius > body.radius + 0.2f,
                    $"shot hit box: trigger, radius {hitbox.radius:F2} m around a body of {body.radius:F2} m");
                Check(Mathf.Approximately(body.radius, 0.4f) && Mathf.Approximately(zombie.Agent.radius, 0.45f) &&
                      Mathf.Approximately(walker.AttackRange, 0.6f) && prefab.GetComponent<CapsuleCollider>().radius == body.radius,
                    "body collider, agent radius and attack range unchanged (0.4 / 0.45 / 0.6 m)");
                Check(!hitbox.enabled, "no hit box while the zombie is still in the ground");
            });
            // Hold it in place once it is up (an ability pose), so the shot geometry is exact.
            for (float t = 1.1f; t < 5f; t += 0.05f)
                At(t, () =>
                {
                    if (!frozen && zombie.CanAct)
                        frozen = zombie.BeginAction(CharacterAnim.Idle);
                });
            At(5.0f, () =>
            {
                Check(frozen && hitbox.enabled && body.enabled, "risen: body and hit box on");
                AimPast(zombie, 0.6f);
                before = zombie.Health;
                shots = 0;
                s_Bot.FireHeld = true;
            });
            At(6.0f, () =>
            {
                Check(shots == 1 && zombie.Health < before,
                    $"a shot 0.6 m from the centre (outside the body) hits: {shots} shot, {before} -> {zombie.Health}");
                s_Weapon.Refill();
                AimPast(zombie, 0.9f);
                before = zombie.Health;
                shots = 0;
                s_Bot.FireHeld = true;
            });
            At(7.0f, () =>
            {
                Check(shots == 1 && zombie.Health == before, $"a shot 0.9 m from the centre still misses ({shots} shot)");
                // The axe: the body 0.12 m out of reach, the hit box within it.
                var melee = s_Player.GetComponent<PlayerMelee>();
                s_Player.GetComponent<WeaponSwitcher>().Switch(WeaponSlot.Axe);
                Vector3 to = zombie.transform.position - s_Player.position;
                to.y = 0f;
                Teleport(zombie.transform.position - to.normalized * (melee.Range + body.radius + 0.12f));
                s_Bot.AimDirection = to.normalized;
            });
            At(7.3f, () =>
            {
                before = zombie.Health;
                s_Player.GetComponent<PlayerMelee>().Swing();
            });
            At(8.0f, () =>
            {
                Check(zombie.Health == before, "the axe reaches the body only, not the shot hit box");
                zombie.EndAction();
                zombie.TakeDamage(100000, Vector3.right);
            });
            At(8.3f, () => Check(!hitbox.enabled && !body.enabled, "dead: body and hit box off"));
            At(8.5f, Finish);
        }

        // Aims so the shot line from the muzzle passes <paramref name="miss"/> metres beside the zombie's centre.
        private static void AimPast(ZombieController zombie, float miss)
        {
            Vector3 to = zombie.transform.position - s_Weapon.Muzzle.position;
            to.y = 0f;
            float angle = Mathf.Asin(Mathf.Clamp(miss / to.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
            s_Bot.AimActive = true;
            s_Bot.AimDirection = Quaternion.Euler(0f, angle, 0f) * to.normalized;
        }
    }
}
