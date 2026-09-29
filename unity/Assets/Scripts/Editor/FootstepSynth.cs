using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The player's footsteps (Audio/SFX/Player_StepSoft1..4.wav), synthesised here so they can be tuned without the
    /// Python audio tools: a soft rubber-sole step on asphalt — a low heel thump, then the toe, with a little grit
    /// scuff — instead of the old hard clicks. Every variant has its own pitch, timing and grit so a run does not
    /// sound like one sample on a loop. Built by <see cref="PlayerBuilder"/> when missing (Tools/Setup to force).
    /// </summary>
    public static class FootstepSynth
    {
        public const string c_Prefix = "Player_StepSoft";
        public const int c_Variants = 4;
        private const string c_Folder = "Assets/Audio/SFX";
        private const int c_Rate = 44100;
        private const float c_Length = 0.16f;
        private const float c_Peak = 0.8f;

        [MenuItem("Tools/Setup/Build Footstep Sounds")]
        public static void BuildAll() => Build(true);

        /// <summary>Writes the variants (all of them when <paramref name="force"/>, otherwise only the missing ones).</summary>
        public static void Build(bool force)
        {
            bool any = false;
            for (int i = 1; i <= c_Variants; i++)
            {
                string path = $"{c_Folder}/{c_Prefix}{i}.wav";
                if (!force && File.Exists(path))
                    continue;
                WriteWav(path, Synthesise(i));
                any = true;
            }
            if (!any)
                return;
            AssetDatabase.Refresh();
            Debug.Log($"[FootstepSynth] {c_Variants} soft footsteps in {c_Folder}.");
        }

        private static float[] Synthesise(int variant)
        {
            var rnd = new System.Random(1000 + variant * 37);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            int n = Mathf.RoundToInt(c_Length * c_Rate);
            var s = new float[n];

            float pitch = R(0.88f, 1.12f);
            float toeAt = R(0.035f, 0.055f);              // heel -> toe
            float grit = R(0.18f, 0.32f);
            float lowState = 0f, highState = 0f;
            float phaseHeel = 0f, phaseToe = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)c_Rate;
                // Heel: a soft sine thump sliding down from ~120 Hz, 2 ms attack, fast decay.
                float heelEnv = Attack(t, 0.002f) * Mathf.Exp(-t / 0.022f);
                phaseHeel += 2f * Mathf.PI * Mathf.Lerp(120f, 55f, Mathf.Clamp01(t / 0.05f)) * pitch / c_Rate;
                float heel = Mathf.Sin(phaseHeel) * heelEnv * 0.6f;
                // Toe: a lighter, slightly higher thump a moment later.
                float tt = t - toeAt;
                float toeEnv = tt < 0f ? 0f : Attack(tt, 0.002f) * Mathf.Exp(-tt / 0.018f);
                phaseToe += tt < 0f ? 0f : 2f * Mathf.PI * 150f * pitch / c_Rate;
                float toe = Mathf.Sin(phaseToe) * toeEnv * 0.35f;
                // Grit: band-limited noise (a one-pole low-pass at ~3 kHz minus one at ~350 Hz) with both hits.
                float noise = R(-1f, 1f);
                lowState += (noise - lowState) * 0.35f;              // ~3 kHz
                highState += (lowState - highState) * 0.05f;         // ~350 Hz
                float band = lowState - highState;
                float gritEnv = Attack(t, 0.003f) * Mathf.Exp(-t / 0.03f) + (tt < 0f ? 0f : Mathf.Exp(-tt / 0.025f) * 0.7f);
                float scuff = band * gritEnv * grit;
                s[i] = heel + toe + scuff;
            }
            // Normalise, fade out the tail.
            float peak = 1e-5f;
            foreach (float v in s)
                peak = Mathf.Max(peak, Mathf.Abs(v));
            int fade = Mathf.RoundToInt(0.012f * c_Rate);
            for (int i = 0; i < n; i++)
            {
                float k = c_Peak / peak;
                if (i > n - fade)
                    k *= (n - i) / (float)fade;
                s[i] *= k;
            }
            return s;
        }

        private static float Attack(float t, float time) => Mathf.Clamp01(t / time);

        private static void WriteWav(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);                // PCM
            w.Write((short)1);                // mono
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
