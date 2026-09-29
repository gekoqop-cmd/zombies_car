using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ТАБЛИЦА ЛИДЕРОВ" (main menu and the victory screen): the top 10 of the Yandex Games "best_time" leaderboard —
    /// place, avatar (loaded from the SDK's URL, a placeholder otherwise), name and time (mm:ss) — and the player's own
    /// line under it when they are not in the top. While the answer comes it says "ЗАГРУЗКА…"; with nothing to show
    /// (no SDK — the editor, a local build —, an error, an empty table) it says so. Runs on unscaled time.
    /// </summary>
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject Root;
            public Image Plate;
            public TMP_Text Rank;
            public Image Avatar;
            public TMP_Text Name;
            public TMP_Text Time;
        }

        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private Row[] m_Rows = Array.Empty<Row>();
        [Tooltip("The player's own line when they are not in the top.")]
        [SerializeField] private Row m_PlayerRow;
        [SerializeField] private TMP_Text m_Status;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Sprite m_AvatarPlaceholder;
        [SerializeField] private Color m_RowColor = new Color(0.03f, 0.03f, 0.05f, 0.6f);
        [SerializeField] private Color m_PlayerColor = new Color(0.93f, 0.47f, 0.13f, 0.75f);
        [SerializeField] private Color[] m_MedalColors =
        {
            new Color(1f, 0.82f, 0.3f, 1f), new Color(0.85f, 0.88f, 0.92f, 1f), new Color(0.85f, 0.55f, 0.3f, 1f),
        };
        [Tooltip("Real seconds to wait for the SDK before giving up.")]
        [SerializeField, Min(1f)] private float m_Timeout = 10f;

        private static readonly Dictionary<string, Sprite> s_Avatars = new Dictionary<string, Sprite>();

        private bool m_Open;
        private int m_Request;
        private float m_OpenedAt;

        public bool IsOpen => m_Open;

        /// <summary>What the panel shows now (tests): loading, empty / unavailable, or the table.</summary>
        public enum State { Hidden, Loading, Empty, Table }

        public State Current { get; private set; } = State.Hidden;

        private void Awake()
        {
            m_CloseButton.onClick.AddListener(Close);
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            m_Open = true;
            m_OpenedAt = Time.unscaledTime;
            m_Group.blocksRaycasts = m_Group.interactable = true;
            AudioManager.PlaySfx(Sound.UiOpen);
            ShowRows(null, null);
            SetStatus(State.Loading, "ЗАГРУЗКА…");
            int request = ++m_Request;
            YandexSdk.GetLeaderboardEntries(m_Rows.Length, result =>
            {
                if (this != null && m_Open && request == m_Request)
                    Show(result);
            });
        }

        public void Close()
        {
            if (!m_Open)
                return;
            m_Open = false;
            m_Request++;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            Current = State.Hidden;
            AudioManager.PlaySfx(Sound.UiClose);
        }

        private void Show(YandexSdk.LeaderboardEntries result)
        {
            if (result == null || !result.available)
            {
                SetStatus(State.Empty, "ТАБЛИЦА ДОСТУПНА В ЯНДЕКС ИГРАХ\nПОСЛЕ ВХОДА В АККАУНТ");
                return;
            }
            if (result.entries.Length == 0)
            {
                SetStatus(State.Empty, "ПОКА НИКТО НЕ ПРОШЁЛ ВСЕ 30 ВОЛН.\nСТАНЬ ПЕРВЫМ!");
                return;
            }
            // The top rows by place; the player's line under them only when they are further down.
            var top = new List<YandexSdk.LeaderboardEntry>(result.entries);
            top.RemoveAll(e => e.rank < 1 || e.rank > m_Rows.Length);
            top.Sort((a, b) => a.rank.CompareTo(b.rank));
            YandexSdk.LeaderboardEntry player = result.Player != null && result.Player.rank > m_Rows.Length ? result.Player : null;
            ShowRows(top, player);
            SetStatus(State.Table, string.Empty);
        }

        private void ShowRows(List<YandexSdk.LeaderboardEntry> top, YandexSdk.LeaderboardEntry player)
        {
            for (int i = 0; i < m_Rows.Length; i++)
            {
                YandexSdk.LeaderboardEntry e = top != null && i < top.Count ? top[i] : null;
                m_Rows[i].Root.SetActive(e != null);
                if (e != null)
                    Fill(m_Rows[i], e, false);
            }
            m_PlayerRow.Root.SetActive(player != null);
            if (player != null)
                Fill(m_PlayerRow, player, true);
        }

        private void Fill(Row row, YandexSdk.LeaderboardEntry e, bool player)
        {
            row.Rank.SetText(e.rank.ToString());
            row.Rank.color = e.rank >= 1 && e.rank <= m_MedalColors.Length ? m_MedalColors[e.rank - 1] : Color.white;
            row.Name.SetText(string.IsNullOrEmpty(e.name) ? "ИГРОК" : e.name.ToUpperInvariant());
            row.Time.SetText(MetaProgress.FormatTime(e.score));
            row.Plate.color = player ? m_PlayerColor : m_RowColor;
            row.Avatar.sprite = m_AvatarPlaceholder;
            if (!string.IsNullOrEmpty(e.avatar))
            {
                if (s_Avatars.TryGetValue(e.avatar, out Sprite cached))
                    row.Avatar.sprite = cached;
                else
                    StartCoroutine(LoadAvatar(e.avatar, row.Avatar));
            }
        }

        private IEnumerator LoadAvatar(string url, Image target)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    yield break;                                          // the placeholder stays
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                s_Avatars[url] = sprite;
                if (target != null)
                    target.sprite = sprite;
            }
        }

        private void SetStatus(State state, string text)
        {
            Current = state;
            m_Status.gameObject.SetActive(state != State.Table);
            m_Status.SetText(text);
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            m_Window.localScale = Vector3.one * (m_Open ? Mathf.Lerp(0.9f, 1f, UiMotion.BackOut((Time.unscaledTime - m_OpenedAt) / 0.3f, 1.6f)) : 1f);
            if (!m_Open && m_Group.alpha <= 0f)
                gameObject.SetActive(false);
            if (m_Open && Current == State.Loading && Time.unscaledTime - m_OpenedAt > m_Timeout)
            {
                m_Request++;                                              // a late answer is ignored
                SetStatus(State.Empty, "НЕ УДАЛОСЬ ЗАГРУЗИТЬ ТАБЛИЦУ");
            }
            UnityEngine.InputSystem.Keyboard keys = UnityEngine.InputSystem.Keyboard.current;
            if (m_Open && keys != null && keys.escapeKey.wasPressedThisFrame)
                Close();
        }
    }
}
