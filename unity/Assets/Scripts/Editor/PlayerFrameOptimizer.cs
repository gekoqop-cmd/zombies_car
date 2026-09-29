using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// One-off optimisation of the player frames for video memory (Tools/Setup/Optimize Player Frames):
    /// <list type="bullet">
    /// <item>every frame (with the rifle, no-gun, rifle-only) resampled to <see cref="c_Scale"/> of its size: the
    /// character is drawn about 150–200 px tall on a 1080p screen, the frames were 320 px — the same look for about a
    /// third of the memory;</item>
    /// <item>the idle loop keeps every second frame at half the frame rate (same breathing, half the frames).</item>
    /// </list>
    /// Player_Frames.json follows (canvas, pivot, standing height, barrel tips, idle frames and fps); PlayerBuilder sizes the
    /// body from the standing height, so the character keeps its size on screen. Runs once: frames already at the target
    /// height are left alone. tools/characters/player_mp4/process.py exports at this size too.
    /// </summary>
    public static class PlayerFrameOptimizer
    {
        public const int c_TargetStandingHeight = 192;          // was 320
        private const string c_Frames = EditorPaths.c_CharacterTextures + "/Player";
        private const string c_Meta = EditorPaths.c_DataCharacters + "/Player_Frames.json";

        private const float c_Scale = c_TargetStandingHeight / 320f;

        [MenuItem("Tools/Setup/Optimize Player Frames")]
        public static void Run()
        {
            string json = File.ReadAllText(c_Meta);
            var meta = JsonUtility.FromJson<Meta>(json);
            if (meta.standingHeightPx <= c_TargetStandingHeight)
            {
                Debug.Log($"[PlayerFrameOptimizer] Frames are already {meta.standingHeightPx} px tall: nothing to do.");
                return;
            }
            float k = c_TargetStandingHeight / (float)meta.standingHeightPx;
            int width = Mathf.RoundToInt(meta.canvas[0] * k / 2f) * 2;           // even: the pivot stays on a pixel edge
            int height = Mathf.RoundToInt(meta.canvas[1] * k);

            // Idle: every second frame at half the rate.
            var dropped = new List<string>();
            foreach (ClipMeta clip in meta.clips.Where(c => c.name == "Idle" && c.fps > 8f))
            {
                var keep = Enumerable.Range(0, clip.frames.Length).Where(i => i % 2 == 0 && i < clip.frames.Length - 1).ToArray();
                dropped.AddRange(clip.frames.Where((f, i) => !keep.Contains(i)));
                clip.frames = keep.Select(i => clip.frames[i]).ToArray();
                clip.gunTips = keep.Select(i => clip.gunTips[i]).ToArray();
                clip.fps *= 0.5f;
            }

            int resized = 0;
            foreach (ClipMeta clip in meta.clips)
            {
                foreach (string frame in clip.frames)
                {
                    string suffix = frame.Substring("Player_".Length);
                    foreach (string name in new[] { frame, PlayerGunMask.c_NoGunPrefix + suffix, PlayerGunMask.c_GunPrefix + suffix })
                    {
                        string path = $"{c_Frames}/{name}.png";
                        if (!File.Exists(path))
                            continue;
                        Resample(path, width, height);
                        resized++;
                    }
                }
                foreach (Point tip in clip.gunTips)
                {
                    tip.x *= k;
                    tip.y *= k;
                }
            }
            foreach (string frame in dropped)
            {
                string suffix = frame.Substring("Player_".Length);
                foreach (string name in new[] { frame, PlayerGunMask.c_NoGunPrefix + suffix, PlayerGunMask.c_GunPrefix + suffix })
                    AssetDatabase.DeleteAsset($"{c_Frames}/{name}.png");
            }

            meta.canvas = new[] { width, height };
            meta.pivot.x = width * 0.5f;
            meta.standingHeightPx = c_TargetStandingHeight;
            if (meta.muzzleFlashSize != null)
                meta.muzzleFlashSize = meta.muzzleFlashSize.Select(v => v * k).ToArray();
            meta.pixelsPerUnitHint = c_TargetStandingHeight;
            File.WriteAllText(c_Meta, JsonUtility.ToJson(meta, true));
            AssetDatabase.Refresh();
            Debug.Log($"[PlayerFrameOptimizer] {resized} frames resampled to {width}x{height} (x{k:F2}), {dropped.Count} idle frames dropped.");
        }

        // Area resampling in premultiplied alpha: no dark or white fringe at the soft edges.
        private static void Resample(string path, int w, int h)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(path));
            int sw = src.width, sh = src.height;
            Color[] s = src.GetPixels();
            UnityEngine.Object.DestroyImmediate(src);
            var dst = new Color[w * h];
            float fx = sw / (float)w, fy = sh / (float)h;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float x0 = x * fx, x1 = x0 + fx, y0 = y * fy, y1 = y0 + fy;
                float r = 0f, g = 0f, b = 0f, a = 0f, area = 0f;
                for (int sy = Mathf.FloorToInt(y0); sy < Mathf.CeilToInt(y1) && sy < sh; sy++)
                {
                    float wy = Mathf.Min(y1, sy + 1) - Mathf.Max(y0, sy);
                    for (int sx = Mathf.FloorToInt(x0); sx < Mathf.CeilToInt(x1) && sx < sw; sx++)
                    {
                        float wgt = wy * (Mathf.Min(x1, sx + 1) - Mathf.Max(x0, sx));
                        Color c = s[sy * sw + sx];
                        r += c.r * c.a * wgt;
                        g += c.g * c.a * wgt;
                        b += c.b * c.a * wgt;
                        a += c.a * wgt;
                        area += wgt;
                    }
                }
                float alpha = area > 0f ? a / area : 0f;
                dst[y * w + x] = a > 1e-6f ? new Color(r / a, g / a, b / a, alpha) : new Color(0f, 0f, 0f, 0f);
            }
            // Transparent pixels take the nearest opaque colour (clean mip maps), like the exported frames.
            Bleed(dst, w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(dst);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void Bleed(Color[] px, int w, int h)
        {
            var known = px.Select(c => c.a > 0.5f).ToArray();
            var frontier = new Queue<int>();
            for (int i = 0; i < px.Length; i++)
                if (known[i])
                    frontier.Enqueue(i);
            while (frontier.Count > 0)
            {
                int i = frontier.Dequeue();
                int x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int n = ny * w + nx;
                    if (known[n])
                        continue;
                    known[n] = true;
                    px[n] = new Color(px[i].r, px[i].g, px[i].b, px[n].a);
                    frontier.Enqueue(n);
                }
            }
        }

        [Serializable] private sealed class Point { public float x; public float y; }

        [Serializable]
        private sealed class ClipMeta
        {
            public string[] frames;
            public float fps;
            public bool loop;
            public Point[] gunTips;
            public int[] stepFrames;
            public float strideMetres;
            public string name;
        }

        [Serializable]
        private sealed class Meta
        {
            public int pixelsPerUnitHint;
            public int[] canvas;
            public Point pivot;
            public int standingHeightPx;
            public int sourceFps;
            public ClipMeta[] clips;
            public float runSpeedMetresPerSecond;
            public float[] muzzleFlashSize;
        }
    }
}
