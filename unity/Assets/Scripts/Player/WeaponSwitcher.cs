using System;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.Player
{
    /// <summary>The weapons the player can hold.</summary>
    public enum WeaponSlot
    {
        Gun,
        Axe,
    }

    /// <summary>
    /// Which weapon is in hand: the gun or the axe. PC: Q or the mouse wheel toggles, 1 / 2 or a click on a HUD slot
    /// pick; touch screens: a tap on a weapon slot (<see cref="WeaponState.Request"/>). When the gun is completely empty (no rounds in the magazine nor in reserve) and the player
    /// attacks, the axe comes out by itself, and the gun comes back by itself as soon as there are rounds again (only
    /// after such an automatic switch). Switching cancels a reload. Publishes the weapon to <see cref="WeaponState"/>
    /// for the HUD.
    /// </summary>
    [DefaultExecutionOrder(-40)]   // before the gun and the axe read their Holstered / Active flags
    public sealed class WeaponSwitcher : MonoBehaviour
    {
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Gun;
        [SerializeField] private PlayerMelee m_Axe;
        [SerializeField] private WeaponState m_State;
        [SerializeField] private RunState m_Run;

        private IPlayerInput m_Input;
        private WeaponSlot m_Current = WeaponSlot.Gun;
        private bool m_Automatic;

        public WeaponSlot Current => m_Current;

        /// <summary>The weapon changed (sound, HUD, the axe in hand).</summary>
        public event Action<WeaponSlot> Switched;

        private void Start()
        {
            m_Input = m_InputProvider != null ? m_InputProvider.Input : null;
            Apply(WeaponSlot.Gun, false, true);
        }

        private void OnEnable()
        {
            if (m_State != null)
                m_State.Requested += OnRequested;
        }

        private void OnDisable()
        {
            if (m_State != null)
                m_State.Requested -= OnRequested;
        }

        // A HUD slot was clicked / tapped.
        private void OnRequested(WeaponSlot slot)
        {
            if (GamePause.IsPaused || (m_Run != null && m_Run.IsOver))
                return;
            Switch(slot);
        }

        private void Update()
        {
            if (m_Input == null || (m_Run != null && m_Run.IsOver))
                return;
            bool inputAllowed = !GamePause.IsPaused && GamePause.ChangedFrame != Time.frameCount;
            if (inputAllowed)
            {
                int slot = m_Input.WeaponSlotPressed;
                if (slot >= 0)
                    Switch((WeaponSlot)slot);
                else if (m_Input.SwitchWeaponPressed)
                    Switch(m_Current == WeaponSlot.Gun ? WeaponSlot.Axe : WeaponSlot.Gun);
            }
            // Out of ammo: the axe comes out when the player tries to shoot; the gun is back once there are rounds.
            bool attack = m_Input.FireHeld || m_Input.FirePressed || m_Input.QuickTap;
            if (m_Current == WeaponSlot.Gun && m_Gun.IsEmpty && attack)
                Apply(WeaponSlot.Axe, true, false);
            else if (m_Automatic && m_Current == WeaponSlot.Axe && !m_Gun.IsEmpty)
                Apply(WeaponSlot.Gun, false, false);
        }

        /// <summary>Takes <paramref name="slot"/> in hand (input, tests).</summary>
        public void Switch(WeaponSlot slot) => Apply(slot, false, false);

        private void Apply(WeaponSlot slot, bool automatic, bool silent)
        {
            m_Automatic = automatic;
            if (slot == m_Current && !silent)
                return;
            m_Current = slot;
            m_Gun.Holstered = slot != WeaponSlot.Gun;
            m_Axe.Active = slot == WeaponSlot.Axe;
            if (m_State != null)
                m_State.Set(slot, automatic);
            if (!silent)
            {
                AudioManager.PlaySfx(Sound.WeaponSwitch);
                Switched?.Invoke(slot);
            }
        }
    }
}
