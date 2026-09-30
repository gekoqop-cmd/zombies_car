using System.Collections;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Boot scene entry: loads user settings, creates the persistent loader, waits for the Yandex cloud save to be read
    /// (at most <see cref="c_CloudWait"/> seconds: a slow network must not keep the player out) and opens the main menu.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private const float c_CloudWait = 5f;

        [SerializeField] private GameSettings m_Settings;

        private IEnumerator Start()
        {
            SettingsService.Load(m_Settings);
            float until = Time.realtimeSinceStartup + c_CloudWait;
            while (!YandexSdk.CloudReady && Time.realtimeSinceStartup < until)
                yield return null;
            SceneLoader.EnsureExists().LoadMainMenu();
        }
    }
}
