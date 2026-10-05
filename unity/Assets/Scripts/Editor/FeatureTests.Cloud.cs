using System.Linq;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 17. cloud saves
        /// <summary>
        /// The Yandex cloud copy of the progress (<see cref="MetaProgress.MergeCloud"/>): a new device takes it all;
        /// when both changed, coins and equipped skins come from the newer copy while skins, the tutorial and the map
        /// records are joined (nothing won is lost, the best time is the best of both); a broken copy changes nothing;
        /// every saved change asks for a cloud write.
        /// </summary>
        private static void Cloud()
        {
            At(1.0f, () =>
            {
                s_Director.enabled = false;
                // A new device: nothing here yet, the cloud has a played account.
                MetaProgress.ResetAll();
                PlayerPrefsKeys(false);
                int rev = MetaProgress.Revision;
                string cloud = JsonUtility.ToJson(new MetaProgress.Snapshot
                {
                    revision = rev + 100, coins = 500, owned = new[] { "gun_gold", "axe_red" }, seen = new[] { "gun_gold" },
                    equipped = new[] { "Gun:gun_gold" }, tutorialDone = true, liveHintsDone = true,
                    maps = new[] { new MetaProgress.MapSnapshot { map = "MapConfig_Map01", beaten = true, bestTime = 900f, runs = 5, wins = 1, wave = 30, kills = 700, bosses = 3 } },
                });
                Check(MetaProgress.MergeCloud(cloud), "a new device takes the cloud progress in");
                MetaProgress.Snapshot now = MetaProgress.Export();
                Check(MetaProgress.Coins == 500 && now.owned.Contains("gun_gold") && now.owned.Contains("axe_red") &&
                      MetaProgress.EquippedId(CosmeticSlot.Gun) == "gun_gold",
                    $"coins {MetaProgress.Coins}, skins [{string.Join(",", now.owned)}], rifle {MetaProgress.EquippedId(CosmeticSlot.Gun)}");
                Check(MetaProgress.TutorialDone && MetaProgress.IsBeaten("MapConfig_Map01") && MetaProgress.BestTime("MapConfig_Map01") == 900f &&
                      MetaProgress.Records("MapConfig_Map01").Runs == 5 && MetaProgress.Records("MapConfig_Map01").BestKills == 700,
                    "the tutorial is done and the map's records came along");
                Check(MetaProgress.Revision >= rev + 100, $"the revision follows the cloud ({MetaProgress.Revision})");

                // Played on here after that: coins spent, a record beaten — this copy is the newer one now.
                bool asked = false;
                void OnSaved() => asked = true;
                MetaProgress.Saved += OnSaved;
                MetaProgress.TrySpend(100);
                MetaProgress.Saved -= OnSaved;
                Check(asked, "a saved change asks for a cloud write");
                MetaProgress.RecordVictory("MapConfig_Map01", 850f);
                // The cloud still has the old copy, plus a skin won on another device and a faster time there.
                string older = JsonUtility.ToJson(new MetaProgress.Snapshot
                {
                    revision = rev + 100, coins = 500, owned = new[] { "gun_gold", "axe_red", "bullet_green" },
                    equipped = new[] { "Gun:axe_red" }, maps = new[] { new MetaProgress.MapSnapshot { map = "MapConfig_Map01", beaten = true, bestTime = 800f, runs = 9 } },
                });
                MetaProgress.MergeCloud(older);
                Check(MetaProgress.Coins == 400 && MetaProgress.EquippedId(CosmeticSlot.Gun) == "gun_gold",
                    $"the newer copy (here) keeps its coins ({MetaProgress.Coins}) and equipped skin");
                Check(MetaProgress.Export().owned.Contains("bullet_green"), "the skin won on the other device is added");
                Check(MetaProgress.BestTime("MapConfig_Map01") == 800f && MetaProgress.Records("MapConfig_Map01").Runs == 9,
                    $"records: the best of both (time {MetaProgress.BestTime("MapConfig_Map01")}, runs {MetaProgress.Records("MapConfig_Map01").Runs})");

                // Broken or empty copies change nothing.
                string before = MetaProgress.ExportJson();
                Check(!MetaProgress.MergeCloud("{oops") && !MetaProgress.MergeCloud("") && !MetaProgress.MergeCloud(null) &&
                      MetaProgress.ExportJson() == before, "a broken or empty cloud copy changes nothing");
                // The export reads back as the same progress.
                MetaProgress.Snapshot back = JsonUtility.FromJson<MetaProgress.Snapshot>(before);
                MetaProgress.MapSnapshot map01 = back.maps.FirstOrDefault(m => m.map == "MapConfig_Map01");
                Check(back.coins == 400 && back.owned.Length == 3 && map01 != null && map01.bestTime == 800f,
                    $"the cloud JSON reads back ({before.Length} chars: coins {back.coins}, skins {back.owned.Length}, " +
                    $"maps [{string.Join(",", back.maps.Select(m => m.map))}], best {map01?.bestTime})");

                // The checkpoints, the achievements and the daily row travel too (and old copies without them still read).
                MetaProgress.SaveCheckpoint(new MetaProgress.Checkpoint { map = "MapConfig_Map01", difficulty = 0, wave = 21, money = 77 });
                MetaProgress.Achieve(MetaProgress.MadmanAchievement);
                MetaProgress.Snapshot full = JsonUtility.FromJson<MetaProgress.Snapshot>(MetaProgress.ExportJson());
                Check(full.checkpoints.Any(c => c.wave == 21 && c.money == 77) && full.achieved.Contains(MetaProgress.MadmanAchievement),
                    "a checkpoint and an achievement go to the cloud copy");
                MetaProgress.ClearCheckpoint("MapConfig_Map01", ZombiCar.Core.Difficulty.Easy);
                MetaProgress.ForgetAchievement(MetaProgress.MadmanAchievement);
                Check(MetaProgress.MergeCloud(JsonUtility.ToJson(full)) && MetaProgress.GetCheckpoint("MapConfig_Map01", ZombiCar.Core.Difficulty.Easy)?.wave == 21 &&
                      MetaProgress.IsAchieved(MetaProgress.MadmanAchievement), "another device takes them back in");
                MetaProgress.ClearCheckpoint("MapConfig_Map01", ZombiCar.Core.Difficulty.Easy);
                MetaProgress.ForgetAchievement(MetaProgress.MadmanAchievement);
                Check(!YandexSdk.CloudEnabled && YandexSdk.CloudReady, "outside Yandex Games: no cloud, nothing to wait for");

                MetaProgress.ResetAll();
                PlayerPrefsKeys(true);
            });
            At(1.2f, Finish);
        }

        // The tests share the editor's PlayerPrefs: the tutorial flags and the map records are cleared before, and the
        // tutorial marked done after (other scenarios expect no intro cards).
        private static void PlayerPrefsKeys(bool after)
        {
            foreach (string what in new[] { "runs", "wins", "wave", "kills", "bosses" })
                PlayerPrefs.DeleteKey($"meta.record.{what}.MapConfig_Map01");
            PlayerPrefs.DeleteKey("meta.beaten.MapConfig_Map01");
            PlayerPrefs.DeleteKey("meta.bestTime.MapConfig_Map01");
            PlayerPrefs.SetInt("meta.tutorialDone", after ? 1 : 0);
            PlayerPrefs.SetInt("meta.liveHintsDone", after ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
