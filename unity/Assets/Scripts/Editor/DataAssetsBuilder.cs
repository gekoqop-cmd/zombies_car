using UnityEditor;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
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
        public const string c_WaveConfig = EditorPaths.c_Data + "/WaveConfig_Map01.asset";
        public const string c_CameraConfig = EditorPaths.c_Data + "/CameraConfig.asset";
        public const string c_CarConfig = EditorPaths.c_Data + "/CarConfig.asset";
        public const string c_AmmoState = EditorPaths.c_DataState + "/AmmoState.asset";
        public const string c_PlayerStats = EditorPaths.c_DataState + "/PlayerStats.asset";
        public const string c_WaveState = EditorPaths.c_DataState + "/WaveState.asset";

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
            Ensure<WaveConfig>(c_WaveConfig);
            Ensure<CameraConfig>(c_CameraConfig);
            Ensure<CarConfig>(c_CarConfig);
            Ensure<AmmoState>(c_AmmoState);
            Ensure<PlayerStats>(c_PlayerStats);
            Ensure<WaveState>(c_WaveState);
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

        public static T Load<T>(string path) where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Ensure<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }
    }
}
