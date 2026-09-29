using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// UI fireworks: bursts of coloured sparks over <see cref="m_Sky"/> that open, hang, droop and fade, each with a
    /// soft pop. They go on while <see cref="Playing"/> (the victory finale) or for a while (<see cref="PlayFor"/>, a
    /// legendary skin). Runs on unscaled time.
    /// </summary>
    public sealed class UiFireworks : MonoBehaviour
    {
        [Tooltip("Where the sparks fly.")]
        [SerializeField] private RectTransform m_Sky;
        [SerializeField] private Image m_SparkTemplate;
        [SerializeField, Min(8)] private int m_Sparks = 160;
        [SerializeField, Min(4)] private int m_SparksPerBurst = 22;
        [SerializeField] private Vector2 m_BurstInterval = new Vector2(0.25f, 0.55f);
        [Tooltip("Where the bursts open: a share of the sky's height (0 bottom, 1 top).")]
        [SerializeField] private Vector2 m_BurstHeight = new Vector2(0.45f, 0.9f);
        [SerializeField] private Color[] m_Colors =
        {
            new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.4f, 0.3f, 1f), new Color(0.45f, 0.85f, 1f, 1f),
            new Color(0.6f, 1f, 0.45f, 1f), new Color(0.85f, 0.5f, 1f, 1f), Color.white,
        };

        private struct Spark
        {
            public Image Image;
            public Vector2 Velocity;
            public float Born;
            public float Life;
        }

        private Spark[] m_Pool;
        private int m_Next;
        private float m_NextBurst;
        private float m_Until = float.NegativeInfinity;
        private bool m_Forever;
        private Color[] m_Palette;

        /// <summary>Bursts keep coming (the victory finale); false lets the sparks in the air finish.</summary>
        public bool Playing
        {
            get => m_Forever || Time.unscaledTime < m_Until;
            set
            {
                if (value && !Playing)
                    m_NextBurst = Time.unscaledTime + 0.3f;
                m_Forever = value;
                if (!value)
                    m_Until = float.NegativeInfinity;
            }
        }

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
            m_Palette = m_Colors;
        }

        /// <summary>
        /// Bursts for <paramref name="seconds"/>, the first one at once; <paramref name="palette"/> (optional) are the
        /// colours to use this time (a legendary skin: gold and white).
        /// </summary>
        public void PlayFor(float seconds, Color[] palette = null)
        {
            m_Palette = palette != null && palette.Length > 0 ? palette : m_Colors;
            m_Until = Time.unscaledTime + seconds;
            m_NextBurst = Time.unscaledTime;
        }

        private void Update()
        {
            if (m_Pool == null)
                return;
            if (Playing && Time.unscaledTime >= m_NextBurst)
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
                Random.Range(sky.yMin + sky.height * m_BurstHeight.x, sky.yMin + sky.height * m_BurstHeight.y));
            Color color = m_Palette[Random.Range(0, m_Palette.Length)];
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
