using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Mutes all audio while the tab/app is not focused or is paused (Yandex Games moderation rule).
    /// The Yandex SDK pause events (game_api_pause / resume) can call <see cref="SetExternalPause"/> later.
    /// </summary>
    public sealed class AudioFocusGuard : MonoBehaviour
    {
        private bool m_Unfocused;
        private bool m_Paused;
        private bool m_ExternalPause;

        public void SetExternalPause(bool paused)
        {
            m_ExternalPause = paused;
            Apply();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            m_Unfocused = !hasFocus;
            Apply();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            m_Paused = pauseStatus;
            Apply();
        }

        private void Apply() => AudioListener.pause = m_Unfocused || m_Paused || m_ExternalPause;
    }
}
