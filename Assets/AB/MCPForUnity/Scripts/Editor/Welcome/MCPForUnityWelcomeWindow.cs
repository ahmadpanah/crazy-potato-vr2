using AnkleBreaker.MCPForUnity.Editor.Docs;
using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Welcome
{
    /// <summary>
    /// The plugin setup window: what the MCP plugin is, whether it is installed, whether it is
    /// current, and one button for whichever of those needs doing.
    ///
    /// <para>The studio Welcome window (Installer/Welcome) owns <c>Tools/AnkleBreaker/MCP For
    /// Unity/Welcome</c> and the first opening. This one is the dependency prompt: opened by the
    /// <c>Tools/AnkleBreaker/Dependencies Installer/MCP For Unity</c> menu, by the startup check
    /// while the plugin is missing, by the CLICKME inspector, the Welcome and doc links, all through
    /// <see cref="MCPForUnityWindows.OpenWelcome"/>.</para>
    ///
    /// <para>It is deliberately usable <b>before</b> the plugin exists in the project — it never
    /// references the plugin's assembly, only the version-agnostic probes in
    /// <see cref="MCPForUnityPlugin"/>. That is the difference between this window and the plugin's
    /// own welcome screen, which cannot help anyone who has not installed it yet.</para>
    /// </summary>
    internal sealed class MCPForUnityWelcomeWindow : EditorWindow
    {
        private const string WELCOME_DIR = "Assets/AB/MCPForUnity/Scripts/Editor/Welcome/";
        private const string UXML_PATH = WELCOME_DIR + "MCPForUnityWelcome.uxml";
        private const string USS_PATH = WELCOME_DIR + "MCPForUnityWelcome.uss";

        // The bridge can start, stop or move port with nothing to raise an event about it, so the
        // live parts of this window are polled. Only a changed signature triggers a rebuild.
        private const long LIVE_POLL_MS = 2000;

        private ScrollView _body;
        private VisualElement _footer;
        private string _signature;

        // The "checking..." line and its button change far more often than the page they sit on, so
        // they are updated in place. Rebuilding the whole body twice per verification pass — once to
        // say it started, once to show what it found — would scroll-jump the reader for nothing.
        private Label _checkStatus;
        private Button _recheckButton;

        /// <summary>
        /// Hands the installer assembly a way to open this window without referencing it, so the
        /// dependency prompt and the setup asset can both route here.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterOpeners()
        {
            MCPForUnityWindows.WelcomeOpener = Open;
            MCPForUnityWindows.DocumentationOpener = () => MCPForUnityDocsWindow.OpenPage(null);
            MCPForUnityWindows.WelcomeIsOpenProbe = HasOpenInstances<MCPForUnityWelcomeWindow>;
        }

        public static void Open()
        {
            var window = GetWindow<MCPForUnityWelcomeWindow>("MCP Plugin Setup");
            window.minSize = new Vector2(560f, 480f);
            window.Show();
            window.Focus();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            var tree = LoadAsset<VisualTreeAsset>(UXML_PATH, "MCPForUnityWelcome t:VisualTreeAsset");
            if (tree != null) tree.CloneTree(rootVisualElement);
            var style = LoadAsset<StyleSheet>(USS_PATH, "MCPForUnityWelcome t:StyleSheet");
            if (style != null) rootVisualElement.styleSheets.Add(style);
            MCPForUnityTheme.Apply(rootVisualElement, "ab-welcome");

            _body = rootVisualElement.Q<ScrollView>("welcome-body");
            _footer = rootVisualElement.Q<VisualElement>("welcome-footer");

            MCPForUnityPluginInstaller.Changed += Rebuild;
            MCPForUnityUpdateChecker.Completed += OnCheckCompleted;
            MCPForUnitySetupCheck.Completed += OnSetupChecked;

            // Opening the window is the moment a stale answer matters most, but a forced check on
            // every open would burn the hourly GitHub quota on someone who just docks the window.
            // The six-hour cache decides.
            MCPForUnityPlugin.Refresh();
            if (MCPForUnityPlugin.IsInstalled) MCPForUnityUpdateChecker.Check();

            // Someone opening this window is asking what state their setup is in, so the answer
            // should not be a stale one — unless they turned the automatic checking off, in which
            // case the footer toggle promised that nothing runs until they press Re-check, and an
            // exception "just on open" would quietly break that promise on every domain reload.
            if (MCPForUnityPrefs.AutoVerifySetup) MCPForUnitySetupCheck.Check(force: true);

            Rebuild();
            rootVisualElement.schedule.Execute(PollLiveState).Every(LIVE_POLL_MS);
        }

        private void OnDisable()
        {
            MCPForUnityPluginInstaller.Changed -= Rebuild;
            MCPForUnityUpdateChecker.Completed -= OnCheckCompleted;
            MCPForUnitySetupCheck.Completed -= OnSetupChecked;
        }

        /// <summary>The editor regained focus: the world outside Unity is exactly what steps 2 and
        /// 3 live in, so this is the likeliest moment for an answer to have gone stale.</summary>
        private void OnFocus()
        {
            // Paced, not forced: alt-tabbing between two windows must not launch a process each time.
            if (_body != null && MCPForUnityPrefs.AutoVerifySetup) MCPForUnitySetupCheck.Check();
        }

        /// <summary>
        /// Closing this window while the plugin is still missing usually means "not now" — so it
        /// has to count as one.
        ///
        /// <para>Without this the startup check re-opened and re-focused the window after
        /// <b>every domain reload</b>: it re-arms on each one, and only the fallback prompt's
        /// button ever set the session dismiss. The case that hurt was the most common failure
        /// this package documents — git missing from the PATH, which cannot be fixed without
        /// restarting Unity, so the user was told to install and then interrupted on every
        /// recompile until they could.</para>
        ///
        /// <para>It routes through <c>NotifyWelcomeClosed</c> rather than <c>SetDismissed</c>
        /// because a window can be closed by the editor as well as by a person — Unity applying
        /// its saved layout during boot closes everything that is open — and only the startup
        /// check knows which of the two just happened. Calling the blunt version here silently
        /// disabled that check's own retry.</para>
        /// </summary>
        private void OnDestroy()
        {
            if (!MCPForUnityPlugin.IsInstalled) MCPForUnityDependenciesInstaller.NotifyWelcomeClosed();
        }

        private void OnCheckCompleted(MCPUpdateCheckResult result) => Rebuild();

        /// <summary>A verification pass landed. Only redraw if it says something new.</summary>
        private void OnSetupChecked(MCPSetupReport report) => PollLiveState();

        /// <summary>Rebuilds only when something a reader would notice actually moved.</summary>
        private void PollLiveState()
        {
            if (_body == null) return;

            // Cheap, and it keeps the "checking..." line truthful between rebuilds.
            UpdateCheckStatus();

            if (MCPForUnityPrefs.AutoVerifySetup) MCPForUnitySetupCheck.Check();
            if (Signature() != _signature) Rebuild();
        }

        private static string Signature()
        {
            MCPForUnityPlugin.Refresh();
            var bridge = MCPForUnityBridge.Resolve();
            var result = MCPForUnityUpdateChecker.LastResult;
            return string.Join("|",
                MCPForUnityPlugin.IsInstalled ? "1" : "0",
                MCPForUnityPlugin.InstalledVersion ?? "-",
                MCPForUnityPlugin.IsTrackingBranch ? "branch" : "pinned",
                MCPForUnityPluginInstaller.IsBusy ? "busy" : "idle",
                MCPForUnityPluginInstaller.StatusMessage,
                bridge.IsServing ? bridge.Port.ToString() : "0",
                result.HasValue ? result.Value.Status.ToString() : "none",
                SetupSignature());
        }

        /// <summary>
        /// What the verification pass found, flattened. Deliberately excludes "a pass is running":
        /// that transition is carried by <see cref="UpdateCheckStatus"/>, which changes one label
        /// instead of rebuilding the page under the reader twice every ten seconds.
        /// </summary>
        private static string SetupSignature()
        {
            var report = MCPForUnitySetupCheck.Last;
            if (!report.HasValue) return "unchecked";

            var text = new System.Text.StringBuilder();
            for (int step = 1; step <= 4; step++)
            {
                var status = report.Value.Step(step);
                text.Append(status.State).Append(':').Append(status.Headline).Append('/')
                    .Append(MCPForUnityPrefs.IsStepDone(step) ? '1' : '0').Append(';');
            }
            return text.ToString();
        }

        private static T LoadAsset<T>(string path, string searchFilter) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            // Asset Store imports let the buyer drop the package anywhere; find it by name rather
            // than showing an unstyled window.
            foreach (var guid in AssetDatabase.FindAssets(searchFilter))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            Debug.LogWarning("[MCP For Unity] Missing asset: " + path);
            return null;
        }

        // -- Content ---------------------------------------------------------

        private void Rebuild()
        {
            if (_body == null) return;

            // A rebuild can be triggered by a poll while the reader is halfway down the page.
            Vector2 scroll = _body.scrollOffset;

            _signature = Signature();
            _body.Clear();
            var host = _body.contentContainer;

            BuildStatusCard(host);
            BuildIntro(host);
            BuildSteps(host);
            BuildResources(host);
            BuildFooter();

            _body.schedule.Execute(() => _body.scrollOffset = scroll);
        }

        private void BuildStatusCard(VisualElement host)
        {
            MCPForUnityPlugin.Refresh();

            bool busy = MCPForUnityPluginInstaller.IsBusy;
            bool installed = MCPForUnityPlugin.IsInstalled;
            var result = MCPForUnityUpdateChecker.LastResult;

            string tone = "warn";
            string badge = "Not installed";
            string title = MCPForUnityPlugin.DisplayName + " is not in this project";
            string detail = "Install it in one click. It is free, open source, and lands in your " +
                            "project through Package Manager.";
            string note = null;

            if (busy)
            {
                tone = "warn";
                badge = "Working";
                title = MCPForUnityPluginInstaller.StatusMessage;
                detail = "Unity will recompile once Package Manager is done.";
            }
            else if (installed)
            {
                string installedLabel = MCPForUnityPlugin.DescribeInstalled();
                tone = "ok";
                badge = "Installed";
                title = MCPForUnityPlugin.DisplayName + " " + installedLabel;
                detail = MCPForUnityUpdateChecker.IsChecking
                    ? "Checking for a newer release..."
                    : "Ready. Open the MCP Dashboard to see the bridge status.";

                if (!MCPForUnityUpdateChecker.IsChecking && result.HasValue)
                {
                    var r = result.Value;
                    switch (r.Status)
                    {
                        case MCPUpdateStatus.UpdateAvailable:
                            tone = "warn";
                            badge = "Update available";
                            title = "v" + r.InstalledVersion + "  ->  v" + r.LatestVersion;
                            detail = string.IsNullOrEmpty(r.LatestReleaseName)
                                ? "A newer release is published."
                                : r.LatestReleaseName;
                            break;
                        case MCPUpdateStatus.UpToDate:
                            detail = "Up to date with the latest release (v" + r.LatestVersion + ").";
                            break;
                        case MCPUpdateStatus.Ahead:
                            badge = "Ahead of release";
                            detail = "This copy (v" + r.InstalledVersion + ") is newer than the latest " +
                                     "published release (v" + r.LatestVersion + ").";
                            break;
                        case MCPUpdateStatus.CheckFailed:
                            tone = "ok";
                            detail = "Installed. Update check failed: " + r.Error;
                            break;
                        case MCPUpdateStatus.Unknown:
                            tone = "warn";
                            badge = "Unmanaged copy";
                            detail = r.Error;
                            break;
                    }
                }

                note = DescribeInstallShape();
            }

            var card = new VisualElement();
            card.AddToClassList("ab-welcome__card");
            card.AddToClassList("ab-welcome__card--" + tone);

            var badgeLabel = new Label(badge.ToUpperInvariant());
            badgeLabel.AddToClassList("ab-welcome__badge");
            badgeLabel.AddToClassList("ab-welcome__badge--" + tone);
            card.Add(badgeLabel);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("ab-welcome__card-title");
            card.Add(titleLabel);

            var detailLabel = new Label(detail);
            detailLabel.AddToClassList("ab-welcome__card-detail");
            card.Add(detailLabel);

            if (!string.IsNullOrEmpty(note))
            {
                var noteLabel = new Label(note);
                noteLabel.AddToClassList("ab-welcome__card-note");
                card.Add(noteLabel);
            }

            var actions = new VisualElement();
            actions.AddToClassList("ab-welcome__actions");

            if (!installed)
            {
                actions.Add(Button("Install the plugin", MCPForUnityPluginInstaller.InstallLatest,
                    primary: true, enabled: !busy));
                // Closing the window already counts as "not now" (see OnDestroy); saying so out
                // loud is what stops a user hunting the footer toggle for a way out. The dismiss
                // is set here rather than left to OnDestroy because this one is unambiguous — a
                // button nobody but the user can press is worth honouring even mid-startup.
                actions.Add(Button("Not now", () =>
                {
                    MCPForUnityDependenciesInstaller.SetDismissed();
                    Close();
                }, enabled: !busy));
            }
            else
            {
                bool updatable = result.HasValue && result.Value.Status == MCPUpdateStatus.UpdateAvailable;
                if (updatable)
                {
                    string tag = result.Value.LatestTag;
                    actions.Add(Button("Update to v" + result.Value.LatestVersion,
                        () => MCPForUnityPluginInstaller.Install(tag), primary: true, enabled: !busy));
                    actions.Add(Button("Release notes",
                        () => Application.OpenURL(result.Value.LatestReleaseUrl)));
                }

                if (MCPForUnityPlugin.IsTrackingBranch)
                {
                    actions.Add(Button("Reinstall pinned to a release",
                        MCPForUnityPluginInstaller.InstallLatest, primary: !updatable, enabled: !busy));
                }

                actions.Add(Button("Check for updates",
                    () => MCPForUnityUpdateChecker.Check(force: true),
                    primary: !updatable && !MCPForUnityPlugin.IsTrackingBranch,
                    enabled: !MCPForUnityUpdateChecker.IsChecking && !busy));

                actions.Add(Button("Open MCP Dashboard",
                    () => MCPForUnityPlugin.OpenDashboard()));
                actions.Add(Button("Manage in Package Manager",
                    MCPForUnityPluginInstaller.OpenInPackageManager,
                    enabled: MCPForUnityPlugin.IsManagedByPackageManager));
            }

            card.Add(actions);
            host.Add(card);
        }

        /// <summary>
        /// Says how this copy was resolved, so the pinning promise is visible rather than assumed.
        /// An install started while the release lookup failed falls back to the default branch — that
        /// used to be a console warning nobody was looking at, and the card claimed nothing.
        /// </summary>
        private static string DescribeInstallShape()
        {
            if (MCPForUnityPlugin.IsTrackingBranch)
                return "This copy follows the repository's default branch, not a release tag - which is " +
                       "what an install falls back to when the release lookup fails. Reinstall to " +
                       "pin it to a published release.";

            var bridge = MCPForUnityBridge.Resolve();
            if (bridge.IsServing)
                return "Bridge serving on " + bridge.Url + " (the plugin assigns this port from " +
                       MCPForUnityBridge.PortRangeLabel + " and may pick a different one next session).";

            return null;
        }

        private void BuildIntro(VisualElement host)
        {
            Divider(host);
            Section(host, "What this asset does");
            Para(host,
                "The AnkleBreaker Unity MCP plugin opens a local bridge inside the Unity Editor so " +
                "MCP-compatible AI assistants - Claude, Cursor, Codex, Kimi - can read and drive your " +
                "project: scenes, GameObjects, components, assets, builds, profiling and more.");
            Para(host,
                "This asset is the front door to it. It installs the plugin, keeps it pinned to a " +
                "published release, tells you when a newer one ships, and ships the manual you are " +
                "one button away from.");
        }

        private void BuildSteps(VisualElement host)
        {
            Divider(host);

            var report = MCPForUnitySetupCheck.Last;
            bool installed = MCPForUnityPlugin.IsInstalled;
            var bridge = MCPForUnityBridge.Resolve();

            BuildSetupHeader(host, report);

            Step(host, 1, "Install the Unity plugin",
                "Adds " + MCPForUnityPlugin.PackageId + " through Package Manager, pinned to the latest " +
                "published release. Everything below depends on this one.",
                StatusFor(report, 1), tickable: false,
                actions: installed
                    ? null
                    : new[] { ("Install the plugin", (System.Action)MCPForUnityPluginInstaller.InstallLatest, true) });

            // Steps 2 and 3 happen outside Unity. The check reads the traces they leave — a Node
            // runtime on the PATH, an MCP entry in a client's config file — which is enough to
            // recognise a working setup and never enough to deny one, hence the manual button too.
            Step(host, 2, "Install the MCP server",
                "The Node.js half of the setup: the process your AI client launches. On the npx route " +
                "there is nothing to install by hand - it is fetched on first launch. What it needs is " +
                "Node.js " + MCPForUnityPlugin.MinimumNodeMajor + " or newer on this machine.",
                StatusFor(report, 2), tickable: true,
                extra: BuildServerMethods);

            Step(host, 3, "Point your AI client at it",
                "Claude Desktop, Claude Code, Cursor, Codex or Kimi. One entry naming the server, in " +
                "that client's config. The documentation carries the exact form for each, on Windows, " +
                "macOS and Linux.",
                StatusFor(report, 3), tickable: true,
                actions: new[]
                {
                    ("Read the client setup guide",
                        (System.Action)(() => MCPForUnityDocsWindow.OpenPage("client-setup")), false)
                },
                extra: BuildClientShortcut);

            // With no plugin there is no dashboard to open, so step 4 carries no button in that
            // state: it would answer "install the plugin from the Welcome window", which is this
            // window, while step 1 above already holds the only action that moves anything forward.
            var bridgeActions = !installed
                ? null
                : bridge.IsServing
                    ? new[]
                    {
                        ("Open the ping URL", (System.Action)(() => Application.OpenURL(bridge.PingUrl)), false),
                        ("Open MCP Dashboard", (System.Action)(() => MCPForUnityPlugin.OpenDashboard()), false)
                    }
                    : new[]
                    {
                        ("Open MCP Dashboard", (System.Action)(() => MCPForUnityPlugin.OpenDashboard()), true)
                    };

            Step(host, 4, "Check the bridge is live",
                "The plugin serves this editor on a port it assigns itself from " +
                MCPForUnityBridge.PortRangeLabel + ". This step is measured, not asserted: the address " +
                "below is asked for an answer every time this page checks itself.",
                StatusFor(report, 4), tickable: false, actions: bridgeActions);
        }

        /// <summary>
        /// The wizard's own header: how far along the setup is, when it was last measured, and the
        /// one button that measures it again.
        /// </summary>
        private void BuildSetupHeader(VisualElement host, MCPSetupReport? report)
        {
            var row = new VisualElement();
            row.AddToClassList("ab-welcome__setup-head");

            var title = new Label("Set up in four steps");
            title.AddToClassList("ab-welcome__section-title");
            title.AddToClassList("ab-welcome__setup-head-title");
            row.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("ab-welcome__footer-spacer");
            row.Add(spacer);

            int done = 0;
            for (int step = 1; step <= 4; step++)
                if (IsStepComplete(report, step)) done++;

            var progress = new Label(done + " of 4 done");
            progress.AddToClassList("ab-welcome__chip");
            progress.AddToClassList(done == 4 ? "ab-welcome__chip--ok" : "ab-welcome__chip--neutral");
            row.Add(progress);

            _recheckButton = Button("Re-check now", () =>
            {
                MCPForUnitySetupCheck.Check(force: true);
                UpdateCheckStatus();
            });
            row.Add(_recheckButton);
            host.Add(row);

            _checkStatus = new Label();
            _checkStatus.AddToClassList("ab-welcome__setup-status");
            host.Add(_checkStatus);
            UpdateCheckStatus();

            Hint(host,
                "This page verifies each step by itself - it looks for the plugin, for Node.js, for an " +
                "MCP entry in the config files of the clients it knows, and it asks the bridge to " +
                "answer. What it cannot see, you can mark done yourself: a client it has never heard " +
                "of is still a client that works.");
        }

        /// <summary>Keeps the live line honest without redrawing the page around it.</summary>
        private void UpdateCheckStatus()
        {
            if (_recheckButton != null) _recheckButton.SetEnabled(!MCPForUnitySetupCheck.IsRunning);
            if (_checkStatus == null) return;

            if (MCPForUnitySetupCheck.IsRunning)
            {
                _checkStatus.text = "Checking this setup...";
                return;
            }

            string age = MCPForUnitySetupCheck.DescribeAge();
            _checkStatus.text = age == null
                ? "Not checked yet."
                : MCPForUnityPrefs.AutoVerifySetup
                    ? "Checked " + age + "."
                    : "Checked " + age + ". Automatic re-checking is off.";
        }

        /// <summary>
        /// Step 2's two routes, as a choice rather than as one wall of configuration.
        ///
        /// <para>The simple one is the honest description of what most people actually do now: they
        /// ask the assistant to install its own MCP server. It is one paste, it needs no file paths,
        /// and it covers step 3 as a side effect - so it is the default.</para>
        /// </summary>
        private void BuildServerMethods(VisualElement host)
        {
            bool simple = MCPForUnityPrefs.UseSimpleServerMethod;

            var switcher = new VisualElement();
            switcher.AddToClassList("ab-welcome__methods");
            switcher.Add(Pill("Simple - ask your assistant", simple, () =>
            {
                MCPForUnityPrefs.UseSimpleServerMethod = true;
                Rebuild();
            }));
            switcher.Add(Pill("Manual - do it yourself", !simple, () =>
            {
                MCPForUnityPrefs.UseSimpleServerMethod = false;
                Rebuild();
            }));
            host.Add(switcher);

            if (simple)
            {
                StepPara(host,
                    "Paste this to Claude, Cursor, Codex or any assistant that speaks MCP. It installs " +
                    "the server and writes its own configuration entry:");
                CodeBlock(host, MCPForUnityPlugin.AssistantInstallRequest);
                Hint(host,
                    "That single request covers step 3 as well - the assistant edits the config it " +
                    "reads itself. It still needs Node.js " + MCPForUnityPlugin.MinimumNodeMajor +
                    "+ on this machine, which is what the line above checks.");
                StepActions(host,
                    ("Get Node.js", () => Application.OpenURL(MCPForUnityPlugin.NodeDownloadUrl), false),
                    ("Read the server setup",
                        () => MCPForUnityDocsWindow.OpenPage("client-setup"), false));
                return;
            }

            StepPara(host,
                "Let npm fetch the server when your client launches it. Nothing to clone, nothing to " +
                "keep updated - this is the command your client's config will carry:");
            CodeBlock(host, MCPForUnityPlugin.ServerNpxCommand);
            StepPara(host, "Or clone it, if you want to read or modify the server:");
            CodeBlock(host,
                "git clone " + MCPForUnityPlugin.ServerRepositoryUrl + ".git\n" +
                "cd unity-mcp-server\n" +
                "npm install");
            Hint(host,
                "You do not run either command day to day - your AI client launches the server itself. " +
                "Step 3 is where you tell it how.");
            StepActions(host,
                ("Get Node.js", () => Application.OpenURL(MCPForUnityPlugin.NodeDownloadUrl), false),
                ("Read the server setup", () => MCPForUnityDocsWindow.OpenPage("client-setup"), false));
        }

        /// <summary>The two clients that take a command instead of a hand-edited file, since that
        /// turns step 3 into one paste for anyone using them.</summary>
        private void BuildClientShortcut(VisualElement host)
        {
            StepPara(host,
                "Claude Code and Codex take a command rather than a file to edit:");
            CodeBlock(host,
                "claude mcp add unity -- " + MCPForUnityPlugin.ServerNpxCommand + "\n" +
                "codex mcp add unity -- " + MCPForUnityPlugin.ServerNpxCommand);
        }

        private void BuildResources(VisualElement host)
        {
            Divider(host);
            Section(host, "Resources");

            var row = new VisualElement();
            row.AddToClassList("ab-welcome__actions");
            row.Add(Button("Documentation", () => MCPForUnityDocsWindow.OpenPage(null), primary: true));
            row.Add(Button("Plugin on GitHub", () => Application.OpenURL(MCPForUnityPlugin.RepositoryUrl)));
            row.Add(Button("Releases", () => Application.OpenURL(MCPForUnityPlugin.ReleasesUrl)));
            row.Add(Button("Report an issue", () => Application.OpenURL(MCPForUnityPlugin.IssuesUrl)));
            row.Add(Button("Discord", () => Application.OpenURL(MCPForUnityPlugin.DiscordUrl)));
            row.Add(Button("Sponsor", () => Application.OpenURL(MCPForUnityPlugin.SponsorsUrl)));
            host.Add(row);
        }

        private void BuildFooter()
        {
            if (_footer == null) return;
            _footer.Clear();

            var autoOpen = new Toggle("Open this window when the plugin is missing")
            {
                value = MCPForUnityPrefs.AutoOpenWhenMissing
            };
            autoOpen.AddToClassList("ab-welcome__footer-toggle");
            autoOpen.RegisterValueChangedCallback(e => MCPForUnityPrefs.AutoOpenWhenMissing = e.newValue);
            _footer.Add(autoOpen);

            var notify = new Toggle("Tell me when a plugin update ships")
            {
                value = MCPForUnityPrefs.NotifyUpdates
            };
            notify.AddToClassList("ab-welcome__footer-toggle");
            notify.RegisterValueChangedCallback(e => MCPForUnityPrefs.NotifyUpdates = e.newValue);
            _footer.Add(notify);

            // Opt-out rather than opt-in, but present: the check reads config files and runs
            // "node --version", and someone is entitled to decide when that happens.
            var autoVerify = new Toggle("Verify the setup automatically")
            {
                value = MCPForUnityPrefs.AutoVerifySetup
            };
            autoVerify.AddToClassList("ab-welcome__footer-toggle");
            autoVerify.tooltip = "Re-check the four setup steps while this window is open. Off leaves " +
                                 "every answer where the last Re-check put it.";
            autoVerify.RegisterValueChangedCallback(e =>
            {
                MCPForUnityPrefs.AutoVerifySetup = e.newValue;
                if (e.newValue) MCPForUnitySetupCheck.Check(force: true);
                UpdateCheckStatus();
            });
            _footer.Add(autoVerify);

            var spacer = new VisualElement();
            spacer.AddToClassList("ab-welcome__footer-spacer");
            _footer.Add(spacer);

            var note = new Label("Settings are per-machine.");
            note.AddToClassList("ab-welcome__footer-note");
            _footer.Add(note);
        }

        // -- Small builders --------------------------------------------------

        private static void Section(VisualElement host, string text)
        {
            var label = new Label(text);
            label.AddToClassList("ab-welcome__section-title");
            host.Add(label);
        }

        private static void Para(VisualElement host, string text)
        {
            var label = new Label(text) { enableRichText = true };
            label.AddToClassList("ab-welcome__para");
            host.Add(label);
        }

        private static void Divider(VisualElement host)
        {
            var divider = new VisualElement();
            divider.AddToClassList("ab-welcome__divider");
            host.Add(divider);
        }

        private static Button Button(string text, System.Action onClick,
            bool primary = false, bool enabled = true)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ab-welcome__btn");
            if (primary) button.AddToClassList("ab-welcome__btn--primary");
            button.SetEnabled(enabled);
            return button;
        }

        private static void StepPara(VisualElement host, string text)
        {
            var label = new Label(text) { enableRichText = true };
            label.AddToClassList("ab-welcome__step-para");
            host.Add(label);
        }

        /// <summary>Small print: the caveat, the side effect, the thing that saves a support ticket.</summary>
        private static void Hint(VisualElement host, string text)
        {
            var label = new Label(text) { enableRichText = true };
            label.AddToClassList("ab-welcome__hint");
            host.Add(label);
        }

        private static void StepActions(VisualElement host,
            params (string label, System.Action action, bool primary)[] actions)
        {
            var row = new VisualElement();
            row.AddToClassList("ab-welcome__actions");
            foreach (var (label, action, primary) in actions) row.Add(Button(label, action, primary));
            host.Add(row);
        }

        /// <summary>One of a pair of mutually exclusive routes. A Button rather than a Toggle so it
        /// takes keyboard focus and shows a pointer, like the documentation window's tabs.</summary>
        private static Button Pill(string text, bool active, System.Action onClick)
        {
            var pill = new Button(onClick) { text = text };
            pill.AddToClassList("ab-welcome__pill");
            if (active) pill.AddToClassList("ab-welcome__pill--active");
            return pill;
        }

        /// <summary>Monospace block with a Copy button, same contract as the manual's code blocks —
        /// a command nobody can copy is a command they will mistype.</summary>
        private static void CodeBlock(VisualElement host, string code)
        {
            var box = new VisualElement();
            box.AddToClassList("ab-welcome__code");

            var text = new Label(code) { enableRichText = false };
#if UNITY_2023_1_OR_NEWER
            text.selection.isSelectable = true; // nice-to-have; API absent on 2022.3
#endif
            text.AddToClassList("ab-welcome__code-text");
            box.Add(text);

            var copy = new Button { text = "Copy" };
            copy.AddToClassList("ab-welcome__code-copy");
            copy.tooltip = "Copy this to the clipboard.";
            // A copy that says nothing leaves the user pressing it twice to be sure it took.
            copy.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = code;
                copy.text = "Copied";
                copy.AddToClassList("ab-welcome__code-copy--done");
                copy.schedule.Execute(() =>
                {
                    copy.text = "Copy";
                    copy.RemoveFromClassList("ab-welcome__code-copy--done");
                }).StartingIn(1200);
            };
            box.Add(copy);
            host.Add(box);
        }

        // -- Steps -----------------------------------------------------------

        /// <summary>What the last pass found for a step, or a neutral placeholder until one lands.</summary>
        private static MCPStepStatus StatusFor(MCPSetupReport? report, int index)
            => report?.Step(index) ?? new MCPStepStatus(MCPCheckState.Pending, null, null);

        /// <summary>
        /// Verified by the check, or claimed by the user — with one exception that matters.
        ///
        /// <para>A manual mark answers <see cref="MCPCheckState.NotDetected"/>, which only ever
        /// means "this package did not find it where it knows to look". It does <b>not</b> answer
        /// <see cref="MCPCheckState.Failed"/>, where something was found and it is wrong: Node.js 16
        /// stays Node.js 16 however firmly the reader ticks the box, and a step that went green over
        /// it would hide the one line on the page that explains why nothing works.</para>
        /// </summary>
        private static bool IsStepComplete(MCPSetupReport? report, int index)
        {
            if (report.HasValue)
            {
                var status = report.Value.Step(index);
                if (status.IsPassed) return true;
                if (status.State == MCPCheckState.Failed) return false;
            }
            return MCPForUnityPrefs.IsStepDone(index);
        }

        private static string ChipText(MCPStepStatus status, bool marked, bool tickable)
        {
            switch (status.State)
            {
                case MCPCheckState.Passed: return "VERIFIED";
                case MCPCheckState.Failed: return "ACTION NEEDED";
                case MCPCheckState.Blocked: return marked ? "MARKED DONE" : "WAITING";
                case MCPCheckState.Pending: return marked ? "MARKED DONE" : "CHECKING";
                default: return marked ? "MARKED DONE" : tickable ? "NOT DETECTED" : "TO DO";
            }
        }

        /// <summary>
        /// Which palette tone a step wears. "Not detected" is deliberately a warning and never an
        /// error: this package looks in the places it knows about, and a setup it cannot see is far
        /// more often a client it has not heard of than a setup that is broken. A measured failure
        /// is the opposite case, and no manual mark tones it down.
        /// </summary>
        private static string ChipTone(MCPStepStatus status, bool marked)
        {
            switch (status.State)
            {
                case MCPCheckState.Failed: return "error";
                case MCPCheckState.Passed: return "ok";
                case MCPCheckState.Blocked:
                case MCPCheckState.Pending: return marked ? "ok" : "neutral";
                default: return marked ? "ok" : "warn";
            }
        }

        /// <summary>
        /// One wizard step: what it is, what the last verification pass found, what to press — and,
        /// for the steps the editor can only look for rather than observe, a button to say it is
        /// done regardless.
        /// </summary>
        private void Step(VisualElement host, int index, string title, string body,
            MCPStepStatus status, bool tickable,
            (string label, System.Action action, bool primary)[] actions = null,
            System.Action<VisualElement> extra = null)
        {
            bool verified = status.State == MCPCheckState.Passed;
            bool failed = status.State == MCPCheckState.Failed;
            bool marked = tickable && MCPForUnityPrefs.IsStepDone(index);
            // See IsStepComplete: a tick overrules "not found", never "found and wrong".
            bool done = verified || (marked && !failed);

            var step = new VisualElement();
            step.AddToClassList("ab-welcome__step");
            if (done) step.AddToClassList("ab-welcome__step--done");
            else if (status.State == MCPCheckState.Failed) step.AddToClassList("ab-welcome__step--attention");

            var number = new Label(done ? "OK" : index.ToString());
            number.AddToClassList("ab-welcome__step-index");
            if (done) number.AddToClassList("ab-welcome__step-index--done");
            step.Add(number);

            var text = new VisualElement();
            text.AddToClassList("ab-welcome__step-text");

            var head = new VisualElement();
            head.AddToClassList("ab-welcome__step-head");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("ab-welcome__step-title");
            head.Add(titleLabel);

            var spacer = new VisualElement();
            spacer.AddToClassList("ab-welcome__footer-spacer");
            head.Add(spacer);

            string tone = ChipTone(status, marked);
            var chip = new Label(ChipText(status, marked, tickable));
            chip.AddToClassList("ab-welcome__chip");
            chip.AddToClassList("ab-welcome__chip--" + tone);
            head.Add(chip);
            text.Add(head);

            var bodyLabel = new Label(body) { enableRichText = true };
            bodyLabel.AddToClassList("ab-welcome__step-body");
            text.Add(bodyLabel);

            // What the machine found, in its own line, so it is never confused with the static
            // explanation above it.
            string detail = status.Detail;
            if (marked && failed)
                detail = "You marked this step done, but the check found a problem with it: " + detail;
            else if (marked && !verified)
                detail = "You marked this step done." + (string.IsNullOrEmpty(detail) ? "" : " " + detail);

            if (!string.IsNullOrEmpty(detail))
            {
                var check = new Label(detail);
                check.AddToClassList("ab-welcome__step-check");
                check.AddToClassList("ab-welcome__step-check--" + tone);
                text.Add(check);
            }

            extra?.Invoke(text);

            if (actions != null && actions.Length > 0) StepActions(text, actions);

            // No manual button once the check proves the step: an overrule that cannot change the
            // answer is a control that does nothing.
            if (tickable && !verified)
            {
                bool wasMarked = marked;
                var mark = new Button(() =>
                {
                    MCPForUnityPrefs.SetStepDone(index, !wasMarked);
                    Rebuild();
                })
                {
                    text = wasMarked ? "Marked as done - click to undo" : "Mark this step as done"
                };
                mark.AddToClassList("ab-welcome__done-btn");
                // The green "settled" look would contradict the red card it sits in.
                if (wasMarked && !failed) mark.AddToClassList("ab-welcome__done-btn--marked");
                mark.tooltip = "Remembered for this project, on this machine. The automatic check " +
                               "keeps running either way.";
                text.Add(mark);
            }

            step.Add(text);
            host.Add(step);
        }
    }
}
