using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The workshop (HUD scene), opened by <see cref="WorkshopZone"/> through <see cref="RunState.WorkshopOpen"/>:
    /// the car upgrades of the <see cref="CarUpgradeCatalog"/> with their levels and prices, the scrap the player has
    /// and the car's current durability, armour, spikes and discharge. The game is paused while it is open; Done,
    /// Esc or E close it.
    /// </summary>
    public sealed class WorkshopScreen : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CarUpgradeCatalog m_Catalog;
        [SerializeField] private CarState m_Car;
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private RectTransform m_Window;
        [SerializeField] private TMP_Text m_Money;
        [SerializeField] private TMP_Text m_CarStatus;
        [SerializeField] private WorkshopRowView[] m_Rows = System.Array.Empty<WorkshopRowView>();
        [SerializeField] private Button m_CloseButton;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.18f;

        private bool m_Open;
        private float m_OpenTime;

        public bool IsOpen => m_Open;

        /// <summary>A purchase went through / was refused (sounds).</summary>
        public event System.Action<CarUpgrade> Bought;
        public event System.Action Refused;
        public event System.Action<bool> OpenChanged;

        private void Awake()
        {
            for (int i = 0; i < m_Rows.Length; i++)
            {
                int index = i;
                m_Rows[i].BuyButton.onClick.AddListener(() => Buy(index));
            }
            m_CloseButton.onClick.AddListener(Close);
            m_Root.alpha = 0f;
            m_Root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_Run.WorkshopChanged += OnWorkshopChanged;
            m_Run.Changed += Refresh;
            m_Run.ModifiersChanged += Refresh;
            if (m_Car != null)
                m_Car.Changed += Refresh;
            OnWorkshopChanged();
        }

        private void OnDisable()
        {
            m_Run.WorkshopChanged -= OnWorkshopChanged;
            m_Run.Changed -= Refresh;
            m_Run.ModifiersChanged -= Refresh;
            if (m_Car != null)
                m_Car.Changed -= Refresh;
            if (m_Open)
                GamePause.Set(GamePause.Reason.Workshop, false);
        }

        private void OnWorkshopChanged()
        {
            if (m_Run.WorkshopOpen == m_Open)
                return;
            m_Open = m_Run.WorkshopOpen;
            if (m_Open)
            {
                m_OpenTime = Time.unscaledTime;
                m_Root.gameObject.SetActive(true);
                Refresh();
            }
            m_Root.interactable = m_Root.blocksRaycasts = m_Open;
            GamePause.Set(GamePause.Reason.Workshop, m_Open);
            AudioManager.PlaySfx(m_Open ? Sound.UiOpen : Sound.UiClose);
            OpenChanged?.Invoke(m_Open);
        }

        /// <summary>Buys the upgrade of row <paramref name="index"/> (buttons, tests).</summary>
        public bool Buy(int index)
        {
            if (!m_Open || index < 0 || index >= m_Catalog.Upgrades.Length)
                return false;
            CarUpgrade upgrade = m_Catalog.Upgrades[index];
            if (!m_Run.TryBuy(upgrade))
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                Refused?.Invoke();
                return false;
            }
            AudioManager.PlaySfx(Sound.UiBuy);
            Debug.Log($"[WorkshopScreen] Bought {upgrade.Id} level {m_Run.LevelOf(upgrade)}, scrap left {m_Run.Money}.");
            m_Rows[index].Punch();
            Refresh();
            Bought?.Invoke(upgrade);
            return true;
        }

        public void Close() => m_Run.SetWorkshopOpen(false);

        private void Refresh()
        {
            if (!m_Open)
                return;
            m_Money.SetText(m_Run.Money.ToString());
            CarUpgrade[] upgrades = m_Catalog.Upgrades;
            for (int i = 0; i < m_Rows.Length; i++)
            {
                bool used = i < upgrades.Length;
                m_Rows[i].gameObject.SetActive(used);
                if (used)
                    m_Rows[i].Bind(upgrades[i], m_Run.LevelOf(upgrades[i]), m_Run.Money);
            }
            if (m_Car != null && m_CarStatus != null)
            {
                RunModifiers mods = m_Run.Modifiers;
                m_CarStatus.SetText(
                    $"ПРОЧНОСТЬ {m_Car.Health} / {m_Car.MaxHealth}    БРОНЯ {Mathf.RoundToInt(m_Car.Armor * 100f)}%    " +
                    $"ШИПЫ {m_Car.Spikes}    РАЗРЯД {mods.Whole(StatEffect.CarShock)}");
            }
        }

        private void Update()
        {
            float target = m_Open ? 1f : 0f;
            if (m_Root.gameObject.activeSelf)
            {
                m_Root.alpha = Mathf.MoveTowards(m_Root.alpha, target, Time.unscaledDeltaTime / m_FadeTime);
                float pop = UiMotion.BackOut((Time.unscaledTime - m_OpenTime) / 0.3f, 1.4f);
                if (m_Window != null)
                    m_Window.localScale = Vector3.one * (m_Open ? Mathf.Lerp(0.85f, 1f, pop) : 1f);
                if (!m_Open && m_Root.alpha <= 0f)
                    m_Root.gameObject.SetActive(false);
            }
            if (!m_Open)
                return;
            Keyboard keyboard = Keyboard.current;
            bool sameFrame = GamePause.ChangedFrame == Time.frameCount;
            if (keyboard != null && !sameFrame && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
                Close();
        }
    }
}
