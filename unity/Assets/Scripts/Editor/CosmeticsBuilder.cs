using UnityEditor;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Fills Data/Cosmetics: the skins of the rifle, the axe and the bullets and the three cases of the main menu
    /// (prices, the rarity chances shown on them). The asset is rewritten on every build: edit the lists here.
    /// </summary>
    public static class CosmeticsBuilder
    {
        public const string c_Catalog = EditorPaths.c_Data + "/Cosmetics.asset";

        public static CosmeticCatalog Catalog
        {
            get
            {
                var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(c_Catalog);
                return catalog != null ? catalog : Run();
            }
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        [MenuItem("Tools/Setup/Build Cosmetics")]
        public static CosmeticCatalog Run()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_Data);
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(c_Catalog);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
                AssetDatabase.CreateAsset(catalog, c_Catalog);
            }
            const CosmeticSlot gun = CosmeticSlot.Gun, axe = CosmeticSlot.Axe, bullet = CosmeticSlot.Bullet;
            const Rarity common = Rarity.Common, rare = Rarity.Rare, epic = Rarity.Epic, legend = Rarity.Legendary;
            CosmeticItem[] items =
            {
                // Rifle: the colour multiplies the rifle's lifted greys (PlayerGunMask).
                new CosmeticItem("gun_default", "СТАНДАРТ", gun, common, Color.white, Color.white, isDefault: true),
                new CosmeticItem("gun_olive", "ОЛИВА", gun, common, C(0.62f, 0.7f, 0.42f), Color.white),
                new CosmeticItem("gun_sand", "ПЕСОК", gun, common, C(0.95f, 0.82f, 0.58f), Color.white),
                new CosmeticItem("gun_graphite", "ГРАФИТ", gun, common, C(0.5f, 0.53f, 0.6f), Color.white),
                new CosmeticItem("gun_ice", "ЛЁД", gun, rare, C(0.6f, 0.88f, 1f), Color.white),
                new CosmeticItem("gun_blood", "КРОВЬ", gun, rare, C(0.95f, 0.25f, 0.22f), Color.white),
                new CosmeticItem("gun_toxic", "ЯД", gun, rare, C(0.5f, 1f, 0.35f), Color.white),
                new CosmeticItem("gun_neon", "НЕОН", gun, epic, C(1f, 0.35f, 0.95f), Color.white),
                new CosmeticItem("gun_cobalt", "КОБАЛЬТ", gun, epic, C(0.3f, 0.5f, 1f), Color.white),
                new CosmeticItem("gun_gold", "ЗОЛОТО", gun, legend, C(1f, 0.8f, 0.3f), Color.white),

                // Axe: blade tint, slash colour.
                new CosmeticItem("axe_default", "СТАНДАРТ", axe, common, Color.white, C(1f, 0.97f, 0.86f), isDefault: true),
                new CosmeticItem("axe_rust", "РЖАВЧИНА", axe, common, C(0.95f, 0.62f, 0.4f), C(1f, 0.75f, 0.5f)),
                new CosmeticItem("axe_steel", "СТАЛЬ", axe, common, C(0.82f, 0.88f, 0.95f), C(0.85f, 0.92f, 1f)),
                new CosmeticItem("axe_ice", "ЛЁД", axe, rare, C(0.6f, 0.9f, 1f), C(0.55f, 0.9f, 1f)),
                new CosmeticItem("axe_blood", "КРОВЬ", axe, rare, C(1f, 0.35f, 0.3f), C(1f, 0.25f, 0.2f)),
                new CosmeticItem("axe_toxic", "ЯД", axe, epic, C(0.55f, 1f, 0.4f), C(0.5f, 1f, 0.3f)),
                new CosmeticItem("axe_neon", "НЕОН", axe, epic, C(1f, 0.45f, 1f), C(0.95f, 0.4f, 1f)),
                new CosmeticItem("axe_gold", "ЗОЛОТО", axe, legend, C(1f, 0.82f, 0.35f), C(1f, 0.85f, 0.3f)),

                // Bullets: tracer colour (made HDR in Bullet.ApplySkin), trail colour, size.
                new CosmeticItem("bullet_default", "ОБЫЧНЫЕ", bullet, common, C(1f, 0.72f, 0.28f), Color.white, isDefault: true),
                new CosmeticItem("bullet_white", "БЕЛЫЕ", bullet, common, C(1f, 1f, 1f), Color.white),
                new CosmeticItem("bullet_green", "ЗЕЛЁНЫЕ", bullet, common, C(0.45f, 1f, 0.4f), Color.white),
                new CosmeticItem("bullet_plasma", "ПЛАЗМА", bullet, rare, C(0.35f, 0.7f, 1f), C(0.3f, 0.65f, 1f), 1.1f, true),
                new CosmeticItem("bullet_fire", "ОГНЕННЫЕ", bullet, rare, C(1f, 0.42f, 0.1f), C(1f, 0.35f, 0.05f), 1.1f, true),
                new CosmeticItem("bullet_toxic", "ЯДОВИТЫЕ", bullet, epic, C(0.5f, 1f, 0.2f), C(0.35f, 1f, 0.15f), 1.25f, true),
                new CosmeticItem("bullet_void", "ФИОЛЕТОВЫЕ", bullet, epic, C(0.8f, 0.3f, 1f), C(0.7f, 0.2f, 1f), 1.25f, true),
                new CosmeticItem("bullet_gold", "ЗОЛОТЫЕ", bullet, legend, C(1f, 0.85f, 0.3f), C(1f, 0.7f, 0.2f), 1.4f, true),
            };
            CaseDef[] cases =
            {
                new CaseDef("ОРУЖЕЙНЫЙ КЕЙС", gun, 150),
                new CaseDef("КЕЙС ТОПОРОВ", axe, 120),
                new CaseDef("КЕЙС ЭФФЕКТОВ ПУЛЬ", bullet, 100),
            };
            catalog.EditorSet(items, cases);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CosmeticsBuilder] {items.Length} skins, {cases.Length} cases.");
            return catalog;
        }
    }
}
