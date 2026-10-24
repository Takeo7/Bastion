using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Bastion.Client.Net.EditorTools
{
    /// <summary>
    /// The app, built: the scenes of the build settings, into Builds/&lt;system&gt;/ (or -salida &lt;path&gt;). In batchmode:
    /// unity/tools/unity.sh -executeMethod Bastion.Client.Net.EditorTools.AppBuild.Mac (or .Windows, .Linux). Two copies play
    /// together on one computer if each has its own BASTION_DATOS (NetLink).
    /// </summary>
    public static class AppBuild
    {
        /// <summary>The name the player sees; the files go without the accent (productName «Bastion»).</summary>
        private const string Shown = "Bastión";

        /// <summary>
        /// Builds/Mac/Bastion.app, signed ad hoc and checked, and Builds/Bastion-Mac.zip to hand it on. Its executable is
        /// «Bastion»: with the accent, codesign can't seal it («a sealed resource is missing or invalid»). The Finder and the
        /// menu bar show «Bastión» (CFBundleName, CFBundleDisplayName). A signature that doesn't check fails the build.
        /// </summary>
        public static void Mac()
        {
            var app = Build(BuildTarget.StandaloneOSX, "Builds/Mac/Bastion.app");
            if (app == null) return;
            app = Path.GetFullPath(app);
            var plist = Path.Combine(app, "Contents", "Info.plist");
            var zip = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(app)!)!, "Bastion-Mac.zip");
            var steps = new[]
            {
                ("plutil", $"-replace CFBundleName -string {Shown} \"{plist}\""),
                ("plutil", $"-replace CFBundleDisplayName -string {Shown} \"{plist}\""),
                ("codesign", $"--force --deep --sign - \"{app}\""),
                ("codesign", $"--verify --deep --strict --verbose=2 \"{app}\""),
            };
            foreach (var (tool, arguments) in steps)
                if (!Ran(tool, arguments)) return;
            if (File.Exists(zip)) File.Delete(zip);
            if (!Ran("ditto", $"-c -k --sequesterRsrc --keepParent \"{app}\" \"{zip}\"")) return;
            Debug.Log($"[red] firma ad hoc comprobada (codesign --verify --deep --strict) · {new FileInfo(zip).Length / (1024 * 1024)} MB → {zip}");
        }

        /// <summary>
        /// Builds/Windows/Bastion.exe, with its Bastion_Data and the rest beside it: the folder goes whole. From a Mac only
        /// with Mono (IL2CPP for Windows needs a Windows computer); it needs Unity's «Windows Build Support (Mono)».
        /// </summary>
        public static void Windows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Bastion.exe");
        }

        /// <summary>
        /// Builds/Linux/Bastion.x86_64, with its Bastion_Data and the rest beside it: the folder goes whole (a .tar.gz keeps
        /// the executable's permission). Mono, as for Windows; it needs Unity's «Linux Build Support (Mono)».
        /// </summary>
        public static void Linux()
        {
            Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Bastion.x86_64");
        }

        /// <summary>The project's active platform, said in the log: a build for another one must leave it as it was.</summary>
        public static void Target()
        {
            Debug.Log($"[red] plataforma activa: {EditorUserBuildSettings.activeBuildTarget}");
        }

        /// <summary>Where it was built; null if it wasn't (in batchmode, Unity exits with 1).</summary>
        private static string? Build(BuildTarget target, string where)
        {
            var before = EditorUserBuildSettings.activeBuildTarget;
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-salida");
            var output = at >= 0 && at + 1 < args.Length ? args[at + 1] : where;
            var scenes = EditorBuildSettings.scenes.Where((s) => s.enabled).Select((s) => s.path).ToArray();
            var unpanelled = WithoutPanel(scenes);
            if (unpanelled.Count > 0)
            {
                Debug.LogError($"[red] app ({target}): no se compila, hay UIDocument sin PanelSettings (en la app no se pintarían): {string.Join(" · ", unpanelled)}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return null;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = output, target = target, options = BuildOptions.None });
            var summary = report.summary;
            // Left on another platform, everyone's next run would import the assets again for it (and capture on it).
            if (EditorUserBuildSettings.activeBuildTarget != before) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(before), before);
            Debug.Log($"[red] app ({target}): {summary.result} en {summary.totalTime.TotalSeconds:F0} s · {summary.totalSize / (1024 * 1024)} MB · {summary.totalErrors} errores · {string.Join(", ", scenes)} → {output}");
            if (summary.result == BuildResult.Succeeded) return output;
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return null;
        }

        /// <summary>
        /// The UIDocuments of the build's scenes without a panel to draw on (no PanelSettings, nor a parent document's):
        /// in the app they show nothing, though the editor's captures give them one by code (24/10: the start screen black
        /// after «Made with Unity», on the Mac and on Linux). Where each one is.
        /// </summary>
        private static List<string> WithoutPanel(string[] scenes)
        {
            var missing = new List<string>();
            foreach (var path in scenes)
            {
                var open = SceneManager.GetSceneByPath(path);
                var scene = open.isLoaded ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var document in root.GetComponentsInChildren<UIDocument>(true))
                        if (document.panelSettings == null && document.parentUI == null) missing.Add($"{path}: {PathOf(document.transform)}");
                if (!open.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            return missing;
        }

        private static string PathOf(Transform t)
        {
            return t.parent == null ? t.name : $"{PathOf(t.parent)}/{t.name}";
        }

        /// <summary>A command of the Mac's, waited for; if it fails, what it said goes to the log and the build fails.</summary>
        private static bool Ran(string tool, string arguments)
        {
            var start = new ProcessStartInfo(tool, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0) return true;
            Debug.LogError($"[red] {tool} {arguments} (salida {process.ExitCode}): {(output.Result + error).Trim().Replace('\n', ' ')}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }
    }
}
