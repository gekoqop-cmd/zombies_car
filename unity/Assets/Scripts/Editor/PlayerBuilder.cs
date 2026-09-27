using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombiCar.Core;
using ZombiCar.Player;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The player's 2D look from the frames cut out of refsanim/Player/*.mp4 by tools/characters/player_mp4
    /// (Art/Textures/Characters/Player + Data/Characters/Player_Frames.json): one crunched sprite atlas per clip,
    /// AnimationClips (sprite + barrel position per frame, footstep events), the Animator Controller
    /// (Idle / Run by speed, Shot, Reload) and the view hierarchy of the player prefab (billboard, body, muzzle,
    /// muzzle flash, blob shadow). Called by <see cref="PrefabBuilder"/>.
    /// </summary>
    public static class PlayerBuilder
    {
        private const string c_Frames = EditorPaths.c_CharacterTextures + "/Player";
        private const string c_Atlases = c_Frames + "/Atlases";
        private const string c_Meta = EditorPaths.c_DataCharacters + "/Player_Frames.json";
        private const string c_Animations = "Assets/Art/Animations/Player";
        private const string c_Controller = c_Animations + "/Player.controller";
        private const string c_Sfx = "Assets/Audio/SFX";

        // Apparent height of the standing character (the sprite is stretched by 1 / cos(pitch) so it is not squashed).
        private const float c_StandingHeight = 1.75f;
        private const float c_RunThreshold = 0.3f;
        private const int c_BodySortingOrder = 0;
        private const int c_FlashSortingOrder = 1;
        private static readonly Vector2 s_Shadow = new Vector2(1.3f, 0.6f);   // blob width / depth, metres
        private const int c_AtlasMaxSize = 2048;
        private const int c_CrunchQuality = 60;

        [Serializable]
        private sealed class Point
        {
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class ClipMeta
        {
            public string name;
            public string[] frames;
            public float fps;
            public bool loop;
            public Point[] gunTips;
            public int[] stepFrames;
            public float strideMetres;
        }

        [Serializable]
        private sealed class FramesMeta
        {
            public int standingHeightPx;
            public ClipMeta[] clips;
        }

        private static FramesMeta LoadMeta()
        {
            if (!File.Exists(c_Meta))
                throw new InvalidOperationException($"[PlayerBuilder] {c_Meta} missing: run tools/characters/player_mp4/process.py");
            return JsonUtility.FromJson<FramesMeta>(File.ReadAllText(c_Meta));
        }

        private static Sprite Frame(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{c_Frames}/{name}.png");
            if (sprite == null)
                throw new InvalidOperationException($"[PlayerBuilder] No sprite {name} in {c_Frames}");
            return sprite;
        }

        /// <summary>Body scale: 1.75 m apparent height; y also carries the camera-pitch compensation.</summary>
        private static Vector2 BodyScale(FramesMeta meta)
        {
            float s = c_StandingHeight / (meta.standingHeightPx / ArtImportPostprocessor.c_CharacterPixelsPerUnit);
            return new Vector2(s, s * CharacterBuilder.PitchCompensation);
        }

        // ------------------------------------------------------------------ assets
        [MenuItem("Tools/Build Player Animations")]
        public static void BuildAssets()
        {
            FramesMeta meta = LoadMeta();
            EditorPaths.EnsureFolder(c_Animations);
            EditorPaths.EnsureFolder(c_Atlases);
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;   // atlases are packed for the player build

            var clips = new Dictionary<string, AnimationClip>();
            foreach (ClipMeta clip in meta.clips)
            {
                Sprite[] sprites = clip.frames.Select(Frame).ToArray();
                BuildAtlas(clip.name, sprites);
                clips[clip.name] = BuildClip(clip, sprites);
            }
            BuildAtlas("MuzzleFlash", new[] { Frame("Player_MuzzleFlash") });
            BuildController(clips);

            // Barrel height (world) for aiming = average barrel height of the clips the player shoots from.
            float barrelPx = meta.clips.Where(c => c.name != "Reload").SelectMany(c => c.gunTips).Average(t => t.y);
            float barrelHeight = barrelPx / ArtImportPostprocessor.c_CharacterPixelsPerUnit * BodyScale(meta).y;
            var weapon = DataAssetsBuilder.Load<WeaponConfig>(DataAssetsBuilder.c_WeaponConfig);
            CharacterBuilder.SetProps(weapon, ("m_MuzzleHeight", barrelHeight));
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerBuilder] {meta.clips.Length} clips ({string.Join(", ", meta.clips.Select(c => $"{c.name} {c.frames.Length}f @{c.fps:F1}"))}), " +
                      $"barrel height {barrelHeight:F2} m.");
        }

        private static void BuildAtlas(string name, Sprite[] sprites)
        {
            string path = $"{c_Atlases}/Player_{name}.spriteatlasv2";
            var atlas = new SpriteAtlasAsset();
            atlas.Add(sprites.Cast<Object>().ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter importer)
            {
                SpriteAtlasPackingSettings packing = importer.packingSettings;
                packing.enableRotation = false;
                packing.enableTightPacking = true;
                packing.padding = 4;
                importer.packingSettings = packing;
                SpriteAtlasTextureSettings texture = importer.textureSettings;
                texture.generateMipMaps = true;
                texture.sRGB = true;
                texture.filterMode = FilterMode.Bilinear;
                importer.textureSettings = texture;
                // Crunched DXT: a fraction of the download of plain DXT5 (Yandex build size).
                foreach (string platform in new[] { "DefaultTexturePlatform", "WebGL" })
                {
                    TextureImporterPlatformSettings settings = importer.GetPlatformSettings(platform);
                    settings.overridden = platform != "DefaultTexturePlatform";
                    settings.maxTextureSize = c_AtlasMaxSize;
                    settings.textureCompression = TextureImporterCompression.Compressed;
                    settings.crunchedCompression = true;
                    settings.compressionQuality = c_CrunchQuality;
                    importer.SetPlatformSettings(settings);
                }
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip BuildClip(ClipMeta meta, Sprite[] sprites)
        {
            string path = $"{c_Animations}/Player_{meta.name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = meta.fps;

            // One sprite key per frame; the last frame is held for a whole frame time (a trailing duplicate key).
            float dt = 1f / meta.fps;
            var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i * dt, value = sprites[i] };
            keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length * dt, value = sprites[sprites.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);

            // Barrel position per frame (px from the feet -> sprite units), stepped.
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            for (int i = 0; i <= sprites.Length; i++)
            {
                Point tip = meta.gunTips[Mathf.Min(i, sprites.Length - 1)];
                x.AddKey(new Keyframe(i * dt, tip.x / ArtImportPostprocessor.c_CharacterPixelsPerUnit, float.PositiveInfinity, float.PositiveInfinity));
                y.AddKey(new Keyframe(i * dt, tip.y / ArtImportPostprocessor.c_CharacterPixelsPerUnit, float.PositiveInfinity, float.PositiveInfinity));
            }
            clip.SetCurve("Muzzle", typeof(Transform), "m_LocalPosition.x", x);
            clip.SetCurve("Muzzle", typeof(Transform), "m_LocalPosition.y", y);
            clip.SetCurve("Muzzle", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0f, sprites.Length * dt, 0f));

            AnimationUtility.SetAnimationEvents(clip, (meta.stepFrames ?? Array.Empty<int>())
                .Select(f => new AnimationEvent { time = f * dt, functionName = nameof(PlayerAnimationEvents.OnFootstep) }).ToArray());

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = meta.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(c_Controller) != null)
                AssetDatabase.DeleteAsset(c_Controller);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(c_Controller);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("RunSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ShotSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Reloading", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ReloadSpeed", AnimatorControllerParameterType.Float);
            SetDefault(controller, "RunSpeed", 1f);
            SetDefault(controller, "ShotSpeed", 1f);
            SetDefault(controller, "ReloadSpeed", 1f);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = State(sm, "Idle", clips["Idle"], null, new Vector3(300f, 0f));
            AnimatorState run = State(sm, "Run", clips["Run"], "RunSpeed", new Vector3(300f, 150f));
            AnimatorState shot = State(sm, "Shot", clips["Shot"], "ShotSpeed", new Vector3(560f, 0f));
            AnimatorState reload = State(sm, "Reload", clips["Reload"], "ReloadSpeed", new Vector3(560f, 150f));
            sm.defaultState = idle;

            // Sprite frames never blend: every transition is instant.
            Link(idle, run).AddCondition(AnimatorConditionMode.Greater, c_RunThreshold, "Speed");
            Link(run, idle).AddCondition(AnimatorConditionMode.Less, c_RunThreshold, "Speed");
            Link(idle, reload).AddCondition(AnimatorConditionMode.If, 0f, "Reloading");
            AnimatorStateTransition runToReload = Link(run, reload);
            runToReload.AddCondition(AnimatorConditionMode.If, 0f, "Reloading");
            runToReload.AddCondition(AnimatorConditionMode.Less, c_RunThreshold, "Speed");
            Link(reload, idle).AddCondition(AnimatorConditionMode.IfNot, 0f, "Reloading");
            Link(reload, run).AddCondition(AnimatorConditionMode.Greater, c_RunThreshold, "Speed");   // move while reloading
            Link(idle, shot).AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            Link(shot, shot).AddCondition(AnimatorConditionMode.If, 0f, "Shoot");                      // next shot restarts it
            Link(shot, run).AddCondition(AnimatorConditionMode.Greater, c_RunThreshold, "Speed");
            Link(shot, reload).AddCondition(AnimatorConditionMode.If, 0f, "Reloading");
            AnimatorStateTransition shotEnd = Link(shot, idle);
            shotEnd.hasExitTime = true;                                                                  // the whole clip
            shotEnd.exitTime = 1f;
            EditorUtility.SetDirty(controller);
        }

        private static void SetDefault(AnimatorController controller, string name, float value)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            foreach (AnimatorControllerParameter p in parameters)
            {
                if (p.name == name)
                    p.defaultFloat = value;
            }
            controller.parameters = parameters;
        }

        private static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip, string speedParameter, Vector3 position)
        {
            AnimatorState state = sm.AddState(name, position);
            state.motion = clip;
            state.writeDefaultValues = false;
            if (speedParameter != null)
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParameter;
            }
            return state;
        }

        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = from == to;
            return t;
        }

        // ------------------------------------------------------------------ prefab view
        /// <summary>
        /// Adds the view (billboard + body sprite with the Animator, animated barrel, muzzle flash, blob shadow),
        /// PlayerView, PlayerHealth and the crowd obstacle to the player root, and wires the weapon's muzzle.
        /// </summary>
        public static void BuildView(GameObject root, PlayerInputProvider input, PlayerWeapon weapon, DesktopPlayerInput desktop)
        {
            FramesMeta meta = LoadMeta();
            Vector2 scale = BodyScale(meta);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterBuilder.c_SpriteMaterial);
            ClipMeta idleMeta = meta.clips.First(c => c.name == "Idle");

            var view = new GameObject("View").transform;
            view.SetParent(root.transform, false);
            CharacterBuilder.SetProps(view.gameObject.AddComponent<YAxisBillboard>(), ("m_UpdateEveryFrame", true));

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(view, false);
            bodyGo.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = Frame(idleMeta.frames[0]);
            body.sharedMaterial = spriteMaterial;
            body.color = CharacterBuilder.s_SpriteTint;
            body.sortingOrder = c_BodySortingOrder;
            body.shadowCastingMode = ShadowCastingMode.Off;
            body.receiveShadows = false;
            var animator = bodyGo.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(c_Controller);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Barrel end: moved per frame by the clips; bullets leave from here (mirrors with the body).
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(bodyGo.transform, false);
            muzzle.localPosition = new Vector3(idleMeta.gunTips[0].x, idleMeta.gunTips[0].y, 0f) / ArtImportPostprocessor.c_CharacterPixelsPerUnit;
            var flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(muzzle, false);
            var flash = flashGo.AddComponent<SpriteRenderer>();
            flash.sprite = Frame("Player_MuzzleFlash");
            flash.sharedMaterial = spriteMaterial;
            flash.sortingOrder = c_FlashSortingOrder;
            flash.shadowCastingMode = ShadowCastingMode.Off;
            flash.enabled = false;

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(view, false);
            shadowGo.transform.localPosition = new Vector3(0f, CharacterBuilder.c_ShadowHeight, 0f);
            shadowGo.transform.localScale = new Vector3(s_Shadow.x, 1f, s_Shadow.y);
            shadowGo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CharacterBuilder.c_QuadMesh);
            var shadow = shadowGo.AddComponent<MeshRenderer>();
            shadow.sharedMaterial = MaterialLibrary.Get("M_ShadowBlob");
            shadow.shadowCastingMode = ShadowCastingMode.Off;
            shadow.receiveShadows = false;
            shadow.sortingOrder = CharacterBuilder.c_ShadowSortingOrder;

            var cc = root.GetComponent<CharacterController>();
            var obstacle = root.AddComponent<NavMeshObstacle>();       // zombie crowd avoidance steers around the player
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = cc.center;
            obstacle.radius = cc.radius;
            obstacle.height = cc.height;
            obstacle.carving = false;

            ClipMeta shotMeta = meta.clips.First(c => c.name == "Shot");
            ClipMeta reloadMeta = meta.clips.First(c => c.name == "Reload");
            var playerView = view.gameObject.AddComponent<PlayerView>();
            PrefabBuilder.Wire(playerView, ("m_Animator", animator), ("m_Body", body), ("m_MuzzleFlash", flash), ("m_Controller", cc),
                ("m_InputProvider", input), ("m_Weapon", weapon),
                ("m_Config", DataAssetsBuilder.Load<PlayerConfig>(DataAssetsBuilder.c_PlayerConfig)),
                ("m_ShotSound", Sound("Player_Shot")), ("m_ReloadSound", Sound("Player_Reload")));
            CharacterBuilder.SetProps(playerView, ("m_Scale", scale), ("m_Tint", CharacterBuilder.s_SpriteTint),
                ("m_ShotClipLength", shotMeta.frames.Length / shotMeta.fps), ("m_ReloadClipLength", reloadMeta.frames.Length / reloadMeta.fps),
                ("m_StepSounds", new Object[] { Sound("Player_Step1"), Sound("Player_Step2"), Sound("Player_Step3") }));
            PrefabBuilder.Wire(bodyGo.AddComponent<PlayerAnimationEvents>(), ("m_View", playerView));

            WeaponConfig config = DataAssetsBuilder.Load<WeaponConfig>(DataAssetsBuilder.c_WeaponConfig);
            PrefabBuilder.Wire(weapon, ("m_Muzzle", muzzle));
            CharacterBuilder.SetProps(desktop, ("m_AimPlaneHeight", config.MuzzleHeight));
            var health = root.AddComponent<PlayerHealth>();
            PrefabBuilder.Wire(health, ("m_Stats", DataAssetsBuilder.Load<PlayerStats>(DataAssetsBuilder.c_PlayerStats)), ("m_View", playerView));
        }

        public static AudioClip Sound(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{c_Sfx}/{name}.ogg");
            if (clip == null)
                Debug.LogError($"[PlayerBuilder] Sound {name} missing in {c_Sfx} (tools/characters/player_mp4/audio.py)");
            return clip;
        }
    }
}
