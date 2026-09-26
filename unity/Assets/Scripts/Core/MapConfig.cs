using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>Static description of one map: scene, number, wave goal and the size of the playable/visual area.</summary>
    [CreateAssetMenu(fileName = "MapConfig", menuName = "ZombiCar/Map Config")]
    public sealed class MapConfig : ScriptableObject
    {
        [SerializeField] private string m_SceneName = SceneNames.c_Map01;
        [SerializeField, Min(1)] private int m_MapNumber = 1;
        [Tooltip("Waves the player has to survive to complete the map.")]
        [SerializeField, Min(1)] private int m_WaveThreshold = 10;

        [Header("Areas (metres, X = east, Y = north)")]
        [SerializeField] private Vector3 m_PlayAreaCenter = Vector3.zero;
        [SerializeField] private Vector2 m_PlayAreaSize = new Vector2(44f, 30f);
        [SerializeField] private Vector2 m_GroundSize = new Vector2(100f, 80f);
        [Tooltip("Radius of the empty circle around the car in the middle of the map.")]
        [SerializeField, Min(0f)] private float m_FreeZoneRadius = 8f;
        [Tooltip("How far inside the play area the camera look point is allowed to go.")]
        [SerializeField, Min(0f)] private float m_CameraEdgeMargin = 4f;

        public string SceneName => m_SceneName;
        public int MapNumber => m_MapNumber;
        public int WaveThreshold => m_WaveThreshold;
        public Vector3 PlayAreaCenter => m_PlayAreaCenter;
        public Vector2 PlayAreaSize => m_PlayAreaSize;
        public Vector2 GroundSize => m_GroundSize;
        public float FreeZoneRadius => m_FreeZoneRadius;
        public float CameraEdgeMargin => m_CameraEdgeMargin;

        public Bounds PlayAreaBounds => new Bounds(m_PlayAreaCenter, new Vector3(m_PlayAreaSize.x, 0f, m_PlayAreaSize.y));
    }
}
