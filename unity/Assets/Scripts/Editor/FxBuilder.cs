using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

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
