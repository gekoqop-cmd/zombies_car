using UnityEditor;
using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.EditorTools
{
    /// <summary>Creates the ScriptableObject data assets (configs + shared runtime state) if they do not exist.</summary>
    public static class DataAssetsBuilder
    {
        public const string c_MapConfig = EditorPaths.c_Data + "/MapConfig_Map01.asset";
        public const string c_GameSettings = EditorPaths.c_Data + "/GameSettings.asset";
        public const string c_PlayerConfig = EditorPaths.c_Data + "/PlayerConfig.asset";
        public const string c_WeaponConfig = EditorPaths.c_Data + "/WeaponConfig_Default.asset";
        public const string c_WaveConfig = EditorPaths.c_Data + "/WaveConfig_Map01.asset";
        public const string c_AmmoState = EditorPaths.c_DataState + "/AmmoState.asset";
        public const string c_PlayerStats = EditorPaths.c_DataState + "/PlayerStats.asset";
        public const string c_WaveState = EditorPaths.c_DataState + "/WaveState.asset";

        [MenuItem("Tools/Setup/Create Data Assets")]
        public static void Run()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_Data);
            EditorPaths.EnsureFolder(EditorPaths.c_DataState);
            Ensure<MapConfig>(c_MapConfig);
            Ensure<GameSettings>(c_GameSettings);
            Ensure<PlayerConfig>(c_PlayerConfig);
            Ensure<WeaponConfig>(c_WeaponConfig);
            Ensure<WaveConfig>(c_WaveConfig);
            Ensure<AmmoState>(c_AmmoState);
            Ensure<PlayerStats>(c_PlayerStats);
            Ensure<WaveState>(c_WaveState);
            AssetDatabase.SaveAssets();
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
