using Unity.Cinemachine;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Keeps the framing consistent on 16:9, 19.5:9 and 4:3: the reference vertical FOV is used for 16:9 and wider
    /// screens (wider screens simply see more to the sides), narrower screens widen the vertical FOV so the
    /// horizontal coverage of 16:9 is preserved.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class CameraAspectAdapter : MonoBehaviour
    {
        private const float c_ReferenceAspect = 16f / 9f;

        [SerializeField, Range(10f, 60f)] private float m_ReferenceVerticalFov = ViewConstants.c_CameraFov;
        [SerializeField, Range(10f, 80f)] private float m_MaxVerticalFov = ViewConstants.c_MaxCameraFov;

        private CinemachineCamera m_Camera;
        private int m_LastWidth;
        private int m_LastHeight;

        private void Awake() => m_Camera = GetComponent<CinemachineCamera>();

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.width != m_LastWidth || Screen.height != m_LastHeight)
                Apply();
        }

        private void Apply()
        {
            m_LastWidth = Screen.width;
            m_LastHeight = Screen.height;
            if (m_LastHeight <= 0)
                return;

            m_Camera.Lens.FieldOfView = VerticalFov((float)m_LastWidth / m_LastHeight, m_ReferenceVerticalFov, m_MaxVerticalFov);
        }

        /// <summary>Vertical FOV for a screen aspect: the reference FOV at 16:9 and wider, the 16:9 width on narrower screens.</summary>
        public static float VerticalFov(float aspect, float referenceVerticalFov, float maxVerticalFov)
        {
            if (aspect >= c_ReferenceAspect)
                return referenceVerticalFov;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(referenceVerticalFov * 0.5f * Mathf.Deg2Rad) * c_ReferenceAspect);
            float fov = 2f * Mathf.Atan(Mathf.Tan(halfHorizontal) / aspect) * Mathf.Rad2Deg;
            return Mathf.Min(fov, maxVerticalFov);
        }
    }
}
