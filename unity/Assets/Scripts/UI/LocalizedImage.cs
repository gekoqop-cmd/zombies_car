using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>A picture with words on it (the logo): the English one in English, when there is one.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class LocalizedImage : MonoBehaviour
    {
        [SerializeField] private Sprite m_Russian;
        [SerializeField, OptionalReference] private Sprite m_English;

        private void OnEnable()
        {
            Loc.Changed += Show;
            Show();
        }

        private void OnDisable() => Loc.Changed -= Show;

        private void Show()
        {
            Loc.EnsureLoaded();
            GetComponent<Image>().sprite = Loc.IsEnglish && m_English != null ? m_English : m_Russian;
        }
    }
}
