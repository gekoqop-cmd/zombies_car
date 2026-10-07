using System.Linq;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 21. how the fight feels
        /// <summary>
        /// A blow to the player shakes the camera, stops the game for a moment when it is big and buzzes a phone; the
        /// settings' "ТРЯСКА И ВИБРАЦИЯ" turns all of it off. Kills in a row make a streak ("СЕРИЯ" on the HUD, ×1.25
        /// scrap from 10) that a blow breaks. The music grows tenser late in the run and most with an enraged boss,
        /// whose wind-ups throb red. On a phone the scenery that is never seen is not drawn.
        /// </summary>
        private static void Feel()
        {
            bool shakeWas = SettingsService.ShakeEnabled;
            BossBrain brain = null;
            bool sawWindup = false;
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Find<LevelUpScreen>().enabled = false;
                SettingsService.SetShakeEnabled(true);
                s_PlayerHealth.TakeDamage(20, Vector3.right);
                Check(CameraShake.LastKick > 0.1f && GameFeel.LastStop > 0f, $"a big blow: shake {CameraShake.LastKick:F2}, hit stop {GameFeel.LastStop:F2} s");
                if (DeviceInfo.IsMobile)
                    Check(GameFeel.LastVibration > 0, $"and a vibration on a phone ({GameFeel.LastVibration} ms)");
            });
            At(1.4f, () =>
            {
                Check(Mathf.Approximately(GamePause.SlowMotion, 1f), "the hit stop is over at once");
                var settings = Find<SettingsPanel>();
                settings.Show();
            });
            At(1.7f, () =>
            {
                Shot("21_Settings_Shake");
                var settings = Find<SettingsPanel>();
                bool shown = settings.GetComponentsInChildren<Transform>(true)
                    .Any(t => t.name == "ShakeButton" && t.gameObject.activeInHierarchy);
                settings.ToggleShake();
                Check(shown && !SettingsService.ShakeEnabled, "ТРЯСКА И ВИБРАЦИЯ: the switch is there (every device), ВЫКЛ");
                settings.Hide();
            });
            At(2.0f, () =>
            {
                float kick = CameraShake.LastKick, stop = GameFeel.LastStop;
                GameFeel.Hurt(40);
                CameraShake.Add(0.9f);
                Check(Mathf.Approximately(CameraShake.LastKick, kick) && Mathf.Approximately(GameFeel.LastStop, stop) &&
                      Mathf.Approximately(GamePause.SlowMotion, 1f), "with it off: no shake, no hit stop");
                SettingsService.SetShakeEnabled(true);
                s_PlayerHealth.Heal(1000);
            });

            // A kill streak.
            At(2.3f, () =>
            {
                Vector3 p = s_Player.position;
                for (int i = 0; i < 12; i++)
                    s_Spawner.SpawnAt(Zombie("Walker"), p + new Vector3(6f + i * 0.3f, 0f, 3f));
            });
            At(4.0f, () =>
            {
                foreach (ZombieController z in LiveZombies().ToArray())
                    z.TakeDamage(100000, Vector3.right);
                Check(s_Run.Streak >= 10 && Mathf.Approximately(s_Run.StreakMultiplier, 1.25f), $"12 kills in a row: streak {s_Run.Streak} (×{s_Run.StreakMultiplier})");
            });
            At(4.4f, () =>
            {
                Check(Find<StreakView>().GetComponent<CanvasGroup>().alpha > 0.5f, "\"СЕРИЯ\" on the HUD");
                Shot("21_Streak");
                s_PlayerHealth.TakeDamage(5, Vector3.right);
                Check(s_Run.Streak == 0, "a blow to the player breaks the streak");
                s_PlayerHealth.Heal(1000);
            });

            // The music and the boss.
            At(4.8f, () =>
            {
                s_Director.enabled = true;                                    // the waves run again for the boss
                s_Director.SkipTo(30, 0.3f);
            });
            for (float t = 5f; t < 12f; t += 0.2f)
                At(t, () =>
                {
                    s_PlayerHealth.Heal(1000);
                    s_Car.Repair(1000);
                    if (brain != null)
                        sawWindup |= brain.IsWindingUp();
                });
            At(6.2f, () =>
            {
                ZombieController boss = s_Director.Boss;
                brain = boss != null ? boss.GetComponent<BossBrain>() : null;
                Check(boss != null, "the last boss is out");
                Teleport(boss.transform.position + new Vector3(-3.5f, 0f, -2f));
                boss.TakeDamage(Mathf.CeilToInt(boss.MaxHealth * 0.55f), Vector3.right);
            });
            At(11.0f, () =>
            {
                float intensity = AudioManager.Instance != null ? AudioManager.Instance.MusicIntensity : 0f;
                Check(intensity > 0.8f, $"an enraged last boss: the music at its tensest ({intensity:F2})");
                Check(sawWindup, "its blows wind up first (the boss throbs red)");
                Shot("21_Boss_Windup");
                if (DeviceInfo.IsMobile)
                {
                    var trim = Find<MobileTrim>();
                    Check(trim.Trimmed > 0, $"a phone does not draw what it never shows ({trim.Trimmed} renderers off)");
                }
                SettingsService.SetShakeEnabled(shakeWas);
            });
            At(11.3f, Finish);
        }
    }
}
