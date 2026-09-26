using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Chooses the input implementation for the current device and exposes it as <see cref="IPlayerInput"/>.
    /// Player components ask this provider instead of referencing a concrete input class.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputProvider : MonoBehaviour
    {
        [SerializeField] private DesktopPlayerInput m_Desktop;
        [SerializeField] private MobilePlayerInput m_Mobile;

        public IPlayerInput Input { get; private set; }

        private void Awake()
        {
            bool useMobile = Application.isMobilePlatform && m_Mobile != null && m_Mobile.IsImplemented;
            if (useMobile)
            {
                Input = m_Mobile;
                if (m_Desktop != null)
                    m_Desktop.enabled = false;
            }
            else
            {
                Input = m_Desktop;
                if (m_Mobile != null)
                    m_Mobile.enabled = false;
                if (Application.isMobilePlatform)
                    Debug.LogWarning("[PlayerInputProvider] Touch controls are not implemented yet, using PC input.");
            }
        }
    }
}
