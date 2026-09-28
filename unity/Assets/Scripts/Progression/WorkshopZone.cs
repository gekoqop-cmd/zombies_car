using UnityEngine;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The spot in front of the workshop's garage door: between waves the interact key opens the workshop (car
    /// upgrades, <see cref="RunState.SetWorkshopOpen"/> — the HUD shows it and pauses the game); during a wave the
    /// prompt says it opens after the wave. A marker above the spot (wrench icon + ring on the ground) bobs and glows
    /// while the workshop can be used.
    /// </summary>
    public sealed class WorkshopZone : Interactable
    {
        private const string c_Prompt = "МАСТЕРСКАЯ: ПРОКАЧКА МАШИНЫ";
        private const string c_Closed = "МАСТЕРСКАЯ ОТКРОЕТСЯ ПОСЛЕ ВОЛНЫ";

        [SerializeField] private RunState m_Run;
        [SerializeField] private WaveState m_Waves;
        [Header("Marker")]
        [SerializeField] private Transform m_Marker;
        [SerializeField] private SpriteRenderer m_MarkerSprite;
        [SerializeField] private Renderer m_Ring;
        [SerializeField, Min(0f)] private float m_BobHeight = 0.18f;
        [SerializeField, Min(0.01f)] private float m_BobSpeed = 2.2f;
        [SerializeField] private Color m_ClosedTint = new Color(0.55f, 0.55f, 0.6f, 0.6f);

        private Vector3 m_IconBase;
        private MaterialPropertyBlock m_Block;
        private Color m_RingColor;

        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>The workshop can be opened now (between waves, the run going on).</summary>
        public bool IsOpen => m_Run != null && !m_Run.IsOver && (m_Waves == null || !m_Waves.IsWaveRunning);

        private void Awake()
        {
            if (m_Marker != null)
                m_IconBase = m_Marker.localPosition;
            m_Block = new MaterialPropertyBlock();
            if (m_Ring != null)
                m_RingColor = m_Ring.sharedMaterial.GetColor(s_BaseColor);
        }

        public override bool GetPrompt(out string text)
        {
            bool open = IsOpen;
            text = open ? c_Prompt : c_Closed;
            return open;
        }

        public override void Complete(PlayerInteractor player)
        {
            if (IsOpen)
                m_Run.SetWorkshopOpen(true);
        }

        private void LateUpdate()
        {
            bool open = IsOpen;
            float t = Time.time * m_BobSpeed;
            if (m_Marker != null)
                m_Marker.localPosition = m_IconBase + Vector3.up * (open ? Mathf.Sin(t) * m_BobHeight : 0f);
            if (m_MarkerSprite != null)
                m_MarkerSprite.color = open ? Color.white : m_ClosedTint;
            if (m_Ring != null)
            {
                Color c = m_RingColor;
                c.a *= open ? 0.75f + 0.25f * Mathf.Sin(t * 1.5f) : 0.25f;
                m_Block.SetColor(s_BaseColor, c);
                m_Ring.SetPropertyBlock(m_Block);
            }
        }
    }
}
