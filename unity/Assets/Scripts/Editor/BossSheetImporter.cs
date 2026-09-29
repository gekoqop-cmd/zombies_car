using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Turns a painted boss pose sheet (referens/bosses/boss&lt;N&gt;_sheet.png: ONE row of 7 poses on a white or transparent background,
    /// facing right — idle, idle, walk, walk, attack, hit, dead) into the boss frames
    /// Art/Textures/Characters/Boss&lt;N&gt;/Boss&lt;N&gt;_0..6.png:
    /// <list type="bullet">
    /// <item>a white background is removed by a flood fill from the sheet border (white inside the drawing stays) with a
    /// soft, colour-decontaminated edge; a transparent sheet is taken as it is;</item>
    /// <item>the poses are the big separate blobs (two touching poses are cut at their thinnest column; drops and
    /// splashes stay with the nearest pose);</item>
    /// <item>every pose keeps its painted size (CharacterBuilder draws boss frames at PixelScale 2) and goes on one canvas
    /// with the feet on the bottom row and the legs centred, so the animation does not jump.</item>
    /// </list>
    /// Writes Logs/BossSheet_&lt;N&gt;.png (the frames side by side on grey) to check. Tools/Setup/Import Boss Sheets; batch:
    /// -executeMethod ZombiCar.EditorTools.BossSheetImporter.ImportAll.
    /// </summary>
    public static class BossSheetImporter
    {
        private const string c_Sheets = "../referens/bosses";
        private const int c_Frames = 7;
        private const float c_WhiteTolerance = 0.13f;  // max distance of a background pixel from pure white
        private const float c_EdgeSoftness = 0.3f;     // distance from white over which an edge pixel becomes opaque
        private const int c_MinGap = 3;                // empty columns that separate two poses
        private const float c_PocketWhiteness = 0.04f; // an enclosed patch this close to pure white is background ...
        private const int c_MinPocket = 40;            // ... when it has at least this many pixels
        private const int c_Pad = 4;
        private const int c_CaptionGap = 6;            // empty rows between the poses and a caption strip under them

        [MenuItem("Tools/Setup/Import Boss Sheets")]
        public static void ImportAll()
        {
            int done = 0;
            for (int boss = 1; boss <= 3; boss++)
            {
                string sheet = $"{c_Sheets}/boss{boss}_sheet.png";
                if (!File.Exists(sheet))
                    continue;
                Import(sheet, $"Boss{boss}");
                done++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[BossSheetImporter] {done} boss sheet(s) imported.");
        }

        public static void Import(string sheetPath, string boss)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(sheetPath));
            int w = tex.width, h = tex.height;
            Color[] px = tex.GetPixels();
            UnityEngine.Object.DestroyImmediate(tex);

            // A sheet exported with transparency needs no background removal (only the faint haze is dropped).
            bool transparent = px[0].a < 0.5f && px[w - 1].a < 0.5f && px[px.Length - 1].a < 0.5f;
            if (transparent)
            {
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a < 0.04f)
                        px[i] = new Color(0f, 0f, 0f, 0f);
                }
            }
            else
            {
                RemoveBackground(px, w, h);
            }
            DropCaptions(px, w, h);
            List<(RectInt rect, Color[] px)> poses = SplitBlobs(px, w, h);
            if (poses.Count != c_Frames)
                throw new InvalidOperationException($"[BossSheetImporter] {sheetPath}: found {poses.Count} poses, expected {c_Frames}. " +
                                                    "Leave clear white space between the poses.");

            // The poses keep the size they are painted at: each boss is as big as its sheet draws it (a sheet with the
            // character about 350-450 px tall gives a boss about 4-5 m tall at PixelScale 2).
            const float scale = 1f;
            var frames = new List<(Color[] px, int w, int h, float anchorX)>();
            foreach ((RectInt r, Color[] crop) in poses)
            {
                int cw = Mathf.Max(1, Mathf.RoundToInt(r.width * scale)), ch = Mathf.Max(1, Mathf.RoundToInt(r.height * scale));
                Color[] scaled = Resample(crop, r.width, r.height, cw, ch);
                frames.Add((scaled, cw, ch, LegsCentre(scaled, cw, ch)));
            }

            // One canvas for all poses: feet on the bottom row, the legs' centre on the canvas centre.
            float left = frames.Max(f => f.anchorX), right = frames.Max(f => f.w - f.anchorX);
            int half = Mathf.CeilToInt(Mathf.Max(left, right)) + c_Pad;
            int canvasW = half * 2, canvasH = frames.Max(f => f.h) + c_Pad;
            string folder = $"{EditorPaths.c_CharacterTextures}/{boss}";
            Directory.CreateDirectory(folder);
            var check = new Color[canvasW * c_Frames * canvasH];
            for (int i = 0; i < frames.Count; i++)
            {
                var canvas = new Color[canvasW * canvasH];
                (Color[] f, int fw, int fh, float ax) = frames[i];
                int ox = Mathf.RoundToInt(half - ax);
                for (int y = 0; y < fh; y++)
                for (int x = 0; x < fw; x++)
                {
                    int cx = ox + x;
                    if (cx >= 0 && cx < canvasW && y < canvasH)
                        canvas[y * canvasW + cx] = f[y * fw + x];
                }
                Bleed(canvas, canvasW, canvasH);
                Save($"{folder}/{boss}_{i}.png", canvas, canvasW, canvasH);
                for (int y = 0; y < canvasH; y++)
                for (int x = 0; x < canvasW; x++)
                {
                    Color c = canvas[y * canvasW + x];
                    check[y * canvasW * c_Frames + i * canvasW + x] = Color.Lerp(new Color(0.3f, 0.32f, 0.36f, 1f), new Color(c.r, c.g, c.b, 1f), c.a);
                }
            }
            Directory.CreateDirectory("Logs");
            Save($"Logs/BossSheet_{boss}.png", check, canvasW * c_Frames, canvasH);
            Debug.Log($"[BossSheetImporter] {boss}: {c_Frames} frames {canvasW}x{canvasH} from {sheetPath} (x{scale:F2}).");
        }

        // ------------------------------------------------------------------ background
        private static float Whiteness(Color c) => Mathf.Max(1f - c.r, 1f - c.g, 1f - c.b);

        private static void RemoveBackground(Color[] px, int w, int h)
        {
            var bg = new bool[px.Length];
            var queue = new Queue<int>();
            void Seed(int x, int y)
            {
                int i = y * w + x;
                if (!bg[i] && Whiteness(px[i]) <= c_WhiteTolerance)
                {
                    bg[i] = true;
                    queue.Enqueue(i);
                }
            }
            for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
            for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }
            void Fill()
            {
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int x = i % w, y = i / w;
                    if (x > 0) Seed(x - 1, y);
                    if (x < w - 1) Seed(x + 1, y);
                    if (y > 0) Seed(x, y - 1);
                    if (y < h - 1) Seed(x, y + 1);
                }
            }
            Fill();
            // Background pockets enclosed by the drawing (between an arm and the body): pure-white patches of some size.
            // Small white details (teeth, eye glints) are smaller and not pure white.
            var seen = new bool[px.Length];
            var patch = new List<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (bg[s] || seen[s] || Whiteness(px[s]) > c_PocketWhiteness)
                    continue;
                patch.Clear();
                var stack = new Stack<int>();
                stack.Push(s);
                seen[s] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    patch.Add(i);
                    int x = i % w, y = i / w;
                    foreach (int n in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                    {
                        if (n < 0 || seen[n] || bg[n] || Whiteness(px[n]) > c_PocketWhiteness)
                            continue;
                        seen[n] = true;
                        stack.Push(n);
                    }
                }
                if (patch.Count < c_MinPocket)
                    continue;
                foreach (int i in patch)
                    Seed(i % w, i / w);
            }
            Fill();                                        // grow the pockets over their soft near-white rim
            // Edge pixels next to the background: alpha from how far they are from white, white taken back out.
            for (int i = 0; i < px.Length; i++)
            {
                if (bg[i])
                {
                    px[i] = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                int x = i % w, y = i / w;
                bool edge = (x > 0 && bg[i - 1]) || (x < w - 1 && bg[i + 1]) || (y > 0 && bg[i - w]) || (y < h - 1 && bg[i + w]);
                if (!edge)
                    continue;
                float a = Mathf.Clamp01(Whiteness(px[i]) / c_EdgeSoftness);
                if (a <= 0.02f)
                {
                    px[i] = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                Color c = px[i];
                px[i] = new Color(Mathf.Clamp01((c.r - (1f - a)) / a), Mathf.Clamp01((c.g - (1f - a)) / a),
                    Mathf.Clamp01((c.b - (1f - a)) / a), a);
            }
        }

        /// <summary>
        /// Pose captions ("1 IDLE" …) in a strip under the poses, apart from them by empty rows: that strip is cleared. The
        /// strip is the content below the lowest empty band when it is short (under a fifth of the drawing's height).
        /// </summary>
        private static void DropCaptions(Color[] px, int w, int h)
        {
            var filled = new bool[h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w && !filled[y]; x++)
                filled[y] = px[y * w + x].a > 0.1f;
            int top = Array.LastIndexOf(filled, true), bottom = Array.IndexOf(filled, true);
            if (bottom < 0)
                return;
            // Texture rows go up from the bottom: scan up from the lowest content for the first empty band.
            int y0 = bottom;
            while (y0 <= top && filled[y0])
                y0++;
            int gapStart = y0;
            while (y0 <= top && !filled[y0])
                y0++;
            int gap = y0 - gapStart;
            bool strip = gap >= c_CaptionGap && y0 <= top && gapStart - bottom < (top - bottom) * 0.2f;
            if (!strip)
                return;
            for (int y = 0; y < gapStart; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color(1f, 1f, 1f, 0f);
            Debug.Log($"[BossSheetImporter] caption strip ({gapStart - bottom} px) removed.");
        }

        // ------------------------------------------------------------------ poses
        /// <summary>
        /// The poses as separate pictures (pixels outside a pose are cleared): the 7 biggest connected blobs are the poses
        /// (claws and splashes may reach over a neighbour's columns, so columns alone cannot cut them), smaller bits (drops,
        /// splashes) go with the nearest pose. Returns the poses left to right with their bounds.
        /// </summary>
        private static List<(RectInt rect, Color[] px)> SplitBlobs(Color[] px, int w, int h)
        {
            var label = new int[px.Length];
            var sizes = new List<int> { 0 };
            var boxes = new List<RectInt> { default };
            var stack = new Stack<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (label[s] != 0 || px[s].a <= 0.1f)
                    continue;
                int id = sizes.Count, size = 0, x0 = w, y0 = h, x1 = -1, y1 = -1;
                label[s] = id;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    int x = i % w, y = i / w;
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int n = ny * w + nx;
                        if (label[n] == 0 && px[n].a > 0.1f)
                        {
                            label[n] = id;
                            stack.Push(n);
                        }
                    }
                }
                sizes.Add(size);
                boxes.Add(new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
            }
            // Poses are the big blobs (bits under 15 % of the biggest are drops and splashes). Two poses that touch make
            // one blob about twice as wide as the others: it is cut at its thinnest column until there are 7.
            int biggest = sizes.Max();
            while (true)
            {
                int[] big = Enumerable.Range(1, sizes.Count - 1).Where(i => sizes[i] >= biggest * 0.15f).ToArray();
                if (big.Length >= c_Frames)
                    break;
                int widest = big.OrderByDescending(i => boxes[i].width).First();
                float median = big.Select(i => (float)boxes[i].width).OrderBy(v => v).ElementAt(big.Length / 2);
                if (boxes[widest].width < median * 1.5f)
                    break;                                              // nothing looks like two poses: give up
                CutBlob(px, w, h, label, sizes, boxes, widest);
            }
            int[] poses = Enumerable.Range(1, sizes.Count - 1).Where(i => sizes[i] >= biggest * 0.15f)
                .OrderByDescending(i => sizes[i]).Take(c_Frames).OrderBy(i => boxes[i].center.x).ToArray();
            if (poses.Length < c_Frames)
                return new List<(RectInt, Color[])>();
            // Every other blob joins the pose whose box is nearest.
            var owner = new int[sizes.Count];
            for (int b = 1; b < sizes.Count; b++)
            {
                int pose = Array.IndexOf(poses, b);
                if (pose >= 0)
                {
                    owner[b] = pose;
                    continue;
                }
                RectInt r = boxes[b];
                owner[b] = Enumerable.Range(0, poses.Length).OrderBy(p => BoxDistance(boxes[poses[p]], r)).First();
            }
            var result = new List<(RectInt, Color[])>();
            for (int p = 0; p < poses.Length; p++)
            {
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                for (int b = 1; b < sizes.Count; b++)
                {
                    if (owner[b] != p)
                        continue;
                    RectInt r = boxes[b];
                    x0 = Math.Min(x0, r.xMin); y0 = Math.Min(y0, r.yMin); x1 = Math.Max(x1, r.xMax - 1); y1 = Math.Max(y1, r.yMax - 1);
                }
                var rect = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                var crop = new Color[rect.width * rect.height];
                for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    int i = (rect.y + y) * w + rect.x + x;
                    int l = label[i];
                    crop[y * rect.width + x] = l > 0 && owner[l] == p ? px[i] : new Color(1f, 1f, 1f, 0f);
                }
                DropEdgeScraps(crop, rect.width, rect.height);
                result.Add((rect, crop));
            }
            return result;
        }

        /// <summary>
        /// Splits blob <paramref name="id"/> in two at the column (in its middle 60 %) with the fewest of its pixels: the
        /// right part gets a new label.
        /// </summary>
        private static void CutBlob(Color[] px, int w, int h, int[] label, List<int> sizes, List<RectInt> boxes, int id)
        {
            RectInt r = boxes[id];
            int from = r.xMin + Mathf.RoundToInt(r.width * 0.2f), to = r.xMin + Mathf.RoundToInt(r.width * 0.8f);
            int cut = from, fewest = int.MaxValue;
            for (int x = from; x < to; x++)
            {
                int n = 0;
                for (int y = r.yMin; y < r.yMax; y++)
                    if (label[y * w + x] == id)
                        n++;
                if (n < fewest)
                {
                    fewest = n;
                    cut = x;
                }
            }
            int right = sizes.Count;
            int leftSize = 0, rightSize = 0;
            int lx0 = w, lx1 = -1, ly0 = h, ly1 = -1, rx0 = w, rx1 = -1, ry0 = h, ry1 = -1;
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                int i = y * w + x;
                if (label[i] != id)
                    continue;
                if (x >= cut)
                {
                    label[i] = right;
                    rightSize++;
                    rx0 = Math.Min(rx0, x); rx1 = Math.Max(rx1, x); ry0 = Math.Min(ry0, y); ry1 = Math.Max(ry1, y);
                }
                else
                {
                    leftSize++;
                    lx0 = Math.Min(lx0, x); lx1 = Math.Max(lx1, x); ly0 = Math.Min(ly0, y); ly1 = Math.Max(ly1, y);
                }
            }
            sizes[id] = leftSize;
            boxes[id] = new RectInt(lx0, ly0, lx1 - lx0 + 1, ly1 - ly0 + 1);
            sizes.Add(rightSize);
            boxes.Add(new RectInt(rx0, ry0, rx1 - rx0 + 1, ry1 - ry0 + 1));
        }

        // A cut between two touching poses can leave a sliver of the neighbour (a boot heel) on the edge of a pose: small
        // pieces that touch the left or right edge go (drops and splashes inside the pose stay).
        private static void DropEdgeScraps(Color[] px, int w, int h)
        {
            var label = new int[px.Length];
            var pieces = new List<(int size, bool edge, List<int> pixels)>();
            var stack = new Stack<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (label[s] != 0 || px[s].a <= 0.1f)
                    continue;
                var pixels = new List<int>();
                bool edge = false;
                label[s] = pieces.Count + 1;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    pixels.Add(i);
                    int x = i % w, y = i / w;
                    edge |= x == 0 || x == w - 1;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        int n = ny * w + nx;
                        if (label[n] == 0 && px[n].a > 0.1f)
                        {
                            label[n] = label[s];
                            stack.Push(n);
                        }
                    }
                }
                pieces.Add((pixels.Count, edge, pixels));
            }
            if (pieces.Count < 2)
                return;
            int biggest = pieces.Max(p => p.size);
            foreach ((int size, bool edge, List<int> pixels) in pieces)
            {
                if (!edge || size >= biggest * 0.04f)
                    continue;
                foreach (int i in pixels)
                    px[i] = new Color(1f, 1f, 1f, 0f);
            }
        }

        private static float BoxDistance(RectInt a, RectInt b)
        {
            float dx = Math.Max(0, Math.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Math.Max(0, Math.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return dx * dx + dy * dy;
        }

        private static List<RectInt> SplitPoses(Color[] px, int w, int h)
        {
            var used = new bool[w];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h && !used[x]; y++)
                used[x] = px[y * w + x].a > 0.1f;
            var spans = new List<(int from, int to)>();
            int start = -1, gap = 0;
            for (int x = 0; x < w; x++)
            {
                if (used[x])
                {
                    if (start < 0)
                        start = x;
                    gap = 0;
                }
                else if (start >= 0 && ++gap >= c_MinGap)
                {
                    spans.Add((start, x - gap));
                    start = -1;
                }
            }
            if (start >= 0)
                spans.Add((start, w - 1));
            // Stray drops between two poses: a narrow span joins its nearest neighbour.
            int minWidth = w / (c_Frames * 5);
            while (spans.Count > c_Frames)
            {
                int i = Enumerable.Range(0, spans.Count).OrderBy(k => spans[k].to - spans[k].from).First();
                if (spans[i].to - spans[i].from > minWidth)
                    break;
                int j = i == 0 ? 1 : i == spans.Count - 1 ? i - 1 :
                    spans[i].from - spans[i - 1].to < spans[i + 1].from - spans[i].to ? i - 1 : i + 1;
                spans[Math.Min(i, j)] = (Math.Min(spans[i].from, spans[j].from), Math.Max(spans[i].to, spans[j].to));
                spans.RemoveAt(Math.Max(i, j));
            }
            var rects = new List<RectInt>();
            foreach ((int from, int to) in spans)
            {
                int y0 = h, y1 = -1;
                for (int y = 0; y < h; y++)
                for (int x = from; x <= to; x++)
                {
                    if (px[y * w + x].a <= 0.1f)
                        continue;
                    y0 = Math.Min(y0, y);
                    y1 = Math.Max(y1, y);
                }
                rects.Add(new RectInt(from, y0, to - from + 1, y1 - y0 + 1));
            }
            return rects;
        }

        private static Color[] Crop(Color[] px, int w, RectInt r)
        {
            var c = new Color[r.width * r.height];
            for (int y = 0; y < r.height; y++)
            for (int x = 0; x < r.width; x++)
                c[y * r.width + x] = px[(r.y + y) * w + r.x + x];
            return c;
        }

        // Centre of the solid pixels in the lowest 30 % (the legs): the anchor that keeps the boss in place.
        private static float LegsCentre(Color[] px, int w, int h)
        {
            int rows = Mathf.Max(1, Mathf.RoundToInt(h * 0.3f));
            double sum = 0, n = 0;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < w; x++)
            {
                float a = px[y * w + x].a;
                if (a < 0.5f)
                    continue;
                sum += x;
                n++;
            }
            return n > 0 ? (float)(sum / n) : w * 0.5f;
        }

        // Area resampling in premultiplied alpha.
        private static Color[] Resample(Color[] s, int sw, int sh, int w, int h)
        {
            var d = new Color[w * h];
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
                d[y * w + x] = a > 1e-6f ? new Color(r / a, g / a, b / a, area > 0f ? a / area : 0f) : new Color(0f, 0f, 0f, 0f);
            }
            return d;
        }

        // Transparent pixels take the nearest solid colour (no halo in the mip maps).
        private static void Bleed(Color[] px, int w, int h)
        {
            var known = px.Select(c => c.a > 0.5f).ToArray();
            var queue = new Queue<int>();
            for (int i = 0; i < px.Length; i++)
                if (known[i])
                    queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                foreach (int n in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                {
                    if (n < 0 || known[n])
                        continue;
                    known[n] = true;
                    px[n] = new Color(px[i].r, px[i].g, px[i].b, px[n].a);
                    queue.Enqueue(n);
                }
            }
        }

        private static void Save(string path, Color[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
