using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Progression;
using ZombiCar.UI;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 16. legendary card
        /// <summary>
        /// The run's legendary card: not offered before level 5, then in every hand until taken (and never shown when
        /// the pool draws at random); it is dressed up on the card, taken once it is at its maximum ("MAX" on the HUD at
        /// once) and it gives both of its stats. Then the vampire card for an ad: offered now and then from its level,
        /// with the ad disc, taken after the ad at its maximum.
        /// </summary>
        private static void Legendary()
        {
            LevelUpScreen levelUp = null;
            PerkPool pool = AssetDatabase.LoadAssetAtPath<PerkPool>(DataAssetsBuilder.c_PerkPool);
            Perk legend = pool.Legendary;
            float damageBefore = 0f, fireBefore = 0f;
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                levelUp = Find<LevelUpScreen>();
                Check(legend != null && legend.MaxStacks == 1 && legend.Weight == 0f && legend.HasSecond,
                    $"the pool has a legendary card: {legend?.Title}");
                s_Progression.AddXp(35);                                           // level 2: too early for it
            });
            At(1.6f, () =>
            {
                Check(levelUp.IsOpen && !levelUp.Hand.Contains(legend), "level 2: no legendary card yet");
                levelUp.Pick(0);
            });
            At(2.3f, () =>
            {
                // Up to level 5 (a few level-ups at once): the legendary card is dealt.
                while (s_Stats.Level < pool.LegendaryFromLevel)
                    s_Progression.AddXp(200);
            });
            // Hands dealt for the levels before 5 go first (a card from each), then the legendary card comes.
            for (float x = 2.9f; x < 5.5f; x += 0.5f)
                At(x, () =>
                {
                    if (levelUp.IsOpen && !levelUp.Hand.Contains(legend))
                        levelUp.Pick(0);
                });
            At(6.0f, () =>
            {
                Check(levelUp.IsOpen && levelUp.Hand.Contains(legend), $"level {s_Stats.Level}: the legendary card is in the hand");
                Check(Object.FindObjectsByType<PerkCardView>(FindObjectsSortMode.None).Any(c => c.IsLegendary),
                    "it is shown as the legendary card");
                Shot("16_Legendary_Card");
            });
            At(6.5f, () => Check(levelUp.Reroll(), "a reroll for an ad"));
            At(7.0f, () =>
            {
                Check(levelUp.Hand.Contains(legend), "the new hand still holds it (until it is taken)");
                damageBefore = s_Run.Modifiers[StatEffect.WeaponDamage];
                fireBefore = s_Run.Modifiers[StatEffect.FireRate];
                levelUp.Pick(levelUp.Hand.ToList().IndexOf(legend));
            });
            At(8.0f, () =>
            {
                Check(s_Run.StacksOf(legend) == 1, "taken: at its maximum at once");
                Check(s_Run.Modifiers[StatEffect.WeaponDamage] > damageBefore + 0.3f && s_Run.Modifiers[StatEffect.FireRate] > fireBefore + 0.2f,
                    "both of its stats are given (damage +35%, fire rate +25%)");
                var tray = Object.FindAnyObjectByType<PerkTrayView>().transform.Find(legend.Id);
                Check(tray != null && tray.Find("LegendaryRays") != null &&
                      tray.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "MAX"), "on the HUD: MAX in gold rays");
                if (levelUp.IsOpen)
                    Check(!levelUp.Hand.Contains(legend), "the next hands go without it");
                Shot("16_Legendary_Taken");
            });

            // The legendary card for an ad: now and then from its level, taken after the ad, at its maximum at once.
            Perk vampire = pool.AdLegendary;
            float healBefore = 0f;
            bool shown = false;
            At(8.4f, () =>
            {
                Check(vampire != null && vampire.ForAd && vampire.MaxStacks == 1 && vampire.Effect == StatEffect.KillHeal,
                    $"the pool has a legendary card for an ad: {vampire?.Title}");
                for (int i = 0; i < 6; i++)
                    s_Progression.AddXp(400);                                    // plenty of level-ups past its level
            });
            for (float x = 9.0f; x < 15.0f; x += 0.4f)
                At(x, () =>
                {
                    if (!levelUp.IsOpen || levelUp.Hand.Contains(vampire))
                        return;
                    if (s_Stats.Level >= pool.AdLegendaryFromLevel)
                        levelUp.Reroll();
                    else
                        levelUp.Pick(0);
                });
            At(15.2f, () =>
            {
                shown = levelUp.IsOpen && levelUp.Hand.Contains(vampire);
                Check(shown, $"level {s_Stats.Level}: the vampire card comes now and then");
                if (!shown)
                    return;
                Check(Object.FindObjectsByType<PerkCardView>(FindObjectsSortMode.None)
                        .Any(c => c.IsLegendary && c.transform.Find("Body/AdBadge")?.gameObject.activeSelf == true),
                    "it wears the ad disc");
                Shot("16_Legendary_Vampire");
                healBefore = s_Run.Modifiers[StatEffect.KillHeal];
                levelUp.Pick(levelUp.Hand.ToList().IndexOf(vampire));
            });
            At(16.2f, () =>
            {
                if (shown)
                    Check(s_Run.StacksOf(vampire) == 1 && s_Run.Modifiers[StatEffect.KillHeal] >= healBefore + 6f - 0.01f,
                        "taken after the ad: 6 health per kill at once");
            });
            At(16.5f, Finish);
        }
    }
}
