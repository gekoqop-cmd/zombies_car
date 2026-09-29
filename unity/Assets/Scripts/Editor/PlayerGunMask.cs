using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Separates the rifle from the player frames (tools/characters/player_mp4 bake it into every frame):
    /// <list type="bullet">
    /// <item>Player_NoGun_&lt;Clip&gt;_NN — the character without the rifle (and the gloves on it), the gap where the
    /// rifle crossed the body filled from the surrounding cloth: shown while the axe is in hand, so the player no longer
    /// holds the axe and the rifle at once;</item>
    /// <item>Player_GunGray_&lt;Clip&gt;_NN — only the rifle, in lifted greys (ink outlines stay dark): drawn over the
    /// no-gun body and tinted by the equipped rifle skin.</item>
    /// </list>
    /// Same canvas and pivot as the source frames. The rifle is found per frame from its barrel tip
    /// (Player_Frames.json): thick dark-metal / wood areas (thin ink outlines are opened away first) connected to the
    /// barrel, grown back over their own outline. Writes Logs/GunMaskCheck.png (original | no gun | gold skin).
    /// </summary>
    public static class PlayerGunMask
    {
        private const string c_Frames = EditorPaths.c_CharacterTextures + "/Player";
        private const string c_Meta = EditorPaths.c_DataCharacters + "/Player_Frames.json";
        public const string c_NoGunPrefix = "Player_NoGun_";
        public const string c_GunPrefix = "Player_GunGray_";
        private const string c_Check = "Logs/GunMaskCheck.png";      // Temp/ is wiped when the editor quits

        // Search area around the rifle, px from the barrel tip (image y up): the stock reaches back to the body centre.
        private const int c_BackFromCentre = 30;
        private const int c_Above = 36;
        private const int c_Below = 62;
        private const int c_ScarfReach = 25;         // px in front of the body centre the scarf / chin reach
        private const int c_AboveNearBody = 10;
        private const int c_OpenRadius = 2;          // removes ink lines up to ~4 px
        private const int c_GrowRadius = 3;          // takes the rifle's own outline back
        private const int c_FillReach = 26;          // a hole with cloth this close on both sides is inside the body

        [Serializable] private sealed class Point { public float x; public float y; }
        [Serializable] private sealed class ClipMeta { public string name; public string[] frames; public Point[] gunTips; }
        [Serializable] private sealed class FramesMeta { public ClipMeta[] clips; public int standingHeightPx; }

        // The pixel sizes above were tuned on 320 px tall frames: scaled to the frames at hand.
        private static float s_K = 1f;
        private static int Px(int designPx) => Mathf.Max(1, Mathf.RoundToInt(designPx * s_K));

        [MenuItem("Tools/Setup/Build Player No-Gun Frames")]
        public static void Run()
        {
            var meta = JsonUtility.FromJson<FramesMeta>(File.ReadAllText(c_Meta));
            s_K = meta.standingHeightPx > 0 ? meta.standingHeightPx / 320f : 1f;
            var check = new List<(Color[] a, Color[] b, Color[] c, int w, int h)>();
            int count = 0;
            foreach (ClipMeta clip in meta.clips)
            {
                for (int f = 0; f < clip.frames.Length; f++)
                {
                    string name = clip.frames[f];
                    var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    src.LoadImage(File.ReadAllBytes($"{c_Frames}/{name}.png"));
                    int w = src.width, h = src.height;
                    Color[] px = src.GetPixels();
                    Point tip = clip.gunTips[f];
                    Split(px, w, h, new Vector2Int(Mathf.RoundToInt(w * 0.5f + tip.x), Mathf.RoundToInt(tip.y)),
                        out Color[] noGun, out Color[] gun);
                    string suffix = name.Substring("Player_".Length);
                    Save($"{c_Frames}/{c_NoGunPrefix}{suffix}.png", noGun, w, h);
                    Save($"{c_Frames}/{c_GunPrefix}{suffix}.png", gun, w, h);
                    if (f == 0 || f == clip.frames.Length / 2)
                        check.Add((px, noGun, Skinned(noGun, gun, new Color(1f, 0.78f, 0.25f)), w, h));
                    UnityEngine.Object.DestroyImmediate(src);
                    count++;
                }
            }
            AssetDatabase.Refresh();
            WriteCheck(check);
            Debug.Log($"[PlayerGunMask] {count} frames split into {c_NoGunPrefix}* and {c_GunPrefix}*; check sheet {c_Check}.");
        }

        private static void Split(Color[] px, int w, int h, Vector2Int tip, out Color[] noGun, out Color[] gun)
        {
            int x0 = Mathf.Max(0, w / 2 - Px(c_BackFromCentre)), x1 = Mathf.Min(w - 1, tip.x + Px(4));
            int y0 = Mathf.Max(0, tip.y - Px(c_Below)), y1 = Mathf.Min(h - 1, tip.y + Px(c_Above));
            // Near the body only a low band above the barrel line: the dark scarf and the chin sit just above the stock.
            int scarfEnd = w / 2 + Px(c_ScarfReach);
            bool InRegion(int x, int y) => x >= x0 && x <= x1 && y >= y0 && y <= (x < scarfEnd ? tip.y + Px(c_AboveNearBody) : y1);

            // Rifle material: dark low-saturation metal (the gloves on it too) or the brown wood.
            var candidate = new bool[w * h];
            var dark = new bool[w * h];
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                Color c = px[i];
                if (c.a < 0.5f || !InRegion(x, y))
                    continue;
                Color.RGBToHSV(c, out float hue, out float sat, out float val);
                bool metal = val < 0.44f && sat < 0.38f;
                bool wood = hue > 0.02f && hue < 0.13f && sat > 0.35f && val > 0.22f && val < 0.78f;
                candidate[i] = metal || wood;
                dark[i] = val < 0.36f;
            }

            // Thick parts only (the ink outlines of the body are thin), then everything connected to the barrel.
            bool[] opened = Dilate(Erode(candidate, w, h, Px(c_OpenRadius)), w, h, Px(c_OpenRadius));
            var core = new bool[w * h];
            var stack = new Stack<int>();
            // Seeds along the barrel and the receiver (the thin barrel alone may not survive the opening).
            for (int y = tip.y - Px(14); y <= tip.y + Px(14); y++)
            for (int x = tip.x - Px(110); x <= tip.x; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h)
                    continue;
                int i = y * w + x;
                if (opened[i] && !core[i])
                {
                    core[i] = true;
                    stack.Push(i);
                }
            }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int n = ny * w + nx;
                    if (!core[n] && opened[n])
                    {
                        core[n] = true;
                        stack.Push(n);
                    }
                }
            }

            // Grow back over the rifle's own outline and small details, close pinholes.
            bool[] grown = Dilate(core, w, h, Px(c_GrowRadius));
            var mask = new bool[w * h];
            for (int i = 0; i < mask.Length; i++)
            {
                int x = i % w, y = i / w;
                mask[i] = InRegion(x, y) && px[i].a > 0.03f && (core[i] || (grown[i] && (candidate[i] || dark[i] || px[i].a < 0.5f)));
            }
            mask = Erode(Dilate(mask, w, h, 1), w, h, 1);
            for (int i = 0; i < mask.Length; i++)
                mask[i] &= px[i].a > 0.03f;

            // The rifle alone (lifted greys, ink stays dark) for the skins.
            gun = new Color[w * h];
            for (int i = 0; i < gun.Length; i++)
            {
                if (!mask[i])
                    continue;
                Color c = px[i];
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                float g = lum < 0.12f ? lum * 1.4f : 0.34f + 0.66f * Mathf.Sqrt(Mathf.Clamp01((lum - 0.12f) / 0.7f));
                gun[i] = new Color(g, g, g, c.a);
            }

            // The body without it: the rifle's pixels go, the ones inside the body are filled from the cloth around.
            noGun = (Color[])px.Clone();
            var known = new bool[w * h];
            for (int i = 0; i < noGun.Length; i++)
            {
                if (mask[i])
                    noGun[i].a = 0f;                    // colour kept: the edge mips blend into dark ink, not black
                known[i] = noGun[i].a > 0.5f;
            }
            var fill = new List<int>();
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;
                int x = i % w, y = i / w;
                bool Opaque(int dx, int dy)
                {
                    for (int k = 1; k <= Px(c_FillReach); k++)
                    {
                        int nx = x + dx * k, ny = y + dy * k;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            return false;
                        if (known[ny * w + nx])
                            return true;
                    }
                    return false;
                }
                // Inside the body: cloth on three of the four sides (the chest behind the rifle is open only to the
                // front; the barrel in the air has at most the hand behind it).
                int sides = (Opaque(-1, 0) ? 1 : 0) + (Opaque(1, 0) ? 1 : 0) + (Opaque(0, 1) ? 1 : 0) + (Opaque(0, -1) ? 1 : 0);
                if (sides >= 3)
                    fill.Add(i);
            }
            Inpaint(noGun, known, fill, w, h);
            KeepMainBody(noGun, w, h);
        }

        // Diffusion fill: every hole pixel takes the average of its known neighbours, ring by ring from the edge.
        private static void Inpaint(Color[] px, bool[] known, List<int> holes, int w, int h)
        {
            var left = new HashSet<int>(holes);
            for (int pass = 0; pass < 200 && left.Count > 0; pass++)
            {
                var done = new List<(int i, Color c)>();
                foreach (int i in left)
                {
                    int x = i % w, y = i / w;
                    Color sum = Color.clear;
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h || !known[ny * w + nx])
                            continue;
                        sum += px[ny * w + nx];
                        n++;
                    }
                    if (n >= 2)
                        done.Add((i, sum / n));
                }
                if (done.Count == 0)
                    break;
                foreach ((int i, Color c) in done)
                {
                    px[i] = new Color(c.r, c.g, c.b, 1f);
                    known[i] = true;
                    left.Remove(i);
                }
            }
        }

        // Pieces cut off from the body (the front hand on the handguard) go: the axe is held there instead.
        private static void KeepMainBody(Color[] px, int w, int h)
        {
            var label = new int[w * h];
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (label[s] != 0 || px[s].a < 0.3f)
                    continue;
                int id = sizes.Count, size = 0;
                label[s] = id;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    int x = i % w, y = i / w;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int n = ny * w + nx;
                        if (label[n] == 0 && px[n].a >= 0.3f)
                        {
                            label[n] = id;
                            stack.Push(n);
                        }
                    }
                }
                sizes.Add(size);
            }
            int main = sizes.IndexOf(sizes.Max());
            int keepAbove = sizes[main] / 8;                              // a second big part (a detached leg) stays
            bool[] near = Dilate(label.Select(l => l == main || (l > 0 && sizes[l] > keepAbove)).ToArray(), w, h, 2);
            for (int i = 0; i < px.Length; i++)
            {
                int l = label[i];
                if (l == main || (l > 0 && sizes[l] > keepAbove))
                    continue;
                // Small detached bits (hand remnants) and the faint haze of the barrel's soft edge go.
                if (l > 0 || !near[i])
                    px[i].a = 0f;
            }
        }

        private static bool[] Erode(bool[] m, int w, int h, int r) => Morph(m, w, h, r, true);
        private static bool[] Dilate(bool[] m, int w, int h, int r) => Morph(m, w, h, r, false);

        private static bool[] Morph(bool[] m, int w, int h, int r, bool erode)
        {
            var result = new bool[m.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool v = erode;
                for (int dy = -r; dy <= r && v == erode; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    bool s = nx >= 0 && ny >= 0 && nx < w && ny < h && m[ny * w + nx];
                    if (erode ? !s : s)
                    {
                        v = !erode;
                        break;
                    }
                }
                result[y * w + x] = v;
            }
            return result;
        }

        private static Color[] Skinned(Color[] body, Color[] gun, Color tint)
        {
            var result = (Color[])body.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                Color g = gun[i] * tint;
                g.a = gun[i].a;
                result[i] = Color.Lerp(result[i], new Color(g.r, g.g, g.b, 1f), g.a);
                result[i].a = Mathf.Max(body[i].a, gun[i].a);
            }
            return result;
        }

        private static void Save(string path, Color[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void WriteCheck(List<(Color[] a, Color[] b, Color[] c, int w, int h)> frames)
        {
            if (frames.Count == 0)
                return;
            int w = frames[0].w, h = frames[0].h;
            var sheet = new Texture2D(w * 3, h * frames.Count, TextureFormat.RGBA32, false);
            var bg = new Color(0.2f, 0.22f, 0.26f, 1f);
            for (int r = 0; r < frames.Count; r++)
            {
                Color[][] cols = { frames[r].a, frames[r].b, frames[r].c };
                for (int c = 0; c < 3; c++)
                {
                    var block = new Color[w * h];
                    for (int i = 0; i < block.Length; i++)
                        block[i] = Color.Lerp(bg, new Color(cols[c][i].r, cols[c][i].g, cols[c][i].b, 1f), cols[c][i].a);
                    sheet.SetPixels(c * w, (frames.Count - 1 - r) * h, w, h, block);
                }
            }
            sheet.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(c_Check, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }
}
