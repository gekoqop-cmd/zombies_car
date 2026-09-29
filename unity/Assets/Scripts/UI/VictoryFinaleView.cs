using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The HUD side of the victory finale: the fanfare, a huge "ГОРОД СПАСЁН!" that slams in and pulses, and
    /// fireworks — bursts of coloured sparks over the screen that fall and fade — which keep going behind the victory
    /// screen. Everything runs on unscaled time (the map is in slow motion, then paused).
    /// </summary>
    public sealed class VictoryFinaleView : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Text;
        [SerializeField] private TMP_Text m_Subtitle;
        [Tooltip("Where the sparks fly (the whole screen).")]
        [SerializeField] private RectTransform m_Sky;
        [SerializeField] private Image m_SparkTemplate;
        [SerializeField, Min(8)] private int m_Sparks = 160;
        [SerializeField, Min(4)] private int m_SparksPerBurst = 22;
        [SerializeField] private Vector2 m_BurstInterval = new Vector2(0.25f, 0.55f);
        [SerializeField] private Color[] m_Colors =
        {
            new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.4f, 0.3f, 1f), new Color(0.45f, 0.85f, 1f, 1f),
            new Color(0.6f, 1f, 0.45f, 1f), new Color(0.85f, 0.5f, 1f, 1f), Color.white,
        };
        [Tooltip("Seconds the big text stays before it fades (the victory screen comes then).")]
        [SerializeField, Min(0.5f)] private float m_TextTime = 3.6f;

        private struct Spark
        {
            public Image Image;
            public Vector2 Velocity;
            public float Born;
            public float Life;
        }

        private Spark[] m_Pool;
        private int m_Next;
        private float m_Start = float.NegativeInfinity;
        private float m_NextBurst;
        private bool m_Playing;

        /// <summary>The finale shows (tests).</summary>
        public bool IsPlaying => m_Playing;

        private void Awake()
        {
            m_Pool = new Spark[m_Sparks];
            for (int i = 0; i < m_Sparks; i++)
            {
                Image image = Instantiate(m_SparkTemplate, m_Sky);
                image.gameObject.SetActive(false);
                m_Pool[i] = new Spark { Image = image };
            }
            m_SparkTemplate.gameObject.SetActive(false);
            m_Group.alpha = 0f;
            m_Group.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_Run.OutcomeChanged += OnOutcome;
            OnOutcome();
        }

        private void OnDisable() => m_Run.OutcomeChanged -= OnOutcome;

        private void OnOutcome()
        {
            bool victory = m_Run.Outcome == RunOutcome.Victory;
            if (victory == m_Playing)
                return;
            m_Playing = victory;
            m_Group.gameObject.SetActive(victory);
            if (!victory)
                return;
            m_Start = Time.unscaledTime;
            m_NextBurst = m_Start + 0.3f;
            m_Subtitle.SetText($"30 ВОЛН  •  {m_Run.BossKills} БОССА  •  {MetaProgress.FormatTime(m_Run.PlayTime)}");
            AudioManager.PlaySfx(Sound.Victory);
        }

        private void Update()
        {
            if (!m_Playing)
                return;
            float t = Time.unscaledTime - m_Start;
            // The text slams in big, settles, pulses; fades when the victory screen comes.
            float fade = 1f - Mathf.Clamp01((t - m_TextTime) / 0.4f);
            m_Group.alpha = Mathf.Clamp01(t / 0.15f) * fade;
            float slam = t < 0.35f ? Mathf.Lerp(2.4f, 1f, UiMotion.CubicOut(t / 0.35f)) : 1f + 0.03f * Mathf.Sin(t * 6f);
            m_Text.localScale = Vector3.one * slam;
            m_Text.localRotation = Quaternion.Euler(0f, 0f, -4f * Mathf.Clamp01(1f - t / 0.35f));

            if (Time.unscaledTime >= m_NextBurst)
            {
                Burst();
                m_NextBurst = Time.unscaledTime + Random.Range(m_BurstInterval.x, m_BurstInterval.y);
            }
            UpdateSparks();
        }

        private void Burst()
        {
            Rect sky = m_Sky.rect;
            var center = new Vector2(Random.Range(sky.xMin + sky.width * 0.12f, sky.xMax - sky.width * 0.12f),
                Random.Range(sky.yMin + sky.height * 0.45f, sky.yMax - sky.height * 0.1f));
            Color color = m_Colors[Random.Range(0, m_Colors.Length)];
            float speed = Random.Range(280f, 480f);
            for (int i = 0; i < m_SparksPerBurst; i++)
            {
                ref Spark s = ref m_Pool[m_Next];
                m_Next = (m_Next + 1) % m_Pool.Length;
                float angle = (i + Random.value * 0.5f) / m_SparksPerBurst * Mathf.PI * 2f;
                s.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(0.75f, 1.05f);
                s.Born = Time.unscaledTime;
                s.Life = Random.Range(0.9f, 1.4f);
                s.Image.color = color;
                s.Image.rectTransform.anchoredPosition = center;
                s.Image.gameObject.SetActive(true);
            }
            AudioManager.PlaySfx(Sound.Explosion, 0.25f, Random.Range(1.5f, 1.9f));   // a high, soft pop
        }

        private void UpdateSparks()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < m_Pool.Length; i++)
            {
                ref Spark s = ref m_Pool[i];
                if (!s.Image.gameObject.activeSelf)
                    continue;
                float age = (Time.unscaledTime - s.Born) / s.Life;
                if (age >= 1f)
                {
                    s.Image.gameObject.SetActive(false);
                    continue;
                }
                s.Velocity *= 1f - 1.6f * dt;                                   // air drag: the burst opens, then hangs
                s.Velocity += Vector2.down * 260f * dt;                           // and droops
                RectTransform rt = s.Image.rectTransform;
                rt.anchoredPosition += s.Velocity * dt;
                rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.3f, age);
                Color c = s.Image.color;
                c.a = 1f - age * age;
                s.Image.color = c;
            }
        }
    }
}
