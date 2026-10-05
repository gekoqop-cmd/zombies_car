using System.Reflection;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 20. touch aim (run with -forceMobile)
        /// <summary>
        /// The touch aim: a quick tap fires one shot — not a burst — at the nearest zombie the gun can see (not a nearer
        /// one behind the car); dragging the aim stick diagonally aims along the same diagonal on the screen (the
        /// camera's perspective, not only its yaw); on a phone the bullet leaves from the player along the aim line.
        /// </summary>
        private static void MobileAim()
        {
            MobilePlayerInput mobile = null;
            ZombieController visible = null, hidden = null;
            int shots = 0;
            System.Action onShot = () => shots++;
            Vector3 firstShotFrom = Vector3.zero;
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;
                mobile = s_Player.GetComponent<MobilePlayerInput>();
                // The touch input drives the gun again (the scenarios' bot took it over).
                FieldInfo input = typeof(PlayerWeapon).GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                input.SetValue(s_Weapon, mobile);
                // A zombie hidden behind the car, close; another one in the open, farther.
                Vector3 car = s_Car.transform.position;
                Teleport(car + new Vector3(-4.5f, 0f, 0f));
                hidden = s_Spawner.SpawnAt(Zombie("Walker"), car + new Vector3(2.6f, 0f, 0f));
                visible = s_Spawner.SpawnAt(Zombie("Walker"), car + new Vector3(-4.5f, 0f, -8.5f));
                foreach (ZombieController z in new[] { hidden, visible })
                    if (z != null)
                        z.SpeedScale = 0f;                                 // they stand still for the check
            });
            At(3.2f, () =>
            {
                s_Weapon.Fired += onShot;
                shots = 0;
                MobileControls.TapFrame = Time.frameCount;                     // a quick tap on the aim stick
            });
            At(3.25f, () =>
            {
                Check(mobile.TapTarget == visible.transform,
                    $"a tap aims at the zombie the gun can see, not the nearer one behind the car ({mobile.TapTarget?.name})");
                firstShotFrom = s_Weapon.LastShotOrigin;
            });
            At(4.0f, () =>
            {
                Check(shots == 1, $"one tap, one shot (not a burst): {shots}");
                if (DeviceInfo.IsMobile)
                {
                    Vector3 d = firstShotFrom - s_Player.position;
                    d.y = 0f;
                    Check(firstShotFrom != Vector3.zero && d.magnitude < 1.6f, $"on a phone the shot leaves from the player ({d.magnitude:F2} m)");
                }
                Shot("20_MobileAim_Tap");
                // Drag the stick up and right on the screen.
                MobileControls.Aim = new Vector2(1f, 1f).normalized;
                MobileControls.Aiming = true;
            });
            At(4.3f, () =>
            {
                Camera cam = Camera.main;
                Vector3 from = s_Player.position + Vector3.up;
                Vector2 a = cam.WorldToScreenPoint(from), b = cam.WorldToScreenPoint(from + mobile.AimDirection * 4f);
                float angle = Vector2.Angle(b - a, new Vector2(1f, 1f));
                Check(angle < 4f, $"a diagonal stick aims along the same diagonal on the screen ({angle:F1}°)");
                Shot("20_MobileAim_Diagonal");
                MobileControls.Reset();
                s_Weapon.Fired -= onShot;
            });
            At(4.6f, Finish);
        }
    }
}
