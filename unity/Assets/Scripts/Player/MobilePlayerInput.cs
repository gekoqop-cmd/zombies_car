using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Touch controls (Brawl Stars style), fed by the on-screen sticks through <see cref="MobileControls"/>:
    /// the left stick moves; dragging the right stick aims along it and fires while held; a quick tap on it fires one
    /// shot (or one axe swing) at the best target in range — the nearest zombie the gun can see, not one behind a wall
    /// or the car — or straight ahead when there is none. The aim stick points where the thumb points on the screen:
    /// its direction is laid onto the ground through the camera's perspective (a diagonal on the screen hits what is on
    /// that diagonal), not just turned by the camera's yaw.
    /// </summary>
    [DefaultExecutionOrder(-60)]   // aim ready before PlayerView (-50) and PlayerWeapon read it
    public sealed class MobilePlayerInput : MonoBehaviour, IPlayerInput
    {
        private static readonly Collider[] s_Candidates = new Collider[32];
        private const float c_LowSight = 0.5f;                       // the foot of the bullets' hit band

        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("The axe: a tap with the axe in hand swings once.")]
        [SerializeField, OptionalReference] private PlayerMelee m_Melee;
        [Tooltip("Seconds a tap keeps the aim line and the facing on its target, and waits for the gun to be ready.")]
        [SerializeField, Min(0.05f)] private float m_TapAim = 0.3f;
        [Tooltip("Height of the shots above the ground: the stick's direction is laid onto this plane.")]
        [SerializeField, Min(0f)] private float m_AimHeight = 1f;

        private Vector3 m_AimDirection = Vector3.right;
        private float m_TapUntil = float.NegativeInfinity;
        private bool m_TapPending;
        private int m_LastTapFrame = -1;
        private Transform m_Target;

        public bool IsImplemented => true;

        public Vector2 Move => MobileControls.Move;
        public Vector3 AimDirection => m_AimDirection;
        public bool AimActive => MobileControls.Aiming || Time.time < m_TapUntil;
        public bool FireHeld => MobileControls.Aiming;
        public bool FirePressed => false;

        /// <summary>A tap waits for its one shot or swing (until the gun fires or the tap's moment passes).</summary>
        public bool QuickTap => m_TapPending && Time.time < m_TapUntil;

        public bool FireReleased => false;
        public bool ReloadPressed => MobileControls.ReloadFrame >= Time.frameCount - 1;
        public bool InteractPressed => MobileControls.InteractFrame >= Time.frameCount - 1;
        public bool SwitchWeaponPressed => MobileControls.SwitchWeaponFrame >= Time.frameCount - 1;
        public int WeaponSlotPressed => -1;

        /// <summary>The target of the last tap (tests).</summary>
        public Transform TapTarget => m_Target;

        public bool TryGetAimRay(out Ray ray)
        {
            ray = default;   // sticks aim by direction only
            return false;
        }

        private void OnEnable()
        {
            if (m_Weapon != null)
                m_Weapon.Fired += OnShot;
            if (m_Melee != null)
                m_Melee.Swung += OnShot;
        }

        private void OnDisable()
        {
            if (m_Weapon != null)
                m_Weapon.Fired -= OnShot;
            if (m_Melee != null)
                m_Melee.Swung -= OnShot;
            MobileControls.Reset();
        }

        // One tap, one shot: the tap is used up by it.
        private void OnShot() => m_TapPending = false;

        /// <summary>A tap on the aim stick (the stick, tests): one shot at the best target.</summary>
        public void Tap()
        {
            m_Target = BestTarget();
            m_TapPending = true;
            m_TapUntil = Time.time + m_TapAim;
            m_AimDirection = m_Target != null ? Flat(m_Target.position - transform.position, m_AimDirection) : FacingFallback();
        }

        // Early (before the weapon and the view): the aim of this frame is ready when they read it.
        private void Update()
        {
            if (MobileControls.TapFrame != m_LastTapFrame && MobileControls.TapFrame >= Time.frameCount - 1)
            {
                m_LastTapFrame = MobileControls.TapFrame;
                Tap();
            }

            if (MobileControls.Aiming && MobileControls.Aim.sqrMagnitude > 1e-4f)
            {
                m_AimDirection = ScreenToGround(MobileControls.Aim);
                m_TapPending = false;
                m_TapUntil = float.NegativeInfinity;
                m_Target = null;
            }
            else if (Time.time < m_TapUntil && m_Target != null && m_Target.gameObject.activeInHierarchy)
                m_AimDirection = Flat(m_Target.position - transform.position, m_AimDirection);
        }

        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : fallback;
        }

        private Vector3 FacingFallback()
        {
            Vector2 move = MobileControls.Move;
            return move.sqrMagnitude > 0.01f ? YawToGround(move) : m_AimDirection;
        }

        // The stick's screen direction onto the ground through the camera: the point the thumb points to, a little way
        // from the player on the screen, is cast onto the shots' height plane; the aim goes from the player to it.
        private Vector3 ScreenToGround(Vector2 stick)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return YawToGround(stick);
            Vector3 origin = transform.position + Vector3.up * m_AimHeight;
            Vector3 screen = cam.WorldToScreenPoint(origin);
            if (screen.z <= 0f)
                return YawToGround(stick);
            float reach = Mathf.Max(60f, Screen.height * 0.18f);
            Ray ray = cam.ScreenPointToRay((Vector2)screen + stick.normalized * reach);
            var plane = new Plane(Vector3.up, origin);
            if (!plane.Raycast(ray, out float distance))
                return YawToGround(stick);
            return Flat(ray.GetPoint(distance) - origin, YawToGround(stick));
        }

        private static Vector3 YawToGround(Vector2 screen)
        {
            Camera cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            Vector3 d = Quaternion.Euler(0f, yaw, 0f) * new Vector3(screen.x, 0f, screen.y);
            return d.normalized;
        }

        // The nearest zombie in range the shot can reach (nothing in the way); the nearest at all when none can be.
        private Transform BestTarget()
        {
            float range = m_Weapon != null && m_Weapon.Config != null ? m_Weapon.Range : 14f;
            if (m_Melee != null && m_Melee.Active)
                range = Mathf.Max(range, m_Melee.Range * 2.5f);
            Vector3 eye = transform.position + Vector3.up * m_AimHeight;
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, s_Candidates, Layers.Enemies, QueryTriggerInteraction.Ignore);
            Transform best = null, bestHidden = null;
            float bestDistance = float.MaxValue, bestHiddenDistance = float.MaxValue;
            int blockers = (1 << Layers.c_ObstacleIndex) | (1 << Layers.c_CarObstacleIndex);
            for (int i = 0; i < count; i++)
            {
                Transform t = s_Candidates[i].transform;
                Vector3 to = t.position - transform.position;
                to.y = 0f;
                float d = to.sqrMagnitude;
                // Bullets are stopped over a band of heights (low cover, the car's body): look at the band's foot too.
                Vector3 aimPoint = new Vector3(t.position.x, eye.y, t.position.z);
                Vector3 low = new Vector3(transform.position.x, c_LowSight, transform.position.z);
                bool hidden = Physics.Linecast(eye, aimPoint, blockers, QueryTriggerInteraction.Ignore) ||
                              Physics.Linecast(low, new Vector3(t.position.x, c_LowSight, t.position.z), blockers, QueryTriggerInteraction.Ignore);
                if (!hidden && d < bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
                else if (hidden && d < bestHiddenDistance)
                {
                    bestHiddenDistance = d;
                    bestHidden = t;
                }
            }
            return best != null ? best : bestHidden;
        }
    }
}
