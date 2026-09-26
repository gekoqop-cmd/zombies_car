using System;
using System.Collections.Generic;
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
    /// Inspector), zombie prefabs (Prefabs/Enemies), the sprite view of the player and the wave line-up.
    /// Called by <see cref="PrefabBuilder"/>.
    /// </summary>
    public static class CharacterBuilder
    {
        // The sheet draws every character at one scale: the player's idle frame (148 px) is 1.8 m.
        private const float c_SheetPixelsPerMetre = 148f / 1.8f;
        // Player gun tip in the shoot frame, px from the feet (x right, y up): tools/characters/slice_player.py.
        private static readonly Vector2 s_PlayerGunTipPx = new Vector2(45.9f, 97f);
        private const float c_MuzzleFlashLength = 0.8f;             // metres on screen
        private const float c_ShadowHeight = 0.08f;                  // above decals and the workshop's soil slab
        private const int c_ShadowSortingOrder = -1;                 // ground shadows before every character sprite
        private const int c_MuzzleFlashSortingOrder = 1;
        private const string c_SpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string c_QuadMesh = EditorPaths.c_GeneratedMeshes + "/SM_QuadXZ.asset";
        // Unlit sprites are painted fully lit: a warm tint sits them in the sunset.
        private static readonly Color s_SpriteTint = new Color(1f, 0.93f, 0.85f, 1f);

        public const string c_PlayerSprites = EditorPaths.c_DataCharacters + "/Player_Sprites.asset";

        private sealed class Look
        {
            public string Name;
            public float Size = 1f;                                  // relative to the sheet scale
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
            // Wave line-up.
            public int FromWave;
            public float Weight;
        }

        /// <summary>Numeric frame indices shared by one character's clips (characters.png, one sheet per character).</summary>
        private static string[] Idx(params int[] indices) => Array.ConvertAll(indices, i => i.ToString());

        /// <summary>Named frames of one player clip (referens/person.png, one panel per clip): "{prefix}_0".."{prefix}_{count-1}".</summary>
        private static string[] Seq(string prefix, int count)
        {
            var frames = new string[count];
            for (int i = 0; i < count; i++)
                frames[i] = $"{prefix}_{i}";
            return frames;
        }

        // Sheet columns: 0-1 idle, 2-3 walk / lunge, 4 attack, 5 hit, 6 dead (see referens/characters.png).
        private static (CharacterAnim, string[], float, bool)[] HumanoidClips(int[] walk, float walkFps) => new[]
        {
            (CharacterAnim.Idle, Idx(0, 1), 2f, true),
            (CharacterAnim.Move, Idx(walk), walkFps, true),
            (CharacterAnim.Attack, Idx(3, 4), 5f, false),
            (CharacterAnim.Hit, Idx(5), 1f, true),
            (CharacterAnim.Dead, Idx(5, 6), 5f, false),
        };

        // referens/person.png: run right (mirrored for left), idle, shooting, reload and the two turning poses used
        // when the player aims mostly up/down the screen instead of left/right (see PlayerSpriteView).
        private static readonly Look s_PlayerLook = new Look
        {
            Name = "Player",
            Clips = new[]
            {
                (CharacterAnim.Idle, Seq("Idle", 4), 4f, true),
                (CharacterAnim.Move, Seq("Move", 6), 10f, true),
                (CharacterAnim.Shoot, Seq("Shoot", 4), 12f, true),
                (CharacterAnim.Reload, Seq("Reload", 4), 4f, true),
                (CharacterAnim.TurnUp, Seq("TurnUp", 4), 3f, true),
                (CharacterAnim.TurnDown, Seq("TurnDown", 4), 3f, true),
            },
        };

        private static readonly ZombieSpec[] s_Zombies =
        {
            new ZombieSpec
            {
                Look = new Look { Name = "Walker", Clips = HumanoidClips(new[] { 2, 1 }, 3.5f) },
                Health = 30, Speed = 1.3f, Damage = 4, AttackInterval = 1.3f, FromWave = 1, Weight = 5f,
            },
            new ZombieSpec
            {
                // Hard hat: takes twice the bullets, barely flinches.
                Look = new Look { Name = "Worker", Clips = HumanoidClips(new[] { 2, 1 }, 3.2f) },
                Health = 60, Speed = 1.15f, SpeedVariation = 0.1f, Damage = 5, AttackInterval = 1.4f,
                Stagger = 0.12f, Knockback = 0.1f, FromWave = 2, Weight = 2f,
            },
            new ZombieSpec
            {
                // Runner.
                Look = new Look { Name = "RedCap", Clips = HumanoidClips(new[] { 2, 3 }, 5f) },
                Health = 25, Speed = 2.1f, Damage = 4, AttackInterval = 1.1f, AttackAnimSpeed = 1.2f, Aggro = 6f,
                Knockback = 0.25f, FromWave = 2, Weight = 2f,
            },
            new ZombieSpec
            {
                // Stalker: hunts the player instead of the car.
                Look = new Look { Name = "Hoodie", Clips = HumanoidClips(new[] { 2, 3 }, 4f) },
                Health = 40, Speed = 1.6f, Damage = 6, AttackInterval = 1.2f, Preference = ZombieTarget.Player,
                FromWave = 3, Weight = 1.5f,
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
                Preference = ZombieTarget.Player, Stagger = 0.15f, Knockback = 0.35f, FromWave = 4, Weight = 1.2f,
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
                Aggro = 3f, Stagger = 0f, Knockback = 0f, FromWave = 5, Weight = 0.5f,
            },
        };

        /// <summary>Sprite clip assets, zombie configs + prefabs and the wave line-up.</summary>
        public static void BuildAll()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_DataCharacters);
            EditorPaths.EnsureFolder(EditorPaths.c_DataZombies);
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsEnemies);
            EnsureSpriteImport();

            BuildSprites(s_PlayerLook);
            var configs = new List<ZombieConfig>();
            foreach (ZombieSpec spec in s_Zombies)
                configs.Add(BuildZombie(spec, BuildSprites(spec.Look)));
            ConfigureWaves(configs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterBuilder] Player sprites + {configs.Count} zombie types.");
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

        // ------------------------------------------------------------------ visuals shared by zombies and the player
        private static float SpriteScale(Look look) => ViewScale * look.Size;

        /// <summary>Sheet pixels -> metres at 100 px per unit.</summary>
        private static float ViewScale => ArtImportPostprocessor.c_CharacterPixelsPerUnit / c_SheetPixelsPerMetre;

        /// <summary>1 / cos(camera pitch): an upright sprite seen from above is not squashed.</summary>
        private static float PitchCompensation => 1f / Mathf.Cos(ViewConstants.c_CameraPitch * Mathf.Deg2Rad);

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
        private static ZombieConfig BuildZombie(ZombieSpec spec, CharacterSprites sprites)
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
            SetProps(config, ("m_Prefab", prefab.GetComponent<ZombieController>()));
            return config;
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

        // ------------------------------------------------------------------ player
        /// <summary>Adds the sprite view (idle / run / shoot / hurt clips, muzzle flash, blob shadow) and the health.</summary>
        public static void BuildPlayerView(GameObject root, PlayerInputProvider input, PlayerWeapon weapon)
        {
            CharacterSprites sprites = AssetDatabase.LoadAssetAtPath<CharacterSprites>(c_PlayerSprites);
            SpriteAnimator animator = BuildView(root, s_PlayerLook, sprites, true, out Transform view, out _, out _);

            // Muzzle flash at the gun tip of the shoot frame; a child of the sprite, so it follows facing and bounce.
            var flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(animator.transform, false);
            flashGo.transform.localPosition = s_PlayerGunTipPx / ArtImportPostprocessor.c_CharacterPixelsPerUnit;
            var flash = flashGo.AddComponent<SpriteRenderer>();
            flash.sprite = Frame("Player", "MuzzleFlash");
            flash.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(c_SpriteMaterial);
            flash.sortingOrder = c_MuzzleFlashSortingOrder;
            flash.shadowCastingMode = ShadowCastingMode.Off;
            flash.enabled = false;                                      // shown for a moment on every shot
            float flashLength = flash.sprite.bounds.max.x;             // the flash fills the right half
            float k = c_MuzzleFlashLength / (flashLength * SpriteScale(s_PlayerLook));
            flashGo.transform.localScale = new Vector3(k, k / PitchCompensation, 1f);   // undo the sprite's Y stretch

            var obstacle = root.AddComponent<NavMeshObstacle>();   // zombie crowd avoidance steers around the player
            obstacle.shape = NavMeshObstacleShape.Capsule;
            var cc = root.GetComponent<CharacterController>();
            obstacle.center = cc.center;
            obstacle.radius = cc.radius;
            obstacle.height = cc.height;
            obstacle.carving = false;

            var playerView = view.gameObject.AddComponent<PlayerSpriteView>();
            SetProps(playerView, ("m_Animator", animator), ("m_MuzzleFlash", flash), ("m_Controller", cc),
                ("m_InputProvider", input), ("m_Weapon", weapon));
            var health = root.AddComponent<PlayerHealth>();
            SetProps(health, ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)), ("m_View", playerView));
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
