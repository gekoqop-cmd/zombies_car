using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Paints the UI pictures that have no art source: UI_Icon_Ad (a TV screen with a play mark on a gold disc —
    /// every "reward for an ad" button uses it), UI_Icon_Coin (the coins for the cases), the records' stopwatch, trophy
    /// and flag, and white shapes tinted in the game (UI_Rays behind a case prize, UI_Spark for fireworks). Drawn with
    /// antialiased shapes (4×4 supersampling), written once to Art/Textures/UI (the import settings come from
    /// <see cref="ArtImportPostprocessor"/>); delete a PNG to paint it again.
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
            Paint("UI_Rays", Rays, force);
            Paint("UI_Icon_Stopwatch", StopwatchIcon, force);
            Paint("UI_Icon_Trophy", TrophyIcon, force);
            Paint("UI_Icon_Flag", FlagIcon, force);
            Paint("UI_Spark", Spark, force);
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

        // White light rays fading out from the centre (the case prize's burst, tinted by rarity in the game).
        private static Color Rays(Vector2 p)
        {
            float r = p.magnitude;
            if (r >= 1f)
                return Color.clear;
            float angle = Mathf.Atan2(p.y, p.x);
            float ray = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.92f, Mathf.Cos(angle * 12f) * 0.5f + 0.5f));
            float fade = Mathf.Pow(1f - r, 1.3f);
            float core = Mathf.Pow(Mathf.Clamp01(1f - r / 0.35f), 2f) * 0.6f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(ray * fade * 0.95f + core));
        }

        // A white spark with a hot core and a soft edge (fireworks: tinted in the game).
        private static Color Spark(Vector2 p)
        {
            float r = p.magnitude;
            if (r >= 1f)
                return Color.clear;
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a * a * 1.4f + (r < 0.35f ? 0.6f : 0f)));
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

        // ---------------------------------------------------------------- record icons (the map records window)
        private static readonly Color s_Ink = new Color(0.12f, 0.08f, 0.06f, 1f);

        // A dark outline around whatever the fill draws, like the painted game icons.
        private static Color Outlined(Vector2 p, Func<Vector2, Color> fill)
        {
            Color c = fill(p);
            if (c.a > 0f)
                return c;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                if (fill(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.07f).a > 0f)
                    return s_Ink;
            }
            return Color.clear;
        }

        private static Color Gold(Vector2 p) => Color.Lerp(s_GoldBottom, s_GoldTop, Mathf.Clamp01(p.y * 0.7f + 0.5f));

        private static Color StopwatchIcon(Vector2 p) => Outlined(p, q =>
        {
            Vector2 c = q - new Vector2(0f, -0.1f);
            if (RoundBox(q - new Vector2(0f, 0.66f), new Vector2(0.1f, 0.09f), 0.03f) <= 0f ||
                RoundBox(q - new Vector2(0f, 0.8f), new Vector2(0.2f, 0.06f), 0.05f) <= 0f)
                return Gold(q);
            float r = c.magnitude;
            if (r > 0.64f)
                return Color.clear;
            if (r > 0.52f)
                return Gold(q);
            if (Segment(c, Vector2.zero, new Vector2(0f, 0.36f)) < 0.045f || Segment(c, Vector2.zero, new Vector2(0.24f, -0.08f)) < 0.045f)
                return s_Ink;
            if (Segment(c, new Vector2(0f, 0.4f), new Vector2(0f, 0.48f)) < 0.03f)
                return new Color(0.85f, 0.2f, 0.12f, 1f);
            return Color.Lerp(new Color(0.86f, 0.84f, 0.78f, 1f), new Color(1f, 0.99f, 0.95f, 1f), Mathf.Clamp01(c.y + 0.5f));
        });

        private static Color TrophyIcon(Vector2 p) => Outlined(p, q =>
        {
            // Bowl: widens upwards; handles: rings on both sides; then the stem and the base.
            bool bowl = q.y >= -0.08f && q.y <= 0.66f && Mathf.Abs(q.x) <= 0.16f + 0.3f * Mathf.Sqrt(Mathf.Clamp01((q.y + 0.08f) / 0.74f));
            float handle = Mathf.Min((new Vector2(Mathf.Abs(q.x), q.y) - new Vector2(0.5f, 0.34f)).magnitude, 1f);
            bool ring = handle > 0.11f && handle < 0.19f && Mathf.Abs(q.x) > 0.4f;
            bool stem = Mathf.Abs(q.x) < 0.07f && q.y > -0.42f && q.y < -0.05f;
            bool baseBox = RoundBox(q - new Vector2(0f, -0.56f), new Vector2(0.34f, 0.12f), 0.04f) <= 0f;
            if (!(bowl || ring || stem || baseBox))
                return Color.clear;
            if (baseBox)
                return new Color(0.45f, 0.25f, 0.12f, 1f);
            Color c = Gold(q);
            if (bowl && Star(q - new Vector2(0f, 0.3f), 0.17f, 0.07f))
                c = Color.Lerp(c, new Color(1f, 0.98f, 0.8f, 1f), 0.8f);
            return c;
        });

        private static Color FlagIcon(Vector2 p) => Outlined(p, q =>
        {
            if (Segment(q, new Vector2(-0.42f, -0.72f), new Vector2(-0.42f, 0.74f)) < 0.05f)
                return new Color(0.5f, 0.32f, 0.18f, 1f);
            float wave = 0.07f * Mathf.Sin((q.x + 0.4f) * 6f);
            if (q.x > -0.38f && q.x < 0.6f && q.y < 0.7f + wave && q.y > 0.12f + wave + (q.x + 0.38f) * 0.05f)
                return Color.Lerp(new Color(0.7f, 0.1f, 0.08f, 1f), new Color(0.95f, 0.25f, 0.18f, 1f), Mathf.Clamp01(q.y));
            if (RoundBox(q - new Vector2(-0.42f, -0.76f), new Vector2(0.26f, 0.08f), 0.06f) <= 0f)
                return new Color(0.42f, 0.42f, 0.46f, 1f);
            return Color.clear;
        });

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
