using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.Waves;

namespace ZombiCar.Car
{
    /// <summary>
    /// Ammo at the car: between waves the interact key (E / the touch action button) refills the gun — the magazine and
    /// the reserve to the maximum — in a short channel (the player stands still and reloads, the refill sound plays).
    /// One refill per break: the car gets a supply at the end of every wave, used up by the refill and lost when the
    /// next wave starts (nothing before wave 1: the run starts with a full magazine and the starting reserve). During a
    /// wave, or after the refill, the prompt only shows when the player is low on ammo, saying when the next one comes.
    /// A bobbing ammo box above the trunk shows while the refill can be used and pops when it is taken.
    /// </summary>
    public sealed class CarResupply : Interactable
    {
        private const string c_Prompt = "ПОПОЛНИТЬ ПАТРОНЫ";
        private const string c_DuringWave = "ПАТРОНЫ У МАШИНЫ — ТОЛЬКО МЕЖДУ ВОЛНАМИ";
        private const string c_NextBreak = "ПАТРОНЫ У МАШИНЫ — ПОСЛЕ СЛЕДУЮЩЕЙ ВОЛНЫ";
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private RunState m_Run;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private PlayerWeapon m_Weapon;
        [SerializeField] private PlayerView m_View;
        [Tooltip("The car's body: the distance is measured to its surface.")]
        [SerializeField] private Collider m_Body;
        [Tooltip("Seconds of the refill (the player stands still).")]
        [SerializeField, Min(0f)] private float m_Duration = 1.2f;
        [Tooltip("Share of the full load (magazine + reserve) below which the closed prompt still shows.")]
        [SerializeField, Range(0f, 1f)] private float m_LowAmmo = 0.3f;
        [Header("Marker")]
        [SerializeField] private Transform m_Marker;
        [SerializeField] private SpriteRenderer m_MarkerSprite;
        [SerializeField] private Renderer m_Glow;
        [SerializeField, Min(0f)] private float m_BobHeight = 0.15f;
        [SerializeField, Min(0.01f)] private float m_BobSpeed = 2.6f;
        [SerializeField, Min(0.01f)] private float m_PopTime = 0.45f;

        private bool m_Supplied;
        private float m_PopStart = float.NegativeInfinity;
        private Vector3 m_MarkerBase;
        private Vector3 m_MarkerScale;
        private MaterialPropertyBlock m_Block;
        private Color m_GlowColor;

        public override float Duration => m_Duration;

        /// <summary>A refill waits at the car in this break.</summary>
        public bool IsSupplied => m_Supplied;

        /// <summary>The refill can be used now: between waves, not used yet in this break, the gun not full.</summary>
        public bool IsAvailable => m_Supplied && m_Run != null && !m_Run.IsOver && m_Waves != null && !m_Waves.IsWaveRunning &&
                                   m_Weapon != null && !m_Weapon.IsFull;

        private bool IsLow
        {
            get
            {
                if (m_Weapon == null)
                    return false;
                int full = m_Weapon.MagazineSize + m_Weapon.MaxReserve;
                return m_Weapon.InMagazine + m_Weapon.Reserve < full * m_LowAmmo;
            }
        }

        public override bool IsOffered => m_Run != null && !m_Run.IsOver && (IsAvailable || IsLow);

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            if (m_Marker != null)
            {
                m_MarkerBase = m_Marker.localPosition;
                m_MarkerScale = m_Marker.localScale;
            }
            if (m_Glow != null)
                m_GlowColor = m_Glow.sharedMaterial.GetColor(s_BaseColor);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_Supplied = false;
            if (m_Waves == null)
                return;
            m_Waves.WaveStarted += OnWaveStarted;
            m_Waves.WaveEnded += OnWaveEnded;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_Waves == null)
                return;
            m_Waves.WaveStarted -= OnWaveStarted;
            m_Waves.WaveEnded -= OnWaveEnded;
        }

        private void OnWaveStarted(int wave) => m_Supplied = false;

        private void OnWaveEnded(int wave) => m_Supplied = wave < m_Waves.WaveCount;

        public override float DistanceTo(Vector3 position)
        {
            if (m_Body == null || !m_Body.enabled)
                return base.DistanceTo(position);
            Vector3 d = position - m_Body.ClosestPoint(position);
            d.y = 0f;
            return d.magnitude;
        }

        public override bool GetPrompt(out string text)
        {
            if (IsAvailable)
            {
                text = c_Prompt;
                return true;
            }
            text = m_Waves != null && m_Waves.IsWaveRunning ? c_DuringWave : c_NextBreak;
            return false;
        }

        public override void Begin(PlayerInteractor player)
        {
            AudioManager.PlaySfx(Sound.AmmoRefill);
            if (m_View != null)
                m_View.PlayRefill(m_Duration);
        }

        public override void Cancel(PlayerInteractor player)
        {
            if (m_View != null)
                m_View.StopRefill();
        }

        public override void Complete(PlayerInteractor player)
        {
            if (!IsAvailable)
                return;
            m_Weapon.Refill();
            m_Supplied = false;
            m_PopStart = Time.time;
        }

        /// <summary>Puts a refill at the car now (test tools).</summary>
        public void Supply() => m_Supplied = true;

        private void LateUpdate()
        {
            if (m_Marker == null)
                return;
            float pop = (Time.time - m_PopStart) / m_PopTime;
            bool popping = pop >= 0f && pop < 1f;
            bool shown = IsAvailable || popping;
            if (m_MarkerSprite != null)
                m_MarkerSprite.enabled = shown;
            if (m_Glow != null)
                m_Glow.enabled = shown;
            if (!shown)
                return;
            float t = Time.time * m_BobSpeed;
            float alpha = popping ? 1f - pop : 1f;
            m_Marker.localPosition = m_MarkerBase + Vector3.up * (popping ? pop * 0.6f : Mathf.Sin(t) * m_BobHeight);
            m_Marker.localScale = m_MarkerScale * (popping ? 1f + pop * 0.6f : 1f);
            if (m_MarkerSprite != null)
                m_MarkerSprite.color = new Color(1f, 1f, 1f, alpha);
            if (m_Glow != null)
            {
                Color c = m_GlowColor;
                c.a *= alpha * (0.75f + 0.25f * Mathf.Sin(t * 1.5f));
                m_Block.SetColor(s_BaseColor, c);
                m_Glow.SetPropertyBlock(m_Block);
            }
        }
    }
}
