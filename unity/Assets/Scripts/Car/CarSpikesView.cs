using UnityEngine;
using ZombiCar.Progression;

namespace ZombiCar.Car
{
    /// <summary>
    /// The workshop's "spikes" upgrade on the car model: one set of steel spikes per level (front bumper, rear bumper,
    /// the sides, a second row), built by the prefab builder and hidden until bought. A new set pops out of the body.
    /// Follows <see cref="CarHealth.Spikes"/> (damage per hit = level x the upgrade's damage per level), so a new run
    /// starts bare.
    /// </summary>
    public sealed class CarSpikesView : MonoBehaviour
    {
        [SerializeField] private CarHealth m_Car;
        [SerializeField] private RunState m_Run;
        [Tooltip("Spike sets in the order they are bought.")]
        [SerializeField] private GameObject[] m_Tiers = System.Array.Empty<GameObject>();
        [Tooltip("Spike damage one workshop level adds (CarUpgrades 'spikes').")]
        [SerializeField, Min(1)] private int m_DamagePerLevel = 8;
        [SerializeField, Min(0.01f)] private float m_PopTime = 0.35f;

        private float[] m_PopStart;
        private int m_Shown;

        private void Awake()
        {
            m_PopStart = new float[m_Tiers.Length];
            for (int i = 0; i < m_Tiers.Length; i++)
            {
                m_PopStart[i] = float.NegativeInfinity;
                if (m_Tiers[i] != null)
                    m_Tiers[i].SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (m_Run != null)
                m_Run.ModifiersChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (m_Run != null)
                m_Run.ModifiersChanged -= Refresh;
        }

        private void Refresh()
        {
            if (m_Car == null || m_PopStart == null)
                return;
            int levels = Mathf.Clamp(m_Car.Spikes / m_DamagePerLevel, 0, m_Tiers.Length);
            for (int i = 0; i < m_Tiers.Length; i++)
            {
                if (m_Tiers[i] == null)
                    continue;
                bool show = i < levels;
                if (show && !m_Tiers[i].activeSelf)
                    m_PopStart[i] = Time.time;
                m_Tiers[i].SetActive(show);
                if (!show)
                    ScaleSpikes(m_Tiers[i].transform, 1f);
            }
            m_Shown = levels;
        }

        // A bought set grows out of the body with a small overshoot (every spike from its own base).
        private void Update()
        {
            for (int i = 0; i < m_Shown; i++)
            {
                float t = (Time.time - m_PopStart[i]) / m_PopTime;
                if (m_Tiers[i] == null || t < 0f || t > 1.2f)
                    continue;
                t = Mathf.Clamp01(t);
                float s = t < 1f ? Mathf.Lerp(0.05f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.25f : 1f;
                ScaleSpikes(m_Tiers[i].transform, s);
            }
        }

        private static void ScaleSpikes(Transform tier, float scale)
        {
            foreach (Transform spike in tier)
                spike.localScale = Vector3.one * scale;
        }
    }
}
