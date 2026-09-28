using UnityEngine;
using ZombiCar.Player;
using ZombiCar.Progression;

namespace ZombiCar.Core
{
    /// <summary>
    /// Lives in every map scene. Starts a fresh run (<see cref="RunState"/>, the player's stats) before anything else
    /// of the map wakes up, starts the map's music, and makes the map playable when it is opened directly in the
    /// Editor: loads settings and adds the HUD scene if the normal Boot -> MainMenu flow was skipped.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MapBootstrap : MonoBehaviour
    {
        [SerializeField] private MapConfig m_Map;
        [SerializeField] private RunState m_Run;
        [SerializeField] private PlayerStats m_PlayerStats;

        public MapConfig Map => m_Map;

        private void OnDestroy() => GameSession.ExitMap();

        private void Awake()
        {
            SettingsService.EnsureLoaded();
            SceneTint.Characters = m_Map != null ? m_Map.CharacterTint : Color.white;
            if (m_PlayerStats != null)
                m_PlayerStats.ResetToDefaults();
            if (m_Run != null)
                m_Run.BeginRun(m_Map);
        }

        private void Start()
        {
            // No baked lighting: rebuild the ambient probe from the scene's gradient ambient colours.
            DynamicGI.UpdateEnvironment();
            GameSession.EnterMap();
            SceneLoader.EnsureExists().EnsureHudLoaded();
            if (m_Map != null)
                AudioManager.PlayMusic(m_Map.Music);
            if (QualityAutoTuner.IsNeeded)
                gameObject.AddComponent<QualityAutoTuner>();
        }
    }
}
