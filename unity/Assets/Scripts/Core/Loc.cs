using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZombiCar.Core
{
    public enum GameLanguage
    {
        Russian,
        English,
    }

    /// <summary>
    /// The game's languages. The game is written in Russian: every text (code, prefabs, data assets) is Russian, and in
    /// English it is translated as it is drawn. Every TextMeshPro text gets a text preprocessor that swaps it for its
    /// translation (Resources/Localization/en.txt, "russian TAB english" per line, made by tools/loc/extract.py), so no
    /// label or <c>SetText</c> needs to know about languages. A text is looked up whole, then with its numbers as
    /// placeholders ("ВОЛНА 4" → "ВОЛНА {0}" → "WAVE {0}"), then against the templates with placeholders for any value
    /// ("СЛОЖНОСТЬ: {0}", its value translated in turn), then line by line and piece by piece between rich-text tags.
    /// The language: the player's choice (the menu's RU / EN switch, saved), else the platform's (Yandex SDK, or the
    /// system language): Russian for ru / be / kk / uk / uz, English for the rest.
    /// </summary>
    public static class Loc
    {
        private const string c_Key = "settings.language";
        private const string c_Table = "Localization/en";
        private static readonly Regex s_Number = new Regex(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
        private static readonly Regex s_Placeholder = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);
        private static readonly Regex s_Tag = new Regex(@"(<[^<>]+>)", RegexOptions.Compiled);
        // A run of Russian words (with the spaces and hyphens between them).
        private static readonly Regex s_Words = new Regex(@"\p{IsCyrillic}[\p{IsCyrillic} \-]*\p{IsCyrillic}|\p{IsCyrillic}", RegexOptions.Compiled);
        private static readonly string[] s_RussianPlatforms = { "ru", "be", "kk", "uk", "uz" };

        private static Dictionary<string, string> s_Table;
        private static readonly List<(Regex pattern, string english, int literal)> s_Templates = new List<(Regex, string, int)>();
        private static readonly Dictionary<string, string> s_Cache = new Dictionary<string, string>();
        private static readonly HashSet<string> s_Misses = new HashSet<string>();
        private static bool s_Loaded;
        private static bool s_Chosen;

        /// <summary>The language the texts are shown in.</summary>
        public static GameLanguage Language { get; private set; } = GameLanguage.Russian;

        public static bool IsEnglish => Language == GameLanguage.English;

        /// <summary>The language changed (the texts redraw by themselves; pictures with words swap on this).</summary>
        public static event Action Changed;

        /// <summary>Texts with no translation seen in English (editor: written to Logs/loc_misses.txt).</summary>
        public static IReadOnlyCollection<string> Misses => s_Misses;

        /// <summary>The language from the saved choice, else from the platform.</summary>
        public static void EnsureLoaded()
        {
            if (s_Loaded)
                return;
            s_Loaded = true;
            if (PlayerPrefs.HasKey(c_Key))
            {
                s_Chosen = true;
                Language = PlayerPrefs.GetInt(c_Key) == 1 ? GameLanguage.English : GameLanguage.Russian;
            }
            else
            {
                Language = FromPlatform(Application.systemLanguage == SystemLanguage.Russian ||
                                        Application.systemLanguage == SystemLanguage.Belarusian ||
                                        Application.systemLanguage == SystemLanguage.Ukrainian ? "ru" : "en");
            }
        }

        /// <summary>The platform told its language (Yandex SDK): used unless the player picked one.</summary>
        public static void SetPlatformLanguage(string code)
        {
            EnsureLoaded();
            if (s_Chosen || string.IsNullOrEmpty(code))
                return;
            Apply(FromPlatform(code));
        }

        /// <summary>The player's choice (the menu's switch): saved, every text redrawn.</summary>
        public static void Choose(GameLanguage language)
        {
            EnsureLoaded();
            s_Chosen = true;
            PlayerPrefs.SetInt(c_Key, language == GameLanguage.English ? 1 : 0);
            PlayerPrefs.Save();
            Apply(language);
        }

        /// <summary>A language for this session only, not saved (tests, screenshots).</summary>
        public static void SetForSession(GameLanguage language)
        {
            EnsureLoaded();
            Apply(language);
        }

        private static GameLanguage FromPlatform(string code)
        {
            code = code.ToLowerInvariant();
            foreach (string ru in s_RussianPlatforms)
            {
                if (code.StartsWith(ru))
                    return GameLanguage.Russian;
            }
            return GameLanguage.English;
        }

        private static void Apply(GameLanguage language)
        {
            if (language == Language)
                return;
            Language = language;
            LocalizationRuntime.RefreshAll();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- translation
        /// <summary>The text in the current language (Russian as it is; English from the table).</summary>
        public static string T(string russian)
        {
            EnsureLoaded();
            if (!IsEnglish || string.IsNullOrEmpty(russian) || !HasCyrillic(russian))
                return russian;
            if (s_Cache.TryGetValue(russian, out string cached))
                return cached;
            LoadTable();
            string english = Translate(russian, 0) ?? russian;
            if (s_Cache.Count > 4000)
                s_Cache.Clear();
            s_Cache[russian] = english;
            return english;
        }

        /// <summary>Russian or English by the current language (texts made in code that are not in the table).</summary>
        public static string Pick(string russian, string english) => IsEnglish ? english : russian;

        private static bool HasCyrillic(string s)
        {
            foreach (char c in s)
            {
                if (c >= 'Ѐ' && c <= 'ӿ')
                    return true;
            }
            return false;
        }

        private static string Translate(string s, int depth)
        {
            if (depth > 3)
                return null;
            if (s_Table.TryGetValue(s, out string exact))
                return exact;
            // Surrounding spaces and line breaks kept as they are.
            string trimmed = s.Trim();
            if (trimmed.Length != s.Length && trimmed.Length > 0)
            {
                string inner = Translate(trimmed, depth + 1);
                return inner == null ? null : s.Substring(0, s.IndexOf(trimmed, StringComparison.Ordinal)) + inner +
                                                s.Substring(s.IndexOf(trimmed, StringComparison.Ordinal) + trimmed.Length);
            }
            // Numbers as placeholders.
            var numbers = new List<string>();
            string key = s_Number.Replace(s, m =>
            {
                numbers.Add(m.Value);
                return "{" + (numbers.Count - 1) + "}";
            });
            if (numbers.Count > 0 && s_Table.TryGetValue(key, out string withNumbers))
                return Fill(withNumbers, numbers);
            // Templates with any value in the placeholders (translated in turn).
            foreach ((Regex pattern, string english, int _) in s_Templates)
            {
                Match m = pattern.Match(s);
                if (!m.Success)
                    continue;
                var values = new List<string>();
                for (int g = 1; g < m.Groups.Count; g++)
                {
                    string v = m.Groups[g].Value;
                    values.Add(HasCyrillic(v) ? Translate(v, depth + 1) ?? v : v);
                }
                return Fill(english, values);
            }
            // Line by line.
            if (s.IndexOf('\n') >= 0)
            {
                string[] lines = s.Split('\n');
                bool any = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!HasCyrillic(lines[i]))
                        continue;
                    string line = Translate(lines[i], depth + 1);
                    if (line != null)
                    {
                        lines[i] = line;
                        any = true;
                    }
                }
                return any ? string.Join("\n", lines) : null;
            }
            // Piece by piece between rich-text tags.
            if (s.IndexOf('<') >= 0)
            {
                string[] parts = s_Tag.Split(s);
                if (parts.Length > 1)
                {
                    bool any = false;
                    var sb = new StringBuilder();
                    foreach (string part in parts)
                    {
                        string piece = part;
                        if (!part.StartsWith("<") && HasCyrillic(part))
                        {
                            string t = Translate(part, depth + 1);
                            if (t != null)
                            {
                                piece = t;
                                any = true;
                            }
                        }
                        sb.Append(piece);
                    }
                    return any ? sb.ToString() : null;
                }
            }
            // Last: the Russian words in it one run at a time ("РЕДКОЕ 30%" -> "RARE 30%"), when every run is known.
            bool all = true;
            string words = s_Words.Replace(s, m =>
            {
                if (s_Table.TryGetValue(m.Value, out string w))
                    return w;
                all = false;
                return m.Value;
            });
            if (all && words != s)
                return words;
            if (s_Misses.Add(s))
                WriteMiss(s);
            return null;
        }

        private static string Fill(string english, List<string> values) =>
            s_Placeholder.Replace(english, m =>
            {
                int i = int.Parse(m.Groups[1].Value);
                return i < values.Count ? values[i] : m.Value;
            });

        private static void LoadTable()
        {
            if (s_Table != null)
                return;
            s_Table = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>(c_Table);
            if (asset == null)
            {
                Debug.LogWarning($"[Loc] Resources/{c_Table} missing: no English");
                return;
            }
            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                int tab = line.IndexOf('\t');
                if (tab <= 0)
                    continue;
                string ru = Unescape(line.Substring(0, tab));
                string en = Unescape(line.Substring(tab + 1));
                if (en.Length == 0)
                    continue;
                s_Table[ru] = en;
                if (!s_Placeholder.IsMatch(ru))
                    continue;
                // "{0} ВОЛН" -> ^(.+?) ВОЛН$
                string pattern = "^" + s_Placeholder.Replace(Regex.Escape(ru).Replace(@"\{", "{"), "(.+?)") + "$";
                int literal = s_Placeholder.Replace(ru, string.Empty).Length;
                s_Templates.Add((new Regex(pattern, RegexOptions.Singleline), en, literal));
            }
            // The most specific templates first.
            s_Templates.Sort((a, b) => b.literal.CompareTo(a.literal));
        }

        private static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");

        private static void WriteMiss(string s)
        {
#if UNITY_EDITOR
            try
            {
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.AppendAllText("Logs/loc_misses.txt", s.Replace("\n", "\\n") + "\n");
            }
            catch (System.IO.IOException)
            {
            }
#endif
        }
    }

    /// <summary>
    /// Hooks every TextMeshPro text of the loaded scenes (and the ones made later) to <see cref="Loc"/>: the text is
    /// translated as it is drawn, and redrawn when the language changes.
    /// </summary>
    public sealed class LocalizationRuntime : MonoBehaviour, ITextPreprocessor
    {
        private static LocalizationRuntime s_Instance;
        private readonly List<TMP_Text> m_Texts = new List<TMP_Text>();
        private readonly HashSet<TMP_Text> m_Hooked = new HashSet<TMP_Text>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            Loc.EnsureLoaded();
            if (s_Instance != null)
                return;
            var go = new GameObject("Localization");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<LocalizationRuntime>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            HookAll();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookAll();

        /// <summary>The preprocessor: the text in the current language.</summary>
        public string PreprocessText(string text) => Loc.T(text);

        private void HookAll()
        {
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Hook(text, false);
        }

        private bool Hook(TMP_Text text, bool redraw)
        {
            if (text == null || ReferenceEquals(text.textPreprocessor, this))
                return false;
            text.textPreprocessor = this;
            if (m_Hooked.Add(text))
                m_Texts.Add(text);
            // A button's word may be longer in English: it shrinks to fit instead of running over the button.
            if (!text.enableAutoSizing && text.GetComponentInParent<UnityEngine.UI.Button>(true) != null)
            {
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = Mathf.Max(10f, text.fontSize * 0.55f);
                text.enableAutoSizing = true;
            }
            if (redraw && Loc.IsEnglish)
                text.ForceMeshUpdate(true, true);            // already drawn untranslated: draw it again now
            return true;
        }

        // A text drawn for the first time (made after the scene loaded: a card, a pooled plate).
        private void OnTextChanged(Object obj)
        {
            if (obj is TMP_Text text)
                Hook(text, true);
        }

        /// <summary>Every hooked text redrawn (the language changed).</summary>
        public static void RefreshAll()
        {
            if (s_Instance == null)
                return;
            List<TMP_Text> texts = s_Instance.m_Texts;
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    s_Instance.m_Hooked.Remove(text);
                    texts.RemoveAt(i);
                    continue;
                }
                text.havePropertiesChanged = true;
                text.SetAllDirty();
            }
        }
    }
}
