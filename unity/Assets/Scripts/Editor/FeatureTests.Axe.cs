using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.Props;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 6. the axe
        /// <summary>
        /// Q (the weapon button on touch screens) swaps the gun for the axe and back, with a sound and the HUD slots;
        /// a swing hits the zombies in the arc in front (not behind), holding attack keeps swinging while the gun
        /// stays silent; the axe breaks an explosive barrel; the axe comes out by itself when the gun is empty and the
        /// gun is back once there are rounds; the axe cards make it reach further.
        /// </summary>
        private static void Axe()
        {
            WeaponSwitcher switcher = null;
            PlayerMelee melee = null;
            WeaponState state = null;
            Renderer blade = null;
            ZombieController[] front = null;
            ZombieController behind = null;
            int[] frontBefore = null;
            int behindBefore = 0, fired = 0, landed = 0;
            bool swingSound = false, hitSound = false, switchSound = false;
            ExplosiveProp barrel = null;

            for (float t = 1.2f; t < 14f; t += 0.1f)
                At(t, () => s_PlayerHealth.Heal(1000));

            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;                       // kills level the player up
                switcher = s_Player.GetComponent<WeaponSwitcher>();
                melee = s_Player.GetComponent<PlayerMelee>();
                state = AssetDatabase.LoadAssetAtPath<WeaponState>(DataAssetsBuilder.c_WeaponState);
                blade = s_Player.GetComponentInChildren<AxeView>(true).transform.Find("Pivot/Blade").GetComponent<Renderer>();
                s_Weapon.Fired += () => fired++;
                melee.Hit += n => landed = n;
                Check(switcher.Current == WeaponSlot.Gun && !s_Weapon.Holstered && !melee.Active && !blade.enabled,
                    "the run starts with the gun in hand, the axe hidden");
                WeaponSlotsView[] shown = Object.FindObjectsByType<WeaponSlotsView>(FindObjectsInactive.Exclude);
                bool inTouch = shown.Length == 1 && shown[0].GetComponentInParent<MobileControlsLayout>(true) != null;
                Check(shown.Length == 1 && inTouch == DeviceInfo.IsMobile,
                    DeviceInfo.IsMobile ? "touch screen: the weapon column by the fire stick" : "desktop: the weapon column in the HUD");
                s_Bot.AimActive = true;
                s_Bot.AimDirection = Vector3.right;
                s_Bot.PressSwitch();
            });
            for (float t = 1.02f; t < 1.4f; t += 0.02f)
                At(t, () => switchSound |= Playing("Weapon_Switch"));
            At(1.4f, () =>
            {
                Check(switcher.Current == WeaponSlot.Axe && s_Weapon.Holstered && melee.Active && state.Current == WeaponSlot.Axe,
                    "switch: the axe in hand, the gun put away");
                Check(switchSound, "weapon switch sound");
                Check(blade.enabled, "the axe is drawn in the hand");
                Vector3 p = s_Player.position;
                front = new[]
                {
                    s_Spawner.SpawnAt(Zombie("Walker"), p + new Vector3(1.3f, 0f, 0.5f)),
                    s_Spawner.SpawnAt(Zombie("Worker"), p + new Vector3(1.4f, 0f, -0.5f)),
                    s_Spawner.SpawnAt(Zombie("Hoodie"), p + new Vector3(1.6f, 0f, 0.1f)),
                };
                behind = s_Spawner.SpawnAt(Zombie("Worker"), p + new Vector3(-1.5f, 0f, 0f));
                Shot("6_Axe_Carry");
            });
            // Swing once all of them are out of the ground (batch frames are slow: the rise takes longer in real time).
            float swungAt = -1f;
            bool swingChecked = false;
            for (float t = 2.4f; t < 7f; t += 0.05f)
                At(t, () =>
                {
                    if (swungAt < 0f && front.Append(behind).All(z => z.GetComponent<Collider>().enabled))
                    {
                        frontBefore = front.Select(z => z.Health).ToArray();
                        behindBefore = behind.Health;
                        Vector3 aim = front.Aggregate(Vector3.zero, (sum, z) => sum + z.transform.position) / front.Length - s_Player.position;
                        aim.y = 0f;
                        s_Bot.AimDirection = aim.normalized;
                        EditorApplicationDelay(() =>
                        {
                            Time.timeScale = 0.3f;                          // slow motion: catch the wind-up and the blow
                            melee.Swing();
                            swingSound = Playing("Axe_Swing1") || Playing("Axe_Swing2");
                            EditorApplicationDelay(() => Shot("6_Axe_Windup"), 0.2f);
                            EditorApplicationDelay(() => Shot("6_Axe_Strike"), 0.55f);
                            EditorApplicationDelay(() => Time.timeScale = 1f, 0.9f);
                        }, 0.1f);
                        swungAt = Time.realtimeSinceStartup;
                    }
                    if (swungAt > 0f && Time.realtimeSinceStartup < swungAt + 1.2f)
                        hitSound |= Playing("Axe_Hit1") || Playing("Axe_Hit2");
                    if (swungAt > 0f && !swingChecked && Time.realtimeSinceStartup >= swungAt + 1.3f)
                    {
                        swingChecked = true;
                        Check(swingSound, "swing sound");
                        int damage = melee.Damage;
                        bool allHit = front.Select((z, i) => !z.IsAlive || frontBefore[i] - z.Health == damage).All(b => b)
                                      && front.Any(z => !z.IsAlive);
                        Check(allHit && landed >= 3, $"one blow hit the {landed} zombies in the arc in front ({damage} damage)");
                        Check(behind.IsAlive && behind.Health == behindBefore, "the zombie behind the player was not hit");
                        Check(hitSound, "axe hit sound");
                        s_Bot.FireHeld = true;                             // held attack: swing after swing
                    }
                });
            At(8.0f, () =>
            {
                Check(front.All(z => !z.IsAlive), "holding attack keeps swinging: the zombies in front are chopped down");
                Check(fired == 0, "the gun stays silent while the axe is in hand");
                Vector3 aim = behind.transform.position - s_Player.position;
                aim.y = 0f;
                s_Bot.AimDirection = aim.normalized;
            });
            At(10.0f, () =>
            {
                Check(!behind.IsAlive, "turned around: the last zombie is chopped down too");
                s_Bot.FireHeld = false;
                barrel = ExplosiveProp.All.First(b => b.IsAlive);
                barrel.transform.position = s_Player.position + Vector3.right * 1.3f;
                Physics.SyncTransforms();
                s_Bot.AimDirection = Vector3.right;
            });
            At(10.3f, () => melee.Swing());
            At(10.9f, () =>
            {
                Check(!barrel.IsAlive, "the axe breaks an explosive barrel (it blows up)");
                state.Request(WeaponSlot.Gun);                              // a click / tap on the gun slot
            });
            At(11.2f, () =>
            {
                Check(switcher.Current == WeaponSlot.Gun && !s_Weapon.Holstered && !melee.Active, "gun slot clicked: the gun in hand");
                // Empty gun: no rounds in the magazine nor in reserve.
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(PlayerWeapon).GetField("m_InMagazine", flags).SetValue(s_Weapon, 0);
                typeof(PlayerWeapon).GetField("m_Reserve", flags).SetValue(s_Weapon, 0);
                typeof(PlayerWeapon).GetMethod("Publish", flags).Invoke(s_Weapon, null);
                s_Bot.FireHeld = true;
            });
            At(11.6f, () =>
            {
                Check(switcher.Current == WeaponSlot.Axe && state.Automatic, "out of ammo: attacking takes the axe out by itself");
                var slots = Object.FindAnyObjectByType<WeaponSlotsView>(FindObjectsInactive.Exclude);
                Transform hint = slots != null ? slots.transform.Find("EmptyHint") : null;
                if (hint != null)
                    Check(hint.gameObject.activeSelf && hint.GetComponent<TMPro.TMP_Text>().text.Contains("ПАТРОНЫ КОНЧИЛИСЬ"), "HUD says the gun is empty");
                Shot("6_Axe_Empty");
                s_Bot.FireHeld = false;
                s_Weapon.Refill();
            });
            At(12.0f, () =>
            {
                Check(switcher.Current == WeaponSlot.Gun && !s_Weapon.Holstered, "rounds again: the gun is back by itself");
                float range = melee.Range;
                int damage = melee.Damage;
                var pool = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool);
                s_Run.TakePerk(pool.Perks.First(p => p.Id == "axerange"));
                s_Run.TakePerk(pool.Perks.First(p => p.Id == "axedamage"));
                Check(melee.Range > range + 0.1f && melee.Damage > damage, $"axe cards: reach {range:F1} -> {melee.Range:F1} m, " +
                    $"damage {damage} -> {melee.Damage}");
            });
            At(12.3f, Finish);
        }
    }
}
