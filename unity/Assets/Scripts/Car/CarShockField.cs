using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.Car
{
    /// <summary>
    /// Workshop upgrade "electric discharge": while the run has <see cref="StatEffect.CarShock"/>, the car zaps every
    /// zombie within <see cref="m_Radius"/> of it every <see cref="m_Interval"/> seconds, with an expanding electric
    /// ring on the ground and a crackle.
    /// </summary>
    public sealed class CarShockField : MonoBehaviour
    {
        private static readonly Collider[] s_Hits = new Collider[48];

        [SerializeField] private CarHealth m_Car;
        [SerializeField] private RunState m_Run;
        [Tooltip("Flat ring on the ground, scaled out and faded on every pulse.")]
        [SerializeField] private Renderer m_Ring;
        [SerializeField, Min(0.5f)] private float m_Radius = 4.5f;
        [SerializeField, Min(0.5f)] private float m_Interval = 6f;
        [SerializeField, Min(0.05f)] private float m_RingTime = 0.4f;

        private readonly System.Collections.Generic.HashSet<IDamageable> m_Zapped = new System.Collections.Generic.HashSet<IDamageable>();
        private MaterialPropertyBlock m_Block;
        private float m_NextPulse;
        private float m_PulseTime = float.NegativeInfinity;
        private Color m_RingColor;

        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        public float Radius => m_Radius;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            if (m_Ring != null)
            {
                m_RingColor = m_Ring.sharedMaterial.GetColor(s_BaseColor);
                m_Ring.enabled = false;
            }
        }

        private void Update()
        {
            AnimateRing();
            int damage = m_Run != null ? m_Run.Modifiers.Whole(StatEffect.CarShock) : 0;
            if (damage <= 0 || m_Car == null || !m_Car.IsAlive || m_Run.IsOver)
            {
                m_NextPulse = Time.time + m_Interval * 0.5f;       // the first pulse comes soon after the purchase
                return;
            }
            if (Time.time < m_NextPulse)
                return;
            m_NextPulse = Time.time + m_Interval;
            Pulse(damage);
        }

        /// <summary>Zaps every zombie in range now (also used by tests).</summary>
        public int Pulse(int damage)
        {
            m_PulseTime = Time.time;
            m_Zapped.Clear();
            int count = Physics.OverlapSphereNonAlloc(transform.position, m_Radius, s_Hits, Layers.Enemies, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = s_Hits[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || !m_Zapped.Add(target))
                    continue;
                Vector3 away = s_Hits[i].transform.position - transform.position;
                target.TakeDamage(damage, away);
            }
            AudioManager.PlaySfx(Sound.CarShock);
            AnimateRing();                                          // the ring shows from this frame on
            return m_Zapped.Count;
        }

        private void AnimateRing()
        {
            if (m_Ring == null)
                return;
            float t = (Time.time - m_PulseTime) / m_RingTime;
            bool visible = t >= 0f && t < 1f;
            m_Ring.enabled = visible;
            if (!visible)
                return;
            float size = m_Radius * 2f * Mathf.Lerp(0.3f, 1.05f, 1f - (1f - t) * (1f - t));
            m_Ring.transform.localScale = new Vector3(size, 1f, size) / Mathf.Max(0.001f, transform.lossyScale.x);
            Color c = m_RingColor;
            c.a *= 1f - t * t;
            m_Block.SetColor(s_BaseColor, c);
            m_Ring.SetPropertyBlock(m_Block);
        }
    }
}
