using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>The game's named sound effects (played through <see cref="AudioManager.PlaySfx"/>).</summary>
    public enum Sound
    {
        None,
        // UI.
        UiClick,
        UiOpen,
        UiClose,
        UiBuy,
        UiDenied,
        // Run.
        LevelUp,
        CardPick,
        WaveStart,
        WaveEnd,
        Victory,
        Defeat,
        // Car and player.
        CarShock,
        CarHit,
        PlayerHurt,
        AmmoRefill,
        // Weapons and props.
        Explosion,
        AxeSwing,
        AxeHit,
        WeaponSwitch,
        // Bosses.
        BossRoar,
        BossAttack,
        BossDeath,
        BossSummon,
        BossSpit,
        SpitSplat,
        BossCharge,
        BossSlam,
    }

    /// <summary>
    /// Clips of every <see cref="Sound"/> with their volume, pitch variation and the shortest time between two plays
    /// (so a crowd of zombies hitting the car does not stack dozens of copies). Built by the editor's AudioBuilder from
    /// Assets/Audio/SFX (tools/audio/make_sfx.py).
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "ZombiCar/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public Sound Id;
            [Tooltip("One is picked at random on every play.")]
            public AudioClip[] Clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float Volume = 1f;
            [Range(0f, 0.5f)] public float PitchJitter;
            [Tooltip("Seconds (real time) before the same sound may play again.")]
            [Min(0f)] public float MinInterval;
        }

        [SerializeField] private Entry[] m_Entries = Array.Empty<Entry>();

        [NonSerialized] private Dictionary<Sound, Entry> m_ById;

        public Entry Get(Sound id)
        {
            if (m_ById == null)
            {
                m_ById = new Dictionary<Sound, Entry>();
                foreach (Entry e in m_Entries)
                {
                    if (e != null && e.Clips != null && e.Clips.Length > 0)
                        m_ById[e.Id] = e;
                }
            }
            return m_ById.TryGetValue(id, out Entry entry) ? entry : null;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(Entry[] entries)
        {
            m_Entries = entries;
            m_ById = null;
        }

        public Entry[] EditorEntries => m_Entries;
#endif
    }
}
