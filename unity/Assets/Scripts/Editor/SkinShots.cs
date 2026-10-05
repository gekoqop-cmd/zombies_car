using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.UI;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Play-mode walkthrough of the cases and the skins, with checks and Screenshots/Skins/*.png: the cases screen, a
    /// case spinning and its prize, the daily bonus's legendary case and black bullets, the collection; then the map with
    /// a gold rifle, the black bullets and a neon axe, the axe
    /// in hand without the rifle, the four spike sets on the car and the first tutorial hint. The player's saved coins,
    /// skins and tutorial flag are restored afterwards.
    /// Tools/Diagnostics/Skin Screenshots (batch: without -quit; exits 0 when every check passed).
    /// </summary>
    [InitializeOnLoad]
    public static class SkinShots
    {
        private const string c_StateKey = "ZombiCar.SkinShots.State";
        private const string c_Folder = "Screenshots/Skins";

        private sealed class Bot : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public Vector3 AimDirection { get; set; } = Vector3.right;
            public bool AimActive { get; set; } = true;
            public bool FireHeld { get; set; }
            public bool FirePressed => false;
            public bool FireReleased => false;
            public bool QuickTap => false;
            public bool ReloadPressed => false;
            public bool InteractPressed => false;
            public bool SwitchWeaponPressed => false;
            public int SlotUntilFrame { get; set; } = -1;
            public int Slot { get; set; } = -1;
            public int WeaponSlotPressed => Time.frameCount <= SlotUntilFrame ? Slot : -1;

            public bool TryGetAimRay(out Ray ray)
            {
                ray = default;
                return false;
            }
        }

        private static readonly List<(float time, Action action)> s_Steps = new List<(float, Action)>();
        private static bool s_Started;
        private static float s_Start;
        private static int s_Next;
        private static int s_Errors;
        private static int s_Failures;
        private static Bot s_Bot;
        private static bool s_TutorialWas;
        private static readonly string[] s_MetaKeys =
        {
            "meta.coins", "meta.owned", "meta.equip.Gun", "meta.equip.Axe", "meta.equip.Bullet", "meta.freecases", "meta.daily.day",
            "meta.daily.date",
        };
        private static readonly HashSet<string> s_IntKeys = new HashSet<string> { "meta.coins", "meta.freecases", "meta.daily.day", "meta.daily.date" };
        private static readonly Dictionary<string, string> s_Saved = new Dictionary<string, string>();

        // The editor's PlayerPrefs are the developer's own: kept aside and put back after the walkthrough.
        private static void SaveMeta()
        {
            s_Saved.Clear();
            foreach (string key in s_MetaKeys)
            {
                if (PlayerPrefs.HasKey(key))
                    s_Saved[key] = s_IntKeys.Contains(key) ? PlayerPrefs.GetInt(key).ToString() : PlayerPrefs.GetString(key);
            }
        }

        private static void RestoreMeta()
        {
            foreach (string key in s_MetaKeys)
            {
                if (!s_Saved.TryGetValue(key, out string value))
                    PlayerPrefs.DeleteKey(key);
                else if (s_IntKeys.Contains(key))
                    PlayerPrefs.SetInt(key, int.Parse(value));
                else
                    PlayerPrefs.SetString(key, value);
            }
            PlayerPrefs.Save();
        }

        static SkinShots()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Skin Screenshots")]
        public static void Run()
        {
            s_Started = false;
            s_Steps.Clear();
            s_Next = 0;
            EditorSceneManager.OpenScene(SceneBuilder.c_MenuScene, OpenSceneMode.Single);
            SessionState.SetInt(c_StateKey, 1);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            int state = SessionState.GetInt(c_StateKey, 0);
            if (state == 2)
            {
                if (EditorApplication.isPlaying)
                    return;
                EditorApplication.update -= Tick;
                SessionState.SetInt(c_StateKey, 0);
                bool ok = s_Errors == 0 && s_Failures == 0;
                Debug.Log($"[SkinShots] Finished: {s_Failures} failed checks, {s_Errors} errors -> {(ok ? "PASS" : "FAIL")}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(ok ? 0 : 1);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
            {
                s_Started = true;
                s_Start = Time.realtimeSinceStartup;
                s_Errors = s_Failures = 0;
                Application.logMessageReceived += (m, st, type) =>
                {
                    if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    {
                        s_Errors++;
                        Debug.Log($"[SkinShots] error seen: {m}");
                    }
                };
                BuildSteps();
            }
            float t = Time.realtimeSinceStartup - s_Start;
            while (s_Next < s_Steps.Count && t >= s_Steps[s_Next].time)
            {
                try
                {
                    s_Steps[s_Next++].action();
                }
                catch (Exception e)
                {
                    s_Failures++;
                    Debug.LogError($"[SkinShots] step failed: {e}");
                }
            }
        }

        private static void Check(bool condition, string what)
        {
            if (!condition)
                s_Failures++;
            Debug.Log($"[SkinShots] {(condition ? "PASS" : "FAIL")}: {what}");
        }

        private static void BuildSteps()
        {
            float t = 0.5f;
            void At(float time, Action action) => s_Steps.Add((time, action));
            MainMenuController Menu() => Object.FindAnyObjectByType<MainMenuController>();
            CosmeticCatalog catalog = CosmeticsBuilder.Catalog;

            At(t, () =>
            {
                s_TutorialWas = MetaProgress.TutorialDone;
                SaveMeta();
                MetaProgress.ResetAll();
                MetaProgress.TutorialDone = false;
                MetaProgress.AddCoins(1000);
                Shot("01_Menu");
                Menu().Cases.Show();
            });
            At(t += 0.6f, () =>
            {
                Check(Menu().Cases.IsOpen, "the cases screen opens from the menu");
                Check(CaseChances(Menu()).Contains("???") && !CaseChances(Menu()).Contains("+15%"),
                    "the legendary rifle's bonus is a secret on the case (???)");
                Shot("02_Cases");
                Menu().Cases.ToggleChances();
                Check(Menu().Cases.ChancesShown, "the one \"?\" over the cases opens the chances");
            });
            At(t += 0.4f, () =>
            {
                Shot("02b_Cases_Chances");
                Menu().Cases.ToggleChances();
                Check(!Menu().Cases.ChancesShown, "pressing again closes the chances");
                Check(Menu().Cases.BuyCase(catalog.Cases[0]), "the rifle case opens for 150 coins");
                Check(MetaProgress.Coins <= 1000 - 150 + 60, $"coins after the case: {MetaProgress.Coins}");
            });
            At(t += 1.2f, () => Shot("03_Roulette_Spinning"));
            At(t += 3.8f, () =>
            {
                CasesScreen cases = Menu().Cases;
                Check(!cases.IsSpinning && cases.LastPrize != null && MetaProgress.Owns(cases.LastPrize),
                    $"the roulette stopped on {cases.LastPrize?.Title} ({cases.LastPrize?.Rarity}), now owned");
                Shot("04_Roulette_Prize");
                // A legendary prize: the fireworks and the fanfare (put on the reveal by hand, the roll is random).
                CosmeticItem legend = catalog.Items.First(i => i.Rarity == Rarity.Legendary && i.Slot == CosmeticSlot.Gun);
                Click(Menu().Cases, "OkButton");
                Menu().Cases.ShowPrize(legend);
            });
            At(t += 1.2f, () =>
            {
                Check(Object.FindAnyObjectByType<SkinRevealView>().FireworksPlaying, "a legendary skin sets off fireworks");
                Shot("04b_Legendary_Fireworks");
                Click(Menu().Cases, "OkButton");
                // The 5th day's bonus: the legendary case, a gold roulette of the legendary skins.
                MetaProgress.SetDaily(4, MetaProgress.Today - 1);
                Check(Menu().Daily.Take() && Menu().Cases.IsSpinning, "day 5: the legendary case spins on the cases screen");
            });
            At(t += 1.2f, () => Shot("04c_LegendaryCase_Spinning"));
            At(t += 3.8f, () =>
            {
                CosmeticItem prize = Menu().Cases.LastPrize;
                Check(prize != null && prize.Rarity == Rarity.Legendary && !prize.IsExclusive && MetaProgress.Owns(prize),
                    $"the legendary case gives a legendary skin: {prize?.Title} ({prize?.Slot})");
                Shot("04d_LegendaryCase_Prize");
                Click(Menu().Cases, "OkButton");
                // The 7th day's bonus: the black bullets, never in a case.
                MetaProgress.SetDaily(6, MetaProgress.Today - 1);
                Check(catalog.Droppable(CosmeticSlot.Bullet).All(i => i.Id != "bullet_black") &&
                      catalog.LegendaryPool().All(i => i.Id != "bullet_black"), "no case drops the black bullets");
                Check(Menu().Daily.Take() && MetaProgress.Owns(catalog.Find("bullet_black")), "day 7: the black bullets are won");
            });
            At(t += 1.2f, () =>
            {
                Shot("04e_BlackBullets_Prize");
                Click(Menu().Cases, "OkButton");
            });
            At(t += 0.4f, () =>
            {
                Check(CaseChances(Menu()).Contains("+15%"), "won: the case now tells the legendary rifle's bonus");
                // The skins for the map: gold rifle, neon axe, fire bullets (new: counted on СКИНЫ).
                foreach (string id in new[] { "gun_gold", "axe_neon", "bullet_fire" })
                    MetaProgress.Grant(catalog.Find(id));
                Menu().Cases.Hide();
            });
            At(t += 0.5f, () =>
            {
                Check(Badge(Menu(), "SkinsButton"), "new skins are counted on СКИНЫ");
                Shot("05_Menu_NewSkins");
                Click(Menu(), "SkinsButton");
            });
            At(t += 0.6f, () =>
            {
                SkinsPanel skins = Menu().Skins;
                Check(skins.IsOpen && skins.CurrentTab == SkinsPanel.CharactersTab, "СКИНЫ opens on the characters");
                Shot("05d_Characters");
                Click(skins, "Locked2");
            });
            At(t += 0.35f, () =>
            {
                SkinsPanel skins = Menu().Skins;
                Check(skins.BubbleShown && skins.BubbleText == "В РАЗРАБОТКЕ :)", "a ? card says В РАЗРАБОТКЕ :)");
                Shot("05e_Characters_InDevelopment");
                Click(skins, "Tab1");
            });
            At(t += 0.4f, () =>
            {
                SkinsPanel skins = Menu().Skins;
                Check(skins.CurrentTab == 1 && skins.Tiles.Count(tile => tile.gameObject.activeSelf) == 10, "ОРУЖИЕ: the 10 rifle skins");
                ItemTile gold = skins.Tiles.First(tile => tile.Item != null && tile.Item.Id == "gun_gold");
                Check(gold.transform.Find("Bonus").gameObject.activeSelf, "the legendary rifle shows its bonus (+15% к скорострельности)");
                gold.Button.onClick.Invoke();
            });
            At(t += 0.3f, () =>
            {
                Check(catalog.Equipped(CosmeticSlot.Gun).Id == "gun_gold", "a tap on the gold rifle puts it on");
                Shot("05_Collection");
                Click(Menu().Skins, "Tab2");
            });
            At(t += 0.4f, () =>
            {
                MetaProgress.Equip(catalog.Find("axe_neon"));
                Click(Menu().Skins, "Tab3");
            });
            At(t += 0.4f, () =>
            {
                MetaProgress.Equip(catalog.Find("bullet_black"));
                Check(Menu().Skins.CurrentTab == 3, "ЭФФЕКТ ПУЛЬ");
                Shot("05f_Collection_BulletEffects");
                Menu().Skins.Close();
            });
            At(t += 0.4f, () => Check(!Badge(Menu(), "SkinsButton"), "every new skin seen: the count on СКИНЫ is gone"));
            At(t += 0.5f, () => Click(Menu(), "PlayButton"));
            At(t += 0.8f, () =>
            {
                Shot("05b_Map_Select");
                Click(Object.FindAnyObjectByType<MapSelectScreen>(FindObjectsInactive.Include).Current, "RecordsButton");
            });
            At(t += 0.8f, () =>
            {
                var records = Object.FindAnyObjectByType<MapRecordsPanel>();
                Check(records != null && records.IsOpen, "РЕКОРДЫ opens the map's records");
                Shot("05c_Map_Records");
                records.Close();
            });
            At(t += 0.4f, () => Click(Object.FindAnyObjectByType<MapSelectScreen>(FindObjectsInactive.Include).Current, "PlayButton"));
            // ИГРАТЬ asks for the difficulty first: СРЕДНИЙ, from wave 1.
            At(t += 0.6f, () =>
            {
                var difficulty = Object.FindAnyObjectByType<DifficultyScreen>();
                Check(difficulty != null && difficulty.State == "cards", "ИГРАТЬ opens the difficulty choice");
                difficulty?.Choose(Difficulty.Normal);
                if (difficulty != null && difficulty.State == "resume")
                    difficulty.StartOver();
            });

            // Map01 with the skins.
            At(t += 3.5f, () =>
            {
                InstallBot();
                // The first run opens the intro cards over a paused game; they are closed before the walk goes on.
                var cards = Object.FindAnyObjectByType<TutorialCardsView>();
                Check(cards != null && cards.IsOpen && GamePause.Has(GamePause.Reason.Tutorial), "first run: the tutorial cards open");
                Shot("06_Map_Tutorial");
                cards?.Close();
                Teleport(new Vector3(-3f, 0f, -5f));
                s_Bot.AimDirection = Vector3.right;
                s_Bot.FireHeld = true;
            });
            At(t += 0.9f, () =>
            {
                Check(Object.FindAnyObjectByType<PlayerView>().transform.Find("Body/GunSkin").GetComponent<SpriteRenderer>().enabled,
                    "the rifle skin overlay is drawn");
                var run = AssetDatabase.LoadAssetAtPath<RunState>(DataAssetsBuilder.c_RunState);
                Check(run.Modifiers[StatEffect.FireRate] >= 0.15f - 0.001f, $"the gold rifle's bonus is on in the run: fire rate +{run.Modifiers[StatEffect.FireRate]:P0}");
                Check(run.Modifiers[StatEffect.BulletPierce] >= 1f - 0.001f,
                    $"the black bullets' bonus is on from the start: pierce +{run.Modifiers[StatEffect.BulletPierce]:0}");
                CloseUp("07_GoldRifle_BlackBullets", 7f);
            });
            // More close-ups a moment apart: one of them catches a bullet in flight.
            At(t += 0.07f, () => CloseUp("07b_BlackBullets", 5f));
            At(t += 0.07f, () => CloseUp("07c_BlackBullets", 5f));
            At(t += 0.3f, () =>
            {
                s_Bot.FireHeld = false;
                s_Bot.Slot = 1;
                s_Bot.SlotUntilFrame = Time.frameCount + 2;
            });
            At(t += 0.6f, () =>
            {
                var melee = Object.FindAnyObjectByType<PlayerMelee>();
                var body = Object.FindAnyObjectByType<PlayerView>().transform.Find("Body").GetComponent<SpriteRenderer>();
                Check(melee.Active && body.sprite.name.StartsWith(PlayerGunMask.c_NoGunPrefix),
                    $"axe in hand: body frame {body.sprite.name} (no rifle)");
                s_Bot.FireHeld = true;
            });
            At(t += 0.25f, () => CloseUp("08_Axe_NoRifle_Swing", 5f));
            At(t += 0.5f, () =>
            {
                s_Bot.FireHeld = false;
                CloseUp("09_Axe_NoRifle", 5f);
                // All four spike sets.
                var run = AssetDatabase.LoadAssetAtPath<RunState>(DataAssetsBuilder.c_RunState);
                var upgrades = AssetDatabase.LoadAssetAtPath<CarUpgradeCatalog>(DataAssetsBuilder.c_CarUpgrades);
                CarUpgrade spikes = upgrades.Upgrades.First(u => u.Id == "spikes");
                run.AddMoney(5000);
                for (int i = 0; i < spikes.RegularLevels; i++)
                    run.TryBuy(spikes);
                Teleport(new Vector3(0f, 0f, -7f));
            });
            At(t += 1.2f, () =>
            {
                var car = Object.FindAnyObjectByType<CarHealth>();
                int shown = car.GetComponent<CarSpikesView>() != null
                    ? car.transform.Find("Spikes").Cast<Transform>().Count(c => c.gameObject.activeSelf) : 0;
                Check(car.Spikes == 78 && shown == 4, $"spikes {car.Spikes} damage, {shown} sets on the car");
                CloseUp("10_Car_Spikes", 9f, car.transform.position);
            });
            At(t += 0.4f, () =>
            {
                RestoreMeta();
                MetaProgress.Reload();
                MetaProgress.TutorialDone = s_TutorialWas;
                SessionState.SetInt(c_StateKey, 2);
                EditorApplication.ExitPlaymode();
            });
        }

        // The legendary bonus plate of the rifle case.
        private static string CaseChances(Component menu) =>
            menu.GetComponentsInChildren<Transform>(true).First(t => t.name == "Case1").Find("Bonus/Text").GetComponent<TMPro.TMP_Text>().text;

        // The new-skins count on a button is showing.
        private static bool Badge(Component root, string button)
        {
            Transform b = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(tr => tr.name == button);
            Transform badge = b != null ? b.Find("NewBadge") : null;
            return badge != null && badge.gameObject.activeSelf;
        }

        private static void Click(Component root, string name)
        {
            Button button = root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            if (button == null || !button.IsInteractable())
            {
                Debug.LogError($"[SkinShots] Button {name} not found or disabled under {root.name}");
                return;
            }
            button.onClick.Invoke();
        }

        private static void Teleport(Vector3 position)
        {
            var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            var controller = weapon.GetComponent<CharacterController>();
            controller.enabled = false;
            weapon.transform.position = position;
            controller.enabled = true;
            Object.FindAnyObjectByType<CameraFollowTarget>().Snap();
            var vcam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
                vcam.PreviousStateIsValid = false;
        }

        private static void InstallBot()
        {
            var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Bot = new Bot();
            var provider = weapon.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in weapon.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }
            // The tutorial reads the input through the provider too.
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }
        }

        private static string Path(string name)
        {
            string folder = System.IO.Path.GetFullPath(c_Folder);
            Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, name + ".png");
        }

        private static void Shot(string name)
        {
            Camera cam = Camera.main;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.5f - canvas.sortingOrder * 0.001f;
            }
            cam.cullingMask |= 1 << UiFactory.c_UiLayer;
            MapScreenshots.Render(cam, 1920, 1080, Path(name));
        }

        // A close camera on the player (or a point) without the HUD, to see the sprite and the car.
        private static void CloseUp(string name, float distance, Vector3? at = null)
        {
            Camera main = Camera.main;
            Vector3 target = (at ?? Object.FindAnyObjectByType<PlayerWeapon>().transform.position) + Vector3.up * 1f;
            var go = new GameObject("SkinShotCamera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.cullingMask = main.cullingMask & ~(1 << UiFactory.c_UiLayer);
            go.transform.rotation = main.transform.rotation;
            go.transform.position = target - go.transform.forward * distance;
            MapScreenshots.Render(cam, 1280, 900, Path(name));
            Object.DestroyImmediate(go);
        }
    }
}
