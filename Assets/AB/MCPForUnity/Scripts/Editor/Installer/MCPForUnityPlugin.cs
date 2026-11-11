using System;
using System.Linq;
using System.Reflection;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>
    /// Single source of truth about the AnkleBreaker Unity MCP plugin: where it lives, and
    /// whether a copy of it is present in this project.
    ///
    /// <para>Nothing here references the plugin's own assembly. That is the point — this type
    /// must keep compiling while the plugin is <b>absent</b>, which is the state it exists to
    /// describe and the state every user is in before their first install.</para>
    /// </summary>
    public static class MCPForUnityPlugin
    {
        // ── Identity ───────────────────────────────────────────────────────

        /// <summary>UPM id, as declared by the plugin's package.json.</summary>
        public const string PackageId = "com.anklebreaker.unity-mcp";

        public const string DisplayName = "AnkleBreaker Unity MCP";

        /// <summary>Editor assembly shipped by the plugin. Probing the loaded assemblies also
        /// detects copies dropped straight into Assets/, which Package Manager knows nothing about.</summary>
        public const string AssemblyName = "AnkleBreaker.UnityMCP.Editor";

        // ── Where it comes from ────────────────────────────────────────────

        public const string GitUrl = "https://github.com/AnkleBreaker-Studio/unity-mcp-plugin.git";
        public const string RepositoryUrl = "https://github.com/AnkleBreaker-Studio/unity-mcp-plugin";
        public const string ReleasesUrl = RepositoryUrl + "/releases";
        public const string IssuesUrl = RepositoryUrl + "/issues";

        public const string LatestReleaseApiUrl =
            "https://api.github.com/repos/AnkleBreaker-Studio/unity-mcp-plugin/releases/latest";

        /// <summary>The Node.js half of the setup: the MCP server AI clients actually talk to.</summary>
        public const string ServerRepositoryUrl = "https://github.com/AnkleBreaker-Studio/unity-mcp-server";

        /// <summary>npm id of that server. Also the string this package looks for when it checks
        /// whether an AI client has been pointed at it.</summary>
        public const string ServerNpmPackage = "anklebreaker-unity-mcp";

        /// <summary>The one command that fetches and launches the server, quoted verbatim in the
        /// setup steps and in every client config this package documents.</summary>
        public const string ServerNpxCommand = "npx -y " + ServerNpmPackage + "@latest";

        /// <summary>Oldest Node.js the server supports, as declared by its package.json engines field.</summary>
        public const int MinimumNodeMajor = 18;

        public const string NodeDownloadUrl = "https://nodejs.org/en/download";

        /// <summary>
        /// What to say to an MCP-capable assistant so it installs this server itself — the route
        /// most people actually take now, and the one that needs no file paths.
        ///
        /// <para>It lives here, next to the identity it names, because the Welcome window and the
        /// manual both print it: two copies of the same instruction are two copies that drift.</para>
        /// </summary>
        public const string AssistantInstallRequest =
            "Install this MCP server for me and add it to your own MCP configuration:\n" +
            "  name: unity\n" +
            "  command: " + ServerNpxCommand + "\n" +
            "  docs: " + ServerRepositoryUrl + "\n" +
            "Then restart yourself so the server is loaded, and tell me which Unity project you can see.";

        public const string DiscordUrl = "https://discord.gg/2D36VUmsYN";
        public const string SponsorsUrl = "https://github.com/sponsors/AnkleBreaker-Studio";
        public const string StudioUrl = "https://www.anklebreaker-studio.com";

        /// <summary>
        /// This asset's own Asset Store page. The review form lives there and nowhere else — the
        /// publisher page carries none — which is what lets the buttons leading here say so.
        ///
        /// <para><c>/packages/slug/{id}</c> is Unity's canonical redirect: it survives a renamed
        /// listing, where the title-bearing URL does not. It answers 404 until the listing is
        /// published, which is the listing's state and not a wrong address.</para>
        /// </summary>
        public const string AssetStoreUrl = "https://assetstore.unity.com/packages/slug/395602";

        // ── What it adds to the editor once installed ──────────────────────

        /// <summary>The plugin's submenu: every window it adds is registered under this prefix.</summary>
        public const string PluginMenuRoot = "Window/AB Unity MCP/";

        /// <summary>The dashboard's own menu item, added by plugin 2.39.7. Older plugins have no
        /// working one: see <see cref="OpenDashboard"/>.</summary>
        public const string DashboardMenuPath = "Window/AB Unity MCP/Dashboard";
        /// <summary>The AnkleBreaker Welcome window, which ships with the plugin (2.40.0 and later)
        /// rather than with this asset. Older plugins had their own at Window/AB Unity MCP/Welcome.</summary>
        public const string PluginWelcomeMenuPath = "Tools/AnkleBreaker/Unity MCP/Welcome";
        public const string ActionHistoryMenuPath = "Window/AB Unity MCP/Action History";

        /// <summary>Dashboard window type, resolved by name because this assembly must not
        /// reference the plugin. See <see cref="OpenDashboard"/> for why the name is needed.</summary>
        private const string DashboardWindowType = "UnityMCP.Editor.MCPDashboardWindow";

        // The bridge port is deliberately NOT a constant here: the plugin auto-assigns it from a
        // range and can move it between sessions. Ask MCPForUnityBridge, which resolves the live
        // one. See that type for why hardcoding 7890 is wrong.

        /// <summary>Minimum Unity version, as declared by the plugin's package.json ("2021.3.18f1").</summary>
        public const string MinimumUnityVersion = "2021.3";

        // ── Presence ───────────────────────────────────────────────────────

        // Every probe below is cached until Refresh(). Installing, updating or removing a UPM
        // package triggers a domain reload, which resets these fields on its own, so the cache
        // cannot outlive the state it describes.
        private static bool _probed;
        private static PackageInfo _package;
        private static bool _assemblyLoaded;

        /// <summary>The plugin is present, whether through Package Manager or as loose files.</summary>
        public static bool IsInstalled
        {
            get { Probe(); return _package != null || _assemblyLoaded; }
        }

        /// <summary>Package Manager owns this copy, so this package can update it in one click.</summary>
        public static bool IsManagedByPackageManager
        {
            get { Probe(); return _package != null; }
        }

        /// <summary>Installed version, or null when the plugin is missing or was copied into
        /// Assets/ (loose files carry no version Unity can report).</summary>
        public static string InstalledVersion
        {
            get { Probe(); return _package?.version; }
        }

        /// <summary>Where Package Manager resolved this copy from — a git URL, a local folder,
        /// an embedded package. Null when the plugin is missing or unmanaged.</summary>
        public static string InstalledSource
        {
            get { Probe(); return _package?.packageId; }
        }

        /// <summary>This copy came from a git URL (as opposed to a local or embedded folder).</summary>
        public static bool InstalledFromGit
        {
            get { Probe(); return _package != null && _package.source == UnityEditor.PackageManager.PackageSource.Git; }
        }

        /// <summary>
        /// A git copy that is following a branch instead of a release tag — the state this package
        /// exists to avoid, and the one an install falls back to when the release lookup fails.
        ///
        /// <para>Read from <c>packageId</c>, which for a git dependency carries the whole resolved
        /// source (<c>...unity-mcp-plugin.git#v2.39.4</c>). Deriving it beats remembering a flag:
        /// it stays true for a copy someone else installed by hand, and it cannot drift out of
        /// sync with what Package Manager actually did.</para>
        /// </summary>
        public static bool IsTrackingBranch
        {
            get
            {
                if (!InstalledFromGit) return false;
                string id = _package.packageId;
                return !string.IsNullOrEmpty(id) && id.IndexOf('#') < 0;
            }
        }

        /// <summary>Human-readable one-liner for the installed state, for status cards.</summary>
        public static string DescribeInstalled()
        {
            Probe();
            if (_package != null) return "v" + _package.version;
            if (_assemblyLoaded) return "installed (not managed by Package Manager)";
            return "not installed";
        }

        /// <summary>Drops the cached probe. Call after any operation that could have changed the
        /// project's packages without a domain reload having happened yet.</summary>
        public static void Refresh() => _probed = false;

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            _package = FindPackage();
            _assemblyLoaded = FindAssembly() != null;
        }

        private static PackageInfo FindPackage()
        {
            // The asset-path form is the cheap, long-standing probe and covers every UPM install
            // shape (registry, git, local, embedded). Fall back to the assembly form so a plugin
            // relocated by the user is still recognised.
            try
            {
                var info = PackageInfo.FindForAssetPath("Packages/" + PackageId + "/package.json");
                if (info != null) return info;

                var assembly = FindAssembly();
                return assembly != null ? PackageInfo.FindForAssembly(assembly) : null;
            }
            catch (Exception)
            {
                // Package Manager is unavailable this early in some editor states; absent is the
                // honest answer, and the next Refresh() will ask again.
                return null;
            }
        }

        private static Assembly FindAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyName);
        }

        /// <summary>True when <paramref name="assemblyName"/> is compiled and loaded right now.</summary>
        public static bool IsAssemblyLoaded(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == assemblyName);
        }

        // ── Opening the plugin's windows ───────────────────────────────────

        /// <summary>
        /// Opens the plugin's MCP Dashboard.
        ///
        /// <para>From plugin 2.39.7 the dashboard has its own leaf, <see cref="DashboardMenuPath"/>,
        /// and executing it opens the window the way the plugin means to. Before that the plugin
        /// registered the leaf at <c>Window/AB Unity MCP</c> itself, under which its own children
        /// (<c>.../Welcome</c>, <c>.../Action History</c>) live: Unity turned the path into a submenu,
        /// the leaf became unreachable and nothing could execute it. For those versions the window
        /// is opened by its type name, the route that always worked.</para>
        ///
        /// <para><b>The menu is only executed once it is known to exist.</b> <c>ExecuteMenuItem</c>
        /// does not fail quietly on a path it cannot resolve: Unity logs a red console <i>error</i>
        /// whose stack trace names this file, even when the window then opens by type. With an older
        /// plugin every button leading here would print it, so the registered menu items are read
        /// first (<see cref="UnityEditor.TypeCache"/>, which logs nothing).</para>
        /// </summary>
        /// <returns>False when the plugin is absent or its window could not be opened.</returns>
        public static bool OpenDashboard()
        {
            // An absent plugin registers neither the menu nor the window. Say what to do instead of
            // probing: the state this fires in, a buyer touring the manual before installing, is the
            // one where a console error is most likely to be read as "the asset I just bought is broken".
            if (!IsInstalled)
            {
                UnityEngine.Debug.LogWarning("[MCP For Unity] The MCP Dashboard is unavailable — " + DisplayName +
                    " is not installed in this project. Select CLICKME.asset and press Open the plugin setup to install it.");
                return false;
            }

            if (IsMenuItemRegistered(DashboardMenuPath) && UnityEditor.EditorApplication.ExecuteMenuItem(DashboardMenuPath))
                return true;

            // Plugins older than 2.39.7: no reachable menu item, open the window by type.
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != AssemblyName) continue;
                    var type = assembly.GetType(DashboardWindowType);
                    if (type == null) break;
                    UnityEditor.EditorWindow.GetWindow(type, false, "AB Unity MCP").Show();
                    return true;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[MCP For Unity] Could not open the MCP Dashboard: " + e.Message);
                return false;
            }

            UnityEngine.Debug.LogWarning("[MCP For Unity] " + DisplayName + " is installed, but its dashboard " +
                "window could not be opened — neither " + DashboardMenuPath + " nor " + DashboardWindowType +
                " answered. Check the console for plugin compile errors, then open it from the MCP button in " +
                "the editor toolbar, and please report the plugin version: " + IssuesUrl);
            return false;
        }

        /// <summary>True when a loaded assembly declares <paramref name="menuPath"/> as a menu item
        /// (validation functions excluded). Reading attributes logs nothing, unlike asking Unity to
        /// execute a path that does not exist.</summary>
        public static bool IsMenuItemRegistered(string menuPath)
        {
            try
            {
                foreach (MethodInfo method in UnityEditor.TypeCache.GetMethodsWithAttribute<UnityEditor.MenuItem>())
                {
                    foreach (UnityEditor.MenuItem item in method.GetCustomAttributes(typeof(UnityEditor.MenuItem), false))
                    {
                        // A shortcut, when declared, follows the path after a space.
                        if (!item.validate && item.menuItem != null &&
                            (item.menuItem == menuPath || item.menuItem.StartsWith(menuPath + " ", StringComparison.Ordinal)))
                            return true;
                    }
                }
            }
            catch (Exception)
            {
                // An assembly that cannot be reflected over is not the dashboard's: treat as absent.
            }
            return false;
        }
    }
}
