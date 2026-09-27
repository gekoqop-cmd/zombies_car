using System;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Car
{
    /// <summary>
    /// Look of the Car_Player model per <see cref="CarDamageStage"/>: swaps the paint atlas material of every body
    /// part (slot <see cref="c_PaintSlot"/>; the emissive lights stay), shows the damage parts of the stage and hides
    /// the intact parts they replace (dented hood for the hood, bent bumper, fallen cargo for the roof cargo), and
    /// turns on stage effects (engine smoke). Instant, in both directions, driven by <see cref="CarHealth.StageChanged"/>.
    /// On the Ultra tier the paint is the Ultra version of the stage's material (<see cref="UltraMaterialSwap"/>
    /// leaves the paint slot to this component).
    /// </summary>
    public sealed class CarDamageView : MonoBehaviour, IMaterialSlotOwner
    {
        private const int c_PaintSlot = 0;

        [Serializable]
        private sealed class StageLook
        {
            public CarDamageStage Stage;
            public Material Paint;
            [Tooltip("Shown in this stage (and hidden in the others).")]
            public GameObject[] Show = Array.Empty<GameObject>();
            [Tooltip("Hidden in this stage (the intact parts that damage parts replace).")]
            public GameObject[] Hide = Array.Empty<GameObject>();
        }

        [SerializeField] private CarHealth m_Health;
        [Tooltip("Every renderer painted with the car atlas (material slot 0).")]
        [SerializeField] private Renderer[] m_BodyRenderers = Array.Empty<Renderer>();
        [SerializeField] private StageLook[] m_Stages = Array.Empty<StageLook>();
        [SerializeField] private UltraMaterialSet m_UltraSet;

        private readonly System.Collections.Generic.List<Material> m_Buffer = new System.Collections.Generic.List<Material>();
        private CarDamageStage m_Shown;
        private bool m_Started;

        private void OnEnable()
        {
            SettingsService.QualityChanged += OnQualityChanged;
            if (m_Health != null)
                m_Health.StageChanged += Apply;
        }

        private void Start()
        {
            m_Started = true;
            Apply(m_Health != null ? m_Health.Stage : CarDamageStage.Intact);
        }

        private void OnDisable()
        {
            SettingsService.QualityChanged -= OnQualityChanged;
            if (m_Health != null)
                m_Health.StageChanged -= Apply;
        }

        public bool OwnsSlot(Renderer renderer, int index) => index == c_PaintSlot && Array.IndexOf(m_BodyRenderers, renderer) >= 0;

        private void OnQualityChanged(QualityTier tier)
        {
            if (m_Started)
                Apply(m_Shown);
        }

        /// <summary>Shows <paramref name="stage"/> (also usable from tools to preview a stage).</summary>
        public void Apply(CarDamageStage stage)
        {
            StageLook look = null;
            foreach (StageLook s in m_Stages)
            {
                if (s.Stage == stage)
                    look = s;
            }
            if (look == null)
                return;
            m_Shown = stage;

            // Objects of the other stages first, so a part shown by this stage is not switched off again.
            foreach (StageLook other in m_Stages)
            {
                if (other == look)
                    continue;
                SetActive(other.Show, false);
                SetActive(other.Hide, true);
            }
            SetActive(look.Hide, false);
            SetActive(look.Show, true);

            if (look.Paint == null)
                return;
            Material paint = m_UltraSet != null && SettingsService.Quality >= QualityTier.Ultra ? m_UltraSet.UltraOf(look.Paint) : look.Paint;
            // Only the paint slot changes: the emissive lights keep whatever the quality tier gave them.
            foreach (Renderer body in m_BodyRenderers)
            {
                if (body == null)
                    continue;
                body.GetSharedMaterials(m_Buffer);
                if (m_Buffer.Count <= c_PaintSlot || m_Buffer[c_PaintSlot] == paint)
                    continue;
                m_Buffer[c_PaintSlot] = paint;
                body.SetSharedMaterials(m_Buffer);
            }
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            foreach (GameObject go in objects)
            {
                if (go != null && go.activeSelf != active)
                    go.SetActive(active);
            }
        }
    }
}
