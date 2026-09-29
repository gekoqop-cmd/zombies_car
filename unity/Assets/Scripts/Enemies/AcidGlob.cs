using System;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// A boss's acid glob: flies in an arc to where the player was, a warning circle on the ground marks the landing
    /// spot while it flies; on landing it splashes (comic splat that fades) and burns the player — and the car, at half
    /// damage — within its radius. Reused by the boss that owns it.
    /// </summary>
    public sealed class AcidGlob : MonoBehaviour
    {
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform m_Ball;
        [SerializeField] private Transform m_Shadow;
        [SerializeField] private Renderer m_Target;
        [SerializeField] private Renderer m_Splat;
        [SerializeField, Min(0f)] private float m_ArcHeight = 3f;
        [SerializeField, Min(0.1f)] private float m_SplatTime = 1.4f;

        private Vector3 m_From;
        private Vector3 m_To;
        private float m_Flight;
        private float m_Age;
        private int m_Damage;
        private float m_Radius;
        private float m_Pitch = 1f;
        private ZombieSpawner m_Owner;
        private bool m_Landed;
        private MaterialPropertyBlock m_Block;
        private Color m_TargetColor;
        private Color m_SplatColor;
        private Vector3 m_ShadowScale;
        private float m_Puddle;
        private int m_PuddleDps;
        private float m_NextBurn;

        private const float c_BurnTick = 0.5f;

        public bool IsBusy => gameObject.activeSelf;

        /// <summary>The next launch leaves a burning puddle for <paramref name="seconds"/> (0: just the splash).</summary>
        public void SetPuddle(float seconds, int damagePerSecond)
        {
            m_Puddle = Mathf.Max(0f, seconds);
            m_PuddleDps = damagePerSecond;
        }

        /// <summary>Raised when the glob lands: the point and whether it hit the player.</summary>
        public event Action<Vector3, bool> Landed;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_TargetColor = m_Target.sharedMaterial.GetColor(s_BaseColor);
            m_SplatColor = m_Splat.sharedMaterial.GetColor(s_BaseColor);
            m_ShadowScale = m_Shadow.localScale;
        }

        public void Launch(Vector3 from, Vector3 to, float flight, int damage, float radius, ZombieSpawner owner, float pitch)
        {
            gameObject.SetActive(true);
            to.y = 0f;
            m_From = from;
            m_To = to;
            m_Flight = Mathf.Max(0.1f, flight);
            m_Damage = damage;
            m_Radius = radius;
            m_Owner = owner;
            m_Pitch = pitch;
            m_Age = 0f;
            m_Landed = false;
            transform.position = to;
            m_Ball.gameObject.SetActive(true);
            m_Shadow.gameObject.SetActive(true);
            m_Target.enabled = true;
            m_Splat.enabled = false;
            m_Target.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            m_Splat.transform.localScale = new Vector3(radius * 2.3f, 1f, radius * 2.3f);
            Update();
        }

        private void Update()
        {
            m_Age += Time.deltaTime;
            if (!m_Landed)
            {
                float t = Mathf.Clamp01(m_Age / m_Flight);
                Vector3 p = Vector3.Lerp(m_From, m_To, t);
                p.y = Mathf.Lerp(m_From.y, 0.3f, t) + 4f * m_ArcHeight * t * (1f - t);
                m_Ball.position = p;
                m_Shadow.position = new Vector3(p.x, 0.05f, p.z);
                m_Shadow.localScale = m_ShadowScale * Mathf.Lerp(0.4f, 1f, t);
                // The warning circle pulses faster as the glob comes down.
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(m_Age * Mathf.Lerp(6f, 18f, t)));
                SetColor(m_Target, m_TargetColor, pulse);
                if (t >= 1f)
                    Land();
                return;
            }
            // A puddle stays and burns for a while, then fades like a splash.
            float onGround = m_Age - m_Flight;
            if (onGround < m_Puddle)
            {
                SetColor(m_Splat, m_SplatColor, 0.75f + 0.25f * Mathf.Sin(m_Age * 5f));
                if (Time.time >= m_NextBurn)
                {
                    m_NextBurn = Time.time + c_BurnTick;
                    PlayerHealth player = m_Owner != null ? m_Owner.Player : null;
                    if (player != null && player.IsAlive && Planar(player.transform.position, m_To) <= m_Radius * 1.1f + player.Radius * 0.5f)
                        player.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(m_PuddleDps * c_BurnTick)), Vector3.zero);
                }
                return;
            }
            float fade = 1f - Mathf.Clamp01((onGround - m_Puddle) / m_SplatTime);
            SetColor(m_Splat, m_SplatColor, fade);
            if (fade <= 0f)
                gameObject.SetActive(false);
        }

        private void Land()
        {
            m_Landed = true;
            m_NextBurn = Time.time + c_BurnTick;
            m_Ball.gameObject.SetActive(false);
            m_Shadow.gameObject.SetActive(false);
            m_Target.enabled = false;
            m_Splat.enabled = true;
            AudioManager.PlaySfx(Sound.SpitSplat, 1f, m_Pitch);
            bool hitPlayer = false;
            PlayerHealth player = m_Owner != null ? m_Owner.Player : null;
            if (player != null && player.IsAlive && Planar(player.transform.position, m_To) <= m_Radius + player.Radius)
            {
                player.TakeDamage(m_Damage, player.transform.position - m_To);
                hitPlayer = true;
            }
            CarHealth car = m_Owner != null ? m_Owner.Car : null;
            if (car != null && car.IsAlive && Planar(car.Body.ClosestPoint(m_To), m_To) <= m_Radius)
                car.TakeDamage(Mathf.Max(1, m_Damage / 2), Vector3.zero);
            Landed?.Invoke(m_To, hitPlayer);
        }

        private void SetColor(Renderer r, Color baseColor, float alpha)
        {
            Color c = baseColor;
            c.a *= alpha;
            m_Block.SetColor(s_BaseColor, c);
            r.SetPropertyBlock(m_Block);
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
