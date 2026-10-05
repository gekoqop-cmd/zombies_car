using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The run's difficulty across the screen before the waves start, every run (also one going on from its
    /// checkpoint): "РЕЖИМ: ХАРДКОР" in the difficulty's colour, its face and what it means ("БОЛЬШЕ ЗОМБИ, БОЛЬШЕ
    /// НАГРАДЫ"). It waits for the tutorial cards or any pause to go, then pops in, holds and fades; it never takes
    /// clicks. Runs on unscaled time.
    /// </summary>
    public sealed class ModeBanner : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private Image m_Plate;
        [SerializeField] private Image m_Face;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Tagline;
        [Tooltip("The faces in the Difficulty order: easy, normal, hardcore.")]
        [SerializeField] private Sprite[] m_Faces = System.Array.Empty<Sprite>();
        [SerializeField, Min(0f)] private float m_Delay = 0.5f;
        [SerializeField, Min(0.01f)] private float m_In = 0.3f;
        [SerializeField, Min(0f)] private float m_Hold = 2.4f;
        [SerializeField, Min(0.01f)] private float m_Out = 0.6f;

        private float m_ShownAt = float.PositiveInfinity;
        private float m_ReadyAt = -1f;

        /// <summary>The banner has been shown in this run (tests).</summary>
        public bool Shown => m_ShownAt < float.PositiveInfinity;

        public string Title => m_Title.text;

        private void Start()
        {
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            Difficulty d = m_Run.Difficulty;
            Color color = DifficultyRules.Color(d);
            m_Title.SetText($"РЕЖИМ: {DifficultyRules.Title(d)}");
            m_Title.color = Color.Lerp(color, Color.white, 0.25f);
            m_Tagline.SetText(DifficultyRules.Tagline(d));
            Color plate = color * 0.55f;
            plate.a = 0.92f;
            m_Plate.color = plate;
            int i = (int)d;
            m_Face.sprite = i < m_Faces.Length ? m_Faces[i] : null;
            m_Face.enabled = m_Face.sprite != null;
        }

        private void Update()
        {
            if (!Shown)
            {
                // After the tutorial cards and any other pause: the first thing seen when the game runs.
                if (GamePause.IsPaused || m_Run.IsOver)
                {
                    m_ReadyAt = -1f;
                    return;
                }
                if (m_ReadyAt < 0f)
                    m_ReadyAt = Time.unscaledTime + m_Delay;
                if (Time.unscaledTime < m_ReadyAt)
                    return;
                m_ShownAt = Time.unscaledTime;
                AudioManager.PlaySfx(Sound.WaveStart);
            }
            float t = Time.unscaledTime - m_ShownAt;
            float alpha = t < m_In ? t / m_In : t < m_In + m_Hold ? 1f : 1f - (t - m_In - m_Hold) / m_Out;
            m_Group.alpha = Mathf.Clamp01(alpha);
            m_Body.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, UiMotion.BackOut(t / m_In, 2.2f));
            if (m_Face.enabled)
                m_Face.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(Time.unscaledTime * 3f));
            if (alpha <= 0f && t > m_In)
                enabled = false;
        }
    }
}
