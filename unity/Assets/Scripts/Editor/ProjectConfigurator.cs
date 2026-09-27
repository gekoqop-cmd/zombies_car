using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// One-shot project setup: serialization (Force Text), player/WebGL settings, physics layers, four URP assets
    /// (Low/Medium/High sharing one Forward renderer, Ultra with its own renderer that adds SSAO), quality levels
    /// and shader stripping.
    /// </summary>
    public static class ProjectConfigurator
    {
        private const string c_TemplateRpAsset = EditorPaths.c_Settings + "/Mobile_RPAsset.asset";
        private const string c_TemplateRenderer = EditorPaths.c_Settings + "/Mobile_Renderer.asset";
        private const string c_Renderer = EditorPaths.c_Settings + "/URP_Renderer.asset";
        private const string c_UltraRenderer = EditorPaths.c_Settings + "/URP_Renderer_Ultra.asset";
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
            public int AnisotropicMode;        // 0 = off, 1 = per texture, 2 = forced on every texture
            // Ultra-only extras; the defaults keep Low / Medium / High exactly as they were.
            public string Renderer = c_Renderer;
            public bool AdditionalLightShadows;
            public int AdditionalShadowAtlas = 2048;
            public bool RealtimeReflectionProbes;
            public bool DepthTexture;
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
            // Powerful PCs only (selected by hand): supersampling, MSAA 4x, 4 x 4096 shadow cascades with high soft
            // shadows, 8 per-pixel lights with shadows, SSAO (own renderer), depth texture for bokeh depth of field,
            // realtime reflection probes, forced 16x anisotropy (see SettingsService.ApplyQuality).
            new TierSpec { Name = "Ultra", Hdr = true, Msaa = 4, RenderScale = 1.25f, ShadowDistance = 90f, ShadowResolution = 4096,
                Cascades = 4, SoftShadows = true, SoftShadowQuality = 3, AdditionalLightsMode = 1, AdditionalLightsLimit = 8,
                TextureMipLimit = 0, AnisotropicMode = 2, Renderer = c_UltraRenderer, AdditionalLightShadows = true,
                AdditionalShadowAtlas = 4096, RealtimeReflectionProbes = true, DepthTexture = true },
        };

        // SSAO of the Ultra renderer (scene scale: camera ~32 m away, objects 0.5..5 m).
        private const float c_SsaoIntensity = 1.4f;
        private const float c_SsaoRadius = 0.6f;
        private const float c_SsaoDirectLighting = 0.3f;
        private const float c_SsaoFalloff = 120f;

        [MenuItem("Tools/Setup/Configure Project")]
        public static void Run()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            PlayerSettings.companyName = "gekoqop";
            PlayerSettings.productName = "Rustwave";
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
            var sharedRenderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(c_Renderer);
            var rendererSo = new SerializedObject(sharedRenderer);
            rendererSo.FindProperty("m_RenderingMode").intValue = 0;   // Forward: cheapest path for WebGL / mobile
            rendererSo.ApplyModifiedPropertiesWithoutUndo();
            ScriptableRendererData ultraRenderer = EnsureUltraRenderer();

            var result = new UniversalRenderPipelineAsset[s_Tiers.Length];
            for (int i = 0; i < s_Tiers.Length; i++)
            {
                TierSpec t = s_Tiers[i];
                string path = $"{EditorPaths.c_Settings}/URP_{t.Name}.asset";
                if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path) == null)
                {
                    // The URP template is deleted after the first setup: a new tier starts from the previous one
                    // (every setting below is written anyway).
                    string source = AssetDatabase.LoadMainAssetAtPath(c_TemplateRpAsset) != null
                        ? c_TemplateRpAsset
                        : $"{EditorPaths.c_Settings}/URP_{s_Tiers[i - 1].Name}.asset";
                    AssetDatabase.CopyAsset(source, path);
                }
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var so = new SerializedObject(asset);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = t.Renderer == c_UltraRenderer ? ultraRenderer : sharedRenderer;
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
                so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = t.AdditionalLightShadows;
                so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = t.AdditionalShadowAtlas;
                so.FindProperty("m_UseSRPBatcher").boolValue = true;
                so.FindProperty("m_SupportsDynamicBatching").boolValue = false;
                so.FindProperty("m_MixedLightingSupported").boolValue = false;   // no baked lighting -> fewer variants
                so.FindProperty("m_SupportsLightCookies").boolValue = false;
                so.FindProperty("m_SupportsLightLayers").boolValue = false;
                so.FindProperty("m_UseAdaptivePerformance").boolValue = false;
                so.FindProperty("m_RequireDepthTexture").boolValue = t.DepthTexture;
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

        /// <summary>Copy of the shared Forward renderer plus a high-quality SSAO renderer feature (Ultra only).</summary>
        private static ScriptableRendererData EnsureUltraRenderer()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(c_UltraRenderer) == null)
                AssetDatabase.CopyAsset(c_Renderer, c_UltraRenderer);
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(c_UltraRenderer);
            var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (ssao == null)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
                var rso = new SerializedObject(renderer);
                SerializedProperty features = rso.FindProperty("m_RendererFeatures");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                SerializedProperty map = rso.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Enum values from ScreenSpaceAmbientOcclusionSettings (internal): DepthNormals = 1, NormalQuality.High = 2,
            // AOSampleOption.High = 0 (12 samples), BlurQualityOptions.High = 0 (bilateral).
            var so = new SerializedObject(ssao);
            so.FindProperty("m_Settings.Downsample").boolValue = false;
            so.FindProperty("m_Settings.AfterOpaque").boolValue = false;
            so.FindProperty("m_Settings.Source").intValue = 1;
            so.FindProperty("m_Settings.NormalSamples").intValue = 2;
            so.FindProperty("m_Settings.Samples").intValue = 0;
            so.FindProperty("m_Settings.BlurQuality").intValue = 0;
            so.FindProperty("m_Settings.Intensity").floatValue = c_SsaoIntensity;
            so.FindProperty("m_Settings.Radius").floatValue = c_SsaoRadius;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = c_SsaoDirectLighting;
            so.FindProperty("m_Settings.Falloff").floatValue = c_SsaoFalloff;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(renderer);
            return renderer;
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
                level.FindPropertyRelative("realtimeReflectionProbes").boolValue = t.RealtimeReflectionProbes;
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
