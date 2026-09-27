using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// A component that switches some material slots itself (e.g. the car's paint per damage stage) and picks their
    /// Ultra versions on its own: <see cref="UltraMaterialSwap"/> leaves those slots alone.
    /// </summary>
    public interface IMaterialSlotOwner
    {
        bool OwnsSlot(Renderer renderer, int index);
    }
}
