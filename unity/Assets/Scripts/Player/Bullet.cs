using System;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Pooled projectile (no Rigidbody, no collider). It is drawn at the barrel height and, every frame, sweeps a
    /// vertical capsule from <see cref="WeaponConfig.HitBandBottom"/> up to that height over the distance it covers
    /// (no tunnelling at any frame rate), so cover lower than the barrel and short zombies stop it as well.
    /// Stops at the first obstacle or enemy (damaging only enemies), or after its range, then returns to the pool.
    /// </summary>
    public sealed class Bullet : MonoBehaviour
    {
        private static readonly Collider[] s_Overlap = new Collider[4];

        private Vector3 m_Direction;
        private float m_Speed;
        private float m_Remaining;
        private float m_Radius;
        private float m_BandBottom;
        private int m_Damage;
        private int m_BlockMask;
        private Action<Bullet> m_Release;
        private bool m_Active;

        /// <summary>A bullet stopped: point, surface normal, and whether it hit an enemy (impact effects, sound).</summary>
        public static event Action<Vector3, Vector3, bool> Impacted;

        public void Launch(Vector3 position, Vector3 direction, WeaponConfig config, int blockMask, Action<Bullet> release)
        {
            direction.y = 0f;                      // bullets always fly level
            m_Direction = direction.normalized;
            m_Speed = config.BulletSpeed;
            m_Remaining = config.Range;
            m_Radius = config.BulletRadius;
            m_BandBottom = Mathf.Min(config.HitBandBottom, position.y);
            m_Damage = config.Damage;
            m_BlockMask = blockMask;
            m_Release = release;
            m_Active = true;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(m_Direction, Vector3.up));

            // Fired from inside cover (the player pressed against a wall or the car): a cast would start inside
            // the collider and miss it, so check the start position first.
            Band(position, out Vector3 bottom, out Vector3 top);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, m_Radius, s_Overlap, m_BlockMask, QueryTriggerInteraction.Ignore);
            if (count > 0)
                Stop(s_Overlap[0], position, -m_Direction);
        }

        private void Update()
        {
            if (!m_Active)
                return;

            float step = Mathf.Min(m_Speed * Time.deltaTime, m_Remaining);
            Band(transform.position, out Vector3 bottom, out Vector3 top);
            if (Physics.CapsuleCast(bottom, top, m_Radius, m_Direction, out RaycastHit hit, step, m_BlockMask,
                    QueryTriggerInteraction.Ignore))
            {
                // The visible bullet ends where the sweep touched, at its own height.
                Vector3 end = transform.position + m_Direction * hit.distance;
                Stop(hit.collider, new Vector3(hit.point.x, end.y, hit.point.z), hit.normal);
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

        private void Stop(Collider collider, Vector3 point, Vector3 normal)
        {
            transform.position = point;
            bool enemy = ((1 << collider.gameObject.layer) & Layers.Enemies) != 0;
            Impacted?.Invoke(point, normal, enemy);
            if (enemy && collider.TryGetComponent(out IDamageable target))
                target.TakeDamage(m_Damage, m_Direction);
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
