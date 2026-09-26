using UnityEngine;

namespace ZombiCar.UI
{
    /// <summary>Fits a full-screen RectTransform into Screen.safeArea (notches, rounded corners, browser bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform m_Rect;
        private Rect m_Applied;
        private Vector2Int m_ScreenSize;

        private void Awake() => m_Rect = (RectTransform)transform;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != m_Applied || Screen.width != m_ScreenSize.x || Screen.height != m_ScreenSize.y)
                Apply();
        }

        private void Apply()
        {
            m_Applied = Screen.safeArea;
            m_ScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (m_ScreenSize.x <= 0 || m_ScreenSize.y <= 0)
                return;
            m_Rect.anchorMin = new Vector2(m_Applied.xMin / m_ScreenSize.x, m_Applied.yMin / m_ScreenSize.y);
            m_Rect.anchorMax = new Vector2(m_Applied.xMax / m_ScreenSize.x, m_Applied.yMax / m_ScreenSize.y);
            m_Rect.offsetMin = Vector2.zero;
            m_Rect.offsetMax = Vector2.zero;
        }
    }
}
