using UnityEditor;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>
    /// What the stamped CLICKME hub needs from this package's own code, which it reaches by name
    /// (its config names <see cref="StatusLine"/> and the Dashboard menu below).
    /// </summary>
    internal static class MCPForUnityClickMe
    {
        private const string DASHBOARD_MENU = "Tools/AnkleBreaker/MCP For Unity/Dashboard";

        /// <summary>The live line of the hub's Ready band: plugin version, bridge, update.</summary>
        internal static string StatusLine()
        {
            if (!MCPForUnityPlugin.IsInstalled) return null;
            // Resolved live: the plugin picks its port from a range and can move it between sessions.
            MCPBridgeInfo bridge = MCPForUnityBridge.Resolve();
            string line = "Plugin " + MCPForUnityPlugin.DescribeInstalled() +
                          (bridge.IsServing ? " · bridge on " + bridge.Url : " · no bridge serving this project");
            var last = MCPForUnityUpdateChecker.LastResult;
            if (last.HasValue && last.Value.Status == MCPUpdateStatus.UpdateAvailable)
                line += " · v" + last.Value.LatestVersion + " available";
            return line;
        }

        /// <summary>One menu for every plugin version: OpenDashboard knows the plugin's own entry
        /// (2.39.7 and later) and the window type of the older ones.</summary>
        [MenuItem(DASHBOARD_MENU, false, 20)]
        private static void OpenDashboard() => MCPForUnityPlugin.OpenDashboard();

        [MenuItem(DASHBOARD_MENU, true)]
        private static bool CanOpenDashboard() => MCPForUnityPlugin.IsInstalled;
    }
}
