using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Imports TextMesh Pro essentials (once) and creates a dynamic SDF font with Cyrillic from Inter SemiBold
    /// (SIL OFL, Art/Fonts; a dense face that stays readable over the bright map). Latin/Cyrillic/digits are
    /// pre-rendered into the atlas, anything else is added at runtime. <see cref="OutlineMaterial"/> is the HUD's
    /// variant with a black outline and drop shadow.
    /// </summary>
    public static class TmpSetup
    {
        private const string c_TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string c_SourceFont = EditorPaths.c_Fonts + "/Inter-SemiBold.ttf";
        private const string c_FontAsset = EditorPaths.c_Fonts + "/UI_Font SDF.asset";
        private const string c_OutlineMaterial = EditorPaths.c_Fonts + "/UI_Font SDF Outline.mat";
        // Outline / shadow in SDF units (a share of the glyph's distance range): ~2-3 px at the HUD's text sizes.
        private const float c_OutlineWidth = 0.22f;
        private const float c_FaceDilate = 0.08f;
        private static readonly Color s_OutlineColor = new Color(0f, 0f, 0f, 0.95f);
        private static readonly Color s_ShadowColor = new Color(0f, 0f, 0f, 0.6f);
        private const float c_ShadowOffset = 0.9f;
        private const float c_ShadowSoftness = 0.25f;
        private const int c_SamplingSize = 72;
        private const int c_Padding = 8;
        private const int c_AtlasSize = 1024;
        private const int c_TarBlock = 512;

        private const string c_Charset =
            " !\"#%&'()*+,-./0123456789:;<=>?@[]_«»—–…№" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        [MenuItem("Tools/Setup/Ensure TMP Font")]
        public static TMP_FontAsset EnsureFont()
        {
            ImportEssentials();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(c_FontAsset);
            if (font != null && AssetDatabase.GetAssetPath(font.sourceFontFile) != c_SourceFont)
            {
                AssetDatabase.DeleteAsset(c_FontAsset);             // made from another face: rebuild it
                AssetDatabase.DeleteAsset(c_OutlineMaterial);
                font = null;
            }
            if (font == null)
                font = CreateFont();
            if (font != null)
                MakeDefaultFont(font);
            return font;
        }

        /// <summary>
        /// The font's material with a black outline and a soft drop shadow, for text drawn straight over the map
        /// (HUD). Rebuilt from the font material each time, so it follows the font atlas.
        /// </summary>
        public static Material OutlineMaterial()
        {
            TMP_FontAsset font = EnsureFont();
            var material = AssetDatabase.LoadAssetAtPath<Material>(c_OutlineMaterial);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, c_OutlineMaterial);
            }
            else
            {
                material.shader = font.material.shader;
                material.CopyPropertiesFromMaterial(font.material);
            }
            material.SetFloat(ShaderUtilities.ID_FaceDilate, c_FaceDilate);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, c_OutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, s_OutlineColor);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, s_ShadowColor);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, c_ShadowOffset * 0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -c_ShadowOffset);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, c_ShadowSoftness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>New TextMeshPro objects get the Cyrillic font too (the stock LiberationSans SDF has no Cyrillic).</summary>
        private static void MakeDefaultFont(TMP_FontAsset font)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(c_TmpSettings);
            if (settings == null)
                return;
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset CreateFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(c_SourceFont);
            if (font == null)
            {
                Debug.LogError($"[TmpSetup] {c_SourceFont} not found (TMP essentials missing).");
                return null;
            }

            EditorPaths.EnsureFolder(EditorPaths.c_Fonts);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, c_SamplingSize, c_Padding, GlyphRenderMode.SDFAA,
                c_AtlasSize, c_AtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(c_FontAsset);
            AssetDatabase.CreateAsset(asset, c_FontAsset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.TryAddCharacters(c_Charset, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[TmpSetup] Glyphs missing in {font.name}: {missing}");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TmpSetup] Created {c_FontAsset} ({asset.characterTable.Count} glyphs pre-rendered).");
            return asset;
        }

        private static void ImportEssentials()
        {
            if (File.Exists(c_TmpSettings))
                return;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string unitypackage = package != null
                ? Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage")
                : null;
            if (unitypackage == null || !File.Exists(unitypackage))
            {
                Debug.LogError("[TmpSetup] TMP Essential Resources.unitypackage not found in com.unity.ugui.");
                return;
            }
            // AssetDatabase.ImportPackage is deferred in batch mode, so the archive is unpacked directly (GUIDs are
            // kept through the .meta files) and imported synchronously before the font asset is created.
            int files = ExtractUnityPackage(unitypackage, Directory.GetCurrentDirectory());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (File.Exists(c_TmpSettings))
                Debug.Log($"[TmpSetup] TMP Essential Resources extracted ({files} files).");
            else
                Debug.LogError("[TmpSetup] TMP essentials extraction did not produce TMP Settings.asset.");
        }

        /// <summary>
        /// Unpacks a .unitypackage (gzipped tar of &lt;guid&gt;/pathname, &lt;guid&gt;/asset, &lt;guid&gt;/asset.meta)
        /// into the project. Only paths under Assets/ are written; HDRP shader graphs are skipped (URP project).
        /// </summary>
        private static int ExtractUnityPackage(string packagePath, string projectRoot)
        {
            var entries = new Dictionary<string, Dictionary<string, byte[]>>();
            using (FileStream file = File.OpenRead(packagePath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[c_TarBlock];
                while (ReadExactly(gzip, header, c_TarBlock) && header.Any(b => b != 0))
                {
                    string name = Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
                    string prefix = Encoding.ASCII.GetString(header, 345, 155).TrimEnd('\0');
                    if (prefix.Length > 0)
                        name = prefix + "/" + name;
                    long size = Convert.ToInt64(Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' '), 8);
                    var data = new byte[size];
                    long padding = (c_TarBlock - size % c_TarBlock) % c_TarBlock;
                    if (!ReadExactly(gzip, data, (int)size) || !ReadExactly(gzip, new byte[padding], (int)padding))
                        break;
                    char type = (char)header[156];
                    string[] parts = name.Replace('\\', '/').TrimStart('.', '/').Split('/');
                    if ((type != '0' && type != '\0') || parts.Length != 2)
                        continue;
                    if (!entries.TryGetValue(parts[0], out Dictionary<string, byte[]> files))
                        entries[parts[0]] = files = new Dictionary<string, byte[]>();
                    files[parts[1]] = data;
                }
            }

            int written = 0;
            foreach (Dictionary<string, byte[]> files in entries.Values)
            {
                if (!files.TryGetValue("pathname", out byte[] pathBytes))
                    continue;
                string relative = Encoding.UTF8.GetString(pathBytes).Split('\n')[0].Trim();
                if (!relative.StartsWith("Assets/") || relative.Contains("..") || relative.Contains("HDRP"))
                    continue;
                string target = Path.Combine(projectRoot, relative);
                if (files.TryGetValue("asset", out byte[] asset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target) ?? projectRoot);
                    File.WriteAllBytes(target, asset);
                    written++;
                }
                else
                {
                    Directory.CreateDirectory(target);   // folder entry
                }
                if (files.TryGetValue("asset.meta", out byte[] meta))
                    File.WriteAllBytes(target + ".meta", meta);
            }
            return written;
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }
            return true;
        }
    }
}
