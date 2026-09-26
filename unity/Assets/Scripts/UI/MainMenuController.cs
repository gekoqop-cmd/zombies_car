using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>Main menu: the Play button loads the configured map (with the HUD on top).</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MapConfig m_Map;
        [SerializeField] private Button m_PlayButton;

        private void Awake() => SettingsService.EnsureLoaded();

        private void OnEnable() => m_PlayButton.onClick.AddListener(Play);

        private void OnDisable() => m_PlayButton.onClick.RemoveListener(Play);

        private void Play()
        {
            m_PlayButton.interactable = false;
            SceneLoader.EnsureExists().LoadMap(m_Map);
        }
    }
}
