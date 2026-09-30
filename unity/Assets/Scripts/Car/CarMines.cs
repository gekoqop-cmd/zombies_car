using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Progression;
using ZombiCar.Props;

namespace ZombiCar.Car
{
    /// <summary>
    /// The workshop's mines (<see cref="StatEffect.PlaceMines"/>, for a rewarded ad): taken between waves, they fill the
    /// free places of a ring of eight around the car (the corners first) and wait there; a zombie stepping close sets one off — the comic
    /// explosion, heavy damage falling off to the edge of the blast and a push. Publishes how many lie there to
    /// <see cref="RunState.ArmedMines"/> (the workshop does not sell more while all are still armed).
    /// </summary>
    public sealed class CarMines : MonoBehaviour
    {
        private static readonly Collider[] s_Hits = new Collider[64];

        [SerializeField] private RunState m_Run;
        [Tooltip("An inactive mine to copy.")]
        [SerializeField] private GameObject m_Template;
        [SerializeField, OptionalReference] private ComicExplosion m_Explosion;
        [Tooltip("Distance of the mines from the car's centre, metres.")]
        [SerializeField, Min(1f)] private float m_RingRadius = 3.6f;
        [Tooltip("A zombie this close sets a mine off, metres.")]
        [SerializeField, Min(0.2f)] private float m_TriggerRadius = 1.2f;
        [SerializeField, Min(0.5f)] private float m_BlastRadius = 3.5f;
        [SerializeField, Min(1)] private int m_Damage = 220;
        [Tooltip("Share of the damage left at the edge of the blast.")]
        [SerializeField, Range(0f, 1f)] private float m_EdgeDamage = 0.4f;
        [SerializeField, Min(0f)] private float m_Push = 1.6f;

        private readonly List<GameObject> m_Mines = new List<GameObject>();
        private readonly HashSet<IDamageable> m_Hit = new HashSet<IDamageable>();
        private readonly List<int> m_Slots = new List<int>();          // the ring place of each mine in m_Mines
        private const int c_Places = 8;

        public int Armed => m_Mines.Count;

        private void OnEnable() => m_Run.EffectApplied += OnEffectApplied;

        private void OnDisable() => m_Run.EffectApplied -= OnEffectApplied;

        private void OnEffectApplied(StatEffect effect, float value)
        {
            if (effect == StatEffect.PlaceMines)
                Place(Mathf.RoundToInt(value));
        }

        /// <summary>Lays up to <paramref name="count"/> mines on the free places around the car (workshop, tests).</summary>
        public void Place(int count)
        {
            for (int slot = 0; slot < c_Places && count > 0; slot++)
            {
                if (m_Slots.Contains(slot))
                    continue;                                            // still armed there
                count--;
                // The four corners, then between them.
                float angle = (slot < 4 ? 45f + slot * 90f : slot * 90f) * Mathf.Deg2Rad;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * m_RingRadius;
                at.y = transform.position.y;
                GameObject go = Instantiate(m_Template, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                go.name = $"Mine{m_Mines.Count + 1}";
                go.SetActive(true);
                m_Mines.Add(go);
                m_Slots.Add(slot);
            }
            m_Run.SetArmedMines(m_Mines.Count);
        }

        private void Update()
        {
            for (int i = m_Mines.Count - 1; i >= 0; i--)
            {
                if (ZombieNear(m_Mines[i].transform.position))
                    Explode(i);
            }
        }

        private bool ZombieNear(Vector3 at)
        {
            int count = Physics.OverlapSphereNonAlloc(at, m_TriggerRadius, s_Hits, Layers.Enemies, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var zombie = s_Hits[i].GetComponentInParent<ZombieController>();
                if (zombie != null && zombie.IsAlive)
                    return true;
            }
            return false;
        }

        private void Explode(int index)
        {
            Vector3 at = m_Mines[index].transform.position;
            Destroy(m_Mines[index]);
            m_Mines.RemoveAt(index);
            m_Slots.RemoveAt(index);
            m_Run.SetArmedMines(m_Mines.Count);
            if (m_Explosion != null)
                ComicExplosion.Spawn(m_Explosion, at, m_BlastRadius, 1f);
            m_Hit.Clear();
            int count = Physics.OverlapSphereNonAlloc(at + Vector3.up * 0.5f, m_BlastRadius, s_Hits, Layers.Enemies, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = s_Hits[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || !m_Hit.Add(target))
                    continue;
                Vector3 away = s_Hits[i].transform.position - at;
                away.y = 0f;
                float falloff = Mathf.Clamp01(away.magnitude / m_BlastRadius);
                target.TakeDamage(Mathf.RoundToInt(m_Damage * Mathf.Lerp(1f, m_EdgeDamage, falloff)), away);
                if (target is ZombieController zombie && zombie.IsAlive)
                    zombie.Push(away.normalized * m_Push * (1f - falloff));
            }
        }

        private void OnDestroy()
        {
            foreach (GameObject mine in m_Mines)
            {
                if (mine != null)
                    Destroy(mine);
            }
        }
    }
}
