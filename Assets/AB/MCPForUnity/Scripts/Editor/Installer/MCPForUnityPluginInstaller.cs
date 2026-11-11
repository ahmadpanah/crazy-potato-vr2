using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>
    /// Installs and updates the AnkleBreaker Unity MCP plugin through Package Manager.
    ///
    /// <para><b>Why installs are pinned to a tag.</b> Unity records the resolved commit of a git
    /// dependency in <c>packages-lock.json</c> and keeps honouring it, so adding the bare
    /// repository URL a second time is a no-op: the user clicks "Update" and nothing happens.
    /// Adding <c>...git#v2.39.4</c> changes the dependency string itself, which forces a
    /// re-resolve — and leaves the project on a version a teammate can reproduce.</para>
    /// </summary>
    public static class MCPForUnityPluginInstaller
    {
        private const string LOG_PREFIX = "[MCP For Unity] ";

        // A git install ends in a domain reload, which wipes the AddRequest mid-flight. The target
        // is mirrored here so the outcome can still be reported after the reload lands.
        private const string SESSION_PENDING = "AB.MCPForUnity.PendingInstall";

        private static AddRequest _request;

        // InstallLatest resolves the tag over the network before it can call Client.Add, and that
        // gap is long enough for a second click to slip through _request's null check.
        private static bool _resolvingLatest;

        /// <summary>An install or update is running; UI should disable its action buttons.</summary>
        public static bool IsBusy => _request != null || _resolvingLatest || !string.IsNullOrEmpty(PendingTarget);

        /// <summary>What the current operation is doing, for a status line. Empty when idle.</summary>
        public static string StatusMessage { get; private set; } = string.Empty;

        /// <summary>Raised whenever <see cref="IsBusy"/> or <see cref="StatusMessage"/> changes.</summary>
        public static event Action Changed;

        private static string PendingTarget
        {
            get => SessionState.GetString(SESSION_PENDING, string.Empty);
            set => SessionState.SetString(SESSION_PENDING, value ?? string.Empty);
        }

        /// <summary>
        /// Resolves the newest published release, then installs or updates the plugin to it.
        /// Falls back to the repository's default branch when the release lookup fails (for example
        /// the GitHub API rate limit). The fallback clones the same github.com URL, so it does not
        /// help when GitHub itself cannot be reached: that install fails in Package Manager.
        /// </summary>
        public static void InstallLatest()
        {
            if (IsBusy) return;

            _resolvingLatest = true;
            SetStatus("Looking up the latest release...");
            MCPForUnityUpdateChecker.Check(force: true, callback: result =>
            {
                _resolvingLatest = false;
                string tag = result.LatestTag;
                if (string.IsNullOrEmpty(tag))
                {
                    Debug.LogWarning(LOG_PREFIX + "Could not determine the latest release (" +
                        (result.Error ?? "unknown reason") +
                        "). Installing from the repository's default branch instead.");
                }
                Install(tag);
            });
        }

        /// <summary>
        /// Installs the plugin at <paramref name="versionTag"/> (e.g. "v2.39.4"), or from the
        /// default branch when null.
        /// </summary>
        public static void Install(string versionTag)
        {
            if (_request != null)
            {
                Debug.LogWarning(LOG_PREFIX + "An install is already running.");
                return;
            }

            string url = string.IsNullOrEmpty(versionTag)
                ? MCPForUnityPlugin.GitUrl
                : MCPForUnityPlugin.GitUrl + "#" + versionTag;

            string label = string.IsNullOrEmpty(versionTag) ? "latest commit" : versionTag;
            Debug.Log(LOG_PREFIX + "Installing " + MCPForUnityPlugin.DisplayName + " (" + label + ") from " + url);

            PendingTarget = url;
            SetStatus("Installing " + label + "...");

            try
            {
                _request = Client.Add(url);
            }
            catch (Exception e)
            {
                PendingTarget = string.Empty;
                SetStatus(string.Empty);
                Debug.LogError(LOG_PREFIX + "Package Manager refused the request: " + e.Message);
                return;
            }

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
            if (!request.IsCompleted) return;

            EditorApplication.update -= Poll;
            _request = null;
            PendingTarget = string.Empty;

            if (request.Status == StatusCode.Success)
            {
                var package = request.Result;
                Debug.Log(LOG_PREFIX + "Installed " + package.displayName + " " + package.version + ".");
                SetStatus(string.Empty);
                MCPForUnityPlugin.Refresh();
                // The freshly installed release is now the installed one; a stale cached check
                // would otherwise keep the "update available" banner up until it expires.
                MCPForUnityUpdateChecker.ClearCache();
            }
            else
            {
                Debug.LogError(LOG_PREFIX + "Install failed: " + request.Error?.message + Hint(request.Error));
                SetStatus(string.Empty);
            }
        }

        private static string Hint(UnityEditor.PackageManager.Error error)
        {
            string message = error?.message ?? string.Empty;
            if (message.IndexOf("git", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "\nUnity installs git packages with the git command line client. Install git, " +
                       "make sure it is on the PATH, and restart Unity — Package Manager only reads " +
                       "the PATH at editor startup.";
            }
            return string.Empty;
        }

        /// <summary>
        /// Reports the outcome of an install that was still in flight when the domain reloaded.
        /// Without this the most common success path — Unity recompiling the moment the package
        /// lands — would finish in silence.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ReportPendingAfterReload()
        {
            string pending = PendingTarget;
            if (string.IsNullOrEmpty(pending)) return;

            PendingTarget = string.Empty;
            EditorApplication.delayCall += () =>
            {
                MCPForUnityPlugin.Refresh();
                if (MCPForUnityPlugin.IsInstalled)
                {
                    Debug.Log(LOG_PREFIX + MCPForUnityPlugin.DisplayName + " " +
                              MCPForUnityPlugin.DescribeInstalled() + " is installed.");
                    MCPForUnityUpdateChecker.ClearCache();
                }
                else
                {
                    Debug.LogWarning(LOG_PREFIX + "The install of " + pending +
                                     " did not complete. Check the console above for the Package Manager error.");
                }
                Changed?.Invoke();
            };
        }

        /// <summary>Opens Package Manager on the installed plugin so the user can remove or inspect it.</summary>
        public static void OpenInPackageManager()
        {
            UnityEditor.PackageManager.UI.Window.Open(MCPForUnityPlugin.PackageId);
        }

        private static void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
