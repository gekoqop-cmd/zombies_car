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
        [Tooltip("The equipped bullet skin is put on every pooled bullet once, when it is created.")]
        [SerializeField] private ZombiCar.Core.CosmeticCatalog m_Cosmetics;

        private ZombiCar.Core.CosmeticItem m_Skin;

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
            m_Skin = m_Cosmetics != null ? m_Cosmetics.Equipped(ZombiCar.Core.CosmeticSlot.Bullet) : null;
            m_Pool = new ObjectPool<Bullet>(
                createFunc: () =>
                {
                    Bullet b = Instantiate(m_Prefab, m_Container);
                    if (m_Skin != null && !m_Skin.IsDefault)
                        b.ApplySkin(m_Skin);
                    return b;
                },
                actionOnGet: b => b.gameObject.SetActive(true),
                actionOnRelease: b => b.gameObject.SetActive(false),
                actionOnDestroy: b =>
                {
                    if (b != null)                           // already gone with the scene (pools are reset on exit)
                        Destroy(b.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, capacity),
                maxSize: c_MaxPoolSize);
        }
    }
}
