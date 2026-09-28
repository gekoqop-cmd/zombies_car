using System.Collections.Generic;
using UnityEngine;

namespace ZombiCar.Progression
{
    /// <summary>
    /// Something on the map the player can use with the interact key (E / the touch action button) when close enough:
    /// the workshop, the car's ammo refill. The nearest one in range is offered by <see cref="PlayerInteractor"/>,
    /// which shows its prompt on the HUD. An interaction is instant or takes <see cref="Duration"/> seconds, during
    /// which the player stands still.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> s_All = new List<Interactable>();

        [Tooltip("Distance from the player at which the prompt shows, metres.")]
        [SerializeField, Min(0.1f)] private float m_Radius = 2.5f;
        [Tooltip("Pictogram of the interaction (the touch action button shows it).")]
        [SerializeField] private Sprite m_Icon;

        public static IReadOnlyList<Interactable> All => s_All;

        public float Radius => m_Radius;
        public Sprite Icon => m_Icon;

        /// <summary>Seconds the interaction takes (0 = instant).</summary>
        public virtual float Duration => 0f;

        /// <summary>Whether the prompt is offered at all right now (false: the player walks by without a prompt).</summary>
        public virtual bool IsOffered => true;

        protected virtual void OnEnable() => s_All.Add(this);

        protected virtual void OnDisable() => s_All.Remove(this);

        /// <summary>Planar distance from <paramref name="position"/> to where the interaction is (its edge for big objects).</summary>
        public virtual float DistanceTo(Vector3 position)
        {
            Vector3 d = position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>The prompt to show and whether the interaction can be used now (otherwise the prompt says why not).</summary>
        public abstract bool GetPrompt(out string text);

        /// <summary>The interaction starts (a timed one: before its duration; the player may still cancel by moving away).</summary>
        public virtual void Begin(PlayerInteractor player)
        {
        }

        /// <summary>A timed interaction was interrupted.</summary>
        public virtual void Cancel(PlayerInteractor player)
        {
        }

        /// <summary>The interaction is done (at once, or when its duration has passed).</summary>
        public abstract void Complete(PlayerInteractor player);
    }
}
