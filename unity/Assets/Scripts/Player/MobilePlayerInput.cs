using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Touch controls (Brawl Stars style), fed by the on-screen sticks through <see cref="MobileControls"/>:
    /// the left stick moves; dragging the right stick aims along it and fires while held; a quick tap on it auto-aims
    /// at the nearest zombie in range and fires a short burst (straight ahead if there is none).
    /// Stick directions are screen directions, turned onto the ground by the camera's yaw.
    /// </summary>
    [DefaultExecutionOrder(-60)]   // aim ready before PlayerView (-50) and PlayerWeapon read it
    public sealed class MobilePlayerInput : MonoBehaviour, IPlayerInput
    {
        private static readonly Collider[] s_Candidates = new Collider[32];

        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("Seconds of fire after a quick tap.")]
        [SerializeField, Min(0.05f)] private float m_TapBurst = 0.35f;

        private Vector3 m_AimDirection = Vector3.right;
        private float m_BurstUntil = float.NegativeInfinity;
        private int m_LastTapFrame = -1;
        private Transform m_Target;

        public bool IsImplemented => true;

        public Vector2 Move => MobileControls.Move;
        public Vector3 AimDirection => m_AimDirection;
        public bool AimActive => MobileControls.Aiming || Time.time < m_BurstUntil;
        public bool FireHeld => AimActive;
        public bool FirePressed => false;
        public bool FireReleased => false;
        public bool QuickTap => false;
        public bool ReloadPressed => MobileControls.ReloadFrame >= Time.frameCount - 1;
        public bool InteractPressed => MobileControls.InteractFrame >= Time.frameCount - 1;
        public bool SwitchWeaponPressed => MobileControls.SwitchWeaponFrame >= Time.frameCount - 1;
        public int WeaponSlotPressed => -1;

        public bool TryGetAimRay(out Ray ray)
        {
            ray = default;   // sticks aim by direction only
            return false;
        }

        private void OnDisable() => MobileControls.Reset();

        // Early (before the weapon and the view): the aim of this frame is ready when they read it.
        private void Update()
        {
            if (MobileControls.TapFrame != m_LastTapFrame && MobileControls.TapFrame >= Time.frameCount - 1)
            {
                m_LastTapFrame = MobileControls.TapFrame;
                m_Target = NearestEnemy();
                m_BurstUntil = Time.time + m_TapBurst;
                if (m_Target == null)
                    m_AimDirection = FacingFallback();
            }

            if (MobileControls.Aiming && MobileControls.Aim.sqrMagnitude > 1e-4f)
            {
                m_AimDirection = ScreenToGround(MobileControls.Aim);
                m_BurstUntil = float.NegativeInfinity;
                m_Target = null;
            }
            else if (Time.time < m_BurstUntil && m_Target != null && m_Target.gameObject.activeInHierarchy)
            {
                Vector3 to = m_Target.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-4f)
                    m_AimDirection = to.normalized;
            }
        }

        private Vector3 FacingFallback()
        {
            Vector2 move = MobileControls.Move;
            return move.sqrMagnitude > 0.01f ? ScreenToGround(move) : m_AimDirection;
        }

        private static Vector3 ScreenToGround(Vector2 screen)
        {
            Camera cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            Vector3 d = Quaternion.Euler(0f, yaw, 0f) * new Vector3(screen.x, 0f, screen.y);
            return d.normalized;
        }

        private Transform NearestEnemy()
        {
            float range = m_Weapon != null && m_Weapon.Config != null ? m_Weapon.Range : 14f;
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, s_Candidates, Layers.Enemies, QueryTriggerInteraction.Ignore);
            Transform best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float d = (s_Candidates[i].transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = s_Candidates[i].transform;
                }
            }
            return best;
        }
    }
}
