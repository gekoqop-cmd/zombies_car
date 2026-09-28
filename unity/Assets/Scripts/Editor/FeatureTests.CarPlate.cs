using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 8. the car plate, hit alerts and sounds
        /// <summary>
        /// The car's durability plate is one compact row lying over the car (not over the lot behind it) and clears out
        /// of the way while zombies attacking from behind or the side are under it; a hit on the car brings up the
        /// fading "МАШИНУ АТАКУЮТ!" warning (not again right away) and a metal bang; the player taking damage makes a
        /// hit sound; the bar colour follows the car's damage stage also after the hull upgrade raises the maximum.
        /// </summary>
        private static void CarPlate()
        {
            CarHealthBar plate = null;
            CarAttackAlert alert = null;
            bool covered = false, fadedWhenCovered = true, carHit = false, alertSeen = false;
            float minAlpha = 1f;

            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;
                plate = Find<CarHealthBar>();
                alert = Find<CarAttackAlert>();
                Teleport(s_Car.transform.position + new Vector3(0f, 0f, -6f));   // in front of the car, off the plate
            });
            At(1.8f, () =>
            {
                Camera cam = Camera.main;
                Rect plateRect = ScreenRect(cam, plate.GetComponentInChildren<Canvas>().GetComponent<RectTransform>());
                Bounds car = s_Car.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer))
                    .Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Rect carRect = ScreenRect(cam, car);
                Check(plateRect.height < cam.pixelHeight * 0.055f && plateRect.width < cam.pixelHeight * 0.3f,
                    $"compact plate: {plateRect.width / cam.pixelHeight:P0} x {plateRect.height / cam.pixelHeight:P0} of the screen height");
                Check(carRect.Contains(plateRect.center), "the plate lies over the car itself");
                Check(!plate.IsCovering && Mathf.Abs(plate.Alpha - 0.8f) < 0.05f, $"idle: translucent ({plate.Alpha:F2})");
                Shot("8_Plate_Idle");
                // Zombies come at the car from behind and from the side.
                Vector3 c = s_Car.transform.position;
                foreach (Vector3 offset in new[] { new Vector3(-1.2f, 0f, 3.4f), new Vector3(0.6f, 0f, 3.6f), new Vector3(2.4f, 0f, 2.6f), new Vector3(-3f, 0f, 1.2f) })
                    s_Spawner.SpawnAt(Zombie("Worker"), c + offset);
            });
            for (float t = 1.9f; t < 7.5f; t += 0.05f)
                At(t, () =>
                {
                    if (plate.IsCovering)
                    {
                        covered = true;
                        minAlpha = Mathf.Min(minAlpha, plate.Alpha);
                    }
                    carHit |= Playing("Car_Hit1") || Playing("Car_Hit2") || Playing("Car_Hit3");
                    alertSeen |= alert.Alpha > 0.9f;
                    s_Car.Repair(1000);
                });
            At(5.0f, () => Shot("8_Plate_Uncovered"));
            At(7.5f, () =>
            {
                Check(covered, "zombies at the back of the car are under the plate on screen");
                Check(minAlpha < 0.3f, $"the plate clears out of their way (alpha {minAlpha:F2})");
                Check(carHit, "zombie hits on the car: metal bang");
                Check(alertSeen && alert.Shows == 1, $"\"МАШИНУ АТАКУЮТ!\" came up once during the attack ({alert.Shows})");
                foreach (ZombieController z in LiveZombies().ToArray())
                    z.TakeDamage(100000, Vector3.right);
            });
            At(9.0f, () =>
            {
                Check(!plate.IsCovering && plate.Alpha > 0.75f, $"zombies gone: the plate is back ({plate.Alpha:F2})");
                Check(alert.Alpha < 0.01f, "the warning faded out");
                s_PlayerHealth.TakeDamage(5, Vector3.right);
                Check(Playing("Player_Hurt1") || Playing("Player_Hurt2"), "the player takes a hit: hit sound");
                s_PlayerHealth.Heal(5);
                // Hull upgrade: the maximum grows, the stage thresholds (absolute HP) stay.
                var pool = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool);
                s_Run.TakePerk(pool.Perks.First(p => p.Id == "carplate"));
                s_Car.TakeDamage(s_Car.Health - (s_Car.Config.DamagedAtOrBelow + 10), Vector3.right);
            });
            At(10.0f, () =>
            {
                Color c = plate.FillColor;
                Check(s_Car.MaxHealth > s_Car.Config.MaxHealth && s_Car.Stage == CarDamageStage.Intact && c.g > c.r,
                    $"{s_Car.Health} / {s_Car.MaxHealth}: intact car, green bar ({c})");
                s_Car.TakeDamage(20, Vector3.right);
            });
            At(11.0f, () =>
            {
                Color c = plate.FillColor;
                Check(s_Car.Stage == CarDamageStage.Damaged && c.r > c.g, $"{s_Car.Health} / {s_Car.MaxHealth}: damaged car, yellow bar ({c})");
                Shot("8_Plate_Damaged");
            });
            At(11.2f, Finish);
        }

        private static Rect ScreenRect(Camera cam, RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return ScreenRect(cam, corners);
        }

        private static Rect ScreenRect(Camera cam, Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            return ScreenRect(cam, corners);
        }

        private static Rect ScreenRect(Camera cam, Vector3[] points)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Vector3 point in points)
            {
                Vector3 p = cam.WorldToScreenPoint(point);
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
                yMin = Mathf.Min(yMin, p.y);
                yMax = Mathf.Max(yMax, p.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
