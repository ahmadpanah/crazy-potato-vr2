using System.Collections.Generic;
using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>"Get Started" tab — welcome, quick start, guided tour, installation.
    /// Written for someone who has just imported the asset and has no plugin yet.</summary>
    internal static class Pages_GetStarted
    {
        public static void Register(List<DocPage> pages)
        {
            pages.Add(new DocPage
            {
                Id = "welcome", Tab = DocRegistry.TAB_START, Order = 0,
                Title = "Welcome",
                SearchText = "welcome what is mcp for unity overview intro model context protocol " +
                             "ai assistant claude cursor codex kimi bridge free",
                Build = BuildWelcome,
            });
            pages.Add(new DocPage
            {
                Id = "quick-start", Tab = DocRegistry.TAB_START, Order = 10,
                Title = "Quick Start (5 minutes)",
                SearchText = "quick start first setup five minutes install plugin server client " +
                             "connect ping port 7890",
                Build = BuildQuickStart,
            });
            pages.Add(new DocPage
            {
                Id = "guided-tour", Tab = DocRegistry.TAB_START, Order = 20,
                Title = "Guided Tour",
                SearchText = "guided tour onboarding steps checklist walkthrough learn dashboard " +
                             "action history",
                Build = BuildTour,
            });
            pages.Add(new DocPage
            {
                Id = "installation", Tab = DocRegistry.TAB_START, Order = 30,
                Title = "Installation",
                SearchText = "installation install package manager git url upm requirements unity " +
                             "version node js pinned tag remove uninstall without git zip add package from disk",
                Build = BuildInstallation,
            });
        }

        private static void BuildWelcome(VisualElement host)
        {
            DocUI.H1(host, "Welcome to MCP For Unity");
            DocUI.Para(host,
                "This asset is the front door to the <b>AnkleBreaker Unity MCP plugin</b> — a free, " +
                "open-source bridge that lets MCP-compatible AI assistants read and drive the Unity " +
                "Editor: scenes, GameObjects, components, assets, builds, profiling, shader graphs and more.");
            DocUI.Bullets(host,
                "<b>Install in one click</b> — no Package Manager git URL to copy by hand.",
                "<b>Pinned to a release</b> — the version you get is a published tag, not whatever " +
                "the default branch happened to be that day.",
                "<b>Stays current</b> — the asset checks GitHub for newer releases and tells you once, " +
                "never twice, per version.",
                "<b>Documented in-editor</b> — this window, searchable, with buttons that do the thing " +
                "they describe.");

            DocUI.Divider(host);
            DocUI.H2(host, "The two halves of a working setup");
            DocUI.Para(host,
                "Everything runs on your machine. The installer only asks GitHub which release is newest; " +
                "the plugin's Welcome window also reads the public AnkleBreaker catalogue (raw.githubusercontent.com) " +
                "and the studio devlog (anklebreaker-studio.com). Nothing is sent about your project.");
            DocUI.Table(host,
                ("Unity plugin", "Runs inside the editor and serves a local HTTP bridge bound to " +
                                 "127.0.0.1, on a port it assigns itself from " +
                                 MCPForUnityBridge.PortRangeLabel + ". This is what the asset installs."),
                ("MCP server", "A small Node.js process your AI client launches. It speaks MCP to the " +
                               "client and HTTP to the plugin. " +
                               DocUI.Link("Setup guide", "page:client-setup") + "."));

            DocUI.Callout(host, "tip",
                "New here? Take the " + DocUI.Link("Quick Start", "page:quick-start") +
                " — it is five minutes end to end. Prefer the long version? " +
                DocUI.Link("Installation", "page:installation") + " covers requirements, manual " +
                "installs (with git, and without it) and removal.");

            DocUI.Actions(host,
                ("Open the plugin setup", "action:open-welcome"),
                ("Quick Start", "page:quick-start"),
                ("Plugin on GitHub", "url:" + MCPForUnityPlugin.RepositoryUrl));
        }

        private static void BuildQuickStart(VisualElement host)
        {
            DocUI.H1(host, "Quick Start — up and running in 5 minutes");

            DocUI.H2(host, "1. Install the Unity plugin");
            DocUI.Para(host,
                "Select <b>CLICKME.asset</b> at the package root, press <b>Open the plugin setup</b>, then " +
                "<b>Install the plugin</b>. Unity adds <b>" + MCPForUnityPlugin.PackageId +
                "</b> through Package Manager, pinned to the latest published release, then recompiles.");
            DocUI.Callout(host, "info",
                "Unity resolves git packages with the git command line client. If the install fails " +
                "with a git error, install git, make sure it is on the PATH, and restart Unity — " +
                "Package Manager only reads the PATH at editor startup.");
            DocUI.Actions(host, ("Install now", "action:install-plugin"));

            DocUI.H2(host, "2. Confirm the bridge is live");
            DocUI.Para(host,
                "Once Unity finishes recompiling, the console prints <b>[AB-UMCP] Server started on " +
                "port &lt;n&gt;</b>. The MCP Dashboard shows the same status inside the editor — the " +
                "button below opens it. Or visit the ping endpoint in a browser:");
            DocUI.Code(host, MCPForUnityBridge.DescribePingUrl());
            DocUI.Para(host,
                "It answers with JSON carrying your Unity version and project name. " +
                "If it does, the Unity half is done.");
            DocUI.Callout(host, "info",
                "That port is not fixed. The plugin picks a free one from <b>" +
                MCPForUnityBridge.PortRangeLabel + "</b> and can land on a different one after a " +
                "restart, or when another editor is already running. Read the current one from the " +
                "MCP Dashboard, or from the step above — this page fills it in live when a bridge " +
                "is up. See " + DocUI.Link("Several Unity editors at once", "page:multiple-editors") + ".");
            DocUI.Actions(host, ("Open MCP Dashboard", "action:open-dashboard"));

            DocUI.H2(host, "3. Install the MCP server");
            DocUI.Para(host,
                "The server is a Node.js process (Node 18 or newer). The short way is to let npm " +
                "fetch it on demand — there is nothing to clone and nothing to keep updated:");
            DocUI.Code(host, "npx -y anklebreaker-unity-mcp@latest");
            DocUI.Para(host, "Or clone it, if you want to read or modify it:");
            DocUI.Code(host,
                "git clone https://github.com/AnkleBreaker-Studio/unity-mcp-server.git\n" +
                "cd unity-mcp-server\n" +
                "npm install");
            DocUI.Callout(host, "info",
                "You do not run this command yourself day to day — your AI client launches it. Step 4 " +
                "is where you tell the client how.");

            DocUI.H2(host, "4. Point your AI client at the server");
            DocUI.Para(host,
                "Add the server to your client's MCP configuration. The " +
                DocUI.Link("client setup page", "page:client-setup") + " carries the exact entry for " +
                "Claude Desktop, Claude Code, Cursor, Codex and Kimi, on Windows, macOS and Linux. Restart " +
                "the client, then ask it something about your open scene.");

            DocUI.Callout(host, "tip",
                "Keep Unity open and focused on the project you want the assistant to work in. The " +
                "bridge serves the running editor, not the project folder on disk.");

            DocUI.Actions(host,
                ("Next: Guided Tour", "page:guided-tour"),
                ("Client setup", "page:client-setup"),
                ("Troubleshooting", "page:troubleshooting"));
        }

        private static void BuildTour(VisualElement host)
        {
            DocUI.H1(host, "Guided Tour");
            DocUI.Para(host,
                "Hands-on steps through everything this asset and the plugin give you. Check each one " +
                "off as you go — the checkmarks are remembered on this machine.");

            DocUI.TourStep(host, 1, "Install the plugin",
                "Press the button below. Watch the console for the Package Manager result and the " +
                "<b>[AB-UMCP] Server started on port &lt;n&gt;</b> line once Unity finishes recompiling.",
                "Install the plugin", "action:install-plugin");

            DocUI.TourStep(host, 2, "Meet the plugin setup window",
                "It is the status page for the whole setup: which version is installed, whether a newer " +
                "one shipped, and the four setup steps. It opens by itself when the plugin is missing; " +
                "reopen it from <b>Tools > AnkleBreaker > Dependencies Installer > MCP For Unity</b>.",
                "Open the plugin setup", "action:open-welcome");

            DocUI.TourStep(host, 3, "Open the MCP Dashboard",
                "The plugin's own control panel: bridge status, port, auto-start and settings. " +
                "Open it from here, or from <b>Window > AB Unity MCP > Dashboard</b> (plugin 2.39.7 " +
                "and later; this button also works with older versions). See " +
                DocUI.Link("The MCP Dashboard", "page:mcp-dashboard") + ".",
                "Open MCP Dashboard", "action:open-dashboard");

            DocUI.TourStep(host, 4, "Look at the Action History",
                "Every operation an assistant performs through the bridge is recorded, so you can see " +
                "exactly what changed in your project and when.",
                "Open Action History", "menu:" + MCPForUnityPlugin.ActionHistoryMenuPath);

            DocUI.TourStep(host, 5, "Run an update check",
                "The asset caches GitHub's answer for six hours so it never burns the API rate limit. " +
                "This button forces a fresh check right now.",
                "Check for updates", "action:check-updates");

            DocUI.TourStep(host, 6, "Connect an AI client",
                "Install the Node.js server and add it to your client's MCP config. This is the only " +
                "step that happens outside Unity.",
                "Read the client setup guide", "page:client-setup");

            DocUI.Callout(host, "tip",
                "Done with the tour? The " + DocUI.Link("Manual", "page:how-it-works") +
                " explains how the bridge actually works, " +
                DocUI.Link("Updates and version pinning", "page:updating") +
                " explains how it stays current, and " +
                DocUI.Link("Troubleshooting", "page:troubleshooting") + " covers what to do when it does not.");
        }

        private static void BuildInstallation(VisualElement host)
        {
            DocUI.H1(host, "Installation");

            DocUI.H2(host, "Requirements");
            DocUI.Table(host,
                ("Unity", MCPForUnityPlugin.MinimumUnityVersion + ".18f1 or newer, as declared by the " +
                          "plugin's package.json (tested on 2022.3 LTS and Unity 6)"),
                ("Platform", "Windows, macOS and Linux editors alike"),
                ("git", "On the PATH — Unity uses it to resolve the package"),
                ("Node.js", "18 or newer, for the MCP server half"),
                ("Network", "GitHub, to download the package and read release tags. The plugin's Welcome window also " +
                            "reads raw.githubusercontent.com and anklebreaker-studio.com; offline, its catalogue " +
                            "and devlog panels hide themselves"));

            DocUI.H2(host, "Installing from this asset");
            DocUI.Para(host,
                "The <b>Install the plugin</b> button resolves the newest published release and adds " +
                "the repository at that tag — for release v1.2.3 it would add:");
            DocUI.Code(host, MCPForUnityPlugin.GitUrl + "#v1.2.3");
            DocUI.Para(host,
                "Pinning the tag is deliberate. Unity records the resolved commit of a git dependency " +
                "in <b>packages-lock.json</b> and keeps honouring it, so re-adding the bare repository " +
                "URL does nothing. Changing the tag is what makes an update actually resolve — and it " +
                "leaves your project on a version a teammate can reproduce.");

            DocUI.H2(host, "Installing by hand");
            DocUI.Para(host,
                "Nothing stops you doing it yourself: <b>Window > Package Manager > + > Add package " +
                "from git URL</b>, then paste:");
            DocUI.Code(host, MCPForUnityPlugin.GitUrl);
            DocUI.Callout(host, "info",
                "Without a <b>#tag</b> suffix you track the repository's default branch. That is fine " +
                "for trying things out, but this asset cannot then tell you which release you are on.");

            DocUI.H2(host, "Without git");
            DocUI.Para(host,
                "Package Manager needs git for a git URL. Without it, download the source zip of a " +
                "release from " + DocUI.Link("the releases page", "url:" + MCPForUnityPlugin.ReleasesUrl) +
                ", extract it <b>outside Assets/</b>, then <b>Window > Package Manager > + > Add package " +
                "from disk</b> and pick its <b>package.json</b>. This asset's Install and Update buttons " +
                "always use git: to update without it, replace the folder with a newer release the same way.");

            DocUI.H2(host, "Removing the plugin");
            DocUI.Para(host,
                "Remove <b>" + MCPForUnityPlugin.PackageId + "</b> from Package Manager like any other " +
                "package. This asset keeps working — it will simply offer to install it again.");
            DocUI.Actions(host,
                ("Open Package Manager", "action:open-package-manager"),
                ("Install the plugin", "action:install-plugin"),
                ("Next: How it works", "page:how-it-works"));
        }
    }
}
