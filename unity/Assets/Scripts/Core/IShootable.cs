namespace ZombiCar.Core
{
    /// <summary>
    /// A prop bullets and melee hits damage although it is scenery, not an enemy (e.g. an explosive barrel). The car is
    /// <see cref="IDamageable"/> but not shootable: bullets stop at it without damage.
    /// </summary>
    public interface IShootable : IDamageable
    {
    }
}
