using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>Boot scene entry: loads user settings, creates the persistent loader and opens the main menu.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private GameSettings m_Settings;

        private void Start()
        {
            SettingsService.Load(m_Settings);
            SceneLoader.EnsureExists().LoadMainMenu();
        }
    }
}
