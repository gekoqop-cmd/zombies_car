using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombiCar.Core;
using ZombiCar.Player;
using Debug = UnityEngine.Debug;
using SettingsService = ZombiCar.Core.SettingsService;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Diagnostics/Compare Quality Tiers: renders Map01 from the same game-camera views on High and Ultra
    /// (Screenshots/Quality_&lt;view&gt;_&lt;tier&gt;.png) and times every tier: frames rendered at 1920x1080 with a GPU
    /// sync after each frame, so the numbers are relative costs on this machine (the Editor on D3D11), not browser FPS.
    /// </summary>
    public static class QualityCompare
    {
        private const string c_Output = "Screenshots";
        private const int c_Width = 1920;
        private const int c_Height = 1080;
        private const int c_WarmupFrames = 8;
        private const int c_TimedFrames = 60;
        private const float c_DustSimulation = 10f;
        private const float c_SparkSimulation = 0.07f;

        private static readonly (string name, Vector3 lookAt)[] s_Views =
        {
            ("Workshop", new Vector3(-7f, 0f, 3f)),
            ("Car", new Vector3(0f, 0f, -1.5f)),
            ("NorthBackground", new Vector3(15f, 0f, 13f)),     // the trees beyond the north road
        };

        private static readonly QualityTier[] s_ShotTiers = { QualityTier.High, QualityTier.Ultra };

        [MenuItem("Tools/Diagnostics/Compare Quality Tiers")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene scene = EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            DynamicGI.UpdateEnvironment();
            Camera cam = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
            cam.fieldOfView = ViewConstants.c_CameraFov;
            string folder = Path.GetFullPath(c_Output);
            Directory.CreateDirectory(folder);
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;

            foreach ((string view, Vector3 lookAt) in s_Views)
            {
                PlaceCamera(cam, lookAt);
                foreach (QualityTier tier in s_ShotTiers)
                {
                    Apply(scene, tier);
                    string path = Path.Combine(folder, $"Quality_{view}_{tier}.png");
                    MapScreenshots.Render(cam, c_Width, c_Height, null);
                    MapScreenshots.Render(cam, c_Width, c_Height, path);
                    Debug.Log($"[QualityCompare] {path}");
                }
            }

            PlaceCamera(cam, s_Views[1].lookAt);
            var report = new StringBuilder($"[QualityCompare] GPU+CPU time per frame at {c_Width}x{c_Height}, " +
                                           $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), Editor:\n");
            float highMs = 0f;
            foreach (QualityTier tier in new[] { QualityTier.Low, QualityTier.Medium, QualityTier.High, QualityTier.Ultra })
            {
                Apply(scene, tier);
                float ms = TimeFrames(cam);
                if (tier == QualityTier.High)
                    highMs = ms;
                string vsHigh = tier == QualityTier.Ultra && highMs > 0f ? $"  (x{ms / highMs:F2} of High)" : string.Empty;
                report.AppendLine($"  {tier,-7} {ms,6:F2} ms  ~{1000f / ms,5:F0} fps{vsHigh}");
            }
            Debug.Log(report.ToString());

            SettingsService.ApplyQuality(QualityTier.Medium);            // the project's editor default
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);   // discard the preview state
        }

        /// <summary>Which Ultra feature changes the ground brightness: renders Ultra with one feature off at a time.</summary>
        public static void DiagnoseGround()
        {
            UltraQualityBuilder.BuildMaterials();                        // pick up tuned Ultra material values
            Scene scene = EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            Camera cam = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
            PlaceCamera(cam, s_Views[0].lookAt);
            ShaderUtil.allowAsyncCompilation = false;
            var report = new StringBuilder("[QualityCompare] ground RGB (asphalt patch):\n");
            Apply(scene, QualityTier.High);
            Ground(cam);                                                  // warm-up: shaders compile on the first frame
            report.AppendLine($"  High                 {Ground(cam)}");
            Apply(scene, QualityTier.Ultra);
            report.AppendLine($"  Ultra                {Ground(cam)}");

            var asset = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            var ssao = asset.rendererDataList[0].rendererFeatures.FirstOrDefault(f => f is UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion);
            Color32[] withAo = Frame(cam);
            ssao?.SetActive(false);
            report.AppendLine($"  Ultra, no SSAO       {Ground(cam)}");
            Color32[] withoutAo = Frame(cam);
            ssao?.SetActive(true);
            int changed = withAo.Where((p, i) => Mathf.Abs(p.r - withoutAo[i].r) + Mathf.Abs(p.g - withoutAo[i].g) > 6).Count();
            report.AppendLine($"  SSAO feature {(ssao != null ? "present" : "MISSING")}: darkens {100f * changed / withAo.Length:F1}% of the frame noticeably");

            GameObject ultraVolume = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == "Ultra Volume").gameObject;
            ultraVolume.SetActive(false);
            report.AppendLine($"  Ultra, no volume     {Ground(cam)}");
            ultraVolume.SetActive(true);

            UltraMaterialSwap swap = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UltraMaterialSwap>(true)).First();
            swap.ApplyQualityTier(QualityTier.High);
            report.AppendLine($"  Ultra, old materials {Ground(cam)}");
            swap.ApplyQualityTier(QualityTier.Ultra);

            Light sun = RenderSettings.sun;
            LightShadows shadows = sun.shadows;
            sun.shadows = LightShadows.None;
            report.AppendLine($"  Ultra, no sun shadow {Ground(cam)}");
            sun.shadows = shadows;
            Debug.Log(report.ToString());
            SettingsService.ApplyQuality(QualityTier.Medium);
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
        }

        private static Color32[] Frame(Camera cam)
        {
            var rt = new RenderTexture(c_Width, c_Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt });
            RenderTexture.active = rt;
            var tex = new Texture2D(c_Width, c_Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, c_Width, c_Height), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            return px;
        }

        private static string Ground(Camera cam)
        {
            var rt = new RenderTexture(c_Width, c_Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt });
            RenderTexture.active = rt;
            const int x = 800, y = c_Height - 760, w = 100, h = 60;          // the open asphalt below the workshop
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            Color32[] px = tex.GetPixels32();
            Vector3 sum = px.Aggregate(Vector3.zero, (s, p) => s + new Vector3(p.r, p.g, p.b)) / px.Length;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            return $"{sum.x:F0} {sum.y:F0} {sum.z:F0}";
        }

        private static void PlaceCamera(Camera cam, Vector3 lookAt)
        {
            Quaternion rotation = Quaternion.Euler(ViewConstants.c_CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(lookAt + rotation * new Vector3(0f, 0f, -ViewConstants.c_CameraDistance), rotation);
        }

        /// <summary>Switches the quality level and lets every tier-aware component of the scene react (as in the game).</summary>
        private static void Apply(Scene scene, QualityTier tier)
        {
            SettingsService.ApplyQuality(tier);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (IQualityTierListener listener in root.GetComponentsInChildren<IQualityTierListener>(true))
                listener.ApplyQualityTier(tier);

            // Particles do not run in Edit mode: advance the dust and fire one burst of muzzle sparks for the picture.
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(false))
            {
                if (ps.main.loop)
                    ps.Simulate(c_DustSimulation, true, true);
            }
            ShotEffects effects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ShotEffects>(true)).FirstOrDefault();
            PlayerWeapon weapon = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerWeapon>(true)).FirstOrDefault();
            ParticleSystem sparks = effects != null ? effects.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "MuzzleSparks") : null;
            if (sparks == null || weapon == null)
                return;
            sparks.Clear();
            if (tier < QualityTier.Ultra)
                return;
            sparks.transform.SetPositionAndRotation(weapon.Muzzle.position, Quaternion.LookRotation(new Vector3(1f, 0f, -0.3f)));
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            sparks.Simulate(c_SparkSimulation, true, true);
        }

        private static float TimeFrames(Camera cam)
        {
            var rt = new RenderTexture(c_Width, c_Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            var sync = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            var timer = new Stopwatch();
            for (int i = 0; i < c_WarmupFrames + c_TimedFrames; i++)
            {
                if (i == c_WarmupFrames)
                    timer.Start();
                RenderPipeline.SubmitRenderRequest(cam, request);
                RenderTexture.active = rt;
                sync.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);             // waits for the GPU to finish the frame
                sync.Apply(false);
                RenderTexture.active = null;
            }
            timer.Stop();
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(sync);
            return (float)timer.Elapsed.TotalMilliseconds / c_TimedFrames;
        }
    }
}
