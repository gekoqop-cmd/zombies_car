using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZombiCar.Player
{
    /// <summary>
    /// PC input through the Input System: WASD / arrows move, the mouse cursor projected onto the ground aims,
    /// the left mouse button fires (hold = automatic fire). Keyboard bindings use physical key positions,
    /// so they work with any keyboard layout (Yandex Games requirement).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DesktopPlayerInput : MonoBehaviour, IPlayerInput
    {
        private const string c_Vector2Composite = "2DVector";

        [Tooltip("Where aiming starts from (the player). The aim direction is cursor-on-ground minus this position.")]
        [SerializeField] private Transform m_AimOrigin;
        [Tooltip("Height of the plane the cursor ray is projected on (ground level).")]
        [SerializeField] private float m_AimPlaneHeight;
        [Tooltip("Cursor closer than this to the player keeps the previous direction (avoids jitter).")]
        [SerializeField, Min(0f)] private float m_MinAimDistance = 0.3f;

        private InputAction m_MoveAction;
        private InputAction m_FireAction;
        private InputAction m_PointAction;
        private Camera m_Camera;
        private Vector3 m_AimDirection = Vector3.forward;
        private bool m_FireStartedOverUi;

        public Vector2 Move { get; private set; }
        public Vector3 AimDirection => m_AimDirection;
        public bool AimActive { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool FireReleased { get; private set; }
        public bool QuickTap => false;

        /// <summary>Cursor position on the ground (valid while <see cref="AimActive"/>).</summary>
        public Vector3 AimPoint { get; private set; }

        private void Awake()
        {
            if (m_AimOrigin == null)
                m_AimOrigin = transform;

            m_MoveAction = new InputAction("Move", InputActionType.Value);
            m_MoveAction.AddCompositeBinding(c_Vector2Composite)
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            m_MoveAction.AddCompositeBinding(c_Vector2Composite)
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            m_MoveAction.AddBinding("<Gamepad>/leftStick");

            m_FireAction = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            m_PointAction = new InputAction("Point", InputActionType.Value, "<Mouse>/position");
        }

        private void OnEnable()
        {
            m_MoveAction.Enable();
            m_FireAction.Enable();
            m_PointAction.Enable();
        }

        private void OnDisable()
        {
            m_MoveAction.Disable();
            m_FireAction.Disable();
            m_PointAction.Disable();
            Move = Vector2.zero;
            FireHeld = FirePressed = FireReleased = AimActive = false;
        }

        private void OnDestroy()
        {
            m_MoveAction?.Dispose();
            m_FireAction?.Dispose();
            m_PointAction?.Dispose();
        }

        private void Update()
        {
            Move = Vector2.ClampMagnitude(m_MoveAction.ReadValue<Vector2>(), 1f);
            // A click that starts on a HUD button (settings, etc.) must not shoot.
            bool pressedNow = m_FireAction.WasPressedThisFrame();
            if (pressedNow)
                m_FireStartedOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool released = m_FireAction.WasReleasedThisFrame();
            FireHeld = m_FireAction.IsPressed() && !m_FireStartedOverUi;
            FirePressed = pressedNow && !m_FireStartedOverUi;
            FireReleased = released && !m_FireStartedOverUi;
            if (released)
                m_FireStartedOverUi = false;
            UpdateAim();
        }

        private void UpdateAim()
        {
            AimActive = Mouse.current != null;
            if (!AimActive)
                return;
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
                if (m_Camera == null)
                    return;
            }

            Ray ray = m_Camera.ScreenPointToRay(m_PointAction.ReadValue<Vector2>());
            var ground = new Plane(Vector3.up, new Vector3(0f, m_AimPlaneHeight, 0f));
            if (!ground.Raycast(ray, out float distance))
                return;

            AimPoint = ray.GetPoint(distance);
            Vector3 toCursor = AimPoint - m_AimOrigin.position;
            toCursor.y = 0f;
            if (toCursor.sqrMagnitude >= m_MinAimDistance * m_MinAimDistance)
                m_AimDirection = toCursor.normalized;
        }
    }
}
