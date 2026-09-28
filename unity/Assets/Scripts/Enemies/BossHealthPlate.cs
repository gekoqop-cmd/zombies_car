using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// World-space health plate over a boss (like the car's): its name and a red bar with the health number; the bar
    /// eases down with a light "lost" trail and the plate flashes on hits. It faces the camera, keeps the same size on
    /// screen at any distance, turns orange-red in the enraged phase and fades out when the boss dies.
    /// </summary>
    public sealed class BossHealthPlate : MonoBehaviour
    {
        [SerializeField] private ZombieController m_Boss;
        [SerializeField] private BossBrain m_Brain;
        [SerializeField] private RectTransform m_Plate;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private Image m_Fill;
        [SerializeField] private Image m_Trail;
        [SerializeField] private Image m_Flash;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Value;
        [SerializeField] private Color m_FillColor = new Color(0.86f, 0.18f, 0.16f, 1f);
        [SerializeField] private Color m_EnragedColor = new Color(1f, 0.45f, 0.12f, 1f);
        [SerializeField, Range(0.02f, 0.3f)] private float m_ScreenHeight = 0.07f;
        [SerializeField, Min(0.01f)] private float m_FillTime = 0.15f;
        [SerializeField, Min(0f)] private float m_TrailDelay = 0.3f;
        [SerializeField, Min(0.01f)] private float m_TrailTime = 0.4f;

        private Camera m_Camera;
        private float m_Shown = 1f;
        private float m_ShownVelocity;
        private float m_Trailing = 1f;
        private float m_TrailVelocity;
        private float m_TrailStart;
        private float m_FlashStart = float.NegativeInfinity;
        private int m_LastHealth = -1;
        private string m_LastName;

        private void OnEnable()
        {
            m_Boss.Spawned += OnSpawned;
            m_Boss.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            m_Boss.Spawned -= OnSpawned;
            m_Boss.Damaged -= OnDamaged;
        }

        private void OnSpawned(ZombieController boss)
        {
            m_Shown = m_Trailing = 1f;
            m_LastHealth = -1;
            m_Group.alpha = 1f;
        }

        private void OnDamaged(ZombieController boss, int damage)
        {
            m_FlashStart = Time.time;
            m_TrailStart = Time.time + m_TrailDelay;
        }

        private void LateUpdate()
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
            if (m_Boss.Config == null)
                return;                                                // pooled, not spawned yet
            string title = m_Boss.Config.DisplayName;
            if (title != m_LastName)
            {
                m_LastName = title;
                m_Title.text = title;
            }
            int health = Mathf.Max(0, m_Boss.Health);
            if (health != m_LastHealth)
            {
                m_LastHealth = health;
                m_Value.text = $"{health} / {m_Boss.MaxHealth}";
            }
            float target = m_Boss.MaxHealth > 0 ? (float)health / m_Boss.MaxHealth : 0f;
            m_Shown = Mathf.SmoothDamp(m_Shown, target, ref m_ShownVelocity, m_FillTime);
            if (m_Trailing < m_Shown || Time.time >= m_TrailStart)
                m_Trailing = Mathf.SmoothDamp(m_Trailing, m_Shown, ref m_TrailVelocity, m_TrailTime);
            SetAmount(m_Fill, m_Shown);
            SetAmount(m_Trail, Mathf.Max(m_Trailing, m_Shown));
            m_Fill.color = m_Brain != null && m_Brain.IsEnraged ? m_EnragedColor : m_FillColor;
            float flash = 1f - Mathf.Clamp01((Time.time - m_FlashStart) / 0.25f);
            Color f = m_Flash.color;
            f.a = flash * flash * 0.5f;
            m_Flash.color = f;
            if (!m_Boss.IsAlive)
                m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, 0f, Time.deltaTime * 1.5f);
            if (m_Camera != null)
                FaceCamera();
        }

        private static void SetAmount(Image image, float amount)
        {
            RectTransform rt = image.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(amount), rt.anchorMax.y);
            image.enabled = amount > 0.001f;
        }

        // Parallel to the screen and scaled so the plate is m_ScreenHeight of the view at its distance.
        private void FaceCamera()
        {
            Transform cam = m_Camera.transform;
            transform.rotation = cam.rotation;
            float viewHeight = m_Camera.orthographic
                ? 2f * m_Camera.orthographicSize
                : 2f * Vector3.Dot(transform.position - cam.position, cam.forward) * Mathf.Tan(0.5f * m_Camera.fieldOfView * Mathf.Deg2Rad);
            float scale = Mathf.Max(0.001f, viewHeight * m_ScreenHeight / m_Plate.rect.height);
            transform.localScale = new Vector3(scale, scale, scale);
        }
    }
}
