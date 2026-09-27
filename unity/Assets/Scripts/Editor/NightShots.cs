using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombiCar.Core;
using SettingsService = ZombiCar.Core.SettingsService;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Diagnostics/Night Map Screenshots: renders Map01 (evening) and Map02 (night) from the same game-camera
    /// views — the lot, the workshop, the gas sign, the car — on Medium and Ultra (Screenshots/Night/*.png), with the
    /// map's character tint applied to the sprites as in play.
    /// </summary>
    public static class NightShots
    {
        private const string c_Output = "Screenshots/Night";
        private const int c_Width = 1920;
        private const int c_Height = 1080;

        private static readonly (string name, Vector3 lookAt)[] s_Views =
        {
            ("Lot", new Vector3(0f, 0f, 1f)),
            ("Workshop", new Vector3(-8f, 0f, 6f)),
            ("GasSign", new Vector3(14f, 0f, 12f)),
            ("Car", new Vector3(0.5f, 0f, -1f)),
        };

        private static readonly QualityTier[] s_Tiers = { QualityTier.Medium, QualityTier.Ultra };

        [MenuItem("Tools/Diagnostics/Night Map Screenshots")]
        public static void Run()
        {
            string folder = Path.GetFullPath(c_Output);
            Directory.CreateDirectory(folder);
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            foreach ((string map, string path) in new[] { ("Map01", SceneBuilder.c_MapScene), ("Map02", SceneBuilder.c_NightMapScene) })
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                DynamicGI.UpdateEnvironment();
                TintCharacters(scene);
                Camera cam = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
                cam.fieldOfView = ViewConstants.c_CameraFov;
                foreach ((string view, Vector3 lookAt) in s_Views)
                {
                    QualityCompare.PlaceCamera(cam, lookAt);
                    foreach (QualityTier tier in s_Tiers)
                    {
                        QualityCompare.Apply(scene, tier);
                        string file = Path.Combine(folder, $"{map}_{view}_{tier}.png");
                        MapScreenshots.Render(cam, c_Width, c_Height, null);
                        MapScreenshots.Render(cam, c_Width, c_Height, file);
                        Debug.Log($"[NightShots] {file}");
                    }
                }
            }
            SettingsService.ApplyQuality(QualityTier.Medium);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
        }

        // PlayerView / SpriteAnimator apply the map tint at run time; in Edit mode the sprites get it here.
        private static void TintCharacters(Scene scene)
        {
            MapBootstrap bootstrap = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapBootstrap>(true)).FirstOrDefault();
            Color tint = bootstrap != null && bootstrap.Map != null ? bootstrap.Map.CharacterTint : Color.white;
            foreach (SpriteRenderer sprite in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)))
                sprite.color *= tint;
        }
    }
}
