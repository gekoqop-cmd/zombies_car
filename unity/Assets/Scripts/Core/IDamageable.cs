using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>Something bullets or zombies can hurt: zombies, the player, the car.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <param name="amount">Health points to remove.</param>
        /// <param name="direction">Planar direction of the hit (for knockback), may be zero.</param>
        void TakeDamage(int amount, Vector3 direction);
    }
}
