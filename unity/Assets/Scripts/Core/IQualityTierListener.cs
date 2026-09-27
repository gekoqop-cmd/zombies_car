namespace ZombiCar.Core
{
    /// <summary>
    /// Scene component that adapts itself to the graphics tier. Components subscribe to
    /// <see cref="SettingsService.QualityChanged"/> at runtime; editor tools call it directly to preview a tier.
    /// </summary>
    public interface IQualityTierListener
    {
        void ApplyQualityTier(QualityTier tier);
    }
}
