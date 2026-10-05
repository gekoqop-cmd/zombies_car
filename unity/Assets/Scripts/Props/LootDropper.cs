using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.Waves;

namespace ZombiCar.Props
{
    /// <summary>
    /// Pickups on the ground of a map. A boss fight sheds ammo boxes: every <see cref="m_AmmoStep"/> of the boss's
    /// health it loses, a box is tossed out next to it, so the boss can be finished with the gun. A boss that dies
    /// before the last wave scatters its scrap reward in pieces around it (<see cref="Scatter"/>). Pieces fly out in an
    /// arc, lie bobbing with gold rays behind them, and are taken by walking over them (an ammo box stays while the gun
    /// is full): each flies into the player, then the scrap's icon flies on to the HUD counter. When the next wave
    /// starts, whatever still lies there flies to the player by itself; when the run ends, the scrap left is counted.
    /// </summary>
    public sealed class LootDropper : MonoBehaviour
    {
        private sealed class Piece
        {
            public Transform Root;
            public SpriteRenderer Sprite;
            public Transform Rays;
            public LootKind Kind;
            public int Amount;
            public Vector3 From, To;
            public float Born;
            public float Phase;
            public bool Flying;
            public float FlyStart;
            public Vector3 FlyFrom;
            public bool Active;
        }

        [SerializeField] private RunState m_Run;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private WaveDirector m_Director;
        [SerializeField] private Transform m_Player;
        [SerializeField] private PlayerWeapon m_Weapon;
        [Tooltip("An inactive pickup: a billboard root with the sprite and the rays behind it (copied for every piece).")]
        [SerializeField] private Transform m_Template;
        [SerializeField] private Sprite m_ScrapSprite;
        [SerializeField] private Sprite m_AmmoSprite;

        [Header("Boss scrap")]
        [Tooltip("Pieces a boss's scrap reward is split into.")]
        [SerializeField, Min(1)] private int m_ScrapPieces = 12;
        [SerializeField, Min(0f)] private float m_ScatterMin = 1.6f;
        [SerializeField, Min(0f)] private float m_ScatterMax = 4.6f;

        [Header("Boss ammo")]
        [Tooltip("Share of the boss's health between two ammo boxes.")]
        [SerializeField, Range(0.02f, 0.5f)] private float m_AmmoStep = 0.1f;
        [Tooltip("Rounds in one box (into the reserve, up to its maximum).")]
        [SerializeField, Min(1)] private int m_AmmoPerBox = 35;
        [Tooltip("How far from the boss, towards the player, a box lands.")]
        [SerializeField, Min(0f)] private float m_AmmoToss = 2.6f;

        [Header("Pieces")]
        [SerializeField, Min(0.1f)] private float m_PickupRadius = 1.3f;
        [SerializeField, Min(0.05f)] private float m_ThrowTime = 0.55f;
        [SerializeField, Min(0f)] private float m_ThrowHeight = 1.8f;
        [SerializeField, Min(0.05f)] private float m_FlyTime = 0.22f;
        [SerializeField, Min(0f)] private float m_Hover = 0.35f;

        private readonly List<Piece> m_Pieces = new List<Piece>();
        private ZombieController m_TrackedBoss;
        private float m_NextAmmoAt;

        /// <summary>Pieces lying on the ground or flying to the player now (tests).</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (Piece p in m_Pieces)
                    n += p.Active ? 1 : 0;
                return n;
            }
        }

        /// <summary>The scrap still lying on the ground (a checkpoint saved now counts it as picked up).</summary>
        public int PendingScrap
        {
            get
            {
                int sum = 0;
                foreach (Piece p in m_Pieces)
                    sum += p.Active && p.Kind == LootKind.Scrap ? p.Amount : 0;
                return sum;
            }
        }

        /// <summary>Pieces of <paramref name="kind"/> lying or flying now (tests).</summary>
        public int CountOf(LootKind kind)
        {
            int n = 0;
            foreach (Piece p in m_Pieces)
                n += p.Active && p.Kind == kind ? 1 : 0;
            return n;
        }

        /// <summary>Where the pieces of <paramref name="kind"/> lie (tests).</summary>
        public IEnumerable<Vector3> PositionsOf(LootKind kind)
        {
            foreach (Piece p in m_Pieces)
                if (p.Active && p.Kind == kind)
                    yield return p.To;
        }

        private void OnEnable()
        {
            if (m_Waves != null)
                m_Waves.WaveStarted += OnWaveStarted;
            if (m_Run != null)
                m_Run.OutcomeChanged += OnOutcome;
        }

        private void OnDisable()
        {
            if (m_Waves != null)
                m_Waves.WaveStarted -= OnWaveStarted;
            if (m_Run != null)
                m_Run.OutcomeChanged -= OnOutcome;
        }

        /// <summary>
        /// A boss's scrap reward scattered round <paramref name="at"/> in pieces; false (nothing dropped: the caller
        /// pays it at once) on the last wave, where the run ends with the boss.
        /// </summary>
        public bool Scatter(Vector3 at, int total)
        {
            if (total <= 0 || m_Template == null || m_Waves == null || m_Waves.Wave >= m_Waves.WaveCount)
                return false;
            int pieces = Mathf.Clamp(m_ScrapPieces, 1, total);
            int each = total / pieces, rest = total - each * pieces;
            for (int i = 0; i < pieces; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) / pieces * Mathf.PI * 2f;
                float distance = Random.Range(m_ScatterMin, m_ScatterMax);
                Vector3 to = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Spawn(LootKind.Scrap, each + (i < rest ? 1 : 0), at, to, i * 0.04f);
            }
            return true;
        }

        /// <summary>An ammo box tossed from <paramref name="at"/> towards the player (the boss fight; tests).</summary>
        public void DropAmmo(Vector3 at)
        {
            Vector3 toPlayer = m_Player != null ? m_Player.position - at : Vector3.forward;
            toPlayer.y = 0f;
            Vector3 dir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x) * Random.Range(-1.2f, 1.2f);
            Spawn(LootKind.Ammo, m_AmmoPerBox, at, at + dir * m_AmmoToss + side, 0f);
        }

        private void Spawn(LootKind kind, int amount, Vector3 from, Vector3 to, float delay)
        {
            if (amount <= 0 || m_Template == null)
                return;
            // On walkable ground: never inside the car, a wall or off the lot.
            if (NavMesh.SamplePosition(to, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                to = hit.position;
            to.y = 0f;
            Piece piece = Take();
            piece.Kind = kind;
            piece.Amount = amount;
            piece.From = new Vector3(from.x, 0.8f, from.z);
            piece.To = to;
            piece.Born = Time.time + delay;
            piece.Phase = Random.value * 6f;
            piece.Flying = false;
            piece.Active = true;
            piece.Sprite.sprite = kind == LootKind.Ammo ? m_AmmoSprite : m_ScrapSprite;
            piece.Root.position = piece.From;
            piece.Root.gameObject.SetActive(true);
        }

        private Piece Take()
        {
            foreach (Piece p in m_Pieces)
                if (!p.Active)
                    return p;
            Transform root = Instantiate(m_Template, transform);
            root.name = "Loot";
            var piece = new Piece { Root = root, Sprite = root.Find("Sprite").GetComponent<SpriteRenderer>(), Rays = root.Find("Rays") };
            m_Pieces.Add(piece);
            return piece;
        }

        private void Update()
        {
            ShedAmmo();
            float now = Time.time;
            Vector3 player = m_Player != null ? m_Player.position : Vector3.zero;
            foreach (Piece p in m_Pieces)
            {
                if (!p.Active)
                    continue;
                if (p.Flying)
                {
                    float f = Mathf.Clamp01((now - p.FlyStart) / m_FlyTime);
                    p.Root.position = Vector3.Lerp(p.FlyFrom, player + Vector3.up, f * f);
                    p.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, f);
                    if (f >= 1f)
                        Collect(p);
                    continue;
                }
                float t = (now - p.Born) / m_ThrowTime;
                if (t < 0f)
                {
                    p.Root.position = p.From;
                    continue;
                }
                if (t < 1f)
                {
                    Vector3 flat = Vector3.Lerp(p.From, p.To, t);
                    flat.y = Mathf.Lerp(p.From.y, m_Hover, t) + Mathf.Sin(t * Mathf.PI) * m_ThrowHeight;
                    p.Root.position = flat;
                    p.Root.localScale = Vector3.one;
                    continue;
                }
                float bob = Mathf.Sin(now * 3.2f + p.Phase);
                p.Root.position = p.To + Vector3.up * (m_Hover + 0.08f * bob);
                p.Root.localScale = Vector3.one * (1f + 0.05f * bob);
                if (p.Rays != null)
                    p.Rays.localRotation = Quaternion.Euler(0f, 0f, now * 60f + p.Phase * 30f);
                Vector3 d = player - p.To;
                d.y = 0f;
                bool wanted = p.Kind != LootKind.Ammo || m_Weapon == null || !m_Weapon.IsFull;
                if (m_Player != null && wanted && d.sqrMagnitude <= m_PickupRadius * m_PickupRadius && !(m_Run != null && m_Run.IsOver))
                    StartFlying(p);
            }
        }

        // Every step of the boss's health lost tosses a box out; a new boss starts its own count.
        private void ShedAmmo()
        {
            ZombieController boss = m_Director != null ? m_Director.Boss : null;
            if (boss != m_TrackedBoss)
            {
                m_TrackedBoss = boss;
                m_NextAmmoAt = 1f - m_AmmoStep;
            }
            if (boss == null || boss.MaxHealth <= 0)
                return;
            float share = (float)boss.Health / boss.MaxHealth;
            while (share <= m_NextAmmoAt && m_NextAmmoAt > 0.001f)
            {
                DropAmmo(boss.transform.position);
                m_NextAmmoAt -= m_AmmoStep;
            }
        }

        private void StartFlying(Piece p)
        {
            p.Flying = true;
            p.FlyStart = Time.time;
            p.FlyFrom = p.Root.position;
        }

        private void Collect(Piece p)
        {
            p.Active = false;
            p.Root.gameObject.SetActive(false);
            Vector3 at = p.Root.position;
            if (p.Kind == LootKind.Scrap)
            {
                m_Run?.AddLootMoney(p.Amount, at);
                AudioManager.PlaySfx(Sound.UiBuy);
            }
            else
            {
                if (m_Weapon != null)
                    m_Weapon.Resupply(p.Amount);
                m_Run?.ReportLoot(LootKind.Ammo, p.Amount, at);
                AudioManager.PlaySfx(Sound.SupplyAmmoBox);
            }
        }

        // The next wave: what is left flies to the player by itself.
        private void OnWaveStarted(int wave)
        {
            foreach (Piece p in m_Pieces)
                if (p.Active && !p.Flying)
                    StartFlying(p);
        }

        // The run is over: the scrap still lying there is counted at once (the coins at the end come from it).
        private void OnOutcome()
        {
            if (m_Run == null || !m_Run.IsOver)
                return;
            foreach (Piece p in m_Pieces)
            {
                if (!p.Active)
                    continue;
                p.Active = false;
                p.Root.gameObject.SetActive(false);
                if (p.Kind == LootKind.Scrap)
                    m_Run.AddMoney(p.Amount);
            }
        }
    }
}
