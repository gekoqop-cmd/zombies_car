using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Turns a vertical sprite quad to face the camera around the world Y axis only (no tilt, no roll).
    /// The camera yaw is fixed in this game, so aligning once is enough; enable per-frame updates for cameras that orbit.
    /// </summary>
    public sealed class YAxisBillboard : MonoBehaviour
    {
        private const float c_MinPlanarLength = 1e-4f;

        [SerializeField] private bool m_UpdateEveryFrame;

        private Transform m_Camera;

        private void Start() => Align();

        private void LateUpdate()
        {
            if (m_UpdateEveryFrame)
                Align();
        }

        private void Align()
        {
            if (m_Camera == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;
                m_Camera = cam.transform;
            }
            Vector3 forward = m_Camera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < c_MinPlanarLength)
                return;
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
