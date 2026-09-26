using System;
using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Pooled projectile. Moves with a sphere cast each frame (no Rigidbody, no collider), stops at the first
    /// obstacle or after its range, then returns itself to the pool.
    /// </summary>
    public sealed class Bullet : MonoBehaviour
    {
        private Vector3 m_Direction;
        private float m_Speed;
        private float m_Remaining;
        private float m_Radius;
        private int m_BlockMask;
        private Action<Bullet> m_Release;
        private bool m_Active;

        public void Launch(Vector3 position, Vector3 direction, WeaponConfig config, int blockMask, Action<Bullet> release)
        {
            m_Direction = direction;
            m_Speed = config.BulletSpeed;
            m_Remaining = config.Range;
            m_Radius = config.BulletRadius;
            m_BlockMask = blockMask;
            m_Release = release;
            m_Active = true;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
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
