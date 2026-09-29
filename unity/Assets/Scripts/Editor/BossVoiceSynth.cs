using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Each boss's own voice (Audio/SFX/Boss&lt;N&gt;_Roar / Attack / Death / Special.wav), synthesised here so the three
    /// sound clearly apart:
    /// <list type="bullet">
    /// <item>Matka — wet and bubbling: a gurgling mid-pitched roar, a squelching slap, a drowning death gurgle, the
    /// brood's call (sacs popping);</item>
    /// <item>Taran — iron and bass: a low growl with a metal ring, a heavy clang, a collapse of clattering metal, the
    /// hiss of the acid rain spraying up;</item>
    /// <item>Zhnets (the Reaper) — deep and burning: a sub-bass demonic roar with fire crackle, a blade whoosh, a
    /// long falling groan fading into embers, a whoosh of fire for the reap.</item>
    /// </list>
    /// Mono, 44.1 kHz; imported as short effects (Vorbis, decompressed on load) by ArtImportPostprocessor. Built by
    /// CharacterBuilder when missing (Tools/Setup/Build Boss Voices to force).
    /// </summary>
    public static class BossVoiceSynth
    {
        public const string c_Folder = "Assets/Audio/SFX";
        private const int c_Rate = 44100;
        private static readonly string[] s_Kinds = { "Roar", "Attack", "Death", "Special" };

        public static string PathOf(int boss, string kind) => $"{c_Folder}/Boss{boss}_{kind}.wav";

        [MenuItem("Tools/Setup/Build Boss Voices")]
        public static void BuildAll() => Build(true);

        public static void Build(bool force)
        {
            bool any = false;
            for (int boss = 1; boss <= 3; boss++)
            {
                foreach (string kind in s_Kinds)
                {
                    string path = PathOf(boss, kind);
                    if (!force && File.Exists(path))
                        continue;
                    WriteWav(path, Normalize(Synth(boss, kind)));
                    any = true;
                }
            }
            if (!any)
                return;
            AssetDatabase.Refresh();
            Debug.Log("[BossVoiceSynth] boss voices written to " + c_Folder);
        }

        public static AudioClip Clip(int boss, string kind) => AssetDatabase.LoadAssetAtPath<AudioClip>(PathOf(boss, kind));

        // ------------------------------------------------------------------ voices
        private static float[] Synth(int boss, string kind)
        {
            var rnd = new System.Random(boss * 100 + Array.IndexOf(s_Kinds, kind));
            switch (boss)
            {
                case 1:
                    switch (kind)
                    {
                        case "Roar": return Growl(rnd, 1.3f, 190f, 120f, 0.55f, 18f, 0.5f, wet: true);
                        case "Attack": return Mix(Squelch(rnd, 0.45f), Thump(0.35f, 70f, 0.8f));
                        case "Death": return Growl(rnd, 2.2f, 170f, 60f, 0.5f, 11f, 0.6f, wet: true);
                        default: return Mix(Bubbles(rnd, 1.4f, 18), Growl(rnd, 1.4f, 240f, 200f, 0.3f, 22f, 0.4f, wet: true), 0.8f, 0.5f);
                    }
                case 2:
                    switch (kind)
                    {
                        case "Roar": return Mix(Growl(rnd, 1.4f, 85f, 70f, 0.7f, 7f, 0.25f, wet: false), MetalRing(rnd, 1.4f, 180f, 0.35f), 1f, 0.45f);
                        case "Attack": return Mix(MetalRing(rnd, 0.9f, 140f, 0.9f), Thump(0.5f, 55f, 1f));
                        case "Death": return Mix(Growl(rnd, 2.2f, 75f, 40f, 0.6f, 5f, 0.3f, wet: false), Clatter(rnd, 2.2f), 1f, 0.7f);
                        default: return Mix(Hiss(rnd, 1.3f, 0.25f, 0.7f), Growl(rnd, 1.3f, 90f, 80f, 0.4f, 6f, 0.2f, wet: false), 0.9f, 0.4f);
                    }
                default:
                    switch (kind)
                    {
                        case "Roar": return Mix(Growl(rnd, 1.8f, 55f, 42f, 0.85f, 4f, 0.2f, wet: false), Crackle(rnd, 1.8f), 1f, 0.5f);
                        case "Attack": return Mix(Whoosh(rnd, 0.55f, 900f, 3500f), Thump(0.3f, 90f, 0.5f), 1f, 0.6f);
                        case "Death": return Mix(Growl(rnd, 3f, 60f, 25f, 0.8f, 3f, 0.2f, wet: false), Crackle(rnd, 3f), 1f, 0.6f);
                        default: return Mix(Whoosh(rnd, 1.1f, 250f, 2200f), Crackle(rnd, 1.1f), 1f, 0.7f);
                    }
            }
        }

        // A roar: a sawtooth voice gliding from f0 to f1, rough (noise, jitter), with a tremolo, through a band-pass
        // formant; "wet" adds a bubbling gurgle.
        private static float[] Growl(System.Random rnd, float seconds, float f0, float f1, float rough, float tremolo,
            float tremoloDepth, bool wet)
        {
            int n = Samples(seconds);
            var s = new float[n];
            float phase = 0f, lp = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate, k = t / seconds;
                float env = Attack(t, 0.08f) * Mathf.Pow(1f - k, 0.6f);
                float f = Mathf.Lerp(f0, f1, k) * (1f + (float)(rnd.NextDouble() - 0.5) * 0.08f * rough);
                phase += f / c_Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float voice = saw * (1f - rough * 0.5f) + noise * rough * 0.6f;
                float trem = 1f - tremoloDepth * (0.5f + 0.5f * Mathf.Sin(t * tremolo * Mathf.PI * 2f));
                if (wet)
                    trem *= 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 37f + Mathf.Sin(t * 13f) * 4f));
                // Formant-ish filter: a low-pass then a gentle high-pass.
                lp += (voice - lp) * 0.18f;
                bp += (lp - bp) * 0.02f;
                s[i] = (lp - bp) * env * trem;
            }
            return s;
        }

        private static float[] Thump(float seconds, float freq, float level)
        {
            int n = Samples(seconds);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate;
                phase += Mathf.Lerp(freq * 1.8f, freq, Mathf.Clamp01(t / 0.08f)) / c_Rate;
                s[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t / 0.09f) * Attack(t, 0.003f) * level;
            }
            return s;
        }

        // Struck iron: inharmonic partials ringing down.
        private static float[] MetalRing(System.Random rnd, float seconds, float fundamental, float level)
        {
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f, 13.3f };
            int n = Samples(seconds);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate, v = 0f;
                for (int p = 0; p < ratios.Length; p++)
                    v += Mathf.Sin(t * fundamental * ratios[p] * Mathf.PI * 2f) * Mathf.Exp(-t * (2.5f + p * 2.2f)) / (1f + p);
                float hit = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / 0.012f);
                s[i] = (v * 0.7f + hit * 0.5f) * Attack(t, 0.002f) * level;
            }
            return s;
        }

        private static float[] Clatter(System.Random rnd, float seconds)
        {
            int n = Samples(seconds);
            var s = new float[n];
            for (int hit = 0; hit < 7; hit++)
            {
                float at = (float)rnd.NextDouble() * seconds * 0.7f + 0.2f;
                float[] ring = MetalRing(rnd, 0.5f, 200f + (float)rnd.NextDouble() * 300f, 0.4f + (float)rnd.NextDouble() * 0.4f);
                Add(s, ring, Samples(at));
            }
            return s;
        }

        // Wet slap: a noise burst through a falling low-pass.
        private static float[] Squelch(System.Random rnd, float seconds)
        {
            int n = Samples(seconds);
            var s = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate;
                float cut = Mathf.Lerp(0.5f, 0.03f, Mathf.Clamp01(t / seconds));
                lp += ((float)(rnd.NextDouble() * 2.0 - 1.0) - lp) * cut;
                s[i] = lp * Mathf.Exp(-t / 0.12f) * Attack(t, 0.004f) * (0.8f + 0.2f * Mathf.Sin(t * 180f));
            }
            return s;
        }

        private static float[] Bubbles(System.Random rnd, float seconds, int count)
        {
            int n = Samples(seconds);
            var s = new float[n];
            for (int b = 0; b < count; b++)
            {
                int start = Samples((float)rnd.NextDouble() * seconds * 0.85f);
                float f = 300f + (float)rnd.NextDouble() * 700f;
                int len = Samples(0.06f + (float)rnd.NextDouble() * 0.08f);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)c_Rate;
                    s[start + i] += Mathf.Sin(t * f * (1f + t * 12f) * Mathf.PI * 2f) * Mathf.Exp(-t / 0.03f) * 0.6f;
                }
            }
            return s;
        }

        private static float[] Hiss(System.Random rnd, float seconds, float rise, float level)
        {
            int n = Samples(seconds);
            var s = new float[n];
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate, k = t / seconds;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                hp = 0.92f * (hp + noise - prev);
                prev = noise;
                s[i] = hp * Attack(t, rise) * Mathf.Pow(1f - k, 1.5f) * level;
            }
            return s;
        }

        private static float[] Crackle(System.Random rnd, float seconds)
        {
            int n = Samples(seconds);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (rnd.NextDouble() > 0.0025)
                    continue;
                float amp = 0.3f + (float)rnd.NextDouble() * 0.7f;
                for (int j = 0; j < 90 && i + j < n; j++)
                    s[i + j] += (float)(rnd.NextDouble() * 2.0 - 1.0) * amp * Mathf.Exp(-j / 18f);
            }
            float k0 = 1f;
            for (int i = 0; i < n; i++)
                s[i] *= k0 * (1f - i / (float)n * 0.6f);
            return s;
        }

        // A whoosh: noise through a band-pass sweeping from f0 to f1 and back, swelling and fading.
        private static float[] Whoosh(System.Random rnd, float seconds, float f0, float f1)
        {
            int n = Samples(seconds);
            var s = new float[n];
            float low = 0f, band = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate, k = t / seconds;
                float f = Mathf.Lerp(f0, f1, Mathf.Sin(k * Mathf.PI));
                float c = 2f * Mathf.Sin(Mathf.PI * f / c_Rate);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                low += c * band;
                float high = noise - low - 0.5f * band;
                band += c * high;
                s[i] = band * Mathf.Sin(k * Mathf.PI);
            }
            return s;
        }

        // ------------------------------------------------------------------ helpers
        private static int Samples(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * c_Rate));

        private static float Attack(float t, float time) => Mathf.Clamp01(t / time);

        private static float[] Mix(float[] a, float[] b, float ka = 1f, float kb = 1f)
        {
            var s = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < s.Length; i++)
                s[i] = (i < a.Length ? a[i] * ka : 0f) + (i < b.Length ? b[i] * kb : 0f);
            return s;
        }

        private static void Add(float[] into, float[] what, int at)
        {
            for (int i = 0; i < what.Length && at + i < into.Length; i++)
                into[at + i] += what[i];
        }

        // Peak at -1 dBFS with a short fade at both ends (no clicks).
        private static float[] Normalize(float[] s)
        {
            float peak = 1e-5f;
            foreach (float v in s)
                peak = Mathf.Max(peak, Mathf.Abs(v));
            float k = 0.89f / peak;
            int fade = Mathf.Min(s.Length / 4, Samples(0.01f));
            for (int i = 0; i < s.Length; i++)
            {
                float edge = Mathf.Min(1f, Mathf.Min(i, s.Length - 1 - i) / (float)Mathf.Max(1, fade));
                s[i] *= k * edge;
            }
            return s;
        }

        private static void WriteWav(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(c_Rate);
            w.Write(c_Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float v in samples)
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * short.MaxValue));
        }
    }
}
