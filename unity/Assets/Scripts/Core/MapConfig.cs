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
        [Tooltip("Asphalt ground under the whole view: covers what the game camera can see from any point of its " +
                 "confined range on 4:3 .. 21:9 screens (x +-44, z -22.5..32 on Map01) plus a small margin.")]
        [SerializeField] private Vector3 m_GroundCenter = new Vector3(0f, 0f, 5f);
        [SerializeField] private Vector2 m_GroundSize = new Vector2(92f, 58f);
        [Tooltip("Radius of the empty circle around the car in the middle of the map.")]
        [SerializeField, Min(0f)] private float m_FreeZoneRadius = 8f;
        [Tooltip("How far inside the play area the camera look point is allowed to go.")]
        [SerializeField, Min(0f)] private float m_CameraEdgeMargin = 4f;
        [Tooltip("Multiplies the character sprites' colour (they are unlit): white by day, cool at night.")]
        [SerializeField] private Color m_CharacterTint = Color.white;

        [Header("Music")]
        [SerializeField] private AudioClip m_Music;
        [Tooltip("Plays while a boss is alive.")]
        [SerializeField] private AudioClip m_BossMusic;

        public string SceneName => m_SceneName;
        public int MapNumber => m_MapNumber;
        public int WaveThreshold => m_WaveThreshold;
        public Vector3 PlayAreaCenter => m_PlayAreaCenter;
        public Vector2 PlayAreaSize => m_PlayAreaSize;
        public Vector3 GroundCenter => m_GroundCenter;
        public Vector2 GroundSize => m_GroundSize;
        public float FreeZoneRadius => m_FreeZoneRadius;
        public float CameraEdgeMargin => m_CameraEdgeMargin;
        public Color CharacterTint => m_CharacterTint;
        public AudioClip Music => m_Music;
        public AudioClip BossMusic => m_BossMusic;

        public Bounds PlayAreaBounds => new Bounds(m_PlayAreaCenter, new Vector3(m_PlayAreaSize.x, 0f, m_PlayAreaSize.y));
    }
}
