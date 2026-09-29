using UnityEngine;
using UnityEngine.UI;

namespace ZombiCar.UI
{
    /// <summary>Loops sprite frames on a UI image (the HUD's breathing portrait). Runs on unscaled time.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public sealed class UiFrameAnimation : MonoBehaviour
    {
        [SerializeField] private Sprite[] m_Frames = System.Array.Empty<Sprite>();
        [SerializeField, Min(0.1f)] private float m_Fps = 8f;

        private UnityEngine.UI.Image m_Image;
        private int m_Shown = -1;

        private void Awake() => m_Image = GetComponent<UnityEngine.UI.Image>();

        private void Update()
        {
            if (m_Frames.Length == 0)
                return;
            int frame = Mathf.FloorToInt(Time.unscaledTime * m_Fps) % m_Frames.Length;
            if (frame == m_Shown)
                return;
            m_Shown = frame;
            m_Image.sprite = m_Frames[frame];
        }
    }
}
