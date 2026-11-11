using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnkleBreaker.MCPForUnity.Installer.Editor.Hub
{
    /// <summary>
    /// Deletes what an earlier version of this package left behind. A package import adds and
    /// overwrites, it never deletes: a buyer who updates keeps every file the old version shipped
    /// and this one no longer does, an old Welcome window and its duplicate menu included.
    ///
    /// <para>The list is written by <c>welcome.py stamp</c> from the config's <c>legacy</c> block.
    /// Each entry is resolved by GUID, so a package folder the buyer moved is still found. It is
    /// deleted only when it lies under Assets/, its file name is the recorded one (a GUID reused
    /// by something else is left alone) and it is not part of this window. A listed folder goes
    /// only once it is empty. Once nothing is left, a load costs one GUID lookup per entry.</para>
    ///
    /// <para>Stamped only for a package that has a legacy or rehome list, into the Welcome's own assembly:
    /// it depends on nothing but the editor.</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class MCPForUnityHubLegacy
    {
        private const string SELF = "MCPForUnityHubLegacy";

        /// <summary>GUID, then the asset path in the version that shipped it. Files first, then
        /// folders deepest first.</summary>
        private static readonly string[] ENTRIES =
        {
            "bb8d9637b6422194fa823464e42095a9", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/MCPForUnitySetupSO.cs",
            "97b20b42f75fae74ca583a5c72b70ee9", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPForUnity.clickme.json",
            "34845c3cf4c323c49b727130e9a6c2e1", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPForUnity.welcome.json",
            "41e11a4b5a610a3110773bfd7ba49302", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginClickMe.cs",
            "d08c23f164fd3ff42a0c9146475294ee", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginClickMe.uss",
            "53b6a44caffe2314c97ce0749ff224e6", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcome.ClickMe.cs",
            "1ab218243a66f82409addf7611b37d1d", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcome.cs",
            "fbab6bbf4708909489259c44b3e5a78e", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcome.Start.cs",
            "e584b2cd582d8064c94fd167d51613e3", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcome.Store.cs",
            "dbe107bcc8e2b21418ad4346a9036b15", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcome.uss",
            "eda77f1f71283dd4985ee358a37d72a9", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcomeLegacy.cs",
            "3576e675cc7eefe4dbcc68876c8cca72", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcomeModel.cs",
            "75e907660c9deb942b38a3850556cb94", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/MCPPluginWelcomeServices.cs",
            "f5c048ad404f618408d93a6f6344087e", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/Cartograph.jpg",
            "33504e8f9c801f648bb47e765f6037f5", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/Changeling.jpg",
            "fc9fc5a1e102ad04da43dc6fa863d51f", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/CinematicCameraController.jpg",
            "98b88d057e5bcde4e866621f16ab25c8", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/Engraver.jpg",
            "711fb3b86650e7d43af31a40b9de9657", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/EnvironmentRocks.jpg",
            "613cbfe84f21164469d7b22e78191631", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/Gatling.jpg",
            "9b3bd917cf6f6c44b924ae76be0b3198", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/ManagersLoader.jpg",
            "c9e45d59b49afd04f800d6a48b6eb782", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/ManagersLoaderFishNet.jpg",
            "3e663c4ea3e383644ad01a7ba5be4dc3", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/MapGUIPro.jpg",
            "cf9a6eb04ddf1b4408fbeb69758ad23c", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/MCPForUnity.jpg",
            "f1cab69979d9d0c478a1c6c52737397a", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/PopupMaster.jpg",
            "339cf244fafb2274a80048ebcd6d4c8b", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/RadialMenu.jpg",
            "a60825bccfcdde5418aef2ca7f696ed6", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/Tombstack.jpg",
            "5965b99ec0e73524082ee4056ae57d8c", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards/TweenInInspector.jpg",
            "2d9ff1f50b354c643bb1107d4d23c308", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Games/kickdom-hero.jpg",
            "028e4a9ff0a6a1943969560971a9d18d", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Games/kickdom-logo.png",
            "5d6b05332f53a1f44830d2773f51e8ce", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Games/mithrall-hero.jpg",
            "cf1bc34c4a9d7c542b8d06d0e7e9a10d", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Games/mithrall-logo.png",
            "39df83a830c6e7d4c9fe94eca28a0503", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Icon.png",
            "dda984b76c54c564e8f84a77cd222bf6", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/welcome-catalog.json",
            "45c8161a57400074cbf196850e6b5c1b", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Cards",
            "4bbaceba9c1fd5340bc394a3fcf4f105", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media/Games",
            "869b0d0e9af2c4542bbae03b98bbf64d", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome/Media",
            "135e1175557305c4dbb36c43476725ed", "Assets/AB/MCPForUnity/Scripts/Editor/Installer/Welcome",
        };

        /// <summary>What the stamp writes beside this file: never deleted, whatever the list says.</summary>
        private static readonly string[] OWN = { "MCPForUnityHub.cs", "MCPForUnityHubInspector.cs", "MCPForUnityHubLegacy.cs" };

        /// <summary>GUID, then file name: files this version ships directly in the package root.
        /// An update onto a root folder with another name (renamed by the buyer or by an earlier
        /// version) places a new root-level file in a fresh folder named after ours; a folder that
        /// holds nothing but such files is moved home and removed.</summary>
        private static readonly string[] REHOME = {  };

        /// <summary>Folders between the package root and this file.</summary>
        private const int ROOT_DEPTH = 4;

        /// <summary>EditorApplication.update, not delayCall: an editor left in the background after
        /// an import keeps its delayCall queue pending for minutes, while update keeps ticking.</summary>
        static MCPForUnityHubLegacy()
        {
            EditorApplication.update -= WaitForIdle;
            EditorApplication.update += WaitForIdle;
        }

        /// <summary>No asset is deleted while the editor imports, compiles, plays or builds.</summary>
        private static void WaitForIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                return;
            EditorApplication.update -= WaitForIdle;
            Run();
        }

        /// <summary>Deletes what remains of the list and returns the removed asset paths.</summary>
        internal static List<string> Run()
        {
            var removed = new List<string>();
            string own = OwnFolder();
            if (own == null) return removed;
            Rehome(own);

            var files = new List<string>();
            var folders = new List<string>();
            for (int i = 0; i + 1 < ENTRIES.Length; i += 2)
            {
                string path = AssetDatabase.GUIDToAssetPath(ENTRIES[i]);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (!string.Equals(Path.GetFileName(path), Path.GetFileName(ENTRIES[i + 1]), StringComparison.Ordinal)) continue;
                if (IsOwn(path, own)) continue;
                if (AssetDatabase.IsValidFolder(path)) folders.Add(path);
                else if (File.Exists(path)) files.Add(path);
            }
            if (files.Count == 0 && folders.Count == 0) return removed;

            if (files.Count > 0)
            {
                CloseWindowsOf(files);
                var failed = new List<string>();
                AssetDatabase.DeleteAssets(files.ToArray(), failed);
                foreach (string file in files)
                    if (!failed.Contains(file)) removed.Add(file);
            }

            folders.Sort((a, b) => b.Length.CompareTo(a.Length));
            foreach (string folder in folders)
            {
                if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0 && AssetDatabase.DeleteAsset(folder))
                    removed.Add(folder);
            }

            if (removed.Count > 0)
                Debug.Log("MCP For Unity: removed " + removed.Count + " asset(s) left by its previous version " +
                          "(a package import never deletes):\n" + string.Join("\n", removed));
            return removed;
        }

        /// <summary>Moves stray root-level files into the package root. Only a folder holding
        /// nothing but these files counts as stray: one the buyer filled is left alone.</summary>
        private static void Rehome(string own)
        {
            if (REHOME.Length == 0) return;
            string root = own;
            for (int i = 0; i < ROOT_DEPTH && !string.IsNullOrEmpty(root); i++)
                root = Path.GetDirectoryName(root)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root)) return;

            var moved = new List<string>();
            var strays = new HashSet<string>();
            for (int i = 0; i + 1 < REHOME.Length; i += 2)
            {
                string path = AssetDatabase.GUIDToAssetPath(REHOME[i]);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (dir == root || !HoldsOnlyRootFiles(dir)) continue;
                string target = root + "/" + REHOME[i + 1];
                if (File.Exists(target) || !string.IsNullOrEmpty(AssetDatabase.MoveAsset(path, target))) continue;
                moved.Add(path + " -> " + target);
                strays.Add(dir);
            }
            foreach (string dir in strays)
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) AssetDatabase.DeleteAsset(dir);

            if (moved.Count > 0)
                Debug.Log("MCP For Unity: moved " + moved.Count + " file(s) into the package folder " +
                          "(an update onto a renamed folder placed them beside it):\n" + string.Join("\n", moved));
        }

        private static bool HoldsOnlyRootFiles(string dir)
        {
            if (string.IsNullOrEmpty(dir) || Directory.GetDirectories(dir).Length > 0) return false;
            foreach (string file in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 5);
                bool known = false;
                for (int i = 1; i < REHOME.Length; i += 2) known |= REHOME[i] == name;
                if (!known) return false;
            }
            return true;
        }

        /// <summary>An old window already open would outlive its script by one domain reload.</summary>
        private static void CloseWindowsOf(List<string> files)
        {
            var doomed = new HashSet<string>(files);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                MonoScript script = window != null ? MonoScript.FromScriptableObject(window) : null;
                if (script != null && doomed.Contains(AssetDatabase.GetAssetPath(script))) window.Close();
            }
        }

        private static string OwnFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets(SELF + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == SELF + ".cs") return Path.GetDirectoryName(path)?.Replace('\\', '/');
            }
            return null;
        }

        /// <summary>This window's files, its Media, its folder and every folder holding it.</summary>
        private static bool IsOwn(string path, string own)
        {
            if (path == own || own.StartsWith(path + "/", StringComparison.Ordinal)) return true;
            if (path.StartsWith(own + "/Media/", StringComparison.Ordinal)) return true;
            string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            return dir == own && Array.IndexOf(OWN, Path.GetFileName(path)) >= 0;
        }
    }
}
