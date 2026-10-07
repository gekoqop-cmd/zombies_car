using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombiCar.Car;
using ZombiCar.Characters;
using ZombiCar.Core;
using ZombiCar.Player;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// One zombie: rises out of the ground at its spawn point, walks on the NavMesh to the car (or to the player once
    /// the player is close; hunters always go for the player), hits in melee, staggers when shot and dies: a regular zombie
    /// bursts into pieces (<see cref="ZombieGibs"/>), a boss falls and sinks back into the ground; then it returns to the
    /// <see cref="ZombieSpawner"/> pool. The body is a 2D sprite driven by
    /// <see cref="SpriteAnimator"/>; the root never rotates. Bosses (<see cref="BossBrain"/>) take it over for their
    /// abilities through <see cref="BeginAction"/> / <see cref="EndAction"/> and scale its speed, damage and the damage
    /// it takes.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class ZombieController : MonoBehaviour, IDamageable
    {
        private const float c_RepathInterval = 0.3f;
        private const float c_RiseTime = 0.9f;
        private const float c_CorpseTime = 2.5f;
        private const float c_SinkTime = 1f;
        private const float c_SinkDepth = 0.9f;
        private const float c_GibsLieTime = 1.6f;          // burst zombies: the pieces lie this long, then fade
        private const float c_GibsFadeTime = 0.6f;
        private const float c_MoveAnimMinSpeed = 0.15f;
        private const float c_StaggerImmunity = 0.5f;      // after a stagger, hits only flash for this long (no stun-lock)
        private const float c_StrikeReachSlack = 1.3f;     // the target may step back a little during the wind-up
        private const float c_AggroRelease = 1.5f;
        private const float c_HitFlashTime = 0.15f;
        private static readonly Color s_HitFlash = new Color(1f, 0.3f, 0.25f, 1f);

        private enum State { Rising, Chasing, Attacking, Staggered, Busy, Dead }

        [SerializeField] private SpriteAnimator m_Animator;
        [SerializeField] private Transform m_Shadow;
        [Tooltip("Share of the attack clip after which the hit lands (the strike frame).")]
        [SerializeField, Range(0f, 1f)] private float m_StrikeMoment = 0.5f;
        [Tooltip("Larger trigger around the body that only bullets use (near misses still hit); on and off with the body.")]
        [SerializeField] private Collider m_ShotHitbox;
        [Tooltip("Height of the sprite, metres: rising starts this deep under the ground.")]
        [SerializeField, Min(0.1f)] private float m_VisualHeight = 2.5f;
        [Tooltip("Regular zombies burst into pieces when they die (bosses fall and sink instead).")]
        [SerializeField, OptionalReference] private ZombieGibs m_Gibs;

        private NavMeshAgent m_Agent;
        private CapsuleCollider m_Collider;
        private ZombieConfig m_Config;
        private ZombieSpawner m_Owner;
        private Vector3 m_ShadowScale;
        private State m_State = State.Dead;
        private float m_StateTime;
        private int m_Health;
        private int m_MaxHealth;
        private bool m_Silent;                    // removed by a cheat: no kill reward
        private float m_NextRepath;
        private float m_NextAttack;
        private float m_StaggerAllowedAt;
        private bool m_Struck;
        private bool m_ChasingPlayer;
        private float m_BaseSpeed;
        private float m_SpeedScale = 1f;
        private Vector3 m_LastHit;

        private static readonly List<ZombieController> s_Active = new List<ZombieController>();

        /// <summary>Zombies out on the map (alive, or dead and still sinking): what may be on screen.</summary>
        public static IReadOnlyList<ZombieController> Active => s_Active;

        public ZombieConfig Config => m_Config;
        public bool IsAlive => m_State != State.Dead;
        public int Health => m_Health;
        public int MaxHealth => m_MaxHealth > 0 ? m_MaxHealth : m_Config != null ? m_Config.MaxHealth : 1;
        public NavMeshAgent Agent => m_Agent;
        public SpriteAnimator Animator => m_Animator;
        public ZombieSpawner Owner => m_Owner;
        public float VisualHeight => m_VisualHeight;

        /// <summary>Walking and fighting normally: an ability may start (<see cref="BeginAction"/>).</summary>
        public bool CanAct => m_State == State.Chasing;

        /// <summary>An ability drives the zombie (it neither walks nor attacks by itself).</summary>
        public bool IsBusy => m_State == State.Busy;

        /// <summary>Multiplies the walking speed (boss enrage).</summary>
        public float SpeedScale
        {
            get => m_SpeedScale;
            set
            {
                m_SpeedScale = Mathf.Max(0.05f, value);
                if (m_Agent.enabled)
                    m_Agent.speed = m_BaseSpeed * m_SpeedScale;
            }
        }

        /// <summary>Multiplies the melee damage.</summary>
        public float DamageScale { get; set; } = 1f;

        /// <summary>Multiplies the damage taken (a stunned boss takes more).</summary>
        public float DamageTakenScale { get; set; } = 1f;

        /// <summary>A pooled zombie came out (again).</summary>
        public event Action<ZombieController> Spawned;

        /// <summary>A hit landed on this zombie (the damage after scaling).</summary>
        public event Action<ZombieController, int> Damaged;

        /// <summary>This zombie's melee strike landed on its target.</summary>
        public event Action<ZombieController> Struck;

        /// <summary>This zombie died.</summary>
        public event Action<ZombieController> Died;

        private void OnEnable() => s_Active.Add(this);

        private void OnDisable() => s_Active.Remove(this);

        private void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            m_Collider = GetComponent<CapsuleCollider>();
            m_Agent.updateRotation = false;
            m_ShadowScale = m_Shadow.localScale;
        }

        /// <summary>Starts a pooled zombie at <paramref name="position"/> (a point on the NavMesh).</summary>
        public void Spawn(ZombieConfig config, ZombieSpawner owner, Vector3 position)
        {
            m_Config = config;
            m_Owner = owner;
            m_MaxHealth = Mathf.Max(1, Mathf.RoundToInt(config.MaxHealth * owner.HealthScale));   // the run's difficulty
            m_Health = m_MaxHealth;
            m_Silent = false;
            m_ChasingPlayer = false;
            m_NextAttack = 0f;
            m_StaggerAllowedAt = 0f;

            transform.SetPositionAndRotation(position, Quaternion.identity);
            m_Agent.enabled = true;
            m_Agent.Warp(position);
            m_BaseSpeed = config.MoveSpeed * UnityEngine.Random.Range(1f - config.SpeedVariation, 1f + config.SpeedVariation);
            m_SpeedScale = 1f;
            DamageScale = owner.DamageScale;
            DamageTakenScale = 1f;
            m_Agent.speed = m_BaseSpeed;
            m_Agent.avoidancePriority = config.IsBoss ? 10 : UnityEngine.Random.Range(30, 70);
            m_Agent.isStopped = true;
            SetShootable(false);                  // not shootable while still in the ground

            m_Animator.ResetState();
            m_LastHit = Vector3.zero;
            if (m_Gibs != null)
                m_Gibs.Hide();
            if (owner.Car != null)
                m_Animator.SetFacing(owner.Car.transform.position.x - position.x);
            m_Animator.SetLift(-m_VisualHeight);
            m_Shadow.localScale = Vector3.zero;
            Enter(State.Rising);
            Spawned?.Invoke(this);
        }

        /// <summary>
        /// Stops the zombie for an ability (from walking only: not while rising, attacking, staggered or dead) and plays
        /// <paramref name="anim"/>. The ability ends it with <see cref="EndAction"/>.
        /// </summary>
        public bool BeginAction(CharacterAnim anim, float animSpeed = 1f)
        {
            if (!CanAct)
                return false;
            Enter(State.Busy);
            m_Animator.Play(anim, animSpeed, true);
            return true;
        }

        public void EndAction()
        {
            if (m_State == State.Busy)
                Enter(State.Chasing);
        }

        // The body collider and the shot hit box: off while rising from the ground and once dead.
        private void SetShootable(bool on)
        {
            m_Collider.enabled = on;
            m_ShotHitbox.enabled = on;
        }

        /// <summary>Pushes the zombie along the NavMesh (explosions, the axe).</summary>
        public void Push(Vector3 displacement)
        {
            if (m_State == State.Dead || !m_Agent.enabled || (m_Config != null && m_Config.IsBoss))
                return;
            displacement.y = 0f;
            m_Agent.Move(displacement);
        }

        /// <summary>
        /// Kills the zombie now in any state, also while it still rises from the ground (developer cheats). Without
        /// <paramref name="rewarded"/> it is only removed: no kill, scrap or experience.
        /// </summary>
        public void Kill(bool rewarded)
        {
            if (m_State == State.Dead)
                return;
            m_Health = 0;
            m_Silent = !rewarded;
            Enter(State.Dead);
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (m_State == State.Dead || m_State == State.Rising)
                return;
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * DamageTakenScale));
            m_LastHit = direction;
            m_Health -= amount;
            m_Animator.Flash(s_HitFlash, c_HitFlashTime);
            m_Animator.Kick(direction);
            Damaged?.Invoke(this, amount);
            if (m_Health <= 0)
            {
                Enter(State.Dead);
                return;
            }
            if (m_State == State.Busy)
                return;                                                // abilities are not interrupted
            if (m_Config.Knockback > 0f && direction.sqrMagnitude > 1e-4f)
            {
                direction.y = 0f;
                m_Agent.Move(direction.normalized * m_Config.Knockback);
            }
            if (m_Config.StaggerTime > 0f && Time.time >= m_StaggerAllowedAt)
                Enter(State.Staggered);
        }

        private void Update()
        {
            m_StateTime += Time.deltaTime;
            switch (m_State)
            {
                case State.Rising: UpdateRising(); break;
                case State.Chasing: UpdateChasing(); break;
                case State.Attacking: UpdateAttacking(); break;
                case State.Staggered:
                    if (m_StateTime >= m_Config.StaggerTime)
                        Enter(State.Chasing);
                    break;
                case State.Dead: UpdateDead(); break;
            }
        }

        private void Enter(State state)
        {
            m_State = state;
            m_StateTime = 0f;
            switch (state)
            {
                case State.Chasing:
                    m_NextRepath = 0f;
                    break;
                case State.Attacking:
                    Stop();
                    m_Struck = false;
                    m_NextAttack = Time.time + m_Config.AttackInterval;
                    m_Animator.Play(CharacterAnim.Attack, m_Config.AttackAnimSpeed, true);
                    break;
                case State.Busy:
                    Stop();                       // an ability (a charge moves it itself; a stunned boss stays put)
                    break;
                case State.Staggered:
                    Stop();
                    m_StaggerAllowedAt = Time.time + m_Config.StaggerTime + c_StaggerImmunity;
                    m_Animator.Play(CharacterAnim.Hit, 1f, true);
                    break;
                case State.Dead:
                    m_Agent.enabled = false;      // corpses are not in the crowd avoidance
                    SetShootable(false);
                    m_Animator.Play(CharacterAnim.Dead, 1f, true);
                    if (m_Gibs != null)
                    {
                        m_Animator.SetAlpha(0f);
                        m_Shadow.localScale = Vector3.zero;
                        m_Gibs.Burst(m_LastHit, m_Animator.Facing);
                    }
                    if (m_Silent)
                        m_Owner.NotifyRemoved(this);
                    else
                        m_Owner.NotifyDied(this);
                    Died?.Invoke(this);
                    break;
            }
        }

        private void UpdateRising()
        {
            float t = Mathf.Clamp01(m_StateTime / c_RiseTime);
            float eased = 1f - (1f - t) * (1f - t);
            m_Animator.SetLift(-m_VisualHeight * (1f - eased));
            m_Animator.SetRising(t);
            m_Shadow.localScale = m_ShadowScale * eased;
            if (t < 1f)
                return;
            SetShootable(true);
            Enter(State.Chasing);
        }

        private void UpdateChasing()
        {
            if (!FindTarget(out IDamageable target, out Vector3 point, out float gap))
            {
                Stop();
                m_Animator.Play(CharacterAnim.Idle);
                return;
            }
            if (gap <= m_Config.AttackRange)
            {
                m_Animator.SetFacing(point.x - transform.position.x);
                if (Time.time >= m_NextAttack)
                {
                    Enter(State.Attacking);
                    return;
                }
                Stop();
                m_Animator.Play(CharacterAnim.Idle);
                return;
            }

            m_Agent.isStopped = false;
            if (Time.time >= m_NextRepath)
            {
                m_NextRepath = Time.time + c_RepathInterval;
                m_Agent.SetDestination(point);
            }
            Vector3 velocity = m_Agent.velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            if (speed > c_MoveAnimMinSpeed)
            {
                m_Animator.Play(CharacterAnim.Move, speed / m_Config.MoveSpeed);
                m_Animator.SetFacing(velocity.x);
            }
            else
            {
                m_Animator.Play(CharacterAnim.Idle);
            }
        }

        private void UpdateAttacking()
        {
            if (!m_Struck && m_Animator.Progress >= m_StrikeMoment)
            {
                m_Struck = true;
                if (FindTarget(out IDamageable target, out Vector3 point, out float gap) &&
                    gap <= m_Config.AttackRange * c_StrikeReachSlack)
                {
                    target.TakeDamage(Mathf.RoundToInt(m_Config.Damage * DamageScale), point - transform.position);
                    Struck?.Invoke(this);
                    // The car's spikes (workshop upgrade) hurt whoever hits it.
                    if (target is CarHealth car && car.Spikes > 0)
                    {
                        TakeDamage(car.Spikes, transform.position - point);
                        if (m_State != State.Attacking)
                            return;                                // killed or staggered by the spikes
                    }
                }
            }
            if (m_Animator.IsFinished)
                Enter(State.Chasing);
        }

        private void UpdateDead()
        {
            if (m_Gibs != null)
            {
                float fade = Mathf.Clamp01((m_StateTime - c_GibsLieTime) / c_GibsFadeTime);
                m_Gibs.SetFade(1f - fade);
                if (fade >= 1f)
                {
                    m_Gibs.Hide();
                    m_Owner.Release(this);
                }
                return;
            }
            if (m_StateTime < c_CorpseTime)
                return;
            float t = Mathf.Clamp01((m_StateTime - c_CorpseTime) / c_SinkTime);
            m_Animator.SetLift(-c_SinkDepth * t);
            m_Animator.SetAlpha(1f - t);
            m_Shadow.localScale = m_ShadowScale * (1f - t);
            if (t >= 1f)
                m_Owner.Release(this);
        }

        /// <summary>
        /// Picks the car or the player. <paramref name="point"/> is where to walk (the nearest point of the car's body
        /// or the player's position), <paramref name="gap"/> the distance from this zombie's body to the target's surface.
        /// </summary>
        private bool FindTarget(out IDamageable target, out Vector3 point, out float gap)
        {
            Vector3 position = transform.position;
            PlayerHealth player = m_Owner.Player;
            CarHealth car = m_Owner.Car;
            bool playerAlive = player != null && player.IsAlive;
            bool carAlive = car != null && car.IsAlive;
            float playerDistance = playerAlive ? PlanarDistance(position, player.transform.position) : float.MaxValue;

            if (m_Config.Preference == ZombieTarget.Player)
                m_ChasingPlayer = playerAlive;
            else if (!carAlive)
                m_ChasingPlayer = playerAlive;
            else if (m_ChasingPlayer)
                m_ChasingPlayer = playerAlive && playerDistance <= m_Config.AggroRadius * c_AggroRelease;
            else
                m_ChasingPlayer = playerAlive && playerDistance <= m_Config.AggroRadius;

            if (m_ChasingPlayer)
            {
                target = player;
                point = player.transform.position;
                gap = playerDistance - player.Radius - m_Agent.radius;
                return true;
            }
            if (carAlive)
            {
                target = car;
                point = car.Body.ClosestPoint(position);
                point.y = position.y;
                gap = PlanarDistance(position, point) - m_Agent.radius;
                return true;
            }
            target = null;
            point = position;
            gap = float.MaxValue;
            return false;
        }

        private void Stop()
        {
            if (!m_Agent.enabled)
                return;
            m_Agent.isStopped = true;
            m_Agent.velocity = Vector3.zero;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
