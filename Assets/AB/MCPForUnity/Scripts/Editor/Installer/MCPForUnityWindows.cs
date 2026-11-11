using System;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>
    /// Lets the installer assembly open this package's windows without referencing the assembly
    /// they live in. The editor assembly registers the openers at load; until it does — or if it
    /// failed to compile — the openers are null and callers fall back to something that always works.
    ///
    /// <para>A delegate rather than <c>ExecuteMenuItem</c> on purpose: a menu path is a string the
    /// compiler cannot check, so renaming a menu item would silently break every caller.</para>
    /// </summary>
    public static class MCPForUnityWindows
    {
        private const string LOG_PREFIX = "[MCP For Unity] ";

        /// <summary>Opens the Welcome window. Registered by the editor assembly at load.</summary>
        public static Action WelcomeOpener;

        /// <summary>Opens the Documentation window. Registered by the editor assembly at load.</summary>
        public static Action DocumentationOpener;

        /// <summary>Answers whether the Welcome window is open right now. Registered alongside the
        /// opener; lets a caller confirm an open actually took, which matters during editor
        /// startup where the window layout can close a freshly opened window.</summary>
        public static Func<bool> WelcomeIsOpenProbe;

        public static bool CanOpenWelcome => WelcomeOpener != null;
        public static bool CanOpenDocumentation => DocumentationOpener != null;

        /// <summary>True when the Welcome window exists. False when it does not, and also when the
        /// editor assembly is unavailable to answer — callers treat both as "not shown yet".</summary>
        public static bool IsWelcomeOpen
        {
            get
            {
                var probe = WelcomeIsOpenProbe;
                if (probe == null) return false;
                try
                {
                    return probe();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Opens the Welcome window; returns false when it is unavailable.</summary>
        public static bool OpenWelcome() => Invoke(WelcomeOpener, "Welcome");

        /// <summary>Opens the Documentation window; returns false when it is unavailable.</summary>
        public static bool OpenDocumentation() => Invoke(DocumentationOpener, "Documentation");

        private static bool Invoke(Action opener, string label)
        {
            if (opener == null) return false;
            try
            {
                opener();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(LOG_PREFIX + "Could not open the " + label + " window: " + e.Message);
                return false;
            }
        }
    }
}
