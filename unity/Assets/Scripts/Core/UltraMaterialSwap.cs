using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// On the Ultra tier puts the Ultra version of every material listed in <see cref="UltraMaterialSet"/> on the
    /// renderers of this scene (asphalt, car metal, lamps); any other tier gets the regular materials back. Slots an
    /// <see cref="IMaterialSlotOwner"/> above the renderer switches itself (the car paint per damage stage) are skipped.
    /// </summary>
    public sealed class UltraMaterialSwap : MonoBehaviour, IQualityTierListener
    {
        [SerializeField] private UltraMaterialSet m_Set;

        private struct Slot
        {
            public Renderer Renderer;
            public int Index;
            public Material Regular;
            public Material Ultra;
        }

        private readonly List<Slot> m_Slots = new List<Slot>();
        private readonly List<Material> m_Buffer = new List<Material>();
        private bool m_Collected;
        private bool m_UltraApplied;

        private void OnEnable()
        {
            SettingsService.QualityChanged += ApplyQualityTier;
            ApplyQualityTier(SettingsService.Quality);
        }

        private void OnDisable() => SettingsService.QualityChanged -= ApplyQualityTier;

        public void ApplyQualityTier(QualityTier tier)
        {
            bool ultra = tier >= QualityTier.Ultra;
            if (m_Set == null || (!ultra && !m_UltraApplied))
                return;                                   // nothing to undo: the scene holds the regular materials
            Collect();
            foreach (Slot slot in m_Slots)
            {
                if (slot.Renderer == null)
                    continue;
                slot.Renderer.GetSharedMaterials(m_Buffer);
                m_Buffer[slot.Index] = ultra ? slot.Ultra : slot.Regular;
                slot.Renderer.SetSharedMaterials(m_Buffer);
            }
            m_UltraApplied = ultra;
        }

        private void Collect()
        {
            if (m_Collected)
                return;
            m_Collected = true;
            var map = new Dictionary<Material, Material>();
            foreach (UltraMaterialSet.Pair pair in m_Set.Pairs)
            {
                if (pair.Regular != null && pair.Ultra != null)
                    map[pair.Regular] = pair.Ultra;
            }
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var owner = renderer.GetComponentInParent<IMaterialSlotOwner>(true);
                renderer.GetSharedMaterials(m_Buffer);
                for (int i = 0; i < m_Buffer.Count; i++)
                {
                    if (owner != null && owner.OwnsSlot(renderer, i))
                        continue;
                    if (m_Buffer[i] != null && map.TryGetValue(m_Buffer[i], out Material ultra))
                        m_Slots.Add(new Slot { Renderer = renderer, Index = i, Regular = m_Buffer[i], Ultra = ultra });
                }
            }
        }
    }
}
