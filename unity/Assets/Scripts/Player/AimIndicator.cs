using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Thin strip on the ground from the player along the aim direction. The mesh (a quad with a gradient texture
    /// that fades towards the end) is generated once; its length equals the weapon range.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AimIndicator : MonoBehaviour
    {
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private Transform m_Follow;
        [SerializeField, Min(0.01f)] private float m_Width = 0.14f;
        [Tooltip("Gap between the player's feet and the start of the strip.")]
        [SerializeField, Min(0f)] private float m_StartOffset = 0.45f;
        [Tooltip("Height above the ground (above decals, below everything else).")]
        [SerializeField, Min(0f)] private float m_Height = 0.035f;

        private IPlayerInput m_Input;
        private MeshRenderer m_Renderer;

        private void Awake() => m_Renderer = GetComponent<MeshRenderer>();

        private void Start()
        {
            m_Input = m_InputProvider != null ? m_InputProvider.Input : null;
            float length = m_Weapon != null && m_Weapon.Config != null ? m_Weapon.Config.Range : 1f;
            // Child of the player (destroyed with it) but placed in world space every LateUpdate,
            // so it points exactly at the aim even while the body is still turning.
            GetComponent<MeshFilter>().sharedMesh = BuildStrip(length - m_StartOffset, m_Width);
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
            transform.SetPositionAndRotation(origin + direction * m_StartOffset, Quaternion.LookRotation(direction, Vector3.up));
        }

        private static Mesh BuildStrip(float length, float width)
        {
            float half = width * 0.5f;
            var mesh = new Mesh { name = "AimStrip" };
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f),
                new Vector3(half, 0f, length), new Vector3(-half, 0f, length),
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
