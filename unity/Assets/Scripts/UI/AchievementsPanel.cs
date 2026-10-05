using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Core;

namespace ZombiCar.UI
{
    /// <summary>
    /// "ДОСТИЖЕНИЯ" (main menu): a compact plate per achievement (<see cref="Achievements.All"/>; one for now —
    /// "БЕЗУМЕЦ!"). Locked: grey, a padlock, the reward still shown ("676" and the coin). Unlocked: the reward glows and
    /// a tap on it takes the coins. Taken: "ПОЛУЧЕНО". Runs on unscaled time.
    /// </summary>
    public sealed class AchievementsPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Item
        {
            public Image Plate;
            public GameObject Lock;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Description;
            public Button Reward;
            public Image RewardPlate;
            public Image RewardGlow;
            public TMP_Text RewardText;
            public TMP_Text Taken;
            [Tooltip("The coin after the reward: gone with the number once taken.")]
            public GameObject Coin;
        }

        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Item[] m_Items = Array.Empty<Item>();
        [SerializeField] private Color m_LockedPlate = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        [SerializeField] private Color m_OpenPlate = new Color(0.3f, 0.2f, 0.06f, 0.92f);
        [SerializeField] private Color m_RewardOn = new Color(1f, 0.78f, 0.25f, 1f);
        [SerializeField] private Color m_RewardOff = new Color(0.36f, 0.36f, 0.4f, 1f);

        private bool m_Open;
        private float m_OpenedAt;

        public bool IsOpen => m_Open;

        private void Awake()
        {
            m_CloseButton.onClick.AddListener(Close);
            for (int i = 0; i < m_Items.Length; i++)
            {
                int index = i;
                m_Items[i].Reward.onClick.AddListener(() => Claim(index));
            }
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
            Refresh();
        }

        public void Close()
        {
            if (!m_Open)
                return;
            m_Open = false;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            AudioManager.PlaySfx(Sound.UiClose);
        }

        /// <summary>Takes achievement <paramref name="index"/>'s coins when it is unlocked and not taken (buttons, tests).</summary>
        public bool Claim(int index)
        {
            if (index < 0 || index >= m_Items.Length || index >= Achievements.All.Length)
                return false;
            Achievements.Info info = Achievements.All[index];
            if (!MetaProgress.ClaimAchievement(info.Id, info.Reward))
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                return false;
            }
            AudioManager.PlaySfx(Sound.UiBuy);
            Refresh();
            return true;
        }

        private void Refresh()
        {
            for (int i = 0; i < m_Items.Length && i < Achievements.All.Length; i++)
            {
                Achievements.Info info = Achievements.All[i];
                Item item = m_Items[i];
                bool achieved = MetaProgress.IsAchieved(info.Id), taken = MetaProgress.IsClaimed(info.Id);
                item.Title.SetText(info.Title);
                item.Description.SetText(info.Description);
                item.RewardText.SetText(info.Reward.ToString());
                item.Plate.color = achieved ? m_OpenPlate : m_LockedPlate;
                item.Lock.SetActive(!achieved);
                item.Icon.color = achieved ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
                item.Reward.interactable = achieved && !taken;
                item.RewardPlate.color = achieved && !taken ? m_RewardOn : m_RewardOff;
                item.Taken.gameObject.SetActive(taken);
                item.RewardText.gameObject.SetActive(!taken);
                if (item.Coin != null)
                    item.Coin.SetActive(!taken);
            }
        }

        private void Update()
        {
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, m_Open ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            m_Window.localScale = Vector3.one * (m_Open ? Mathf.Lerp(0.9f, 1f, UiMotion.BackOut((Time.unscaledTime - m_OpenedAt) / 0.3f, 1.6f)) : 1f);
            if (!m_Open && m_Group.alpha <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            // A reward waiting to be taken glows and swells.
            float now = Time.unscaledTime;
            foreach (Item item in m_Items)
            {
                bool ready = item.Reward.interactable;
                Color glow = item.RewardGlow.color;
                glow.a = ready ? 0.55f + 0.45f * Mathf.Sin(now * 5f) : 0f;
                item.RewardGlow.color = glow;
                item.Reward.transform.localScale = Vector3.one * (ready ? 1f + 0.06f * Mathf.Sin(now * 5f) : 1f);
            }
        }
    }
}
