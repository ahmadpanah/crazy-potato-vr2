using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Installer.Editor.Hub
{
    // Stamped from clickme-inspector/Template/Host by `clickme.py host`. Edit the template, not
    // this copy: a re-stamp overwrites it.

    /// <summary>
    /// The inspector of <c>CLICKME.asset</c>. The hub is not this package's: it ships with
    /// the MCP plugin, beside the one Welcome window, and this inspector hands the asset to it.
    /// Until that package is installed, or while its version has no hub, it says what to do.
    ///
    /// <para>Nothing runs until the asset is shown: no <c>InitializeOnLoad</c>, no update hook.
    /// The hub is looked up by name, as this assembly references nothing.</para>
    /// </summary>
    [CustomEditor(typeof(MCPForUnityHub))]
    // UnityEditor.Editor in full: most stamped namespaces have an .Editor segment, which a bare
    // Editor would resolve to.
    internal sealed class MCPForUnityHubInspector : UnityEditor.Editor
    {
        private const string PRODUCT = "MCP For Unity";
        private const string TAGLINE = "Let your AI agent work inside the Unity Editor";
        private const string ICON = "Media/Icon.png";
        private const string HUB_ASSEMBLY = "UnityMCP.Editor.Welcome";
        private const string HUB_TYPE = "UnityMCP.Editor.Welcome.UnityMcpWelcome+ClickMeInspector";
        private const string DEPENDENCY_ASSEMBLY = "AnkleBreaker.UnityMCP.Editor";
        private const string MISSING_TITLE = "Install the MCP plugin";
        private const string MISSING_TEXT = "This package's guide and hub ship with the free MCP plugin, installed from GitHub. Git must be installed on this machine; the plugin setup installs its latest release.";
        private const string OUTDATED_TITLE = "Update the MCP plugin";
        private const string OUTDATED_TEXT = "The plugin in this project is older than its guide and hub. The plugin setup updates it to the latest release.";
        private const string INSTALL_LABEL = "Open the plugin setup";
        private const string INSTALL_MENU = "Tools/AnkleBreaker/Dependencies Installer/MCP For Unity";

        /// <summary>Label, kind (menu or url), target: the buttons under the install one.</summary>
        private static readonly string[] LINKS = { "Documentation", "menu", "Tools/AnkleBreaker/MCP For Unity/Documentation", "Plugin on GitHub", "url", "https://github.com/AnkleBreaker-Studio/unity-mcp-plugin", "Ask on Discord", "url", "https://discord.gg/2D36VUmsYN" };

        private UnityEditor.Editor _hub;
        private Texture2D _icon;

        // The hub draws its own header; the default one only repeats "CLICKME".
        protected override void OnHeaderGUI() { }

        public override bool UseDefaultMargins() => false;

        public override VisualElement CreateInspectorGUI()
        {
            ReleaseHub();
            Type hub = FindHub();
            if (hub != null)
            {
                _hub = CreateEditor(target, hub);
                VisualElement drawn = _hub != null ? _hub.CreateInspectorGUI() : null;
                if (drawn != null) return drawn;
                ReleaseHub();
            }
            return Waiting(IsLoaded(DEPENDENCY_ASSEMBLY));
        }

        private void OnDisable() => ReleaseHub();

        private void ReleaseHub()
        {
            if (_hub != null) DestroyImmediate(_hub);
            _hub = null;
        }

        private static Type FindHub()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != HUB_ASSEMBLY) continue;
                Type type = assembly.GetType(HUB_TYPE, false);
                return type != null && typeof(UnityEditor.Editor).IsAssignableFrom(type) ? type : null;
            }
            return null;
        }

        private static bool IsLoaded(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == name) return true;
            return false;
        }

        // -- Before the hub exists ---------------------------------------------

        /// <summary>One card, in the editor's own colours so both skins read it: what is
        /// missing, one button to fix it, and the package's other ways in.</summary>
        private VisualElement Waiting(bool installed)
        {
            var root = new VisualElement();
            root.style.paddingLeft = root.style.paddingRight = 14;
            root.style.paddingTop = root.style.paddingBottom = 12;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 12;
            Texture2D icon = Icon();
            if (icon != null)
            {
                var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.style.width = image.style.height = 40;
                image.style.marginRight = 10;
                header.Add(image);
            }
            var names = new VisualElement();
            names.style.flexShrink = 1;
            names.Add(Text(PRODUCT, 16, FontStyle.Bold, 1f));
            if (TAGLINE.Length > 0) names.Add(Text(TAGLINE, 12, FontStyle.Normal, 0.75f));
            header.Add(names);
            root.Add(header);

            var card = new VisualElement();
            card.style.paddingLeft = card.style.paddingRight = 12;
            card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.borderTopWidth = card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = card.style.borderRightWidth = 1;
            Color line = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.14f) : new Color(0f, 0f, 0f, 0.18f);
            card.style.borderTopColor = card.style.borderBottomColor = line;
            card.style.borderLeftColor = card.style.borderRightColor = line;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            card.Add(Text(installed ? OUTDATED_TITLE : MISSING_TITLE, 13, FontStyle.Bold, 1f));
            Label body = Text(installed ? OUTDATED_TEXT : MISSING_TEXT, 12, FontStyle.Normal, 0.8f);
            body.style.marginTop = 4;
            body.style.marginBottom = 10;
            card.Add(body);
            Button install = Action(INSTALL_LABEL, "menu", INSTALL_MENU);
            install.style.height = 26;
            install.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(install);
            root.Add(card);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 8;
            for (int i = 0; i + 2 < LINKS.Length; i += 3)
                row.Add(Action(LINKS[i], LINKS[i + 1], LINKS[i + 2]));
            if (row.childCount > 0) root.Add(row);
            return root;
        }

        private static Label Text(string text, int size, FontStyle weight, float opacity)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = weight;
            label.style.opacity = opacity;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        /// <summary>A menu that does not exist in this project gives a disabled button, never a
        /// console error.</summary>
        private static Button Action(string label, string kind, string target)
        {
            bool url = kind == "url";
            var button = new Button(() =>
            {
                if (url) Application.OpenURL(target);
                else EditorApplication.ExecuteMenuItem(target);
            }) { text = label };
            button.SetEnabled(url || MenuExists(target));
            return button;
        }

        private static bool MenuExists(string path) => !string.IsNullOrEmpty(path) && Menu.GetEnabled(path);

        private Texture2D Icon()
        {
            if (_icon != null) return _icon;
            string script = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string dir = string.IsNullOrEmpty(script) ? null : System.IO.Path.GetDirectoryName(script);
            _icon = dir == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(dir.Replace(System.IO.Path.DirectorySeparatorChar, '/') + "/" + ICON);
            return _icon;
        }
    }
}
