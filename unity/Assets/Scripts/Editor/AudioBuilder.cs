using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using ZombiCar.Core;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// The game mixer (Audio/GameMixer.mixer: Master → Music + SFX, volumes exposed as "MasterVolume" / "MusicVolume" /
    /// "SfxVolume", driven by the settings sliders), the <see cref="SoundBank"/> of named effects (Data/Audio, from the
    /// table below and Audio/SFX) and the scenes' "[Audio]" object with the <see cref="AudioManager"/>. Unity has no
    /// public API for authoring mixers, so the asset is made through the editor's internal AudioMixerController; if
    /// that API changes the build logs it and the AudioManager falls back to AudioListener.volume.
    /// </summary>
    public static class AudioBuilder
    {
        public const string c_Mixer = "Assets/Audio/GameMixer.mixer";
        public const string c_SoundBank = EditorPaths.c_Data + "/Audio/SoundBank.asset";
        public const string c_Music = "Assets/Audio/Music";
        private const string c_Sfx = "Assets/Audio/SFX";
        private const string c_MasterVolume = "MasterVolume";
        private const string c_SfxVolume = "SfxVolume";
        private const string c_SfxGroup = "SFX";
        private const string c_MusicGroup = "Music";
        private const string c_MusicVolume = "MusicVolume";
        private const BindingFlags c_Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Named effects: files in Audio/SFX (tools/audio/make_sfx.py), volume, pitch variation, minimum interval.</summary>
        private static readonly (Sound id, string[] files, float volume, float jitter, float interval)[] s_Sounds =
        {
            (Sound.UiClick, new[] { "UI_Click" }, 0.7f, 0.05f, 0.03f),
            (Sound.UiOpen, new[] { "UI_Open" }, 0.8f, 0f, 0.05f),
            (Sound.UiClose, new[] { "UI_Close" }, 0.7f, 0f, 0.05f),
            (Sound.UiBuy, new[] { "UI_Buy" }, 0.8f, 0.03f, 0f),
            (Sound.UiDenied, new[] { "UI_Denied" }, 0.7f, 0f, 0.1f),
            (Sound.LevelUp, new[] { "LevelUp" }, 0.8f, 0f, 0.2f),
            (Sound.CardPick, new[] { "Card_Pick" }, 0.8f, 0f, 0.1f),
            (Sound.WaveStart, new[] { "Wave_Start" }, 0.85f, 0f, 1f),
            (Sound.WaveEnd, new[] { "Wave_End" }, 0.8f, 0f, 1f),
            (Sound.Victory, new[] { "Victory" }, 0.9f, 0f, 1f),
            (Sound.Defeat, new[] { "Defeat" }, 0.9f, 0f, 1f),
            (Sound.CarShock, new[] { "Car_Shock" }, 0.6f, 0.08f, 0.2f),
            (Sound.CarHit, new[] { "Car_Hit1", "Car_Hit2", "Car_Hit3" }, 0.55f, 0.1f, 0.12f),
            (Sound.PlayerHurt, new[] { "Player_Hurt1", "Player_Hurt2" }, 0.75f, 0.08f, 0.15f),
            (Sound.AmmoRefill, new[] { "Ammo_Refill" }, 0.85f, 0f, 0.3f),
            (Sound.Explosion, new[] { "Explosion1", "Explosion2" }, 1f, 0.06f, 0.05f),
            (Sound.AxeSwing, new[] { "Axe_Swing1", "Axe_Swing2" }, 0.6f, 0.08f, 0.05f),
            (Sound.AxeHit, new[] { "Axe_Hit1", "Axe_Hit2" }, 0.8f, 0.08f, 0.04f),
            (Sound.WeaponSwitch, new[] { "Weapon_Switch" }, 0.6f, 0.05f, 0.1f),
            (Sound.BossRoar, new[] { "Boss_Roar" }, 1f, 0.04f, 0.5f),
            (Sound.BossAttack, new[] { "Boss_Attack" }, 0.9f, 0.08f, 0.2f),
            (Sound.BossDeath, new[] { "Boss_Death" }, 1f, 0f, 1f),
            (Sound.BossSummon, new[] { "Boss_Summon" }, 0.85f, 0.05f, 0.5f),
            (Sound.BossSpit, new[] { "Boss_Spit" }, 0.8f, 0.1f, 0.08f),
            (Sound.SpitSplat, new[] { "Spit_Splat" }, 0.7f, 0.12f, 0.06f),
            (Sound.BossCharge, new[] { "Boss_Charge" }, 0.9f, 0.05f, 0.5f),
            (Sound.BossSlam, new[] { "Boss_Slam" }, 1f, 0.05f, 0.3f),
            (Sound.UpgradeArmor, new[] { "Upg_Armor" }, 0.85f, 0f, 0.1f),
            (Sound.UpgradeHull, new[] { "Upg_Hull" }, 0.85f, 0f, 0.1f),
            (Sound.UpgradeSpikes, new[] { "Upg_Spikes" }, 0.85f, 0f, 0.1f),
            (Sound.UpgradeShock, new[] { "Upg_Shock" }, 0.8f, 0f, 0.1f),
            (Sound.UpgradeAmmoCase, new[] { "Upg_AmmoCase" }, 0.8f, 0f, 0.1f),
            (Sound.SupplyMedkit, new[] { "Sup_Medkit" }, 0.85f, 0f, 0.1f),
            (Sound.SupplyAmmoBox, new[] { "Sup_AmmoBox" }, 0.85f, 0f, 0.1f),
            (Sound.SupplyMines, new[] { "Sup_Mines" }, 0.65f, 0f, 0.1f),
            (Sound.SupplyRepair, new[] { "Sup_Repair" }, 0.85f, 0f, 0.1f),
            (Sound.LegendaryDrop, new[] { "Legendary_Drop" }, 0.95f, 0f, 1f),
        };

        [MenuItem("Tools/Build Audio Mixer")]
        public static void BuildMixer() => EnsureMixer();

        public static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(c_Mixer);
            if (existing != null && existing.FindMatchingGroups(c_SfxGroup).Length > 0 &&
                existing.FindMatchingGroups(c_MusicGroup).Length > 0)
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
                object music = controllerType.GetMethod("CreateNewGroup", c_Any).Invoke(mixer, new object[] { c_MusicGroup, false });
                controllerType.GetMethod("AddChildToParent", c_Any).Invoke(mixer, new[] { music, master });

                MethodInfo expose = controllerType.GetMethods(c_Any).First(m => m.Name == "AddExposedParameter" &&
                    m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == pathType);
                foreach ((object group, string name) in new[] { (master, c_MasterVolume), (sfx, c_SfxVolume), (music, c_MusicVolume) })
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

        /// <summary>Rebuilds the sound bank from the table and the clips in Audio/SFX (missing clips are left out).</summary>
        public static SoundBank BuildSoundBank()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_Data + "/Audio");
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(c_SoundBank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, c_SoundBank);
            }
            var entries = new List<SoundBank.Entry>();
            var missing = new List<string>();
            foreach ((Sound id, string[] files, float volume, float jitter, float interval) in s_Sounds)
            {
                AudioClip[] clips = files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>($"{c_Sfx}/{f}.ogg")).Where(c => c != null).ToArray();
                if (clips.Length == 0)
                {
                    missing.Add(id.ToString());
                    continue;
                }
                entries.Add(new SoundBank.Entry { Id = id, Clips = clips, Volume = volume, PitchJitter = jitter, MinInterval = interval });
            }
            bank.EditorSetEntries(entries.ToArray());
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioBuilder] Sound bank: {entries.Count} sounds" + (missing.Count > 0 ? $" (no clips yet: {string.Join(", ", missing)})" : "."));
            return bank;
        }

        public static AudioClip Music(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{c_Music}/{name}.ogg");
            if (clip == null)
                Debug.LogError($"[AudioBuilder] {c_Music}/{name}.ogg missing (tools/audio/make_music.py)");
            return clip;
        }

        /// <summary>
        /// Adds the scene's "[Audio]" object: the session's <see cref="AudioManager"/> (effect voices on the SFX group,
        /// music on the Music group, the sound bank). The first scene played keeps it; later copies remove themselves.
        /// </summary>
        public static AudioManager AddAudio()
        {
            AudioMixer mixer = EnsureMixer();
            var go = new GameObject("[Audio]");
            var manager = go.AddComponent<AudioManager>();
            AudioMixerGroup sfx = mixer != null ? mixer.FindMatchingGroups(c_SfxGroup).FirstOrDefault() : null;
            AudioMixerGroup music = mixer != null ? mixer.FindMatchingGroups(c_MusicGroup).FirstOrDefault() : null;
            PrefabBuilder.Wire(manager, ("m_Mixer", mixer), ("m_SfxGroup", sfx), ("m_MusicGroup", music),
                ("m_Bank", AssetDatabase.LoadAssetAtPath<SoundBank>(c_SoundBank) ?? BuildSoundBank()));
            return manager;
        }
    }
}
