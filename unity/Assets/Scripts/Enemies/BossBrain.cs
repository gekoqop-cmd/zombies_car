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
    /// <item>summon — a green ring grows round the boss, then minions rise from it (the swarm of the second phase: more
    ///   of them, with the extra kinds);</item>
    /// <item>acid spit — globs lobbed at the player, warning circles where they will land, optionally leaving burning
    ///   puddles; a big fast volley is an acid rain (bigger still in the second phase);</item>
    /// <item>scythe sweep — a half-disc of danger in front of the boss, then a wide cut;</item>
    /// <item>fire cracks — a fan of lines on the ground towards the player, then they burn;</item>
    /// <item>reap (second phase) — a mark under the player, the boss fades to ash and appears on it, straight into a
    ///   quick sweep;</item>
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

        private enum Ability { None, Summon, Spit, Charge, Dash, Stunned, Slam, Shockwave, Sweep, SweepFlash, Cracks, CracksBurn, Reap }

        private const float c_SweepFlashTime = 0.3f;
        private const float c_CracksBurnTime = 0.8f;
        private const float c_ReapStrikeWindup = 0.35f;      // the sweep right after a reap comes out quicker

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
        private float m_NextSweep;
        private float m_NextCracks;
        private float m_NextReap;
        private int m_Volley;
        private Vector3 m_SweepDirection;
        private Vector3 m_ReapTarget;
        private readonly List<Vector3> m_CrackDirections = new List<Vector3>();
        private readonly List<Renderer> m_CrackLines = new List<Renderer>();
        private readonly List<Renderer> m_CrackFlames = new List<Renderer>();
        private Vector3 m_CracksFrom;

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
            m_NextSweep = now;
            m_NextCracks = now + 2f;
            m_NextReap = now;
            m_Zombie.Animator.SetAlpha(1f);
            HideGroundEffects();
            if (m_Aura != null)
                m_Aura.enabled = true;
            PlayerHealth player = m_Zombie.Owner != null ? m_Zombie.Owner.Player : null;
            m_PlayerMover = player != null ? player.GetComponent<PlayerController>() : null;
            Voice(m_Config.RoarClip, Sound.BossRoar, 1f, 1f);
            CameraShake.Add(0.45f);
        }

        private void OnDied(ZombieController zombie)
        {
            m_Ability = Ability.None;
            HideGroundEffects();
            Voice(m_Config.DeathClip, Sound.BossDeath, 1f, 1f);
            CameraShake.Add(0.7f);
        }

        private void OnStruck(ZombieController zombie)
        {
            Voice(m_Config.AttackClip, Sound.BossAttack, 1f, 1f);
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
            m_NextReap = Time.time + 2f;
            m_NextSummon = Mathf.Min(m_NextSummon, Time.time + 1f);        // the swarm comes at once
            Voice(m_Config.RoarClip, Sound.BossRoar, 1f, 0.9f);
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
            if (m_Enraged && m_Config.Reap && now >= m_NextReap && player > m_Config.SweepRange && player < 40f)
            {
                BeginReap();
                return;
            }
            if (m_Config.Charge && now >= m_NextCharge && player >= m_Config.ChargeMinRange && player <= m_Config.ChargeMaxRange)
            {
                BeginCharge();
                return;
            }
            if (m_Config.Cracks && now >= m_NextCracks && player <= m_Config.CracksLength)
            {
                BeginCracks();
                return;
            }
            if (m_Config.Sweep && now >= m_NextSweep && player <= m_Config.SweepRange * 1.15f)
            {
                BeginSweep(m_Config.SweepWindup);
                return;
            }
            if (m_Config.Summon && now >= m_NextSummon && MinionsAlive < m_Config.MaxMinions)
            {
                Begin(Ability.Summon, CharacterAnim.Attack, 0.6f);
                Voice(m_Config.SpecialClip, Sound.BossSummon, 1f, 1f);
                return;
            }
            if (m_Config.Spit && now >= m_NextSpit && player <= m_Config.SpitRange)
            {
                Begin(Ability.Spit, CharacterAnim.Attack, 0.9f);
                m_Fired = 0;
                m_Volley = Mathf.RoundToInt(m_Config.SpitVolley * (m_Enraged ? m_Config.EnrageVolley : 1f));
                if (m_Volley > 6)
                    Voice(m_Config.SpecialClip, Sound.BossSpit, 1f, 0.8f);         // the acid rain has its own cue
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
                case Ability.SweepFlash: m_NextSweep = now + Cooldown(m_Config.SweepInterval); break;
                case Ability.CracksBurn: m_NextCracks = now + Cooldown(m_Config.CracksInterval); break;
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
                    while (m_Fired < m_Volley && m_AbilityTime >= m_Config.SpitWindup + m_Fired * m_Config.SpitCadence)
                        SpitNow(m_Fired++);
                    if (m_Fired >= m_Volley && m_AbilityTime >= m_Config.SpitWindup + m_Fired * m_Config.SpitCadence + 0.3f)
                        End(Ability.Spit);
                    break;
                case Ability.Sweep:
                {
                    // A half-disc of danger in front of the boss, pulsing faster as the blow comes.
                    float k = Mathf.Clamp01(m_AbilityTime / m_Config.SweepWindup);
                    ShowRing(m_Telegraph, transform.position + m_SweepDirection * m_Config.SweepRange * 0.45f,
                        m_Config.SweepRange * 1.35f * Mathf.Lerp(0.6f, 1f, k), m_DangerColor,
                        0.5f + 0.5f * Mathf.Abs(Mathf.Sin(m_AbilityTime * Mathf.Lerp(8f, 22f, k))));
                    if (m_AbilityTime >= m_Config.SweepWindup)
                        SweepNow();
                    break;
                }
                case Ability.SweepFlash:
                {
                    float flash = Mathf.Clamp01(m_AbilityTime / c_SweepFlashTime);
                    ShowRing(m_Shockwave, transform.position + m_SweepDirection * m_Config.SweepRange * 0.45f,
                        m_Config.SweepRange * 1.4f * Mathf.Lerp(0.8f, 1.1f, flash), m_ShockwaveColor, 1f - flash * flash);
                    if (flash >= 1f)
                        End(Ability.SweepFlash);
                    break;
                }
                case Ability.Cracks:
                {
                    // The cracks crawl out from the boss along the ground (first 60 % of the wind-up), then glow
                    // faster and faster until they burst.
                    float k = Mathf.Clamp01(m_AbilityTime / m_Config.CracksWindup);
                    float grow = Mathf.Clamp01(k / 0.6f);
                    grow = 1f - (1f - grow) * (1f - grow);
                    float pulse = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(m_AbilityTime * Mathf.Lerp(5f, 22f, k)));
                    ShowCracks(m_DangerColor, pulse, grow, 1f);
                    if (m_AbilityTime >= m_Config.CracksWindup)
                        CracksNow();
                    break;
                }
                case Ability.CracksBurn:
                {
                    float burn = Mathf.Clamp01(m_AbilityTime / c_CracksBurnTime);
                    float flicker = 0.75f + 0.25f * Mathf.Sin(m_AbilityTime * 40f);
                    ShowCracks(m_ShockwaveColor, (1f - burn * burn) * flicker, 1f, Mathf.Lerp(1.6f, 1f, burn));
                    ShowFlames(burn);
                    if (burn >= 1f)
                        End(Ability.CracksBurn);
                    break;
                }
                case Ability.Reap:
                {
                    // The landing mark pulses on the ground while the boss fades into ash, then he stands on it.
                    float k = Mathf.Clamp01(m_AbilityTime / m_Config.ReapWindup);
                    ShowRing(m_Telegraph, m_ReapTarget, 3.2f * Mathf.Lerp(1.3f, 0.9f, k), m_DangerColor,
                        0.5f + 0.5f * Mathf.Abs(Mathf.Sin(m_AbilityTime * 18f)));
                    m_Zombie.Animator.SetAlpha(1f - k);
                    if (m_AbilityTime >= m_Config.ReapWindup)
                        ReapNow();
                    break;
                }
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
            // The swarm (second phase): more of them, the extra kinds (fast hounds) among them.
            int wanted = m_Config.SummonCount + (m_Enraged ? m_Config.EnrageSummonBonus : 0);
            int count = Mathf.Min(wanted, m_Config.MaxMinions + (m_Enraged ? m_Config.EnrageSummonBonus : 0) - MinionsAlive);
            float start = UnityEngine.Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float angle = (start + 360f * i / count) * Mathf.Deg2Rad;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * m_Config.SummonRadius;
                ZombieConfig[] pool = m_Enraged && m_Config.EnrageMinions.Length > 0 && i % 2 == 1 ? m_Config.EnrageMinions : minions;
                ZombieController minion = owner.SpawnAt(pool[UnityEngine.Random.Range(0, pool.Length)], at, 2.5f);
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
            AcidGlob glob = Glob();
            glob.SetPuddle(m_Config.SpitPuddle, m_Config.SpitPuddleDps);
            // An acid rain (a big volley) falls from high up and a little slower, so the circles can be dodged.
            float flight = m_Config.SpitFlight + (m_Volley > 6 ? UnityEngine.Random.Range(0f, 0.35f) : 0f);
            glob.Launch(mouth, target, flight, Mathf.RoundToInt(m_Config.SpitDamage * m_Zombie.DamageScale),
                m_Config.SpitRadius, m_Zombie.Owner, Pitch);
            if (index % 3 == 0)
                AudioManager.PlaySfx(Sound.BossSpit, 1f, Pitch);
        }

        // ---------------------------------------------------------------- scythe sweep
        private void BeginSweep(float windup)
        {
            Begin(Ability.Sweep, CharacterAnim.Attack, 0.8f / Mathf.Max(0.2f, windup));
            if (m_Ability != Ability.Sweep)
                return;
            m_AbilityTime = m_Config.SweepWindup - windup;
            m_SweepDirection = DirectionToPlayer();
            AudioManager.PlaySfx(Sound.BossCharge, 0.8f, Pitch * 1.1f);
        }

        private void SweepNow()
        {
            m_Ability = Ability.SweepFlash;
            m_AbilityTime = 0f;
            if (m_Telegraph != null)
                m_Telegraph.enabled = false;
            Voice(m_Config.AttackClip, Sound.BossAttack, 1f, 1f);
            CameraShake.Add(0.45f);
            float half = m_Config.SweepArc * 0.5f;
            PlayerHealth player = m_Zombie.Owner.Player;
            if (player != null && player.IsAlive)
            {
                Vector3 to = Flat(player.transform.position - transform.position);
                if (to.magnitude <= m_Config.SweepRange + player.Radius && Vector3.Angle(m_SweepDirection, to) <= half)
                {
                    player.TakeDamage(Mathf.RoundToInt(m_Config.SweepDamage * m_Zombie.DamageScale), to);
                    if (m_PlayerMover != null)
                        m_PlayerMover.Push(to.normalized * m_Config.SweepKnockback);
                }
            }
            CarHealth car = m_Zombie.Owner.Car;
            if (car != null && car.IsAlive)
            {
                Vector3 to = Flat(car.Body.ClosestPoint(transform.position) - transform.position);
                if (to.magnitude <= m_Config.SweepRange && Vector3.Angle(m_SweepDirection, to) <= half)
                    car.TakeDamage(Mathf.RoundToInt(m_Config.SweepCarDamage * m_Zombie.DamageScale), to);
            }
        }

        // ---------------------------------------------------------------- fire cracks
        private void BeginCracks()
        {
            Begin(Ability.Cracks, CharacterAnim.Attack, 0.7f);
            if (m_Ability != Ability.Cracks)
                return;
            // A fan of lines centred on the player.
            Vector3 aim = DirectionToPlayer();
            m_CracksFrom = Flat(transform.position);                          // the cracks stay where they were struck
            m_CrackDirections.Clear();
            int n = m_Config.CracksCount;
            for (int i = 0; i < n; i++)
            {
                float angle = (i - (n - 1) * 0.5f) * m_Config.CracksSpread;
                m_CrackDirections.Add(Quaternion.Euler(0f, angle, 0f) * aim);
            }
            Voice(m_Config.RoarClip, Sound.BossRoar, 0.7f, 1.15f);
        }

        private void CracksNow()
        {
            m_Ability = Ability.CracksBurn;
            m_AbilityTime = 0f;
            AudioManager.PlaySfx(Sound.BossSlam, 1f, Pitch);
            CameraShake.Add(0.55f);
            Vector3 from = m_CracksFrom;
            float half = m_Config.CracksWidth * 0.5f;
            PlayerHealth player = m_Zombie.Owner.Player;
            if (player != null && player.IsAlive && OnCrack(Flat(player.transform.position), from, half + player.Radius))
                player.TakeDamage(Mathf.RoundToInt(m_Config.CracksDamage * m_Zombie.DamageScale), Vector3.zero);
            CarHealth car = m_Zombie.Owner.Car;
            if (car != null && car.IsAlive && OnCrack(Flat(car.transform.position), from, half + 1.2f))
                car.TakeDamage(Mathf.RoundToInt(m_Config.CracksDamage * 0.6f * m_Zombie.DamageScale), Vector3.zero);
        }

        private bool OnCrack(Vector3 point, Vector3 from, float reach)
        {
            foreach (Vector3 dir in m_CrackDirections)
            {
                float along = Vector3.Dot(point - from, dir);
                if (along < 0f || along > m_Config.CracksLength)
                    continue;
                if (Vector3.Distance(point, from + dir * along) <= reach)
                    return true;
            }
            return false;
        }

        /// <summary>The crack lines: <paramref name="grow"/> of their length (from the boss out), <paramref name="width"/> x.</summary>
        private void ShowCracks(Color color, float alpha, float grow, float width)
        {
            while (m_CrackLines.Count < m_CrackDirections.Count && m_ChargeLine != null)
            {
                Renderer line = Instantiate(m_ChargeLine, m_ChargeLine.transform.parent);
                line.name = $"Crack{m_CrackLines.Count + 1}";
                m_CrackLines.Add(line);
            }
            float length = Mathf.Max(0.05f, m_Config.CracksLength * grow);
            for (int i = 0; i < m_CrackLines.Count; i++)
            {
                Renderer line = m_CrackLines[i];
                bool used = i < m_CrackDirections.Count;
                line.enabled = used;
                if (!used)
                    continue;
                Vector3 dir = m_CrackDirections[i];
                line.transform.SetPositionAndRotation(m_CracksFrom + Vector3.up * c_GroundHeight + dir * (length * 0.5f),
                    Quaternion.FromToRotation(Vector3.right, dir));
                line.transform.localScale = new Vector3(length, 1f, m_Config.CracksWidth * width);
                SetColor(line, color, alpha);
            }
        }

        /// <summary>
        /// Fire bursting out of the cracks: flame rings along every line light up one after another from the boss
        /// outwards (a wave of fire running down the cracks), each growing and fading.
        /// </summary>
        private void ShowFlames(float burn)
        {
            const int perLine = 5;
            int needed = m_CrackDirections.Count * perLine;
            while (m_CrackFlames.Count < needed && m_Shockwave != null)
            {
                Renderer flame = Instantiate(m_Shockwave, m_Shockwave.transform.parent);
                flame.name = $"CrackFlame{m_CrackFlames.Count + 1}";
                m_CrackFlames.Add(flame);
            }
            for (int i = 0; i < m_CrackFlames.Count; i++)
            {
                Renderer flame = m_CrackFlames[i];
                bool used = i < needed && burn < 1f;
                flame.enabled = used;
                if (!used)
                    continue;
                int line = i / perLine, step = i % perLine;
                float along = (step + 0.5f) / perLine;
                float local = Mathf.Clamp01((burn - along * 0.45f) / 0.55f);   // the far ones catch fire later
                if (local <= 0f)
                {
                    flame.enabled = false;
                    continue;
                }
                Vector3 at = m_CracksFrom + m_CrackDirections[line] * (m_Config.CracksLength * along);
                ShowRing(flame, at, m_Config.CracksWidth * Mathf.Lerp(0.8f, 2.4f, local), m_ShockwaveColor, 1f - local * local);
            }
        }

        // ---------------------------------------------------------------- reap (teleport)
        private void BeginReap()
        {
            PlayerHealth player = m_Zombie.Owner.Player;
            Vector3 target = player.transform.position - DirectionToPlayer() * 2f;     // lands just in front of the player
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                return;
            Begin(Ability.Reap, CharacterAnim.Idle, 1f);
            if (m_Ability != Ability.Reap)
                return;
            m_ReapTarget = hit.position;
            m_NextReap = Time.time + Cooldown(m_Config.ReapInterval);
            Voice(m_Config.SpecialClip, Sound.BossSummon, 1f, 0.8f);
        }

        private void ReapNow()
        {
            m_Zombie.Agent.Warp(m_ReapTarget);
            m_Zombie.Animator.SetAlpha(1f);
            m_Zombie.Animator.Flash(new Color(1f, 0.55f, 0.2f, 1f), 0.3f);
            CameraShake.AddAt(transform.position, 0.4f);
            if (m_Telegraph != null)
                m_Telegraph.enabled = false;
            // Straight into a quick sweep.
            m_Ability = Ability.Sweep;
            m_AbilityTime = m_Config.SweepWindup - c_ReapStrikeWindup;
            m_SweepDirection = DirectionToPlayer();
            m_Zombie.Animator.Play(CharacterAnim.Attack, 0.8f / c_ReapStrikeWindup, true);
            FaceTarget();
        }

        /// <summary>The boss's own voice when it has one, else the shared boss sound at its pitch.</summary>
        private void Voice(AudioClip clip, Sound fallback, float volume, float pitch)
        {
            if (clip != null && AudioManager.Instance != null)
                AudioManager.Instance.Play(clip, volume, 0.04f);
            else
                AudioManager.PlaySfx(fallback, volume, Pitch * pitch);
        }

        private Vector3 DirectionToPlayer()
        {
            PlayerHealth player = m_Zombie.Owner != null ? m_Zombie.Owner.Player : null;
            Vector3 to = player != null ? Flat(player.transform.position - transform.position) : Vector3.right;
            return to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.right;
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
                Voice(m_Config.AttackClip, Sound.BossAttack, 1f, 1f);
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
            foreach (Renderer r in m_CrackLines)
            {
                if (r != null)
                    r.enabled = false;
            }
            foreach (Renderer r in m_CrackFlames)
            {
                if (r != null)
                    r.enabled = false;
            }
            if (m_Zombie != null && m_Zombie.IsAlive)
                m_Zombie.Animator.SetAlpha(1f);
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
