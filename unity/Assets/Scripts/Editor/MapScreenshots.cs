using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Capture Map01 Screenshots: renders Map01 (+ the HUD scene) from the game camera into
    /// &lt;project&gt;/Screenshots — gameplay views at 16:9, 19.5:9 and 4:3, close-ups of the map areas and a wide
    /// overview of the whole play area for comparison with referens/concept.png. Works in batch mode.
    /// </summary>
    public static class MapScreenshots
    {
        private const string c_OutputFolder = "Screenshots";
        private const int c_Height = 1080;
        private const float c_OverviewDistance = 62f;   // whole play area + road and houses beyond the fence
        private const float c_OverviewShadowDistance = 140f;

        private sealed class Shot
        {
            public string Name;
            public Vector3 LookAt;
            public float Distance = ViewConstants.c_CameraDistance;
            public int Width = 1920;
            public bool Hud;
            public bool Overview;
        }

        private static readonly Shot[] s_Shots =
        {
            new Shot { Name = "Map01_Gameplay_16x9", LookAt = SceneBuilder.PlayerSpawn, Hud = true },
            new Shot { Name = "Map01_Gameplay_19.5x9", LookAt = SceneBuilder.PlayerSpawn, Hud = true, Width = 2340 },
            new Shot { Name = "Map01_Gameplay_4x3", LookAt = SceneBuilder.PlayerSpawn, Hud = true, Width = 1440 },
            new Shot { Name = "Map01_Workshop", LookAt = new Vector3(-7f, 0f, 4f) },
            new Shot { Name = "Map01_GasStation", LookAt = new Vector3(10f, 0f, 4f) },
            new Shot { Name = "Map01_North", LookAt = new Vector3(3f, 0f, 10f) },
            new Shot { Name = "Map01_South", LookAt = new Vector3(0f, 0f, -8f) },
            new Shot { Name = "Map01_Overview", LookAt = Vector3.zero, Distance = c_OverviewDistance, Overview = true },
        };

        [MenuItem("Tools/Capture Map01 Screenshots")]
        public static void CaptureAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene map = EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            Scene hud = EditorSceneManager.OpenScene(SceneBuilder.c_HudScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(map);
            DynamicGI.UpdateEnvironment();

            Camera cam = map.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
            Canvas hudCanvas = hud.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First();
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;   // overlay canvases are not part of camera renders
            hudCanvas.worldCamera = cam;
            hudCanvas.planeDistance = cam.nearClipPlane + 0.5f;

            string folder = Path.GetFullPath(c_OutputFolder);
            Directory.CreateDirectory(folder);
            // Async compilation would draw not-yet-compiled shaders with the cyan placeholder.
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float shadowDistance = urp != null ? urp.shadowDistance : 0f;
            bool fog = RenderSettings.fog;
            float fov = cam.fieldOfView;

            foreach (Shot shot in s_Shots)
            {
                float aspect = (float)shot.Width / c_Height;
                cam.fieldOfView = CameraAspectAdapter.VerticalFov(aspect, fov, ViewConstants.c_MaxCameraFov);
                Quaternion rotation = Quaternion.Euler(ViewConstants.c_CameraPitch, 0f, 0f);
                cam.transform.SetPositionAndRotation(shot.LookAt + rotation * new Vector3(0f, 0f, -shot.Distance), rotation);
                hudCanvas.gameObject.SetActive(shot.Hud);
                RenderSettings.fog = fog && !shot.Overview;
                if (urp != null)
                    urp.shadowDistance = shot.Overview ? c_OverviewShadowDistance : shadowDistance;
                string path = Path.Combine(folder, shot.Name + ".png");
                Render(cam, shot.Width, c_Height, null);   // warm-up: shader variants, shadow maps, TMP meshes
                Render(cam, shot.Width, c_Height, path);
                Debug.Log($"[MapScreenshots] {path}");
            }

            if (urp != null)
                urp.shadowDistance = shadowDistance;
            RenderSettings.fog = fog;
            EditorSceneManager.CloseScene(hud, true);   // nothing is saved: the scenes stay as built
            CaptureMenu(folder);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
        }

        private static void CaptureMenu(string folder)
        {
            const int uiLayer = 5;
            const int width = 1920;
            Scene menu = EditorSceneManager.OpenScene(SceneBuilder.c_MenuScene, OpenSceneMode.Single);
            Camera cam = menu.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
            Canvas canvas = menu.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First();
            cam.cullingMask = 1 << uiLayer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 1f;
            string path = Path.Combine(folder, "MainMenu.png");
            Render(cam, width, c_Height, null);
            Render(cam, width, c_Height, path);
            Debug.Log($"[MapScreenshots] {path}");
        }

        private static void Render(Camera cam, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            // The camera-space HUD sizes itself from the camera's pixel size; batch mode runs no editor update,
            // so the scalers are re-enabled by hand to recompute the scale for this resolution.
            cam.targetTexture = rt;
            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Exclude))
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
                text.ForceMeshUpdate(true, true);

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();
            cam.targetTexture = null;

            if (path == null)
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                return;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
