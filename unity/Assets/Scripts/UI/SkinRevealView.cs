using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// The prize of a case, shown big after the roulette: the skin's plate leans after the cursor (or a finger) while it
    /// is over it — never more than a tilt, it springs back when let go — a glossy band sweeps over it and a glow breathes
    /// behind it, both in the rarity's colour; the rarer the skin, the brighter the shine, epic and legendary get rays,
    /// and a legendary one sets off fireworks with a fanfare.
    /// Runs on unscaled time.
    /// </summary>
    public sealed class SkinRevealView : MonoBehaviour
    {
        [SerializeField] private RectTransform m_Card;
        [SerializeField] private Image m_Plate;
        [SerializeField] private Image m_Icon;
        [SerializeField] private RectTransform m_Shine;
        [SerializeField] private Graphic m_ShineGraphic;
        [SerializeField] private Graphic m_Glow;
        [SerializeField] private RectTransform m_Rays;
        [SerializeField] private Graphic m_RaysGraphic;
        [Tooltip("Largest lean, degrees.")]
        [SerializeField, Range(0f, 45f)] private float m_MaxTilt = 22f;
        [Tooltip("Seconds between two sweeps of the shine.")]
        [SerializeField, Min(0.3f)] private float m_ShinePeriod = 2.2f;
        [Tooltip("A legendary skin sets off fireworks (with its own fanfare) for this many seconds.")]
        [SerializeField, OptionalReference] private UiFireworks m_Fireworks;
        [SerializeField, Min(0.5f)] private float m_LegendaryFireworks = 4.5f;

        private static readonly Color[] s_LegendaryColors =
        {
            new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.7f, 0.2f, 1f), new Color(1f, 0.95f, 0.7f, 1f), Color.white,
            new Color(1f, 0.5f, 0.2f, 1f),
        };

        private Color m_Rarity;
        private float m_ShineStrength;
        private float m_ShownAt;
        private Vector2 m_Tilt;
        private Vector2 m_TiltVelocity;

        /// <summary>The legendary fireworks are going (tests).</summary>
        public bool FireworksPlaying => m_Fireworks != null && m_Fireworks.Playing;

        /// <summary>Puts <paramref name="item"/> on the plate and pops it in (a legendary one with fireworks).</summary>
        public void Show(CosmeticItem item, Sprite slotIcon)
        {
            m_Rarity = CosmeticCatalog.RarityColor(item.Rarity);
            m_ShineStrength = item.Rarity switch
            {
                Rarity.Legendary => 0.95f,
                Rarity.Epic => 0.75f,
                Rarity.Rare => 0.55f,
                _ => 0.35f,
            };
            m_Plate.color = m_Rarity * new Color(0.6f, 0.6f, 0.6f, 1f);
            m_Icon.sprite = slotIcon;
            m_Icon.color = item.Color;
            m_Rays.gameObject.SetActive(item.Rarity >= Rarity.Epic);
            if (item.Rarity == Rarity.Legendary)
            {
                if (m_Fireworks != null)
                    m_Fireworks.PlayFor(m_LegendaryFireworks, s_LegendaryColors);
                AudioManager.PlaySfx(Sound.LegendaryDrop);
            }
            m_ShownAt = Time.unscaledTime;
            m_Tilt = m_TiltVelocity = Vector2.zero;
            gameObject.SetActive(true);
            Update();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (m_Fireworks != null)
                m_Fireworks.Playing = false;
        }

        private void Update()
        {
            float t = Time.unscaledTime - m_ShownAt;
            m_Card.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, UiMotion.BackOut(Mathf.Clamp01(t / 0.45f)));

            // Lean towards the pointer while it touches the card; a slow idle sway otherwise.
            Vector2 target = new Vector2(Mathf.Sin(t * 1.3f) * 3f, Mathf.Cos(t * 1.1f) * 2f);
            if (PointerOver(out Vector2 local))
            {
                Vector2 half = m_Card.rect.size * 0.5f;
                Vector2 n = new Vector2(Mathf.Clamp(local.x / half.x, -1f, 1f), Mathf.Clamp(local.y / half.y, -1f, 1f));
                target = new Vector2(-n.y, n.x) * m_MaxTilt;              // x: lean back / forward, y: turn left / right
            }
            m_Tilt = Vector2.SmoothDamp(m_Tilt, target, ref m_TiltVelocity, 0.12f, Mathf.Infinity, Time.unscaledDeltaTime);
            m_Card.localRotation = Quaternion.Euler(m_Tilt.x, m_Tilt.y, 0f);

            // The shine sweeps across; the tilt pushes it along like light on foil.
            float width = m_Card.rect.width;
            float sweep = Mathf.Repeat(t / m_ShinePeriod, 1f);
            m_Shine.anchoredPosition = new Vector2(Mathf.Lerp(-width, width, sweep) + m_Tilt.y / m_MaxTilt * width * 0.35f, 0f);
            Color shine = Color.Lerp(Color.white, m_Rarity, 0.35f);
            shine.a = m_ShineStrength * Mathf.Sin(sweep * Mathf.PI);
            m_ShineGraphic.color = shine;

            Color glow = m_Rarity;
            glow.a = 0.45f + 0.25f * m_ShineStrength * (0.5f + 0.5f * Mathf.Sin(t * 3f));
            m_Glow.color = glow;
            m_Glow.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 3f));
            if (m_Rays.gameObject.activeSelf)
            {
                m_Rays.localRotation = Quaternion.Euler(0f, 0f, -t * 18f);
                Color rays = m_Rarity;
                rays.a = 0.55f * m_ShineStrength;
                m_RaysGraphic.color = rays;
            }
        }

        private bool PointerOver(out Vector2 local)
        {
            local = Vector2.zero;
            Pointer pointer = Pointer.current;
            if (pointer == null)
                return false;
            if (pointer is Touchscreen touch && !touch.primaryTouch.press.isPressed)
                return false;
            Vector2 screen = pointer.position.ReadValue();
            // Measured against the upright card so the lean does not move the target under the pointer.
            RectTransform parent = (RectTransform)m_Card.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 inParent))
                return false;
            local = inParent - (Vector2)m_Card.localPosition;
            Vector2 half = m_Card.rect.size * 0.5f * 1.15f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }
    }
}
