---
name: mcp-for-unity
description: Install, connect, update and troubleshoot the AnkleBreaker Unity MCP plugin with the free MCP Plugin for Unity asset (Assets/AB/MCPForUnity) in a Unity project.
---

# MCP Plugin for Unity (AnkleBreaker)

This free, editor-only asset installs, documents and keeps up to date the open-source
**AnkleBreaker Unity MCP** plugin (`com.anklebreaker.unity-mcp`): the bridge that lets an MCP
client (Claude Desktop, Claude Code, Cursor, Codex, Kimi...) read and drive the Unity Editor.
The asset ships **no runtime code and no copy of the plugin**: it fetches the plugin from GitHub.

All paths below are relative to this file's folder (the package root, `Assets/AB/MCPForUnity`
by default). The package may be moved; the code finds its assets by name when it is.

## Requirements

| Need | Detail |
|---|---|
| Unity | The plugin declares 2021.3.18f1 or newer; the asset is built on 2022.3 LTS. |
| Git | **On the PATH before Unity starts.** Package Manager resolves the plugin with the git client. After installing Git, restart Unity. |
| Node.js | 18 or newer, for the MCP server (`npx -y anklebreaker-unity-mcp@latest`). |
| Network | GitHub for the plugin download and the release lookup. The plugin's Welcome window also reads `raw.githubusercontent.com` (public AnkleBreaker catalogue and card images) and `anklebreaker-studio.com` (devlog feed, post and cover). Downloads only; nothing about the project is sent. |

No Git? Download the release's source zip from
<https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/releases>, extract it **outside**
`Assets/`, then `Window > Package Manager > + > Add package from disk...` and pick its
`package.json` (also on the Documentation page **Installation**, "Without git"). Later updates through
the asset's buttons still need Git; without it, replace the folder with a newer release the same way.

## Entry points

- `CLICKME.asset` (package root): select it. While the plugin is missing (or older than 2.40.0,
  which first ships the hub), its Inspector is one card, **Install the MCP plugin** or **Update the
  MCP plugin**, with **Open the plugin setup**, then Documentation, Plugin on GitHub and Discord.
  With the plugin installed, the Inspector is the hub the plugin ships: plugin version and bridge
  address, **Open the Welcome guide**, four tiles (**Open the plugin setup**, **MCP Dashboard**,
  **Documentation**, **Report a bug**), links to the Welcome's Assets and Studio tabs, Discord and
  **Leave a review**. It runs nothing until it is displayed.
- `Tools > AnkleBreaker > Unity MCP > Welcome`: the studio Welcome window. It ships with the
  plugin (2.40.0 and later), not with this asset, so GitHub users of the plugin get it too; it
  opens once per project (getting started, AnkleBreaker assets, studio news). Its steps point at
  the plugin's Dashboard and the MCP server's guide on GitHub. Offline (or if the catalogue is
  unreachable or malformed) its Assets tab, recommendations and devblog hide silently; the
  getting-started content stays.
- `Tools > AnkleBreaker > MCP For Unity > Documentation`: 16 in-editor pages (quick start,
  guided tour, installation, client setup, dashboard, updates, several editors, troubleshooting,
  FAQ, what ships, API, scripted install, HTTP bridge). Search covers every page.
- `Tools > AnkleBreaker > Dependencies Installer > MCP For Unity`: the **plugin setup window**
  (title "MCP Plugin Setup", `Scripts/Editor/Welcome/MCPForUnityWelcomeWindow.cs`). It also
  opens from CLICKME, from documentation links, and by itself while the plugin
  is missing. It shows the installed version, **Install the plugin** / **Update to vX** /
  **Reinstall pinned to a release** / **Check for updates**, and four measured setup steps.

## First run: plugin, AI client, checks

1. Select `CLICKME.asset` > **Open the plugin setup** > **Install the plugin**. The asset resolves
   the newest GitHub release and adds `https://github.com/AnkleBreaker-Studio/unity-mcp-plugin.git#vX.Y.Z`
   (pinned tag) through Package Manager, then Unity recompiles.
2. Check the Console for `[AB-UMCP] Server started on port <n>`.
3. Connect the AI client. Simplest route: paste this request to the client
   (text from `MCPForUnityPlugin.AssistantInstallRequest`):

   ```text
   Install this MCP server for me and add it to your own MCP configuration:
     name: unity
     command: npx -y anklebreaker-unity-mcp@latest
     docs: https://github.com/AnkleBreaker-Studio/unity-mcp-server
   Then restart yourself so the server is loaded, and tell me which Unity project you can see.
   ```

   Per-client manual configuration (Claude Desktop, Claude Code, Cursor, Codex TOML, Kimi, with
   Windows, macOS and Linux paths) is on the Documentation page **Connecting your AI client**.
4. In the plugin setup window, step 4 shows the live bridge. Steps 2 and 3 happen outside Unity:
   the check looks for `node` on the PATH and for a config entry in the known client files (Claude
   Desktop, Claude Code, Cursor, Codex, Kimi, and the project's `.vscode/mcp.json`); a
   client it does not know reads as *Not detected*, which is not a failure.

## Facts that prevent common mistakes

- **The port is not fixed.** The plugin picks one in **7890-7899** and can change it between
  sessions. Read it, never hardcode it: `MCPForUnityBridge.Resolve()` reads the shared registry
  (`%LOCALAPPDATA%/UnityMCP/instances.json` on Windows, `~/.local/share/UnityMCP/instances.json`
  on macOS/Linux); the CLICKME Inspector and setup step 4 display it. Health check:
  `curl http://127.0.0.1:<port>/api/ping` answers JSON with the Unity version and project name.
- **The dashboard's menu is `Window > AB Unity MCP > Dashboard`, from plugin 2.39.7.** Older
  plugins registered it at `Window/AB Unity MCP`, which their own children shadow, so no menu
  opens it there. `Tools > AnkleBreaker > MCP For Unity > Dashboard` (also CLICKME >
  **MCP Dashboard**), the documentation action and the plugin's
  toolbar button > **Open Dashboard...** work with every version. From code:
  `MCPForUnityPlugin.OpenDashboard()` (runs `MCPForUnityPlugin.DashboardMenuPath` when the plugin
  registers it, otherwise opens the window by type).
- **The plugin needs `com.unity.test-framework` and `com.unity.ugui` to compile**, and does not
  declare them (plugin 2.39.7). Unity's default project templates include both; in a project
  created without them, every install route adds a plugin that fails with errors in
  `MCPTestRunnerCommands.cs` and `MCPUICommands.cs`. Add both from the Unity registry, then Unity
  recompiles the plugin.
- **Updates are pinned to tags**.
  Re-adding the bare Git URL does nothing because
  `Packages/packages-lock.json` keeps the resolved commit; the asset changes the `#vX.Y.Z` suffix.
  A copy installed without a tag is reported as tracking a branch, with a pinned reinstall offered.
  Install falls back to the default branch only when the release lookup fails (for example the API
  rate limit): it clones the same github.com URL, so it does not work with GitHub unreachable. A copy newer than the latest release is `Ahead` and is
  never "updated" backwards. A copy dropped into `Assets/` is "unmanaged": its version cannot be read,
  and the update check returns `MCPUpdateStatus.Unknown` for it.
- Release lookups are cached **six hours** (GitHub allows 60 anonymous calls per hour per IP).
  One Console line per new plugin release, never a popup.
- macOS: the MCP server's default Unity Hub path is the Windows one; set `UNITY_HUB_PATH`
  (see **Connecting your AI client**).

## Editor API (namespace `AnkleBreaker.MCPForUnity.Installer.Editor`)

Assembly `AnkleBreaker.MCPForUnity.Installer.Editor` references nothing, so these compile whether
or not the plugin is installed. Editor-only; there is no runtime assembly.

```csharp
using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEngine;

if (!MCPForUnityPlugin.IsInstalled)
    MCPForUnityPluginInstaller.InstallLatest();          // resolve newest release, pin it
else
    Debug.Log(MCPForUnityPlugin.DescribeInstalled());    // version / source one-liner

MCPForUnityUpdateChecker.Check(force: false, callback: r =>
{
    if (r.Status == MCPUpdateStatus.UpdateAvailable)
        MCPForUnityPluginInstaller.Install(r.LatestTag); // e.g. "v2.39.5"
});

MCPBridgeInfo bridge = MCPForUnityBridge.Resolve();      // cached 2 s
if (bridge.IsServing) Debug.Log(bridge.PingUrl);         // http://127.0.0.1:<port>/api/ping

MCPForUnitySetupCheck.Completed += report =>
    Debug.Log(report.Bridge.State + " " + report.Bridge.Headline);
MCPForUnitySetupCheck.Check(force: true);                // runs off the main thread
```

- `MCPForUnityPlugin`: `IsInstalled`, `IsManagedByPackageManager`, `InstalledVersion`,
  `InstalledFromGit`, `IsTrackingBranch`, `Refresh()`, constants `PackageId`, `GitUrl`,
  `MinimumNodeMajor` (18), `ServerNpxCommand`.
- `MCPForUnityPluginInstaller`: `InstallLatest()`, `Install(tag)` (`null` = default branch),
  `IsBusy`, `StatusMessage`, event `Changed`, `OpenInPackageManager()`.
- `MCPForUnityUpdateChecker`: `Check(force, callback)`, `LastResult`, event `Completed`;
  `MCPUpdateStatus` = Unknown (an unmanaged copy in `Assets/`: version unreadable), NotInstalled,
  UpToDate, UpdateAvailable, Ahead, CheckFailed. Before any check completes, `LastResult` is null.
- `MCPForUnitySetupCheck`: `Check(force)`, `Last`, event `Completed`; `MCPSetupReport` has
  `Plugin`, `Server` (Node.js), `Client`, `Bridge`, each an `MCPStepStatus`
  (`MCPCheckState` Pending, Blocked, NotDetected, Failed, Passed).
- Conditional compilation: the installer assembly defines `AB_UNITY_MCP` via `versionDefines`
  when `com.anklebreaker.unity-mcp` is present; declare the same entry on your own asmdef to use
  the plugin's types.

## Files

- `CLICKME.asset`, `README.md`, `SKILL.md`.
- `Scripts/Editor/Installer/`: identity, probe, installer, update checker, setup check, bridge
  resolver, prefs, dependency gate, and `MCPForUnityClickMe.cs` (the hub's status line and the
  Dashboard menu).
- `Scripts/Editor/Installer/Hub/`: the CLICKME inspector (`MCPForUnityHub*.cs`, stamped by
  clickme-inspector `clickme.py host`, do not hand-edit). It draws the hub shipped by the plugin
  and, on update, deletes the Welcome window earlier versions of this asset shipped
  (`Scripts/Editor/Installer/Welcome/`).
- `Scripts/Editor/Welcome/`: the plugin setup window. `Scripts/Editor/Docs/`: documentation.
- Settings are EditorPrefs `AB.MCPForUnity.*` (listed on the Documentation page
  **What ships in this asset**); the plugin's Welcome window keeps a download cache in
  `Library/AnkleBreakerWelcome`. The only project file the asset changes is `Packages/manifest.json`,
  when an install adds the plugin dependency.

## Limits

- This asset does not contain the MCP plugin or server; both come from public GitHub/npm.
- The plugin's own features (tools, dashboard, action history) belong to the plugin: see its
  README at <https://github.com/AnkleBreaker-Studio/unity-mcp-plugin>.
- The setup check can only see the clients and config files it knows; closing the plugin setup
  window while the plugin is missing stops it reopening for the current editor session only.
- Support: Discord <https://discord.gg/2D36VUmsYN>; plugin issues on its GitHub repository.
