using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombiCar.Characters;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// 2D characters from referens/characters.png (cut by tools/characters/slice_characters.py): sprite clips
    /// (Data/Characters), zombie configs (Data/Zombies, created once with the stats below, then tuned in the
    /// Inspector), zombie prefabs (Prefabs/Enemies) and the wave line-up (the player: PlayerBuilder).
    /// Called by <see cref="PrefabBuilder"/>.
    /// </summary>
    public static class CharacterBuilder
    {
        // The zombie sheet draws every character at one scale: 148 px (a standing figure) is 1.8 m.
        private const float c_SheetPixelsPerMetre = 148f / 1.8f;
        internal const float c_ShadowHeight = 0.08f;                 // above decals and the workshop's soil slab
        internal const int c_ShadowSortingOrder = -1;                // ground shadows before every character sprite
        internal const string c_SpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        internal const string c_QuadMesh = EditorPaths.c_GeneratedMeshes + "/SM_QuadXZ.asset";
        // Unlit sprites are painted fully lit: a warm tint sits them in the sunset.
        internal static readonly Color s_SpriteTint = new Color(1f, 0.93f, 0.85f, 1f);

        private sealed class Look
        {
            public string Name;
            public float Size = 1f;                                  // relative to the sheet scale
            public float PixelScale = 1f;                            // frames upscaled by this (tools/characters/boss_frames.py)
            public Vector2 Shadow = new Vector2(1.2f, 0.6f);        // blob width (x) and depth (z), metres
            public (CharacterAnim anim, string[] frames, float fps, bool loop)[] Clips;
        }

        private sealed class ZombieSpec
        {
            public Look Look;
            public float AgentRadius = 0.45f;                        // crowd spacing: the sprites are ~1 m wide
            public float ColliderRadius = 0.4f;
            public float ColliderHeight = 1.8f;                      // >= 1.3 m: bullets fly at ~1.05 m
            // Initial stats of the ZombieConfig asset.
            public int Health;
            public float Speed;
            public float SpeedVariation = 0.15f;
            public int Damage;
            public float AttackInterval;
            public float AttackRange = 0.6f;
            public float AttackAnimSpeed = 1f;
            public ZombieTarget Preference = ZombieTarget.Car;
            public float Aggro = 4.5f;
            public float Stagger = 0.2f;
            public float Knockback = 0.2f;
            public int Xp = 5;
            public int Reward = 2;
            // Wave line-up.
            public int FromWave;
            public float Weight;
            // Bosses.
            public string DisplayName;
            public BossSpec Boss;
        }

        /// <summary>Abilities of a boss (written once into its BossConfig asset) and where it comes.</summary>
        private sealed class BossSpec
        {
            public int Wave;
            public float RegularShare = 0.5f;
            public string[] Minions = Array.Empty<string>();
            public (string field, object value)[] Abilities = Array.Empty<(string, object)>();
        }

        /// <summary>Numeric frame indices shared by one character's clips (characters.png, one sheet per character).</summary>
        private static string[] Idx(params int[] indices) => Array.ConvertAll(indices, i => i.ToString());

        // Sheet columns: 0-1 idle, 2-3 walk / lunge, 4 attack, 5 hit, 6 dead (see referens/characters.png).
        private static (CharacterAnim, string[], float, bool)[] HumanoidClips(int[] walk, float walkFps) => new[]
        {
            (CharacterAnim.Idle, Idx(0, 1), 2f, true),
            (CharacterAnim.Move, Idx(walk), walkFps, true),
            (CharacterAnim.Attack, Idx(3, 4), 5f, false),
            (CharacterAnim.Hit, Idx(5), 1f, true),
            (CharacterAnim.Dead, Idx(5, 6), 5f, false),
        };

        private static readonly ZombieSpec[] s_Zombies =
        {
            new ZombieSpec
            {
                Look = new Look { Name = "Walker", Clips = HumanoidClips(new[] { 2, 1 }, 3.5f) },
                Health = 30, Speed = 1.3f, Damage = 4, AttackInterval = 1.3f, Xp = 5, Reward = 2, FromWave = 1, Weight = 5f,
            },
            new ZombieSpec
            {
                // Hard hat: takes twice the bullets, barely flinches.
                Look = new Look { Name = "Worker", Clips = HumanoidClips(new[] { 2, 1 }, 3.2f) },
                Health = 60, Speed = 1.15f, SpeedVariation = 0.1f, Damage = 5, AttackInterval = 1.4f,
                Stagger = 0.12f, Knockback = 0.1f, Xp = 10, Reward = 3, FromWave = 2, Weight = 2f,
            },
            new ZombieSpec
            {
                // Runner.
                Look = new Look { Name = "RedCap", Clips = HumanoidClips(new[] { 2, 3 }, 5f) },
                Health = 25, Speed = 2.1f, Damage = 4, AttackInterval = 1.1f, AttackAnimSpeed = 1.2f, Aggro = 6f,
                Knockback = 0.25f, Xp = 5, Reward = 3, FromWave = 2, Weight = 2f,
            },
            new ZombieSpec
            {
                // Stalker: hunts the player instead of the car.
                Look = new Look { Name = "Hoodie", Clips = HumanoidClips(new[] { 2, 3 }, 4f) },
                Health = 40, Speed = 1.6f, Damage = 6, AttackInterval = 1.2f, Preference = ZombieTarget.Player,
                Xp = 7, Reward = 3, FromWave = 3, Weight = 1.5f,
            },
            new ZombieSpec
            {
                Look = new Look
                {
                    Name = "Hound", Size = 0.9f, Shadow = new Vector2(1.7f, 0.7f),
                    Clips = new[]
                    {
                        (CharacterAnim.Idle, Idx(0, 1), 3f, true),
                        (CharacterAnim.Move, Idx(2, 3), 7f, true),
                        (CharacterAnim.Attack, Idx(3, 4), 6f, false),
                        (CharacterAnim.Hit, Idx(5), 1f, true),
                        (CharacterAnim.Dead, Idx(5, 6), 5f, false),
                    },
                },
                AgentRadius = 0.5f, ColliderRadius = 0.5f, ColliderHeight = 1.6f,
                Health = 18, Speed = 3.6f, SpeedVariation = 0.1f, Damage = 3, AttackInterval = 0.8f, AttackRange = 0.5f,
                Preference = ZombieTarget.Player, Stagger = 0.15f, Knockback = 0.35f, Xp = 4, Reward = 2, FromWave = 4, Weight = 1.2f,
            },
            new ZombieSpec
            {
                Look = new Look
                {
                    Name = "Brute", Size = 1.35f, Shadow = new Vector2(2f, 0.95f),
                    Clips = new[]
                    {
                        (CharacterAnim.Idle, Idx(0, 1), 1.6f, true),
                        (CharacterAnim.Move, Idx(2, 1), 2.4f, true),
                        (CharacterAnim.Attack, Idx(3, 4), 3f, false),
                        (CharacterAnim.Hit, Idx(5), 1f, true),
                        (CharacterAnim.Dead, Idx(5, 6), 4f, false),
                    },
                },
                AgentRadius = 0.75f, ColliderRadius = 0.75f, ColliderHeight = 2.6f,
                Health = 250, Speed = 0.95f, SpeedVariation = 0.05f, Damage = 20, AttackInterval = 2f, AttackRange = 0.8f,
                Aggro = 3f, Stagger = 0f, Knockback = 0f, Xp = 40, Reward = 15, FromWave = 5, Weight = 0.5f,
            },
        };

        private static (CharacterAnim, string[], float, bool)[] BruteClips() => new[]
        {
            (CharacterAnim.Idle, Idx(0, 1), 1.6f, true),
            (CharacterAnim.Move, Idx(2, 1), 2.4f, true),
            (CharacterAnim.Attack, Idx(3, 4), 3f, false),
            (CharacterAnim.Hit, Idx(5), 1f, true),
            (CharacterAnim.Dead, Idx(5, 6), 4f, false),
        };

        // Bosses of waves 10 / 20 / 30: recoloured, 2x upscaled brute / worker frames (tools/characters/boss_frames.py).
        private static readonly ZombieSpec[] s_Bosses =
        {
            new ZombieSpec
            {
                // Brood mother: slow, goes for the car, keeps calling minions out of the ground.
                Look = new Look { Name = "Boss1", Size = 1.9f, PixelScale = 2f, Shadow = new Vector2(2.1f, 1f), Clips = BruteClips() },
                AgentRadius = 1f, ColliderRadius = 1f, ColliderHeight = 3.4f,
                Health = 1600, Speed = 0.85f, SpeedVariation = 0.02f, Damage = 25, AttackInterval = 2.2f, AttackRange = 1f,
                AttackAnimSpeed = 0.9f, Aggro = 4f, Stagger = 0f, Knockback = 0f, Xp = 300, Reward = 150,
                DisplayName = "МАТКА",
                Boss = new BossSpec
                {
                    Wave = 10, RegularShare = 0.5f, Minions = new[] { "Walker", "RedCap", "Hound" },
                    Abilities = new (string, object)[]
                    {
                        ("m_Summon", true), ("m_SummonInterval", 7f), ("m_SummonWindup", 1f), ("m_SummonCount", 4),
                        ("m_SummonRadius", 2.8f), ("m_MaxMinions", 12), ("m_VoicePitch", 1.1f),
                        ("m_AuraColor", new Color(0.5f, 1f, 0.3f, 0.55f)),
                    },
                },
            },
            new ZombieSpec
            {
                // Juggernaut: hunts the player, charges and spits acid; dazed after a charge.
                Look = new Look { Name = "Boss2", Size = 2.1f, PixelScale = 2f, Shadow = new Vector2(1.6f, 0.8f),
                    Clips = HumanoidClips(new[] { 2, 1 }, 3.2f) },
                AgentRadius = 0.9f, ColliderRadius = 0.85f, ColliderHeight = 3.6f,
                Health = 3200, Speed = 1.2f, SpeedVariation = 0.02f, Damage = 30, AttackInterval = 1.6f, AttackRange = 1f,
                Preference = ZombieTarget.Player, Aggro = 30f, Stagger = 0f, Knockback = 0f, Xp = 600, Reward = 300,
                DisplayName = "ТАРАН",
                Boss = new BossSpec
                {
                    Wave = 20, RegularShare = 0.5f,
                    Abilities = new (string, object)[]
                    {
                        ("m_Spit", true), ("m_SpitInterval", 5f), ("m_SpitVolley", 3), ("m_SpitDamage", 14), ("m_SpitRadius", 1.5f),
                        ("m_Charge", true), ("m_ChargeInterval", 8f), ("m_ChargeWindup", 1f), ("m_ChargeSpeed", 13f),
                        ("m_ChargeDistance", 12f), ("m_ChargeMinRange", 3.5f), ("m_ChargeDamage", 30), ("m_ChargeCarDamage", 40),
                        ("m_VoicePitch", 0.9f), ("m_AuraColor", new Color(0.75f, 0.45f, 1f, 0.55f)),
                    },
                },
            },
            new ZombieSpec
            {
                // The reaper (final): everything the other two do, then a second phase with the ground slam.
                Look = new Look { Name = "Boss3", Size = 2.3f, PixelScale = 2f, Shadow = new Vector2(2.5f, 1.2f), Clips = BruteClips() },
                AgentRadius = 1.15f, ColliderRadius = 1.15f, ColliderHeight = 4f,
                Health = 6500, Speed = 1f, SpeedVariation = 0.02f, Damage = 40, AttackInterval = 1.8f, AttackRange = 1.2f,
                AttackAnimSpeed = 0.9f, Aggro = 5f, Stagger = 0f, Knockback = 0f, Xp = 1000, Reward = 500,
                DisplayName = "ЖНЕЦ",
                Boss = new BossSpec
                {
                    Wave = 30, RegularShare = 0.4f, Minions = new[] { "Walker", "Worker", "RedCap", "Hoodie", "Hound" },
                    Abilities = new (string, object)[]
                    {
                        ("m_Summon", true), ("m_SummonInterval", 9f), ("m_SummonWindup", 1f), ("m_SummonCount", 5),
                        ("m_SummonRadius", 3.2f), ("m_MaxMinions", 14),
                        ("m_Spit", true), ("m_SpitInterval", 6f), ("m_SpitVolley", 5), ("m_SpitDamage", 16), ("m_SpitRadius", 1.6f),
                        ("m_Charge", true), ("m_ChargeInterval", 10f), ("m_ChargeWindup", 0.9f), ("m_ChargeSpeed", 14f),
                        ("m_ChargeDamage", 36), ("m_ChargeCarDamage", 45),
                        ("m_Enrage", true), ("m_EnrageAt", 0.5f), ("m_EnrageSpeed", 1.35f), ("m_EnrageDamage", 1.3f),
                        ("m_EnrageCooldowns", 0.6f), ("m_EnrageScale", 1.12f), ("m_EnrageTint", new Color(1f, 0.6f, 0.5f, 1f)),
                        ("m_Slam", true), ("m_SlamInterval", 6f), ("m_SlamRadius", 4.8f), ("m_SlamDamage", 30), ("m_SlamCarDamage", 25),
                        ("m_VoicePitch", 0.78f), ("m_AuraColor", new Color(1f, 0.55f, 0.2f, 0.6f)),
                    },
                },
            },
        };

        /// <summary>Sprite clip assets, zombie configs + prefabs and the wave line-up.</summary>
        public static void BuildAll()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_DataCharacters);
            EditorPaths.EnsureFolder(EditorPaths.c_DataZombies);
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsEnemies);
            EnsureSpriteImport();

            var configs = new List<ZombieConfig>();
            foreach (ZombieSpec spec in s_Zombies)
                configs.Add(BuildZombie(spec, BuildSprites(spec.Look)));
            ConfigureWaves(configs);
            AcidGlob glob = BuildAcidGlob();
            var bosses = new List<WaveConfig.BossWave>();
            foreach (ZombieSpec spec in s_Bosses)
            {
                ZombieConfig boss = BuildZombie(spec, BuildSprites(spec.Look), configs, glob);
                bosses.Add(new WaveConfig.BossWave(spec.Boss.Wave, boss, spec.Boss.RegularShare));
            }
            ConfigureBosses(bosses);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterBuilder] {configs.Count} zombie types, {bosses.Count} bosses.");
        }

        // ------------------------------------------------------------------ sprites
        /// <summary>Frames imported before the import rules existed are re-imported as sprites.</summary>
        private static void EnsureSpriteImport()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { EditorPaths.c_CharacterTextures }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static Sprite Frame(string name, string suffix)
        {
            string path = $"{EditorPaths.c_CharacterTextures}/{name}/{name}_{suffix}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException($"[CharacterBuilder] No sprite at {path} (run tools/characters/slice_characters.py)");
            return sprite;
        }

        private static CharacterSprites BuildSprites(Look look)
        {
            string path = $"{EditorPaths.c_DataCharacters}/{look.Name}_Sprites.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CharacterSprites>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CharacterSprites>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var clips = new List<CharacterSprites.Clip>();
            foreach ((CharacterAnim anim, string[] frames, float fps, bool loop) in look.Clips)
                clips.Add(new CharacterSprites.Clip(anim, Array.ConvertAll(frames, f => Frame(look.Name, f)), fps, loop));
            asset.EditorSetClips(clips.ToArray());
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ------------------------------------------------------------------ zombie visuals
        private static float SpriteScale(Look look) => ViewScale * look.Size / look.PixelScale;

        /// <summary>Sheet pixels -> metres at 100 px per unit.</summary>
        private static float ViewScale => ArtImportPostprocessor.c_CharacterPixelsPerUnit / c_SheetPixelsPerMetre;

        /// <summary>1 / cos(camera pitch): an upright sprite seen from above is not squashed.</summary>
        internal static float PitchCompensation => 1f / Mathf.Cos(ViewConstants.c_CameraPitch * Mathf.Deg2Rad);

        /// <summary>
        /// View (faces the camera) with the Sprite (animated, pivot at the feet) and a blob shadow on the ground.
        /// Returns the animator; <paramref name="visualHeight"/> is the sprite's height in world metres.
        /// </summary>
        private static SpriteAnimator BuildView(GameObject root, Look look, CharacterSprites sprites, bool billboardEveryFrame,
            out Transform view, out Transform shadow, out float visualHeight)
        {
            view = new GameObject("View").transform;
            view.SetParent(root.transform, false);
            var billboard = view.gameObject.AddComponent<YAxisBillboard>();
            SetProps(billboard, ("m_UpdateEveryFrame", billboardEveryFrame));

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(view, false);
            var renderer = spriteGo.AddComponent<SpriteRenderer>();
            Sprite idle = sprites.Get(CharacterAnim.Idle).Frames[0];
            renderer.sprite = idle;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(c_SpriteMaterial);
            renderer.color = s_SpriteTint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            float scale = SpriteScale(look);
            var scale2 = new Vector2(scale, scale * PitchCompensation);
            spriteGo.transform.localScale = new Vector3(scale2.x, scale2.y, 1f);
            var animator = spriteGo.AddComponent<SpriteAnimator>();
            SetProps(animator, ("m_Sprites", sprites), ("m_Scale", scale2), ("m_Tint", s_SpriteTint));
            visualHeight = idle.rect.height / ArtImportPostprocessor.c_CharacterPixelsPerUnit * scale2.y;

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(view, false);
            shadowGo.transform.localPosition = new Vector3(0f, c_ShadowHeight, 0f);
            shadowGo.transform.localScale = new Vector3(look.Shadow.x * look.Size, 1f, look.Shadow.y * look.Size);
            shadowGo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(c_QuadMesh);
            var shadowRenderer = shadowGo.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = MaterialLibrary.Get("M_ShadowBlob");
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowRenderer.sortingOrder = c_ShadowSortingOrder;
            shadow = shadowGo.transform;
            return animator;
        }

        // ------------------------------------------------------------------ zombies
        private static ZombieConfig BuildZombie(ZombieSpec spec, CharacterSprites sprites, List<ZombieConfig> minions = null,
            AcidGlob glob = null)
        {
            string name = "Zombie_" + spec.Look.Name;
            var root = new GameObject(name);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = spec.ColliderRadius;
            collider.height = spec.ColliderHeight;
            collider.center = new Vector3(0f, spec.ColliderHeight * 0.5f, 0f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;                       // moved by the NavMesh agent
            body.useGravity = false;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = spec.AgentRadius;
            agent.height = spec.ColliderHeight;
            agent.speed = spec.Speed;
            agent.acceleration = 14f;
            agent.angularSpeed = 0f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;                         // enabled on spawn, once placed on the NavMesh

            SpriteAnimator animator = BuildView(root, spec.Look, sprites, false, out _, out Transform shadow, out float height);
            var zombie = root.AddComponent<ZombieController>();
            SetProps(zombie, ("m_Animator", animator), ("m_Shadow", shadow), ("m_VisualHeight", height));
            SetLayerRecursively(root, Layers.c_EnemyIndex);
            if (spec.Boss != null)
                BuildBossParts(root, zombie, spec, minions, glob, height);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{EditorPaths.c_PrefabsEnemies}/{name}.prefab");
            Object.DestroyImmediate(root);

            string configPath = $"{EditorPaths.c_DataZombies}/{name}.asset";
            var config = AssetDatabase.LoadAssetAtPath<ZombieConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ZombieConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                SetProps(config, ("m_MaxHealth", spec.Health), ("m_MoveSpeed", spec.Speed), ("m_SpeedVariation", spec.SpeedVariation),
                    ("m_Damage", spec.Damage), ("m_AttackInterval", spec.AttackInterval), ("m_AttackRange", spec.AttackRange),
                    ("m_AttackAnimSpeed", spec.AttackAnimSpeed), ("m_Preference", (int)spec.Preference), ("m_AggroRadius", spec.Aggro),
                    ("m_StaggerTime", spec.Stagger), ("m_Knockback", spec.Knockback));
            }
            // Fields added after the configs were created: kill rewards and the boss identity follow the lists above.
            SetProps(config, ("m_Prefab", prefab.GetComponent<ZombieController>()), ("m_Xp", spec.Xp), ("m_Reward", spec.Reward),
                ("m_IsBoss", spec.Boss != null));
            var so = new SerializedObject(config);
            so.FindProperty("m_DisplayName").stringValue = spec.DisplayName ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        // ------------------------------------------------------------------ bosses
        private const float c_BossFxHeight = 0.07f;

        /// <summary>
        /// BossBrain with its abilities asset (Data/Zombies/Boss*_Abilities, created once, then tuned in the Inspector),
        /// the health plate, the aura and the ground telegraphs (summon ring, charge path, slam shockwave).
        /// </summary>
        private static void BuildBossParts(GameObject root, ZombieController zombie, ZombieSpec spec, List<ZombieConfig> minions,
            AcidGlob glob, float height)
        {
            string abilitiesPath = $"{EditorPaths.c_DataZombies}/{spec.Look.Name}_Abilities.asset";
            var abilities = AssetDatabase.LoadAssetAtPath<BossConfig>(abilitiesPath);
            if (abilities == null)
            {
                abilities = ScriptableObject.CreateInstance<BossConfig>();
                AssetDatabase.CreateAsset(abilities, abilitiesPath);
                SetProps(abilities, spec.Boss.Abilities);
                ZombieConfig[] minionConfigs = spec.Boss.Minions
                    .Select(n => minions?.FirstOrDefault(c => c != null && c.name == "Zombie_" + n)).Where(c => c != null).ToArray();
                SetProps(abilities, ("m_Minions", minionConfigs.Cast<Object>().ToArray()));
            }

            Material aura = FxBuilder.Material("M_FX_BossAura", "FX_Aura", Color.white, true);
            Material ring = FxBuilder.Material("M_FX_BossTelegraph", "FX_TargetRing", Color.white, false);
            Material line = FxBuilder.Material("M_FX_BossCharge", "FX_ChargeLine", Color.white, false);
            Material wave = FxBuilder.Material("M_FX_BossShockwave", "FX_ShockRing", Color.white, true);
            float size = spec.AgentRadius * 3.6f;
            MeshRenderer auraRenderer = FxBuilder.GroundQuad("Aura", root.transform, aura, size, c_BossFxHeight);
            MeshRenderer telegraph = FxBuilder.GroundQuad("Telegraph", root.transform, ring, 1f, c_BossFxHeight + 0.01f);
            MeshRenderer charge = FxBuilder.GroundQuad("ChargePath", root.transform, line, 1f, c_BossFxHeight + 0.02f);
            MeshRenderer shock = FxBuilder.GroundQuad("Shockwave", root.transform, wave, 1f, c_BossFxHeight + 0.03f);
            foreach (MeshRenderer r in new[] { auraRenderer, telegraph, charge, shock })
                r.gameObject.layer = 0;

            var brain = root.AddComponent<BossBrain>();
            PrefabBuilder.Wire(brain, ("m_Config", abilities), ("m_GlobPrefab", glob), ("m_Aura", auraRenderer), ("m_Telegraph", telegraph),
                ("m_ChargeLine", charge), ("m_Shockwave", shock));
            BossHealthPlate plate = WorldPlateBuilder.BuildBossPlate(root, zombie, brain, height + 0.35f);
            SetLayerRecursively(plate.transform.parent.gameObject, UiFactory.c_UiLayer);
        }

        /// <summary>The boss's acid glob: a billboarded blob, its shadow, the warning circle and the splash.</summary>
        private static AcidGlob BuildAcidGlob()
        {
            var root = new GameObject("Boss_AcidGlob");
            var ballRoot = new GameObject("Ball");
            ballRoot.transform.SetParent(root.transform, false);
            SetProps(ballRoot.AddComponent<YAxisBillboard>(), ("m_UpdateEveryFrame", true));
            var ball = new GameObject("Sprite");
            ball.transform.SetParent(ballRoot.transform, false);
            ball.transform.localScale = new Vector3(0.7f, 0.7f * PitchCompensation, 1f);
            ball.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var ballRenderer = ball.AddComponent<MeshRenderer>();
            ballRenderer.sharedMaterial = FxBuilder.Material("M_FX_AcidBall", "FX_AcidBall", Color.white, false);
            ballRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ballRenderer.receiveShadows = false;

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(root.transform, false);
            shadowGo.transform.localScale = new Vector3(0.8f, 1f, 0.5f);
            shadowGo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(c_QuadMesh);
            var shadowRenderer = shadowGo.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = MaterialLibrary.Get("M_ShadowBlob");
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;

            MeshRenderer target = FxBuilder.GroundQuad("Target", root.transform,
                FxBuilder.Material("M_FX_SpitTarget", "FX_TargetRing", new Color(1f, 0.3f, 0.2f, 0.85f), false), 1f, c_BossFxHeight);
            MeshRenderer splat = FxBuilder.GroundQuad("Splat", root.transform,
                FxBuilder.Material("M_FX_AcidSplat", "FX_AcidSplat", Color.white, false), 1f, c_BossFxHeight + 0.01f);
            var glob = root.AddComponent<AcidGlob>();
            PrefabBuilder.Wire(glob, ("m_Ball", ballRoot.transform), ("m_Shadow", shadowGo.transform), ("m_Target", target), ("m_Splat", splat));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{EditorPaths.c_PrefabsEnemies}/Boss_AcidGlob.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<AcidGlob>();
        }

        /// <summary>The bosses of waves 10 / 20 / 30 (the map has at least 30 waves).</summary>
        private static void ConfigureBosses(List<WaveConfig.BossWave> bosses)
        {
            WaveConfig waves = DataAssetsBuilder.Load<WaveConfig>(DataAssetsBuilder.c_WaveConfig);
            waves.EditorSetBosses(bosses.ToArray(), bosses.Max(b => b.Wave));
            EditorUtility.SetDirty(waves);
        }

        /// <summary>Fills the Map01 wave line-up once (kept when edited in the Inspector).</summary>
        private static void ConfigureWaves(List<ZombieConfig> configs)
        {
            WaveConfig waves = DataAssetsBuilder.Load<WaveConfig>(DataAssetsBuilder.c_WaveConfig);
            if (waves.Enemies != null && waves.Enemies.Length > 0)
                return;
            var entries = new WaveConfig.Entry[s_Zombies.Length];
            for (int i = 0; i < s_Zombies.Length; i++)
                entries[i] = new WaveConfig.Entry(configs[i], s_Zombies[i].FromWave, s_Zombies[i].Weight);
            waves.EditorSetEnemies(entries);
            EditorUtility.SetDirty(waves);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Sets serialized fields (object references, bool, int / enum, float, Vector2/3, Color, object arrays).</summary>
        public static void SetProps(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, object value) in values)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                    throw new InvalidOperationException($"{target.GetType().Name} has no serialized field {field}");
                switch (value)
                {
                    case null: p.objectReferenceValue = null; break;
                    case Object o: p.objectReferenceValue = o; break;
                    case Object[] array:
                        p.arraySize = array.Length;
                        for (int i = 0; i < array.Length; i++)
                            p.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                        break;
                    case bool b: p.boolValue = b; break;
                    case int i: p.intValue = i; break;
                    case float f: p.floatValue = f; break;
                    case Vector2 v2: p.vector2Value = v2; break;
                    case Vector3 v3: p.vector3Value = v3; break;
                    case Color c: p.colorValue = c; break;
                    default: throw new ArgumentException($"Unsupported value type {value.GetType().Name} for {field}");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }
}
