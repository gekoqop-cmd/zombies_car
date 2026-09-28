using UnityEngine;
using ZombiCar.Core;
using ZombiCar.Player;

namespace ZombiCar.Progression
{
    /// <summary>
    /// On the player: finds the nearest <see cref="Interactable"/> in range, publishes its prompt to
    /// <see cref="InteractionState"/> for the HUD, and uses it on the interact input (E on the keyboard, the action
    /// button on touch screens). A timed interaction roots the player until it is done; walking out of range or the
    /// interactable becoming unavailable cancels it. Ignores input while the game is paused and on the frame a pause
    /// ended (the key that closed a pop-up must not open the next one).
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputProvider m_InputProvider;
        [SerializeField] private InteractionState m_State;
        [SerializeField] private RunState m_Run;

        private IPlayerInput m_Input;
        private Interactable m_Current;
        private Interactable m_Channel;
        private float m_ChannelTime;

        /// <summary>A timed interaction is running: the player stands still.</summary>
        public bool IsBusy => m_Channel != null;

        public Interactable Current => m_Current;

        private void Start() => m_Input = m_InputProvider != null ? m_InputProvider.Input : null;

        private void OnDisable()
        {
            CancelChannel();
            if (m_State != null)
                m_State.Clear();
        }

        private void Update()
        {
            if (m_Input == null)
                return;
            if (m_Run != null && m_Run.IsOver)
            {
                CancelChannel();
                m_State.Clear();
                return;
            }

            if (m_Channel != null)
            {
                UpdateChannel();
                return;
            }

            m_Current = Nearest();
            if (m_Current == null)
            {
                m_State.Set(false, null, false);
                return;
            }
            bool available = m_Current.GetPrompt(out string text);
            m_State.Set(true, text, available, m_Current.Icon);

            bool pressed = m_Input.InteractPressed && !GamePause.IsPaused && GamePause.ChangedFrame != Time.frameCount;
            if (pressed && available)
                Use(m_Current);
        }

        /// <summary>Uses <paramref name="target"/> now (also the test tools).</summary>
        public void Use(Interactable target)
        {
            target.Begin(this);
            if (target.Duration <= 0f)
            {
                target.Complete(this);
                return;
            }
            m_Channel = target;
            m_ChannelTime = 0f;
            m_State.SetProgress(0f);
        }

        private void UpdateChannel()
        {
            bool inRange = m_Channel != null && m_Channel.isActiveAndEnabled &&
                           m_Channel.DistanceTo(transform.position) <= m_Channel.Radius;
            bool available = inRange && m_Channel.GetPrompt(out string text);
            if (!available)
            {
                CancelChannel();
                return;
            }
            m_ChannelTime += Time.deltaTime;
            float progress = Mathf.Clamp01(m_ChannelTime / m_Channel.Duration);
            m_State.SetProgress(progress);
            if (progress < 1f)
                return;
            Interactable done = m_Channel;
            m_Channel = null;
            m_State.SetProgress(-1f);
            done.Complete(this);
        }

        private void CancelChannel()
        {
            if (m_Channel == null)
                return;
            Interactable cancelled = m_Channel;
            m_Channel = null;
            if (m_State != null)
                m_State.SetProgress(-1f);
            cancelled.Cancel(this);
        }

        private Interactable Nearest()
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            Vector3 position = transform.position;
            foreach (Interactable candidate in Interactable.All)
            {
                if (!candidate.IsOffered)
                    continue;
                float distance = candidate.DistanceTo(position);
                if (distance <= candidate.Radius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
