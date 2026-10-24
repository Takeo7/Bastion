using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bastion.Client.Net.EditorTools
{
    /// <summary>
    /// The app, built for the Mac: the scenes of the build settings, into Builds/Mac/Bastion.app (or -salida &lt;path&gt;). In
    /// batchmode: unity/tools/unity.sh -executeMethod Bastion.Client.Net.EditorTools.AppBuild.Mac. Two copies play together
    /// on one computer if each has its own BASTION_DATOS (NetLink).
    /// </summary>
    public static class AppBuild
    {
        public static void Mac()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-salida");
            var output = at >= 0 && at + 1 < args.Length ? args[at + 1] : "Builds/Mac/Bastion.app";
            var scenes = EditorBuildSettings.scenes.Where((s) => s.enabled).Select((s) => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
            var summary = report.summary;
            Debug.Log($"[red] app: {summary.result} en {summary.totalTime.TotalSeconds:F0} s · {summary.totalSize / (1024 * 1024)} MB · {summary.totalErrors} errores · {string.Join(", ", scenes)} → {output}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
