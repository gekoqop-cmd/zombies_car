using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Enemies;

namespace ZombiCar.Car
{
    /// <summary>
    /// Compact world-space durability plate on the player's car (built by SceneBuilder): the car icon and a green →
    /// yellow → red bar with the HP number, one row, over the middle of the car body so it covers the car rather than the
    /// ground around it. With <c>Stage Colours</c> on, the bar's colour steps at the damage-stage thresholds of
    /// <see cref="CarConfig"/> (absolute HP, so it agrees with the look of the car body also after the hull upgrades
    /// raise the maximum). The bar eases to the new value while a light "lost" segment trails behind it, and the plate
    /// flashes on every hit. Translucent while nothing happens, fully shown for a moment after a hit, and nearly
    /// invisible while a zombie or the player is under it on screen (it draws over everything: its materials skip the
    /// depth test). Always faces the camera and keeps the same size on screen.
    /// </summary>
    public sealed class CarHealthBar : MonoBehaviour
    {
        // Stage colours: each stage fades from its low end to its high end.
        private static readonly Color s_WreckedLow = new Color(0.78f, 0.14f, 0.12f);
        private static readonly Color s_WreckedHigh = new Color(0.95f, 0.36f, 0.14f);
        private static readonly Color s_DamagedLow = new Color(0.98f, 0.6f, 0.16f);
        private static readonly Color s_DamagedHigh = new Color(0.97f, 0.8f, 0.2f);
        private static readonly Color s_IntactLow = new Color(0.46f, 0.82f, 0.28f);
        private static readonly Color s_IntactHigh = new Color(0.34f, 0.82f, 0.3f);

        [SerializeField] private CarHealth m_Car;
        [Tooltip("The canvas; its height is what is kept constant on screen.")]
        [SerializeField] private RectTransform m_Plate;
        [SerializeField] private Image m_Fill;
        [Tooltip("Segment of health just lost; follows the fill after a short delay.")]
        [SerializeField] private Image m_Trail;
        [SerializeField] private Image m_Flash;
        [SerializeField] private TMP_Text m_Value;
        [SerializeField] private string m_ValueFormat = "{0} / {1}";

        [Header("Look")]
        [SerializeField] private Gradient m_Colors = DefaultColors();
        [Tooltip("Colour the bar by the car's damage stages (CarConfig thresholds) instead of Colors.")]
        [SerializeField] private bool m_StageColours = true;
        [Tooltip("Plate height as a fraction of the screen height.")]
        [SerializeField, Range(0.02f, 0.3f)] private float m_ScreenHeight = 0.042f;

        [Header("Visibility")]
        [Tooltip("Fades the whole plate.")]
        [SerializeField] private CanvasGroup m_Group;
        [Tooltip("The player: the plate clears out of the way when the player is under it on screen.")]
        [SerializeField] private Transform m_Player;
        [Tooltip("Alpha while nothing happens.")]
        [SerializeField, Range(0f, 1f)] private float m_IdleAlpha = 0.8f;
        [Tooltip("Alpha while a zombie or the player is under the plate on screen.")]
        [SerializeField, Range(0f, 1f)] private float m_UncoverAlpha = 0.15f;
        [Tooltip("Seconds the plate is fully shown after a hit (unless something is under it).")]
        [SerializeField, Min(0f)] private float m_HitShowTime = 2f;
        [Tooltip("Alpha change per second.")]
        [SerializeField, Min(0.01f)] private float m_FadeSpeed = 6f;
        [Tooltip("Sprite height of the player, metres (the pitch-stretched look); zombies use their own.")]
        [SerializeField, Min(0f)] private float m_PlayerHeight = 2.7f;
        [Tooltip("Half the width of a character sprite, metres.")]
        [SerializeField, Min(0f)] private float m_CharacterHalfWidth = 0.45f;

        [Header("Animation")]
        [Tooltip("Seconds the fill takes to ease to a new value.")]
        [SerializeField, Min(0.01f)] private float m_FillTime = 0.2f;
        [SerializeField, Min(0f)] private float m_TrailDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float m_TrailTime = 0.45f;
        [SerializeField, Min(0.01f)] private float m_FlashTime = 0.3f;
        [SerializeField, Range(0f, 1f)] private float m_FlashAlpha = 0.55f;
        [Tooltip("Extra scale of the plate at the start of a flash.")]
        [SerializeField, Min(0f)] private float m_FlashPunch = 0.08f;

        private readonly Vector3[] m_Corners = new Vector3[4];
        private Camera m_Camera;
        private float m_Target = 1f;
        private float m_Shown = 1f;
        private float m_ShownVelocity;
        private float m_Trailing = 1f;
        private float m_TrailVelocity;
        private float m_TrailStartTime;
        private float m_FlashStartTime = float.NegativeInfinity;
        private int m_LastHealth = -1;

        /// <summary>Something (a zombie, the player) is under the plate on screen: it is faded out of the way.</summary>
        public bool IsCovering { get; private set; }

        /// <summary>Current alpha of the plate (tests).</summary>
        public float Alpha => m_Group != null ? m_Group.alpha : 1f;

        /// <summary>Colour of the bar now (tests).</summary>
        public Color FillColor => m_Fill != null ? m_Fill.color : Color.clear;

        private static Gradient DefaultColors()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.86f, 0.2f, 0.15f), 0f),
                    new GradientColorKey(new Color(0.96f, 0.78f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(0.36f, 0.82f, 0.3f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private void OnEnable()
        {
            if (m_Car != null)
                m_Car.Changed += OnChanged;
            OnChanged();
            m_Shown = m_Trailing = m_Target;
            ApplyFill();
            if (m_Group != null)
                m_Group.alpha = m_IdleAlpha;
        }

        // CarHealth sets its health in Awake, which may run after this OnEnable: read it again and show it without easing.
        private void Start()
        {
            m_LastHealth = -1;
            OnChanged();
            m_Shown = m_Trailing = m_Target;
            ApplyFill();
        }

        private void OnDisable()
        {
            if (m_Car != null)
                m_Car.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            if (m_Car == null)
                return;
            int health = m_Car.Health;
            m_Target = m_Car.MaxHealth > 0 ? (float)health / m_Car.MaxHealth : 0f;
            if (m_Value != null)
                m_Value.SetText(string.Format(m_ValueFormat, health, m_Car.MaxHealth));
            if (m_LastHealth >= 0 && health < m_LastHealth)
            {
                m_FlashStartTime = Time.time;
                m_TrailStartTime = Time.time + m_TrailDelay;
            }
            m_LastHealth = health;
        }

        private void LateUpdate()
        {
            if (m_Camera == null)
                m_Camera = Camera.main;

            m_Shown = Mathf.SmoothDamp(m_Shown, m_Target, ref m_ShownVelocity, m_FillTime);
            if (m_Trailing < m_Shown || Time.time >= m_TrailStartTime)
                m_Trailing = Mathf.SmoothDamp(m_Trailing, m_Shown, ref m_TrailVelocity, m_TrailTime);
            ApplyFill();

            float flash = 1f - Mathf.Clamp01((Time.time - m_FlashStartTime) / m_FlashTime);
            if (m_Flash != null)
            {
                Color c = m_Flash.color;
                c.a = flash * flash * m_FlashAlpha;
                m_Flash.color = c;
            }

            if (m_Camera == null)
                return;
            FaceCamera(1f + m_FlashPunch * flash);
            IsCovering = Covers();
            if (m_Group != null)
            {
                bool hit = Time.time - m_FlashStartTime < m_HitShowTime;
                float target = IsCovering ? m_UncoverAlpha : hit ? 1f : m_IdleAlpha;
                m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, target, m_FadeSpeed * Time.deltaTime);
            }
        }

        private void ApplyFill()
        {
            if (m_Fill != null)
            {
                SetAmount(m_Fill, m_Shown);
                m_Fill.color = BarColour(m_Shown);
            }
            if (m_Trail != null)
                SetAmount(m_Trail, Mathf.Max(m_Trailing, m_Shown));
        }

        // Stage colours by absolute health (the thresholds do not move when the maximum grows); Colors otherwise.
        private Color BarColour(float fraction)
        {
            CarConfig config = m_Car != null ? m_Car.Config : null;
            if (!m_StageColours || config == null)
                return m_Colors.Evaluate(fraction);
            float health = fraction * m_Car.MaxHealth;
            float damaged = config.DamagedAtOrBelow;
            float wrecked = config.WreckedAtOrBelow;
            if (health > damaged)
                return Color.Lerp(s_IntactLow, s_IntactHigh, Mathf.InverseLerp(damaged, Mathf.Max(damaged + 1f, m_Car.MaxHealth), health));
            if (health > wrecked)
                return Color.Lerp(s_DamagedLow, s_DamagedHigh, Mathf.InverseLerp(wrecked, damaged, health));
            return Color.Lerp(s_WreckedLow, s_WreckedHigh, Mathf.InverseLerp(0f, wrecked, health));
        }

        // Filled images use fillAmount; sliced (rounded) bars change width, so their round ends are never cut off.
        private static void SetAmount(Image image, float amount)
        {
            if (image.type == Image.Type.Filled)
            {
                image.fillAmount = amount;
                return;
            }
            RectTransform rt = image.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(amount), rt.anchorMax.y);
            image.enabled = amount > 0.001f;
        }

        // Parallel to the screen and scaled so the plate is m_ScreenHeight of the view at its distance.
        private void FaceCamera(float punch)
        {
            Transform cam = m_Camera.transform;
            transform.rotation = cam.rotation;
            float viewHeight = m_Camera.orthographic
                ? 2f * m_Camera.orthographicSize
                : 2f * Vector3.Dot(transform.position - cam.position, cam.forward) * Mathf.Tan(0.5f * m_Camera.fieldOfView * Mathf.Deg2Rad);
            float plateHeight = m_Plate != null ? m_Plate.rect.height : 1f;
            float scale = Mathf.Max(0.001f, viewHeight * m_ScreenHeight / plateHeight) * punch;
            Vector3 parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;   // the car's import scale
            transform.localScale = new Vector3(scale / parent.x, scale / parent.y, scale / parent.z);
        }

        // The plate's rectangle on screen against the sprites of the player and the zombies (feet to the top of the head).
        private bool Covers()
        {
            if (m_Plate == null)
                return false;
            m_Plate.GetWorldCorners(m_Corners);
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Vector3 corner in m_Corners)
            {
                Vector3 p = m_Camera.WorldToScreenPoint(corner);
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
                yMin = Mathf.Min(yMin, p.y);
                yMax = Mathf.Max(yMax, p.y);
            }
            Rect plate = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            if (m_Player != null && m_Player.gameObject.activeInHierarchy && Overlaps(plate, m_Player.position, m_PlayerHeight))
                return true;
            foreach (ZombieController zombie in ZombieController.Active)
            {
                if (zombie.IsAlive && Overlaps(plate, zombie.transform.position, zombie.VisualHeight))
                    return true;
            }
            return false;
        }

        private bool Overlaps(Rect plate, Vector3 feet, float height)
        {
            Vector3 bottom = m_Camera.WorldToScreenPoint(feet);
            if (bottom.z <= 0f)
                return false;
            Vector3 top = m_Camera.WorldToScreenPoint(feet + Vector3.up * height);
            float half = Mathf.Abs(m_Camera.WorldToScreenPoint(feet + m_Camera.transform.right * m_CharacterHalfWidth).x - bottom.x);
            Rect body = Rect.MinMaxRect(Mathf.Min(bottom.x, top.x) - half, Mathf.Min(bottom.y, top.y),
                Mathf.Max(bottom.x, top.x) + half, Mathf.Max(bottom.y, top.y));
            return body.Overlaps(plate);
        }
    }
}
