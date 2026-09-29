using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Waves;

namespace ZombiCar.UI
{
    /// <summary>
    /// "РЕКОРДЫ" of one map (day or night), opened from its card in the map selection: picture tiles with the best time,
    /// the victories, the best wave, the most zombies and bosses in one run, the runs played (<see cref="MetaProgress.Records"/>).
    /// Runs on unscaled time.
    /// </summary>
    public sealed class MapRecordsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private TMP_Text m_Subtitle;
        [SerializeField] private TMP_Text m_Time;
        [SerializeField] private TMP_Text m_Wins;
        [SerializeField] private TMP_Text m_Wave;
        [SerializeField] private TMP_Text m_Kills;
        [SerializeField] private TMP_Text m_Bosses;
        [SerializeField] private TMP_Text m_Runs;
        [Tooltip("The tiles, popping in one by one.")]
        [SerializeField] private RectTransform[] m_Tiles = System.Array.Empty<RectTransform>();
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Button m_Backdrop;
        [SerializeField] private WaveConfig m_Waves;

        private bool m_Open;
        private float m_OpenedAt;

        public bool IsOpen => m_Open;

        private void Awake()
        {
            m_CloseButton.onClick.AddListener(Close);
            m_Backdrop.onClick.AddListener(Close);
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
        }

        /// <summary>Shows the records of <paramref name="map"/>; <paramref name="title"/> is "ГОРОД  •  ДЕНЬ".</summary>
        public void Open(MapConfig map, string title)
        {
            MetaProgress.MapRecords r = MetaProgress.Records(map.name);
            int waves = m_Waves != null ? m_Waves.WaveCount : 30;
            int bosses = m_Waves != null ? m_Waves.Bosses.Length : 3;
            m_Subtitle.SetText(title);
            m_Time.SetText(r.BestTime > 0f ? MetaProgress.FormatTime(r.BestTime) : "—");
            m_Wins.SetText(r.Wins.ToString());
            m_Wave.SetText(r.Runs > 0 ? $"{r.BestWave}<size=65%>/{waves}</size>" : "—");
            m_Kills.SetText(r.BestKills.ToString());
            m_Bosses.SetText($"{r.BestBosses}<size=65%>/{bosses}</size>");
            m_Runs.SetText(r.Runs.ToString());
            m_Open = true;
            m_OpenedAt = Time.unscaledTime;
            m_Group.blocksRaycasts = m_Group.interactable = true;
            AudioManager.PlaySfx(Sound.UiOpen);
        }

        public void Close()
        {
            if (!m_Open)
                return;
            m_Open = false;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            AudioManager.PlaySfx(Sound.UiClose);
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.18f);
            if (!m_Open)
                return;
            float t = Time.unscaledTime - m_OpenedAt;
            m_Window.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, UiMotion.BackOut(Mathf.Clamp01(t / 0.3f)));
            for (int i = 0; i < m_Tiles.Length; i++)
            {
                float k = Mathf.Clamp01((t - 0.12f - i * 0.07f) / 0.25f);
                m_Tiles[i].localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, UiMotion.BackOut(k));
            }
        }
    }
}
