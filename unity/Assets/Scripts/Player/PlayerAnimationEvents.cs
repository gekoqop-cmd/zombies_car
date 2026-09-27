using UnityEngine;

namespace ZombiCar.Player
{
    /// <summary>
    /// Receives the animation events of the player's clips (they can only call components on the Animator's own
    /// GameObject) and forwards them to <see cref="PlayerView"/>.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimationEvents : MonoBehaviour
    {
        [SerializeField] private PlayerView m_View;

        /// <summary>Foot strike in the Run clip.</summary>
        public void OnFootstep()
        {
            if (m_View != null)
                m_View.Footstep();
        }
    }
}
