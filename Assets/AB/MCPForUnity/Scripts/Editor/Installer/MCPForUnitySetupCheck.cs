using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>How a setup step answered the last time it was probed.</summary>
    public enum MCPCheckState
    {
        /// <summary>No pass has produced an answer for this step yet. Never returned by a check —
        /// it is what a caller shows in the fraction of a second before the first one lands.</summary>
        Pending,

        /// <summary>An earlier step has to happen before this one can be looked at.</summary>
        Blocked,

        /// <summary>The probe ran and found nothing. <b>Not</b> a failure: this package can only
        /// look where it knows to look, so "not detected" never means "not done".</summary>
        NotDetected,

        /// <summary>The probe found something that is actually wrong, and says what.</summary>
        Failed,

        /// <summary>Proven, on this machine, just now.</summary>
        Passed
    }

    /// <summary>One step's answer: a chip's worth of text, and a sentence explaining it.</summary>
    public readonly struct MCPStepStatus
    {
        public readonly MCPCheckState State;

        /// <summary>Four or five words, for the status chip.</summary>
        public readonly string Headline;

        /// <summary>One sentence: what was found, where, and what to do about it.</summary>
        public readonly string Detail;

        public MCPStepStatus(MCPCheckState state, string headline, string detail)
        {
            State = state;
            Headline = headline;
            Detail = detail;
        }

        public bool IsPassed => State == MCPCheckState.Passed;
    }

    /// <summary>One complete pass over the four setup steps.</summary>
    public readonly struct MCPSetupReport
    {
        public readonly MCPStepStatus Plugin;
        public readonly MCPStepStatus Server;
        public readonly MCPStepStatus Client;
        public readonly MCPStepStatus Bridge;
        public readonly DateTime CheckedUtc;

        public MCPSetupReport(MCPStepStatus plugin, MCPStepStatus server, MCPStepStatus client,
            MCPStepStatus bridge, DateTime checkedUtc)
        {
            Plugin = plugin;
            Server = server;
            Client = client;
            Bridge = bridge;
            CheckedUtc = checkedUtc;
        }

        /// <summary>The status for a 1-based step number, as the Welcome window numbers them.</summary>
        public MCPStepStatus Step(int index)
        {
            switch (index)
            {
                case 1: return Plugin;
                case 2: return Server;
                case 3: return Client;
                case 4: return Bridge;
                default: return new MCPStepStatus(MCPCheckState.NotDetected, null, null);
            }
        }
    }

    /// <summary>
    /// Answers "is this setup actually working?" by measuring it, rather than by asking the user.
    ///
    /// <para><b>Why.</b> Three of the four setup steps leave evidence on the machine — a package in
    /// the project, a Node.js runtime on the PATH, an MCP entry in a client's config file, a bridge
    /// answering on a port. A wizard that makes someone tick those off by hand is asking them to
    /// confirm something they came here to find out.</para>
    ///
    /// <para><b>The honesty rule this type is built around:</b> a probe that finds nothing reports
    /// <see cref="MCPCheckState.NotDetected"/>, never a failure. This package scans the config files
    /// of the clients it documents; someone using a client it has never heard of is correctly set up
    /// and invisible to it. That is why every step the editor cannot fully observe keeps a manual
    /// "mark as done" button next to the automatic answer — the check informs the user, it does not
    /// overrule them.</para>
    ///
    /// <para>Everything expensive — launching <c>node --version</c>, reading config files, pinging
    /// the bridge — happens on a background thread; only the values Unity insists on owning are read
    /// on the main thread and handed over. Results are delivered back on the main thread through
    /// <see cref="Completed"/>.</para>
    ///
    /// <para>Nothing here references the plugin's assembly, like the rest of this assembly.</para>
    /// </summary>
    public static class MCPForUnitySetupCheck
    {
        /// <summary>A poll-driven <see cref="Check"/> re-runs at most this often.</summary>
        private const double AutoRecheckSeconds = 10.0;

        /// <summary>Node moves far less often than a bridge port, and probing it costs a process
        /// launch — so that one answer is memoised across passes unless a check is forced.</summary>
        private const double NodeProbeCacheSeconds = 60.0;

        private const int NodeTimeoutMs = 4000;
        private const int PingTimeoutMs = 2000;

        /// <summary>A pass still running after this is treated as lost, not as in progress.</summary>
        private const double StuckAfterSeconds = 30.0;

        /// <summary>Strings that identify this ecosystem's server inside a client config file.
        /// The npm id covers the npx route, the repository name covers a clone.</summary>
        private static readonly string[] ServerNeedles =
        {
            MCPForUnityPlugin.ServerNpmPackage,
            "unity-mcp-server"
        };

        private static readonly object Gate = new object();
        private static MCPSetupReport? _pending;

        private static bool _running;
        private static double _lastStartedAt = double.NegativeInfinity;

        // Written by the worker, reset from the main thread on a forced check. A torn read costs
        // one extra process launch, which is why it needs no lock.
        private static MCPStepStatus _nodeMemo;
        private static DateTime _nodeMemoAtUtc = DateTime.MinValue;

        /// <summary>A pass is in flight right now.</summary>
        public static bool IsRunning => _running;

        /// <summary>The last completed pass, or null when none has finished this session.</summary>
        public static MCPSetupReport? Last { get; private set; }

        /// <summary>Raised on the main thread each time a pass completes.</summary>
        public static event Action<MCPSetupReport> Completed;

        /// <summary>
        /// Runs a verification pass.
        /// </summary>
        /// <param name="force">Ignore both the pacing gate and the memoised Node answer. Use for an
        /// explicit "check now" click, and after anything that could have changed the setup.</param>
        public static void Check(bool force = false)
        {
            // A window polling itself is not a reason to spawn a process; a batch import never is.
            if (Application.isBatchMode) return;
            // A pass that never delivered would otherwise wedge this permanently at "checking...",
            // and every probe in it has its own timeout well under a minute.
            if (_running && EditorApplication.timeSinceStartup - _lastStartedAt < StuckAfterSeconds) return;
            if (!force && EditorApplication.timeSinceStartup - _lastStartedAt < AutoRecheckSeconds) return;

            _lastStartedAt = EditorApplication.timeSinceStartup;
            if (force) _nodeMemoAtUtc = DateTime.MinValue;

            var inputs = CaptureInputs();
            _running = true;
            EditorApplication.update += Deliver;

            var thread = new Thread(() =>
            {
                MCPSetupReport report;
                try
                {
                    report = RunPass(inputs);
                }
                catch (Exception e)
                {
                    // A probe throwing must not leave the window saying "checking..." forever.
                    var failed = new MCPStepStatus(MCPCheckState.NotDetected, "Check failed", e.Message);
                    report = new MCPSetupReport(CheckPlugin(inputs), failed, failed, failed, DateTime.UtcNow);
                }

                lock (Gate) _pending = report;
            })
            {
                IsBackground = true,
                Name = "MCPForUnitySetupCheck"
            };
            thread.Start();
        }

        /// <summary>Human-readable age of the last pass, for a status line.</summary>
        public static string DescribeAge()
        {
            if (!Last.HasValue) return null;
            double seconds = (DateTime.UtcNow - Last.Value.CheckedUtc).TotalSeconds;
            if (seconds < 0.0) return "just now";
            if (seconds < 15.0) return "just now";
            if (seconds < 90.0) return Math.Round(seconds) + "s ago";
            return Math.Round(seconds / 60.0) + " min ago";
        }

        private static void Deliver()
        {
            MCPSetupReport? report;
            lock (Gate)
            {
                report = _pending;
                _pending = null;
            }
            if (!report.HasValue) return;

            EditorApplication.update -= Deliver;
            _running = false;
            Last = report.Value;

            try
            {
                Completed?.Invoke(report.Value);
            }
            catch (Exception e)
            {
                // One window throwing as it rebuilds must not swallow the result for the others.
                Debug.LogException(e);
            }
        }

        // ── Main-thread inputs ─────────────────────────────────────────────

        /// <summary>Everything the worker needs that only the main thread may read.</summary>
        private struct Inputs
        {
            public bool IsWindows;
            public bool PluginInstalled;
            public bool PluginManaged;
            public string PluginVersion;
            public bool BridgeServing;
            public int BridgePort;
            public string BridgeUrl;
            public string BridgePingUrl;
            public string BridgeNote;
            public string ProjectRoot;
        }

        private static Inputs CaptureInputs()
        {
            MCPForUnityPlugin.Refresh();
            // The port can move at any time and the window may have been showing a cached answer
            // for two seconds; a verification pass is exactly the moment not to reuse it.
            MCPForUnityBridge.Invalidate();
            var bridge = MCPForUnityBridge.Resolve();

            string dataPath = Application.dataPath;
            const string assets = "/Assets";
            string root = dataPath.EndsWith(assets, StringComparison.Ordinal)
                ? dataPath.Substring(0, dataPath.Length - assets.Length)
                : dataPath;

            return new Inputs
            {
                IsWindows = Application.platform == RuntimePlatform.WindowsEditor,
                PluginInstalled = MCPForUnityPlugin.IsInstalled,
                PluginManaged = MCPForUnityPlugin.IsManagedByPackageManager,
                PluginVersion = MCPForUnityPlugin.InstalledVersion,
                BridgeServing = bridge.IsServing,
                BridgePort = bridge.Port,
                BridgeUrl = bridge.Url,
                BridgePingUrl = bridge.PingUrl,
                BridgeNote = bridge.Note,
                ProjectRoot = root
            };
        }

        // ── The pass ───────────────────────────────────────────────────────

        private static MCPSetupReport RunPass(Inputs inputs)
        {
            return new MCPSetupReport(
                CheckPlugin(inputs),
                CheckServer(inputs),
                CheckClient(inputs),
                CheckBridge(inputs),
                DateTime.UtcNow);
        }

        private static MCPStepStatus CheckPlugin(Inputs inputs)
        {
            if (!inputs.PluginInstalled)
                return new MCPStepStatus(MCPCheckState.NotDetected, "Not installed",
                    "The plugin's editor assembly is not loaded in this project. The Install button " +
                    "above adds it through Package Manager, pinned to a published release.");

            string version = string.IsNullOrEmpty(inputs.PluginVersion)
                ? "installed"
                : "v" + inputs.PluginVersion;

            return new MCPStepStatus(MCPCheckState.Passed, "Installed " + version,
                inputs.PluginManaged
                    ? "The bridge assembly is loaded and Package Manager owns this copy, so updates " +
                      "are one click away."
                    : "The bridge assembly is loaded. This copy is not managed by Package Manager, " +
                      "so its version cannot be read and it cannot be updated from here.");
        }

        /// <summary>
        /// Step 2 is "install the MCP server", and on the documented route there is nothing to
        /// install: <c>npx</c> fetches it when the AI client launches it. So what is actually
        /// checkable — and what actually breaks — is the Node.js runtime underneath.
        /// </summary>
        private static MCPStepStatus CheckServer(Inputs inputs)
        {
            if ((DateTime.UtcNow - _nodeMemoAtUtc).TotalSeconds < NodeProbeCacheSeconds)
                return _nodeMemo;

            var status = ProbeNode(inputs.IsWindows);
            _nodeMemo = status;
            _nodeMemoAtUtc = DateTime.UtcNow;
            return status;
        }

        private static MCPStepStatus ProbeNode(bool isWindows)
        {
            string nodePath = FindOnPath("node", isWindows);
            if (string.IsNullOrEmpty(nodePath))
                return new MCPStepStatus(MCPCheckState.NotDetected, "Node.js not on the PATH",
                    "No node executable was found on the PATH this editor was launched with. Unity " +
                    "reads the PATH once, at startup, so restart Unity if you installed Node since. " +
                    "If your AI client runs its own Node, this step can still be done - mark it below.");

            if (!TryReadNodeVersion(nodePath, out string version, out string error))
                return new MCPStepStatus(MCPCheckState.NotDetected, "Node.js did not answer",
                    "Found " + nodePath + " but could not read its version: " + error);

            int major = MajorVersion(version);
            if (major > 0 && major < MCPForUnityPlugin.MinimumNodeMajor)
                return new MCPStepStatus(MCPCheckState.Failed, "Node.js " + version + " is too old",
                    "The MCP server needs Node.js " + MCPForUnityPlugin.MinimumNodeMajor +
                    " or newer. Found " + version + " at " + nodePath + ".");

            string npxPath = FindOnPath("npx", isWindows);
            if (string.IsNullOrEmpty(npxPath))
                return new MCPStepStatus(MCPCheckState.Failed, "npx is missing",
                    "Node.js " + version + " is at " + nodePath + ", but no npx alongside it - and " +
                    "npx is what the documented server command runs. Reinstall Node.js, or clone the " +
                    "server and point your client at it with node instead.");

            return new MCPStepStatus(MCPCheckState.Passed, "Node.js " + version,
                "Found at " + nodePath + ", with npx beside it - so \"" +
                MCPForUnityPlugin.ServerNpxCommand + "\" can fetch and launch the server on demand. " +
                "Nothing to install by hand.");
        }

        /// <summary>
        /// Step 3 leaves a trace: an MCP entry naming this ecosystem's server in a client's config
        /// file. Reading those files is the only way the editor can know, and it is a read of files
        /// the user owns — nothing is written and no content is reported, only which file matched.
        /// </summary>
        private static MCPStepStatus CheckClient(Inputs inputs)
        {
            var candidates = ClientConfigs(inputs);
            var found = new List<string>();
            int readable = 0;

            foreach (var candidate in candidates)
            {
                if (!SafeExists(candidate.Path)) continue;
                readable++;
                // Two paths can carry one client's name (macOS looks in two places for Claude
                // Desktop); "found in Claude Desktop and Claude Desktop" would read like a bug.
                if (FileContainsAny(candidate.Path, ServerNeedles) && !found.Contains(candidate.Name))
                    found.Add(candidate.Name);
            }

            if (found.Count > 0)
                return new MCPStepStatus(MCPCheckState.Passed, "Configured in " + Join(found),
                    "An MCP entry pointing at the Unity server was found in " + Join(found) +
                    ". Restart the client after any config edit - most of them read the file only " +
                    "at launch.");

            if (readable == 0)
                return new MCPStepStatus(MCPCheckState.NotDetected, "No client config found",
                    "None of the config files this package knows about exist yet on this machine. " +
                    "Configure a client, or mark this step done if yours keeps its settings elsewhere.");

            return new MCPStepStatus(MCPCheckState.NotDetected, "Not found in " + readable +
                                                                (readable == 1 ? " config file" : " config files"),
                "Read the config files of Claude Desktop, Claude Code, Cursor, Codex, Kimi and VS Code and " +
                "found no entry naming the Unity MCP server. If you use another client, or keep its " +
                "config somewhere else, that is expected - mark this step done yourself.");
        }

        private static MCPStepStatus CheckBridge(Inputs inputs)
        {
            if (!inputs.PluginInstalled)
                return new MCPStepStatus(MCPCheckState.Blocked, "Waiting for step 1",
                    "There is no bridge until the plugin is installed.");

            if (!inputs.BridgeServing)
                return new MCPStepStatus(MCPCheckState.NotDetected, "No bridge is serving",
                    inputs.BridgeNote ??
                    "No live bridge is registered for this project. Open the MCP Dashboard and start " +
                    "it, or enable auto-start.");

            if (!TryPing(inputs.BridgePingUrl, out long elapsedMs, out string error))
                return new MCPStepStatus(MCPCheckState.Failed,
                    "Port " + inputs.BridgePort + " did not answer",
                    "The shared instance registry lists port " + inputs.BridgePort + " for this " +
                    "project, but a request to " + inputs.BridgePingUrl + " failed: " + error +
                    ". The editor that claimed the port may have stopped serving.");

            return new MCPStepStatus(MCPCheckState.Passed, "Answering on port " + inputs.BridgePort,
                inputs.BridgePingUrl + " replied in " + elapsedMs + " ms. The plugin assigns this " +
                "port from " + MCPForUnityBridge.PortRangeLabel + " and may pick a different one " +
                "next session, so read it here rather than hardcoding it.");
        }

        // ── Probes ─────────────────────────────────────────────────────────

        private struct ClientConfig
        {
            public string Name;
            public string Path;

            public ClientConfig(string name, string path)
            {
                Name = name;
                Path = path;
            }
        }

        /// <summary>
        /// Every config file this package documents, per platform. A client missing from this list
        /// is a client whose users get "not detected" and mark the step by hand — which is why that
        /// answer is worded as a gap in the check and not as a gap in their setup.
        /// </summary>
        private static List<ClientConfig> ClientConfigs(Inputs inputs)
        {
            var list = new List<ClientConfig>();
            string home = SafeFolder(Environment.SpecialFolder.UserProfile);
            string project = inputs.ProjectRoot;

            if (inputs.IsWindows)
            {
                string appData = SafeFolder(Environment.SpecialFolder.ApplicationData);
                Add(list, "Claude Desktop", Combine(appData, "Claude", "claude_desktop_config.json"));
            }
            else
            {
                Add(list, "Claude Desktop",
                    Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json"));
                Add(list, "Claude Desktop", Combine(home, ".config", "Claude", "claude_desktop_config.json"));
            }

            Add(list, "Claude Code", Combine(home, ".claude.json"));
            Add(list, "Claude Code (this project)", Combine(project, ".mcp.json"));
            Add(list, "Cursor", Combine(home, ".cursor", "mcp.json"));
            Add(list, "Cursor (this project)", Combine(project, ".cursor", "mcp.json"));
            Add(list, "Codex", Combine(home, ".codex", "config.toml"));
            Add(list, "Codex (this project)", Combine(project, ".codex", "config.toml"));
            Add(list, "Kimi", Combine(home, ".kimi", "mcp.json"));
            Add(list, "VS Code (this project)", Combine(project, ".vscode", "mcp.json"));
            return list;
        }

        private static void Add(List<ClientConfig> list, string name, string path)
        {
            if (!string.IsNullOrEmpty(path)) list.Add(new ClientConfig(name, path));
        }

        private static bool TryReadNodeVersion(string nodePath, out string version, out string error)
        {
            version = null;
            error = null;
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo(nodePath, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(info))
                {
                    if (process == null)
                    {
                        error = "the process could not be started";
                        return false;
                    }

                    // Read before waiting: "v20.11.1" never fills a pipe buffer, and reading first
                    // is the shape that cannot deadlock if a future Node ever prints more.
                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(NodeTimeoutMs))
                    {
                        try { process.Kill(); } catch (Exception) { /* already gone */ }
                        error = "it did not answer within " + (NodeTimeoutMs / 1000) + " seconds";
                        return false;
                    }

                    output = (output ?? string.Empty).Trim();
                    if (output.Length == 0)
                    {
                        error = "it printed no version";
                        return false;
                    }

                    version = output;
                    return true;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static int MajorVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return 0;
            string trimmed = version.Trim();
            if (trimmed.Length > 1 && (trimmed[0] == 'v' || trimmed[0] == 'V')) trimmed = trimmed.Substring(1);

            int end = 0;
            while (end < trimmed.Length && char.IsDigit(trimmed[end])) end++;
            return end > 0 && int.TryParse(trimmed.Substring(0, end), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int major)
                ? major
                : 0;
        }

        private static bool TryPing(string url, out long elapsedMs, out string error)
        {
            elapsedMs = 0;
            error = null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = PingTimeoutMs;
                request.ReadWriteTimeout = PingTimeoutMs;
                // A machine-wide proxy must not stand between this editor and its own loopback
                // port: it turns a one-millisecond answer into a timeout, or a refusal.
                request.Proxy = null;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    elapsedMs = watch.ElapsedMilliseconds;
                    if (response.StatusCode == HttpStatusCode.OK) return true;

                    error = "HTTP " + (int)response.StatusCode;
                    return false;
                }
            }
            catch (WebException e)
            {
                error = e.Status == WebExceptionStatus.Timeout
                    ? "it did not answer within " + PingTimeoutMs + " ms"
                    : e.Message;
                return false;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        // ── File and path helpers ──────────────────────────────────────────

        /// <summary>
        /// First match for an executable name on the PATH, or null.
        ///
        /// <para>Resolving it here rather than letting the shell do it is what keeps the probe
        /// honest: it reports the path it found, and it never has to launch a shell to find out
        /// whether a command exists.</para>
        /// </summary>
        private static string FindOnPath(string exeName, bool isWindows)
        {
            string path;
            try
            {
                path = Environment.GetEnvironmentVariable("PATH");
            }
            catch (Exception)
            {
                return null;
            }
            if (string.IsNullOrEmpty(path)) return null;

            string[] extensions = isWindows
                ? new[] { ".exe", ".cmd", ".bat", string.Empty }
                : new[] { string.Empty };

            foreach (var raw in path.Split(Path.PathSeparator))
            {
                string dir = raw.Trim().Trim('"');
                if (dir.Length == 0) continue;

                foreach (var extension in extensions)
                {
                    string candidate;
                    try
                    {
                        candidate = Path.Combine(dir, exeName + extension);
                    }
                    catch (ArgumentException)
                    {
                        break; // a PATH entry with invalid characters; skip the whole entry
                    }
                    if (SafeExists(candidate)) return candidate;
                }
            }
            return null;
        }

        private static bool SafeExists(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when the file contains any of <paramref name="needles"/>.
        ///
        /// <para>Read in overlapping chunks rather than in one string: Claude Code keeps its MCP
        /// entries in <c>~/.claude.json</c> alongside conversation state, and that file reaches
        /// tens of megabytes on a machine in daily use. The overlap is what stops a needle
        /// straddling two chunks from being missed.</para>
        /// </summary>
        private static bool FileContainsAny(string path, string[] needles)
        {
            int longest = 0;
            foreach (var needle in needles) longest = Math.Max(longest, needle.Length);

            const int chunk = 64 * 1024;
            var buffer = new char[chunk];
            var window = new StringBuilder(chunk + longest);

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    int read;
                    while ((read = reader.Read(buffer, 0, chunk)) > 0)
                    {
                        window.Append(buffer, 0, read);
                        string text = window.ToString();

                        foreach (var needle in needles)
                            if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        window.Clear();
                        int keep = Math.Min(longest, text.Length);
                        window.Append(text, text.Length - keep, keep);
                    }
                }
            }
            catch (Exception)
            {
                // A config file this editor may not read is not a setup problem to report.
                return false;
            }
            return false;
        }

        private static string SafeFolder(Environment.SpecialFolder folder)
        {
            try
            {
                return Environment.GetFolderPath(folder);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Combine(params string[] parts)
        {
            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0])) return null;
            try
            {
                string result = parts[0];
                for (int i = 1; i < parts.Length; i++) result = Path.Combine(result, parts[i]);
                return result;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string Join(List<string> names)
        {
            if (names.Count == 0) return "no client";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " and " + names[1];
            return string.Join(", ", names.ToArray(), 0, names.Count - 1) + " and " + names[names.Count - 1];
        }
    }
}
