using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using ZombiCar.Progression;
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
        private const string c_WaveArgument = "-featureWave";

        // ================================================================== menu screenshots
        /// <summary>The main menu, its settings and the language switch (run with -featureLang en for the English menu).</summary>
        private static void MenuShot()
        {
            At(1.0f, () => SceneLoader.EnsureExists().LoadMainMenu());
            At(4.0f, () =>
            {
                Check(Object.FindAnyObjectByType<LanguageSwitch>() != null, "the main menu has the RU / EN switch");
                Shot("Menu_Main_" + Loc.Language);
                Object.FindAnyObjectByType<MainMenuController>().ShowMapSelection(true);
            });
            At(5.0f, () => Shot("Menu_Maps_" + Loc.Language));
            At(5.2f, Finish);
        }

        // ================================================================== store screenshots
        /// <summary>
        /// Screenshots for the Yandex Games page: run with -forceMobile (the touch sticks on screen), -featureMap
        /// night for the night map and -featureWave &lt;n&gt;. The run jumps to that wave, the player stands by the car and
        /// shoots into the crowd with the aim stick held. Screenshots/Feature_Store_&lt;map&gt;_Wave&lt;n&gt;_*.png at a phone's
        /// 19.5:9 and 16:9.
        /// </summary>
        private static void StoreShot()
        {
            int wave = int.TryParse(Argument(c_WaveArgument), out int w) ? w : 4;
            bool night = Argument(c_MapArgument) == "night";
            string map = night ? "Night" : "Day";
            MobilePlayerInput mobile = null;
            At(1.0f, () =>
            {
                Find<LevelUpScreen>().enabled = false;
                // The default touch layout (sticks at the screen's edges), not one dragged around by other tests.
                if (MobileControlsLayout.Instance != null)
                    MobileControlsLayout.Instance.ResetLayout();
                mobile = s_Player.GetComponent<MobilePlayerInput>();
                typeof(PlayerWeapon).GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s_Weapon, mobile);
                // The day by the car's front left, the night up behind it.
                Teleport(s_Car.transform.position + (night ? new Vector3(1.8f, 0f, 3.2f) : new Vector3(-2.6f, 0f, -2.2f)));
                s_Director.SkipTo(wave, 0.3f);
                // A run in progress: a few levels and cards, medkits in stock, scrap earned (more of it at night).
                var progression = Object.FindAnyObjectByType<PlayerProgression>();
                var pool = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool);
                string[] perks = night ? new[] { "damage", "firerate", "crit", "health", "axedamage", "speed", "pierce" }
                                       : new[] { "damage", "reload", "speed" };
                foreach (string id in perks)
                {
                    Perk perk = pool.Perks.FirstOrDefault(p => p.Id == id);
                    if (perk != null)
                        s_Run.TakePerk(perk);
                }
                if (progression != null)
                {
                    progression.enabled = true;
                    progression.AddXp(night ? 2600 : 420);
                }
                s_Run.AddMoney(night ? 1240 : 185);
                typeof(RunState).GetField("m_Medkits", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s_Run, night ? 3 : 1);
                // The camera as on a 16:9 screen (the store wants 16:9; the batch window is not one).
                foreach (CinemachineCamera vcam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude))
                {
                    if (vcam.TryGetComponent(out CameraAspectAdapter adapter))
                        adapter.enabled = false;
                    vcam.Lens.FieldOfView = CameraAspectAdapter.VerticalFov(1920f / 1080f, ViewConstants.c_CameraFov, ViewConstants.c_MaxCameraFov);
                }
            });
            for (float t = 2f; t < 14f; t += 0.25f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                    s_Weapon.Refill();
                    ZombieController target = LiveZombies().Where(z => !z.Config.IsBoss)
                        .OrderBy(z => (z.transform.position - s_Player.position).sqrMagnitude).FirstOrDefault();
                    if (target == null)
                        return;
                    Vector3 to = target.transform.position - s_Player.position;
                    to.y = 0f;
                    // The stick's direction on the screen: the camera looks along +Z, so world x/z is screen x/y.
                    MobileControls.Aim = new Vector2(to.x, to.z).normalized;
                    MobileControls.Aiming = true;
                });
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                At(8f + i * 2f, () =>
                {
                    ShotSized($"Store_{map}_Wave{wave}_{n}", 1920, 1080);
                    Debug.Log($"[FeatureTests] store shot {n}: {LiveZombies().Count()} zombies, wave {wave}");
                });
            }
            At(14.2f, () =>
            {
                MobileControls.Reset();
                Check(true, "store screenshots taken");
                Finish();
            });
        }
    }
}
