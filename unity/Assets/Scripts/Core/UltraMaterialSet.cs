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

#if UNITY_EDITOR
        public void EditorSetPairs(Pair[] pairs) => m_Pairs = pairs;
#endif
    }
}
