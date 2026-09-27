using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombiCar.Core;
using ZombiCar.Player;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Everything the Ultra graphics tier adds on top of High, built so that Low / Medium / High render exactly as
    /// before: Ultra versions of the key materials (URP Lit: normal map, smoothness, reflections, stronger emission)
    /// swapped in by <see cref="UltraMaterialSwap"/>, and Map01 content that only a <see cref="QualityTierGate"/>
    /// switches on — five more lamp lights (eight in total), 1.5x denser grass and bushes, a reflection probe,
    /// ambient dust — plus shot / impact sparks and a high-priority Ultra post-processing volume.
    /// The URP asset, SSAO renderer and quality level are set up by <see cref="ProjectConfigurator"/>.
    /// </summary>
    public static class UltraQualityBuilder
    {
        private const string c_LitShader = "Universal Render Pipeline/Lit";
        private const string c_ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        private const string c_MaterialFolder = EditorPaths.c_Materials + "/Ultra";
        public const string c_MaterialSet = EditorPaths.c_Data + "/UltraMaterialSet.asset";
        private const string c_VolumeProfile = EditorPaths.c_Settings + "/Map01_UltraVolumeProfile.asset";
        private const string c_ReflectionCubemap = EditorPaths.c_Settings + "/Map01_UltraReflection.exr";
        private const string c_GlowTexture = EditorPaths.c_Vfx + "/T_BulletGlow.png";

        // ------------------------------------------------------------------ materials
        private sealed class UltraSpec
        {
            public string Regular;
            public float Metallic;
            public float Smoothness;
            public string Normal;                // texture under Assets/Art/Textures
            public float NormalScale = 1f;
            public string MetallicSmoothness;    // metallic in R, smoothness in A
            public float EmissionBoost = 1f;     // x the regular emission (brighter bloom halo)
            // Lit + a strong normal map under the low evening sun reads darker than the flat Simple Lit version:
            // the base colour is raised so the tier keeps the same overall brightness.
            public float BrightnessCompensation = 1f;
        }

        private static readonly UltraSpec[] s_Materials =
        {
            new UltraSpec { Regular = "M_Asphalt", Smoothness = 1f, Normal = "Environment/T_Asphalt_N.png", NormalScale = 1f,
                MetallicSmoothness = "Environment/T_Asphalt_MS.png", BrightnessCompensation = 1.25f },   // wet puddles on the stains
            new UltraSpec { Regular = "M_Car_Intact", Metallic = 0.25f, Smoothness = 0.5f },
            new UltraSpec { Regular = "M_Car_Damaged", Metallic = 0.25f, Smoothness = 0.45f },
            new UltraSpec { Regular = "M_Car_Wrecked", Metallic = 0.3f, Smoothness = 0.35f },
            new UltraSpec { Regular = "M_Wreck_Paint_Pickup", Metallic = 0.2f, Smoothness = 0.35f },
            new UltraSpec { Regular = "M_Wreck_Paint_Sedan", Metallic = 0.2f, Smoothness = 0.35f },
            new UltraSpec { Regular = "M_Wreck_Parts", Metallic = 0.5f, Smoothness = 0.4f },
            new UltraSpec { Regular = "M_MetalDark", Metallic = 0.6f, Smoothness = 0.45f },
            new UltraSpec { Regular = "M_MetalLight", Metallic = 0.7f, Smoothness = 0.5f },
            new UltraSpec { Regular = "M_MetalRed", Metallic = 0.4f, Smoothness = 0.45f },
            new UltraSpec { Regular = "M_Barrel", Metallic = 0.45f, Smoothness = 0.4f },
            new UltraSpec { Regular = "M_LampGlow", Smoothness = 0.6f, EmissionBoost = 2f },
            new UltraSpec { Regular = "M_Signs_Emissive", Smoothness = 0.3f, EmissionBoost = 1.7f },
            new UltraSpec { Regular = "M_Car_Emissive", Smoothness = 0.5f, EmissionBoost = 1.5f },
        };

        [MenuItem("Tools/Setup/Build Ultra Materials")]
        public static void BuildMaterials()
        {
            EditorPaths.EnsureFolder(c_MaterialFolder);
            Shader lit = Shader.Find(c_LitShader);
            var pairs = new List<UltraMaterialSet.Pair>();
            foreach (UltraSpec spec in s_Materials)
            {
                Material regular = MaterialLibrary.Get(spec.Regular);
                if (regular == null)
                    continue;
                string path = $"{c_MaterialFolder}/{spec.Regular}_Ultra.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = lit;
                mat.SetTexture("_BaseMap", regular.GetTexture("_BaseMap"));
                mat.SetTextureScale("_BaseMap", regular.GetTextureScale("_BaseMap"));
                Color baseColor = regular.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", new Color(baseColor.r * spec.BrightnessCompensation, baseColor.g * spec.BrightnessCompensation,
                    baseColor.b * spec.BrightnessCompensation, baseColor.a));
                mat.SetFloat("_Surface", regular.GetFloat("_Surface"));
                mat.SetFloat("_AlphaClip", regular.GetFloat("_AlphaClip"));
                mat.SetFloat("_Cutoff", regular.GetFloat("_Cutoff"));
                mat.SetFloat("_Cull", regular.GetFloat("_Cull"));
                mat.SetFloat("_BlendModePreserveSpecular", 0f);
                mat.SetFloat("_WorkflowMode", 1f);                              // metallic
                mat.SetFloat("_Metallic", spec.Metallic);
                mat.SetFloat("_Smoothness", spec.Smoothness);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);                  // metallic map alpha
                mat.SetTexture("_MetallicGlossMap", LoadTexture(spec.MetallicSmoothness));
                mat.SetTexture("_BumpMap", LoadTexture(spec.Normal));
                mat.SetFloat("_BumpScale", spec.NormalScale);
                mat.SetFloat("_SpecularHighlights", 1f);
                mat.SetFloat("_EnvironmentReflections", 1f);
                mat.SetFloat("_ReceiveShadows", 1f);
                Color emission = regular.GetColor("_EmissionColor") * spec.EmissionBoost;
                mat.SetTexture("_EmissionMap", regular.GetTexture("_EmissionMap"));
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = emission.maxColorComponent > 0f
                    ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                    : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.enableInstancing = true;
                BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);
                EditorUtility.SetDirty(mat);
                pairs.Add(new UltraMaterialSet.Pair { Regular = regular, Ultra = mat });
            }

            var set = AssetDatabase.LoadAssetAtPath<UltraMaterialSet>(c_MaterialSet);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UltraMaterialSet>();
                AssetDatabase.CreateAsset(set, c_MaterialSet);
            }
            set.EditorSetPairs(pairs.ToArray());
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UltraQualityBuilder] {pairs.Count} Ultra materials.");
        }

        private static Texture2D LoadTexture(string relative) =>
            string.IsNullOrEmpty(relative) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{EditorPaths.c_Textures}/{relative}");

        // ------------------------------------------------------------------ Map01 content
        private static readonly Color s_LampColor = new Color(1f, 0.62f, 0.3f);
        private static readonly Color s_ProbeSky = new Color(0.86f, 0.62f, 0.45f);   // warm evening sky seen in puddles

        /// <summary>The five extra lamp lights (the scene already has three): position, intensity, range, shadows.</summary>
        private static readonly (string name, Vector3 position, float intensity, float range, bool shadows)[] s_UltraLights =
        {
            ("UltraLight_StreetLamp2", new Vector3(9.5f, 3.45f, 13.4f), 3f, 7f, true),
            ("UltraLight_WorkshopLampL", new Vector3(-11.8f, 3.3f, 5.9f), 1.6f, 4.5f, false),
            ("UltraLight_WorkshopLampR", new Vector3(-6.2f, 3.3f, 5.9f), 1.6f, 4.5f, false),
            ("UltraLight_RoadLampW", new Vector3(-16f, 3.45f, 17.6f), 2.5f, 7f, false),
            ("UltraLight_RoadLampE", new Vector3(11f, 3.45f, 17.6f), 2.5f, 7f, false),
        };

        /// <summary>Existing lights that also cast soft shadows (only pipelines with additional-light shadows, i.e. Ultra).</summary>
        private static readonly string[] s_ShadowedSceneLights = { "Light_WorkshopGarage", "Light_StreetLamp" };

        private const float c_FoliageExtraShare = 0.5f;       // +50% grass and bushes
        private const float c_FoliageOffsetMin = 0.6f;
        private const float c_FoliageOffsetMax = 1.3f;

        /// <summary>Adds the Ultra-only content to the Map01 scene being built by <see cref="SceneBuilder"/>.</summary>
        public static void AddToMap(MapConfig map, Transform lighting, Transform nature, Transform cameraRoot, GameObject systems,
            GameObject player, Func<float, float, bool> isFreeForFoliage)
        {
            var root = new GameObject("UltraOnly").transform;
            var gate = root.gameObject.AddComponent<QualityTierGate>();
            var content = new List<GameObject>();

            // Lights: shadows on the nearest lamps, the rest emission-like fill.
            foreach (string name in s_ShadowedSceneLights)
            {
                Transform t = lighting.Find(name);
                if (t != null && t.TryGetComponent(out Light light))
                    light.shadows = LightShadows.Soft;
            }
            Transform lights = Child(root, "Ultra_Lights", content);
            foreach ((string name, Vector3 position, float intensity, float range, bool shadows) in s_UltraLights)
            {
                var go = new GameObject(name);
                go.transform.SetParent(lights, false);
                go.transform.position = position;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = s_LampColor;
                light.intensity = intensity;
                light.range = range;
                light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }

            AddDenserFoliage(Child(root, "Ultra_Foliage", content), nature, isFreeForFoliage);
            AddReflectionProbe(Child(root, "Ultra_ReflectionProbe", content), map);
            AddDust(Child(root, "Ultra_Dust", content), map);

            foreach (GameObject go in content)
                go.SetActive(false);                            // off in the saved scene: the gate enables it on Ultra
            CharacterBuilder.SetProps(gate, ("m_MinTier", (int)QualityTier.Ultra), ("m_Targets", content.Cast<Object>().ToArray()));

            AddShotEffects(systems, player);
            PrefabBuilder.Wire(systems.AddComponent<UltraMaterialSwap>(),
                ("m_Set", AssetDatabase.LoadAssetAtPath<UltraMaterialSet>(c_MaterialSet)));
            AddUltraVolume(cameraRoot);
        }

        private static Transform Child(Transform parent, string name, List<GameObject> content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            content.Add(go);
            return go.transform;
        }

        /// <summary>For half of the grass tufts and bushes one more instance of the same prefab close by (= 1.5x).</summary>
        private static void AddDenserFoliage(Transform parent, Transform nature, Func<float, float, bool> isFree)
        {
            var rnd = new Random(31);
            int added = 0;
            foreach (Transform t in nature.Cast<Transform>().ToArray())
            {
                if (!(t.name.StartsWith("Grass") || t.name.StartsWith("Bush")) || rnd.NextDouble() > c_FoliageExtraShare)
                    continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null)
                    continue;
                double angle = rnd.NextDouble() * Math.PI * 2;
                float distance = Mathf.Lerp(c_FoliageOffsetMin, c_FoliageOffsetMax, (float)rnd.NextDouble()) * t.localScale.x;
                Vector3 position = t.position + new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * distance;
                if (!isFree(position.x, position.z))
                    continue;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
                copy.transform.SetParent(parent, false);
                copy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
                copy.transform.localScale = t.localScale * Mathf.Lerp(0.8f, 1.1f, (float)rnd.NextDouble());
                added++;
            }
            Debug.Log($"[UltraQualityBuilder] Ultra foliage: +{added} grass tufts and bushes.");
        }

        /// <summary>A box probe over the whole ground, baked once into a cubemap (no runtime rendering cost).</summary>
        private static void AddReflectionProbe(Transform parent, MapConfig map)
        {
            const float probeHeight = 2.5f, boxHeight = 14f;
            parent.position = new Vector3(map.PlayAreaCenter.x, probeHeight, map.PlayAreaCenter.z);
            var probe = parent.gameObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.size = new Vector3(map.GroundSize.x, boxHeight, map.GroundSize.y);
            probe.center = new Vector3(map.GroundCenter.x - map.PlayAreaCenter.x, 0f, map.GroundCenter.z - map.PlayAreaCenter.z);
            probe.resolution = 128;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = s_ProbeSky;
            probe.shadowDistance = 60f;
            try
            {
                if (Lightmapping.BakeReflectionProbe(probe, c_ReflectionCubemap))
                {
                    AssetDatabase.ImportAsset(c_ReflectionCubemap, ImportAssetOptions.ForceUpdate);
                    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(c_ReflectionCubemap);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UltraQualityBuilder] Reflection probe bake failed: {e.Message}");
            }
            if (probe.customBakedTexture == null)
                Debug.LogWarning("[UltraQualityBuilder] Reflection probe has no cubemap; Ultra materials reflect the ambient only.");
        }

        // ------------------------------------------------------------------ particles
        private static Material ParticleMaterial(string name, Color color, bool additive)
        {
            EditorPaths.EnsureFolder(c_MaterialFolder);
            string path = $"{c_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(c_ParticleShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(c_GlowTexture));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);          // BaseShaderGUI.BlendMode: Alpha = 0, Additive = 2
            BaseShaderGUI.SetMaterialKeywords(mat, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static ParticleSystem Particles(string name, Transform parent, Material material, bool stretched)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.035f;
                renderer.lengthScale = 1f;
            }
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            return ps;
        }

        private static Gradient Fade(Color color)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>Warm dust motes drifting over the lot in the evening sun.</summary>
        private static void AddDust(Transform parent, MapConfig map)
        {
            ParticleSystem ps = Particles("Dust", parent, ParticleMaterial("M_FX_Dust", new Color(1f, 0.86f, 0.64f, 0.32f), false), false);
            ps.transform.position = new Vector3(map.PlayAreaCenter.x, 1.6f, map.PlayAreaCenter.z);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.maxParticles = 300;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 24f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(map.PlayAreaSize.x, 2.6f, map.PlayAreaSize.y);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.15f;
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(Color.white);
        }

        private static ParticleSystem Sparks(string name, Transform parent, float speedMin, float speedMax, float coneAngle,
            float sizeMin = 0.03f, float sizeMax = 0.07f)
        {
            ParticleSystem ps = Particles(name, parent, ParticleMaterial("M_FX_Spark", new Color(4f, 2.3f, 0.8f, 1f), true), true);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = 1.2f;
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;                                       // emitted by ShotEffects
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.02f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            return ps;
        }

        private static void AddShotEffects(GameObject systems, GameObject player)
        {
            var root = new GameObject("ShotEffects").transform;
            root.SetParent(systems.transform, false);
            ParticleSystem muzzle = Sparks("MuzzleSparks", root, 5f, 11f, 14f);
            // Impact sparks show on every tier and must read from ~32 m away (1 px ≈ 1.6 cm): bigger than the muzzle ones.
            ParticleSystem impact = Sparks("ImpactSparks", root, 2.5f, 7f, 60f, 0.09f, 0.18f);
            var effects = root.gameObject.AddComponent<ShotEffects>();
            PrefabBuilder.Wire(effects, ("m_Weapon", player.GetComponent<PlayerWeapon>()), ("m_MuzzleSparks", muzzle), ("m_ImpactSparks", impact),
                ("m_ImpactSound", PlayerBuilder.Sound("Bullet_Impact")));
        }

        // ------------------------------------------------------------------ post-processing
        private static void AddUltraVolume(Transform cameraRoot)
        {
            const float priority = 10f;
            var go = new GameObject("Ultra Volume");
            go.transform.SetParent(cameraRoot, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = BuildVolumeProfile();
            go.SetActive(false);                                              // QualityPostProcessApplier: Ultra only

            var applier = cameraRoot.GetComponentInChildren<QualityPostProcessApplier>(true);
            if (applier != null)
                PrefabBuilder.Wire(applier, ("m_UltraVolume", volume));
        }

        /// <summary>Overrides on top of the base profile: HQ bloom, bokeh DoF, lift/gamma/gain, white balance, grain.</summary>
        private static VolumeProfile BuildVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(c_VolumeProfile) != null)
                AssetDatabase.DeleteAsset(c_VolumeProfile);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, c_VolumeProfile);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.84f, 0.66f));
            bloom.highQualityFiltering.Override(true);
            bloom.downscale.Override(BloomDownscaleMode.Half);
            bloom.maxIterations.Override(8);

            // Bokeh focused on the player (camera distance); the far top and near bottom of the view soften slightly.
            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(ViewConstants.c_CameraDistance);
            dof.focalLength.Override(200f);
            dof.aperture.Override(2.8f);
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(1f);

            var lgg = Add<LiftGammaGain>(profile);
            lgg.lift.Override(new Vector4(0.97f, 0.98f, 1.05f, -0.01f));    // slightly cool shadows
            lgg.gamma.Override(new Vector4(1.02f, 1f, 0.97f, 0f));
            lgg.gain.Override(new Vector4(1.04f, 1f, 0.95f, 0.02f));        // warm sunset highlights

            var white = Add<WhiteBalance>(profile);
            white.temperature.Override(5f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.12f, 0.06f, 0.03f));

            // No chromatic aberration: it split the high-contrast rust of the wrecks at the screen edges into
            // red / cyan fringes.

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
