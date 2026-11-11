using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    internal sealed class AbDocsRecommendations
    {
        private readonly VisualElement _rail;
        private readonly ScrollView _cards;
        private readonly AbPackageCarousel _carousel;
        private int _rows;
        private bool _searching;

        internal AbDocsRecommendations(ScrollView sidebar)
        {
            var parent = sidebar.parent;
            int index = parent.IndexOf(sidebar);
            sidebar.RemoveFromHierarchy();
            _rail = new VisualElement { name = "docs-navigation-rail" };
            _rail.AddToClassList("ab-docs__rail");
            _rail.EnableInClassList("ab--light", !EditorGUIUtility.isProSkin);
            var sheet = LoadStyleSheet();
            if (sheet != null) parent.styleSheets.Add(sheet);
            parent.Insert(index, _rail);
            _rail.Add(sidebar);

            var host = new VisualElement { name = "docs-recommendations" };
            host.AddToClassList("ab-docs__recommendations");
            var button = new Button(() => _carousel?.OpenCatalogue())
            {
                text = "Enhance your game  \u2197", name = "enhance-your-game",
                tooltip = "Explore all AnkleBreaker packages on the Asset Store"
            };
            button.AddToClassList("ab-docs__enhance-toggle");
            host.Add(button);
            _cards = new ScrollView(ScrollViewMode.Vertical);
            AbPackageCarousel.AddTo(_cards);
            _carousel = _cards.Q<AbPackageCarousel>();
            host.Add(_cards);
            _rail.Add(host);
            if (_carousel == null) host.style.display = DisplayStyle.None;
            _rail.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        internal void Update(int navigationRows, bool searching)
        {
            _rows = navigationRows;
            _searching = searching;
            Refresh();
        }

        private void Refresh()
        {
            float available = _rail.layout.height - _rows * 25f - 24f;
            int count = available >= 520f ? 2 : available >= 290f ? 1 : 0;
            if (_searching) count = 0;
            _carousel?.SetVisibleCardCount(Mathf.Max(1, count));
            _cards.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _carousel?.SetRotationVisible(count > 0);
        }

        private static StyleSheet LoadStyleSheet()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/AB/MCPForUnity/Scripts/Editor/Docs/AbDocsCarousel.uss");
            if (sheet != null) return sheet;
            foreach (string guid in AssetDatabase.FindAssets("AbDocsRecommendations t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null || script.GetClass() != typeof(AbDocsRecommendations)) continue;
                return AssetDatabase.LoadAssetAtPath<StyleSheet>(path.Substring(0, path.LastIndexOf('/') + 1) + "AbDocsCarousel.uss");
            }
            return null;
        }
    }
}
