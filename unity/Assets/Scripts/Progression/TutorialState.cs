using System;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>The tutorial hint on screen, shared by the map scene (which runs the tutorial) and the HUD (which shows it).</summary>
    [CreateAssetMenu(fileName = "TutorialState", menuName = "ZombiCar/State/Tutorial State")]
    public sealed class TutorialState : ScriptableObject
    {
        [NonSerialized] private string m_Text;
        [NonSerialized] private int m_Step;
        [NonSerialized] private int m_Steps;

        public event Action Changed;

        /// <summary>The hint shown now; empty when there is none.</summary>
        public string Text => m_Text ?? string.Empty;
        public bool Visible => !string.IsNullOrEmpty(m_Text);
        public int Step => m_Step;
        public int Steps => m_Steps;

        public void Show(string text, int step, int steps)
        {
            m_Text = text;
            m_Step = step;
            m_Steps = steps;
            Changed?.Invoke();
        }

        public void Hide()
        {
            m_Text = null;
            Changed?.Invoke();
        }
    }
}
