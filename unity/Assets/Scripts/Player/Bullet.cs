using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>Per-shot numbers of a bullet: the gun's config plus the run's cards (damage, range, pierce, crits).</summary>
    public struct ShotStats
    {
        public int Damage;
        public float Range;
        /// <summary>Enemies the bullet passes through before it stops.</summary>
        public int Pierce;
        /// <summary>Chance 0..1 of a critical hit (double damage).</summary>
        public float CritChance;
    }

    /// <summary>
    /// Pooled projectile (no Rigidbody, no collider). It is drawn at the barrel height and, every frame, sweeps a
    /// vertical capsule from <see cref="WeaponConfig.HitBandBottom"/> up to that height over the distance it covers
    /// (no tunnelling at any frame rate), so cover lower than the barrel and short zombies stop it as well.
    /// Enemies are hit through their (larger) hit boxes; a piercing bullet goes on through as many enemies as it may.
    /// Stops at the first obstacle, the car or the last enemy it may hit (damaging enemies and shootable props such as
    /// explosive barrels), or after its range, then returns to the pool.
    /// </summary>
    public sealed class Bullet : MonoBehaviour
    {
        private const int c_MaxHits = 16;
        private static readonly Collider[] s_Overlap = new Collider[8];
        private static readonly RaycastHit[] s_Hits = new RaycastHit[c_MaxHits];
        private static readonly IComparer<RaycastHit> s_ByDistance =
            Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        // Unlit tracer colours are HDR (over 1) so the bloom picks them up, like the default M_Bullet tint.
        private const float c_SkinGlow = 3f;
        // The outline glows a little (the bloom gives it a soft halo), less than a tracer.
        private const float c_OutlineGlow = 1.35f;

        [Tooltip("The tracer mesh (skins recolour and resize it).")]
        [SerializeField] private Renderer m_Visual;
        [Tooltip("Glowing trail, off unless a bullet skin has one.")]
        [SerializeField] private TrailRenderer m_Trail;
        [Tooltip("A shell a little larger than the tracer, drawn from the inside: the glowing outline of some skins.")]
        [SerializeField, OptionalReference] private Renderer m_Outline;

        private readonly List<IDamageable> m_AlreadyHit = new List<IDamageable>();
        private Vector3 m_Direction;
        private float m_Speed;
        private float m_Remaining;
        private float m_Radius;
        private float m_BandBottom;
        private int m_Damage;
        private int m_PierceLeft;
        private int m_BlockMask;
        private Action<Bullet> m_Release;
        private bool m_Active;

        /// <summary>A bullet stopped: point, surface normal, and whether it hit an enemy (impact effects, sound).</summary>
        public static event Action<Vector3, Vector3, bool> Impacted;

        /// <summary>
        /// Puts a bullet skin on this pooled bullet: tracer colour and size, a trail in its second colour, and for some
        /// (the black bullets) a glowing outline in the second colour around the tracer.
        /// </summary>
        public void ApplySkin(CosmeticItem skin)
        {
            if (m_Visual != null)
            {
                var block = new MaterialPropertyBlock();
                Color c = skin.Color * c_SkinGlow;
                c.a = 1f;
                block.SetColor(s_BaseColor, c);
                m_Visual.SetPropertyBlock(block);
                m_Visual.transform.localScale *= skin.Size;
            }
            if (m_Outline != null)
            {
                m_Outline.enabled = skin.Outline;
                if (skin.Outline)
                {
                    var block = new MaterialPropertyBlock();
                    Color rim = skin.Color2 * c_OutlineGlow;
                    rim.a = 1f;
                    block.SetColor(s_BaseColor, rim);
                    m_Outline.SetPropertyBlock(block);
                }
            }
            if (m_Trail == null)
                return;
            m_Trail.enabled = skin.Trail;
            if (!skin.Trail)
                return;
            Color head = skin.Color2;
            head.a = 0.85f;
            Color tail = skin.Color2;
            tail.a = 0f;
            m_Trail.startColor = head;
            m_Trail.endColor = tail;
            m_Trail.widthMultiplier = 0.12f * skin.Size;
        }

        public void Launch(Vector3 position, Vector3 direction, WeaponConfig config, ShotStats stats, int blockMask,
            Action<Bullet> release)
        {
            direction.y = 0f;                      // bullets always fly level
            m_Direction = direction.normalized;
            m_Speed = config.BulletSpeed;
            m_Remaining = stats.Range;
            m_Radius = config.BulletRadius;
            m_BandBottom = Mathf.Min(config.HitBandBottom, position.y);
            bool crit = stats.CritChance > 0f && UnityEngine.Random.value < stats.CritChance;
            m_Damage = crit ? stats.Damage * 2 : stats.Damage;
            m_PierceLeft = Mathf.Max(0, stats.Pierce);
            m_BlockMask = blockMask;
            m_Release = release;
            m_Active = true;
            m_AlreadyHit.Clear();
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(m_Direction, Vector3.up));
            if (m_Trail != null && m_Trail.enabled)
                m_Trail.Clear();                   // a pooled bullet must not streak from where it last stopped

            // Fired from inside cover (the player pressed against a wall or the car) or point blank into a zombie:
            // a cast would start inside the collider and miss it, so check the start position first.
            Band(position, out Vector3 bottom, out Vector3 top);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, m_Radius, s_Overlap, m_BlockMask, QueryTriggerInteraction.Collide);
            // Prop hit boxes (triggers) are skipped here: standing next to a barrel must not blow it up with every shot.
            for (int i = 0; i < count && m_Active; i++)
                Hit(s_Overlap[i], position, -m_Direction, false);
        }

        private void Update()
        {
            if (!m_Active)
                return;

            float step = Mathf.Min(m_Speed * Time.deltaTime, m_Remaining);
            Band(transform.position, out Vector3 bottom, out Vector3 top);
            int count = Physics.CapsuleCastNonAlloc(bottom, top, m_Radius, m_Direction, s_Hits, step, m_BlockMask,
                QueryTriggerInteraction.Collide);
            if (count > 0)
            {
                Array.Sort(s_Hits, 0, count, s_ByDistance);
                for (int i = 0; i < count && m_Active; i++)
                {
                    RaycastHit hit = s_Hits[i];
                    // The visible bullet ends where the sweep touched, at its own height (a cast that starts
                    // touching a collider reports distance 0 and no point: use the bullet's position).
                    Vector3 end = transform.position + m_Direction * hit.distance;
                    Vector3 point = hit.distance > 0f ? new Vector3(hit.point.x, end.y, hit.point.z) : end;
                    Hit(hit.collider, point, hit.normal, true);
                }
                if (!m_Active)
                    return;
            }

            transform.position += m_Direction * step;
            m_Remaining -= step;
            if (m_Remaining <= 0f)
                Despawn();
        }

        private void Band(Vector3 position, out Vector3 bottom, out Vector3 top)
        {
            top = position;
            bottom = new Vector3(position.x, Mathf.Min(m_BandBottom + m_Radius, position.y), position.z);
        }

        // Enemies take damage and may let the bullet through; anything else stops it (shootable props take damage,
        // also through their wider trigger hit box when <paramref name="propHitboxes"/>).
        private void Hit(Collider collider, Vector3 point, Vector3 normal, bool propHitboxes)
        {
            bool enemy = ((1 << collider.gameObject.layer) & Layers.Enemies) != 0;
            var target = collider.GetComponentInParent<IDamageable>();
            if (enemy)
            {
                if (target == null || m_AlreadyHit.Contains(target) || !target.IsAlive)
                    return;                                          // second collider of a zombie already hit, or a corpse
                m_AlreadyHit.Add(target);
                Impacted?.Invoke(point, normal, true);
                target.TakeDamage(m_Damage, m_Direction);
                if (m_PierceLeft-- > 0)
                    return;
                Stop(point);
                return;
            }
            if (collider.isTrigger && !(propHitboxes && target is IShootable shootableProp && shootableProp.IsAlive))
                return;                                              // other triggers never stop bullets
            Impacted?.Invoke(point, normal, false);
            if (target is IShootable shootable)
                shootable.TakeDamage(m_Damage, m_Direction);
            Stop(point);
        }

        private void Stop(Vector3 point)
        {
            transform.position = point;
            Despawn();
        }

        private void Despawn()
        {
            if (!m_Active)
                return;
            m_Active = false;
            m_Release?.Invoke(this);
        }
    }
}
