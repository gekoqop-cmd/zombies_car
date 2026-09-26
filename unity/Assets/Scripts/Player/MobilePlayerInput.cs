using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// PLACEHOLDER for touch controls (not implemented yet). Implements <see cref="IPlayerInput"/> so the player code
    /// does not change when it is filled in. Planned scheme (Brawl Stars style):
    ///
    /// 1. Left virtual stick (UI, bottom-left, dynamic position = first touch point) -> <see cref="Move"/>.
    /// 2. Right fire stick (UI, bottom-right):
    ///    - short tap (touch shorter than ~0.2 s and moved less than the dead zone) -> <see cref="QuickTap"/> = true for
    ///      one frame; the weapon then auto-aims at the nearest enemy (needs a target finder, e.g. an OverlapSphere
    ///      on an "Enemy" layer, feeding AimDirection for that shot);
    ///    - press + drag past the dead zone -> <see cref="AimActive"/> = true, <see cref="AimDirection"/> = stick
    ///      direction converted from screen space to the ground using the camera yaw; the AimIndicator strip shows it;
    ///    - release after dragging -> <see cref="FireReleased"/> = true (one shot / burst in the aimed direction).
    ///    Automatic fire while holding can map to <see cref="FireHeld"/> if the design wants it.
    /// 3. Sticks live in a separate UI prefab (Prefabs/UI) added to UI_HUD only on mobile; they write their values here.
    /// 4. Enable this component through <see cref="PlayerInputProvider"/> (it already switches on Application.isMobilePlatform;
    ///    later the Yandex SDK deviceInfo can be used instead).
    /// </summary>
    public sealed class MobilePlayerInput : MonoBehaviour, IPlayerInput
    {
        /// <summary>Set to true once the touch controls exist; until then the provider falls back to the PC input.</summary>
        public bool IsImplemented => false;

        public Vector2 Move => Vector2.zero;
        public Vector3 AimDirection => transform.forward;
        public bool AimActive => false;
        public bool FireHeld => false;
        public bool FirePressed => false;
        public bool FireReleased => false;
        public bool QuickTap => false;
    }
}
