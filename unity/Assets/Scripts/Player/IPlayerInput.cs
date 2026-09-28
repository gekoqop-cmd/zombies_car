using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Device-independent player input. Player logic (movement, aiming, shooting) reads only this interface,
    /// never the keyboard, mouse or touch screen directly, so a mobile implementation can be added without
    /// touching the player code.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Movement stick, x = right, y = forward (screen up); magnitude 0..1.</summary>
        Vector2 Move { get; }

        /// <summary>Normalized aim direction on the ground plane (y = 0), world space.</summary>
        Vector3 AimDirection { get; }

        /// <summary>True while the player is aiming manually (mouse on PC, dragged fire stick on mobile).</summary>
        bool AimActive { get; }

        /// <summary>Fire is held this frame (automatic fire).</summary>
        bool FireHeld { get; }

        /// <summary>Fire went down this frame.</summary>
        bool FirePressed { get; }

        /// <summary>Fire went up this frame (mobile: release of the fire stick shoots).</summary>
        bool FireReleased { get; }

        /// <summary>A short tap on the fire control this frame (mobile: auto-aim shot at the nearest enemy).</summary>
        bool QuickTap { get; }

        /// <summary>Reload was requested this frame (PC: R; mobile: the reload button).</summary>
        bool ReloadPressed { get; }

        /// <summary>Use what is nearby this frame: workshop, ammo at the car (PC: E; mobile: the action button).</summary>
        bool InteractPressed { get; }

        /// <summary>Switch to the other weapon this frame (PC: Q or the mouse wheel; mobile: the weapon button).</summary>
        bool SwitchWeaponPressed { get; }

        /// <summary>A weapon picked directly this frame: 0 = gun, 1 = axe, -1 = none (PC: keys 1 / 2).</summary>
        int WeaponSlotPressed { get; }

        /// <summary>
        /// Camera ray through the aim cursor (PC mouse). Devices that aim with a direction only (stick) return false
        /// and the weapon fires along <see cref="AimDirection"/>.
        /// </summary>
        bool TryGetAimRay(out Ray ray);
    }
}
