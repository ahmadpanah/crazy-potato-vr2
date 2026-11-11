using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>How the installed plugin compares to the newest published release.</summary>
    public enum MCPUpdateStatus
    {
        /// <summary>No check has completed yet in this session.</summary>
        Unknown,
        /// <summary>The plugin is not in this project at all.</summary>
        NotInstalled,
        UpToDate,
        UpdateAvailable,
        /// <summary>The installed copy is newer than the latest release (a dev build, or a
        /// branch install) — never offer to "update" someone backwards.</summary>
        Ahead,
        CheckFailed
    }

    /// <summary>Outcome of one update check. A value type so callers can keep it around safely.</summary>
    public readonly struct MCPUpdateCheckResult
    {
        public readonly MCPUpdateStatus Status;
        public readonly string InstalledVersion;
        public readonly string LatestVersion;
        public readonly string LatestReleaseName;
        public readonly string LatestReleaseUrl;
        public readonly string Error;

        public MCPUpdateCheckResult(MCPUpdateStatus status, string installedVersion, string latestVersion,
            string latestReleaseName, string latestReleaseUrl, string error)
        {
            Status = status;
            InstalledVersion = installedVersion;
            LatestVersion = latestVersion;
            LatestReleaseName = latestReleaseName;
            LatestReleaseUrl = latestReleaseUrl;
            Error = error;
        }

        /// <summary>The git ref to install to land on the latest release, or null if unknown.</summary>
        public string LatestTag => string.IsNullOrEmpty(LatestVersion) ? null : "v" + LatestVersion;
    }

    /// <summary>
    /// Asks GitHub which plugin release is newest and compares it to what is installed.
    ///
    /// <para>Results are cached in EditorPrefs for <see cref="CacheHours"/> hours: the GitHub API
    /// allows 60 unauthenticated calls an hour per IP, and a checker that burns them on every
    /// domain reload would start answering "check failed" exactly when a user needs it most.
    /// <see cref="Check"/> with <c>force</c> bypasses the cache for explicit "check now" clicks.</para>
    /// </summary>
    public static class MCPForUnityUpdateChecker
    {
        private const string KEY_VERSION = "AB.MCPForUnity.Cache.LatestVersion";
        private const string KEY_NAME = "AB.MCPForUnity.Cache.LatestReleaseName";
        private const string KEY_URL = "AB.MCPForUnity.Cache.LatestReleaseUrl";
        private const string KEY_TIME = "AB.MCPForUnity.Cache.CheckedUtc";

        private const double CacheHours = 6.0;
        private const int TimeoutSeconds = 10;

        private static UnityWebRequest _request;
        private static readonly List<Action<MCPUpdateCheckResult>> PendingCallbacks =
            new List<Action<MCPUpdateCheckResult>>();

        /// <summary>A request is in flight right now.</summary>
        public static bool IsChecking => _request != null;

        /// <summary>Result of the last completed check this session, or null if none ran.</summary>
        public static MCPUpdateCheckResult? LastResult { get; private set; }

        /// <summary>Raised on the main thread whenever a check completes, cached or live.</summary>
        public static event Action<MCPUpdateCheckResult> Completed;

        /// <summary>
        /// Checks for a newer plugin release.
        /// </summary>
        /// <param name="force">Ignore the cache — use for an explicit user-initiated check.</param>
        /// <param name="callback">Invoked once, on the main thread, when the result is known.</param>
        public static void Check(bool force = false, Action<MCPUpdateCheckResult> callback = null)
        {
            if (!force && TryReadCache(out string cachedVersion, out string cachedName, out string cachedUrl))
            {
                Deliver(Compare(cachedVersion, cachedName, cachedUrl), callback);
                return;
            }

            if (callback != null) PendingCallbacks.Add(callback);
            if (_request != null) return; // already in flight; this callback rides along

            var request = UnityWebRequest.Get(MCPForUnityPlugin.LatestReleaseApiUrl);
            // GitHub rejects requests without a User-Agent outright.
            request.SetRequestHeader("User-Agent", "AnkleBreaker-MCPForUnity");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.timeout = TimeoutSeconds;

            _request = request;
            request.SendWebRequest();
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            var request = _request;
            if (request == null)
            {
                EditorApplication.update -= Poll;
                return;
            }
            if (!request.isDone) return;

            EditorApplication.update -= Poll;
            _request = null;

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    DeliverToAll(Failed(DescribeTransportError(request)));
                    return;
                }

                string tag = ParseTagName(request.downloadHandler.text, out string releaseName);
                if (string.IsNullOrEmpty(tag))
                {
                    DeliverToAll(Failed("GitHub answered, but the response carried no release tag."));
                    return;
                }

                string version = NormalizeVersion(tag);
                // Building the release URL from the tag beats trusting whichever html_url a loose
                // parse happens to find first — a release page URL is fully determined by its tag.
                string url = MCPForUnityPlugin.ReleasesUrl + "/tag/" + tag;

                WriteCache(version, releaseName, url);
                DeliverToAll(Compare(version, releaseName, url));
            }
            catch (Exception e)
            {
                DeliverToAll(Failed("Could not read the GitHub response: " + e.Message));
            }
            finally
            {
                request.Dispose();
            }
        }

        private static string DescribeTransportError(UnityWebRequest request)
        {
            if (request.responseCode == 403)
                return "GitHub rate limit reached (60 requests per hour). Try again later.";
            if (request.responseCode == 404)
                return "The plugin repository has no published release yet.";
            return string.IsNullOrEmpty(request.error) ? "Network request failed." : request.error;
        }

        // ── Result assembly ────────────────────────────────────────────────

        private static MCPUpdateCheckResult Compare(string latestVersion, string releaseName, string releaseUrl)
        {
            MCPForUnityPlugin.Refresh();
            string installed = MCPForUnityPlugin.InstalledVersion;

            if (!MCPForUnityPlugin.IsInstalled)
                return new MCPUpdateCheckResult(MCPUpdateStatus.NotInstalled, null,
                    latestVersion, releaseName, releaseUrl, null);

            // A copy dropped into Assets/ carries no version. Reporting "up to date" would be a
            // guess; reporting "update available" would be a lie. Say what is actually known.
            if (string.IsNullOrEmpty(installed))
                return new MCPUpdateCheckResult(MCPUpdateStatus.Unknown, null,
                    latestVersion, releaseName, releaseUrl,
                    "This copy of the plugin is not managed by Package Manager, so its version cannot be read.");

            int order = CompareVersions(installed, latestVersion);
            var status = order < 0 ? MCPUpdateStatus.UpdateAvailable
                : order > 0 ? MCPUpdateStatus.Ahead
                : MCPUpdateStatus.UpToDate;

            return new MCPUpdateCheckResult(status, installed, latestVersion, releaseName, releaseUrl, null);
        }

        private static MCPUpdateCheckResult Failed(string error)
        {
            MCPForUnityPlugin.Refresh();
            return new MCPUpdateCheckResult(MCPUpdateStatus.CheckFailed,
                MCPForUnityPlugin.InstalledVersion, null, null, null, error);
        }

        private static void DeliverToAll(MCPUpdateCheckResult result)
        {
            LastResult = result;

            // Copy and clear first: a callback is free to start another check, and it must not
            // land in the list we are currently walking.
            var callbacks = PendingCallbacks.ToArray();
            PendingCallbacks.Clear();
            foreach (var callback in callbacks) SafeInvoke(callback, result);

            Completed?.Invoke(result);
        }

        private static void Deliver(MCPUpdateCheckResult result, Action<MCPUpdateCheckResult> callback)
        {
            LastResult = result;
            SafeInvoke(callback, result);
            Completed?.Invoke(result);
        }

        private static void SafeInvoke(Action<MCPUpdateCheckResult> callback, MCPUpdateCheckResult result)
        {
            if (callback == null) return;
            try
            {
                callback(result);
            }
            catch (Exception e)
            {
                // One window throwing while it repaints must not swallow the result for the others.
                Debug.LogException(e);
            }
        }

        // ── Cache ──────────────────────────────────────────────────────────

        private static bool TryReadCache(out string version, out string name, out string url)
        {
            version = EditorPrefs.GetString(KEY_VERSION, string.Empty);
            name = EditorPrefs.GetString(KEY_NAME, string.Empty);
            url = EditorPrefs.GetString(KEY_URL, string.Empty);

            if (string.IsNullOrEmpty(version)) return false;

            string stamp = EditorPrefs.GetString(KEY_TIME, string.Empty);
            if (!DateTime.TryParse(stamp, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime checkedUtc))
                return false;

            double age = (DateTime.UtcNow - checkedUtc).TotalHours;
            // A negative age means the clock moved backwards; treat the entry as unusable rather
            // than as valid for the next six hours plus however far the clock jumped.
            return age >= 0 && age < CacheHours;
        }

        private static void WriteCache(string version, string name, string url)
        {
            EditorPrefs.SetString(KEY_VERSION, version ?? string.Empty);
            EditorPrefs.SetString(KEY_NAME, name ?? string.Empty);
            EditorPrefs.SetString(KEY_URL, url ?? string.Empty);
            EditorPrefs.SetString(KEY_TIME, DateTime.UtcNow.ToString("O"));
        }

        /// <summary>Forgets the cached release so the next check goes to the network.</summary>
        public static void ClearCache()
        {
            EditorPrefs.DeleteKey(KEY_VERSION);
            EditorPrefs.DeleteKey(KEY_NAME);
            EditorPrefs.DeleteKey(KEY_URL);
            EditorPrefs.DeleteKey(KEY_TIME);
        }

        // ── Parsing ────────────────────────────────────────────────────────

        [Serializable]
        private class ReleaseDto
        {
#pragma warning disable 0649 // assigned by JsonUtility
            public string tag_name;
            public string name;
#pragma warning restore 0649
        }

        /// <summary>Reads "tag_name" (and the release title) out of a GitHub release payload.</summary>
        private static string ParseTagName(string json, out string releaseName)
        {
            releaseName = null;
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var dto = JsonUtility.FromJson<ReleaseDto>(json);
                if (dto != null && !string.IsNullOrEmpty(dto.tag_name))
                {
                    releaseName = dto.name;
                    return dto.tag_name;
                }
            }
            catch (Exception)
            {
                // Fall through to the manual read below.
            }

            return ExtractString(json, "tag_name");
        }

        private static string ExtractString(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return null;

            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return null;

            int open = json.IndexOf('"', colon + 1);
            if (open < 0) return null;

            int close = json.IndexOf('"', open + 1);
            return close < 0 ? null : json.Substring(open + 1, close - open - 1);
        }

        /// <summary>Strips a leading "v" and any build metadata, leaving a comparable version.</summary>
        public static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return version;
            version = version.Trim();
            if (version.Length > 1 && (version[0] == 'v' || version[0] == 'V')) version = version.Substring(1);
            int plus = version.IndexOf('+');
            return plus >= 0 ? version.Substring(0, plus) : version;
        }

        /// <summary>
        /// Orders two semver strings: negative when <paramref name="a"/> is older, 0 when equal,
        /// positive when newer. Pre-release suffixes (1.2.3-rc1) sort below their release, as
        /// semver requires — otherwise an rc would forever look like an available "update".
        /// </summary>
        public static int CompareVersions(string a, string b)
        {
            SplitVersion(NormalizeVersion(a), out int[] aNums, out string aPre);
            SplitVersion(NormalizeVersion(b), out int[] bNums, out string bPre);

            for (int i = 0; i < 3; i++)
            {
                if (aNums[i] != bNums[i]) return aNums[i] < bNums[i] ? -1 : 1;
            }

            bool aIsPre = !string.IsNullOrEmpty(aPre);
            bool bIsPre = !string.IsNullOrEmpty(bPre);
            if (aIsPre && !bIsPre) return -1;
            if (!aIsPre && bIsPre) return 1;
            if (!aIsPre) return 0;

            return string.CompareOrdinal(aPre, bPre);
        }

        private static void SplitVersion(string version, out int[] numbers, out string prerelease)
        {
            numbers = new int[3];
            prerelease = null;
            if (string.IsNullOrEmpty(version)) return;

            int dash = version.IndexOf('-');
            if (dash >= 0)
            {
                prerelease = version.Substring(dash + 1);
                version = version.Substring(0, dash);
            }

            var parts = version.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++)
                int.TryParse(parts[i], out numbers[i]);
        }
    }
}
