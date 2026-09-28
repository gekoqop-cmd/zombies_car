using System.Reflection;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Player;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 7. ammo refill at the car
        /// <summary>
        /// The car refills the gun only between waves, once per break: nothing before wave 1 or during a wave (the
        /// prompt says why when the player is low on ammo), "ПОПОЛНИТЬ ПАТРОНЫ" after a wave, E starts a short channel
        /// (standing still, reload animation, refill sound, progress bar) that fills the magazine and the reserve to
        /// the maximum; walking away cancels it; the supply is gone once used and when the next wave starts.
        /// </summary>
        private static void Refill()
        {
            CarResupply resupply = null;
            Animator animator = null;
            bool busy = false, sound = false, reloadAnim = false;
            float pressedAt = -1f;

            At(1.0f, () =>
            {
                s_Director.enabled = false;                                 // the waves are driven by hand
                Find<LevelUpScreen>().enabled = false;
                resupply = Find<CarResupply>();
                animator = s_Player.GetComponentInChildren<PlayerView>().GetComponentInChildren<Animator>();
                Check(resupply != null && !resupply.IsSupplied, "no refill at the car before wave 1");
                Teleport(resupply.transform.position);
            });
            At(1.3f, () =>
            {
                Check(s_Interactor.Current == null, "a full gun: no prompt at the car");
                SetAmmo(4, 10);
            });
            At(1.6f, () =>
            {
                Check(s_Interactor.Current == resupply && !s_Interaction.Available && s_Interaction.Text.Contains("ПОСЛЕ"),
                    $"low on ammo before wave 1: \"{s_Interaction.Text}\" (not available)");
                s_Waves.StartWave(1);
            });
            At(1.9f, () =>
            {
                Check(!s_Interaction.Available && s_Interaction.Text.Contains("МЕЖДУ ВОЛНАМИ"),
                    $"during a wave: \"{s_Interaction.Text}\" (not available)");
                Press();
            });
            At(2.3f, () =>
            {
                Check(!s_Interactor.IsBusy && s_Weapon.Reserve == 10, "E during a wave does nothing");
                s_Waves.EndWave(1, false);
                Check(Find<WaveBanner>().LastSubtitle.Contains("МАШИН"), $"wave cleared banner: \"{Find<WaveBanner>().LastSubtitle}\"");
            });
            At(2.6f, () =>
            {
                Check(resupply.IsSupplied && s_Interaction.Available && s_Interaction.Text == "ПОПОЛНИТЬ ПАТРОНЫ",
                    "after the wave: \"ПОПОЛНИТЬ ПАТРОНЫ\", available");
                Check(resupply.transform.Find("Icon/Sprite").GetComponent<SpriteRenderer>().enabled, "ammo box marker over the car");
                Shot("7_Refill_Prompt");
                // Walking away cancels the refill.
                Press();
            });
            At(2.8f, () =>
            {
                Check(s_Interactor.IsBusy, "E starts the refill (the player stands still)");
                Teleport(resupply.transform.position + new Vector3(4f, 0f, 5f));
            });
            At(3.7f, () =>
            {
                Check(!s_Interactor.IsBusy && s_Weapon.Reserve == 10 && resupply.IsSupplied, $"walking away cancels it, the supply stays (busy {s_Interactor.IsBusy}, " +
                    $"reserve {s_Weapon.Reserve}, supplied {resupply.IsSupplied}, {resupply.DistanceTo(s_Player.position):F1} m)");
                Teleport(resupply.transform.position);
            });
            At(4.0f, () =>
            {
                Press();
                pressedAt = Time.realtimeSinceStartup;
            });
            for (float t = 4.05f; t < 7.6f; t += 0.03f)
                At(t, () =>
                {
                    if (pressedAt < 0.6f)
                        return;
                    busy |= s_Interactor.IsBusy;
                    sound |= Playing("Ammo_Refill");
                    reloadAnim |= animator.GetBool("Reloading");
                    if (s_Interactor.IsBusy && s_Interaction.Progress > 0.4f && pressedAt > 0f)
                    {
                        pressedAt = 0f;                                    // one screenshot mid-refill
                        Shot("7_Refill_Channel");
                    }
                });
            At(7.6f, () =>
            {
                Check(busy && sound && reloadAnim, $"refill: channel {busy}, sound {sound}, reload animation {reloadAnim}");
                Check(s_Weapon.InMagazine == s_Weapon.MagazineSize && s_Weapon.Reserve == s_Weapon.MaxReserve,
                    $"ammo to the maximum: {s_Weapon.InMagazine} / {s_Weapon.Reserve}");
                Check(!resupply.IsSupplied && s_Interactor.Current == null, "once per break: the supply is used up, no prompt with a full gun");
                SetAmmo(2, 0);
            });
            At(7.9f, () =>
            {
                Check(s_Interactor.Current == resupply && !s_Interaction.Available, $"used in this break: \"{s_Interaction.Text}\"");
                Press();
            });
            At(8.3f, () =>
            {
                Check(!s_Interactor.IsBusy && s_Weapon.Reserve == 0, "a second refill in the same break does nothing");
                s_Waves.EndWave(2, false);
                s_Waves.StartWave(3);
            });
            At(8.6f, () =>
            {
                Check(!resupply.IsSupplied, "an unused supply is gone when the next wave starts");
            });
            At(8.8f, Finish);
        }

        private static void SetAmmo(int magazine, int reserve)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(PlayerWeapon).GetField("m_InMagazine", flags).SetValue(s_Weapon, magazine);
            typeof(PlayerWeapon).GetField("m_Reserve", flags).SetValue(s_Weapon, reserve);
            typeof(PlayerWeapon).GetMethod("Publish", flags).Invoke(s_Weapon, null);
        }
    }
}
