using System.Collections.Generic;
using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>"Manual" tab — how the bridge works and how to live with it day to day.</summary>
    internal static class Pages_Manual
    {
        public static void Register(List<DocPage> pages)
        {
            pages.Add(new DocPage
            {
                Id = "how-it-works", Tab = DocRegistry.TAB_MANUAL, Order = 0,
                Title = "How it works",
                SearchText = "how it works architecture bridge http localhost port 7890 mcp server " +
                             "protocol security privacy loopback tools",
                Build = BuildHowItWorks,
            });
            pages.Add(new DocPage
            {
                Id = "client-setup", Tab = DocRegistry.TAB_MANUAL, Order = 10,
                Title = "Connecting your AI client",
                SearchText = "client setup claude desktop code cursor codex kimi config json mcpservers " +
                             "node npx environment variables unity bridge port hub path windows macos " +
                             "mac osx linux appdata library application support toml mcp_servers config.toml " +
                             "ask the assistant install this mcp prompt paste simple method automatic",
                Build = BuildClientSetup,
            });
            pages.Add(new DocPage
            {
                Id = "mcp-dashboard", Tab = DocRegistry.TAB_MANUAL, Order = 20,
                Title = "The MCP Dashboard",
                SearchText = "dashboard window status auto start port settings action history audit " +
                             "what the assistant changed",
                Build = BuildDashboard,
            });
            pages.Add(new DocPage
            {
                Id = "updating", Tab = DocRegistry.TAB_MANUAL, Order = 30,
                Title = "Updates and version pinning",
                SearchText = "update updating version check github releases cache rate limit " +
                             "notification pin tag packages-lock downgrade rollback branch",
                Build = BuildUpdating,
            });
            pages.Add(new DocPage
            {
                Id = "multiple-editors", Tab = DocRegistry.TAB_MANUAL, Order = 40,
                Title = "Several Unity editors at once",
                SearchText = "multiple instances editors projects port conflict select instance " +
                             "which project team shared registry parrelsync clone discovery 7890 7899",
                Build = BuildMultipleEditors,
            });
        }

        private static void BuildHowItWorks(VisualElement host)
        {
            DocUI.H1(host, "How it works");
            DocUI.Para(host,
                "Three processes, two hops, all on your machine:");
            DocUI.Table(host,
                ("Your AI client", "Claude Desktop, Claude Code, Cursor, Codex, Kimi — anything that speaks " +
                                   "the Model Context Protocol. It launches the MCP server as a child process."),
                ("The MCP server", "Node.js. Exposes the Unity tool surface as MCP tools and forwards each " +
                                   "call to the editor over HTTP."),
                ("The Unity plugin", "Runs inside the editor, listens on <b>127.0.0.1</b> on a port it " +
                                     "assigns from " + MCPForUnityBridge.PortRangeLabel + ", executes the " +
                                     "call on the main thread and answers with JSON."));

            DocUI.H2(host, "What the assistant can reach");
            DocUI.Para(host,
                "The plugin ships a large tool surface — scenes and hierarchy, GameObjects and components, " +
                "assets and prefabs, materials and shader graphs, terrain, physics, NavMesh, the profiler, " +
                "the test runner, builds, the console, and more. The authoritative list is the one the " +
                "server advertises to your client; the repository README keeps it current.");
            DocUI.Actions(host, ("Tool list on GitHub", "url:" + MCPForUnityPlugin.RepositoryUrl));

            DocUI.H2(host, "Security posture");
            DocUI.Bullets(host,
                "The bridge binds to <b>127.0.0.1</b> only — it is not reachable from your network.",
                "There is no cloud component. The installer's only outbound call is to the GitHub releases " +
                "API, to read a version tag. The plugin's Welcome window downloads the public AnkleBreaker catalogue " +
                "(raw.githubusercontent.com) and devlog (anklebreaker-studio.com). Nothing is uploaded.",
                "Every operation the assistant performs is recorded in the plugin's " +
                DocUI.Link("Action History", "page:mcp-dashboard") + ", so nothing changes your project unseen.");
            DocUI.Callout(host, "warn",
                "An assistant connected through the bridge can modify and delete project assets. Work on " +
                "a project under version control, and read the Action History when something surprises you.");

            DocUI.Actions(host,
                ("Next: connect a client", "page:client-setup"),
                ("How updates work", "page:updating"),
                ("Open MCP Dashboard", "action:open-dashboard"));
        }

        private static void BuildClientSetup(VisualElement host)
        {
            DocUI.H1(host, "Connecting your AI client");
            DocUI.Para(host,
                "Every MCP client is configured the same way: you tell it how to launch the server " +
                "process. The plugin must be installed and Unity open for the connection to do anything.");

            DocUI.H2(host, "The short way: ask the assistant to do it");
            DocUI.Para(host,
                "Most MCP clients can install their own servers. Paste this to yours and it writes its " +
                "own configuration entry - that is both numbered steps below, done for you:");
            DocUI.Code(host, MCPForUnityPlugin.AssistantInstallRequest);
            DocUI.Callout(host, "tip",
                "It still needs Node.js " + MCPForUnityPlugin.MinimumNodeMajor + " or newer on the " +
                "machine. The plugin setup window's step 2 checks for it and reports where it found it - " +
                "along with the rest of the setup, measured rather than assumed.");
            DocUI.Para(host,
                "The rest of this page is the same setup done by hand, which is what you want when the " +
                "assistant cannot edit its own config, when you are configuring a machine for someone " +
                "else, or when you would simply rather see the file.");

            DocUI.H2(host, "1. Get the server");
            DocUI.Para(host,
                "The simplest route is to let npm fetch it when the client launches it — nothing to " +
                "clone, nothing to update by hand. Requires Node.js 18 or newer on the PATH:");
            DocUI.Code(host, "npx -y anklebreaker-unity-mcp@latest");
            DocUI.Para(host, "Or clone it, if you want to read or modify the server:");
            DocUI.Code(host,
                "git clone https://github.com/AnkleBreaker-Studio/unity-mcp-server.git\n" +
                "cd unity-mcp-server\n" +
                "npm install");

            DocUI.H2(host, "2. Add the server entry");
            DocUI.Para(host,
                "Claude Desktop, Cursor and Kimi take the same <b>mcpServers</b> entry — only the file it " +
                "goes in changes. This is the npx form; if you cloned instead, use " +
                "<b>\"command\": \"node\"</b> with <b>\"args\": [\"&lt;clone&gt;/src/index.js\"]</b>.");
            DocUI.Code(host,
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"unity\": {\n" +
                "      \"command\": \"npx\",\n" +
                "      \"args\": [\"-y\", \"anklebreaker-unity-mcp@latest\"]\n" +
                "    }\n" +
                "  }\n" +
                "}");
            DocUI.Callout(host, "tip",
                "No <b>UNITY_BRIDGE_PORT</b> in that entry, on purpose. The server discovers running " +
                "editors by itself and connects to the only one, or asks you which. Set the port only " +
                "when you want to force a specific editor — see " +
                DocUI.Link("Several Unity editors at once", "page:multiple-editors") + ".");

            DocUI.H3(host, "Where the file lives");
            DocUI.Table(host,
                ("Claude Desktop — Windows", "<b>%APPDATA%\\Claude\\claude_desktop_config.json</b>, " +
                                             "or Settings > Developer > Edit Config"),
                ("Claude Desktop — macOS", "<b>~/Library/Application Support/Claude/claude_desktop_config.json</b>"),
                ("Claude Desktop — Linux", "<b>~/.config/Claude/claude_desktop_config.json</b>"),
                ("Cursor", "<b>~/.cursor/mcp.json</b> for every project, or <b>.cursor/mcp.json</b> " +
                           "inside one project"),
                ("Kimi CLI", "<b>~/.kimi/mcp.json</b>"),
                ("Claude Code", "No file to edit — one command, below"),
                ("Codex CLI", "TOML rather than JSON, and its own command — see below"),
                ("VS Code", "<b>.vscode/mcp.json</b> inside the project. The plugin setup window's check " +
                            "reads it too; its format is VS Code's own, see its documentation"));

            DocUI.H3(host, "Claude Code, from the terminal");
            DocUI.Code(host, "claude mcp add unity -- npx -y anklebreaker-unity-mcp@latest");
            DocUI.Para(host,
                "Add <b>--scope project</b> to write it into the repository's <b>.mcp.json</b> instead of " +
                "your own machine, so the whole team gets the same server.");

            DocUI.H3(host, "Codex, from the terminal");
            DocUI.Code(host, "codex mcp add unity -- npx -y anklebreaker-unity-mcp@latest");
            DocUI.Para(host,
                "Codex is the one client that does not read the JSON above. It keeps its servers in " +
                "<b>~/.codex/config.toml</b> — or <b>.codex/config.toml</b> inside a trusted project — " +
                "and the command writes the entry for you. By hand it reads:");
            DocUI.Code(host,
                "[mcp_servers.unity]\n" +
                "command = \"npx\"\n" +
                "args = [\"-y\", \"anklebreaker-unity-mcp@latest\"]");
            DocUI.Callout(host, "warn",
                "The table name is <b>mcp_servers</b>, with an underscore. Codex ignores a " +
                "<b>mcpServers</b> or <b>mcp-servers</b> table silently — the file looks right and the " +
                "server never appears.");

            DocUI.Callout(host, "info",
                "Restart the client after editing its config. Most of them read it only at launch, which " +
                "is the single most common reason a correct entry appears to do nothing.");
            DocUI.Callout(host, "info",
                "The server and the Unity plugin ship on <b>separate release trains</b>, so their version " +
                "numbers will not match — that is normal, not a mismatch to fix. They negotiate a " +
                "protocol version on connection and degrade gracefully across the gap.");
            DocUI.Actions(host, ("Server repository", "url:" + MCPForUnityPlugin.ServerRepositoryUrl));

            DocUI.H2(host, "Environment variables");
            DocUI.Para(host,
                "All optional. Add them under an <b>\"env\"</b> key in the entry above, an " +
                "<b>[mcp_servers.unity.env]</b> table in the Codex file, or with <b>-e KEY=value</b> on " +
                "<b>claude mcp add</b> and <b>--env KEY=VALUE</b> on <b>codex mcp add</b>.");
            DocUI.Table(host,
                ("UNITY_HUB_PATH", "Unity Hub executable, for the Hub tools. The server's built-in default " +
                                   "is the Windows path, so <b>macOS and Linux users must set this</b> — " +
                                   "see the table below."),
                ("UNITY_BRIDGE_HOST", "Editor bridge host. Default: 127.0.0.1"),
                ("UNITY_BRIDGE_PORT", "Force one editor's port. Unset by default, and better left unset — " +
                                      "the server discovers editors on its own."),
                ("UNITY_BRIDGE_TIMEOUT", "Request timeout in milliseconds. Default: 60000"),
                ("UNITY_MCP_DEBUG", "Set to 1 for verbose server logging"));

            DocUI.H3(host, "Unity Hub, per platform");
            DocUI.Table(host,
                ("Windows", "C:\\Program Files\\Unity Hub\\Unity Hub.exe  (the server's default)"),
                ("macOS", "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"),
                ("Linux", "The Unity Hub AppImage, wherever you installed it"));

            DocUI.H2(host, "3. Check the connection");
            DocUI.Para(host,
                "With Unity open, ask the assistant something that needs the editor — the name of the " +
                "open scene, for instance. If it cannot answer, confirm the bridge itself is alive first:");
            DocUI.Code(host, MCPForUnityBridge.DescribePingUrl());
            DocUI.Actions(host,
                ("Troubleshooting", "page:troubleshooting"),
                ("Open MCP Dashboard", "action:open-dashboard"));
        }

        private static void BuildDashboard(VisualElement host)
        {
            DocUI.H1(host, "The MCP Dashboard");
            DocUI.Para(host,
                "The plugin adds its own windows once installed. They are the plugin's, not this " +
                "asset's — this page just tells you where they are.");
            DocUI.Table(host,
                (MCPForUnityPlugin.DashboardMenuPath,
                    "The MCP Dashboard: bridge status, port, auto-start and settings. Also opens from the " +
                    "button below, from the " + DocUI.Link("plugin setup window", "action:open-welcome") +
                    ", or from <b>Open Dashboard...</b> in the plugin's toolbar button."),
                (MCPForUnityPlugin.ActionHistoryMenuPath,
                    "Every operation performed through the bridge, in order — the audit trail for " +
                    "anything an assistant changed."),
                (MCPForUnityPlugin.PluginWelcomeMenuPath, "The Welcome window: getting started, more " +
                    "AnkleBreaker assets and studio news. It opens by itself once per project."));
            DocUI.Callout(host, "info",
                "The <b>Dashboard</b> menu entry arrived with plugin 2.39.7. An older plugin has no working " +
                "one: it registered the dashboard at <b>Window > AB Unity MCP</b> itself, the path of its " +
                "own submenu, so Unity hid it. With an older plugin, use the button below, CLICKME or the " +
                "toolbar button: they work with every plugin version. The button below and CLICKME use the " +
                "menu entry when the plugin has one and open the window directly otherwise.");
            DocUI.Callout(host, "info",
                "These windows only exist while the plugin is installed. Before that, a link to one " +
                "logs a warning rather than failing silently.");
            DocUI.Actions(host,
                ("Open MCP Dashboard", "action:open-dashboard"),
                ("Open Action History", "menu:" + MCPForUnityPlugin.ActionHistoryMenuPath));
        }

        private static void BuildUpdating(VisualElement host)
        {
            DocUI.H1(host, "Updates and version pinning");

            DocUI.H2(host, "How the check works");
            DocUI.Para(host,
                "This asset asks the GitHub releases API which tag is newest and compares it to the " +
                "version Package Manager reports for the installed package. The answer is cached for " +
                "<b>six hours</b>: the API allows 60 unauthenticated calls an hour per IP, and a checker " +
                "that spent them on every domain reload would start failing exactly when you need it.");
            DocUI.Para(host,
                "<b>Check for updates</b> in the plugin setup window bypasses the cache. So does an install.");
            DocUI.Actions(host, ("Check for updates now", "action:check-updates"));

            DocUI.H2(host, "How you are told");
            DocUI.Para(host,
                "Once per version, as a single console line — never a popup, never twice for the same " +
                "release. The plugin setup window and the setup asset show the same state whenever you look " +
                "at them. Turn the console line off with <b>Tell me when a plugin update ships</b> in " +
                "the plugin setup window footer.");

            DocUI.H2(host, "Why installs are pinned to a tag");
            DocUI.Para(host,
                "Unity records the resolved commit of a git dependency in <b>packages-lock.json</b> and " +
                "keeps honouring it. Adding the bare repository URL a second time is a no-op — you press " +
                "Update and nothing happens. Adding <b>...git#vX.Y.Z</b> changes the dependency string, " +
                "which forces a re-resolve, and leaves the project on a version your teammates get too.");
            DocUI.Callout(host, "info",
                "If the latest-release lookup fails when you press Install (the GitHub API rate limit, " +
                "say), this asset falls back to the repository's default branch rather than failing. It " +
                "clones the same github.com URL, so it does not help when GitHub itself is unreachable. " +
                "It then says so on the plugin setup window's status card, with a button to reinstall " +
                "pinned once the lookup works again.");

            DocUI.H2(host, "Going back to an older release");
            DocUI.Para(host,
                "Add the repository at the tag you want, by hand, through <b>Window > Package Manager > " +
                "+ > Add package from git URL</b>:");
            DocUI.Code(host, MCPForUnityPlugin.GitUrl + "#v1.2.3");
            DocUI.Callout(host, "tip",
                "After a downgrade this asset will correctly report that a newer release exists. That is " +
                "not a bug — it is the same comparison, run against the version you chose.");
            DocUI.Actions(host,
                ("All releases", "url:" + MCPForUnityPlugin.ReleasesUrl),
                ("Open Package Manager", "action:open-package-manager"));
        }

        private static void BuildMultipleEditors(VisualElement host)
        {
            DocUI.H1(host, "Several Unity editors at once");
            DocUI.Para(host,
                "This works, and it needs no configuration. The bridge serves the <b>running editor</b>, " +
                "not a folder on disk, and each editor claims its own port.");

            DocUI.H2(host, "How the port is chosen");
            DocUI.Para(host,
                "The plugin picks a free port from <b>" + MCPForUnityBridge.PortRangeLabel + "</b> — it " +
                "does not insist on one. So a second project does not collide with the first, and the " +
                "port a given project gets <b>can differ between sessions</b>. Never hardcode the one " +
                "you saw yesterday.");
            DocUI.Para(host,
                "Every running editor registers its port, project name and path in a small file shared " +
                "by the whole machine. The MCP server reads that file to discover editors, and so does " +
                "this asset when it tells you which address your bridge is on:");
            DocUI.Code(host,
                "Windows        %LOCALAPPDATA%\\UnityMCP\\instances.json\n" +
                "macOS, Linux   ~/.local/share/UnityMCP/instances.json");

            DocUI.H2(host, "Which editor does the assistant talk to?");
            DocUI.Bullets(host,
                "One editor open: the server finds it and connects. Nothing to do.",
                "Several open: the server lists them and asks you which project to work with.",
                "To force one, set <b>UNITY_BRIDGE_PORT</b> in that client's config to the port the " +
                "MCP Dashboard reports for the editor you mean — and remember it may move next session.",
                "ParrelSync clones and multiplayer virtual players each register separately, so they " +
                "are selectable like any other editor.");

            DocUI.Callout(host, "tip",
                "The reliable habit is not \"keep one editor open\" — it is \"read the port instead of " +
                "assuming it\". The plugin setup window's step 4 and the CLICKME asset both show the live one " +
                "for this project.");
            DocUI.Actions(host,
                ("Open MCP Dashboard", "action:open-dashboard"),
                ("Open the plugin setup", "action:open-welcome"),
                ("Client setup", "page:client-setup"));
        }
    }
}
