using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Lives in every map scene. Makes the map playable when it is opened directly in the Editor:
    /// loads settings and adds the HUD scene if the normal Boot -> MainMenu flow was skipped.
    /// </summary>
    public sealed class MapBootstrap : MonoBehaviour
    {
        [SerializeField] private MapConfig m_Map;

        public MapConfig Map => m_Map;

        private void Awake()
        {
            SettingsService.EnsureLoaded();
            SceneTint.Characters = m_Map != null ? m_Map.CharacterTint : Color.white;
        }

        private void Start()
        {
            // No baked lighting: rebuild the ambient probe from the scene's gradient ambient colours.
            DynamicGI.UpdateEnvironment();
            SceneLoader.EnsureExists().EnsureHudLoaded();
        }
    }
}
