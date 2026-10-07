using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// The death of a regular zombie: the body bursts into the chunks of its own idle frame (cut by
    /// tools/characters/zombie_mp4/process.py), which fly apart away from the killing hit, spin, bleed, bounce on the
    /// ground and lie in their splats until <see cref="ZombieController"/> fades them out. The chunks are upright sprites
    /// in the billboarded view (x right on screen, y up, z away from the camera); the blood is <see cref="BloodFx"/>.
    /// </summary>
    public sealed class ZombieGibs : MonoBehaviour
    {
        private const float c_Gravity = 13f;
        private const float c_Bounce = 0.28f;
        private const float c_Friction = 0.55f;
        private const float c_RestSpeed = 0.7f;
        private const float c_DripInterval = 0.045f;
        private const float c_DripTime = 0.55f;

        [SerializeField] private Transform m_View;
        [SerializeField] private Sprite[] m_Pieces = new Sprite[0];
        [Tooltip("Centre of every piece in the view plane, metres from the feet (facing right).")]
        [SerializeField] private Vector2[] m_Offsets = new Vector2[0];
        [Tooltip("Local scale of the pieces (the body sprite's scale).")]
        [SerializeField] private Vector2 m_Scale = Vector2.one;
        [SerializeField] private Color m_Tint = Color.white;
        [SerializeField] private Material m_SpriteMaterial;
        [SerializeField] private Material m_DropMaterial;
        [SerializeField] private Material m_SplatMaterial;
        [Tooltip("Size of the character: scales the burst speed and the blood.")]
        [SerializeField, Min(0.1f)] private float m_Size = 1f;

        private SpriteRenderer[] m_Renderers;
        private Vector3[] m_Position;
        private Vector3[] m_Velocity;
        private float[] m_Angle;
        private float[] m_Spin;
        private float[] m_Rest;          // height of the centre when lying (about half the piece)
        private bool[] m_Landed;
        private float m_Time;
        private float m_NextDrip;
        private float m_Alpha = 1f;
        private int m_Facing = 1;

        private void Awake()
        {
            int n = m_Pieces.Length;
            m_Renderers = new SpriteRenderer[n];
            m_Position = new Vector3[n];
            m_Velocity = new Vector3[n];
            m_Angle = new float[n];
            m_Spin = new float[n];
            m_Rest = new float[n];
            m_Landed = new bool[n];
            var container = new GameObject("Gibs").transform;
            container.SetParent(m_View, false);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Gib" + i);
                go.transform.SetParent(container, false);
                go.layer = gameObject.layer;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = m_Pieces[i];
                r.sharedMaterial = m_SpriteMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                m_Renderers[i] = r;
                Vector2 extents = m_Pieces[i] != null ? (Vector2)m_Pieces[i].bounds.extents : Vector2.zero;
                m_Rest[i] = Mathf.Min(extents.x * m_Scale.x, extents.y * m_Scale.y) * 0.8f;
            }
            enabled = false;
        }

        /// <summary>Bursts the pieces out: <paramref name="hit"/> is the killing hit's direction (world), may be zero.</summary>
        public void Burst(Vector3 hit, int facing)
        {
            if (m_Renderers == null || m_Renderers.Length == 0)
                return;
            BloodFx.Ensure(m_DropMaterial, m_SplatMaterial);
            m_Facing = facing >= 0 ? 1 : -1;
            hit.y = 0f;
            Vector3 push = hit.sqrMagnitude > 1e-4f ? m_View.InverseTransformDirection(hit.normalized) : Vector3.zero;
            push.y = 0f;

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < m_Offsets.Length; i++)
                centre += Local(i);
            centre /= Mathf.Max(1, m_Offsets.Length);

            float speed = Mathf.Sqrt(m_Size);
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Vector3 p = Local(i);
                Vector3 away = p - centre;
                away.z = Random.Range(-0.6f, 0.6f);
                away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.onUnitSphere;
                m_Position[i] = p;
                m_Velocity[i] = (away * Random.Range(1.6f, 3.4f) + Vector3.up * Random.Range(1.8f, 4.2f)
                    + push * Random.Range(1.2f, 2.8f)) * speed;
                m_Angle[i] = 0f;
                m_Spin[i] = Random.Range(360f, 900f) * (Random.value < 0.5f ? -1f : 1f);
                m_Landed[i] = false;
                m_Renderers[i].enabled = true;
            }
            m_Time = 0f;
            m_NextDrip = 0f;
            m_Alpha = 1f;
            enabled = true;
            Apply();

            Vector3 chest = m_View.TransformPoint(centre);
            Vector3 pushWorld = m_View.TransformDirection(push) * 2.2f;
            BloodFx.Spray(chest, pushWorld, Mathf.RoundToInt(30 * m_Size), m_Size);
            BloodFx.Splat(transform.position + pushWorld * 0.12f, 1.5f * m_Size);
            AudioManager.PlaySfx(Sound.ZombieSplat);
        }

        /// <summary>Fades the lying pieces out (1 = solid, 0 = gone).</summary>
        public void SetFade(float alpha)
        {
            m_Alpha = Mathf.Clamp01(alpha);
            if (enabled)
                return;
            Apply();
        }

        /// <summary>Back to unused (pooled zombie respawns).</summary>
        public void Hide()
        {
            if (m_Renderers == null)
                return;
            foreach (SpriteRenderer r in m_Renderers)
                r.enabled = false;
            enabled = false;
        }

        private Vector3 Local(int i)
        {
            Vector2 o = i < m_Offsets.Length ? m_Offsets[i] : Vector2.zero;
            return new Vector3(o.x * m_Facing, o.y, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_Time += dt;
            bool drip = m_Time < c_DripTime && m_Time >= m_NextDrip;
            if (drip)
                m_NextDrip = m_Time + c_DripInterval;
            bool moving = false;
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Vector3 v = m_Velocity[i];
                if (v.sqrMagnitude == 0f && m_Position[i].y <= m_Rest[i])
                    continue;
                moving = true;
                v.y -= c_Gravity * dt;
                Vector3 p = m_Position[i] + v * dt;
                m_Angle[i] += m_Spin[i] * dt;
                if (p.y <= m_Rest[i])
                {
                    p.y = m_Rest[i];
                    if (!m_Landed[i])
                    {
                        m_Landed[i] = true;
                        BloodFx.Splat(m_View.TransformPoint(p), Random.Range(0.4f, 0.65f) * m_Size);
                    }
                    if (-v.y < c_RestSpeed)
                    {
                        v = Vector3.zero;
                        m_Spin[i] = 0f;
                        // Lie flat-ish: settle on the nearest quarter turn so pieces do not rest on a corner.
                        m_Angle[i] = Mathf.Round(m_Angle[i] / 90f) * 90f + Random.Range(-15f, 15f);
                    }
                    else
                    {
                        v.y = -v.y * c_Bounce;
                        v.x *= c_Friction;
                        v.z *= c_Friction;
                        m_Spin[i] *= 0.45f;
                    }
                }
                else if (drip)
                {
                    BloodFx.Drip(m_View.TransformPoint(p), m_View.TransformDirection(v) * 0.3f, m_Size);
                }
                m_Position[i] = p;
                m_Velocity[i] = v;
            }
            Apply();
            if (!moving)
                enabled = false;
        }

        private void Apply()
        {
            Color color = m_Tint * SceneTint.Characters;
            color.a = m_Alpha;
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Transform t = m_Renderers[i].transform;
                t.localPosition = m_Position[i];
                t.localRotation = Quaternion.Euler(0f, 0f, m_Angle[i]);
                t.localScale = new Vector3(m_Scale.x * m_Facing, m_Scale.y, 1f);
                m_Renderers[i].color = color;
            }
        }
    }
}
