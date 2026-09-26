using UnityEngine;
using UnityEngine.Pool;

namespace ZombiCar.Player
{
    /// <summary>Object pool for bullets (UnityEngine.Pool). Pre-warmed so firing never instantiates.</summary>
    public sealed class BulletPool : MonoBehaviour
    {
        private const int c_MaxPoolSize = 128;
        private const string c_ContainerName = "[Bullets]";

        [SerializeField] private Bullet m_Prefab;

        private ObjectPool<Bullet> m_Pool;
        private Transform m_Container;   // scene-root parent: bullets must not move with the shooter

        public void Prewarm(int count)
        {
            EnsurePool(count);
            var buffer = new Bullet[count];
            for (int i = 0; i < count; i++)
                buffer[i] = m_Pool.Get();
            for (int i = 0; i < count; i++)
                m_Pool.Release(buffer[i]);
        }

        public Bullet Get()
        {
            EnsurePool(0);
            return m_Pool.Get();
        }

        public void Release(Bullet bullet) => m_Pool.Release(bullet);

        private void OnDestroy()
        {
            // Destroying the container removes every pooled bullet (they may already be gone on scene unload).
            if (m_Container != null)
                Destroy(m_Container.gameObject);
        }

        private void EnsurePool(int capacity)
        {
            if (m_Pool != null)
                return;
            m_Container = new GameObject(c_ContainerName).transform;
            m_Pool = new ObjectPool<Bullet>(
                createFunc: () => Instantiate(m_Prefab, m_Container),
                actionOnGet: b => b.gameObject.SetActive(true),
                actionOnRelease: b => b.gameObject.SetActive(false),
                actionOnDestroy: b => Destroy(b.gameObject),
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, capacity),
                maxSize: c_MaxPoolSize);
        }
    }
}
