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
    /// A boss's abilities (<see cref="BossConfig"/>) on top of its <see cref="ZombieController"/>, each telegraphed on
    /// the ground before it hits so the player can react:
    /// <list type="bullet">
    /// <item>summon — a green ring grows round the boss, then minions rise from it;</item>
    /// <item>acid spit — a volley of globs lobbed at the player, warning circles where they will land;</item>
    /// <item>charge — a red chevron path appears (direction locked), then the boss dashes along it, hurting and
    ///   knocking back the player (and ramming the car) and stays dazed afterwards (takes extra damage);</item>
    /// <item>second phase — at a share of its health the boss enrages: grows, glows red, moves and hits harder, uses
    ///   its abilities more often and adds a ground slam (red ring, then a shockwave).</item>
    /// </list>
    /// Roars when it comes out, enrages and dies; its melee blows thud (voice pitch per boss); big moments shake the
    /// screen.
    /// </summary>
    [RequireComponent(typeof(ZombieController))]
    public sealed class BossBrain : MonoBehaviour
    {
        private const float c_GlobInterval = 0.14f;
        private const float c_FirstAbilityDelay = 3f;
        private const float c_GroundHeight = 0.06f;
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        private enum Ability { None, Summon, Spit, Charge, Dash, Stunned, Slam, Shockwave }

        [SerializeField] private BossConfig m_Config;
        [SerializeField] private AcidGlob m_GlobPrefab;
        [Header("Ground effects (children, placed in the world while shown)")]
        [SerializeField] private Renderer m_Aura;
        [SerializeField] private Renderer m_Telegraph;
        [SerializeField] private Renderer m_ChargeLine;
        [SerializeField] private Renderer m_Shockwave;
        [SerializeField] private Color m_SummonColor = new Color(0.5f, 1f, 0.3f, 0.9f);
        [SerializeField] private Color m_DangerColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        [SerializeField] private Color m_ShockwaveColor = new Color(1f, 0.6f, 0.2f, 1f);

        private readonly List<ZombieController> m_Minions = new List<ZombieController>();
        private readonly List<AcidGlob> m_Globs = new List<AcidGlob>();
        private ZombieController m_Zombie;
        private MaterialPropertyBlock m_Block;
        private PlayerController m_PlayerMover;
        private Ability m_Ability;
        private float m_AbilityTime;
        private bool m_Enraged;
        private float m_NextSummon;
        private float m_NextSpit;
        private float m_NextCharge;
        private float m_NextSlam;
        private int m_Fired;
        private Vector3 m_ChargeDirection;
        private float m_ChargeLength;
        private float m_Dashed;
        private bool m_HitPlayer;
        private bool m_HitCar;

        public BossConfig Config => m_Config;
        public bool IsEnraged => m_Enraged;
        public string CurrentAbility => m_Ability.ToString();

        /// <summary>The second phase began (tests, HUD).</summary>
        public event Action Enraged;

        /// <summary>Minions of this boss that are alive.</summary>
        public int MinionsAlive
        {
            get
            {
                m_Minions.RemoveAll(m => m == null || !m.IsAlive);
                return m_Minions.Count;
            }
        }

        private float Pitch => m_Config != null ? m_Config.VoicePitch : 1f;
        private float Cooldown(float seconds) => seconds * (m_Enraged ? m_Config.EnrageCooldowns : 1f);

        private void Awake()
        {
            m_Zombie = GetComponent<ZombieController>();
            m_Block = new MaterialPropertyBlock();
            m_Zombie.Spawned += OnSpawned;
            m_Zombie.Died += OnDied;
            m_Zombie.Struck += OnStruck;
            HideGroundEffects();
        }

        private void OnDestroy()
        {
            m_Zombie.Spawned -= OnSpawned;
            m_Zombie.Died -= OnDied;
            m_Zombie.Struck -= OnStruck;
        }

        // ---------------------------------------------------------------- life
        private void OnSpawned(ZombieController zombie)
        {
            m_Ability = Ability.None;
            m_Enraged = false;
            m_Minions.Clear();
            float now = Time.time + c_FirstAbilityDelay;
            m_NextSummon = now;
            m_NextSpit = now + 1.5f;
            m_NextCharge = now + 3f;
            m_NextSlam = now;
            HideGroundEffects();
            if (m_Aura != null)
                m_Aura.enabled = true;
            PlayerHealth player = m_Zombie.Owner != null ? m_Zombie.Owner.Player : null;
            m_PlayerMover = player != null ? player.GetComponent<PlayerController>() : null;
            AudioManager.PlaySfx(Sound.BossRoar, 1f, Pitch);
            CameraShake.Add(0.45f);
        }

        private void OnDied(ZombieController zombie)
        {
            m_Ability = Ability.None;
            HideGroundEffects();
            AudioManager.PlaySfx(Sound.BossDeath, 1f, Pitch);
            CameraShake.Add(0.7f);
        }

        private void OnStruck(ZombieController zombie)
        {
            AudioManager.PlaySfx(Sound.BossAttack, 1f, Pitch);
            CameraShake.AddAt(transform.position, 0.2f);
        }

        private void Update()
        {
            if (m_Config == null || !m_Zombie.IsAlive)
                return;
            AnimateAura();
            if (m_Config.Enrage && !m_Enraged && m_Zombie.Health <= m_Zombie.MaxHealth * m_Config.EnrageAt)
                Enrage();
            if (m_Ability != Ability.None)
            {
                m_AbilityTime += Time.deltaTime;
                UpdateAbility();
                return;
            }
            if (m_Zombie.CanAct)
                TryStartAbility();
        }

        /// <summary>The second phase: bigger, red, faster and stronger, abilities more often, the ground slam.</summary>
        public void Enrage()
        {
            if (m_Enraged)
                return;
            m_Enraged = true;
            m_Zombie.SpeedScale *= m_Config.EnrageSpeed;
            m_Zombie.DamageScale *= m_Config.EnrageDamage;
            m_Zombie.Animator.ScaleMultiplier = m_Config.EnrageScale;
            m_Zombie.Animator.TintMultiplier = m_Config.EnrageTint;
            m_Zombie.Animator.Flash(Color.white, 0.4f);
            m_NextSlam = Time.time + 1.5f;
            AudioManager.PlaySfx(Sound.BossRoar, 1f, Pitch * 0.9f);
            CameraShake.Add(0.6f);
            Debug.Log($"[BossBrain] {m_Zombie.Config.DisplayName} enraged at {m_Zombie.Health}/{m_Zombie.MaxHealth}.");
            Enraged?.Invoke();
        }

        // ---------------------------------------------------------------- choosing
        private void TryStartAbility()
        {
            float now = Time.time;
            if (m_Enraged && m_Config.Slam && now >= m_NextSlam && PlayerDistance() <= m_Config.SlamRadius * 1.6f)
            {
                Begin(Ability.Slam, CharacterAnim.Attack, 0.45f);
                AudioManager.PlaySfx(Sound.BossRoar, 0.6f, Pitch * 1.1f);
                return;
            }
            float player = PlayerDistance();
            if (m_Config.Charge && now >= m_NextCharge && player >= m_Config.ChargeMinRange && player <= m_Config.ChargeMaxRange)
            {
                BeginCharge();
                return;
            }
            if (m_Config.Summon && now >= m_NextSummon && MinionsAlive < m_Config.MaxMinions)
            {
                Begin(Ability.Summon, CharacterAnim.Attack, 0.6f);
                AudioManager.PlaySfx(Sound.BossSummon, 1f, Pitch);
                return;
            }
            if (m_Config.Spit && now >= m_NextSpit && player <= m_Config.SpitRange)
            {
                Begin(Ability.Spit, CharacterAnim.Attack, 0.9f);
                m_Fired = 0;
            }
        }

        private void Begin(Ability ability, CharacterAnim anim, float animSpeed)
        {
            if (!m_Zombie.BeginAction(anim, animSpeed))
                return;
            m_Ability = ability;
            m_AbilityTime = 0f;
            FaceTarget();
        }

        private void End(Ability finished)
        {
            m_Ability = Ability.None;
            HideGroundEffects();
            if (m_Aura != null)
                m_Aura.enabled = true;
            m_Zombie.DamageTakenScale = 1f;
            m_Zombie.EndAction();
            float now = Time.time;
            switch (finished)
            {
                case Ability.Summon: m_NextSummon = now + Cooldown(m_Config.SummonInterval); break;
                case Ability.Spit: m_NextSpit = now + Cooldown(m_Config.SpitInterval); break;
                case Ability.Stunned: m_NextCharge = now + Cooldown(m_Config.ChargeInterval); break;
                case Ability.Shockwave: m_NextSlam = now + Cooldown(m_Config.SlamInterval); break;
            }
        }

        // ---------------------------------------------------------------- abilities
        private void UpdateAbility()
        {
            switch (m_Ability)
            {
                case Ability.Summon:
                    ShowRing(m_Telegraph, transform.position, m_Config.SummonRadius * 2.2f * Mathf.Clamp01(m_AbilityTime / m_Config.SummonWindup),
                        m_SummonColor, 1f);
                    if (m_AbilityTime >= m_Config.SummonWindup)
                    {
                        SummonNow();
                        End(Ability.Summon);
                    }
                    break;
                case Ability.Spit:
                    if (m_AbilityTime >= m_Config.SpitWindup + m_Fired * c_GlobInterval && m_Fired < m_Config.SpitVolley)
                        SpitNow(m_Fired++);
                    if (m_Fired >= m_Config.SpitVolley && m_AbilityTime >= m_Config.SpitWindup + m_Fired * c_GlobInterval + 0.3f)
                        End(Ability.Spit);
                    break;
                case Ability.Charge:
                    ShowChargeLine(0.55f + 0.45f * Mathf.Abs(Mathf.Sin(m_AbilityTime * 14f)));
                    if (m_AbilityTime >= m_Config.ChargeWindup)
                    {
                        m_Ability = Ability.Dash;
                        m_AbilityTime = 0f;
                        m_Dashed = 0f;
                        m_Zombie.Animator.Play(CharacterAnim.Move, 3f);
                        if (m_ChargeLine != null)
                            m_ChargeLine.enabled = false;
                    }
                    break;
                case Ability.Dash:
                    UpdateDash();
                    break;
                case Ability.Stunned:
                    if (m_AbilityTime >= m_Config.ChargeStun)
                        End(Ability.Stunned);
                    break;
                case Ability.Slam:
                    ShowRing(m_Telegraph, transform.position, m_Config.SlamRadius * 2f * Mathf.Clamp01(m_AbilityTime / m_Config.SlamWindup),
                        m_DangerColor, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(m_AbilityTime * 16f)));
                    if (m_AbilityTime >= m_Config.SlamWindup)
                        SlamNow();
                    break;
                case Ability.Shockwave:
                    float t = Mathf.Clamp01(m_AbilityTime / 0.35f);
                    ShowRing(m_Shockwave, transform.position, m_Config.SlamRadius * 2.3f * Mathf.Lerp(0.3f, 1f, t), m_ShockwaveColor, 1f - t * t);
                    if (t >= 1f)
                        End(Ability.Shockwave);
                    break;
            }
        }

        private void SummonNow()
        {
            ZombieSpawner owner = m_Zombie.Owner;
            ZombieConfig[] minions = m_Config.Minions;
            if (owner == null || minions.Length == 0)
                return;
            int count = Mathf.Min(m_Config.SummonCount, m_Config.MaxMinions - MinionsAlive);
            float start = UnityEngine.Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float angle = (start + 360f * i / count) * Mathf.Deg2Rad;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * m_Config.SummonRadius;
                ZombieController minion = owner.SpawnAt(minions[UnityEngine.Random.Range(0, minions.Length)], at, 2.5f);
                if (minion != null)
                    m_Minions.Add(minion);
            }
            CameraShake.AddAt(transform.position, 0.25f);
        }

        private void SpitNow(int index)
        {
            PlayerHealth player = m_Zombie.Owner != null ? m_Zombie.Owner.Player : null;
            if (player == null || !player.IsAlive)
                return;
            Vector3 target = player.transform.position;
            if (index > 0)
            {
                Vector2 spread = UnityEngine.Random.insideUnitCircle * m_Config.SpitSpread;
                target += new Vector3(spread.x, 0f, spread.y);
            }
            Vector3 mouth = transform.position + Vector3.up * m_Zombie.VisualHeight * 0.6f;
            Glob().Launch(mouth, target, m_Config.SpitFlight, Mathf.RoundToInt(m_Config.SpitDamage * m_Zombie.DamageScale),
                m_Config.SpitRadius, m_Zombie.Owner, Pitch);
            AudioManager.PlaySfx(Sound.BossSpit, 1f, Pitch);
        }

        private AcidGlob Glob()
        {
            foreach (AcidGlob glob in m_Globs)
            {
                if (glob != null && !glob.IsBusy)
                    return glob;
            }
            AcidGlob created = Instantiate(m_GlobPrefab);
            created.gameObject.SetActive(false);
            m_Globs.Add(created);
            return created;
        }

        private void BeginCharge()
        {
            PlayerHealth player = m_Zombie.Owner.Player;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            m_ChargeDirection = to.normalized;
            m_ChargeLength = m_Config.ChargeDistance;
            // Stop at the edge of the walkable area (fences, buildings).
            if (NavMesh.Raycast(transform.position, transform.position + m_ChargeDirection * m_ChargeLength, out NavMeshHit hit, NavMesh.AllAreas))
                m_ChargeLength = Mathf.Max(1f, hit.distance - 0.3f);
            Begin(Ability.Charge, CharacterAnim.Idle, 1.5f);
            if (m_Ability != Ability.Charge)
                return;
            m_HitPlayer = m_HitCar = false;
            AudioManager.PlaySfx(Sound.BossCharge, 1f, Pitch);
            ShowChargeLine(1f);
        }

        private void UpdateDash()
        {
            float step = Mathf.Min(m_Config.ChargeSpeed * Time.deltaTime, m_ChargeLength - m_Dashed);
            Vector3 before = transform.position;
            m_Zombie.Agent.Move(m_ChargeDirection * step);
            float moved = Vector3.Distance(Flat(before), Flat(transform.position));
            m_Dashed += Mathf.Max(moved, step * 0.25f);                      // blocked: still ends
            ZombieSpawner owner = m_Zombie.Owner;
            PlayerHealth player = owner.Player;
            if (!m_HitPlayer && player != null && player.IsAlive &&
                Vector3.Distance(Flat(player.transform.position), Flat(transform.position)) <= m_Config.ChargeHitRadius + player.Radius)
            {
                m_HitPlayer = true;
                player.TakeDamage(Mathf.RoundToInt(m_Config.ChargeDamage * m_Zombie.DamageScale), m_ChargeDirection);
                if (m_PlayerMover != null)
                    m_PlayerMover.Push(m_ChargeDirection * m_Config.ChargeKnockback);
                AudioManager.PlaySfx(Sound.BossAttack, 1f, Pitch);
                CameraShake.Add(0.4f);
            }
            CarHealth car = owner.Car;
            if (!m_HitCar && car != null && car.IsAlive &&
                Vector3.Distance(Flat(car.Body.ClosestPoint(transform.position)), Flat(transform.position)) <= m_Zombie.Agent.radius + 0.4f)
            {
                m_HitCar = true;
                car.TakeDamage(Mathf.RoundToInt(m_Config.ChargeCarDamage * m_Zombie.DamageScale), m_ChargeDirection);
                AudioManager.PlaySfx(Sound.BossSlam, 0.7f, Pitch);
                CameraShake.Add(0.45f);
                m_Dashed = m_ChargeLength;                                  // rammed the car: the dash ends there
            }
            if (m_Dashed < m_ChargeLength - 0.01f)
                return;
            // Dazed after the charge: a window to punish it.
            m_Ability = Ability.Stunned;
            m_AbilityTime = 0f;
            m_Zombie.DamageTakenScale = m_Config.StunDamageTaken;
            m_Zombie.Animator.Play(CharacterAnim.Hit, 1f, true);
        }

        private void SlamNow()
        {
            m_Ability = Ability.Shockwave;
            m_AbilityTime = 0f;
            if (m_Telegraph != null)
                m_Telegraph.enabled = false;
            AudioManager.PlaySfx(Sound.BossSlam, 1f, Pitch);
            CameraShake.Add(0.7f);
            ZombieSpawner owner = m_Zombie.Owner;
            PlayerHealth player = owner.Player;
            if (player != null && player.IsAlive)
            {
                Vector3 away = Flat(player.transform.position - transform.position);
                if (away.magnitude <= m_Config.SlamRadius + player.Radius)
                {
                    player.TakeDamage(Mathf.RoundToInt(m_Config.SlamDamage * m_Zombie.DamageScale), away);
                    if (m_PlayerMover != null)
                        m_PlayerMover.Push(away.normalized * m_Config.SlamKnockback);
                }
            }
            CarHealth car = owner.Car;
            if (car != null && car.IsAlive &&
                Vector3.Distance(Flat(car.Body.ClosestPoint(transform.position)), Flat(transform.position)) <= m_Config.SlamRadius)
                car.TakeDamage(Mathf.RoundToInt(m_Config.SlamCarDamage * m_Zombie.DamageScale), Vector3.zero);
        }

        // ---------------------------------------------------------------- effects
        private void FaceTarget()
        {
            PlayerHealth player = m_Zombie.Owner != null ? m_Zombie.Owner.Player : null;
            if (player != null)
                m_Zombie.Animator.SetFacing(player.transform.position.x - transform.position.x);
        }

        private float PlayerDistance()
        {
            PlayerHealth player = m_Zombie.Owner != null ? m_Zombie.Owner.Player : null;
            return player != null && player.IsAlive
                ? Vector3.Distance(Flat(player.transform.position), Flat(transform.position))
                : float.MaxValue;
        }

        private void AnimateAura()
        {
            if (m_Aura == null || m_Ability == Ability.Summon)
                return;
            Color c = m_Enraged ? m_DangerColor : m_Config.AuraColor;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * (m_Enraged ? 7f : 3f));
            SetColor(m_Aura, c, pulse);
        }

        private void ShowRing(Renderer ring, Vector3 center, float size, Color color, float alpha)
        {
            if (ring == null)
                return;
            ring.enabled = true;
            ring.transform.SetPositionAndRotation(new Vector3(center.x, c_GroundHeight, center.z), Quaternion.identity);
            ring.transform.localScale = new Vector3(Mathf.Max(0.01f, size), 1f, Mathf.Max(0.01f, size));
            SetColor(ring, color, alpha);
        }

        private void ShowChargeLine(float alpha)
        {
            if (m_ChargeLine == null)
                return;
            m_ChargeLine.enabled = true;
            Transform t = m_ChargeLine.transform;
            t.SetPositionAndRotation(Flat(transform.position) + Vector3.up * c_GroundHeight + m_ChargeDirection * (m_ChargeLength * 0.5f),
                Quaternion.FromToRotation(Vector3.right, m_ChargeDirection));
            t.localScale = new Vector3(m_ChargeLength, 1f, 1.8f);
            SetColor(m_ChargeLine, m_DangerColor, alpha);
        }

        private void HideGroundEffects()
        {
            foreach (Renderer r in new[] { m_Telegraph, m_ChargeLine, m_Shockwave })
            {
                if (r != null)
                    r.enabled = false;
            }
        }

        private void SetColor(Renderer r, Color color, float alpha)
        {
            color.a *= alpha;
            m_Block.SetColor(s_BaseColor, color);
            r.SetPropertyBlock(m_Block);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
