using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>
    /// MCP For Unity Documentation — the in-asset manual. Four tabs (Get Started /
    /// Manual / Scripting / Reference), a per-tab sidebar, full-content search,
    /// back/forward history, and clickable actions (see <see cref="DocActions"/>).
    /// Content lives in <c>Docs/Pages/</c>, one static class per chapter, built
    /// with the <see cref="DocUI"/> helpers and registered in <see cref="DocRegistry"/>.
    /// </summary>
    internal sealed class MCPForUnityDocsWindow : EditorWindow
    {
        private const string PREF_WELCOME_SHOWN = "AB.MCPForUnity.Docs.WelcomeShown";
        private const int MAX_HISTORY = 64;

        // Hard paths first; LoadAsset falls back to a name search if the user
        // moved the package folder (common with Asset Store imports).
        private const string DOCS_DIR = "Assets/AB/MCPForUnity/Scripts/Editor/Docs/";
        private const string UXML_PATH = DOCS_DIR + "MCPForUnityDocs.uxml";
        private const string USS_PATH = DOCS_DIR + "MCPForUnityDocs.uss";

        [MenuItem("Tools/AnkleBreaker/MCP For Unity/Documentation", priority = 10)]
        public static void Open() => OpenPage(null);

        /// <summary>Opens the window (reusing the docked instance) on a page id.
        /// Null id = last page or the welcome page.</summary>
        public static void OpenPage(string pageId)
        {
            var window = GetWindow<MCPForUnityDocsWindow>("MCP For Unity Docs");
            window.minSize = new Vector2(720f, 420f);
            window.Show();
            if (pageId != null) window.NavigateTo(pageId, pushHistory: true);
        }

        [SerializeField] private string _currentPageId;
        [SerializeField] private string _activeTab = DocRegistry.TAB_START;

        // Serialized alongside the current page: the page id survived domain reloads while the
        // history did not, so every recompile silently emptied Back/Forward under a window that
        // otherwise looked untouched.
        [SerializeField] private List<string> _history = new List<string>();
        [SerializeField] private int _historyIndex = -1;

        private VisualElement _tabBar;
        private ScrollView _sidebar;
        private ScrollView _content;
        private AbDocsRecommendations _recommendations;
        private ToolbarSearchField _search;
        private Button _backBtn, _fwdBtn;
        private string _filter = string.Empty;

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            var tree = LoadAsset<VisualTreeAsset>(UXML_PATH, "MCPForUnityDocs t:VisualTreeAsset");
            if (tree != null) tree.CloneTree(rootVisualElement);
            var style = LoadAsset<StyleSheet>(USS_PATH, "MCPForUnityDocs t:StyleSheet");
            if (style != null) rootVisualElement.styleSheets.Add(style);
            MCPForUnityTheme.Apply(rootVisualElement, "ab-docs");

            _tabBar = rootVisualElement.Q<VisualElement>("tab-bar");
            _sidebar = rootVisualElement.Q<ScrollView>("docs-sidebar");
            _content = rootVisualElement.Q<ScrollView>("docs-content");
            _search = rootVisualElement.Q<ToolbarSearchField>("docs-search");
            _backBtn = rootVisualElement.Q<Button>("nav-back");
            _fwdBtn = rootVisualElement.Q<Button>("nav-fwd");

            if (_backBtn != null) _backBtn.clicked += GoBack;
            if (_fwdBtn != null) _fwdBtn.clicked += GoForward;
            _search?.RegisterValueChangedCallback(e => { _filter = e.newValue ?? string.Empty; RebuildSidebar(); });
            // The ToolbarSearchField serializes its text across domain reloads
            // without re-firing the change callback — re-sync the filter so the
            // sidebar and the visible search text always agree.
            _filter = _search?.value ?? string.Empty;
            _search?.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);

            // Ctrl+F / Cmd+F focuses the search field from anywhere in the window.
            rootVisualElement.focusable = true;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown);

            _recommendations = new AbDocsRecommendations(_sidebar);
            BuildTabs();

            string start = _currentPageId;
            if (string.IsNullOrEmpty(start)) start = "welcome";
            if (!EditorPrefs.GetBool(PREF_WELCOME_SHOWN, false))
            {
                start = "welcome";
                EditorPrefs.SetBool(PREF_WELCOME_SHOWN, true);
            }
            NavigateTo(start, pushHistory: true);
        }

        private static T LoadAsset<T>(string path, string searchFilter) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            foreach (var guid in AssetDatabase.FindAssets(searchFilter))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            Debug.LogWarning($"[MCP For Unity Docs] Missing asset: {path}");
            return null;
        }

        // -- Navigation ------------------------------------------------------

        public void NavigateTo(string pageId, bool pushHistory)
        {
            var page = DocRegistry.Find(pageId);
            if (page == null)
            {
                Debug.LogWarning($"[MCP For Unity Docs] Unknown page '{pageId}'.");
                return;
            }

            if (pushHistory && page.Id != _currentPageId)
            {
                if (_historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                _history.Add(page.Id);
                if (_history.Count > MAX_HISTORY) _history.RemoveAt(0);
                _historyIndex = _history.Count - 1;
            }

            _currentPageId = page.Id;
            _activeTab = page.Tab;
            RefreshTabVisuals();
            RebuildSidebar();
            RebuildContent(page);
            RefreshNavButtons();
        }

        private void GoBack()
        {
            if (_historyIndex <= 0) return;
            _historyIndex--;
            NavigateTo(_history[_historyIndex], pushHistory: false);
        }

        private void GoForward()
        {
            if (_historyIndex >= _history.Count - 1) return;
            _historyIndex++;
            NavigateTo(_history[_historyIndex], pushHistory: false);
        }

        private void RefreshNavButtons()
        {
            _backBtn?.SetEnabled(_historyIndex > 0);
            _fwdBtn?.SetEnabled(_historyIndex < _history.Count - 1);
        }

        // -- Keyboard --------------------------------------------------------

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.F)
            {
                _search?.Focus();
                evt.StopPropagation();
            }
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            // Esc clears the filter; Enter jumps to the first matching page.
            if (evt.keyCode == KeyCode.Escape)
            {
                _search?.SetValueWithoutNotify(string.Empty);
                _filter = string.Empty;
                RebuildSidebar();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                string first = FirstFilteredPageId();
                if (first != null) { NavigateTo(first, pushHistory: true); evt.StopPropagation(); }
            }
        }

        private string FirstFilteredPageId()
        {
            bool searching = !string.IsNullOrEmpty(_filter);
            string needle = _filter.ToLowerInvariant();
            foreach (var page in DocRegistry.Pages)
            {
                if (searching)
                {
                    if (!page.Title.ToLowerInvariant().Contains(needle) &&
                        !page.SearchText.Contains(needle)) continue;
                }
                else if (page.Tab != _activeTab) continue;
                return page.Id;
            }
            return null;
        }

        // -- Tabs ------------------------------------------------------------

        private void BuildTabs()
        {
            if (_tabBar == null) return;
            _tabBar.Clear();
            foreach (var tab in DocRegistry.TABS)
            {
                string captured = tab;
                var button = new Button(() =>
                {
                    _activeTab = captured;
                    RefreshTabVisuals();
                    RebuildSidebar();
                    foreach (var page in DocRegistry.Pages)
                        if (page.Tab == captured) { NavigateTo(page.Id, pushHistory: true); break; }
                })
                { text = tab };
                button.AddToClassList("ab-docs__tab");
                _tabBar.Add(button);
            }
            RefreshTabVisuals();
        }

        private void RefreshTabVisuals()
        {
            if (_tabBar == null) return;
            foreach (var child in _tabBar.Children())
            {
                if (!(child is Button button)) continue;
                button.EnableInClassList("ab-docs__tab--active", button.text == _activeTab);
            }
        }

        // -- Sidebar (page list / search results) ----------------------------

        private void RebuildSidebar()
        {
            if (_sidebar == null) return;
            _sidebar.Clear();
            bool searching = !string.IsNullOrEmpty(_filter);
            string needle = _filter.ToLowerInvariant();
            int shown = 0;

            foreach (var page in DocRegistry.Pages)
            {
                if (searching)
                {
                    // Search spans ALL tabs: match title or indexed content.
                    if (!page.Title.ToLowerInvariant().Contains(needle) &&
                        !page.SearchText.Contains(needle)) continue;
                }
                else if (page.Tab != _activeTab) continue;

                string id = page.Id;
                var item = new Button(() => NavigateTo(id, pushHistory: true))
                {
                    text = searching ? $"{page.Title}   ·  {page.Tab}" : page.Title
                };
                item.AddToClassList("ab-docs__side-item");
                if (page.Id == _currentPageId) item.AddToClassList("ab-docs__side-item--active");
                _sidebar.Add(item);
                shown++;
            }

            _recommendations?.Update(shown, searching);

            if (shown == 0)
            {
                var empty = new Label(searching
                    ? $"No page matches \"{_filter}\"."
                    : "No pages in this tab yet.");
                empty.AddToClassList("ab-docs__side-empty");
                _sidebar.Add(empty);
            }
        }

        // -- Content ---------------------------------------------------------

        private void RebuildContent(DocPage page)
        {
            if (_content == null) return;
            _content.Clear();
            _content.scrollOffset = Vector2.zero;

            var crumb = new Label($"{page.Tab}  ›  {page.Title}");
            crumb.AddToClassList("ab-docs__breadcrumb");
            _content.Add(crumb);

            page.Build?.Invoke(_content.contentContainer);
        }
    }
}
