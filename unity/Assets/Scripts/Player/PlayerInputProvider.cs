using UnityEngine;
using ZombiCar.Core;

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
            bool useMobile = DeviceInfo.IsMobile && m_Mobile != null && m_Mobile.IsImplemented;
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

            }
        }
    }
}
