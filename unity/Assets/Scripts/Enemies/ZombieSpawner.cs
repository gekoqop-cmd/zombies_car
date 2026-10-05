using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using Random = UnityEngine.Random;
using ZombiCar.Car;
using ZombiCar.Player;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// Spawns pooled zombies at the map's spawn points (children of <see cref="m_SpawnPoints"/>), away from the player,
    /// snapped to the NavMesh. Knows the car and the player the zombies go for and counts the living zombies.
    /// </summary>
    public sealed class ZombieSpawner : MonoBehaviour
    {
        private const string c_ContainerName = "[Zombies]";
        private const int c_MaxPoolSize = 64;
        private const int c_SpawnAttempts = 6;
        private const float c_NavMeshSnap = 2f;

        [SerializeField] private CarHealth m_Car;
        [SerializeField] private PlayerHealth m_Player;
        [SerializeField] private Transform m_SpawnPoints;
        [Tooltip("Spawn points closer to the player than this are skipped (unless all of them are).")]
        [SerializeField, Min(0f)] private float m_MinPlayerDistance = 7f;
        [Tooltip("Random offset around the spawn point, metres.")]
        [SerializeField, Min(0f)] private float m_SpawnJitter = 1.2f;

        private readonly Dictionary<ZombieConfig, ObjectPool<ZombieController>> m_Pools =
            new Dictionary<ZombieConfig, ObjectPool<ZombieController>>();
        private readonly List<Vector3> m_Candidates = new List<Vector3>();
        private Transform m_Container;   // scene-root parent for all zombies

        public CarHealth Car => m_Car;
        public PlayerHealth Player => m_Player;

        /// <summary>The run's difficulty on every zombie that comes out: its health ...</summary>
        public float HealthScale { get; set; } = 1f;

        /// <summary>... and the damage it deals.</summary>
        public float DamageScale { get; set; } = 1f;

        /// <summary>Zombies that are alive (rising, walking or fighting); corpses do not count.</summary>
        public int Alive { get; private set; }

        /// <summary>Zombies killed on this map so far.</summary>
        public int Killed { get; private set; }

        /// <summary>A zombie was killed (experience, scrap, kill counters).</summary>
        public event Action<ZombieController> ZombieDied;

        public void Prewarm(ZombieConfig config, int count)
        {
            ObjectPool<ZombieController> pool = PoolFor(config);
            var buffer = new ZombieController[count];
            for (int i = 0; i < count; i++)
                buffer[i] = pool.Get();
            for (int i = 0; i < count; i++)
                pool.Release(buffer[i]);
        }

        public bool TrySpawn(ZombieConfig config)
        {
            if (config == null || config.Prefab == null || !TryPickPoint(out Vector3 position))
                return false;
            ZombieController zombie = PoolFor(config).Get();
            zombie.Spawn(config, this, position);
            Alive++;
            return true;
        }

        /// <summary>
        /// Spawns <paramref name="config"/> at the NavMesh point nearest to <paramref name="position"/> (boss minions,
        /// tests). Returns the zombie, or null when there is no NavMesh close by.
        /// </summary>
        public ZombieController SpawnAt(ZombieConfig config, Vector3 position, float searchRadius = c_NavMeshSnap)
        {
            if (config == null || config.Prefab == null ||
                !NavMesh.SamplePosition(position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                return null;
            ZombieController zombie = PoolFor(config).Get();
            zombie.Spawn(config, this, hit.position);
            Alive++;
            return zombie;
        }

        /// <summary>Spawns <paramref name="config"/> at the spawn point farthest from the player (bosses: time to see it coming).</summary>
        public ZombieController SpawnFar(ZombieConfig config)
        {
            Vector3 player = m_Player != null ? m_Player.transform.position : Vector3.zero;
            Transform best = null;
            float bestSqr = -1f;
            foreach (Transform point in m_SpawnPoints)
            {
                Vector3 d = point.position - player;
                d.y = 0f;
                if (d.sqrMagnitude > bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = point;
                }
            }
            return best != null ? SpawnAt(config, best.position, 3f) : null;
        }

        public void NotifyDied(ZombieController zombie)
        {
            Alive = Mathf.Max(0, Alive - 1);
            Killed++;
            ZombieDied?.Invoke(zombie);
        }

        /// <summary>A zombie removed without a kill (developer cheats): no reward, no kill count.</summary>
        public void NotifyRemoved(ZombieController zombie) => Alive = Mathf.Max(0, Alive - 1);

        /// <summary>Called by a zombie when its corpse has sunk into the ground.</summary>
        public void Release(ZombieController zombie) => PoolFor(zombie.Config).Release(zombie);

        private void OnDestroy()
        {
            if (m_Container != null)
                Destroy(m_Container.gameObject);
        }

        private bool TryPickPoint(out Vector3 position)
        {
            m_Candidates.Clear();
            Vector3 player = m_Player != null ? m_Player.transform.position : new Vector3(float.MaxValue, 0f, 0f);
            float minSqr = m_MinPlayerDistance * m_MinPlayerDistance;
            foreach (Transform point in m_SpawnPoints)
            {
                Vector3 offset = point.position - player;
                offset.y = 0f;
                if (offset.sqrMagnitude >= minSqr)
                    m_Candidates.Add(point.position);
            }
            if (m_Candidates.Count == 0)
            {
                foreach (Transform point in m_SpawnPoints)
                    m_Candidates.Add(point.position);
            }

            for (int attempt = 0; attempt < c_SpawnAttempts && m_Candidates.Count > 0; attempt++)
            {
                Vector2 jitter = Random.insideUnitCircle * m_SpawnJitter;
                Vector3 p = m_Candidates[Random.Range(0, m_Candidates.Count)] + new Vector3(jitter.x, 0f, jitter.y);
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, c_NavMeshSnap, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }
            position = Vector3.zero;
            return false;
        }

        private ObjectPool<ZombieController> PoolFor(ZombieConfig config)
        {
            if (m_Pools.TryGetValue(config, out ObjectPool<ZombieController> pool))
                return pool;
            if (m_Container == null)
                m_Container = new GameObject(c_ContainerName).transform;
            pool = new ObjectPool<ZombieController>(
                createFunc: () => Instantiate(config.Prefab, m_Container),
                actionOnGet: z => z.gameObject.SetActive(true),
                actionOnRelease: z => z.gameObject.SetActive(false),
                actionOnDestroy: z =>
                {
                    if (z != null)                           // already gone with the scene (pools are reset on exit)
                        Destroy(z.gameObject);
                },
                collectionCheck: false,
                maxSize: c_MaxPoolSize);
            m_Pools.Add(config, pool);
            return pool;
        }
    }
}
