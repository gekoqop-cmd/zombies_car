using UnityEditor;
using UnityEngine;
using ZombiCar.Progression;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Fills the level-up card pool (Data/PerkPool) and the workshop's car upgrades (Data/CarUpgrades) from the
    /// lists below, with the pictograms of tools/ui/make_icons.py. Both assets are rewritten on every build: edit the
    /// lists here. The experience curve and wave rewards (Data/ProgressionConfig) are created once with their
    /// defaults and then tuned in the Inspector.
    /// </summary>
    public static class ProgressionBuilder
    {
        // Card disc colours by group.
        private static readonly Color s_Gun = new Color(0.95f, 0.55f, 0.18f);
        private static readonly Color s_Survival = new Color(0.86f, 0.27f, 0.25f);
        private static readonly Color s_Axe = new Color(0.72f, 0.47f, 0.27f);
        private static readonly Color s_Run = new Color(0.93f, 0.76f, 0.24f);

        private static Sprite Icon(string name) => UiFactory.Sprite("UI_Icon_" + name);

        [MenuItem("Tools/Setup/Build Progression Data")]
        public static void Run()
        {
            var pool = DataAssetsBuilder.Load<PerkPool>(DataAssetsBuilder.c_PerkPool);
            pool.EditorSetPerks(Perks());
            EditorUtility.SetDirty(pool);
            var catalog = DataAssetsBuilder.Load<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
            catalog.EditorSetUpgrades(CarUpgrades(), Supplies());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProgressionBuilder] {pool.Perks.Count} level-up cards, {catalog.Upgrades.Length} car upgrades, " +
                      $"{catalog.Supplies.Length} supplies.");
        }

        // Every card that raises a number gives more each time it is taken (value + step × times taken; the card shows
        // what the next one gives). Flat on purpose: pierce (one more zombie per bullet), the first-aid kit (heals to
        // full) and the pyrotechnic (a fixed tier of the barrels: radius +25%, damage +50%) — abilities, not numbers.
        private static Perk[] Perks() => new[]
        {
            // Gun (on top of the base gun, which stays as it is).
            new Perk("damage", "КРУПНЫЙ КАЛИБР", "Урон оружия +{1}%", Icon("Damage"), s_Gun, StatEffect.WeaponDamage, 0.15f, 5, 1f, 0.03f),
            new Perk("firerate", "СКОРОСТРЕЛ", "Скорострельность +{1}%", Icon("FireRate"), s_Gun, StatEffect.FireRate, 0.1f, 5, 1f, 0.03f),
            new Perk("reload", "БЫСТРЫЕ РУКИ", "Перезарядка быстрее на {1}%", Icon("Reload"), s_Gun, StatEffect.ReloadSpeed, 0.15f, 3, 1f, 0.05f),
            // Ammo is scarce on purpose (WeaponConfig: 20 in the magazine, 60 / 100 in reserve): small steps up.
            new Perk("magazine", "БОЛЬШОЙ МАГАЗИН", "+{0} патронов в магазине", Icon("Magazine"), s_Gun, StatEffect.MagazineSize, 4f, 4, 1f, 1f),
            new Perk("reserve", "ЗАПАСЛИВЫЙ", "+{0} к запасу патронов,\nсразу пополняется", Icon("AmmoBox"), s_Gun, StatEffect.MaxReserve, 20f, 4, 0.8f, 10f),
            new Perk("pierce", "БРОНЕБОЙНЫЕ ПУЛИ", "Пуля пробивает\nещё одного зомби", Icon("Pierce"), s_Gun, StatEffect.BulletPierce, 1f, 3, 0.8f),
            new Perk("crit", "ТОЧНЫЙ ГЛАЗ", "+{1}% шанс\nдвойного урона", Icon("Crit"), s_Gun, StatEffect.CritChance, 0.07f, 4, 1f, 0.02f),
            new Perk("range", "ДАЛЬНОБОЙ", "Дальность стрельбы +{1}%", Icon("Range"), s_Gun, StatEffect.WeaponRange, 0.15f, 3, 0.8f, 0.05f),
            // Axe.
            new Perk("axedamage", "ОСТРЫЙ ТОПОР", "Урон топора +{1}%", Icon("Axe"), s_Axe, StatEffect.AxeDamage, 0.3f, 4, 0.9f, 0.1f),
            new Perk("axerange", "ШИРОКИЙ ЗАМАХ", "Дальность удара\nтопором +{1}%", Icon("AxeRange"), s_Axe, StatEffect.AxeRange, 0.15f, 3, 0.8f, 0.05f),
            // Survival.
            new Perk("health", "ЖИВУЧЕСТЬ", "+{0} к максимуму здоровья\nи лечение на {0}", Icon("Health"), s_Survival, StatEffect.PlayerMaxHealth, 15f, 5, 1f, 5f),
            new Perk("regen", "РЕГЕНЕРАЦИЯ", "Восстанавливает\n+{0} здоровья в секунду", Icon("Regen"), s_Survival, StatEffect.PlayerRegen, 0.75f, 3, 1f, 0.25f),
            new Perk("speed", "ЛЁГКИЕ НОГИ", "Скорость бега +{1}%", Icon("Speed"), s_Survival, StatEffect.MoveSpeed, 0.08f, 3, 0.9f, 0.02f),
            new Perk("vampire", "ВАМПИРИЗМ", "Каждое убийство\nлечит ещё {0} здоровья", Icon("Vampire"), s_Survival, StatEffect.KillHeal, 1f, 3, 0.9f, 1f),
            new Perk("medkit", "АПТЕЧКА", "Полностью восстанавливает\nздоровье", Icon("Medkit"), s_Survival, StatEffect.HealPlayerFull, 0f, 0, 0.6f),
            // No car cards: the car is upgraded only in the workshop (CarUpgrades below).
            // Run.
            new Perk("scrap", "БАРАХОЛЬЩИК", "Металлолома +{1}%", Icon("ScrapGain"), s_Run, StatEffect.MoneyGain, 0.2f, 3, 0.9f, 0.05f),
            new Perk("xp", "СМЕКАЛКА", "Опыта +{1}%", Icon("Xp"), s_Run, StatEffect.XpGain, 0.15f, 3, 0.9f, 0.05f),
            new Perk("boom", "ПИРОТЕХНИК", "Взрывы бочек: радиус +25%,\nурон +50%", Icon("Bomb"), s_Run, StatEffect.ExplosionPower, 1f, 3, 0.8f),
        };

        // Car upgrades. Every level gives more than the one before (value + step × level; the tile shows the next
        // level's gain), and so do the endless master levels after the last one (price: last level × 1.35^n). The last
        // two levels cost ~1.5x of what they used to (scrap piled up by wave 30).
        private static CarUpgrade[] CarUpgrades() => new[]
        {
            // 6 / 7 / 8 / 9 / 10% (40% in all, as before), master 1.5 / 2 / 2.5% ... (capped at 75% by CarHealth).
            new CarUpgrade("armor", "БРОНЯ", "Машина получает на {1}% меньше урона", Icon("Armor"), StatEffect.CarArmor, 0.06f,
                new[] { 60, 110, 180, 405, 600 }, masterValue: 0.015f, masterDescription: "Ещё −{1}% урона машине",
                valueStep: 0.01f, masterStep: 0.005f),
            // +40 / 50 / 60 / 70 / 80 (300 in all, as before), master +12 / 14 / 16 ...
            new CarUpgrade("hull", "УСИЛЕННЫЙ КОРПУС", "+{0} к прочности машины (и ремонт)", Icon("Hull"), StatEffect.CarMaxHealth, 40f,
                new[] { 50, 100, 160, 360, 525 }, masterValue: 12f, masterDescription: "Ещё +{0} к прочности",
                valueStep: 10f, masterStep: 2f),
            // 8 / 10 / 12 / 14 damage (44 in all), master +2 / 3 / 4 ...
            new CarUpgrade("spikes", "ШИПЫ", "Зомби, бьющий машину, получает +{0} урона", Icon("Spikes"), StatEffect.CarSpikes, 8f,
                new[] { 70, 140, 345, 525 }, masterValue: 2f, masterDescription: "Ещё +{0} урона шипов",
                valueStep: 2f, masterStep: 1f),
            // +20 / 25 / 30 (75 in all), master +3 / 4 / 5 ...
            new CarUpgrade("shock", "ЭЛЕКТРОРАЗРЯД", "Раз в 6 с бьёт током зомби рядом: +{0} урона", Icon("Shock"), StatEffect.CarShock, 20f,
                new[] { 150, 420, 675 }, masterValue: 3f, masterDescription: "Ещё +{0} урона разряда",
                valueStep: 5f, masterStep: 1f),
            // +30 / 40 / 50 / 60 rounds (180 in all), master +5 / 6 / 7 ...
            new CarUpgrade("ammo", "ОРУЖЕЙНЫЙ ЯЩИК", "+{0} к запасу патронов (вместимость)", Icon("AmmoBox"), StatEffect.MaxReserve, 30f,
                new[] { 60, 110, 270, 390 }, masterValue: 5f, masterDescription: "Ещё +{0} к запасу патронов",
                valueStep: 10f, masterStep: 1f),
        };

        /// <summary>The repair (the last supply) is paid with a rewarded ad only, this many times per run.</summary>
        public const int c_AdRepairsPerRun = 3;

        // Supplies: bought again and again between waves, the price grows with every purchase in the run. The repair is
        // not for scrap: a rewarded ad gives +200 durability (up to the maximum), c_AdRepairsPerRun times a run.
        private static CarUpgrade[] Supplies() => new[]
        {
            new CarUpgrade("medkit", "АПТЕЧКА", "Полное здоровье героя", Icon("Medkit"), StatEffect.HealPlayerFull, 0f,
                new[] { 60 }, true, 20),
            new CarUpgrade("ammobox", "ЯЩИК ПАТРОНОВ", "Полный магазин и запас", Icon("Magazine"), StatEffect.RefillAmmo, 0f,
                new[] { 40 }, true),
            new CarUpgrade("mines", "МИНЫ", "{0} мины у машины на следующую волну", Icon("Bomb"), StatEffect.PlaceMines, 4f,
                new[] { 120 }, true),
            new CarUpgrade("repair", "РЕМОНТ +200", "Прочность машины +{0}", Icon("Repair"), StatEffect.RepairCar, 200f,
                new[] { 0 }, true, adUsesPerRun: c_AdRepairsPerRun),
        };
    }
}
