using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// One-shot project setup: serialization (Force Text), player/WebGL settings, physics layers, three URP assets
    /// (Low/Medium/High) sharing one Forward renderer, quality levels and shader stripping.
    /// </summary>
    public static class ProjectConfigurator
    {
        private const string c_TemplateRpAsset = EditorPaths.c_Settings + "/Mobile_RPAsset.asset";
        private const string c_TemplateRenderer = EditorPaths.c_Settings + "/Mobile_Renderer.asset";
        private const string c_Renderer = EditorPaths.c_Settings + "/URP_Renderer.asset";
        private static readonly string[] s_ObsoleteTemplateAssets =
        {
            EditorPaths.c_Settings + "/PC_RPAsset.asset", EditorPaths.c_Settings + "/PC_Renderer.asset",
            EditorPaths.c_Settings + "/Mobile_RPAsset.asset", EditorPaths.c_Settings + "/Mobile_Renderer.asset",
        };

        private sealed class TierSpec
        {
            public string Name;
            public bool Hdr;
            public int Msaa;
            public float RenderScale;
            public float ShadowDistance;
            public int ShadowResolution;
            public int Cascades;
            public bool SoftShadows;
            public int SoftShadowQuality;
            public int AdditionalLightsMode;   // 1 = per pixel, 2 = per vertex
            public int AdditionalLightsLimit;
            public int TextureMipLimit;        // 0 = full size (2048), 1 = half (1024)
            public int AnisotropicMode;        // 0 = off, 1 = per texture
        }

        private static readonly TierSpec[] s_Tiers =
        {
            new TierSpec { Name = "Low", Hdr = false, Msaa = 1, RenderScale = 0.8f, ShadowDistance = 35f, ShadowResolution = 1024,
                Cascades = 1, SoftShadows = false, SoftShadowQuality = 1, AdditionalLightsMode = 2, AdditionalLightsLimit = 2,
                TextureMipLimit = 1, AnisotropicMode = 0 },
            new TierSpec { Name = "Medium", Hdr = true, Msaa = 1, RenderScale = 1f, ShadowDistance = 50f, ShadowResolution = 2048,
                Cascades = 1, SoftShadows = true, SoftShadowQuality = 1, AdditionalLightsMode = 1, AdditionalLightsLimit = 3,
                TextureMipLimit = 0, AnisotropicMode = 1 },
            new TierSpec { Name = "High", Hdr = true, Msaa = 2, RenderScale = 1f, ShadowDistance = 60f, ShadowResolution = 2048,
                Cascades = 2, SoftShadows = true, SoftShadowQuality = 2, AdditionalLightsMode = 1, AdditionalLightsLimit = 4,
                TextureMipLimit = 0, AnisotropicMode = 1 },
        };

        [MenuItem("Tools/Setup/Configure Project")]
        public static void Run()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            PlayerSettings.companyName = "gekoqop";
            PlayerSettings.productName = "Zombies Car";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;   // the game pauses when the browser tab is hidden

            ConfigureLayers();
            ConfigurePhysicsMatrix();
            UniversalRenderPipelineAsset[] tiers = ConfigureRenderPipelines();
            ConfigureQuality(tiers);
            ConfigureGraphics(tiers[(int)QualityTier.Medium]);
            WebOptimizer.Optimize();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectConfigurator] Project configured.");
        }

        private static void ConfigureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(Layers.c_PlayerIndex).stringValue = Layers.c_Player;
            layers.GetArrayElementAtIndex(Layers.c_ObstacleIndex).stringValue = Layers.c_Obstacle;
            layers.GetArrayElementAtIndex(Layers.c_GroundIndex).stringValue = Layers.c_Ground;
            layers.GetArrayElementAtIndex(Layers.c_ProjectileIndex).stringValue = Layers.c_Projectile;
            layers.GetArrayElementAtIndex(Layers.c_CameraBoundsIndex).stringValue = Layers.c_CameraBounds;
            layers.GetArrayElementAtIndex(Layers.c_EnemyIndex).stringValue = Layers.c_Enemy;
            layers.GetArrayElementAtIndex(Layers.c_CarObstacleIndex).stringValue = Layers.c_CarObstacle;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePhysicsMatrix()
        {
            // Projectiles use casts only and the camera volume must never block anything. Zombies are kinematic
            // NavMesh agents (crowd avoidance keeps them apart) but still block the player's CharacterController.
            const int layerCount = 32;
            for (int i = 0; i < layerCount; i++)
            {
                Physics.IgnoreLayerCollision(Layers.c_CameraBoundsIndex, i, true);
                Physics.IgnoreLayerCollision(Layers.c_ProjectileIndex, i, true);
            }
            Physics.IgnoreLayerCollision(Layers.c_EnemyIndex, Layers.c_EnemyIndex, true);
        }

        private static UniversalRenderPipelineAsset[] ConfigureRenderPipelines()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(c_Renderer) == null)
                AssetDatabase.CopyAsset(c_TemplateRenderer, c_Renderer);
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(c_Renderer);
            var rendererSo = new SerializedObject(renderer);
            rendererSo.FindProperty("m_RenderingMode").intValue = 0;   // Forward: cheapest path for WebGL / mobile
            rendererSo.ApplyModifiedPropertiesWithoutUndo();

            var result = new UniversalRenderPipelineAsset[s_Tiers.Length];
            for (int i = 0; i < s_Tiers.Length; i++)
            {
                TierSpec t = s_Tiers[i];
                string path = $"{EditorPaths.c_Settings}/URP_{t.Name}.asset";
                if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path) == null)
                    AssetDatabase.CopyAsset(c_TemplateRpAsset, path);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var so = new SerializedObject(asset);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
                so.FindProperty("m_SupportsHDR").boolValue = t.Hdr;
                so.FindProperty("m_MSAA").intValue = t.Msaa;
                so.FindProperty("m_RenderScale").floatValue = t.RenderScale;
                so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                so.FindProperty("m_MainLightShadowmapResolution").intValue = t.ShadowResolution;
                so.FindProperty("m_ShadowDistance").floatValue = t.ShadowDistance;
                so.FindProperty("m_ShadowCascadeCount").intValue = t.Cascades;
                so.FindProperty("m_SoftShadowsSupported").boolValue = t.SoftShadows;
                so.FindProperty("m_SoftShadowQuality").intValue = t.SoftShadowQuality;
                so.FindProperty("m_AdditionalLightsRenderingMode").intValue = t.AdditionalLightsMode;
                so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = t.AdditionalLightsLimit;
                so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
                so.FindProperty("m_UseSRPBatcher").boolValue = true;
                so.FindProperty("m_SupportsDynamicBatching").boolValue = false;
                so.FindProperty("m_MixedLightingSupported").boolValue = false;   // no baked lighting -> fewer variants
                so.FindProperty("m_SupportsLightCookies").boolValue = false;
                so.FindProperty("m_SupportsLightLayers").boolValue = false;
                so.FindProperty("m_UseAdaptivePerformance").boolValue = false;
                so.FindProperty("m_RequireDepthTexture").boolValue = false;
                so.FindProperty("m_RequireOpaqueTexture").boolValue = false;
                so.FindProperty("m_EnableLODCrossFade").boolValue = false;
                so.FindProperty("m_SupportsTerrainHoles").boolValue = false;
                so.FindProperty("m_ReflectionProbeBlending").boolValue = false;
                so.FindProperty("m_ReflectionProbeBoxProjection").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                result[i] = asset;
            }
            return result;
        }

        private static void ConfigureQuality(UniversalRenderPipelineAsset[] tiers)
        {
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            SerializedProperty levels = qs.FindProperty("m_QualitySettings");
            while (levels.arraySize < s_Tiers.Length)
                levels.InsertArrayElementAtIndex(levels.arraySize - 1);
            levels.arraySize = s_Tiers.Length;

            for (int i = 0; i < s_Tiers.Length; i++)
            {
                TierSpec t = s_Tiers[i];
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = t.Name;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = tiers[i];
                level.FindPropertyRelative("globalTextureMipmapLimit").intValue = t.TextureMipLimit;
                level.FindPropertyRelative("anisotropicTextures").intValue = t.AnisotropicMode;
                level.FindPropertyRelative("antiAliasing").intValue = 0;   // MSAA comes from the URP asset
                level.FindPropertyRelative("vSyncCount").intValue = 0;     // the browser paces frames
                level.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
                level.FindPropertyRelative("softParticles").boolValue = false;
                level.FindPropertyRelative("lodBias").floatValue = 1f;
                level.FindPropertyRelative("excludedTargetPlatforms").arraySize = 0;
            }
            qs.FindProperty("m_CurrentQuality").intValue = (int)QualityTier.Medium;

            SerializedProperty perPlatform = qs.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty pair = perPlatform.GetArrayElementAtIndex(i);
                string platform = pair.FindPropertyRelative("first").stringValue;
                int value = (int)QualityTier.Medium;
                if (platform == "Android" || platform == "iPhone")
                    value = (int)QualityTier.Low;
                else if (platform == "Standalone")
                    value = (int)QualityTier.High;
                pair.FindPropertyRelative("second").intValue = value;
            }
            qs.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGraphics(UniversalRenderPipelineAsset defaultAsset)
        {
            GraphicsSettings.defaultRenderPipeline = defaultAsset;
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            // Only linear fog is used by the maps: strip the other fog variants.
            gs.FindProperty("m_FogStripping").intValue = 1;
            gs.FindProperty("m_FogKeepLinear").boolValue = true;
            gs.FindProperty("m_FogKeepExp").boolValue = false;
            gs.FindProperty("m_FogKeepExp2").boolValue = false;
            gs.FindProperty("m_InstancingStripping").intValue = 0;   // strip unused instancing variants
            gs.ApplyModifiedPropertiesWithoutUndo();

            foreach (string path in s_ObsoleteTemplateAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
