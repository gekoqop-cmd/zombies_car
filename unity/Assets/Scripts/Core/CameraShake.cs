using Unity.Cinemachine;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Screen shake on the game's virtual camera (explosions, boss roars, slams): a "trauma" value that events add to
    /// and that decays; the camera is offset and rolled by Perlin noise scaled by trauma², so small bumps stay subtle
    /// and big blasts kick hard. Applied after the camera has followed and been confined (Noise stage), so it never
    /// moves the camera's resting position. Frozen while the game is paused.
    /// </summary>
    public sealed class CameraShake : CinemachineExtension
    {
        [SerializeField, Min(0f)] private float m_MaxOffset = 0.55f;
        [SerializeField, Min(0f)] private float m_MaxRoll = 2.2f;
        [SerializeField, Min(0.1f)] private float m_Frequency = 16f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField, Min(0.01f)] private float m_Decay = 1.4f;

        private static CameraShake s_Instance;
        private float m_Trauma;
        private float m_Seed;

        /// <summary>Current trauma 0..1 (tests).</summary>
        public static float Trauma => s_Instance != null ? s_Instance.m_Trauma : 0f;

        /// <summary>A shake camera is running.</summary>
        public static bool IsReady => s_Instance != null;

        /// <summary>The last shake that was added (tests: slow batch frames decay the trauma within a frame).</summary>
        public static float LastKick { get; private set; }

        /// <summary>Adds <paramref name="amount"/> (0..1) of shake.</summary>
        public static void Add(float amount)
        {
            if (s_Instance == null || amount <= 0f || !SettingsService.ShakeEnabled)
                return;
            s_Instance.m_Trauma = Mathf.Min(1f, s_Instance.m_Trauma + amount);
            LastKick = amount;
        }

        /// <summary>Adds shake that fades with the distance of <paramref name="position"/> from the camera's view centre.</summary>
        public static void AddAt(Vector3 position, float amount, float radius = 18f)
        {
            if (s_Instance == null)
                return;
            Transform follow = s_Instance.ComponentOwner != null ? s_Instance.ComponentOwner.Follow : null;
            float distance = follow != null ? Vector3.Distance(Flat(follow.position), Flat(position)) : 0f;
            Add(amount * Mathf.Clamp01(1f - distance / radius));
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        protected override void Awake()
        {
            base.Awake();
            s_Instance = this;
            m_Seed = Random.value * 100f;
        }

        protected override void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            base.OnDestroy();
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise)
                return;
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Trauma <= 0f)
                return;
            float shake = m_Trauma * m_Trauma;
            float t = Time.time * m_Frequency;
            var offset = new Vector3(Mathf.PerlinNoise(m_Seed, t) * 2f - 1f, Mathf.PerlinNoise(m_Seed + 11f, t) * 2f - 1f, 0f) * (m_MaxOffset * shake);
            float roll = (Mathf.PerlinNoise(m_Seed + 23f, t) * 2f - 1f) * m_MaxRoll * shake;
            state.PositionCorrection += state.RawOrientation * offset;
            state.OrientationCorrection *= Quaternion.Euler(0f, 0f, roll);
            m_Trauma = Mathf.Max(0f, m_Trauma - m_Decay * dt);
        }
    }
}
