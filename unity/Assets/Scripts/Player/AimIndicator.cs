using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Brawl Stars style aim line for the touch controls: a thin strip on the ground from the player along the aim
    /// while the aim stick is held, as long as the weapon's range and cut at the first wall / car / cover in the way.
    /// Hidden on desktop (the mouse cursor shows the aim there).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AimIndicator : MonoBehaviour
    {
        private const int c_CoverMask = (1 << Layers.c_ObstacleIndex) | (1 << Layers.c_CarObstacleIndex);

        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private Transform m_Follow;
        [SerializeField, Min(0.01f)] private float m_Width = 0.45f;
        [Tooltip("Gap between the player's feet and the start of the strip.")]
        [SerializeField, Min(0f)] private float m_StartOffset = 0.45f;
        [Tooltip("Height above the ground (above decals and the lamp light pools).")]
        [SerializeField, Min(0f)] private float m_Height = 0.05f;
        [Tooltip("Height at which the line is tested against cover (bullets fly at the barrel).")]
        [SerializeField, Min(0f)] private float m_TestHeight = 1f;

        private IPlayerInput m_Input;
        private MeshRenderer m_Renderer;
        private float m_Length = 1f;

        private void Awake()
        {
            m_Renderer = GetComponent<MeshRenderer>();
            m_Renderer.enabled = false;
        }

        private void Start()
        {
            if (!DeviceInfo.IsMobile)
            {
                enabled = false;
                return;
            }
            m_Input = m_InputProvider != null ? m_InputProvider.Input : null;
            m_Length = m_Weapon != null && m_Weapon.Config != null ? m_Weapon.Config.Range - m_StartOffset : 1f;
            // Unit-long strip, stretched along z to the free length each frame.
            GetComponent<MeshFilter>().sharedMesh = BuildStrip(m_Width);
        }

        private void LateUpdate()
        {
            bool visible = m_Input != null && m_Input.AimActive && m_Follow != null;
            m_Renderer.enabled = visible;
            if (!visible)
                return;

            Vector3 direction = m_Input.AimDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < Mathf.Epsilon)
                return;
            direction.Normalize();
            Vector3 origin = m_Follow.position;
            origin.y = m_Height;
            Vector3 start = origin + direction * m_StartOffset;

            float length = m_Length;
            Vector3 test = new Vector3(start.x, m_TestHeight, start.z);
            if (Physics.Raycast(test, direction, out RaycastHit hit, m_Length, c_CoverMask, QueryTriggerInteraction.Ignore))
                length = hit.distance;
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction, Vector3.up));
            transform.localScale = new Vector3(1f, 1f, Mathf.Max(0.05f, length));
        }

        private static Mesh BuildStrip(float width)
        {
            float half = width * 0.5f;
            var mesh = new Mesh { name = "AimStrip" };
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f),
                new Vector3(half, 0f, 1f), new Vector3(-half, 0f, 1f),
            };
            // u runs along the strip (texture fades out towards u = 1), v across it.
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
