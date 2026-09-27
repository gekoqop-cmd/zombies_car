using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiCar.Car
{
    /// <summary>
    /// World-space durability plate above the player's car (built by SceneBuilder): icon, title, a green → yellow → red
    /// bar with the HP number. The bar eases to the new value while a light "lost" segment trails behind it, and the
    /// plate flashes on every hit. The plate always faces the camera and keeps the same size on screen whatever the
    /// camera distance or FOV; its materials skip the depth test, so it draws over the car and zombies without
    /// being cut by them.
    /// </summary>
    public sealed class CarHealthBar : MonoBehaviour
    {
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
        [Tooltip("Plate height as a fraction of the screen height.")]
        [SerializeField, Range(0.02f, 0.3f)] private float m_ScreenHeight = 0.07f;

        [Header("Animation")]
        [Tooltip("Seconds the fill takes to ease to a new value.")]
        [SerializeField, Min(0.01f)] private float m_FillTime = 0.2f;
        [SerializeField, Min(0f)] private float m_TrailDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float m_TrailTime = 0.45f;
        [SerializeField, Min(0.01f)] private float m_FlashTime = 0.3f;
        [SerializeField, Range(0f, 1f)] private float m_FlashAlpha = 0.55f;
        [Tooltip("Extra scale of the plate at the start of a flash.")]
        [SerializeField, Min(0f)] private float m_FlashPunch = 0.08f;

        private Camera m_Camera;
        private float m_Target = 1f;
        private float m_Shown = 1f;
        private float m_ShownVelocity;
        private float m_Trailing = 1f;
        private float m_TrailVelocity;
        private float m_TrailStartTime;
        private float m_FlashStartTime = float.NegativeInfinity;
        private int m_LastHealth = -1;

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

            if (m_Camera != null)
                FaceCamera(1f + m_FlashPunch * flash);
        }

        private void ApplyFill()
        {
            if (m_Fill != null)
            {
                m_Fill.fillAmount = m_Shown;
                m_Fill.color = m_Colors.Evaluate(m_Shown);
            }
            if (m_Trail != null)
                m_Trail.fillAmount = Mathf.Max(m_Trailing, m_Shown);
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
    }
}
