using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    public enum DependencyInstallMethod
    {
        Git,
        AssetStore
    }

    [Serializable]
    public class DependencyInfo
    {
        public string DisplayName;
        public string PackageId;
        public string AltPackageId;
        public string AssemblyName;
        public DependencyInstallMethod InstallMethod;
        public string GitUrl;
        public string AssetStoreUrl;
    }

    /// <summary>
    /// Dependency gate for MCP For Unity.
    ///
    /// <para>This package has exactly one dependency and it is also its whole reason to exist: the
    /// AnkleBreaker Unity MCP plugin. So the familiar AnkleBreaker installer contract — detect on
    /// domain reload, offer a one-click install, never claim success on a package that did not
    /// compile — is what drives the product itself, not a side door next to it.</para>
    /// </summary>
    public static class MCPForUnityDependenciesInstaller
    {
        private const string LOG_PREFIX = "[MCP For Unity] ";

        // This package ships no runtime code — there is nothing for a player build to contain — so
        // the assembly whose compilation gates "ready" is the editor one. Reporting deps installed
        // while our own windows failed to compile would be the false success this check prevents.
        private const string OWN_ASSEMBLY = "AnkleBreaker.MCPForUnity.Editor";

        private const string SESSION_KEY_DISMISSED = "AB.MCPForUnity.DepsDismissed";

        private static readonly DependencyInfo[] Dependencies =
        {
            new DependencyInfo
            {
                DisplayName   = MCPForUnityPlugin.DisplayName,
                PackageId     = MCPForUnityPlugin.PackageId,
                AltPackageId  = null,
                AssemblyName  = MCPForUnityPlugin.AssemblyName,
                InstallMethod = DependencyInstallMethod.Git,
                GitUrl        = MCPForUnityPlugin.GitUrl
            }
        };

        // ── Auto-check on domain reload ────────────────────────────────────

        // Startup sequencing, learned the hard way. Unity applies the saved window layout while the
        // editor boots, and applying a layout closes every window open at that moment — a prompt
        // opened from a plain delayCall lands on the wrong side of it and vanishes without a trace.
        // 2022.3 exposes no "layout applied" event, so the check waits for the editor to fall quiet,
        // then confirms its own window actually survived and opens once more if it did not. The
        // confirmation is what makes this correct rather than merely late.
        private const double SETTLE_SECONDS = 1.0;
        private const int MAX_OPEN_ATTEMPTS = 2;

        private static double _quietSince = -1.0;
        private static int _openAttempts;

        // True between "this check opened the Welcome window" and "this check saw it survive".
        // A window that disappears inside that gap was not closed by a person — nobody has had the
        // chance to decide anything yet — it was closed by the editor applying its saved layout,
        // which is the whole reason MAX_OPEN_ATTEMPTS exists. See NotifyWelcomeClosed.
        private static bool _awaitingOpenConfirmation;

        [InitializeOnLoadMethod]
        private static void CheckOnLoad()
        {
            // Headless imports (CI, Asset Store validation, cloud builds) must never pop a window —
            // it can stall a -batchmode -quit process indefinitely.
            if (Application.isBatchMode) return;

            _quietSince = -1.0;
            _openAttempts = 0;
            _awaitingOpenConfirmation = false;
            EditorApplication.update += RunStartupCheck;
        }

        private static void RunStartupCheck()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _quietSince = -1.0;
                return;
            }

            if (_quietSince < 0.0)
            {
                _quietSince = EditorApplication.timeSinceStartup;
                return;
            }
            if (EditorApplication.timeSinceStartup - _quietSince < SETTLE_SECONDS) return;

            if (MCPForUnityPlugin.IsInstalled)
            {
                Done();
                // Reaching here after a removal should nag again, so the dismiss is per-session.
                SessionState.SetBool(SESSION_KEY_DISMISSED, false);
                NotifyIfOutdated();
                return;
            }

            if (SessionState.GetBool(SESSION_KEY_DISMISSED, false) || !MCPForUnityPrefs.AutoOpenWhenMissing)
            {
                Done();
                return;
            }

            // The prompt is up (either window counts) — nothing left to do. Stopping here also means
            // a user who closes it during the settle window is not shown it again.
            if (MCPForUnityWindows.IsWelcomeOpen ||
                EditorWindow.HasOpenInstances<MCPForUnityDependencyWindow>())
            {
                Done();
                return;
            }

            if (_openAttempts >= MAX_OPEN_ATTEMPTS)
            {
                Done();
                return;
            }

            _openAttempts++;
            // Re-arm the settle window so the next pass verifies this open survived rather than
            // stacking a second one on top of it.
            _quietSince = EditorApplication.timeSinceStartup;
            _awaitingOpenConfirmation = true;
            OpenWindow();
        }

        /// <summary>
        /// The startup check is finished, whatever the outcome. From here on the window's fate is
        /// the user's business, so a close means what it looks like it means.
        /// </summary>
        private static void Done()
        {
            _awaitingOpenConfirmation = false;
            EditorApplication.update -= RunStartupCheck;
        }

        /// <summary>
        /// Reports a newer plugin release once — never twice for the same version, never as a
        /// window that steals focus while someone is working.
        /// </summary>
        private static void NotifyIfOutdated()
        {
            if (!MCPForUnityPrefs.NotifyUpdates) return;

            MCPForUnityUpdateChecker.Check(force: false, callback: result =>
            {
                if (result.Status != MCPUpdateStatus.UpdateAvailable) return;
                if (MCPForUnityPrefs.LastNotifiedVersion == result.LatestVersion) return;

                MCPForUnityPrefs.LastNotifiedVersion = result.LatestVersion;
                Debug.Log(LOG_PREFIX + MCPForUnityPlugin.DisplayName + " v" + result.LatestVersion +
                          " is available (installed: v" + result.InstalledVersion +
                          "). Select CLICKME.asset and press Open the plugin setup to update.");
            });
        }

        /// <summary>The user said no. Always honoured — this is only ever called from a control
        /// they had to click.</summary>
        public static void SetDismissed() => SessionState.SetBool(SESSION_KEY_DISMISSED, true);

        /// <summary>
        /// The Welcome window went away while the plugin was still missing. Usually that is a user
        /// saying "not now", and it has to count as one — otherwise the startup check re-opens and
        /// re-focuses the window on every domain reload.
        ///
        /// <para><b>Except while this check is still confirming its own open.</b> Unity applies the
        /// saved window layout during boot, and applying a layout closes every window open at that
        /// moment. Counting that as a dismiss made the retry above unreachable in precisely the
        /// case it was written for: the flag is tested before <c>_openAttempts</c>, so the editor
        /// closing the window would end the check for the session and the buyer would never see
        /// the Welcome window on the import that matters. A close nobody could have performed yet
        /// is not a decision.</para>
        /// </summary>
        public static void NotifyWelcomeClosed()
        {
            if (_awaitingOpenConfirmation) return;
            SetDismissed();
        }

        // ── Detection ──────────────────────────────────────────────────────

        /// <summary>Every dependency present <b>and</b> this package's own editor assembly compiled.</summary>
        public static bool AllDependenciesInstalled()
            => AllRequiredDepsInstalled() && IsOwnAssemblyLoaded();

        public static bool AllRequiredDepsInstalled()
        {
            foreach (var dep in Dependencies)
                if (!MCPForUnityPlugin.IsAssemblyLoaded(dep.AssemblyName)) return false;
            return true;
        }

        public static bool IsOwnAssemblyLoaded() => MCPForUnityPlugin.IsAssemblyLoaded(OWN_ASSEMBLY);

        public static DependencyInfo[] GetDependencies() => Dependencies;

        public static List<DependencyInfo> GetMissingGitDependencies()
        {
            var missing = new List<DependencyInfo>();
            foreach (var dep in Dependencies)
            {
                if (dep.InstallMethod != DependencyInstallMethod.Git) continue;
                if (!MCPForUnityPlugin.IsAssemblyLoaded(dep.AssemblyName)) missing.Add(dep);
            }
            return missing;
        }

        // ── Entry points ───────────────────────────────────────────────────

        /// <summary>
        /// Opens the plugin setup window — the product-grade version of a dependency window — and
        /// falls back to a bare install prompt when the editor assembly is unavailable.
        ///
        /// <para>Its menu is the installer path of every AnkleBreaker package (workflow §7.1). The
        /// window's former menu, <c>.../MCP For Unity/Welcome</c>, is gone: the studio Welcome window
        /// ships with the plugin. The startup check, the CLICKME asset and doc links also call this.</para>
        /// </summary>
        [MenuItem("Tools/AnkleBreaker/Dependencies Installer/MCP For Unity", priority = 100)]
        public static void OpenWindow()
        {
            if (MCPForUnityWindows.OpenWelcome()) return;
            MCPForUnityDependencyWindow.ShowWindow();
        }
    }

    /// <summary>
    /// Minimal fallback prompt, used only when the Welcome window is unavailable. It lives in the
    /// installer assembly — the one with no references and therefore nothing that can stop it
    /// compiling — so a user is never left with a broken editor and no way to fix it.
    /// </summary>
    public class MCPForUnityDependencyWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<MCPForUnityDependencyWindow>(true, "MCP For Unity — Dependencies");
            window.minSize = new Vector2(460f, 240f);
            window.Show();
        }

        private void OnEnable()
        {
            MCPForUnityPluginInstaller.Changed += Repaint;
        }

        private void OnDisable()
        {
            MCPForUnityPluginInstaller.Changed -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MCP For Unity", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "This asset installs and maintains the AnkleBreaker Unity MCP plugin.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            foreach (var dep in MCPForUnityDependenciesInstaller.GetDependencies())
            {
                bool installed = MCPForUnityPlugin.IsAssemblyLoaded(dep.AssemblyName);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(dep.DisplayName, EditorStyles.boldLabel, GUILayout.Width(220f));
                    EditorGUILayout.LabelField(installed ? "Installed" : "MISSING", GUILayout.Width(80f));

                    using (new EditorGUI.DisabledScope(installed || MCPForUnityPluginInstaller.IsBusy))
                    {
                        if (GUILayout.Button("Install", GUILayout.Width(90f)))
                            MCPForUnityPluginInstaller.InstallLatest();
                    }
                }
            }

            if (MCPForUnityPluginInstaller.IsBusy)
                EditorGUILayout.HelpBox(MCPForUnityPluginInstaller.StatusMessage, MessageType.Info);
            else if (MCPForUnityDependenciesInstaller.AllRequiredDepsInstalled() &&
                     !MCPForUnityDependenciesInstaller.IsOwnAssemblyLoaded())
                EditorGUILayout.HelpBox(
                    "The plugin is installed but this asset's editor assembly is not loaded. " +
                    "Unity may still be compiling, or another script in the project has a compile error.",
                    MessageType.Warning);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open the plugin repository"))
                    Application.OpenURL(MCPForUnityPlugin.RepositoryUrl);
                if (GUILayout.Button("I understand, don't ask again this session"))
                {
                    MCPForUnityDependenciesInstaller.SetDismissed();
                    Close();
                }
            }
        }
    }
}
