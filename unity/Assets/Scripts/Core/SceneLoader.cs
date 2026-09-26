using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombiCar.Core
{
    /// <summary>
    /// Persistent scene loader. Maps are loaded as the single active scene and the HUD scene is added on top
    /// (Additive). Created by the Boot scene; <see cref="EnsureExists"/> creates it when a scene is played directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        private const string c_ObjectName = "[SceneLoader]";

        public static SceneLoader Instance { get; private set; }

        public bool IsLoading { get; private set; }
        public MapConfig CurrentMap { get; private set; }

        public static SceneLoader EnsureExists()
        {
            if (Instance == null)
                new GameObject(c_ObjectName).AddComponent<SceneLoader>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (!TryGetComponent(out AudioFocusGuard _))
                gameObject.AddComponent<AudioFocusGuard>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void LoadMainMenu()
        {
            if (!IsLoading)
                StartCoroutine(LoadSingleRoutine(SceneNames.c_MainMenu));
        }

        public void LoadMap(MapConfig map)
        {
            if (map == null)
            {
                Debug.LogError("[SceneLoader] MapConfig is not assigned.");
                return;
            }
            if (!IsLoading)
                StartCoroutine(LoadMapRoutine(map));
        }

        /// <summary>Adds the HUD scene on top of the current map if it is not loaded yet.</summary>
        public void EnsureHudLoaded()
        {
            if (!IsLoading && !SceneManager.GetSceneByName(SceneNames.c_Hud).isLoaded)
                StartCoroutine(LoadHudRoutine());
        }

        private IEnumerator LoadSingleRoutine(string sceneName)
        {
            IsLoading = true;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            IsLoading = false;
        }

        private IEnumerator LoadMapRoutine(MapConfig map)
        {
            IsLoading = true;
            CurrentMap = map;
            yield return SceneManager.LoadSceneAsync(map.SceneName, LoadSceneMode.Single);
            yield return LoadHudRoutineInternal();
            Scene mapScene = SceneManager.GetSceneByName(map.SceneName);
            if (mapScene.IsValid())
                SceneManager.SetActiveScene(mapScene);   // lighting / instantiation stay in the map scene
            IsLoading = false;
        }

        private IEnumerator LoadHudRoutine()
        {
            IsLoading = true;
            yield return LoadHudRoutineInternal();
            IsLoading = false;
        }

        private static IEnumerator LoadHudRoutineInternal()
        {
            if (!SceneManager.GetSceneByName(SceneNames.c_Hud).isLoaded)
                yield return SceneManager.LoadSceneAsync(SceneNames.c_Hud, LoadSceneMode.Additive);
        }
    }
}
