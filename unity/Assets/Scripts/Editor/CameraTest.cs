using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Waves;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Play-mode camera check of Map01: a bot runs right, turns sharply to the left, then stops dead, once per
    /// camera setting in <see cref="s_Variants"/> (applied to the CameraConfig asset at run time, restored at the
    /// end). Every frame logs the player and the camera's look point (where the view centre meets the ground);
    /// per variant it reports how much faster than the player the camera moves, how far it overshoots / slides after
    /// the stop and the largest per-frame jolt, and saves 960x540 frames of the variants marked Record to
    /// Screenshots/CameraTest/&lt;variant&gt;/ for GIFs. Tools/Diagnostics/Camera Test (live editor or batch without -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class CameraTest
    {
        private const string c_StateKey = "ZombiCar.CameraTest.State";
        private const string c_Folder = "Screenshots/CameraTest";
        private const int c_Width = 960;
        private const int c_Height = 540;
        private const float c_Settle = 0.6f;
        private const float c_RunRight = 1.8f;
        private const float c_RunLeft = 1.2f;
        private const float c_Stop = 1.6f;
        private const float c_FrameInterval = 0.1f;
        private static readonly Vector3 s_Start = new Vector3(-8f, 0f, -6f);

        public sealed class Variant
        {
            public string Name;
            public Vector2 Damping;
            public float LookAheadTime, LookAheadMax, DeadZone, AimOffset, AimOffsetMax, MaxLead, LeadSmooth;
            public bool Record;
        }

        // "Old" reproduces the previous defaults (no dead zone, no total cap).
        private static readonly Variant[] s_Variants =
        {
            new Variant { Name = "Old", Damping = new Vector2(0.45f, 0.45f), LookAheadTime = 0.35f, LookAheadMax = 2.2f, DeadZone = 0f,
                AimOffset = 0.2f, AimOffsetMax = 2.5f, MaxLead = 10f, LeadSmooth = 0.45f, Record = true },
            new Variant { Name = "A", Damping = new Vector2(0.25f, 0.25f), LookAheadTime = 0.16f, LookAheadMax = 1f, DeadZone = 1f,
                AimOffset = 0.12f, AimOffsetMax = 1.2f, MaxLead = 1.6f, LeadSmooth = 0.3f },
            new Variant { Name = "B", Damping = new Vector2(0.2f, 0.2f), LookAheadTime = 0.12f, LookAheadMax = 0.7f, DeadZone = 1f,
                AimOffset = 0.12f, AimOffsetMax = 1.2f, MaxLead = 1.4f, LeadSmooth = 0.3f },
            new Variant { Name = "C", Damping = new Vector2(0.3f, 0.3f), LookAheadTime = 0.2f, LookAheadMax = 1.2f, DeadZone = 1f,
                AimOffset = 0.12f, AimOffsetMax = 1.2f, MaxLead = 1.6f, LeadSmooth = 0.4f },
            new Variant { Name = "D", Damping = new Vector2(0.22f, 0.22f), LookAheadTime = 0.14f, LookAheadMax = 0.9f, DeadZone = 1f,
                AimOffset = 0.12f, AimOffsetMax = 1.2f, MaxLead = 1.5f, LeadSmooth = 0.35f },
        };

        // Command line: -cameraVariants Old,B (default all) and -cameraRecord B (frames also for these).
        private static string[] Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : null;
        }

        private sealed class Bot : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public Vector3 AimDirection => Vector3.right;
            public bool AimActive => false;
            public bool FireHeld => false;
            public bool FirePressed => false;
            public bool FireReleased => false;
            public bool QuickTap => false;
            public bool ReloadPressed => false;
            public bool InteractPressed => false;
            public bool SwitchWeaponPressed => false;
            public int WeaponSlotPressed => -1;

            public bool TryGetAimRay(out Ray ray)
            {
                ray = default;
                return false;
            }
        }

        private struct Sample
        {
            public float T;
            public Vector3 Player;
            public Vector3 Look;
        }

        private static bool s_Started;
        private static int s_Variant;
        private static float s_VariantStart;
        private static float s_NextFrame;
        private static int s_FrameIndex;
        private static Bot s_Bot;
        private static Transform s_Player;
        private static CharacterController s_Controller;
        private static CameraFollowTarget s_Target;
        private static CinemachineCamera s_Vcam;
        private static CameraConfig s_Config;
        private static string s_Backup;
        private static List<Variant> s_Run;
        private static readonly List<Sample> s_Samples = new List<Sample>();
        private static readonly StringBuilder s_Report = new StringBuilder();

        static CameraTest()
        {
            if (SessionState.GetInt(c_StateKey, 0) != 0)
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Diagnostics/Camera Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SessionState.SetInt(c_StateKey, 1);
            s_Started = false;
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
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
                return;
            }
            if (state != 1 || !EditorApplication.isPlaying || Time.frameCount < 5)
                return;
            if (!s_Started)
                Begin();
            Step();
        }

        private static void Begin()
        {
            s_Started = true;
            Object.FindAnyObjectByType<WaveDirector>().enabled = false;
            var weapon = Object.FindAnyObjectByType<PlayerWeapon>();
            s_Player = weapon.transform;
            s_Controller = s_Player.GetComponent<CharacterController>();
            s_Target = Object.FindAnyObjectByType<CameraFollowTarget>();
            s_Vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            s_Config = AssetDatabase.LoadAssetAtPath<CameraConfig>(DataAssetsBuilder.c_CameraConfig);
            s_Backup = EditorJsonUtility.ToJson(s_Config);

            s_Bot = new Bot();
            var provider = s_Player.GetComponent<PlayerInputProvider>();
            typeof(PlayerInputProvider).GetProperty(nameof(PlayerInputProvider.Input))?.GetSetMethod(true)?.Invoke(provider, new object[] { s_Bot });
            foreach (MonoBehaviour behaviour in s_Player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                FieldInfo field = behaviour.GetType().GetField("m_Input", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(IPlayerInput))
                    field.SetValue(behaviour, s_Bot);
            }
            string[] only = Argument("-cameraVariants");
            string[] record = Argument("-cameraRecord") ?? Array.Empty<string>();
            s_Run = s_Variants.Where(v => only == null || only.Contains(v.Name)).ToList();
            foreach (Variant v in s_Run)
                v.Record |= record.Contains(v.Name);
            s_Report.Clear();
            s_Report.AppendLine("[CameraTest] variant | cam/player speed max (run, turn) | lead max m | stop: slide m, overshoot m, settle s | max jolt m/s²");
            s_Variant = -1;
            NextVariant();
        }

        private static void NextVariant()
        {
            s_Variant++;
            if (s_Variant >= s_Run.Count)
            {
                Finish();
                return;
            }
            Variant v = s_Run[s_Variant];
            Apply(v);
            s_Bot.Move = Vector2.zero;
            s_Controller.enabled = false;
            s_Player.position = s_Start;
            s_Controller.enabled = true;
            s_Target.Snap();
            s_Vcam.PreviousStateIsValid = false;
            s_Samples.Clear();
            s_VariantStart = Time.time;
            s_NextFrame = 0f;
            s_FrameIndex = 0;
        }

        private static void Apply(Variant v)
        {
            var so = new SerializedObject(s_Config);
            so.FindProperty("m_Damping").vector2Value = v.Damping;
            so.FindProperty("m_LookAheadTime").floatValue = v.LookAheadTime;
            so.FindProperty("m_LookAheadMax").floatValue = v.LookAheadMax;
            so.FindProperty("m_LookAheadDeadZone").floatValue = v.DeadZone;
            so.FindProperty("m_AimOffset").floatValue = v.AimOffset;
            so.FindProperty("m_AimOffsetMax").floatValue = v.AimOffsetMax;
            so.FindProperty("m_MaxLead").floatValue = v.MaxLead;
            so.FindProperty("m_LeadSmoothTime").floatValue = v.LeadSmooth;
            so.ApplyModifiedPropertiesWithoutUndo();
            s_Target.ApplyDamping();
        }

        private static void Step()
        {
            if (s_Variant >= s_Run.Count)
                return;
            Variant v = s_Run[s_Variant];
            float t = Time.time - s_VariantStart;
            float runStart = c_Settle, turn = runStart + c_RunRight, stop = turn + c_RunLeft, end = stop + c_Stop;
            s_Bot.Move = t < runStart ? Vector2.zero : t < turn ? Vector2.right : t < stop ? Vector2.left : Vector2.zero;

            Camera cam = Camera.main;
            s_Samples.Add(new Sample { T = t, Player = s_Player.position, Look = LookPoint(cam) });

            if (v.Record && t >= runStart - 0.2f && t >= s_NextFrame)
            {
                s_NextFrame = t + c_FrameInterval;
                string folder = Path.GetFullPath(Path.Combine(c_Folder, v.Name));
                Directory.CreateDirectory(folder);
                string phase = t < turn ? "run" : t < stop ? "turn" : "stop";
                MapScreenshots.Render(cam, c_Width, c_Height,
                    Path.Combine(folder, $"f{s_FrameIndex++:000}_{phase}_{t.ToString("F2", CultureInfo.InvariantCulture)}s.png"));
            }
            if (t >= end)
            {
                Analyse(v, runStart, turn, stop);
                NextVariant();
            }
        }

        // Where the centre of the view meets the ground plane.
        private static Vector3 LookPoint(Camera cam)
        {
            Transform c = cam.transform;
            float k = -c.position.y / c.forward.y;
            return c.position + c.forward * k;
        }

        private static void Analyse(Variant v, float runStart, float turn, float stop)
        {
            float maxRatioRun = 0f, maxRatioTurn = 0f, maxLead = 0f, maxJolt = 0f;
            Vector3 prevLookVel = Vector3.zero;
            for (int i = 1; i < s_Samples.Count; i++)
            {
                Sample a = s_Samples[i - 1], b = s_Samples[i];
                float dt = b.T - a.T;
                if (dt <= 1e-4f)
                    continue;
                float playerSpeed = Mathf.Abs(b.Player.x - a.Player.x) / dt;
                float lookSpeed = Mathf.Abs(b.Look.x - a.Look.x) / dt;
                Vector3 lookVel = (b.Look - a.Look) / dt;
                if (i > 1)
                    maxJolt = Mathf.Max(maxJolt, ((lookVel - prevLookVel) / dt).magnitude);
                prevLookVel = lookVel;
                if (playerSpeed > 3f)
                {
                    float ratio = lookSpeed / playerSpeed;
                    if (b.T >= runStart && b.T < turn) maxRatioRun = Mathf.Max(maxRatioRun, ratio);
                    else if (b.T >= turn && b.T < stop) maxRatioTurn = Mathf.Max(maxRatioTurn, ratio);
                }
                maxLead = Mathf.Max(maxLead, Mathf.Abs(b.Look.x - b.Player.x));
            }
            // After the stop: how far the camera keeps moving, how far past its rest point it goes, when it settles.
            List<Sample> after = s_Samples.Where(s => s.T >= stop).ToList();
            float rest = after.Last().Look.x;
            float startX = after.First().Look.x;
            float slide = Mathf.Abs(rest - startX);
            float overshoot = 0f, settle = 0f;
            float dir = Mathf.Sign(rest - startX);
            foreach (Sample s in after)
            {
                overshoot = Mathf.Max(overshoot, (s.Look.x - rest) * dir);
                if (Mathf.Abs(s.Look.x - rest) > 0.05f)
                    settle = s.T - stop;
            }
            string line = string.Format(CultureInfo.InvariantCulture,
                "  {0,-4} damp {1:F2} la {2:F2}/{3:F1} smooth {4:F2} | {5:F2}, {6:F2} | {7:F2} | {8:F2}, {9:F2}, {10:F2} | {11:F0}",
                v.Name, v.Damping.x, v.LookAheadTime, v.LookAheadMax, v.LeadSmooth, maxRatioRun, maxRatioTurn, maxLead,
                slide, overshoot, settle, maxJolt);
            s_Report.AppendLine(line);

            string csv = Path.GetFullPath(Path.Combine(c_Folder, $"{v.Name}.csv"));
            Directory.CreateDirectory(Path.GetDirectoryName(csv));
            File.WriteAllLines(csv, new[] { "t,player_x,look_x" }.Concat(s_Samples.Select(s =>
                string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", s.T, s.Player.x, s.Look.x))));
        }

        private static void Finish()
        {
            EditorJsonUtility.FromJsonOverwrite(s_Backup, s_Config);   // the asset keeps its own values
            EditorUtility.SetDirty(s_Config);
            AssetDatabase.SaveAssets();
            string report = s_Report.ToString();
            Debug.Log(report);
            File.WriteAllText(Path.GetFullPath(Path.Combine(c_Folder, "report.txt")), report);
            SessionState.SetInt(c_StateKey, 2);
            EditorApplication.ExitPlaymode();
        }
    }
}
