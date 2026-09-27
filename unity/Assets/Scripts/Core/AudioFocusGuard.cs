using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Pauses the game and its sound while the tab / app is not focused or is minimised (Yandex Games moderation
    /// rule), through <see cref="GamePause"/>. Lives on the persistent scene loader.
    /// </summary>
    public sealed class AudioFocusGuard : MonoBehaviour
    {
        private bool m_Unfocused;
        private bool m_Paused;

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

        private void Apply() => GamePause.Set(GamePause.Reason.Focus, m_Unfocused || m_Paused);
    }
}
