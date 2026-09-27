using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The game mixer (Audio/GameMixer.mixer: Master → SFX, volumes exposed as "MasterVolume" / "SfxVolume") and the
    /// scene's <see cref="AudioManager"/>. Unity has no public API for authoring mixers, so the asset is made through
    /// the editor's internal AudioMixerController; if that API changes the build logs it and the AudioManager falls
    /// back to AudioListener.volume.
    /// </summary>
    public static class AudioBuilder
    {
        public const string c_Mixer = "Assets/Audio/GameMixer.mixer";
        private const string c_MasterVolume = "MasterVolume";
        private const string c_SfxVolume = "SfxVolume";
        private const string c_SfxGroup = "SFX";
        private const BindingFlags c_Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [MenuItem("Tools/Build Audio Mixer")]
        public static void BuildMixer() => EnsureMixer();

        public static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(c_Mixer);
            if (existing != null && existing.FindMatchingGroups(c_SfxGroup).Length > 0)
                return existing;
            if (existing != null)
                AssetDatabase.DeleteAsset(c_Mixer);
            EditorPaths.EnsureFolder("Assets/Audio");

            try
            {
                Assembly editor = typeof(Editor).Assembly;
                Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
                Type groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
                Type groupPathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
                Type pathType = editor.GetType("UnityEditor.Audio.AudioParameterPath", true);

                object mixer = controllerType.GetMethod("CreateMixerControllerAtPath", c_Any).Invoke(null, new object[] { c_Mixer });
                object master = controllerType.GetProperty("masterGroup", c_Any).GetValue(mixer);
                object sfx = controllerType.GetMethod("CreateNewGroup", c_Any).Invoke(mixer, new object[] { c_SfxGroup, false });
                controllerType.GetMethod("AddChildToParent", c_Any).Invoke(mixer, new[] { sfx, master });

                MethodInfo expose = controllerType.GetMethods(c_Any).First(m => m.Name == "AddExposedParameter" &&
                    m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == pathType);
                foreach ((object group, string name) in new[] { (master, c_MasterVolume), (sfx, c_SfxVolume) })
                {
                    object guid = groupType.GetMethod("GetGUIDForVolume", c_Any).Invoke(group, null);
                    object path = Activator.CreateInstance(groupPathType, group, guid);
                    expose.Invoke(mixer, new[] { path });
                    Rename(controllerType, mixer, guid, name);
                }
                EditorUtility.SetDirty((UnityEngine.Object)mixer);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioBuilder] Could not author {c_Mixer} through the internal mixer API " +
                               $"({e.GetBaseException()}); sounds play unrouted, volume via AudioListener.");
                return null;
            }

            var result = AssetDatabase.LoadAssetAtPath<AudioMixer>(c_Mixer);
            Debug.Log($"[AudioBuilder] {c_Mixer}: groups {string.Join(", ", result.FindMatchingGroups(string.Empty).Select(g => g.name))}, " +
                      $"exposed {c_MasterVolume}={result.GetFloat(c_MasterVolume, out _)} {c_SfxVolume}={result.GetFloat(c_SfxVolume, out _)}");
            return result;
        }

        // The exposed parameter is created with a generated name ("MyExposedParam"); give it the name the code uses.
        private static void Rename(Type controllerType, object mixer, object guid, string name)
        {
            PropertyInfo property = controllerType.GetProperty("exposedParameters", c_Any);
            var parameters = (Array)property.GetValue(mixer);
            Type element = parameters.GetType().GetElementType();
            FieldInfo guidField = element.GetField("guid", c_Any);
            FieldInfo nameField = element.GetField("name", c_Any);
            for (int i = 0; i < parameters.Length; i++)
            {
                object p = parameters.GetValue(i);
                if (!guidField.GetValue(p).Equals(guid))
                    continue;
                nameField.SetValue(p, name);
                parameters.SetValue(p, i);
            }
            property.SetValue(mixer, parameters);
        }

        /// <summary>Adds the scene's AudioManager (SFX voice pool on the mixer's SFX group) to <paramref name="systems"/>.</summary>
        public static AudioManager AddManager(GameObject systems)
        {
            AudioMixer mixer = EnsureMixer();
            var manager = systems.AddComponent<AudioManager>();
            AudioMixerGroup sfx = mixer != null ? mixer.FindMatchingGroups(c_SfxGroup).FirstOrDefault() : null;
            PrefabBuilder.Wire(manager, ("m_Mixer", mixer), ("m_SfxGroup", sfx));
            return manager;
        }
    }
}
