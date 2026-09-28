using System;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The interaction prompt shared with the HUD scene: whether one is offered, its text, whether it can be used now,
    /// and the progress of a timed one. Written by <see cref="PlayerInteractor"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionState", menuName = "ZombiCar/State/Interaction State")]
    public sealed class InteractionState : ScriptableObject
    {
        [NonSerialized] private bool m_Visible;
        [NonSerialized] private string m_Text;
        [NonSerialized] private bool m_Available;
        [NonSerialized] private float m_Progress = -1f;
        [NonSerialized] private Sprite m_Icon;

        public event Action Changed;

        public bool Visible => m_Visible;
        public string Text => m_Text;
        public bool Available => m_Available;
        public Sprite Icon => m_Icon;

        /// <summary>0..1 while a timed interaction runs, -1 otherwise (read every frame by the HUD).</summary>
        public float Progress => m_Progress;

        public void Set(bool visible, string text, bool available, Sprite icon = null)
        {
            if (visible == m_Visible && available == m_Available && text == m_Text && icon == m_Icon)
                return;
            m_Visible = visible;
            m_Text = text;
            m_Available = available;
            m_Icon = icon;
            Changed?.Invoke();
        }

        public void SetProgress(float progress) => m_Progress = progress;

        public void Clear()
        {
            m_Progress = -1f;
            Set(false, null, false);
        }
    }
}
