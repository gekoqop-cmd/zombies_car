using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombiCar.Progression;

namespace ZombiCar.UI
{
    /// <summary>
    /// The level-up cards the run has taken: the card's icon on a disc in its colour and how many times it was taken
    /// (gold once maxed out). Rows are clones of a hidden template, laid out by the parent's layout group; a newly
    /// taken card pops.
    /// </summary>
    public sealed class PerkTrayView : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private PerkPool m_Pool;
        [Tooltip("Hidden template item; its Disc / Icon / Level are the fields below.")]
        [SerializeField] private RectTransform m_Template;
        [SerializeField] private Image m_Disc;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_Level;
        [SerializeField] private Color m_MaxedColor = new Color(1f, 0.8f, 0.3f, 1f);
        [SerializeField, Min(0.01f)] private float m_PopTime = 0.35f;

        private sealed class Item
        {
            public RectTransform Root;
            public Image Disc;
            public Image Icon;
            public TMP_Text Level;
            public int Stacks;
            public float Pop;
        }

        private readonly Dictionary<Perk, Item> m_Items = new Dictionary<Perk, Item>();
        private readonly List<Item> m_Popping = new List<Item>();
        private string m_DiscPath;
        private string m_IconPath;
        private string m_LevelPath;

        private void Awake()
        {
            m_DiscPath = PathOf(m_Disc.transform);
            m_IconPath = PathOf(m_Icon.transform);
            m_LevelPath = PathOf(m_Level.transform);
            m_Template.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_Run.ModifiersChanged += Refresh;
            m_Run.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            m_Run.ModifiersChanged -= Refresh;
            m_Run.Changed -= Refresh;
        }

        private void Refresh()
        {
            foreach (Perk perk in m_Pool.Perks)
            {
                if (perk == null)
                    continue;
                int stacks = m_Run.StacksOf(perk);
                m_Items.TryGetValue(perk, out Item item);
                if (stacks <= 0)
                {
                    if (item != null)
                        item.Root.gameObject.SetActive(false);
                    continue;
                }
                if (item == null)
                    m_Items[perk] = item = Create(perk);
                if (!item.Root.gameObject.activeSelf)
                {
                    // Taken again after a fresh run: back at the end of the row.
                    item.Root.gameObject.SetActive(true);
                    item.Root.SetAsLastSibling();
                }
                if (item.Stacks != stacks)
                {
                    item.Stacks = stacks;
                    bool maxed = perk.MaxStacks > 0 && stacks >= perk.MaxStacks;
                    item.Level.SetText(maxed ? "MAX" : stacks.ToString());
                    item.Level.color = maxed ? m_MaxedColor : Color.white;
                    item.Pop = 0f;
                    if (!m_Popping.Contains(item))
                        m_Popping.Add(item);
                }
            }
        }

        private Item Create(Perk perk)
        {
            RectTransform root = Instantiate(m_Template, m_Template.parent);
            root.name = perk.Id;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            var item = new Item
            {
                Root = root,
                Disc = Child(root, m_DiscPath).GetComponent<Image>(),
                Icon = Child(root, m_IconPath).GetComponent<Image>(),
                Level = Child(root, m_LevelPath).GetComponent<TMP_Text>(),
            };
            item.Disc.color = perk.Color;
            item.Icon.sprite = perk.Icon;
            return item;
        }

        private void Update()
        {
            for (int i = m_Popping.Count - 1; i >= 0; i--)
            {
                Item item = m_Popping[i];
                item.Pop = Mathf.Min(1f, item.Pop + Time.unscaledDeltaTime / m_PopTime);
                item.Root.localScale = Vector3.one * (1f + UiMotion.Punch(item.Pop) * 0.4f);
                if (item.Pop >= 1f)
                {
                    item.Root.localScale = Vector3.one;
                    m_Popping.RemoveAt(i);
                }
            }
        }

        private static Transform Child(Transform root, string path) => path.Length == 0 ? root : root.Find(path);

        /// <summary>Path from the template to <paramref name="t"/> ("" for the template itself).</summary>
        private string PathOf(Transform t)
        {
            string path = string.Empty;
            for (; t != null && t != m_Template; t = t.parent)
                path = path.Length == 0 ? t.name : t.name + "/" + path;
            return path;
        }
    }
}
