using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// One card of the map selection: title, picture, a Day / Night switch (cross-fades the day and night pictures)
    /// and Play, which loads the day or the night map. A locked card ("coming soon") has no maps, no Day / Night
    /// switch and a disabled Play (its references are [OptionalReference]).
    /// </summary>
    public sealed class MapCard : MonoBehaviour
    {
        [SerializeField, OptionalReference] private MapConfig m_DayMap;
        [SerializeField, OptionalReference] private MapConfig m_NightMap;
        [SerializeField, OptionalReference] private CanvasGroup m_NightPicture;
        [SerializeField, OptionalReference] private Button m_DayButton;
        [SerializeField, OptionalReference] private Button m_NightButton;
        [SerializeField, OptionalReference] private Image m_DayButtonPlate;
        [SerializeField, OptionalReference] private Image m_NightButtonPlate;
        [SerializeField] private Button m_PlayButton;
        [SerializeField] private Color m_OnColor = new Color(0.93f, 0.47f, 0.13f, 1f);
        [SerializeField] private Color m_OffColor = new Color(0.12f, 0.13f, 0.16f, 0.95f);
        [SerializeField, Min(0.01f)] private float m_CrossFade = 0.25f;

        private bool m_Night;

        public bool IsLocked => m_DayMap == null;
        public bool IsNight => m_Night;

        private void Awake()
        {
            if (m_DayButton != null)
                m_DayButton.onClick.AddListener(() => SetNight(false));
            if (m_NightButton != null)
                m_NightButton.onClick.AddListener(() => SetNight(true));
            if (m_PlayButton != null)
            {
                m_PlayButton.interactable = !IsLocked;
                m_PlayButton.onClick.AddListener(Play);
            }
            SetNight(false, true);
        }

        public void SetNight(bool night) => SetNight(night, false);

        private void SetNight(bool night, bool instant)
        {
            m_Night = night && m_NightMap != null;
            if (instant && m_NightPicture != null)
                m_NightPicture.alpha = m_Night ? 1f : 0f;
            if (m_DayButtonPlate != null)
                m_DayButtonPlate.color = m_Night ? m_OffColor : m_OnColor;
            if (m_NightButtonPlate != null)
                m_NightButtonPlate.color = m_Night ? m_OnColor : m_OffColor;
        }

        private void Update()
        {
            if (m_NightPicture == null)
                return;
            m_NightPicture.alpha = Mathf.MoveTowards(m_NightPicture.alpha, m_Night ? 1f : 0f, Time.unscaledDeltaTime / m_CrossFade);
        }

        private void Play()
        {
            MapConfig map = m_Night ? m_NightMap : m_DayMap;
            if (map == null)
                return;
            m_PlayButton.interactable = false;
            SceneLoader.EnsureExists().LoadMap(map);
        }
    }
}
