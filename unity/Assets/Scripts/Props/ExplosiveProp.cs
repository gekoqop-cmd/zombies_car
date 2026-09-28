using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.Waves;

namespace ZombiCar.Props
{
    /// <summary>
    /// A red barrel or canister that blows up when shot (or hit with the axe, or caught in another blast): a comic
    /// explosion (<see cref="ComicExplosion"/>) that hurts every zombie within its radius — full damage at the centre,
    /// less at the edge — and pushes them away, sets off other explosives nearby a moment later (chain reactions) and
    /// shakes the screen. The player and the car are not hurt. The "pyrotechnic" card makes blasts bigger and
    /// stronger. The prop is gone until the current wave ends, then it is back for the next one.
    /// </summary>
    public sealed class ExplosiveProp : MonoBehaviour, IShootable
    {
        private static readonly Collider[] s_Hits = new Collider[64];
        private static readonly List<ExplosiveProp> s_All = new List<ExplosiveProp>();

        [SerializeField, Min(1)] private int m_Health = 20;
        [SerializeField, Min(0.5f)] private float m_Radius = 4.5f;
        [SerializeField, Min(1)] private int m_Damage = 130;
        [Tooltip("Share of the damage left at the edge of the blast.")]
        [SerializeField, Range(0f, 1f)] private float m_EdgeDamage = 0.35f;
        [Tooltip("How far zombies are pushed at the centre, metres.")]
        [SerializeField, Min(0f)] private float m_Push = 1.4f;
        [Tooltip("Size of the comic effect.")]
        [SerializeField, Min(0.1f)] private float m_FxSize = 1f;
        [SerializeField, Min(0f)] private float m_ChainDelay = 0.15f;
        [SerializeField] private ComicExplosion m_Explosion;
        [SerializeField] private RunState m_Run;
        [SerializeField] private WaveState m_Waves;

        private readonly HashSet<IDamageable> m_Hit = new HashSet<IDamageable>();
        private Renderer[] m_Renderers;
        private Collider[] m_Colliders;
        private int m_Left;
        private bool m_Exploded;
        private float m_RespawnStart = float.NegativeInfinity;
        private float m_BaseScale = 1f;
        private bool m_Popping;

        /// <summary>An explosive went off (tests, statistics).</summary>
        public static event Action<ExplosiveProp> Exploded;

        public static IReadOnlyList<ExplosiveProp> All => s_All;

        public bool IsAlive => !m_Exploded;
        public float Radius => m_Radius * PowerRadius;

        private int Power => m_Run != null ? m_Run.Modifiers.Whole(StatEffect.ExplosionPower) : 0;
        private float PowerRadius => 1f + 0.25f * Power;
        private float PowerDamage => 1f + 0.5f * Power;

        private void Awake()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_Left = m_Health;
        }

        private void OnEnable()
        {
            s_All.Add(this);
            if (m_Waves != null)
                m_Waves.WaveEnded += OnWaveEnded;
        }

        private void OnDisable()
        {
            s_All.Remove(this);
            if (m_Waves != null)
                m_Waves.WaveEnded -= OnWaveEnded;
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (m_Exploded || amount <= 0)
                return;
            m_Left -= amount;
            if (m_Left <= 0)
                Explode();
        }

        /// <summary>Blows the prop up now.</summary>
        public void Explode()
        {
            if (m_Exploded)
                return;
            m_Exploded = true;
            SetPresent(false);
            Vector3 center = transform.position + Vector3.up * 0.5f;
            float radius = Radius;
            if (m_Explosion != null)
                ComicExplosion.Spawn(m_Explosion, transform.position, radius, m_FxSize * Mathf.Lerp(1f, 1.3f, Power / 3f));
            DamageZombies(center, radius);
            ChainNearby(radius);
            Exploded?.Invoke(this);
        }

        private void DamageZombies(Vector3 center, float radius)
        {
            m_Hit.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, s_Hits, Layers.Enemies, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var target = s_Hits[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || !m_Hit.Add(target))
                    continue;
                Vector3 away = s_Hits[i].transform.position - transform.position;
                away.y = 0f;
                float falloff = Mathf.Clamp01(away.magnitude / radius);
                int damage = Mathf.RoundToInt(m_Damage * PowerDamage * Mathf.Lerp(1f, m_EdgeDamage, falloff));
                target.TakeDamage(damage, away);
                if (target is ZombieController zombie && zombie.IsAlive)
                    zombie.Push(away.normalized * m_Push * (1f - falloff));
            }
        }

        private void ChainNearby(float radius)
        {
            foreach (ExplosiveProp other in s_All.ToArray())
            {
                if (other == this || !other.IsAlive)
                    continue;
                Vector3 d = other.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= radius)
                    other.StartCoroutine(other.ChainAfter(m_ChainDelay));
            }
        }

        private IEnumerator ChainAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Explode();
        }

        private void OnWaveEnded(int wave)
        {
            if (!m_Exploded)
                return;
            m_Exploded = false;
            m_Left = m_Health;
            SetPresent(true);
            m_RespawnStart = Time.time;
            m_Popping = true;
        }

        private void Start() => m_BaseScale = transform.localScale.x;

        // Back after a wave with a small pop.
        private void Update()
        {
            if (!m_Popping)
                return;
            float t = Mathf.Clamp01((Time.time - m_RespawnStart) / 0.35f);
            float pop = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
            transform.localScale = Vector3.one * (Mathf.Lerp(0.3f, 1f, t) * pop * m_BaseScale);
            if (t >= 1f)
            {
                transform.localScale = Vector3.one * m_BaseScale;
                m_Popping = false;
            }
        }

        private void SetPresent(bool present)
        {
            foreach (Renderer r in m_Renderers)
                r.enabled = present;
            foreach (Collider c in m_Colliders)
                c.enabled = present;
        }
    }
}
