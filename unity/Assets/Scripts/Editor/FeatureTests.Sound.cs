using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombiCar.Core;
using ZombiCar.UI;
using SettingsService = ZombiCar.Core.SettingsService;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 2. sound and music
        /// <summary>
        /// One persistent audio manager on the mixer's SFX / Music groups, the map's music (day or night), the named
        /// effects of the UI, level-ups, waves and the workshop actually playing, the volume sliders reaching the
        /// mixer, and the cross-fade to the menu music when leaving the map.
        /// </summary>
        private static void SoundAndMusic()
        {
            AudioManager manager = null;
            At(1.2f, () =>
            {
                manager = AudioManager.Instance;
                Check(manager != null, "an audio manager runs");
                Check(Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include).Length == 1, "exactly one audio manager");
                Check(manager.gameObject.scene.name == "DontDestroyOnLoad", "it survives scene loads");
                AudioSource[] sources = manager.GetComponents<AudioSource>();
                Check(sources.All(s => s.outputAudioMixerGroup != null), "every source is routed to the mixer");
                Check(sources.Count(s => s.outputAudioMixerGroup.name == "Music") == 2 &&
                      sources.Count(s => s.outputAudioMixerGroup.name == "SFX") >= 12, "music on the Music group, effects on SFX");
                MapConfig map = Find<MapBootstrap>().Map;
                Check(map.Music != null && manager.CurrentMusic == map.Music, $"the map plays its music: {manager.CurrentMusic?.name}");
                var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(AudioBuilder.c_SoundBank);
                foreach (ZombiCar.Core.Sound id in new[] { ZombiCar.Core.Sound.UiClick, ZombiCar.Core.Sound.UiOpen, ZombiCar.Core.Sound.UiClose, ZombiCar.Core.Sound.UiBuy, ZombiCar.Core.Sound.UiDenied,
                             ZombiCar.Core.Sound.LevelUp, ZombiCar.Core.Sound.CardPick, ZombiCar.Core.Sound.WaveStart, ZombiCar.Core.Sound.WaveEnd, ZombiCar.Core.Sound.Victory, ZombiCar.Core.Sound.Defeat })
                    Check(bank.Get(id) != null, $"sound bank has {id}");
                s_Director.enabled = false;
                s_Waves.StartWave(1);
            });
            At(1.5f, () =>
            {
                Check(Playing("Wave_Start"), "wave start: war horn");
                Check(Find<WaveBanner>().LastTitle == "ВОЛНА 1", "wave start banner");
                Shot("2_WaveBanner");
                s_Waves.EndWave(1, false);
            });
            At(1.8f, () =>
            {
                Check(Playing("Wave_End"), "wave cleared: chime");
                Shot("2_WaveCleared");
                s_Progression.AddXp(40);
            });
            At(2.3f, () => Check(Playing("LevelUp"), "level-up fanfare"));
            At(2.8f, () =>
            {
                LevelUpScreen levelUp = Find<LevelUpScreen>();
                Check(levelUp.IsOpen, "the level-up cards are dealt");
                levelUp.Pick(0);
            });
            At(3.05f, () => Check(Playing("Card_Pick"), "card pick sound"));
            At(3.2f, () =>
            {
                s_Run.SetWorkshopOpen(true);
            });
            At(3.4f, () =>
            {
                Check(Playing("UI_Open"), "workshop opens with a sound");
                s_Run.AddMoney(100);
                Find<WorkshopScreen>().Buy(0);
            });
            At(3.6f, () =>
            {
                Check(Playing("Upg_Armor"), "purchase: the armour's own sound (plates, bolts, the sting)");
                Find<WorkshopScreen>().Buy(3);                                   // 150: too expensive now
            });
            At(3.8f, () =>
            {
                Check(Playing("UI_Denied"), "refused purchase: buzz");
                Find<WorkshopScreen>().Close();
            });
            At(4.0f, () =>
            {
                Check(Playing("UI_Close"), "workshop closes with a sound");
                var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AudioBuilder.c_Mixer);
                SettingsService.SetMusicVolume(0.5f);
                mixer.GetFloat("MusicVolume", out float music);
                Check(Mathf.Abs(music - (-6.02f)) < 0.2f, $"music slider 50% -> mixer {music:F1} dB");
                SettingsService.SetSfxVolume(0.25f);
                mixer.GetFloat("SfxVolume", out float sfx);
                Check(Mathf.Abs(sfx - (-12.04f)) < 0.2f, $"effects slider 25% -> mixer {sfx:F1} dB");
                SettingsService.SetMusicVolume(1f);
                SettingsService.SetSfxVolume(1f);
                SceneLoader.EnsureExists().LoadMainMenu();
            });
            At(7.0f, () =>
            {
                Check(SceneManager.GetActiveScene().name == SceneNames.c_MainMenu, "back in the main menu");
                Check(AudioManager.Instance == manager && Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include).Length == 1,
                    "the same audio manager (the menu's copy removed itself)");
                Check(manager.CurrentMusic != null && manager.CurrentMusic.name == "Music_Menu", $"menu music: {manager.CurrentMusic?.name}");
                AudioSource[] music = manager.GetComponents<AudioSource>().Where(s => s.outputAudioMixerGroup.name == "Music").ToArray();
                Check(music.Any(s => s.isPlaying && s.clip != null && s.clip.name == "Music_Menu"), "menu music is playing (cross-faded)");
            });
            At(7.2f, Finish);
        }

        private static bool Playing(string clip) =>
            AudioManager.Instance != null && AudioManager.Instance.GetComponents<AudioSource>()
                .Any(s => s.isPlaying && s.clip != null && s.clip.name == clip);
    }
}
