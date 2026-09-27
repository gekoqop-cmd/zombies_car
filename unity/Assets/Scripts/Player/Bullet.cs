using System;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Pooled projectile. Moves with a sphere cast each frame (no Rigidbody, no collider), stops at the first
    /// obstacle or enemy (damaging the enemy) or after its range, then returns itself to the pool.
    /// </summary>
    public sealed class Bullet : MonoBehaviour
    {
        private Vector3 m_Direction;
        private float m_Speed;
        private float m_Remaining;
        private float m_Radius;
        private int m_Damage;
        private int m_BlockMask;
        private Action<Bullet> m_Release;
        private bool m_Active;

        /// <summary>A bullet stopped at a surface: point, surface normal (impact effects).</summary>
        public static event Action<Vector3, Vector3> Impacted;

        public void Launch(Vector3 position, Vector3 direction, WeaponConfig config, int blockMask, Action<Bullet> release)
        {
            direction.y = 0f;                      // bullets always fly level at barrel height
            m_Direction = direction.normalized;
            m_Speed = config.BulletSpeed;
            m_Remaining = config.Range;
            m_Radius = config.BulletRadius;
            m_Damage = config.Damage;
            m_BlockMask = blockMask;
            m_Release = release;
            m_Active = true;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(m_Direction, Vector3.up));
        }

        private void Update()
        {
            if (!m_Active)
                return;

            float step = Mathf.Min(m_Speed * Time.deltaTime, m_Remaining);
            if (Physics.SphereCast(transform.position, m_Radius, m_Direction, out RaycastHit hit, step, m_BlockMask,
                    QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                Impacted?.Invoke(hit.point, hit.normal);
                bool enemy = ((1 << hit.collider.gameObject.layer) & Layers.Enemies) != 0;
                if (enemy && hit.collider.TryGetComponent(out IDamageable target))
                    target.TakeDamage(m_Damage, m_Direction);
                Despawn();
                return;
            }

            transform.position += m_Direction * step;
            m_Remaining -= step;
            if (m_Remaining <= 0f)
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
