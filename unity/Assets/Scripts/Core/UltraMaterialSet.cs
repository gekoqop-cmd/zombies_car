using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Pairs of materials: the regular one (Simple Lit, used by Low / Medium / High) and its Ultra version
    /// (URP Lit: normal map, smoothness and reflections, stronger emission for bloom halos).
    /// </summary>
    [CreateAssetMenu(fileName = "UltraMaterialSet", menuName = "ZombiCar/Ultra Material Set")]
    public sealed class UltraMaterialSet : ScriptableObject
    {
        [Serializable]
        public struct Pair
        {
            public Material Regular;
            public Material Ultra;
        }

        [SerializeField] private Pair[] m_Pairs = Array.Empty<Pair>();

        public Pair[] Pairs => m_Pairs;

        /// <summary>The Ultra version of <paramref name="regular"/>, or <paramref name="regular"/> itself if it has none.</summary>
        public Material UltraOf(Material regular)
        {
            foreach (Pair pair in m_Pairs)
            {
                if (pair.Regular == regular && pair.Ultra != null)
                    return pair.Ultra;
            }
            return regular;
        }

#if UNITY_EDITOR
        public void EditorSetPairs(Pair[] pairs) => m_Pairs = pairs;
#endif
    }
}
