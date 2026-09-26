using Unity.Cinemachine;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Sizes the Cinemachine confiner volume from the map's play area: the camera may move so that its look point
    /// (the followed player) stays inside the play area minus a margin. The volume is shifted by the follow offset
    /// because the confiner limits the camera position, not the look point.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraConfinerFromMap : MonoBehaviour
    {
        private const float c_VolumeThickness = 2f;

        [SerializeField] private MapConfig m_Map;
        [SerializeField] private CinemachineFollow m_Follow;

        private void Awake() => Apply();

        public void Apply()
        {
            if (m_Map == null || m_Follow == null)
                return;
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            Vector2 size = m_Map.PlayAreaSize - 2f * m_Map.CameraEdgeMargin * Vector2.one;
            box.center = m_Map.PlayAreaCenter + m_Follow.FollowOffset;
            box.size = new Vector3(Mathf.Max(0.1f, size.x), c_VolumeThickness, Mathf.Max(0.1f, size.y));
        }
    }
}
