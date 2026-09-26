using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Validate Scenes: opens every scene in Build Settings and reports missing scripts and empty or broken
    /// object references on the game's own components (ZombiCar.*). All of them are wired by the builders.
    /// </summary>
    public static class SceneValidator
    {
        private const string c_GameNamespace = "ZombiCar";

        [MenuItem("Tools/Validate Scenes")]
        public static int ValidateBuildScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return -1;
            int problems = 0, objects = 0;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    problems += Validate(t.gameObject, scene.name);
                    objects++;
                }
            }
            if (problems > 0)
                Debug.LogError($"[SceneValidator] {problems} problem(s) in {EditorBuildSettings.scenes.Length} build scenes.");
            else
                Debug.Log($"[SceneValidator] {EditorBuildSettings.scenes.Length} build scenes, {objects} objects: " +
                          "no missing scripts, no empty or broken references.");
            return problems;
        }

        private static int Validate(GameObject go, string sceneName)
        {
            int problems = 0;
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null)
                {
                    Debug.LogError($"[SceneValidator] {sceneName}: missing script on {PathOf(go)}");
                    problems++;
                    continue;
                }
                string ns = component.GetType().Namespace;
                if (ns == null || !ns.StartsWith(c_GameNamespace))
                    continue;

                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != null)
                        continue;
                    string kind = property.objectReferenceEntityIdValue.IsValid() ? "broken" : "empty";
                    Debug.LogError($"[SceneValidator] {sceneName}: {kind} reference {component.GetType().Name}.{property.name} on {PathOf(go)}");
                    problems++;
                }
            }
            return problems;
        }

        private static string PathOf(GameObject go)
        {
            var path = new StringBuilder(go.name);
            for (Transform t = go.transform.parent; t != null; t = t.parent)
                path.Insert(0, t.name + "/");
            return path.ToString();
        }
    }
}
