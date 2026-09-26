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

            public Clip(CharacterAnim anim, Sprite[] frames, float fps, bool loop)
            {
                m_Anim = anim;
                m_Frames = frames;
                m_Fps = fps;
                m_Loop = loop;
            }

            public CharacterAnim Anim => m_Anim;
            public Sprite[] Frames => m_Frames;
            public float Fps => m_Fps;
            public bool Loop => m_Loop;
            public float Duration => m_Frames.Length / m_Fps;
        }

        [SerializeField] private Clip[] m_Clips;

        [NonSerialized] private Clip[] m_ByAnim;

        /// <summary>The clip for <paramref name="anim"/>, falling back to Idle when the character has no such clip.</summary>
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
            return m_ByAnim[(int)anim] ?? m_ByAnim[(int)CharacterAnim.Idle];
        }

#if UNITY_EDITOR
        public void EditorSetClips(Clip[] clips)
        {
            m_Clips = clips;
            m_ByAnim = null;
        }
#endif
    }
}
