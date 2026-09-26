using System.IO;
using UnityEditor;

namespace ZombiCar.EditorTools
{
    /// <summary>Asset locations shared by the editor build tools.</summary>
    public static class EditorPaths
    {
        public const string c_Models = "Assets/Art/Models";
        public const string c_Textures = "Assets/Art/Textures";
        public const string c_CharacterTextures = "Assets/Art/Textures/Characters";
        public const string c_Materials = "Assets/Art/Materials";
        public const string c_Vfx = "Assets/Art/VFX";
        public const string c_Fonts = "Assets/Art/Fonts";
        public const string c_GeneratedMeshes = "Assets/Art/Models/Generated";
        public const string c_Prefabs = "Assets/Prefabs";
        public const string c_PrefabsEnvironment = "Assets/Prefabs/Environment";
        public const string c_PrefabsProps = "Assets/Prefabs/Props";
        public const string c_PrefabsCharacters = "Assets/Prefabs/Characters";
        public const string c_PrefabsEnemies = "Assets/Prefabs/Enemies";
        public const string c_PrefabsUi = "Assets/Prefabs/UI";
        public const string c_Scenes = "Assets/Scenes";
        public const string c_Data = "Assets/Data";
        public const string c_DataState = "Assets/Data/State";
        public const string c_DataCharacters = "Assets/Data/Characters";
        public const string c_DataZombies = "Assets/Data/Zombies";
        public const string c_Settings = "Assets/Settings";

        /// <summary>Creates an Assets/... folder (and parents) if it does not exist yet.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
