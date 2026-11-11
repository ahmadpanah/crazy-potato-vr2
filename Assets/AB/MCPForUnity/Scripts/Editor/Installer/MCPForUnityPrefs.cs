using UnityEditor;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor
{
    /// <summary>
    /// Every persisted preference of this package, in one place so the keys cannot drift apart.
    /// All of them are EditorPrefs — per-machine, never in a project a team shares.
    ///
    /// <para>Two of them are additionally scoped <b>per project</b> (see <see cref="ProjectKey"/>).
    /// A plain machine-wide key would mean "we already introduced this asset to you" is answered
    /// once for a whole machine: import the asset into a second project and its entry point would
    /// never be shown again, in a project where the user has never seen it.</para>
    /// </summary>
    public static class MCPForUnityPrefs
    {
        private const string KEY_AUTO_OPEN = "AB.MCPForUnity.AutoOpenWhenMissing";
        private const string KEY_NOTIFY_UPDATES = "AB.MCPForUnity.NotifyUpdates";
        private const string KEY_LAST_NOTIFIED = "AB.MCPForUnity.LastNotifiedVersion";
        private const string KEY_SETUP_PINGED = "AB.MCPForUnity.SetupAssetPinged";
        private const string KEY_STEP_DONE = "AB.MCPForUnity.SetupStepDone";
        private const string KEY_AUTO_VERIFY = "AB.MCPForUnity.AutoVerifySetup";
        private const string KEY_SIMPLE_METHOD = "AB.MCPForUnity.SimpleServerMethod";

        /// <summary>Open the Welcome window when the plugin is missing. Off means the user never
        /// wants to be interrupted about it again on this machine.</summary>
        public static bool AutoOpenWhenMissing
        {
            get => EditorPrefs.GetBool(KEY_AUTO_OPEN, true);
            set => EditorPrefs.SetBool(KEY_AUTO_OPEN, value);
        }

        /// <summary>Report a newer plugin release, at most once per version, never as a popup.</summary>
        public static bool NotifyUpdates
        {
            get => EditorPrefs.GetBool(KEY_NOTIFY_UPDATES, true);
            set => EditorPrefs.SetBool(KEY_NOTIFY_UPDATES, value);
        }

        /// <summary>Last version already reported, so a single release never nags twice.</summary>
        public static string LastNotifiedVersion
        {
            get => EditorPrefs.GetString(KEY_LAST_NOTIFIED, string.Empty);
            set => EditorPrefs.SetString(KEY_LAST_NOTIFIED, value ?? string.Empty);
        }

        /// <summary>Read and written only by MCPForUnitySetupSO.cs, the hub before CLICKME. An update
        /// leaves that file in the project, compiled with this assembly, until the CLICKME legacy
        /// cleanup (MCPForUnityHubLegacy) deletes it: without this member the assembly would not compile and the cleanup
        /// would never run. Keep it while buyers can update from those versions.</summary>
        public static bool SetupAssetPinged
        {
            get => EditorPrefs.GetBool(ProjectKey(KEY_SETUP_PINGED), false);
            set => EditorPrefs.SetBool(ProjectKey(KEY_SETUP_PINGED), value);
        }

        /// <summary>
        /// Re-run <see cref="MCPForUnitySetupCheck"/> on its own while the Welcome window is open.
        /// Off leaves every answer where the last explicit "Re-check" put it — for someone who
        /// would rather this package never launched <c>node --version</c> behind their back.
        /// </summary>
        public static bool AutoVerifySetup
        {
            get => EditorPrefs.GetBool(KEY_AUTO_VERIFY, true);
            set => EditorPrefs.SetBool(KEY_AUTO_VERIFY, value);
        }

        /// <summary>
        /// Which route step 2 of the Welcome window shows first: the one-paste request to an AI
        /// assistant (true) or the commands to run by hand (false). Remembered because a user who
        /// picked one has told you which kind of reader they are.
        /// </summary>
        public static bool UseSimpleServerMethod
        {
            get => EditorPrefs.GetBool(KEY_SIMPLE_METHOD, true);
            set => EditorPrefs.SetBool(KEY_SIMPLE_METHOD, value);
        }

        /// <summary>
        /// User-ticked state of a Welcome-window setup step. Steps 2 and 3 happen outside Unity —
        /// installing Node and editing an AI client's config — so the editor can only look for the
        /// traces they leave (<see cref="MCPForUnitySetupCheck"/>), never observe them directly.
        /// A user whose client this package does not know about gets to say when they are done.
        /// Per project, because a second project is a second setup as far as the reader is concerned.
        /// </summary>
        public static bool IsStepDone(int step) =>
            EditorPrefs.GetBool(ProjectKey(KEY_STEP_DONE + step), false);

        public static void SetStepDone(int step, bool done) =>
            EditorPrefs.SetBool(ProjectKey(KEY_STEP_DONE + step), done);

        /// <summary>
        /// Suffixes a key with a stable digest of the project path.
        ///
        /// <para>FNV-1a rather than <c>string.GetHashCode()</c>: the runtime is free to change
        /// string hashing between versions, and a changed hash would silently re-fire "first time
        /// here" behaviour on an upgrade.</para>
        /// </summary>
        private static string ProjectKey(string key)
        {
            uint hash = 2166136261u;
            string path = Application.dataPath;
            for (int i = 0; i < path.Length; i++)
            {
                hash ^= path[i];
                hash *= 16777619u;
            }
            return key + "." + hash.ToString("x8");
        }
    }
}
