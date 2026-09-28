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
    /// the player is close; hunters always go for the player), hits in melee, staggers when shot, dies, then sinks back
    /// into the ground and returns to the <see cref="ZombieSpawner"/> pool. The body is a 2D sprite driven by
    /// <see cref="SpriteAnimator"/>; the root never rotates.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class ZombieController : MonoBehaviour, IDamageable
    {
        private const float c_RepathInterval = 0.3f;
        private const float c_RiseTime = 0.9f;
        private const float c_CorpseTime = 2.5f;
        private const float c_SinkTime = 1f;
        private const float c_SinkDepth = 0.9f;
        private const float c_MoveAnimMinSpeed = 0.15f;
        private const float c_StaggerImmunity = 0.5f;      // after a stagger, hits only flash for this long (no stun-lock)
        private const float c_StrikeReachSlack = 1.3f;     // the target may step back a little during the wind-up
        private const float c_AggroRelease = 1.5f;
        private const float c_HitFlashTime = 0.15f;
        private static readonly Color s_HitFlash = new Color(1f, 0.3f, 0.25f, 1f);

        private enum State { Rising, Chasing, Attacking, Staggered, Dead }

        [SerializeField] private SpriteAnimator m_Animator;
        [SerializeField] private Transform m_Shadow;
        [Tooltip("Share of the attack clip after which the hit lands (the strike frame).")]
        [SerializeField, Range(0f, 1f)] private float m_StrikeMoment = 0.5f;
        [Tooltip("Height of the sprite, metres: rising starts this deep under the ground.")]
        [SerializeField, Min(0.1f)] private float m_VisualHeight = 2.5f;

        private NavMeshAgent m_Agent;
        private CapsuleCollider m_Collider;
        private ZombieConfig m_Config;
        private ZombieSpawner m_Owner;
        private Vector3 m_ShadowScale;
        private State m_State = State.Dead;
        private float m_StateTime;
        private int m_Health;
        private float m_NextRepath;
        private float m_NextAttack;
        private float m_StaggerAllowedAt;
        private bool m_Struck;
        private bool m_ChasingPlayer;

        public ZombieConfig Config => m_Config;
        public bool IsAlive => m_State != State.Dead;
        public int Health => m_Health;
        public int MaxHealth => m_Config != null ? m_Config.MaxHealth : 1;

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
            m_Health = config.MaxHealth;
            m_ChasingPlayer = false;
            m_NextAttack = 0f;
            m_StaggerAllowedAt = 0f;

            transform.SetPositionAndRotation(position, Quaternion.identity);
            m_Agent.enabled = true;
            m_Agent.Warp(position);
            m_Agent.speed = config.MoveSpeed * Random.Range(1f - config.SpeedVariation, 1f + config.SpeedVariation);
            m_Agent.avoidancePriority = Random.Range(30, 70);
            m_Agent.isStopped = true;
            m_Collider.enabled = false;           // not shootable while still in the ground

            m_Animator.ResetState();
            if (owner.Car != null)
                m_Animator.SetFacing(owner.Car.transform.position.x - position.x);
            m_Animator.SetLift(-m_VisualHeight);
            m_Shadow.localScale = Vector3.zero;
            Enter(State.Rising);
        }

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (m_State == State.Dead || m_State == State.Rising)
                return;
            m_Health -= amount;
            m_Animator.Flash(s_HitFlash, c_HitFlashTime);
            if (m_Health <= 0)
            {
                Enter(State.Dead);
                return;
            }
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
                case State.Staggered:
                    Stop();
                    m_StaggerAllowedAt = Time.time + m_Config.StaggerTime + c_StaggerImmunity;
                    m_Animator.Play(CharacterAnim.Hit, 1f, true);
                    break;
                case State.Dead:
                    m_Agent.enabled = false;      // corpses are not in the crowd avoidance
                    m_Collider.enabled = false;
                    m_Animator.Play(CharacterAnim.Dead, 1f, true);
                    m_Owner.NotifyDied(this);
                    break;
            }
        }

        private void UpdateRising()
        {
            float t = Mathf.Clamp01(m_StateTime / c_RiseTime);
            float eased = 1f - (1f - t) * (1f - t);
            m_Animator.SetLift(-m_VisualHeight * (1f - eased));
            m_Shadow.localScale = m_ShadowScale * eased;
            if (t < 1f)
                return;
            m_Collider.enabled = true;
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
                    target.TakeDamage(m_Config.Damage, point - transform.position);
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
