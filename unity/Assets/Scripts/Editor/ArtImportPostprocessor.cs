using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Import rules for everything under Assets/Art: FBX at scale 1 without cameras/lights/animation,
    /// textures sized and compressed for WebGL, sprites/cursor/normal maps typed by folder and name.
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        private const string c_ArtRoot = "Assets/Art/";
        private const string c_ModelsRoot = "Assets/Art/Models/";
        private const string c_UiRoot = "Assets/Art/Textures/UI/";
        private const string c_VfxRoot = "Assets/Art/VFX/";
        private const string c_WebGL = "WebGL";

        public const int c_MaxAtlasSize = 2048;
        private const int c_MaxUiSize = 512;
        private const int c_GroundAniso = 4;
        private const float c_AlphaTestReference = 0.5f;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(c_ModelsRoot))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = true;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.addCollider = false;                  // colliders are simple primitives added by the prefab builder
            importer.generateSecondaryUV = false;          // no baked lightmaps (WebGL size), lighting is realtime + painted AO
            importer.importNormals = ModelImporterNormals.Import;   // keeps custom normals of foliage cards
            importer.importTangents = ModelImporterTangents.None;   // models use no normal maps
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(c_ArtRoot))
                return;

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            int maxSize = c_MaxAtlasSize;
            var compression = TextureImporterCompression.Compressed;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = false;
            importer.mipMapsPreserveCoverage = false;
            importer.anisoLevel = 1;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;

            if (assetPath.StartsWith(c_UiRoot))
            {
                maxSize = c_MaxUiSize;
                compression = TextureImporterCompression.CompressedHQ;
                if (file == "UI_Crosshair")
                {
                    importer.textureType = TextureImporterType.Cursor;   // hardware cursor needs readable RGBA32
                    importer.isReadable = true;
                    compression = TextureImporterCompression.Uncompressed;
                }
                else
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = SpriteBorderFor(file);
                }
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
            }
            else if (file.EndsWith("_N"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.anisoLevel = c_GroundAniso;
            }
            else if (file.StartsWith("Decal_Stone") || file == "Nature_Atlas")
            {
                // Alpha-cutout textures: coverage-preserving mips keep cut-outs solid in the distance.
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = c_AlphaTestReference;
                if (file != "Nature_Atlas")
                    importer.wrapMode = TextureWrapMode.Clamp;
            }
            else if (file.StartsWith("Decal_") || file == "T_ShadowBlob" || assetPath.StartsWith(c_VfxRoot))
            {
                // Alpha-blended textures: plain mips (coverage scaling would turn faint alpha into opaque squares).
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
            }
            else if (file == "T_Asphalt" || file == "T_Road")
            {
                importer.anisoLevel = c_GroundAniso;               // ground is seen at a grazing angle
            }

            importer.maxTextureSize = maxSize;
            importer.textureCompression = compression;
            importer.crunchedCompression = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = c_WebGL,
                overridden = true,
                maxTextureSize = maxSize,
                format = compression == TextureImporterCompression.Uncompressed
                    ? TextureImporterFormat.RGBA32
                    : TextureImporterFormat.Automatic,
                textureCompression = compression,
                crunchedCompression = false,
            });
        }

        private static Vector4 SpriteBorderFor(string file)
        {
            // 9-slice borders (left, bottom, right, top) in pixels for the rounded UI sprites.
            switch (file)
            {
                case "UI_Panel": return new Vector4(22, 22, 22, 22);
                case "UI_Bar": return new Vector4(14, 14, 14, 14);
                case "UI_Button": return new Vector4(30, 30, 30, 30);
                default: return Vector4.zero;
            }
        }
    }
}
