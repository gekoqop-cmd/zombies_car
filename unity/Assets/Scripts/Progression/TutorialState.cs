using System;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The tutorial on screen, shared by the map scene (which runs it) and the HUD / menu (which show it): the intro
    /// cards (open / closed) and the live hint of the moment.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialState", menuName = "ZombiCar/State/Tutorial State")]
    public sealed class TutorialState : ScriptableObject
    {
        [NonSerialized] private string m_Text;
        [NonSerialized] private int m_Step;
        [NonSerialized] private int m_Steps;
        [NonSerialized] private bool m_CardsOpen;

        public event Action Changed;

        /// <summary>The intro cards were closed (read to the end or skipped).</summary>
        public event Action CardsClosed;

        /// <summary>The hint shown now; empty when there is none.</summary>
        public string Text => m_Text ?? string.Empty;
        public bool Visible => !string.IsNullOrEmpty(m_Text);

        /// <summary>Step of a numbered hint; 0 for a live hint (shown as "ПОДСКАЗКА").</summary>
        public int Step => m_Step;
        public int Steps => m_Steps;

        /// <summary>The intro cards are on screen (the game waits).</summary>
        public bool CardsOpen => m_CardsOpen;

        public void Show(string text, int step = 0, int steps = 0)
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

        public void OpenCards()
        {
            m_CardsOpen = true;
            m_Text = null;
            Changed?.Invoke();
        }

        public void CloseCards()
        {
            if (!m_CardsOpen)
                return;
            m_CardsOpen = false;
            Changed?.Invoke();
            CardsClosed?.Invoke();
        }
    }
}
