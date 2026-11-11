using System.Collections.Generic;
using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>"Reference" tab — what to do when it breaks, and where everything lives.</summary>
    internal static class Pages_Reference
    {
        public static void Register(List<DocPage> pages)
        {
            pages.Add(new DocPage
            {
                Id = "troubleshooting", Tab = DocRegistry.TAB_REFERENCE, Order = 0,
                Title = "Troubleshooting",
                SearchText = "troubleshooting problem error install failed git not found path rate " +
                             "limit 403 offline connection refused port in use assistant cannot see " +
                             "unity nothing happens update button wrong port changed reassigned 7890 7899",
                Build = BuildTroubleshooting,
            });
            pages.Add(new DocPage
            {
                Id = "faq", Tab = DocRegistry.TAB_REFERENCE, Order = 10,
                Title = "FAQ",
                SearchText = "faq questions free price license open source telemetry privacy data " +
                             "build size runtime does it ship in my game team source control",
                Build = BuildFaq,
            });
            pages.Add(new DocPage
            {
                Id = "whats-in-the-box", Tab = DocRegistry.TAB_REFERENCE, Order = 20,
                Title = "What ships in this asset",
                SearchText = "what ships contents files assemblies menu items preferences editorprefs " +
                             "settings keys folder structure",
                Build = BuildContents,
            });
            pages.Add(new DocPage
            {
                Id = "links", Tab = DocRegistry.TAB_REFERENCE, Order = 30,
                Title = "Links and support",
                SearchText = "links support github discord issues sponsor patreon studio publisher " +
                             "review asset store server repository",
                Build = BuildLinks,
            });
        }

        private static void BuildTroubleshooting(VisualElement host)
        {
            DocUI.H1(host, "Troubleshooting");

            DocUI.H2(host, "The install fails with a git error");
            DocUI.Para(host,
                "Unity resolves git packages with the <b>git command line client</b>. Install git, make " +
                "sure it is on the PATH, then <b>restart Unity</b> — Package Manager reads the PATH once, " +
                "at editor startup, so a git installed while Unity was open is invisible to it.");

            DocUI.H2(host, "\"Update\" seems to do nothing");
            DocUI.Para(host,
                "If the plugin was added as a bare repository URL, Unity holds the resolved commit in " +
                "<b>packages-lock.json</b> and re-adding the same URL is a no-op. This asset avoids that " +
                "by pinning the release tag. Check the plugin setup window still reports an update after the " +
                "recompile; if it does, remove the package and install it again from here.");
            DocUI.Actions(host,
                ("Open Package Manager", "action:open-package-manager"),
                ("Install the plugin", "action:install-plugin"));

            DocUI.H2(host, "The update check says it failed");
            DocUI.Bullets(host,
                "<b>Rate limit</b> — GitHub allows 60 unauthenticated calls an hour per IP. The result " +
                "is cached for six hours precisely to stay well inside that; wait, or keep working.",
                "<b>Offline or behind a proxy</b> — the check needs api.github.com. Everything else in " +
                "the asset keeps working. An install started while the lookup fails but github.com " +
                "answers (a rate limit, say) falls back to the default branch; with GitHub unreachable " +
                "the install itself cannot download the plugin.",
                "<b>No release published</b> — only relevant if the repository has never tagged one.");

            DocUI.H2(host, "The assistant cannot see Unity");
            DocUI.Para(host, "Work down the chain — each step rules out one hop:");
            DocUI.Bullets(host,
                "Is Unity open, with the project you mean, and finished compiling?",
                "Does " + MCPForUnityBridge.DescribePingUrl() + " answer in a browser? If not, the plugin " +
                "is not serving — check the " + DocUI.Link("MCP Dashboard", "page:mcp-dashboard") + ".",
                "Does the client's server entry point at a real path, with Node 18 or newer on the PATH?",
                "Did you restart the AI client after editing its config? Most clients read it only at launch.",
                "Pinned <b>UNITY_BRIDGE_PORT</b> in your client config? The plugin reassigns ports from " +
                MCPForUnityBridge.PortRangeLabel + ", so a port that worked last week can belong to " +
                "another project today — see " +
                DocUI.Link("Several Unity editors at once", "page:multiple-editors") + ".");

            DocUI.H2(host, "The port is not the one the docs showed me");
            DocUI.Para(host,
                "Expected. The plugin assigns its port from <b>" + MCPForUnityBridge.PortRangeLabel +
                "</b> and can pick a different one after a restart or when another editor is already " +
                "running — this is by design, not a fault. Read the live one from the MCP Dashboard, " +
                "from the plugin setup window's step 4, or from the CLICKME asset's inspector. Nothing in " +
                "this asset hardcodes a port, and neither should your notes.");

            DocUI.H2(host, "The version shows as \"unmanaged copy\"");
            DocUI.Para(host,
                "The plugin's assembly is loaded but Package Manager does not own it — usually because " +
                "it was copied into <b>Assets/</b>. Its version cannot be read, so this asset refuses to " +
                "guess whether it is current. Remove the copy and install through Package Manager to get " +
                "update checking back.");

            DocUI.H2(host, "Nothing here matches");
            DocUI.Para(host,
                "The " + DocUI.Link("FAQ", "page:faq") + " covers the questions that are not failures, " +
                "and " + DocUI.Link("What ships in this asset", "page:whats-in-the-box") +
                " lists every file, menu item and setting it owns. Otherwise, open an issue with your " +
                "Unity version, the plugin version from the plugin setup window, and the console output. " +
                DocUI.Link("Discord", "url:" + MCPForUnityPlugin.DiscordUrl) +
                " is usually faster for a question.");
            DocUI.Actions(host,
                ("Report an issue", "url:" + MCPForUnityPlugin.IssuesUrl),
                ("Join the Discord", "url:" + MCPForUnityPlugin.DiscordUrl));
        }

        private static void BuildFaq(VisualElement host)
        {
            DocUI.H1(host, "FAQ");

            DocUI.H3(host, "Is any of this paid?");
            DocUI.Para(host,
                "No. This asset is free and the MCP plugin it installs is free and open source. " +
                "AnkleBreaker Studio funds the work through " +
                DocUI.Link("GitHub Sponsors", "url:" + MCPForUnityPlugin.SponsorsUrl) + ".");

            DocUI.H3(host, "Does any of it end up in my game build?");
            DocUI.Para(host,
                "No. This asset is editor-only — it ships no runtime code at all, so there is nothing " +
                "for a player build to contain. The plugin is likewise an editor tool.");

            DocUI.H3(host, "Does it send my project anywhere?");
            DocUI.Para(host,
                "No. This asset only downloads: the latest release tag from the GitHub API, and for the " +
                "plugin's Welcome window the public AnkleBreaker catalogue and card images (raw.githubusercontent.com) " +
                "and the latest devlog post (anklebreaker-studio.com). The bridge itself binds to <b>127.0.0.1</b> and is not reachable from " +
                "your network. What your AI client does with what it reads is between you and that client.");

            DocUI.H3(host, "Can I commit the plugin to source control?");
            DocUI.Para(host,
                "Commit <b>Packages/manifest.json</b> and <b>packages-lock.json</b> — that is what makes " +
                "your teammates resolve the same version. Do not commit the resolved package folder itself.");

            DocUI.H3(host, "Do I need this asset once the plugin is installed?");
            DocUI.Para(host,
                "Not strictly — the plugin stands on its own. Keeping this asset is what gives you the " +
                "update check, the pinned installs, and this manual.");

            DocUI.H3(host, "Which Unity versions work?");
            DocUI.Para(host,
                "The plugin declares Unity <b>" + MCPForUnityPlugin.MinimumUnityVersion + ".18f1</b> or " +
                "newer and is tested on 2022.3 LTS and Unity 6. This asset targets the same range, on " +
                "Windows, macOS and Linux editors alike.");

            DocUI.Actions(host, ("Troubleshooting", "page:troubleshooting"), ("Links", "page:links"));
        }

        private static void BuildContents(VisualElement host)
        {
            DocUI.H1(host, "What ships in this asset");

            DocUI.H2(host, "Assemblies");
            DocUI.Table(host,
                ("AnkleBreaker.MCPForUnity.Installer.Editor",
                    "Plugin identity, presence probe, update checker, installer, dependency gate and the " +
                    "CLICKME inspector. References nothing, so it keeps compiling in any project state."),
                ("AnkleBreaker.MCPForUnity.Editor",
                    "The plugin setup window and this documentation window. References the installer assembly."));
            DocUI.Callout(host, "info",
                "Both are editor-only. There is no runtime assembly — this asset has nothing to say at " +
                "play time.");

            DocUI.H2(host, "Ways in");
            DocUI.Table(host,
                ("CLICKME.asset", "At the package root. Until the plugin is installed, it opens the plugin " +
                                  "setup. Then its inspector is the hub the plugin ships — status, the " +
                                  "Welcome and its tabs, and four shortcuts."),
                ("Tools > AnkleBreaker > Unity MCP > Welcome", "Ships with the plugin: getting started, more " +
                                                       "AnkleBreaker assets and studio news. Opens by itself " +
                                                       "once per project."),
                ("Tools > AnkleBreaker > MCP For Unity > Documentation", "This window."),
                ("Tools > AnkleBreaker > Dependencies Installer > MCP For Unity", "The plugin setup window: " +
                                                            "install, update and the four setup checks. " +
                                                            "The installer menu every AnkleBreaker asset has."));
            DocUI.Callout(host, "info",
                "Three menu items and one asset, one window each. The Welcome belongs to the plugin, so it " +
                "is the one AnkleBreaker also shows to people who install the plugin from GitHub. CLICKME " +
                "also opens the plugin setup.");

            DocUI.H2(host, "Settings stored on this machine");
            DocUI.Para(host,
                "All EditorPrefs, all per-machine — none of them belong in a shared project. The two " +
                "marked <b>per project</b> carry a digest of the project path (the Welcome window: the project's GUID), because \"we have already " +
                "introduced this asset to you\" is a statement about a project, not about a machine.");
            DocUI.Table(host,
                ("AB.MCPForUnity.AutoOpenWhenMissing", "Open the plugin setup window when the plugin is absent."),
                ("AB.MCPForUnity.NotifyUpdates", "Log one console line per new release."),
                ("AB.MCPForUnity.LastNotifiedVersion", "The release already reported, so it is never reported twice."),
                ("AB.MCPForUnity.SetupStepDone*.*", "<b>Per project.</b> Which of the plugin setup window's " +
                                                    "out-of-Unity setup steps you marked done by hand, for " +
                                                    "the cases the automatic check cannot see."),
                ("AB.MCPForUnity.AutoVerifySetup", "Whether the plugin setup window re-verifies the four setup " +
                                                   "steps by itself, or only when you press Re-check now."),
                ("AB.MCPForUnity.SimpleServerMethod", "Which route step 2 opens on: the request to paste to " +
                                                      "an assistant, or the commands to run by hand."),
                ("AB.MCPForUnity.Cache.*", "The cached release lookup and its timestamp (six-hour lifetime)."),
                ("AB.MCPForUnity.Docs.*", "Documentation window state and guided-tour checkmarks."),
                ("AB.MCPForUnity.CanonicalWelcome.*", "<b>Per project.</b> Written by the plugin. The Welcome window's first " +
                                                      "opening, visited steps, open count and review prompt, " +
                                                      "and the CLICKME hub's visits."));
            DocUI.Callout(host, "info",
                "One more piece of state is <i>not</i> a preference: closing the plugin setup window while the " +
                "plugin is missing is remembered for the current editor session only, so it stops " +
                "reopening on every recompile and asks again next time you launch Unity.");

            DocUI.Actions(host, ("API overview", "page:api-overview"), ("Links", "page:links"));
        }

        private static void BuildLinks(VisualElement host)
        {
            DocUI.H1(host, "Links and support");
            DocUI.Table(host,
                ("Unity plugin", DocUI.Link(MCPForUnityPlugin.RepositoryUrl, "url:" + MCPForUnityPlugin.RepositoryUrl)),
                ("Releases", DocUI.Link(MCPForUnityPlugin.ReleasesUrl, "url:" + MCPForUnityPlugin.ReleasesUrl)),
                ("MCP server", DocUI.Link(MCPForUnityPlugin.ServerRepositoryUrl, "url:" + MCPForUnityPlugin.ServerRepositoryUrl)),
                ("Report an issue", DocUI.Link(MCPForUnityPlugin.IssuesUrl, "url:" + MCPForUnityPlugin.IssuesUrl)),
                ("Discord", DocUI.Link(MCPForUnityPlugin.DiscordUrl, "url:" + MCPForUnityPlugin.DiscordUrl)),
                ("Sponsor the work", DocUI.Link(MCPForUnityPlugin.SponsorsUrl, "url:" + MCPForUnityPlugin.SponsorsUrl)),
                ("AnkleBreaker Studio", DocUI.Link(MCPForUnityPlugin.StudioUrl, "url:" + MCPForUnityPlugin.StudioUrl)),
                ("Model Context Protocol", DocUI.Link("modelcontextprotocol.io", "url:https://modelcontextprotocol.io")));

            DocUI.Divider(host);
            DocUI.Para(host,
                "If this asset saved you time, a review on its Asset Store page helps other developers " +
                "find it — and a star on the plugin repository helps the plugin.");
            DocUI.Actions(host,
                ("Leave a Review", "url:" + MCPForUnityPlugin.AssetStoreUrl),
                ("Star the plugin", "url:" + MCPForUnityPlugin.RepositoryUrl),
                ("Back to Welcome", "page:welcome"));
        }
    }
}
