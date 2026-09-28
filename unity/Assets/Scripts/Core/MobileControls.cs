using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Shared state of the on-screen controls (UI_HUD scene) read by the player's mobile input (map scene), so the two
    /// scenes need no references to each other. Sticks write here every frame; one-shot actions are frame-stamped.
    /// </summary>
    public static class MobileControls
    {
        /// <summary>Left stick, x = right, y = up (screen), magnitude 0..1.</summary>
        public static Vector2 Move { get; set; }

        /// <summary>Right stick, same axes; valid while <see cref="Aiming"/>.</summary>
        public static Vector2 Aim { get; set; }

        /// <summary>The right stick is dragged past its dead zone: aim along <see cref="Aim"/> and fire.</summary>
        public static bool Aiming { get; set; }

        /// <summary>Frame of the last quick tap on the right stick (auto-aim shot).</summary>
        public static int TapFrame { get; set; } = -1;

        /// <summary>Frame of the last press of the reload button.</summary>
        public static int ReloadFrame { get; set; } = -1;

        /// <summary>Frame of the last press of the action button (workshop, ammo at the car).</summary>
        public static int InteractFrame { get; set; } = -1;

        /// <summary>Frame of the last press of the weapon button (gun / axe).</summary>
        public static int SwitchWeaponFrame { get; set; } = -1;

        public static void Reset()
        {
            Move = Aim = Vector2.zero;
            Aiming = false;
        }
    }
}
