using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEditor;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>
    /// Resolves the clickable-link schemes used across the Documentation window.
    /// Every inline <c>&lt;link="scheme:arg"&gt;</c> in a paragraph and every action
    /// button routes through <see cref="Handle"/>:
    /// <list type="bullet">
    /// <item><c>page:{id}</c> — navigate to another doc page.</item>
    /// <item><c>menu:{path}</c> — execute an editor menu item.</item>
    /// <item><c>asset:{path}</c> — ping + select a project asset.</item>
    /// <item><c>url:{https…}</c> — open an external URL.</item>
    /// <item><c>action:{name}</c> — run one of this package's own operations
    /// (install-plugin, check-updates, open-welcome, open-package-manager, open-dashboard).</item>
    /// </list>
    /// </summary>
    internal static class DocActions
    {
        public static void Handle(string link)
        {
            if (string.IsNullOrEmpty(link)) return;
            int separator = link.IndexOf(':');
            if (separator < 0)
            {
                Debug.LogWarning($"[MCP For Unity Docs] Unrecognized link '{link}'.");
                return;
            }
            string scheme = link.Substring(0, separator);
            string arg = link.Substring(separator + 1);

            switch (scheme)
            {
                case "page":
                    MCPForUnityDocsWindow.OpenPage(arg);
                    break;
                case "menu":
                    RunMenu(arg);
                    break;
                case "asset":
                    SelectAsset(arg);
                    break;
                case "url":
                    Application.OpenURL(arg);
                    break;
                case "action":
                    RunAction(arg);
                    break;
                default:
                    Debug.LogWarning($"[MCP For Unity Docs] Unknown link scheme '{scheme}'.");
                    break;
            }
        }

        /// <summary>
        /// Executes an editor menu item, without asking for one that provably is not there.
        ///
        /// <para><c>EditorApplication.ExecuteMenuItem</c> does not fail quietly on an unknown path:
        /// Unity logs a red console <b>error</b> before returning false. Every <c>menu:</c> link in
        /// this manual points into the MCP plugin's own submenu, so the single reason one would be
        /// missing is knowable in advance — the plugin is not installed. Checking that first turns
        /// a red error plus a warning, printed at the exact moment a buyer is touring the manual
        /// before installing, into one warning that says what to do.</para>
        /// </summary>
        private static void RunMenu(string path)
        {
            bool isPluginMenu = !string.IsNullOrEmpty(path) &&
                                path.StartsWith(MCPForUnityPlugin.PluginMenuRoot, System.StringComparison.Ordinal);

            if (isPluginMenu && !MCPForUnityPlugin.IsInstalled)
            {
                Debug.LogWarning($"[MCP For Unity Docs] '{path}' is added by {MCPForUnityPlugin.DisplayName}, " +
                                 "which is not installed in this project yet. Install it from the plugin setup " +
                                 "(select CLICKME.asset, then Open the plugin setup) and try again.");
                return;
            }

            if (!EditorApplication.ExecuteMenuItem(path))
                Debug.LogWarning($"[MCP For Unity Docs] Menu item not found: {path}.");
        }

        /// <summary>
        /// Documentation that can perform the thing it describes. Every target here is an
        /// operation this package owns, so a doc page never has to tell the reader to go find a
        /// button somewhere else.
        /// </summary>
        private static void RunAction(string action)
        {
            switch (action)
            {
                case "install-plugin":
                    MCPForUnityPluginInstaller.InstallLatest();
                    break;
                case "check-updates":
                    MCPForUnityUpdateChecker.Check(force: true);
                    break;
                case "open-welcome":
                    MCPForUnityDependenciesInstaller.OpenWindow();
                    break;
                case "open-package-manager":
                    MCPForUnityPluginInstaller.OpenInPackageManager();
                    break;
                // Not a menu: link — the dashboard's menu item exists only from plugin 2.39.7;
                // OpenDashboard uses it when it is there and opens the window by type otherwise.
                case "open-dashboard":
                    MCPForUnityPlugin.OpenDashboard();
                    break;
                default:
                    Debug.LogWarning($"[MCP For Unity Docs] Unknown action '{action}'.");
                    break;
            }
        }

        private static void SelectAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                Debug.LogWarning($"[MCP For Unity Docs] Asset not found: {path}");
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
