using System;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Batch entry points (unity run ... -executeMethod ZombiCar.EditorTools.ProjectBuild.BuildAll).
    /// BuildAll rebuilds everything that is generated from the art: project settings, font, data assets, materials,
    /// prefabs, scenes, then captures Map01 screenshots. Every step is also available from the Tools menu.
    /// </summary>
    public static class ProjectBuild
    {
        private const string c_WebBuildFolder = "Builds/WebGL";

        [MenuItem("Tools/Build Everything (Settings, Prefabs, Scenes, Screenshots)")]
        public static void BuildAll()
        {
            Step("Configure project", ProjectConfigurator.Run);
            Step("TMP font", () => TmpSetup.EnsureFont());
            Step("Data assets", DataAssetsBuilder.Run);
            Step("Materials", MaterialLibrary.Run);
            Step("Prefabs", PrefabBuilder.BuildAll);
            Step("Scenes", SceneBuilder.BuildAll);
            AssetDatabase.SaveAssets();
            Step("Validate scenes", () => SceneValidator.ValidateBuildScenes());
            Step("Screenshots", MapScreenshots.CaptureAll);
            Debug.Log("[ProjectBuild] BuildAll finished.");
        }

        /// <summary>Rebuilds only prefabs + scenes + screenshots (after art or layout tweaks).</summary>
        public static void RebuildScenes()
        {
            Step("Prefabs", PrefabBuilder.BuildAll);
            Step("Scenes", SceneBuilder.BuildAll);
            AssetDatabase.SaveAssets();
            Step("Validate scenes", () => SceneValidator.ValidateBuildScenes());
            Step("Screenshots", MapScreenshots.CaptureAll);
        }

        /// <summary>Release WebGL player build of the scenes in Build Settings into Builds/WebGL.</summary>
        [MenuItem("Tools/Build WebGL Player")]
        public static void BuildWebGL()
        {
            WebOptimizer.Optimize();
            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
                locationPathName = c_WebBuildFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[ProjectBuild] WebGL build {summary.result}: {summary.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{summary.totalTime.TotalMinutes:F1} min, errors {summary.totalErrors}, warnings {summary.totalWarnings}");
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build {summary.result}");
        }

        private static void Step(string name, Action action)
        {
            var timer = Stopwatch.StartNew();
            Debug.Log($"[ProjectBuild] >>> {name}");
            action();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProjectBuild] <<< {name} ({timer.Elapsed.TotalSeconds:F1} s)");
        }
    }
}
