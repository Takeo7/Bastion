using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bastion.Client.Net.EditorTools
{
    /// <summary>
    /// The app's icon with its version (Arturo, 1/11): the 0.10.1's bus over the sierra with the yellow badge top right
    /// that says Application.version, drawn by unity/tools/icono/icono.swift (AppKit: from a Mac, as the builds are) at
    /// every size, the badge bigger at the small ones. Made at each build and kept out of git:
    /// - the Mac's goes into the app once Unity has built it, as Resources/PlayerIcon.icns (iconutil, every size of
    ///   macOS's set drawn at its own pixels), before it is signed;
    /// - the Windows one, inside the .exe, only Unity puts there: through PlayerSettings, imported in Generado/ (in
    ///   .gitignore) and put back as it was after the build, so ProjectSettings doesn't change.
    /// </summary>
    public static class AppIcon
    {
        /// <summary>The script and its base (packages/desktop/build/icon.png), from the project's folder.</summary>
        private const string Tool = "../tools/icono/icono.swift";
        private const string Base = "../tools/icono/base.png";

        /// <summary>The badge's typeface: UI's Oswald (OFL).</summary>
        private const string Font = "Assets/Bastion/UI/Fonts/Oswald-SemiBold.ttf";

        /// <summary>Where the Windows sizes are imported for PlayerSettings while the build lasts.</summary>
        private const string Generated = "Assets/Bastion/Client/Net/Generado";

        /// <summary>The name Unity gives it in Info.plist (CFBundleIconFile).</summary>
        private const string MacName = "PlayerIcon.icns";

        /// <summary>macOS's set: each file of the .iconset and the pixels it is drawn at.</summary>
        private static readonly (string File, int Pixels)[] MacSet =
        {
            ("icon_16x16.png", 16), ("icon_16x16@2x.png", 32), ("icon_32x32.png", 32), ("icon_32x32@2x.png", 64),
            ("icon_128x128.png", 128), ("icon_128x128@2x.png", 256), ("icon_256x256.png", 256), ("icon_256x256@2x.png", 512),
            ("icon_512x512.png", 512), ("icon_512x512@2x.png", 1024),
        };

        /// <summary>
        /// The Mac's icon into the built `app`: Contents/Resources/PlayerIcon.icns, named in Info.plist. The .icns as it was
        /// written (its SHA-256), for Checked once the app is signed; null if it couldn't be made (in batchmode, Unity
        /// exits with 1).
        /// </summary>
        public static string? Mac(string app)
        {
            var folder = Path.Combine(Path.GetTempPath(), "bastion-icono-" + Guid.NewGuid().ToString("N"));
            try
            {
                var iconset = Path.Combine(folder, "PlayerIcon.iconset");
                Directory.CreateDirectory(iconset);
                if (!Draw(folder, "mac", MacSet.Select((s) => s.Pixels).Distinct())) return null;
                foreach (var (file, pixels) in MacSet) File.Copy(Path.Combine(folder, $"icono-{pixels}.png"), Path.Combine(iconset, file));
                var icns = Path.Combine(app, "Contents", "Resources", MacName);
                if (!AppBuild.Ran("iconutil", $"-c icns \"{iconset}\" -o \"{icns}\"")) return null;
                if (!AppBuild.Ran("plutil", $"-replace CFBundleIconFile -string {MacName} \"{Path.Combine(app, "Contents", "Info.plist")}\"")) return null;
                Debug.Log($"[red] icono {Application.version} · Mac: {MacName}, de 16 a 1024 px · {new FileInfo(icns).Length / 1024} KB");
                return Hash(icns);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// Whether the signed `app` shows the icon Mac wrote (`hash`): Info.plist names it (CFBundleIconFile) and that file
        /// in Resources is the one written. If not, it says so and the build fails.
        /// </summary>
        public static bool Checked(string app, string hash)
        {
            var named = Said("plutil", $"-extract CFBundleIconFile raw -o - \"{Path.Combine(app, "Contents", "Info.plist")}\"")?.Trim();
            var why = string.IsNullOrEmpty(named) ? "Info.plist no nombra ningún icono (CFBundleIconFile)"
                : !File.Exists(Path.Combine(app, "Contents", "Resources", named)) ? $"Info.plist nombra {named}, que no está en Resources"
                : Hash(Path.Combine(app, "Contents", "Resources", named)) != hash ? $"{named} no es el icono con la versión"
                : null;
            if (why == null)
            {
                Debug.Log($"[red] icono {Application.version} · Info.plist lo nombra ({named}) y es el escrito");
                return true;
            }
            Debug.LogError($"[red] icono {Application.version}: {why}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }

        /// <summary>
        /// The Windows icon in PlayerSettings (Standalone) for the build that follows, at each size it asks for. What puts
        /// back the icons there were and takes the imported ones away, to run once it is built; null if it couldn't be made
        /// (in batchmode, Unity exits with 1).
        /// </summary>
        public static Action? Windows()
        {
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var before = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Path.GetDirectoryName(Generated)!.Replace('\\', '/'), Path.GetFileName(Generated));
            if (!Draw(Path.GetFullPath(Generated), "windows", sizes.Distinct())) return null;
            var icons = sizes.Select((n) =>
            {
                var path = $"{Generated}/icono-{n}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }).ToArray();
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
            Debug.Log($"[red] icono {Application.version} · Windows: {string.Join(", ", sizes)} px, por PlayerSettings mientras se compila");
            return () =>
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, before, IconKind.Any);
                AssetDatabase.DeleteAsset(Generated);
                AssetDatabase.SaveAssets();
            };
        }

        /// <summary>The icon at each of `sizes` px into `folder` (icono-&lt;n&gt;.png), for `system` (mac, windows).</summary>
        private static bool Draw(string folder, string system, System.Collections.Generic.IEnumerable<int> sizes)
        {
            return AppBuild.Ran("swift", $"\"{Path.GetFullPath(Tool)}\" \"{Path.GetFullPath(Base)}\" \"{Path.GetFullPath(Font)}\" {Application.version} \"{folder}\" {system} {string.Join(" ", sizes)}");
        }

        private static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select((b) => b.ToString("x2")));
        }

        /// <summary>What a command of the Mac's wrote, or null if it failed.</summary>
        private static string? Said(string tool, string arguments)
        {
            var start = new ProcessStartInfo(tool, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Result : null;
        }
    }
}
