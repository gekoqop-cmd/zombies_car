using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Creates every material of the project (URP Simple Lit / Unlit, GPU instancing on) and remaps the materials
    /// embedded in the FBX files to them. Bushes get a slightly brighter foliage material.
    /// </summary>
    public static class MaterialLibrary
    {
        private const string c_SimpleLit = "Universal Render Pipeline/Simple Lit";
        private const string c_Unlit = "Universal Render Pipeline/Unlit";
        private const float c_AlphaCutoff = 0.5f;
        private const float c_DefaultSmoothness = 0.15f;
        // Declared before s_Specs: static fields initialise in textual order.
        private static readonly Color s_GrassTint = new Color(0.9f, 1f, 0.8f, 1f);     // counters the orange sunset on dry grass
        private const float c_BushLighten = 1.12f;                                      // bushes read slightly lighter than grass

        public enum Surface { Opaque, Cutout, Transparent }

        public sealed class Spec
        {
            public string Name;
            public string Folder;
            public string Texture;          // path relative to Assets/Art/Textures (or absolute Assets/...)
            public string Normal;
            public string EmissionTexture;
            public Color Emission = Color.black;
            public Color Tint = Color.white;
            public Surface Surface = Surface.Opaque;
            public bool TwoSided;
            public bool Unlit;
            public bool ReceiveShadows = true;
            public float Smoothness = c_DefaultSmoothness;
            public Vector2 Tiling = Vector2.one;
        }

        private static Spec S(string name, string folder, string texture) => new Spec { Name = name, Folder = folder, Texture = texture };

        private static readonly Spec[] s_Specs = BuildSpecs();

        private static Spec[] BuildSpecs()
        {
            var list = new List<Spec>();
            // Workshop and its props (individual tiling textures).
            (string mat, string tex)[] workshop =
            {
                ("M_StoneWall", "stone_wall"), ("M_Plaster", "plaster"), ("M_RedBand", "red_band"), ("M_StoneGray", "stone_gray"),
                ("M_Roof", "roof_concrete"), ("M_Floor", "floor_tiles"), ("M_Paver", "paver"), ("M_Soil", "soil"),
                ("M_Rock", "rock"), ("M_Wood", "wood"), ("M_MetalDark", "metal_dark"), ("M_MetalLight", "metal_light"),
                ("M_MetalRed", "metal_red"), ("M_Cardboard", "cardboard"), ("M_Tire", "tire"), ("M_Barrel", "barrel"),
                ("M_Glass", "glass"), ("M_Vent", "vent"), ("M_Fan", "fan"), ("M_Sign", "sign"), ("M_Pegboard", "pegboard"),
            };
            foreach (var (mat, tex) in workshop)
                list.Add(S(mat, "Workshop", $"Workshop/{tex}.png"));
            list.Add(new Spec { Name = "M_LampGlow", Folder = "Workshop", Texture = "Workshop/lamp_glow.png",
                EmissionTexture = "Workshop/lamp_glow.png", Emission = Color.white * 2.5f });
            list.Add(new Spec { Name = "M_Leaf", Folder = "Workshop", Texture = "Workshop/leaf.png", TwoSided = true });
            list.Add(new Spec { Name = "M_Grass", Folder = "Workshop", Texture = "Workshop/grass.png", TwoSided = true });
            list.Find(s => s.Name == "M_Glass").Smoothness = 0.6f;

            // Prop sheets (one atlas per sheet).
            list.Add(S("M_Signs_Atlas", "Signs", "Signs/signs_atlas.png"));
            list.Add(new Spec { Name = "M_Signs_Emissive", Folder = "Signs", Texture = "Signs/signs_emissive.png",
                EmissionTexture = "Signs/signs_emissive.png", Emission = Color.white * 3f });
            list.Add(new Spec { Name = "M_Fences_Atlas", Folder = "Fences", Texture = "Fences/fences_atlas.png",
                Tint = new Color(0.86f, 0.78f, 0.72f, 1f) });   // weathered brown wood under the warm sun
            list.Add(S("M_Street_Atlas", "Street", "Street/street_atlas.png"));
            list.Add(S("M_Cargo_Atlas", "Cargo", "Cargo/cargo_atlas.png"));

            // Nature: alpha-cutout cards, rocks, stone decals.
            list.Add(new Spec { Name = "M_Nature_Foliage", Folder = "Nature", Texture = "Nature/Nature_Atlas.png",
                Surface = Surface.Cutout, TwoSided = true, Smoothness = 0f, Tint = s_GrassTint });
            list.Add(new Spec { Name = "M_Nature_Bush", Folder = "Nature", Texture = "Nature/Nature_Atlas.png",
                Surface = Surface.Cutout, TwoSided = true, Smoothness = 0f,
                Tint = new Color(s_GrassTint.r * c_BushLighten, s_GrassTint.g * c_BushLighten, s_GrassTint.b * c_BushLighten, 1f) });
            list.Add(S("M_Rocks", "Nature", "Nature/Rocks_Atlas.png"));
            for (int i = 1; i <= 3; i++)
                list.Add(new Spec { Name = $"M_Decal_Stone{i}", Folder = "Nature", Texture = $"Nature/Decals/Decal_Stone{i}.png",
                    Surface = Surface.Cutout, Smoothness = 0f, Tint = new Color(0.84f, 0.86f, 0.92f, 1f) });

            // Player car (+ damage stage materials for CarHealth later).
            list.Add(S("M_Car_Intact", "Car", "Car/car_atlas_intact.png"));
            list.Add(S("M_Car_Damaged", "Car", "Car/car_atlas_damaged.png"));
            list.Add(S("M_Car_Wrecked", "Car", "Car/car_atlas_wrecked.png"));
            list.Add(new Spec { Name = "M_Car_Emissive", Folder = "Car", Texture = "Car/car_emissive.png",
                EmissionTexture = "Car/car_emissive.png", Emission = Color.white * 3f });

            // Parked wrecks (tools/wrecks): tileable paint per car + a shared parts atlas.
            list.Add(new Spec { Name = "M_Wreck_Paint_Pickup", Folder = "Vehicles", Texture = "Vehicles/wreck_paint_pickup.png", Smoothness = 0.15f });
            list.Add(new Spec { Name = "M_Wreck_Paint_Sedan", Folder = "Vehicles", Texture = "Vehicles/wreck_paint_sedan.png", Smoothness = 0.15f });
            list.Add(new Spec { Name = "M_Wreck_Parts", Folder = "Vehicles", Texture = "Vehicles/wreck_parts.png", Smoothness = 0.1f });

            // Generated environment.
            list.Add(new Spec { Name = "M_Asphalt", Folder = "Environment", Texture = "Environment/T_Asphalt.png",
                Normal = "Environment/T_Asphalt_N.png", Smoothness = 0.05f });
            list.Add(S("M_Road", "Environment", "Environment/T_Road.png"));
            list.Add(S("M_Sidewalk", "Environment", "Workshop/stone_gray.png"));
            foreach (string decal in new[] { "Decal_Crack1", "Decal_Crack2", "Decal_Pothole1", "Decal_Pothole2", "Decal_Manhole" })
                list.Add(new Spec { Name = "M_" + decal, Folder = "Environment", Texture = $"Environment/{decal}.png",
                    Surface = Surface.Transparent, Smoothness = 0f });
            list.Add(new Spec { Name = "M_ShadowBlob", Folder = "Environment", Texture = "Environment/T_ShadowBlob.png",
                Surface = Surface.Transparent, Unlit = true, Tint = Color.white });
            // Stand-ins for concept objects without a model (tree, bicycle, fire hydrant): flat colour hints.
            (string, Color)[] stubs =
            {
                ("M_Stub_Trunk", new Color(0.42f, 0.29f, 0.19f)), ("M_Stub_Foliage", new Color(0.40f, 0.55f, 0.22f)),
                ("M_Stub_Bicycle", new Color(0.55f, 0.13f, 0.10f)), ("M_Stub_Hydrant", new Color(0.78f, 0.14f, 0.09f)),
            };
            foreach (var (name, color) in stubs)
                list.Add(new Spec { Name = name, Folder = "Stubs", Tint = color, Smoothness = 0f });

            // VFX (characters are 2D sprites: URP Sprite-Unlit-Default, see CharacterBuilder).
            list.Add(new Spec { Name = "M_Bullet", Folder = "VFX", Unlit = true, Tint = new Color(6f, 4.2f, 1.4f, 1f) });
            return list.ToArray();
        }

        [MenuItem("Tools/Setup/Build Materials")]
        public static void Run()
        {
            foreach (Spec spec in s_Specs)
                Build(spec);
            AssetDatabase.SaveAssets();
            RemapModelMaterials();
            Debug.Log($"[MaterialLibrary] {s_Specs.Length} materials built and FBX materials remapped.");
        }

        public static Material Get(string name)
        {
            Spec spec = s_Specs.FirstOrDefault(s => s.Name == name);
            if (spec == null)
            {
                Debug.LogError($"[MaterialLibrary] Unknown material {name}");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Material>(PathOf(spec));
        }

        private static string PathOf(Spec spec) => $"{EditorPaths.c_Materials}/{spec.Folder}/{spec.Name}.mat";

        private static Texture2D LoadTexture(string relative)
        {
            if (string.IsNullOrEmpty(relative))
                return null;
            string path = relative.StartsWith("Assets/") ? relative : $"{EditorPaths.c_Textures}/{relative}";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                Debug.LogError($"[MaterialLibrary] Texture not found: {path}");
            return tex;
        }

        private static void Build(Spec s)
        {
            Shader shader = Shader.Find(s.Unlit ? c_Unlit : c_SimpleLit);
            string path = PathOf(s);
            EditorPaths.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetTexture("_BaseMap", LoadTexture(s.Texture));
            mat.SetTextureScale("_BaseMap", s.Tiling);
            mat.SetColor("_BaseColor", s.Tint);
            mat.SetFloat("_Surface", s.Surface == Surface.Transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);                                   // alpha blending when transparent
            // Plain SrcAlpha blending: with "preserve specular" SimpleLit premultiplies only direct light by alpha,
            // so the ambient term of near-transparent texels would lighten the ground under every decal.
            mat.SetFloat("_BlendModePreserveSpecular", 0f);
            mat.SetFloat("_AlphaClip", s.Surface == Surface.Cutout ? 1f : 0f);
            mat.SetFloat("_Cutoff", c_AlphaCutoff);
            mat.SetFloat("_Cull", s.TwoSided ? 0f : 2f);
            mat.enableInstancing = true;

            if (s.Unlit)
            {
                BaseShaderGUI.SetMaterialKeywords(mat);
            }
            else
            {
                mat.SetFloat("_Smoothness", s.Smoothness);
                mat.SetFloat("_SpecularHighlights", 0f);                  // painted look, no Blinn-Phong highlights
                mat.SetFloat("_ReceiveShadows", s.ReceiveShadows ? 1f : 0f);
                mat.SetTexture("_BumpMap", LoadTexture(s.Normal));
                Texture2D emission = LoadTexture(s.EmissionTexture);
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", s.Emission);
                mat.globalIlluminationFlags = s.Emission.maxColorComponent > 0f
                    ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                    : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                BaseShaderGUI.SetMaterialKeywords(mat, SimpleLitGUI.SetMaterialKeywords);
            }
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Points the materials embedded in every FBX to the library materials (by name).</summary>
        public static void RemapModelMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { EditorPaths.c_Models }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                    continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool changed = false;
                foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string target = embedded.name;
                    if (file.StartsWith("Bush") && target == "M_Nature_Foliage")
                        target = "M_Nature_Bush";
                    Material library = Get(target);
                    if (library == null)
                        continue;
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), library);
                    changed = true;
                }
                if (changed)
                    importer.SaveAndReimport();
            }
        }
    }
}
