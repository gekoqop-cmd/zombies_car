using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Player
{
    /// <summary>
    /// Takes zombie hits for the player and writes them to <see cref="PlayerStats"/> (shown by the HUD).
    /// At zero health zombies stop going for the player; the game-over flow is not implemented yet.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerStats m_Stats;
        [SerializeField] private PlayerView m_View;

        private CharacterController m_Controller;

        public bool IsAlive => m_Stats == null || m_Stats.Health > 0;

        /// <summary>Body radius, metres (zombies measure their reach from it).</summary>
        public float Radius => m_Controller.radius;

        private void Awake() => m_Controller = GetComponent<CharacterController>();

        public void TakeDamage(int amount, Vector3 direction)
        {
            if (!IsAlive || m_Stats == null)
                return;
            m_Stats.SetHealth(m_Stats.Health - amount);
            if (m_View != null)
                m_View.ShowHurt();
            if (!IsAlive)
                Debug.Log("[PlayerHealth] The player is down (game over is not implemented yet).", this);
        }
    }
}
