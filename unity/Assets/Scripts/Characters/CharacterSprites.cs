using System;
using UnityEngine;

namespace ZombiCar.Characters
{
    public enum CharacterAnim
    {
        Idle,
        Move,
        Attack,
        Hit,
        Dead,
        Shoot,
        Reload,
        TurnUp,
        TurnDown,

        // Boss abilities (appended: the values above are serialized). A character without such a clip plays its
        // stand-in (CharacterSprites.Fallback).
        Summon,
        Spit,
        ChargeWindup,
        Charge,
        Stun,
        Cracks,
        Vanish,
        Appear,
    }

    /// <summary>
    /// Frame animations of one 2D character (frames cut from referens/characters.png by tools/characters).
    /// Built by Tools/Build Prefabs; every clip is a list of sprites played at a fixed rate.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSprites", menuName = "ZombiCar/Character Sprites")]
    public sealed class CharacterSprites : ScriptableObject
    {
        [Serializable]
        public sealed class Clip
        {
            [SerializeField] private CharacterAnim m_Anim;
            [SerializeField] private Sprite[] m_Frames;
            [SerializeField, Min(0.1f)] private float m_Fps = 6f;
            [SerializeField] private bool m_Loop = true;
            [Tooltip("Share of the clip at which its blow lands (the strike frame); -1: none.")]
            [SerializeField] private float m_Strike = -1f;

            public Clip(CharacterAnim anim, Sprite[] frames, float fps, bool loop, float strike = -1f)
            {
                m_Anim = anim;
                m_Frames = frames;
                m_Fps = fps;
                m_Loop = loop;
                m_Strike = strike;
            }

            public CharacterAnim Anim => m_Anim;
            public Sprite[] Frames => m_Frames;
            public float Fps => m_Fps;
            public bool Loop => m_Loop;
            public float Duration => m_Frames.Length / m_Fps;

            /// <summary>Seconds into the clip (at speed 1) when its blow lands: the strike frame, else the end.</summary>
            public float StrikeTime => (m_Strike >= 0f ? m_Strike : 1f) * Duration;
        }

        [SerializeField] private Clip[] m_Clips;

        [NonSerialized] private Clip[] m_ByAnim;

        /// <summary>
        /// The clip for <paramref name="anim"/>; without one its stand-in (an ability: the attack, the charge: the walk,
        /// the stun: the hit pose), finally Idle.
        /// </summary>
        public Clip Get(CharacterAnim anim)
        {
            if (m_ByAnim == null)
            {
                m_ByAnim = new Clip[Enum.GetValues(typeof(CharacterAnim)).Length];
                foreach (Clip clip in m_Clips)
                {
                    if (clip != null && clip.Frames != null && clip.Frames.Length > 0)
                        m_ByAnim[(int)clip.Anim] = clip;
                }
            }
            for (CharacterAnim a = anim; ; a = Fallback(a))
            {
                Clip clip = m_ByAnim[(int)a];
                if (clip != null || a == CharacterAnim.Idle)
                    return clip ?? m_ByAnim[(int)CharacterAnim.Idle];
            }
        }

        /// <summary>The clip played for <paramref name="anim"/> when a character has none of its own.</summary>
        public static CharacterAnim Fallback(CharacterAnim anim) => anim switch
        {
            CharacterAnim.Summon or CharacterAnim.Spit or CharacterAnim.ChargeWindup or CharacterAnim.Cracks => CharacterAnim.Attack,
            CharacterAnim.Charge => CharacterAnim.Move,
            CharacterAnim.Stun => CharacterAnim.Hit,
            _ => CharacterAnim.Idle,
        };

#if UNITY_EDITOR
        public void EditorSetClips(Clip[] clips)
        {
            m_Clips = clips;
            m_ByAnim = null;
        }
#endif
    }
}
