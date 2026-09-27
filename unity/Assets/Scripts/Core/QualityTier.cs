namespace ZombiCar.Core
{
    /// <summary>Quality levels; the values match the indices in Project Settings > Quality.</summary>
    public enum QualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
        /// <summary>Powerful PCs only; never picked automatically, only from the settings menu (not offered on mobile).</summary>
        Ultra = 3,
    }
}
