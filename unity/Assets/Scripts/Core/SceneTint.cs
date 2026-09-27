using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Colour the current map multiplies onto the (unlit) character sprites, so the player and the zombies sit in the
    /// map's light: white by day, cool blue at night. Set by <see cref="MapBootstrap"/> from <see cref="MapConfig"/>.
    /// </summary>
    public static class SceneTint
    {
        public static Color Characters { get; set; } = Color.white;
    }
}
