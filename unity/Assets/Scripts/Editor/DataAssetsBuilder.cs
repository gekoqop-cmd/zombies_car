using UnityEditor;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Progression;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    /// <summary>Creates the ScriptableObject data assets (configs + shared runtime state) if they do not exist.</summary>
    public static class DataAssetsBuilder
    {
        public const string c_MapConfig = EditorPaths.c_Data + "/MapConfig_Map01.asset";
        public const string c_NightMapConfig = EditorPaths.c_Data + "/MapConfig_Map02.asset";
        public const string c_GameSettings = EditorPaths.c_Data + "/GameSettings.asset";
        public const string c_PlayerConfig = EditorPaths.c_Data + "/PlayerConfig.asset";
        public const string c_WeaponConfig = EditorPaths.c_Data + "/WeaponConfig_Default.asset";
        public const string c_MeleeConfig = EditorPaths.c_Data + "/MeleeConfig_Axe.asset";
        public const string c_WaveConfig = EditorPaths.c_Data + "/WaveConfig_Map01.asset";
        public const string c_CameraConfig = EditorPaths.c_Data + "/CameraConfig.asset";
        public const string c_CarConfig = EditorPaths.c_Data + "/CarConfig.asset";
        public const string c_AmmoState = EditorPaths.c_DataState + "/AmmoState.asset";
        public const string c_PlayerStats = EditorPaths.c_DataState + "/PlayerStats.asset";
        public const string c_WaveState = EditorPaths.c_DataState + "/WaveState.asset";
        public const string c_RunState = EditorPaths.c_DataState + "/RunState.asset";
        public const string c_InteractionState = EditorPaths.c_DataState + "/InteractionState.asset";
        public const string c_CarState = EditorPaths.c_DataState + "/CarState.asset";
        public const string c_WeaponState = EditorPaths.c_DataState + "/WeaponState.asset";
        public const string c_TutorialState = EditorPaths.c_DataState + "/TutorialState.asset";
        public const string c_ProgressionConfig = EditorPaths.c_Data + "/ProgressionConfig.asset";
        public const string c_PerkPool = EditorPaths.c_Data + "/PerkPool.asset";
        public const string c_CarUpgrades = EditorPaths.c_Data + "/CarUpgrades.asset";

        [MenuItem("Tools/Setup/Create Data Assets")]
        public static void Run()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_Data);
            EditorPaths.EnsureFolder(EditorPaths.c_DataState);
            Ensure<MapConfig>(c_MapConfig);
            EnsureNightMap();
            Ensure<GameSettings>(c_GameSettings);
            Ensure<PlayerConfig>(c_PlayerConfig);
            Ensure<WeaponConfig>(c_WeaponConfig);
            Ensure<MeleeConfig>(c_MeleeConfig);
            Ensure<WaveConfig>(c_WaveConfig);
            Ensure<CameraConfig>(c_CameraConfig);
            Ensure<CarConfig>(c_CarConfig);
            Ensure<AmmoState>(c_AmmoState);
            Ensure<PlayerStats>(c_PlayerStats);
            Ensure<WaveState>(c_WaveState);
            Ensure<RunState>(c_RunState);
            Ensure<InteractionState>(c_InteractionState);
            Ensure<CarState>(c_CarState);
            Ensure<WeaponState>(c_WeaponState);
            Ensure<TutorialState>(c_TutorialState);
            Ensure<ProgressionConfig>(c_ProgressionConfig);
            Ensure<PerkPool>(c_PerkPool);
            Ensure<CarUpgradeCatalog>(c_CarUpgrades);
            ProgressionBuilder.Run();
            CosmeticsBuilder.Run();
            AudioBuilder.BuildSoundBank();
            SetMapMusic(c_MapConfig, "Music_Day");
            SetMapMusic(c_NightMapConfig, "Music_Night");
            AssetDatabase.SaveAssets();
        }

        // Map02: the same lot as Map01 at night — a copy of its config pointing at the Map02 scene.
        private static void EnsureNightMap()
        {
            if (AssetDatabase.LoadAssetAtPath<MapConfig>(c_NightMapConfig) == null)
                AssetDatabase.CopyAsset(c_MapConfig, c_NightMapConfig);
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<MapConfig>(c_NightMapConfig));
            so.FindProperty("m_SceneName").stringValue = SceneNames.c_Map02;
            so.FindProperty("m_MapNumber").intValue = 2;
            so.FindProperty("m_CharacterTint").colorValue = new Color(0.74f, 0.8f, 1f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Waves of a map: the last boss comes at wave 30.</summary>
        public const int c_WaveCount = 30;

        // The evening map plays the driving track, the night map the dark one; both switch to the boss track for bosses.
        private static void SetMapMusic(string mapConfig, string music)
        {
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<MapConfig>(mapConfig));
            so.FindProperty("m_Music").objectReferenceValue = AudioBuilder.Music(music);
            so.FindProperty("m_BossMusic").objectReferenceValue = AudioBuilder.Music("Music_Boss");
            so.FindProperty("m_WaveThreshold").intValue = c_WaveCount;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static T Load<T>(string path) where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Ensure<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }
    }
}
