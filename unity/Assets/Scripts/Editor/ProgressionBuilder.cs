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
        private static readonly Color s_Car = new Color(0.3f, 0.56f, 0.9f);
        private static readonly Color s_Run = new Color(0.93f, 0.76f, 0.24f);

        private static Sprite Icon(string name) => UiFactory.Sprite("UI_Icon_" + name);

        [MenuItem("Tools/Setup/Build Progression Data")]
        public static void Run()
        {
            var pool = DataAssetsBuilder.Load<PerkPool>(DataAssetsBuilder.c_PerkPool);
            pool.EditorSetPerks(Perks());
            EditorUtility.SetDirty(pool);
            var catalog = DataAssetsBuilder.Load<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
            catalog.EditorSetUpgrades(CarUpgrades());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProgressionBuilder] {pool.Perks.Count} level-up cards, {catalog.Upgrades.Length} car upgrades.");
        }

        private static Perk[] Perks() => new[]
        {
            // Gun (on top of the base gun, which stays as it is).
            new Perk("damage", "КРУПНЫЙ КАЛИБР", "Урон оружия +20%", Icon("Damage"), s_Gun, StatEffect.WeaponDamage, 0.2f, 5, 1f),
            new Perk("firerate", "СКОРОСТРЕЛ", "Скорострельность +15%", Icon("FireRate"), s_Gun, StatEffect.FireRate, 0.15f, 5, 1f),
            new Perk("reload", "БЫСТРЫЕ РУКИ", "Перезарядка быстрее на 20%", Icon("Reload"), s_Gun, StatEffect.ReloadSpeed, 0.2f, 3, 1f),
            new Perk("magazine", "БОЛЬШОЙ МАГАЗИН", "+8 патронов в магазине", Icon("Magazine"), s_Gun, StatEffect.MagazineSize, 8f, 4, 1f),
            new Perk("reserve", "ЗАПАСЛИВЫЙ", "+60 к запасу патронов,\nсразу пополняется", Icon("AmmoBox"), s_Gun, StatEffect.MaxReserve, 60f, 4, 1f),
            new Perk("pierce", "БРОНЕБОЙНЫЕ ПУЛИ", "Пуля пробивает\nещё одного зомби", Icon("Pierce"), s_Gun, StatEffect.BulletPierce, 1f, 3, 0.8f),
            new Perk("crit", "ТОЧНЫЙ ГЛАЗ", "10% шанс\nдвойного урона", Icon("Crit"), s_Gun, StatEffect.CritChance, 0.1f, 4, 1f),
            new Perk("range", "ДАЛЬНОБОЙ", "Дальность стрельбы +20%", Icon("Range"), s_Gun, StatEffect.WeaponRange, 0.2f, 3, 0.8f),
            // Survival.
            new Perk("health", "ЖИВУЧЕСТЬ", "+25 к максимуму здоровья\nи лечение на 25", Icon("Health"), s_Survival, StatEffect.PlayerMaxHealth, 25f, 5, 1f),
            new Perk("regen", "РЕГЕНЕРАЦИЯ", "Восстанавливает\n1 здоровья в секунду", Icon("Regen"), s_Survival, StatEffect.PlayerRegen, 1f, 3, 1f),
            new Perk("speed", "ЛЁГКИЕ НОГИ", "Скорость бега +10%", Icon("Speed"), s_Survival, StatEffect.MoveSpeed, 0.1f, 3, 0.9f),
            new Perk("vampire", "ВАМПИРИЗМ", "Каждое убийство\nлечит 2 здоровья", Icon("Vampire"), s_Survival, StatEffect.KillHeal, 2f, 3, 0.9f),
            new Perk("medkit", "АПТЕЧКА", "Полностью восстанавливает\nздоровье", Icon("Medkit"), s_Survival, StatEffect.HealPlayerFull, 0f, 0, 0.6f),
            // Car.
            new Perk("carplate", "БРОНЕЛИСТЫ", "Машина: +80 к прочности\nи ремонт на 80", Icon("CarPlate"), s_Car, StatEffect.CarMaxHealth, 80f, 4, 1f),
            new Perk("carregen", "САМОРЕМОНТ", "Машина чинится\nна 2 в секунду", Icon("CarRegen"), s_Car, StatEffect.CarRegen, 2f, 3, 0.9f),
            // Run.
            new Perk("scrap", "БАРАХОЛЬЩИК", "Металлолома +25%", Icon("ScrapGain"), s_Run, StatEffect.MoneyGain, 0.25f, 3, 0.9f),
            new Perk("xp", "СМЕКАЛКА", "Опыта +20%", Icon("Xp"), s_Run, StatEffect.XpGain, 0.2f, 3, 0.9f),
            new Perk("boom", "ПИРОТЕХНИК", "Взрывы бочек: радиус +25%,\nурон +50%", Icon("Bomb"), s_Run, StatEffect.ExplosionPower, 1f, 3, 0.8f),
        };

        private static CarUpgrade[] CarUpgrades() => new[]
        {
            new CarUpgrade("armor", "БРОНЯ", "Машина получает на {1}% меньше урона", Icon("Armor"), StatEffect.CarArmor, 0.08f,
                new[] { 60, 110, 180, 270, 400 }),
            new CarUpgrade("hull", "УСИЛЕННЫЙ КОРПУС", "+{0} к прочности машины (и ремонт)", Icon("Hull"), StatEffect.CarMaxHealth, 60f,
                new[] { 50, 100, 160, 240, 350 }),
            new CarUpgrade("spikes", "ШИПЫ", "Зомби, бьющий машину, получает {0} урона", Icon("Spikes"), StatEffect.CarSpikes, 8f,
                new[] { 70, 140, 230, 350 }),
            new CarUpgrade("shock", "ЭЛЕКТРОРАЗРЯД", "Раз в 6 с бьёт током зомби рядом: +{0} урона", Icon("Shock"), StatEffect.CarShock, 20f,
                new[] { 150, 280, 450 }),
            new CarUpgrade("ammo", "ОРУЖЕЙНЫЙ ЯЩИК", "+{0} к запасу патронов (вместимость)", Icon("AmmoBox"), StatEffect.MaxReserve, 40f,
                new[] { 60, 110, 180, 260 }),
            new CarUpgrade("repair", "РЕМОНТ", "Восстановить {0} прочности машины", Icon("Repair"), StatEffect.RepairCar, 150f,
                new[] { 40 }, true, 10, 120),
        };
    }
}
