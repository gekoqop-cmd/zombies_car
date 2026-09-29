namespace ZombiCar.Progression
{
    /// <summary>
    /// What a level-up card or a workshop upgrade changes. Stat effects add up in <see cref="RunModifiers"/> for the
    /// whole run; the instant ones (<see cref="HealPlayerFull"/>, <see cref="RepairCar"/>) act once when taken.
    /// Values: multipliers are fractions (+0.2 = +20%), everything else is in the stat's own units.
    /// </summary>
    public enum StatEffect
    {
        // Gun (bonuses on top of WeaponConfig; the base gun is never changed).
        WeaponDamage,      // fraction
        FireRate,          // fraction
        ReloadSpeed,       // fraction: reload time / (1 + value)
        MagazineSize,      // rounds
        MaxReserve,        // rounds (also given at once when taken)
        BulletPierce,      // zombies a bullet passes through
        CritChance,        // 0..1, a critical hit deals double damage
        WeaponRange,       // fraction

        // Player.
        PlayerMaxHealth,   // health points (also healed at once)
        PlayerRegen,       // health per second
        MoveSpeed,         // fraction
        KillHeal,          // health per kill

        // Axe.
        AxeDamage,         // fraction
        AxeRange,          // fraction

        // Car.
        CarMaxHealth,      // durability points (also repaired at once)
        CarArmor,          // share of the damage the car ignores (capped)
        CarRegen,          // durability per second
        CarSpikes,         // damage to a zombie every time it hits the car
        CarShock,          // damage of the electric pulse around the car

        // Run.
        MoneyGain,         // fraction
        XpGain,            // fraction
        ExplosionPower,    // stacks: explosion radius +25% and damage +50% each

        // Instant.
        HealPlayerFull,
        RepairCar,         // durability points
        RefillAmmo,        // the magazine and the reserve full
        PlaceMines,        // mines put around the car (count)
    }
}
