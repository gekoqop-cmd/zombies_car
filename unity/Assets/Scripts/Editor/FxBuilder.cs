using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiCar.Core;
using ZombiCar.Props;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Shared pieces of the gameplay effects (tools/vfx/comic_fx.py sprites): unlit particle-shader materials for
    /// flat ground effects and sprites, and flat quads lying on the ground.
    /// </summary>
    public static class FxBuilder
    {
        public const string c_MaterialFolder = EditorPaths.c_Materials + "/VFX";
        private const string c_ParticleShader = "Universal Render Pipeline/Particles/Unlit";

        public static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{EditorPaths.c_Vfx}/{name}.png");

        /// <summary>A transparent (alpha or additive) unlit material showing <paramref name="texture"/> tinted by <paramref name="color"/>.</summary>
        public static Material Material(string name, string texture, Color color, bool additive)
        {
            EditorPaths.EnsureFolder(c_MaterialFolder);
            string path = $"{c_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(c_ParticleShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(c_ParticleShader);
            Texture2D tex = Texture(texture);
            if (tex == null)
                Debug.LogError($"[FxBuilder] {EditorPaths.c_Vfx}/{texture}.png missing (run tools/vfx/comic_fx.py)");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);          // BaseShaderGUI.BlendMode: Alpha = 0, Additive = 2
            mat.SetFloat("_Cull", (float)CullMode.Off);
            BaseShaderGUI.SetMaterialKeywords(mat, ParticleGUI.SetMaterialKeywords);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public const string c_ExplosionPrefab = EditorPaths.c_Prefabs + "/FX/FX_ComicExplosion.prefab";

        /// <summary>
        /// The comic explosion prefab (<see cref="ComicExplosion"/>): billboarded flash, burst and lettering, ground ring
        /// and scorch, star and smoke-puff particles, an orange flash light. Sizes are for a 4.5 m blast at scale 1.
        /// </summary>
        public static ComicExplosion BuildExplosion()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_Prefabs + "/FX");
            var root = new GameObject("FX_ComicExplosion");
            var sprites = new GameObject("Sprites");
            sprites.transform.SetParent(root.transform, false);
            sprites.transform.localPosition = Vector3.up * 1.1f;
            CharacterBuilder.SetProps(sprites.AddComponent<YAxisBillboard>(), ("m_UpdateEveryFrame", true));
            float pitch = CharacterBuilder.PitchCompensation;
            MeshRenderer flash = Billboard("Flash", sprites.transform, Material("M_FX_BoomFlash", "T_BulletGlow", new Color(1f, 0.85f, 0.55f, 1f), true),
                new Vector3(0f, 0f, 0.05f), new Vector2(3.6f, 3.6f), pitch);
            MeshRenderer burst = Billboard("Burst", sprites.transform, Material("M_FX_ComicBurst", "FX_ComicBurst", Color.white, false),
                Vector3.zero, new Vector2(3.4f, 3.4f), pitch);
            string[] words = { "FX_Boom_Bah", "FX_Boom_Bum", "FX_Boom_Babah" };
            Material[] lettering = words.Select(w => Material("M_" + w, w, Color.white, false)).ToArray();
            MeshRenderer text = Billboard("Text", sprites.transform, lettering[0], new Vector3(0f, 1.1f, -0.15f), new Vector2(3.6f, 1.8f), pitch);
            MeshRenderer ring = GroundQuad("Ring", root.transform, Material("M_FX_BoomRing", "FX_Ring", new Color(1f, 0.8f, 0.45f, 0.9f), true), 1f, 0.06f);
            MeshRenderer scorch = GroundQuad("Scorch", root.transform, Material("M_FX_Scorch", "FX_Scorch", new Color(1f, 1f, 1f, 0.85f), false), 1f, 0.03f);

            ParticleSystem stars = UltraQualityBuilder.Particles("Stars", root.transform, Material("M_FX_Star", "FX_Star", Color.white, false), false);
            stars.transform.localPosition = Vector3.up * 1f;
            ParticleSystem.MainModule main = stars.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 80;
            ParticleSystem.ShapeModule shape = stars.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.EmissionModule emission = stars.emission;
            emission.enabled = false;
            ParticleSystem.RotationOverLifetimeModule spin = stars.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            ParticleSystem.SizeOverLifetimeModule shrink = stars.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystem clouds = UltraQualityBuilder.Particles("Clouds", root.transform, Material("M_FX_Cloud", "FX_Cloud", Color.white, false), false);
            clouds.transform.localPosition = Vector3.up * 0.8f;
            ParticleSystem.MainModule cloudMain = clouds.main;
            cloudMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            cloudMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            cloudMain.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            cloudMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            cloudMain.gravityModifier = -0.15f;
            cloudMain.maxParticles = 40;
            ParticleSystem.ShapeModule cloudShape = clouds.shape;
            cloudShape.shapeType = ParticleSystemShapeType.Sphere;
            cloudShape.radius = 0.6f;
            ParticleSystem.EmissionModule cloudEmission = clouds.emission;
            cloudEmission.enabled = false;
            ParticleSystem.SizeOverLifetimeModule grow = clouds.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
            ParticleSystem.ColorOverLifetimeModule fade = clouds.colorOverLifetime;
            fade.enabled = true;
            fade.color = UltraQualityBuilder.Fade(Color.white);
            ParticleSystem.VelocityOverLifetimeModule drag = clouds.velocityOverLifetime;
            drag.enabled = true;
            drag.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.5f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var fx = root.AddComponent<ComicExplosion>();
            PrefabBuilder.Wire(fx, ("m_Flash", flash), ("m_Burst", burst), ("m_Text", text), ("m_Ring", ring), ("m_Scorch", scorch),
                ("m_Stars", stars), ("m_Clouds", clouds), ("m_Light", light));
            CharacterBuilder.SetProps(fx, ("m_TextMaterials", lettering.Cast<Object>().ToArray()));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, c_ExplosionPrefab);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ComicExplosion>();
        }

        /// <summary>An upright quad facing the camera (child of a billboard), stretched by the camera-pitch compensation.</summary>
        public static MeshRenderer Billboard(string name, Transform parent, Material material, Vector3 position, Vector2 size, float pitch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y * pitch, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>A flat quad lying on the ground (XZ), <paramref name="size"/> metres across, at <paramref name="height"/>.</summary>
        public static MeshRenderer GroundQuad(string name, Transform parent, Material material, float size, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.transform.localScale = new Vector3(size, 1f, size);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CharacterBuilder.c_QuadMesh);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
