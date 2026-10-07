using System;
using UnityEngine;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// Abilities of one boss (Data/Zombies/Boss*_Abilities), run by <see cref="BossBrain"/>. The boss's health, speed
    /// and melee are in its <see cref="ZombieConfig"/>. Every ability is optional; the final boss has them all plus the
    /// enraged second phase with the ground slam.
    /// </summary>
    [CreateAssetMenu(fileName = "BossAbilities", menuName = "ZombiCar/Boss Abilities")]
    public sealed class BossConfig : ScriptableObject
    {
        [Header("Summon minions")]
        [SerializeField] private bool m_Summon;
        [SerializeField, Min(1f)] private float m_SummonInterval = 7f;
        [SerializeField, Min(0.1f)] private float m_SummonWindup = 0.9f;
        [SerializeField, Min(1)] private int m_SummonCount = 4;
        [SerializeField, Min(0.5f)] private float m_SummonRadius = 2.6f;
        [Tooltip("No more summons while this many of the boss's minions are alive.")]
        [SerializeField, Min(1)] private int m_MaxMinions = 12;
        [SerializeField] private ZombieConfig[] m_Minions = Array.Empty<ZombieConfig>();

        [Header("Acid spit volley")]
        [SerializeField] private bool m_Spit;
        [SerializeField, Min(1f)] private float m_SpitInterval = 5f;
        [SerializeField, Min(0.1f)] private float m_SpitWindup = 0.5f;
        [SerializeField, Min(1)] private int m_SpitVolley = 3;
        [SerializeField, Min(1)] private int m_SpitDamage = 14;
        [SerializeField, Min(0.3f)] private float m_SpitRadius = 1.5f;
        [Tooltip("Seconds a glob flies: the warning circle shows this long before it lands.")]
        [SerializeField, Min(0.2f)] private float m_SpitFlight = 1.1f;
        [Tooltip("Globs land around the player within this radius (the first one aims at the player).")]
        [SerializeField, Min(0f)] private float m_SpitSpread = 2.6f;
        [SerializeField, Min(1f)] private float m_SpitRange = 15f;
        [Tooltip("Seconds between two globs of a volley (an acid rain: many globs, close together).")]
        [SerializeField, Min(0.02f)] private float m_SpitCadence = 0.14f;
        [Tooltip("A landed glob leaves a burning puddle this many seconds (0 = none).")]
        [SerializeField, Min(0f)] private float m_SpitPuddle;
        [Tooltip("Damage per second to the player standing in a puddle.")]
        [SerializeField, Min(0)] private int m_SpitPuddleDps = 8;
        [Tooltip("The volley is this many times bigger in the second phase.")]
        [SerializeField, Min(1f)] private float m_EnrageVolley = 1f;

        [Header("Scythe sweep")]
        [SerializeField] private bool m_Sweep;
        [SerializeField, Min(1f)] private float m_SweepInterval = 4f;
        [SerializeField, Min(0.1f)] private float m_SweepWindup = 0.8f;
        [SerializeField, Min(0.5f)] private float m_SweepRange = 4.5f;
        [Tooltip("Width of the arc in front of the boss, degrees.")]
        [SerializeField, Range(30f, 360f)] private float m_SweepArc = 160f;
        [SerializeField, Min(1)] private int m_SweepDamage = 35;
        [SerializeField, Min(1)] private int m_SweepCarDamage = 20;
        [SerializeField, Min(0f)] private float m_SweepKnockback = 5f;

        [Header("Fire cracks (lines of lava fanning out along the ground)")]
        [SerializeField] private bool m_Cracks;
        [SerializeField, Min(1f)] private float m_CracksInterval = 7f;
        [SerializeField, Min(0.1f)] private float m_CracksWindup = 1f;
        [SerializeField, Min(1)] private int m_CracksCount = 4;
        [Tooltip("Angle between two cracks, degrees (the fan is centred on the player).")]
        [SerializeField, Min(1f)] private float m_CracksSpread = 22f;
        [SerializeField, Min(1f)] private float m_CracksLength = 12f;
        [SerializeField, Min(0.2f)] private float m_CracksWidth = 1.3f;
        [SerializeField, Min(1)] private int m_CracksDamage = 26;

        [Header("Reap (second phase): teleport to the player, then a sweep")]
        [SerializeField] private bool m_Reap;
        [SerializeField, Min(1f)] private float m_ReapInterval = 7f;
        [Tooltip("Seconds the landing mark shows before the boss appears on it.")]
        [SerializeField, Min(0.2f)] private float m_ReapWindup = 1f;

        [Header("Swarm (second phase): extra summon")]
        [Tooltip("Also summoned in the second phase (e.g. fast hounds).")]
        [SerializeField] private ZombieConfig[] m_EnrageMinions = Array.Empty<ZombieConfig>();
        [SerializeField, Min(0)] private int m_EnrageSummonBonus;

        [Header("Charge")]
        [SerializeField] private bool m_Charge;
        [SerializeField, Min(1f)] private float m_ChargeInterval = 8f;
        [Tooltip("Seconds the charge path is shown before the dash (the direction is locked when it appears).")]
        [SerializeField, Min(0.1f)] private float m_ChargeWindup = 1f;
        [SerializeField, Min(1f)] private float m_ChargeSpeed = 13f;
        [SerializeField, Min(1f)] private float m_ChargeDistance = 12f;
        [SerializeField, Min(0f)] private float m_ChargeMinRange = 4f;
        [SerializeField, Min(1f)] private float m_ChargeMaxRange = 15f;
        [SerializeField, Min(1)] private int m_ChargeDamage = 30;
        [SerializeField, Min(1)] private int m_ChargeCarDamage = 40;
        [SerializeField, Min(0.3f)] private float m_ChargeHitRadius = 1.5f;
        [SerializeField, Min(0f)] private float m_ChargeKnockback = 7f;
        [Tooltip("Seconds the boss is dazed after a charge (takes more damage meanwhile).")]
        [SerializeField, Min(0f)] private float m_ChargeStun = 1.3f;
        [SerializeField, Min(1f)] private float m_StunDamageTaken = 1.5f;

        [Header("Second phase (enrage)")]
        [SerializeField] private bool m_Enrage;
        [Tooltip("Share of health at which the second phase starts.")]
        [SerializeField, Range(0.05f, 0.95f)] private float m_EnrageAt = 0.5f;
        [SerializeField, Min(1f)] private float m_EnrageSpeed = 1.4f;
        [SerializeField, Min(1f)] private float m_EnrageDamage = 1.3f;
        [Tooltip("Ability cooldowns are multiplied by this in the second phase.")]
        [SerializeField, Range(0.1f, 1f)] private float m_EnrageCooldowns = 0.6f;
        [SerializeField, Min(1f)] private float m_EnrageScale = 1.12f;
        [SerializeField] private Color m_EnrageTint = new Color(1f, 0.55f, 0.5f, 1f);

        [Header("Ground slam (second phase)")]
        [SerializeField] private bool m_Slam;
        [SerializeField, Min(1f)] private float m_SlamInterval = 6f;
        [SerializeField, Min(0.1f)] private float m_SlamWindup = 0.8f;
        [SerializeField, Min(0.5f)] private float m_SlamRadius = 4.5f;
        [SerializeField, Min(1)] private int m_SlamDamage = 28;
        [SerializeField, Min(1)] private int m_SlamCarDamage = 25;
        [SerializeField, Min(0f)] private float m_SlamKnockback = 6f;

        [Header("Look / voice")]
        [Tooltip("Pitch of the boss's roars and hits (bigger boss = lower).")]
        [SerializeField, Range(0.5f, 1.5f)] private float m_VoicePitch = 1f;
        [SerializeField] private Color m_AuraColor = new Color(0.6f, 1f, 0.4f, 0.6f);
        [Tooltip("The boss's own voice (BossVoiceSynth); a missing one falls back to the shared boss sound.")]
        [SerializeField] private AudioClip m_RoarClip;
        [SerializeField] private AudioClip m_AttackClip;
        [SerializeField] private AudioClip m_DeathClip;
        [Tooltip("Its signature ability: the brood's call, the acid rain, the reap.")]
        [SerializeField] private AudioClip m_SpecialClip;
        [Tooltip("The charge's own sound (from the boss's video take); none: the shared charge sound.")]
        [SerializeField] private AudioClip m_ChargeClip;
        [Tooltip("Coming back up out of the ground after the reap's teleport; none: silent.")]
        [SerializeField] private AudioClip m_AppearClip;

        public bool Summon => m_Summon;
        public float SummonInterval => m_SummonInterval;
        public float SummonWindup => m_SummonWindup;
        public int SummonCount => m_SummonCount;
        public float SummonRadius => m_SummonRadius;
        public int MaxMinions => m_MaxMinions;
        public ZombieConfig[] Minions => m_Minions;

        public bool Spit => m_Spit;
        public float SpitInterval => m_SpitInterval;
        public float SpitWindup => m_SpitWindup;
        public int SpitVolley => m_SpitVolley;
        public int SpitDamage => m_SpitDamage;
        public float SpitRadius => m_SpitRadius;
        public float SpitFlight => m_SpitFlight;
        public float SpitSpread => m_SpitSpread;
        public float SpitRange => m_SpitRange;
        public float SpitCadence => m_SpitCadence;
        public float SpitPuddle => m_SpitPuddle;
        public int SpitPuddleDps => m_SpitPuddleDps;
        public float EnrageVolley => m_EnrageVolley;

        public bool Sweep => m_Sweep;
        public float SweepInterval => m_SweepInterval;
        public float SweepWindup => m_SweepWindup;
        public float SweepRange => m_SweepRange;
        public float SweepArc => m_SweepArc;
        public int SweepDamage => m_SweepDamage;
        public int SweepCarDamage => m_SweepCarDamage;
        public float SweepKnockback => m_SweepKnockback;

        public bool Cracks => m_Cracks;
        public float CracksInterval => m_CracksInterval;
        public float CracksWindup => m_CracksWindup;
        public int CracksCount => m_CracksCount;
        public float CracksSpread => m_CracksSpread;
        public float CracksLength => m_CracksLength;
        public float CracksWidth => m_CracksWidth;
        public int CracksDamage => m_CracksDamage;

        public bool Reap => m_Reap;
        public float ReapInterval => m_ReapInterval;
        public float ReapWindup => m_ReapWindup;

        public ZombieConfig[] EnrageMinions => m_EnrageMinions;
        public int EnrageSummonBonus => m_EnrageSummonBonus;

        public bool Charge => m_Charge;
        public float ChargeInterval => m_ChargeInterval;
        public float ChargeWindup => m_ChargeWindup;
        public float ChargeSpeed => m_ChargeSpeed;
        public float ChargeDistance => m_ChargeDistance;
        public float ChargeMinRange => m_ChargeMinRange;
        public float ChargeMaxRange => m_ChargeMaxRange;
        public int ChargeDamage => m_ChargeDamage;
        public int ChargeCarDamage => m_ChargeCarDamage;
        public float ChargeHitRadius => m_ChargeHitRadius;
        public float ChargeKnockback => m_ChargeKnockback;
        public float ChargeStun => m_ChargeStun;
        public float StunDamageTaken => m_StunDamageTaken;

        public bool Enrage => m_Enrage;
        public float EnrageAt => m_EnrageAt;
        public float EnrageSpeed => m_EnrageSpeed;
        public float EnrageDamage => m_EnrageDamage;
        public float EnrageCooldowns => m_EnrageCooldowns;
        public float EnrageScale => m_EnrageScale;
        public Color EnrageTint => m_EnrageTint;

        public bool Slam => m_Slam;
        public float SlamInterval => m_SlamInterval;
        public float SlamWindup => m_SlamWindup;
        public float SlamRadius => m_SlamRadius;
        public int SlamDamage => m_SlamDamage;
        public int SlamCarDamage => m_SlamCarDamage;
        public float SlamKnockback => m_SlamKnockback;

        public float VoicePitch => m_VoicePitch;
        public Color AuraColor => m_AuraColor;
        public AudioClip RoarClip => m_RoarClip;
        public AudioClip AttackClip => m_AttackClip;
        public AudioClip DeathClip => m_DeathClip;
        public AudioClip SpecialClip => m_SpecialClip;
        public AudioClip ChargeClip => m_ChargeClip;
        public AudioClip AppearClip => m_AppearClip;
    }
}
