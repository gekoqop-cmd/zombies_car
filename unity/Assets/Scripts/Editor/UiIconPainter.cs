using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Paints the two gold UI icons that have no art source: UI_Icon_Ad (a TV screen with a play mark on a gold disc —
    /// every "reward for an ad" button uses it) and UI_Icon_Coin (the coins for the cases). Drawn with antialiased
    /// shapes (4×4 supersampling) in the gold of the game's reward texts, written once to Art/Textures/UI (the import
    /// settings come from <see cref="ArtImportPostprocessor"/>); delete a PNG to paint it again.
    /// </summary>
    public static class UiIconPainter
    {
        private const string c_Folder = "Assets/Art/Textures/UI/";
        private const int c_Size = 256;
        private const int c_Samples = 4;

        private static readonly Color s_GoldTop = new Color(1f, 0.9f, 0.45f, 1f);
        private static readonly Color s_GoldBottom = new Color(0.95f, 0.6f, 0.1f, 1f);
        private static readonly Color s_Rim = new Color(0.66f, 0.36f, 0.04f, 1f);

        [MenuItem("Tools/Setup/Paint Gold UI Icons")]
        public static void PaintAll() => EnsureIcons(true);

        /// <summary>Paints the icons that are missing (all of them when <paramref name="force"/>).</summary>
        public static void EnsureIcons(bool force = false)
        {
            Paint("UI_Icon_Ad", AdIcon, force);
            Paint("UI_Icon_Coin", CoinIcon, force);
        }

        private static void Paint(string name, Func<Vector2, Color> shade, bool force)
        {
            string path = c_Folder + name + ".png";
            if (!force && File.Exists(path))
                return;
            var texture = new Texture2D(c_Size, c_Size, TextureFormat.RGBA32, false);
            var pixels = new Color[c_Size * c_Size];
            for (int y = 0; y < c_Size; y++)
            {
                for (int x = 0; x < c_Size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < c_Samples; sy++)
                    {
                        for (int sx = 0; sx < c_Samples; sx++)
                        {
                            // -1..1, y up.
                            var p = new Vector2((x + (sx + 0.5f) / c_Samples) / c_Size * 2f - 1f,
                                (y + (sy + 0.5f) / c_Samples) / c_Size * 2f - 1f);
                            Color c = shade(p);
                            sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);   // premultiplied while averaging
                        }
                    }
                    sum /= c_Samples * c_Samples;
                    pixels[y * c_Size + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[UiIconPainter] Painted {path}.");
        }

        // ---------------------------------------------------------------- icons
        // The gold disc both icons sit on: a vertical gradient, a darker rim, a soft shine at the top.
        private static Color GoldDisc(Vector2 p, out bool inside)
        {
            float r = p.magnitude;
            inside = r <= 0.96f;
            if (!inside)
                return Color.clear;
            if (r > 0.86f)
                return s_Rim;
            Color c = Color.Lerp(s_GoldBottom, s_GoldTop, Mathf.Clamp01(p.y * 0.6f + 0.55f));
            // Shine: an ellipse in the upper part.
            Vector2 s = new Vector2(p.x / 0.62f, (p.y - 0.42f) / 0.3f);
            if (s.sqrMagnitude < 1f)
                c = Color.Lerp(c, Color.white, 0.28f * (1f - s.sqrMagnitude));
            return c;
        }

        private static Color AdIcon(Vector2 p)
        {
            Color c = GoldDisc(p, out bool inside);
            if (!inside)
                return c;
            Vector2 q = p - new Vector2(0f, -0.04f);
            var dark = new Color(0.36f, 0.17f, 0.03f, 1f);
            // Antenna of the TV.
            if (Segment(p, new Vector2(-0.16f, 0.62f), new Vector2(0f, 0.42f)) < 0.035f ||
                Segment(p, new Vector2(0.16f, 0.62f), new Vector2(0f, 0.42f)) < 0.035f)
                return dark;
            if (RoundBox(q, new Vector2(0.5f, 0.38f), 0.1f) <= 0f)
            {
                c = dark;
                if (RoundBox(q, new Vector2(0.4f, 0.28f), 0.06f) <= 0f)
                {
                    c = Color.Lerp(new Color(1f, 0.98f, 0.9f, 1f), new Color(1f, 0.92f, 0.7f, 1f), Mathf.Clamp01(0.5f - q.y));
                    // Play mark.
                    if (InTriangle(q, new Vector2(-0.12f, 0.16f), new Vector2(-0.12f, -0.16f), new Vector2(0.17f, 0f)))
                        c = new Color(0.93f, 0.42f, 0.08f, 1f);
                }
            }
            return c;
        }

        private static Color CoinIcon(Vector2 p)
        {
            Color c = GoldDisc(p, out bool inside);
            if (!inside)
                return c;
            float r = p.magnitude;
            // An embossed inner ring and a star in the middle.
            if (r > 0.64f && r < 0.7f)
                return Color.Lerp(s_Rim, s_GoldBottom, 0.35f);
            if (Star(p, 0.42f, 0.18f))
                c = Color.Lerp(c, new Color(1f, 0.97f, 0.75f, 1f), 0.75f);
            return c;
        }

        // ---------------------------------------------------------------- shapes
        private static float RoundBox(Vector2 p, Vector2 half, float radius)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
            return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v, Vector2 w) => (v.x - u.x) * (w.y - u.y) - (v.y - u.y) * (w.x - u.x);
            float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        // A five-pointed star, point up: inside when the radius is under the star's outline at that angle.
        private static bool Star(Vector2 p, float outer, float inner)
        {
            float angle = Mathf.Atan2(p.x, p.y);                               // 0 = up
            float sector = Mathf.PI * 2f / 5f;
            float a = Mathf.Repeat(angle, sector) / sector;                     // 0..1 within a point
            float t = Mathf.Abs(a - 0.5f) * 2f;                                // 1 at a point, 0 between
            // The outline between a point (outer) and a notch (inner) is straight: interpolate in 1/r.
            float limit = 1f / Mathf.Lerp(1f / inner, 1f / outer, t);
            return p.magnitude <= limit;
        }
    }
}
