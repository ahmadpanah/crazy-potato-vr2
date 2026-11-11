using System.Collections.Generic;
using AnkleBreaker.MCPForUnity.Installer.Editor;
using UnityEngine.UIElements;

namespace AnkleBreaker.MCPForUnity.Editor.Docs
{
    /// <summary>"Scripting" tab — this asset's own editor API, for teams who want the install,
    /// the version check or the gate on their own terms.</summary>
    internal static class Pages_Scripting
    {
        public static void Register(List<DocPage> pages)
        {
            pages.Add(new DocPage
            {
                Id = "api-overview", Tab = DocRegistry.TAB_SCRIPTING, Order = 0,
                Title = "API overview",
                SearchText = "api scripting namespace assembly editor installer plugin status version " +
                             "isinstalled installedversion update checker result bridge resolve port",
                Build = BuildApiOverview,
            });
            pages.Add(new DocPage
            {
                Id = "scripted-install", Tab = DocRegistry.TAB_SCRIPTING, Order = 10,
                Title = "Scripting the install",
                SearchText = "script install automate ci batchmode command line client add package " +
                             "manager bootstrap onboarding team",
                Build = BuildScriptedInstall,
            });
            pages.Add(new DocPage
            {
                Id = "http-bridge", Tab = DocRegistry.TAB_SCRIPTING, Order = 20,
                Title = "Talking to the bridge directly",
                SearchText = "http bridge api ping curl endpoint json port 7890 7899 debug raw request " +
                             "resolve registry instances range reassigned which port",
                Build = BuildHttpBridge,
            });
        }

        private static void BuildApiOverview(VisualElement host)
        {
            DocUI.H1(host, "API overview");
            DocUI.Para(host,
                "Everything this asset does is available to your own editor scripts. The types live in " +
                "<b>AnkleBreaker.MCPForUnity.Installer.Editor</b>, inside the assembly " +
                "<b>AnkleBreaker.MCPForUnity.Installer.Editor</b> — reference it from your editor asmdef.");
            DocUI.Callout(host, "info",
                "That assembly deliberately references nothing. It keeps compiling while the MCP plugin " +
                "is absent, which is the state it exists to describe.");

            DocUI.H2(host, "Is the plugin here, and which version?");
            DocUI.Code(host,
                "using AnkleBreaker.MCPForUnity.Installer.Editor;\n" +
                "using UnityEngine;\n\n" +
                "if (!MCPForUnityPlugin.IsInstalled)\n" +
                "    Debug.Log(\"MCP plugin missing\");\n" +
                "else\n" +
                "    Debug.Log($\"MCP plugin {MCPForUnityPlugin.InstalledVersion}\");");
            DocUI.Table(host,
                ("MCPForUnityPlugin.IsInstalled", "Present, whether through Package Manager or as loose files."),
                ("MCPForUnityPlugin.IsManagedByPackageManager", "Package Manager owns this copy, so it can be updated in one click."),
                ("MCPForUnityPlugin.InstalledVersion", "Version string, or null for a copy Package Manager does not own."),
                ("MCPForUnityPlugin.IsTrackingBranch", "This git copy follows a branch instead of a release tag."),
                ("MCPForUnityPlugin.DescribeInstalled()", "One-liner for a status label."),
                ("MCPForUnityPlugin.Refresh()", "Drops the cached probe. Only needed when packages changed without a domain reload."));

            DocUI.H2(host, "Where is it serving?");
            DocUI.Table(host,
                ("MCPForUnityBridge.Resolve()", "The live bridge for this project: <b>IsServing</b>, " +
                                                "<b>Port</b>, <b>Url</b>, <b>PingUrl</b>, and a " +
                                                "<b>Note</b> explaining any absence."),
                ("MCPForUnityBridge.DescribePingUrl()", "The real ping URL when a bridge is up, and an " +
                                                        "honest range form when it is not."),
                ("MCPForUnityBridge.PortRangeLabel", "The range the plugin assigns from — the only thing " +
                                                     "about the port that is constant."));
            DocUI.Callout(host, "warn",
                "Never store a bridge port. The plugin picks a free one per session, so a value cached " +
                "across restarts will eventually point at another project's editor.");

            DocUI.H2(host, "Is it current?");
            DocUI.Code(host,
                "MCPForUnityUpdateChecker.Check(force: false, callback: result =>\n" +
                "{\n" +
                "    if (result.Status == MCPUpdateStatus.UpdateAvailable)\n" +
                "        Debug.Log($\"v{result.LatestVersion} available (have v{result.InstalledVersion})\");\n" +
                "});");
            DocUI.Para(host,
                "<b>MCPUpdateStatus</b> is one of <b>Unknown</b>, <b>NotInstalled</b>, <b>UpToDate</b>, " +
                "<b>UpdateAvailable</b>, <b>Ahead</b>, <b>CheckFailed</b>. <b>Ahead</b> matters: a copy " +
                "newer than the latest release must never be offered an \"update\" backwards.");
            DocUI.Callout(host, "warn",
                "Results are cached for six hours. Pass <b>force: true</b> only for an explicit " +
                "user-initiated check — GitHub allows 60 unauthenticated calls an hour per IP.");

            DocUI.H2(host, "Install or update it");
            DocUI.Code(host,
                "MCPForUnityPluginInstaller.InstallLatest();      // resolve newest release, then pin it\n" +
                "MCPForUnityPluginInstaller.Install(\"v1.2.3\");    // pin an exact tag\n" +
                "MCPForUnityPluginInstaller.Install(null);        // track the default branch\n\n" +
                "if (MCPForUnityPluginInstaller.IsBusy)\n" +
                "    Debug.Log(MCPForUnityPluginInstaller.StatusMessage);");
            DocUI.Para(host,
                "<b>MCPForUnityPluginInstaller.Changed</b> fires whenever the busy state or status " +
                "message moves — subscribe from a window, unsubscribe when it closes.");

            DocUI.Actions(host,
                ("Next: scripting the install", "page:scripted-install"),
                ("Talking to the bridge", "page:http-bridge"));
        }

        private static void BuildScriptedInstall(VisualElement host)
        {
            DocUI.H1(host, "Scripting the install");
            DocUI.Para(host,
                "Useful when a team should get the plugin without anyone clicking anything, or when a " +
                "build machine needs the bridge available.");

            DocUI.H2(host, "Install on first open, once");
            DocUI.Code(host,
                "using AnkleBreaker.MCPForUnity.Installer.Editor;\n" +
                "using UnityEditor;\n" +
                "using UnityEngine;\n\n" +
                "[InitializeOnLoad]\n" +
                "internal static class TeamMcpBootstrap\n" +
                "{\n" +
                "    private const string DONE = \"MyGame.McpBootstrapped\";\n\n" +
                "    static TeamMcpBootstrap()\n" +
                "    {\n" +
                "        if (Application.isBatchMode) return;          // never block a headless run\n" +
                "        if (SessionState.GetBool(DONE, false)) return;\n" +
                "        SessionState.SetBool(DONE, true);\n\n" +
                "        EditorApplication.delayCall += () =>\n" +
                "        {\n" +
                "            if (!MCPForUnityPlugin.IsInstalled)\n" +
                "                MCPForUnityPluginInstaller.InstallLatest();\n" +
                "        };\n" +
                "    }\n" +
                "}");
            DocUI.Callout(host, "warn",
                "Guard on <b>Application.isBatchMode</b>. A package install inside a " +
                "<b>-batchmode -quit</b> run can stall the process, and CI has no one to unblock it.");

            DocUI.H2(host, "Pin the whole team to one version");
            DocUI.Para(host,
                "Call <b>Install(\"v1.2.3\")</b> instead of <b>InstallLatest()</b>. The resulting " +
                "<b>packages-lock.json</b> entry is what everyone else resolves, so the version is " +
                "decided once, in code review, rather than by whoever installed last.");

            DocUI.H2(host, "Gate your own tooling on the plugin");
            DocUI.Code(host,
                "// Only offer an AI-assisted action when the bridge is actually there.\n" +
                "using (new EditorGUI.DisabledScope(!MCPForUnityPlugin.IsInstalled))\n" +
                "{\n" +
                "    if (GUILayout.Button(\"Ask the assistant\")) { /* ... */ }\n" +
                "}");
            DocUI.Para(host,
                "If your tooling needs the plugin's own types, compile against them conditionally — " +
                "the installer assembly defines <b>AB_UNITY_MCP</b> through <b>versionDefines</b> when " +
                "<b>" + MCPForUnityPlugin.PackageId + "</b> is present. Declare the same " +
                "<b>versionDefines</b> entry on your assembly to get the symbol there too.");

            DocUI.Actions(host, ("API overview", "page:api-overview"), ("Reference", "page:links"));
        }

        private static void BuildHttpBridge(VisualElement host)
        {
            DocUI.H1(host, "Talking to the bridge directly");
            DocUI.Para(host,
                "The plugin serves plain HTTP on <b>127.0.0.1</b>, on a port it assigns from <b>" +
                MCPForUnityBridge.PortRangeLabel + "</b>. You rarely need this — the MCP server is what " +
                "your assistant talks to — but it is the fastest way to answer \"is the editor actually " +
                "listening?\".");
            DocUI.Code(host, "curl " + MCPForUnityBridge.DescribePingUrl());
            DocUI.Para(host,
                "A healthy editor answers with JSON carrying its Unity version and project name. " +
                "Anything else — connection refused, no answer — means the bridge is not running in " +
                "the editor you are pointing at.");

            DocUI.H2(host, "Resolving the port from your own code");
            DocUI.Para(host,
                "Do not hardcode one. The plugin reassigns ports across sessions, so the honest answer " +
                "is always \"ask\". <b>MCPForUnityBridge</b> reads the same shared registry the Node " +
                "server uses, and never caches for more than a couple of seconds:");
            DocUI.Code(host,
                "using AnkleBreaker.MCPForUnity.Installer.Editor;\n" +
                "using UnityEngine;\n\n" +
                "var bridge = MCPForUnityBridge.Resolve();\n" +
                "if (bridge.IsServing)\n" +
                "    Debug.Log($\"this editor answers on {bridge.PingUrl}\");\n" +
                "else\n" +
                "    Debug.Log(bridge.Note);   // why there is no answer");
            DocUI.Callout(host, "warn",
                "Use the ping endpoint for diagnosis, not for automation. Driving the bridge by hand " +
                "bypasses the queueing and safety the MCP server layer provides, and the route surface " +
                "is the plugin's business to change between releases.");
            DocUI.Actions(host,
                ("Troubleshooting", "page:troubleshooting"),
                ("Plugin repository", "url:" + MCPForUnityPlugin.RepositoryUrl));
        }
    }
}
