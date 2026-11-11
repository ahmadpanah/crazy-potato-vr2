using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>One resolved answer to "is this editor serving, and on which port?".</summary>
    public readonly struct MCPBridgeInfo
    {
        /// <summary>A live bridge is registered for this project right now.</summary>
        public readonly bool IsServing;

        /// <summary>Port it answers on, or 0 when nothing is serving.</summary>
        public readonly int Port;

        /// <summary>Seconds since the plugin last refreshed its registry entry.</summary>
        public readonly double AgeSeconds;

        /// <summary>Why there is no answer, for a status line. Null when serving.</summary>
        public readonly string Note;

        public MCPBridgeInfo(bool isServing, int port, double ageSeconds, string note)
        {
            IsServing = isServing;
            Port = port;
            AgeSeconds = ageSeconds;
            Note = note;
        }

        /// <summary>Bridge root, e.g. <c>http://127.0.0.1:7893</c>. Empty when not serving.</summary>
        public string Url => IsServing
            ? "http://" + MCPForUnityBridge.Host + ":" + Port.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        /// <summary>Ping endpoint of the live bridge. Empty when not serving.</summary>
        public string PingUrl => IsServing ? Url + MCPForUnityBridge.PingPath : string.Empty;
    }

    /// <summary>
    /// Answers which port <b>this</b> editor's MCP bridge is serving on, right now.
    ///
    /// <para><b>Why this exists.</b> The port is not a constant. The plugin auto-selects one from
    /// <see cref="PortRangeStart"/>-<see cref="PortRangeEnd"/> (its <c>UseManualPort</c> setting is
    /// off by default) and it can be <b>reassigned across restarts and between sessions</b> — a
    /// second editor, a ParrelSync clone, or a port taken by something else all move it. Any text
    /// that states a fixed 7890 is wrong for everyone it is wrong for, and states it with total
    /// confidence. So nothing in this package hardcodes a port any more: it asks here.</para>
    ///
    /// <para><b>How.</b> The plugin registers its live port in a small JSON file shared by every
    /// Unity instance on the machine, and removes the entry when the bridge stops — so an entry for
    /// this project path <i>is</i> the statement "serving, on this port". The same file is what the
    /// Node MCP server reads to discover editors, so this reads the ecosystem's own source of
    /// truth rather than inventing a second one.</para>
    ///
    /// <para>Nothing here references the plugin's assembly: this type must keep working while the
    /// plugin is absent, like the rest of the installer assembly.</para>
    /// </summary>
    public static class MCPForUnityBridge
    {
        public const string Host = "127.0.0.1";
        public const string PingPath = "/api/ping";

        /// <summary>First port of the range the plugin picks from.</summary>
        public const int PortRangeStart = 7890;

        /// <summary>Last port of the range the plugin picks from.</summary>
        public const int PortRangeEnd = 7899;

        /// <summary>Human-readable range, for prose that must not name one port.</summary>
        public const string PortRangeLabel = "7890-7899";

        /// <summary>
        /// A registry entry older than this is treated as a crashed editor rather than a live one.
        /// The plugin heartbeats every 30 s, but that heartbeat rides <c>EditorApplication.update</c>
        /// and therefore stalls through long compiles — five minutes is the same tolerance the Node
        /// server applies, so both halves agree on when an editor is gone.
        /// </summary>
        private const double StaleAfterSeconds = 300.0;

        // Re-read at most this often. The port can move at any time, so this must stay short — it
        // exists only so an inspector repainting at 30 Hz does not hit the disk 30 times a second.
        private const double CacheSeconds = 2.0;

        private static MCPBridgeInfo _cached;
        private static double _cachedAt = double.NegativeInfinity;

        /// <summary>
        /// Where the plugin and the Node server both keep the shared instance registry.
        /// Windows: <c>%LOCALAPPDATA%/UnityMCP/instances.json</c>. macOS and Linux:
        /// <c>~/.local/share/UnityMCP/instances.json</c>.
        /// </summary>
        public static string RegistryPath
        {
            get
            {
                string dir;
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    dir = Path.Combine(localAppData, "UnityMCP");
                }
                else
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    dir = Path.Combine(home, ".local", "share", "UnityMCP");
                }
                return Path.Combine(dir, "instances.json");
            }
        }

        /// <summary>Resolves the live bridge for the project this editor has open.</summary>
        public static MCPBridgeInfo Resolve()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _cachedAt < CacheSeconds) return _cached;

            _cached = ResolveUncached();
            _cachedAt = now;
            return _cached;
        }

        /// <summary>Drops the short-lived cache so the next <see cref="Resolve"/> re-reads the file.</summary>
        public static void Invalidate() => _cachedAt = double.NegativeInfinity;

        /// <summary>
        /// The ping URL to show a user: the real one when a bridge is up, and an honest
        /// "somewhere in the range" form when it is not — never a made-up port.
        /// </summary>
        public static string DescribePingUrl()
        {
            var bridge = Resolve();
            return bridge.IsServing
                ? bridge.PingUrl
                : "http://" + Host + ":<" + PortRangeLabel + ">" + PingPath;
        }

        private static MCPBridgeInfo ResolveUncached()
        {
            if (!MCPForUnityPlugin.IsInstalled)
                return new MCPBridgeInfo(false, 0, 0.0, "The MCP plugin is not installed in this project.");

            string path = RegistryPath;
            string json;
            try
            {
                if (!File.Exists(path))
                    return new MCPBridgeInfo(false, 0, 0.0,
                        "No bridge has started yet. Open the MCP Dashboard and start it, or enable auto-start.");
                json = ReadShared(path);
            }
            catch (Exception e)
            {
                return new MCPBridgeInfo(false, 0, 0.0, "Could not read the instance registry: " + e.Message);
            }

            string wanted = NormalizePath(ProjectPath);
            foreach (var entry in EnumerateObjects(json))
            {
                string entryPath = NormalizePath(ReadString(entry, "projectPath"));
                if (!string.Equals(entryPath, wanted, StringComparison.OrdinalIgnoreCase)) continue;

                int port = ReadInt(entry, "port");
                if (port <= 0) continue;

                double age = AgeSeconds(ReadString(entry, "lastSeen"));
                if (age > StaleAfterSeconds)
                    return new MCPBridgeInfo(false, port, age,
                        "The registry still lists port " + port + " for this project, but the entry has not " +
                        "been refreshed in over five minutes - the editor that claimed it probably crashed.");

                return new MCPBridgeInfo(true, port, age, null);
            }

            return new MCPBridgeInfo(false, 0, 0.0,
                "The plugin is installed but no bridge is serving this project. Open the MCP Dashboard to start it.");
        }

        /// <summary>Project root, in the exact shape the plugin writes into the registry.</summary>
        private static string ProjectPath
        {
            get
            {
                string dataPath = Application.dataPath;   // ".../<Project>/Assets"
                const string assets = "/Assets";
                return dataPath.EndsWith(assets, StringComparison.Ordinal)
                    ? dataPath.Substring(0, dataPath.Length - assets.Length)
                    : dataPath;
            }
        }

        private static string ReadShared(string path)
        {
            // The plugin writes this file under a named mutex; a reader can still land mid-write.
            // One retry costs nothing and turns a torn read into a correct answer instead of an
            // error the user would have to interpret.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
                catch (IOException) when (attempt == 0)
                {
                    System.Threading.Thread.Sleep(20);
                }
            }
        }

        private static double AgeSeconds(string isoUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return double.MaxValue;
            // RoundtripKind alone: it honours the trailing Z the plugin writes with "o", and the
            // framework rejects combining it with AdjustToUniversal outright.
            if (!DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime stamp))
                return double.MaxValue;

            double age = (DateTime.UtcNow - stamp.ToUniversalTime()).TotalSeconds;
            // A negative age means the clock moved; treat it as fresh rather than as an editor that
            // crashed, since the entry demonstrably exists.
            return age < 0.0 ? 0.0 : age;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace('\\', '/').TrimEnd('/');
        }

        // ── Minimal JSON reading ───────────────────────────────────────────
        // JsonUtility cannot parse a top-level array, and this assembly references nothing on
        // purpose, so it must not depend on Newtonsoft being in the project. The registry is
        // written by one known producer and only four scalar fields are needed, so a small
        // brace-matching scan is enough — and it degrades to "no entry" rather than throwing.

        private static IEnumerable<string> EnumerateObjects(string json)
        {
            if (string.IsNullOrEmpty(json)) yield break;

            int depth = 0, start = -1;
            bool inString = false, escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{') { if (depth++ == 0) start = i; continue; }
                if (c != '}') continue;

                if (--depth == 0 && start >= 0)
                {
                    yield return json.Substring(start, i - start + 1);
                    start = -1;
                }
                if (depth < 0) yield break; // malformed; stop rather than guess
            }
        }

        private static string ReadString(string json, string key)
        {
            int at = ValueStart(json, key);
            if (at < 0 || json[at] != '"') return null;

            var value = new System.Text.StringBuilder();
            for (int i = at + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[++i];
                    value.Append(next == 'n' ? '\n' : next == 't' ? '\t' : next);
                    continue;
                }
                if (c == '"') break;
                value.Append(c);
            }
            return value.ToString();
        }

        private static int ReadInt(string json, string key)
        {
            int at = ValueStart(json, key);
            if (at < 0) return 0;

            int end = at;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return int.TryParse(json.Substring(at, end - at), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int result) ? result : 0;
        }

        /// <summary>Index of the first character of <paramref name="key"/>'s value, or -1.</summary>
        private static int ValueStart(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (at < 0) return -1;

            int colon = json.IndexOf(':', at);
            if (colon < 0) return -1;

            int value = colon + 1;
            while (value < json.Length && char.IsWhiteSpace(json[value])) value++;
            return value < json.Length ? value : -1;
        }
    }
}
