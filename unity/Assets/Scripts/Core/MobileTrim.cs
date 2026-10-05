using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Lighter maps on phones (only there; the PC keeps everything). At the start of a map it stops drawing what a
    /// phone pays for but the player never sees: the props inside the workshop building (behind its walls), and every
    /// prop, tree, fence or decal that lies outside all the camera can ever show — the view of the camera at each
    /// corner of the area it may move in, at the ground and at tree-top height, with a margin, on the widest phone
    /// screen. Colliders stay as they are; only the renderers go.
    /// </summary>
    public sealed class MobileTrim : MonoBehaviour
    {
        [SerializeField] private MapConfig m_Map;
        [Tooltip("The followed player: the camera keeps its offset from it.")]
        [SerializeField] private Transform m_Follow;
        [Tooltip("The workshop building: its props inside the walls are not drawn.")]
        [SerializeField, OptionalReference] private Transform m_Workshop;
        [Tooltip("The scenery roots to trim (environment, props, nature, fences, decals, background).")]
        [SerializeField] private Transform[] m_Roots = System.Array.Empty<Transform>();
        [Tooltip("Extra metres round what the camera can see.")]
        [SerializeField, Min(0f)] private float m_Margin = 4f;
        [Tooltip("The widest screen it plans for (width / height).")]
        [SerializeField, Min(1f)] private float m_WidestAspect = 2.4f;
        [SerializeField, Min(0f)] private float m_TallHeight = 10f;
        [Tooltip("The workshop's footprint (half width, half depth, local metres): props inside it are hidden.")]
        [SerializeField] private Vector2 m_WorkshopHalfSize = new Vector2(2.9f, 2.1f);

        /// <summary>Renderers switched off (tests).</summary>
        public int Trimmed { get; private set; }

        /// <summary>The ground rectangle the camera can ever show, with the margin (tests).</summary>
        public Rect Visible { get; private set; }

        private void Start()
        {
            if (DeviceInfo.IsMobile)
                Trim();
        }

        /// <summary>Switches off what is never seen (the start on a phone; tests anywhere).</summary>
        public void Trim()
        {
            Camera cam = Camera.main;
            if (cam == null || m_Map == null || m_Follow == null)
                return;
            Visible = ViewArea(cam);
            int count = 0;
            if (m_Workshop != null)
            {
                foreach (Transform child in m_Workshop)
                {
                    Vector3 p = child.localPosition;
                    if (!child.name.StartsWith("Prop_") || child.name.StartsWith("Prop_Barrel") ||
                        Mathf.Abs(p.x) > m_WorkshopHalfSize.x || Mathf.Abs(p.z) > m_WorkshopHalfSize.y)
                        continue;
                    foreach (Renderer r in child.GetComponentsInChildren<Renderer>())
                        count += Hide(r);
                }
            }
            foreach (Transform root in m_Roots)
            {
                if (root == null)
                    continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = r.bounds;
                    var flat = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
                    if (!flat.Overlaps(Visible))
                        count += Hide(r);
                }
            }
            Trimmed = count;
            Debug.Log($"[MobileTrim] {count} renderers off (outside the view {Visible} or inside the workshop).");
        }

        private static int Hide(Renderer r)
        {
            if (!r.enabled)
                return 0;
            r.enabled = false;
            return 1;
        }

        // The union of the camera's view on the ground (and at tree-top height) with the look point at each corner of
        // the area the camera may follow the player in.
        private Rect ViewArea(Camera cam)
        {
            Transform t = cam.transform;
            Vector3 offset = t.position - m_Follow.position;
            Vector3 home = t.position;
            float aspect = cam.aspect;
            cam.aspect = Mathf.Max(aspect, m_WidestAspect);
            Vector2 half = m_Map.PlayAreaSize * 0.5f - Vector2.one * m_Map.CameraEdgeMargin;
            Vector3 center = m_Map.PlayAreaCenter;
            var points = new List<Vector3>();
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                t.position = new Vector3(center.x + corner.x * Mathf.Max(0f, half.x), m_Follow.position.y, center.z + corner.y * Mathf.Max(0f, half.y)) + offset;
                foreach (Vector2 viewport in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                {
                    Ray ray = cam.ViewportPointToRay(viewport);
                    foreach (float height in new[] { 0f, m_TallHeight })
                        if (new Plane(Vector3.up, new Vector3(0f, height, 0f)).Raycast(ray, out float d))
                            points.Add(ray.GetPoint(Mathf.Min(d, 400f)));
                }
            }
            t.position = home;
            cam.ResetAspect();
            if (points.Count == 0)
                return new Rect(-1000f, -1000f, 2000f, 2000f);
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (Vector3 p in points)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
            return Rect.MinMaxRect(minX - m_Margin, minZ - m_Margin, maxX + m_Margin, maxZ + m_Margin);
        }
    }
}
