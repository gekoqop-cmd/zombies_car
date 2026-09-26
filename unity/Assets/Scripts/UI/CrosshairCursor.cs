using UnityEngine;

namespace ZombiCar.UI
{
    /// <summary>Replaces the system cursor with a crosshair while this object is active (PC only).</summary>
    public sealed class CrosshairCursor : MonoBehaviour
    {
        [SerializeField] private Texture2D m_Crosshair;
        [Tooltip("Hot spot in pixels; the centre of the texture.")]
        [SerializeField] private Vector2 m_Hotspot = new Vector2(32f, 32f);

        private void OnEnable()
        {
            if (m_Crosshair != null && !Application.isMobilePlatform)
                Cursor.SetCursor(m_Crosshair, m_Hotspot, CursorMode.Auto);
        }

        private void OnDisable() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
