using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Packs the character sprite atlases for WebGL and logs, per atlas, the frames, the packed texture size and how much
    /// it weighs: the stored (crunched) size that goes into the download and the GPU memory once loaded.
    /// Tools/Diagnostics/Atlas Size Report (batch: -executeMethod ZombiCar.EditorTools.AtlasSizeReport.Run -quit).
    /// </summary>
    public static class AtlasSizeReport
    {
        [MenuItem("Tools/Diagnostics/Atlas Size Report")]
        public static void Run()
        {
            SpriteAtlasUtility.PackAllAtlases(BuildTarget.WebGL);
            Type util = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil");
            MethodInfo storage = util?.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            long totalStored = 0, totalGpu = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets/Art/Textures/Characters" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null)
                    continue;
                MethodInfo preview = typeof(SpriteAtlasExtensions).GetMethod("GetPreviewTextures",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var textures = preview != null ? (Texture2D[])preview.Invoke(null, new object[] { atlas }) : Array.Empty<Texture2D>();
                long stored = 0, gpu = 0;
                string dims = string.Empty;
                foreach (Texture2D tex in textures.Where(t => t != null))
                {
                    try
                    {
                        stored += storage != null ? Convert.ToInt64(storage.Invoke(null, new object[] { tex })) : 0;
                    }
                    catch (Exception)
                    {
                        // Internal API differs: the GPU size below still tells the story.
                    }
                    gpu += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                    dims += $"{tex.width}x{tex.height} {tex.format} ";
                }
                int frames = atlas.spriteCount;
                var sprites = new Sprite[frames];
                totalStored += stored;
                totalGpu += gpu;
                Debug.Log($"[AtlasSize] {System.IO.Path.GetFileNameWithoutExtension(path)}: {sprites.Length} frames, {dims}" +
                          $"stored {stored / 1024f:F0} KB, GPU {gpu / 1024f:F0} KB");
            }
            Debug.Log($"[AtlasSize] total stored {totalStored / 1048576f:F2} MB, GPU {totalGpu / 1048576f:F2} MB");
        }
    }
}
