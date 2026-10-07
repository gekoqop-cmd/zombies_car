using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Characters;
using ZombiCar.Enemies;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Play-mode photos of the zombie clips and the death burst on Map01: the walker walking and attacking the car
    /// (close-ups), then a row of every zombie type killed by a hit from the left, photographed in slow motion while the
    /// pieces fly, land and lie in the blood. Output: Screenshots/Gib_*.png. Batch (without -quit, it exits by itself):
    /// Unity.exe -batchmode -projectPath &lt;unity&gt; -executeMethod ZombiCar.EditorTools.GibShots.Run
    /// </summary>
    [InitializeOnLoad]
    public static class GibShots
    {
        private const string c_StateKey = "ZombiCar.GibShots.State";    // 0 idle, 1 running, 2 finished
        private const int c_Width = 1920;
        private const int c_Height = 1080;
        private static readonly string[] s_Types = { "Walker", "Worker", "RedCap", "Hoodie", "Brute", "Hound" };

        private struct Step
        {
            public float Time;
            public Action Action;
        }

        private static readonly List<Step> s_Steps = new List<Step>();
        private static bool s_Started;
        private static float s_StartTime;
        private static int s_NextStep;
        private static int s_Errors;
        private static ZombieSpawner s_Spawner;
        private static CarHealth s_Car;
        private static readonly List<ZombieController> s_Row = new List<ZombieController>();
        private static ZombieController s_Walker;
        private static ZombieController s_Boss;

        static GibShots()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Zombie Death Screenshots")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SessionState.SetInt(c_StateKey, 1);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            int state = SessionState.GetInt(c_StateKey, 0);
            if (state == 2)
            {
                if (EditorApplication.isPlaying)
                    return;
                EditorApplication.update -= Tick;
                SessionState.SetInt(c_StateKey, 0);
                Debug.Log($"[GibShots] Finished, errors {s_Errors}.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(s_Errors == 0 ? 0 : 1);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
                Begin();
            float t = Time.time - s_StartTime;
            while (s_NextStep < s_Steps.Count && t >= s_Steps[s_NextStep].Time)
                s_Steps[s_NextStep++].Action();
        }

        private static void Begin()
        {
            s_Started = true;
            s_StartTime = Time.time;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    s_Errors++;
            };
            Object.FindAnyObjectByType<WaveDirector>().enabled = false;
            // Kills give experience: a level-up would open the card screen and pause the game.
            Object.FindAnyObjectByType<ZombiCar.Progression.PlayerProgression>().enabled = false;
            s_Spawner = Object.FindAnyObjectByType<ZombieSpawner>();
            s_Car = Object.FindAnyObjectByType<CarHealth>();
            BuildSteps();
            Debug.Log("[GibShots] Started.");
        }

        private static ZombieConfig Zombie(string name) =>
            AssetDatabase.LoadAssetAtPath<ZombieConfig>($"{EditorPaths.c_DataZombies}/Zombie_{name}.asset");

        private static void BuildSteps()
        {
            s_Steps.Clear();
            float t = 0.2f;
            void At(float time, Action action) => s_Steps.Add(new Step { Time = time, Action = action });
            Vector3 car = s_Car.transform.position;

            // The player's axe: the stance, both quick swings, the lift for the charged blow and the blow itself (slow motion).
            ZombiCar.Player.PlayerMelee melee = Object.FindAnyObjectByType<ZombiCar.Player.PlayerMelee>();
            Vector3 PlayerAt() => melee.transform.position + Vector3.up * 0.9f;
            At(t, () => Object.FindAnyObjectByType<ZombiCar.Player.WeaponSwitcher>().Switch(ZombiCar.Player.WeaponSlot.Axe));
            At(t += 0.5f, () => CloseUpAt("Axe_0_Stance", PlayerAt(), 420));
            foreach (string swing in new[] { "A", "B" })
            {
                string name = swing;
                At(t += 0.4f, () => { Time.timeScale = 0.25f; melee.Swing(); });
                At(t + 0.03f, () => CloseUpAt($"Axe_1{name}_Windup", PlayerAt(), 420));
                At(t + 0.07f, () => CloseUpAt($"Axe_2{name}_Strike", PlayerAt(), 420));
                At(t + 0.13f, () => CloseUpAt($"Axe_3{name}_Follow", PlayerAt(), 420));
                At(t += 0.3f, () => Time.timeScale = 1f);
                t += 0.5f;
            }
            At(t += 0.3f, () => melee.BeginCharge());
            At(t + 0.15f, () => CloseUpAt("Axe_4_Lifting", PlayerAt(), 420));
            At(t + 1f, () => CloseUpAt("Axe_5_Charged", PlayerAt(), 420));
            At(t += 1.1f, () => { Time.timeScale = 0.25f; melee.Release(); });
            At(t + 0.02f, () => CloseUpAt("Axe_6_Slam", PlayerAt(), 420));
            At(t + 0.06f, () => CloseUpAt("Axe_7_SlamFollow", PlayerAt(), 420));
            At(t += 0.2f, () => Time.timeScale = 1f);
            At(t += 0.3f, () => Object.FindAnyObjectByType<ZombiCar.Player.WeaponSwitcher>().Switch(ZombiCar.Player.WeaponSlot.Gun));
            t += 0.3f;

            // The walker walks in from the right to the car, then hits it.
            At(t, () => s_Walker = s_Spawner.SpawnAt(Zombie("Walker"), car + new Vector3(7f, 0f, -2.6f)));
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                At(t + 1.6f + 0.35f * i, () => CloseUpAt($"Gib_Walker_Move_{n}", s_Walker.transform.position + Vector3.up, 480));
            }
            for (int i = 0; i < 6; i++)
            {
                int n = i;
                At(t + 5f + 0.17f * i, () => CloseUpAt($"Gib_Walker_Attack_{n}_{s_Walker.Animator.Anim}", s_Walker.transform.position + Vector3.up, 480));
            }
            At(t += 6.5f, () => s_Walker.Kill(false));

            // A row of every type, then all killed by a hit from the left.
            Vector3 row = car + new Vector3(-9f, 0f, -6f);
            At(t += 0.5f, () =>
            {
                s_Row.Clear();
                for (int i = 0; i < s_Types.Length; i++)
                    s_Row.Add(s_Spawner.SpawnAt(Zombie(s_Types[i]), row + new Vector3(i * 2.6f, 0f, 0f)));
            });
            At(t += 1.2f, () => Wide("Gib_Row_0_Alive"));
            At(t += 0.05f, () =>
            {
                Time.timeScale = 0.25f;
                foreach (ZombieController z in s_Row)
                    if (z != null && z.IsAlive)
                        z.TakeDamage(10000, Vector3.right);
            });
            float[] moments = { 0.04f, 0.12f, 0.25f, 0.45f, 0.75f };      // game seconds after the hit
            for (int i = 0; i < moments.Length; i++)
            {
                int n = i;
                At(t + moments[i], () =>
                {
                    Wide($"Gib_Row_{n + 1}_Burst");
                    CloseUpAt($"Gib_Close_{n + 1}_Burst", row + new Vector3(2.6f, 0.8f, 0f), 640);
                });
            }
            At(t += 0.8f, () => Time.timeScale = 1f);
            At(t += 0.4f, () => Wide("Gib_Row_6_Lying"));
            At(t + 0.3f, () => CloseUpAt("Gib_Close_6_Lying", row + new Vector3(2.6f, 0.5f, 0f), 640));
            At(t += 1.6f, () => Wide("Gib_Row_7_Gone"));

            // Every boss near the player for a while: its walk, attack and abilities in its own clips, then its death.
            // The player stands the whole fight (a huge health bonus).
            At(t, () =>
            {
                var stats = (ZombiCar.Player.PlayerStats)typeof(ZombiCar.Player.PlayerHealth)
                    .GetField("m_Stats", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(Object.FindAnyObjectByType<ZombiCar.Player.PlayerHealth>());
                stats.SetBonusMaxHealth(1000000);
                stats.SetHealth(1000000);
            });
            for (int b = 1; b <= 3; b++)
            {
                int boss = b;
                At(t += 0.5f, () =>
                {
                    Transform player = Object.FindAnyObjectByType<ZombiCar.Player.PlayerHealth>().transform;
                    s_Boss = s_Spawner.SpawnAt(Zombie($"Boss{boss}"), player.position + new Vector3(7f, 0f, 1f));
                });
                for (int i = 0; i < 40; i++)
                {
                    int n = i;
                    At(t + 1f + 0.35f * i, () =>
                    {
                        if (s_Boss == null || !s_Boss.IsAlive)
                            return;
                        Object.FindAnyObjectByType<ZombiCar.Player.PlayerHealth>().Heal(1000000);
                        Object.FindAnyObjectByType<CarHealth>().Repair(10000);
                        CloseUpAt($"Boss{boss}_{n:00}_{s_Boss.Animator.Anim}", s_Boss.transform.position + Vector3.up * 1.6f, 640);
                    });
                }
                At(t += 15f, () => s_Boss.TakeDamage(100000, Vector3.right));
                At(t + 0.6f, () => CloseUpAt($"Boss{boss}_90_Dying", s_Boss.transform.position + Vector3.up * 1.2f, 640));
                At(t += 1.6f, () => CloseUpAt($"Boss{boss}_91_Dead", s_Boss.transform.position + Vector3.up * 1.2f, 640));
                t += 2.5f;
            }
            At(t + 0.3f, () =>
            {
                SessionState.SetInt(c_StateKey, 2);
                EditorApplication.ExitPlaymode();
            });
            s_Steps.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private static void Wide(string name)
        {
            string path = OutputPath(name);
            MapScreenshots.Render(Camera.main, c_Width, c_Height, path);
        }

        private static void CloseUpAt(string name, Vector3 world, int size)
        {
            string path = OutputPath(name);
            MapScreenshots.Render(Camera.main, c_Width, c_Height, path);
            Camera cam = Camera.main;
            cam.aspect = (float)c_Width / c_Height;
            Vector3 viewport = cam.WorldToViewportPoint(world);
            cam.ResetAspect();
            var full = new Texture2D(2, 2);
            full.LoadImage(File.ReadAllBytes(path));
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * c_Width) - size / 2, 0, c_Width - size);
            int y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * c_Height) - size / 2, 0, c_Height - size);
            var crop = new Texture2D(size, size, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x, y, size, size));
            crop.Apply();
            File.WriteAllBytes(path, crop.EncodeToPNG());
            Object.DestroyImmediate(full);
            Object.DestroyImmediate(crop);
        }

        private static string OutputPath(string name)
        {
            string folder = Path.GetFullPath("Screenshots");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, name + ".png");
        }
    }
}
