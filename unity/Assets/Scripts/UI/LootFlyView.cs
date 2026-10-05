using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// A pickup taken on the map (<see cref="RunState.LootCollected"/>) flies on the HUD from where it was to its
    /// counter — scrap to the scrap counter, an ammo box to the ammo panel — in an arc, shrinking; the scrap counter
    /// counts each piece when it lands (<see cref="RunState.LootLanded"/>). Runs on unscaled time.
    /// </summary>
    public sealed class LootFlyView : MonoBehaviour
    {
        private sealed class Flight
        {
            public RectTransform Rect;
            public Image Image;
            public LootKind Kind;
            public int Amount;
            public Vector2 From, Control, To;
            public float Start;
            public bool Active;
        }

        [SerializeField] private RunState m_Run;
        [Tooltip("The full-screen rect the icons fly in (the HUD's safe area).")]
        [SerializeField] private RectTransform m_Space;
        [SerializeField] private RectTransform m_ScrapTarget;
        [SerializeField] private RectTransform m_AmmoTarget;
        [SerializeField] private Sprite m_ScrapSprite;
        [SerializeField] private Sprite m_AmmoSprite;
        [SerializeField, Min(0.05f)] private float m_FlyTime = 0.6f;
        [SerializeField, Min(8f)] private float m_Size = 72f;
        [Tooltip("Pieces picked up together leave one after another.")]
        [SerializeField, Min(0f)] private float m_Stagger = 0.06f;

        private readonly List<Flight> m_Flights = new List<Flight>();
        private float m_LastStart;

        /// <summary>Icons flying now (tests).</summary>
        public int Flying
        {
            get
            {
                int n = 0;
                foreach (Flight f in m_Flights)
                    n += f.Active ? 1 : 0;
                return n;
            }
        }

        private void OnEnable()
        {
            if (m_Run != null)
                m_Run.LootCollected += OnLoot;
        }

        private void OnDisable()
        {
            if (m_Run != null)
                m_Run.LootCollected -= OnLoot;
            foreach (Flight f in m_Flights)
                Land(f);                                             // nothing stays uncounted
        }

        private void OnLoot(LootKind kind, int amount, Vector3 world)
        {
            Camera cam = Camera.main;
            RectTransform target = kind == LootKind.Ammo ? m_AmmoTarget : m_ScrapTarget;
            if (cam == null || target == null || m_Space == null)
            {
                if (kind == LootKind.Scrap)
                    m_Run.LootLanded(amount);
                return;
            }
            Vector2 screen = cam.WorldToScreenPoint(world);
            Canvas canvas = m_Space.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Space, screen, uiCamera, out Vector2 from);
            Vector2 to = m_Space.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Flight flight = Take();
            flight.Kind = kind;
            flight.Amount = amount;
            flight.From = from;
            flight.To = to;
            flight.Control = Vector2.Lerp(from, to, 0.35f) + new Vector2(Random.Range(-140f, 140f), 220f);
            float now = Time.unscaledTime;
            flight.Start = Mathf.Max(now, m_LastStart + m_Stagger);
            m_LastStart = flight.Start;
            flight.Image.sprite = kind == LootKind.Ammo ? m_AmmoSprite : m_ScrapSprite;
            flight.Rect.anchoredPosition = from;
            flight.Rect.localScale = Vector3.zero;
            flight.Active = true;
            flight.Rect.gameObject.SetActive(true);
        }

        private Flight Take()
        {
            foreach (Flight f in m_Flights)
                if (!f.Active)
                    return f;
            var go = new GameObject("LootIcon", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(m_Space, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(m_Size, m_Size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            var flight = new Flight { Rect = rt, Image = image };
            m_Flights.Add(flight);
            return flight;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            foreach (Flight f in m_Flights)
            {
                if (!f.Active)
                    continue;
                float t = (now - f.Start) / m_FlyTime;
                if (t < 0f)
                    continue;
                if (t >= 1f)
                {
                    Land(f);
                    continue;
                }
                float e = t * t;                                      // slow off the ground, fast into the counter
                Vector2 a = Vector2.Lerp(f.From, f.Control, e), b = Vector2.Lerp(f.Control, f.To, e);
                f.Rect.anchoredPosition = Vector2.Lerp(a, b, e);
                float pop = Mathf.Clamp01(t / 0.15f);
                f.Rect.localScale = Vector3.one * Mathf.Lerp(0f, 1.25f, pop) * Mathf.Lerp(1f, 0.55f, t);
            }
        }

        private void Land(Flight f)
        {
            if (!f.Active)
                return;
            f.Active = false;
            f.Rect.gameObject.SetActive(false);
            if (f.Kind == LootKind.Scrap && m_Run != null)
                m_Run.LootLanded(f.Amount);
        }
    }
}
