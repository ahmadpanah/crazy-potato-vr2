using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    internal sealed class AbPackageCarousel : VisualElement
    {
        [Serializable]
        internal sealed class Catalog
        {
            public string publisherUrl;
            public Product[] products;
        }

        [Serializable]
        internal sealed class Product
        {
            public string id;
            public string name;
            public string blurb;
            public string url;
            public string cardFile;
            public string status;
        }

        private readonly List<Product> _products = new List<Product>();
        private readonly VisualElement _cards;
        private const double RotationSeconds = 8d;
        private readonly VisualElement _progress;
        private readonly IVisualElementScheduledItem _rotation;
        private double _elapsed, _lastTick;
        private bool _hovered;
        private bool _rotationVisible = true;
        private readonly Button _previous;
        private readonly Button _next;
        private readonly Func<string, Texture2D> _loadImage;
        private readonly string _publisherUrl;
        private int _start;
        private int _visibleCount = 1;

        internal AbPackageCarousel(Catalog catalog, string currentPackage, Func<string, Texture2D> loadImage)
        {
            name = "ab-package-carousel";
            AddToClassList("ab-docs__carousel");
            style.flexShrink = 0;
            _loadImage = loadImage;
            _publisherUrl = catalog?.publisherUrl;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog?.products != null)
                foreach (var product in catalog.products)
                    if (product != null && !string.IsNullOrEmpty(product.id) &&
                        !string.Equals(product.id, currentPackage, StringComparison.OrdinalIgnoreCase) &&
                        product.status == "published" && !string.IsNullOrEmpty(product.name) &&
                        IsStoreUrl(product.url) && ids.Add(product.id))
                        _products.Add(product);

            if (_products.Count == 0)
            {
                style.display = DisplayStyle.None;
                return;
            }

            var heading = new VisualElement();
            heading.AddToClassList("ab-docs__carousel-heading");
            var titles = new VisualElement();
            titles.style.flexGrow = 1;
            titles.style.flexShrink = 1;
            titles.Add(Text("More from AnkleBreaker", "ab-docs__carousel-title"));
            heading.Add(titles);
            Add(heading);

            _cards = new VisualElement { name = "carousel-cards" };
            _cards.AddToClassList("ab-docs__carousel-cards");
            _cards.style.flexDirection = FlexDirection.Column;
            Add(_cards);

            var footer = new VisualElement();
            footer.AddToClassList("ab-docs__carousel-footer");
            var track = new VisualElement { name = "carousel-progress" };
            track.AddToClassList("ab-docs__carousel-progress");
            track.tooltip = "Next packages every 8 seconds. Paused while hovered or focused.";
            _progress = new VisualElement { name = "carousel-progress-fill" };
            _progress.AddToClassList("ab-docs__carousel-progress-fill");
            track.Add(_progress);
            _previous = new Button(() => Move(-1)) { text = "\u2039", name = "carousel-previous", tooltip = "Previous packages" };
            _next = new Button(() => Move(1)) { text = "\u203a", name = "carousel-next", tooltip = "Next packages" };
            _previous.AddToClassList("ab-docs__carousel-arrow");
            _next.AddToClassList("ab-docs__carousel-arrow");
            footer.Add(_previous);
            footer.Add(track);
            footer.Add(_next);
            Add(footer);

            _visibleCount = Mathf.Min(_visibleCount, _products.Count);
            RebuildCards();
            RegisterCallback<PointerEnterEvent>(_ => _hovered = true);
            RegisterCallback<PointerLeaveEvent>(_ => _hovered = false);
            RegisterCallback<AttachToPanelEvent>(_ => { _hovered = false; ResetRotation(); });
            RegisterCallback<DetachFromPanelEvent>(_ => _hovered = false);
            _rotation = schedule.Execute(UpdateRotation).Every(33);
        }

        internal void SetRotationVisible(bool visible)
        {
            if (_rotationVisible == visible || _rotation == null) return;
            _rotationVisible = visible;
            ResetRotation();
            if (visible) _rotation.Resume();
            else _rotation.Pause();
        }

        private void ResetRotation()
        {
            _elapsed = 0d;
            _lastTick = EditorApplication.timeSinceStartup;
            if (_progress != null) _progress.style.width = Length.Percent(0f);
        }

        private void UpdateRotation()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = Math.Min(0.25d, Math.Max(0d, now - _lastTick));
            _lastTick = now;
            if (!_rotationVisible || panel == null || _products.Count <= _visibleCount) return;
            for (VisualElement ancestor = this; ancestor != null; ancestor = ancestor.parent)
                if (ancestor.resolvedStyle.display == DisplayStyle.None ||
                    ancestor.resolvedStyle.visibility == Visibility.Hidden) return;
            var focused = panel.focusController.focusedElement as VisualElement;
            if (_hovered || (focused != null && (focused == this || Contains(focused)))) return;
            _elapsed += delta;
            _progress.style.width = Length.Percent((float)Math.Min(100d, _elapsed / RotationSeconds * 100d));
            if (_elapsed >= RotationSeconds) Move(1);
        }

        internal void OpenCatalogue()
        {
            if (IsStoreUrl(_publisherUrl)) Application.OpenURL(_publisherUrl);
        }

        internal void SetVisibleCardCount(int count)
        {
            if (_products.Count == 0) return;
            count = Mathf.Clamp(count, 1, Mathf.Min(2, _products.Count));
            if (count == _visibleCount) return;
            _visibleCount = count;
            _start = Mathf.Min(_start, Mathf.Max(0, _products.Count - count));
            RebuildCards();
        }

        private void Move(int direction)
        {
            _start = (_start + direction * _visibleCount + _products.Count) % _products.Count;
            RebuildCards();
        }

        private void RebuildCards()
        {
            _cards.Clear();
            int end = _start + Mathf.Min(_visibleCount, _products.Count);
            for (int index = _start; index < end; index++)
            {
                Product product = _products[index % _products.Count];
                var card = new Button(() => Application.OpenURL(product.url)) { name = "package-" + product.id, tooltip = "View " + product.name + " on Asset Store" };
                card.AddToClassList("ab-docs__package-card");
                card.style.flexShrink = 0;
                card.style.minWidth = 0;
                if (index + 1 < end) card.style.marginBottom = 10;
                var image = new Image { image = _loadImage?.Invoke(product.cardFile), scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("ab-docs__package-image");
                var media = new VisualElement();
                media.AddToClassList("ab-docs__package-media");
                media.Add(image);
                var action = Text("View on Asset Store  \u2197", "ab-docs__package-link");
                action.pickingMode = PickingMode.Ignore;
                media.Add(action);
                card.Add(media);
                var body = new VisualElement();
                body.AddToClassList("ab-docs__package-body");
                body.Add(Text(product.name, "ab-docs__package-name"));
                body.Add(Text(product.blurb ?? string.Empty, "ab-docs__package-blurb"));
                card.Add(body);
                _cards.Add(card);
            }
            ResetRotation();
            _previous.style.display = _next.style.display = _products.Count > _visibleCount ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Label Text(string value, string className)
        {
            var label = new Label(value) { enableRichText = false };
            label.AddToClassList(className);
            return label;
        }

        private static bool IsStoreUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                   uri.Host == "assetstore.unity.com";
        }

        internal static void AddTo(VisualElement host)
        {
            const string defaultDirectory = "Assets/AB/MCPForUnity/Scripts/Editor/Docs/";
            string directory = defaultDirectory;
            var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(directory + "Carousel/catalog.json");
            if (catalogAsset == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("AbPackageCarousel t:MonoScript"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script == null || script.GetClass() != typeof(AbPackageCarousel)) continue;
                    directory = path.Substring(0, path.LastIndexOf('/') + 1);
                    catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(directory + "Carousel/catalog.json");
                    break;
                }
            }
            if (catalogAsset == null) return;
            Catalog catalog;
            try { catalog = JsonUtility.FromJson<Catalog>(catalogAsset.text); }
            catch (ArgumentException) { return; }
            host.Add(new AbPackageCarousel(catalog, "MCPForUnity", file =>
                string.IsNullOrEmpty(file) || file != System.IO.Path.GetFileName(file) ? null :
                AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "Carousel/" + file)));
        }
    }
}
