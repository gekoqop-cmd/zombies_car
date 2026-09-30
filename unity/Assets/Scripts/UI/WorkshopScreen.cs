using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
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
        [Tooltip("The supplies (first-aid kit, repair, ammo, mines), in the catalog's order.")]
        [SerializeField] private WorkshopRowView[] m_SupplyRows = System.Array.Empty<WorkshopRowView>();
        [Tooltip("To tell whether a supply is needed now.")]
        [SerializeField] private PlayerStats m_Player;
        [SerializeField] private AmmoState m_Ammo;
        [Tooltip("Mines are not sold while this many still lie around the car.")]
        [SerializeField, Min(1)] private int m_MaxArmedMines = 8;
        [SerializeField] private Button m_CloseButton;
        [SerializeField, Min(0.01f)] private float m_FadeTime = 0.18f;

        private bool m_Open;
        private float m_OpenTime;

        public bool IsOpen => m_Open;
        public CarUpgradeCatalog Catalog => m_Catalog;
        public RunState Run => m_Run;

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
            for (int i = 0; i < m_SupplyRows.Length; i++)
            {
                int index = i;
                m_SupplyRows[i].BuyButton.onClick.AddListener(() => BuySupply(index));
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
            if (m_Player != null)
                m_Player.Changed += Refresh;
            if (m_Ammo != null)
                m_Ammo.Changed += Refresh;
            OnWorkshopChanged();
        }

        private void OnDisable()
        {
            m_Run.WorkshopChanged -= OnWorkshopChanged;
            m_Run.Changed -= Refresh;
            m_Run.ModifiersChanged -= Refresh;
            if (m_Car != null)
                m_Car.Changed -= Refresh;
            if (m_Player != null)
                m_Player.Changed -= Refresh;
            if (m_Ammo != null)
                m_Ammo.Changed -= Refresh;
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
            return Purchase(m_Catalog.Upgrades[index], m_Rows[index]);
        }

        /// <summary>Buys the supply of row <paramref name="index"/> (buttons, tests); refused when it is not needed now.</summary>
        public bool BuySupply(int index)
        {
            if (!m_Open || index < 0 || index >= m_Catalog.Supplies.Length)
                return false;
            CarUpgrade supply = m_Catalog.Supplies[index];
            WorkshopRowView row = index < m_SupplyRows.Length ? m_SupplyRows[index] : null;
            return supply.ForAd ? WatchAdFor(supply, row) : Purchase(supply, row, IsUseful(supply));
        }

        /// <summary>
        /// A supply paid with a rewarded ad (the repair): only while it is useful and the run's uses are left; the
        /// effect comes when the platform confirms the reward (a closed or failed video gives nothing, the tile stays).
        /// </summary>
        private bool WatchAdFor(CarUpgrade supply, WorkshopRowView row)
        {
            if (m_Run.LevelOf(supply) >= supply.AdUsesPerRun || !IsUseful(supply))
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                Refused?.Invoke();
                return false;
            }
            YandexSdk.ShowRewardedAd(rewarded =>
            {
                if (!rewarded || this == null || m_Run.LevelOf(supply) >= supply.AdUsesPerRun || !m_Run.GrantUpgrade(supply))
                    return;
                AudioManager.PlaySfx(BoughtSound(supply));
                Debug.Log($"[WorkshopScreen] {supply.Id} for an ad (#{m_Run.LevelOf(supply)} of {supply.AdUsesPerRun}).");
                if (row != null)
                    row.Punch();
                Refresh();
                Bought?.Invoke(supply);
            });
            return true;
        }

        private bool Purchase(CarUpgrade upgrade, WorkshopRowView row, bool useful = true)
        {
            if (!useful || !m_Run.TryBuy(upgrade))
            {
                AudioManager.PlaySfx(Sound.UiDenied);
                Refused?.Invoke();
                return false;
            }
            AudioManager.PlaySfx(BoughtSound(upgrade));
            Debug.Log($"[WorkshopScreen] Bought {upgrade.Id} (#{m_Run.LevelOf(upgrade)}), scrap left {m_Run.Money}.");
            if (row != null)
                row.Punch();
            Refresh();
            Bought?.Invoke(upgrade);
            return true;
        }

        /// <summary>Each upgrade and supply has its own sound (what it does, then the "upgraded" sting).</summary>
        public static Sound BoughtSound(CarUpgrade upgrade) => upgrade.Effect switch
        {
            StatEffect.CarArmor => Sound.UpgradeArmor,
            StatEffect.CarMaxHealth => Sound.UpgradeHull,
            StatEffect.CarSpikes => Sound.UpgradeSpikes,
            StatEffect.CarShock => Sound.UpgradeShock,
            StatEffect.MaxReserve => Sound.UpgradeAmmoCase,
            StatEffect.HealPlayerFull or StatEffect.StoreMedkit => Sound.SupplyMedkit,
            StatEffect.RefillAmmo => Sound.SupplyAmmoBox,
            StatEffect.PlaceMines => Sound.SupplyMines,
            StatEffect.RepairCar => Sound.SupplyRepair,
            _ => Sound.UiBuy,
        };

        /// <summary>Whether a supply would do anything now ("НЕ НУЖНО" otherwise).</summary>
        public bool IsUseful(CarUpgrade supply)
        {
            switch (supply.Effect)
            {
                case StatEffect.HealPlayerFull:
                    return m_Player == null || m_Player.Health < m_Player.MaxHealth;
                case StatEffect.StoreMedkit:
                    return m_Run.Medkits < RunState.MaxMedkits;
                case StatEffect.RepairCar:
                    return m_Car == null || m_Car.Health < m_Car.MaxHealth;
                case StatEffect.RefillAmmo:
                    return m_Ammo == null || !m_Ammo.IsFull;
                case StatEffect.PlaceMines:
                    return m_Run.ArmedMines < m_MaxArmedMines;                // a free place round the car
                default:
                    return true;
            }
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
            CarUpgrade[] supplies = m_Catalog.Supplies;
            for (int i = 0; i < m_SupplyRows.Length; i++)
            {
                bool used = i < supplies.Length;
                m_SupplyRows[i].gameObject.SetActive(used);
                if (used)
                    m_SupplyRows[i].Bind(supplies[i], m_Run.LevelOf(supplies[i]), m_Run.Money, IsUseful(supplies[i]),
                        supplies[i].Effect == StatEffect.StoreMedkit ? $"В ЗАПАСЕ: {m_Run.Medkits}/{RunState.MaxMedkits}" : null);
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
