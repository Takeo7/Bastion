using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Bastion.Client.Units;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
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
    /// unity/tools/unity.sh -executeMethod Bastion.Client.Net.EditorTools.AppBuild.Mac (or .Windows, .Linux; iOS through
    /// unity/tools/ios.sh, in a copy of the project: IosSimulator, IosDevice). Two copies play
    /// together on one computer if each has its own BASTION_DATOS (NetLink). Its data unpacked unless -comprimir (LZ4HC):
    /// the Synty atlases are mostly flat colour and the meshes repeat, so the Mac's 844 MB come to 306, but each build takes
    /// some 95 s more and each load some tenths of a second (26/10, docs/UNITY.md); the releases decide. The sound must be
    /// there as unity/tools/sonido.sh generated it (Sound); -sinSonido builds without checking. So must what the figures
    /// are made of (Figures): what isn't used goes out of the app, and nothing that is may go with it. -desarrollo builds
    /// a development one, only to measure with the profiler (Build).
    /// </summary>
    public static class AppBuild
    {
        /// <summary>The name the player sees, without the accent as the files (productName) and the game (Arturo, cuadro 188).</summary>
        private const string Shown = "Bastion";

        /// <summary>«Bastion 0.14.15»: the name with the version the build carries (bundleVersion, publicar.sh sets it).</summary>
        private static string Versioned() => $"{Shown} {PlayerSettings.bundleVersion}".Trim();

        /// <summary>
        /// Builds/Mac/Bastion.app, signed (ad hoc unless BASTION_FIRMA says an identity: Signer) and checked, and
        /// Builds/Bastion-Mac.zip to hand it on. Its executable is «Bastion»: with the accent, codesign can't seal it («a
        /// sealed resource is missing or invalid»). The Finder and the menu bar show «Bastion 0.14.15» (CFBundleName,
        /// CFBundleDisplayName; «Bastión» until the 0.14.14). Its icon carries the version (AppIcon), put in before
        /// signing and checked after. A signature or an icon that doesn't check fails the build.
        /// </summary>
        public static void Mac()
        {
            var app = Build(BuildTarget.StandaloneOSX, "Builds/Mac/Bastion.app");
            if (app == null) return;
            app = Path.GetFullPath(app);
            var icon = AppIcon.Mac(app);
            if (icon == null) return;
            var plist = Path.Combine(app, "Contents", "Info.plist");
            var zip = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(app)!)!, "Bastion-Mac.zip");
            var signer = Signer();
            // The version in its name (Arturo, cuadro 162: «en el archivo y en el Dock»): «Bastion 0.14.15» in the menu bar
            // (CFBundleName, CFBundleDisplayName) and as the bundle inside the zip, the one copied to Applications and kept in
            // the Dock. The identifier and productName stay: Arturo's preferences and his saves (persistentDataPath comes
            // from companyName/productName, «Bastion-Unity/Bastion», not from the bundle's name, which has the version).
            var shown = Versioned();
            var steps = new[]
            {
                ("plutil", $"-replace CFBundleName -string \"{shown}\" \"{plist}\""),
                ("plutil", $"-replace CFBundleDisplayName -string \"{shown}\" \"{plist}\""),
                ("codesign", signer == null ? $"--force --deep --sign - \"{app}\"" : $"--force --deep --timestamp --sign \"{signer}\" \"{app}\""),
                ("codesign", $"--verify --deep --strict --verbose=2 \"{app}\""),
            };
            foreach (var (tool, arguments) in steps)
                if (!Ran(tool, arguments)) return;
            if (!AppIcon.Checked(app, icon)) return;
            if (File.Exists(zip)) File.Delete(zip);
            // Builds/Mac/Bastion.app keeps its name (publicar.sh, the test copies and Pruebas know it by it); for the zip it is
            // renamed «Bastion 0.14.15.app» beside it and back after (the signature doesn't seal the bundle's own name).
            var named = Path.Combine(Path.GetDirectoryName(app)!, shown + ".app");
            if (Directory.Exists(named)) Directory.Delete(named, true);
            Directory.Move(app, named);
            try
            {
                if (!Ran("ditto", $"-c -k --sequesterRsrc --keepParent \"{named}\" \"{zip}\"")) return;
            }
            finally
            {
                Directory.Move(named, app);
            }
            Debug.Log($"[red] firma {(signer == null ? "ad hoc" : $"de {signer}")} comprobada (codesign --verify --deep --strict) · «{shown}.app» en el zip · {new FileInfo(zip).Length / (1024 * 1024)} MB → {zip}");
        }

        /// <summary>
        /// The Mac app's signer: BASTION_FIRMA=&lt;identity&gt;, one of the keychain's (`security find-identity -v -p
        /// codesigning`: its hash, or its name, «Apple Development: Arturo Wagener (SWRT945VV4)»), signs it with that one and
        /// Apple's timestamp (still valid once the certificate expires, 5/11/2027); unset, empty or «-», ad hoc (null), as
        /// the releases go. The 0.12.16 to 0.12.18 went out signed with that certificate (Gameplay, 10/11), so that what
        /// macOS keeps of the app would be the signature's requirement, the same at each version, and not its code's hash:
        /// the Mac still asked about the firewall or the local network (Arturo, 10/11), and Gatekeeper took it as the ad hoc
        /// one. What was seen, and why it may have asked, in docs/FIRMA.md.
        /// </summary>
        private static string? Signer()
        {
            var id = (Environment.GetEnvironmentVariable("BASTION_FIRMA") ?? "").Trim();
            return id.Length == 0 || id == "-" ? null : id;
        }

        /// <summary>
        /// Builds/Windows/Bastion.exe, with its Bastion_Data and the rest beside it: the folder goes whole. From a Mac only
        /// with Mono (IL2CPP for Windows needs a Windows computer); it needs Unity's «Windows Build Support (Mono)». Its icon
        /// carries the version (AppIcon, by PlayerSettings while it builds). Beside it, medir.bat (the PC's test, docs/
        /// PRUEBA-PC-3090.md), and Builds/Bastion-Windows.zip to hand it on, without Burst's folder that isn't to be shipped
        /// (*DoNotShip*).
        /// </summary>
        public static void Windows()
        {
            var exe = Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Bastion.exe");
            if (exe == null) return;
            var folder = Path.GetDirectoryName(Path.GetFullPath(exe))!;
            File.WriteAllText(Path.Combine(folder, "medir.bat"), MeasureBat.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            var parent = Path.GetDirectoryName(folder)!;
            var zip = Path.Combine(parent, "Bastion-Windows.zip");
            if (File.Exists(zip)) File.Delete(zip);
            // The version in the folder's name inside the zip (cuadro 162), «Bastion 0.14.15» (never with an accent: a name
            // with «ó» in a zip made here may come out garbled in Windows' own unzipping). Bastion.exe keeps its name (its
            // Bastion_Data beside it must match it), and the window's title is productName's (changing it moves the saves).
            var inside = Versioned();
            var named = Path.Combine(parent, inside);
            if (Directory.Exists(named)) Directory.Delete(named, true);
            Directory.Move(folder, named);
            try
            {
                if (!Ran("zip", $"-r -X -q \"{zip}\" \"{inside}\" -x \"*DoNotShip*\"", parent)) return;
            }
            finally
            {
                Directory.Move(named, folder);
            }
            Debug.Log($"[red] app de Windows con medir.bat · «{inside}» en el zip · {new FileInfo(zip).Length / (1024 * 1024)} MB → {zip}");
        }

        /// <summary>
        /// The PC's test, double-clicked (docs/PRUEBA-PC-3090.md): the app once per case of NetLink's BASTION_MEDIR, with
        /// the graphics altos and then medios, its data in «medidas» beside it (not the player's own: their save stays as
        /// it was). Plain ASCII: cmd reads it in the console's code page.
        /// </summary>
        private const string MeasureBat = @"@echo off
rem Bastion: mide el rendimiento por si solo (docs/PRUEBA-PC-3090.md). Abre el juego una vez por caso:
rem la ciudad de dia, la ciudad de noche, el asedio de la base y la base, con graficos altos y luego medios.
rem Son unos 15 minutos sin tocar nada. Los datos quedan en la carpeta medidas, junto a este archivo.
setlocal
cd /d ""%~dp0""
set ""BASTION_DATOS=%~dp0medidas""
set BASTION_ANFITRION=Medida
set BASTION_SEMILLA=7
for %%G in (altos medios) do (
  for %%C in (ciudad ciudad-noche asedio base) do (
    echo Midiendo %%C con graficos %%G...
    set BASTION_GRAFICOS=%%G
    set BASTION_MEDIR=%%C
    start """" /wait ""%~dp0Bastion.exe""
  )
)
echo.
echo Hecho. Manda el archivo medidas\red.log
pause
";

        /// <summary>
        /// Builds/Linux/Bastion.x86_64, with its Bastion_Data and the rest beside it: the folder goes whole (a .tar.gz keeps
        /// the executable's permission). Mono, as for Windows; it needs Unity's «Linux Build Support (Mono)».
        /// </summary>
        public static void Linux()
        {
            Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Bastion.x86_64");
        }

        /// <summary>The app's identifier on iOS, signed as it is by Arturo's personal team.</summary>
        private const string BundleId = "com.takeo7.bastion";

        /// <summary>
        /// iOS's question before the app may use the local network (NSLocalNetworkUsageDescription): the room's TCP, to
        /// join the partner's game or host it. Lore's words.
        /// </summary>
        private const string LocalNetworkUse = "Para jugar con otra persona en la misma red: entrar en su partida o abrir la tuya.";

        /// <summary>
        /// iOS (Arturo, 7/11; docs/IOS.md): the Xcode project for the simulator, Builds/iOS-sim. unity/tools/ios.sh brings
        /// the copy up to date, runs this, then xcodebuild, and installs it. Only in the iOS copy of the project, open with
        /// -buildTarget iOS: switching the main one would make every session import it all again.
        /// </summary>
        public static void IosSimulator()
        {
            Ios(false);
        }

        /// <summary>The same for the iPad and Arturo's Duo, Builds/iOS: signed automatically by the team -equipo &lt;id&gt; says (his personal one).</summary>
        public static void IosDevice()
        {
            Ios(true);
        }

        /// <summary>
        /// The Xcode project: IL2CPP (iOS has no JIT, so link.xml keeps the engine whole), iPhone and iPad, iOS 17 up, only
        /// landscape either way round (Gameplay: the HUD is laid out wide), full screen. Then into its Info.plist the name
        /// the player sees, the question for the local network, the iPhone above 60 Hz and the landscape alone, checked; and
        /// the icon with the version (AppIcon.Ios). As every build: the figures, the sound and the panels checked first
        /// (Build).
        /// </summary>
        private static void Ios(bool device)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                Debug.LogError($"[red] app (iOS): solo en la copia de iOS del proyecto, abierta con -buildTarget iOS (unity/tools/ios.sh); esta está en {EditorUserBuildSettings.activeBuildTarget}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-equipo");
            var team = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(ios, BundleId);
            PlayerSettings.iOS.buildNumber = Application.version;
            PlayerSettings.iOS.sdkVersion = device ? iOSSdkVersion.DeviceSDK : iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "17.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            var project = Build(BuildTarget.iOS, device ? "Builds/iOS" : "Builds/iOS-sim");
            if (project == null) return;
            project = Path.GetFullPath(project);
            if (!AppIcon.Ios(project)) return;
            var plist = Path.Combine(project, "Info.plist");
            var steps = new[]
            {
                ("plutil", $"-replace CFBundleDisplayName -string {Shown} \"{plist}\""),
                ("plutil", $"-replace NSLocalNetworkUsageDescription -string \"{LocalNetworkUse}\" \"{plist}\""),
                // Above 60 Hz on an iPhone's ProMotion screen (Arturo, 7/11: no cap of its own, the settings as on the Mac;
                // GameSettings asks for the screen's rate): Unity writes it false, and the iPhone then stops at 60.
                ("plutil", $"-replace CADisableMinimumFrameDurationOnPhone -bool YES \"{plist}\""),
            };
            foreach (var (tool, arguments) in steps)
                if (!Ran(tool, arguments)) return;
            // The turns Unity writes from PlayerSettings, checked below; an iPad's own list, if it wrote one, would let it turn upright.
            if (Said("plutil", $"-extract UISupportedInterfaceOrientations~ipad json -o - \"{plist}\"") != null && !Ran("plutil", $"-remove UISupportedInterfaceOrientations~ipad \"{plist}\"")) return;
            var turns = Said("plutil", $"-extract UISupportedInterfaceOrientations json -o - \"{plist}\"") ?? "";
            var name = Said("plutil", $"-extract CFBundleDisplayName raw -o - \"{plist}\"")?.Trim();
            var fast = Said("plutil", $"-extract CADisableMinimumFrameDurationOnPhone raw -o - \"{plist}\"")?.Trim();
            if (turns.Contains("Portrait") || !turns.Contains("LandscapeLeft") || !turns.Contains("LandscapeRight") || name != Shown || Said("plutil", $"-extract NSLocalNetworkUsageDescription raw -o - \"{plist}\"") == null || fast != "true")
            {
                Debug.LogError($"[red] app (iOS): el Info.plist no quedó como debe (nombre «{name}», giros {turns.Trim()}, más de 60 Hz {fast})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"[red] app (iOS, {(device ? $"aparato, equipo {(string.IsNullOrEmpty(team) ? "sin decir" : team)}" : "simulador")}): {BundleId} {Application.version}, «{Shown}», solo en horizontal, con el permiso de red local y hasta 120 Hz → {project}");
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
            if (args.Contains("-sinSonido")) Debug.Log($"[red] app ({target}): -sinSonido, sin comprobar el sonido");
            else if (Sound() is string missing)
            {
                Debug.LogError($"[red] app ({target}): no se compila, {missing}. unity/tools/sonido.sh lo genera (docs/UNITY-SONIDO.md); -sinSonido compila sin comprobarlo.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return null;
            }
            if (Figures() is string lacking)
            {
                Debug.LogError($"[red] app ({target}): no se compila, {lacking}.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return null;
            }
            var packed = args.Contains("-comprimir");
            // -desarrollo (Red, 24/11, Producto's yes): a development build, only to measure, never to hand on: the profiler
            // from the first frame (-profiler-enable -profiler-log-file <file> when it is opened), to split the start's
            // 2.17 s without a line. Its player is the development one, slower than the release's: what it gives is the
            // share of each part, not the release's times.
            var development = args.Contains("-desarrollo");
            if (development) Debug.Log($"[red] app ({target}): -desarrollo, compilación de desarrollo, solo para medir");
            var options = (packed ? BuildOptions.CompressWithLz4HC : BuildOptions.None) | (development ? BuildOptions.Development : BuildOptions.None);
            // The Windows icon goes in through PlayerSettings, put back as it was once built (the Mac's, after: Mac).
            Action? unicon = target == BuildTarget.StandaloneWindows64 ? AppIcon.Windows() : () => { };
            if (unicon == null) return null;
            // -sinPresentacion (Red, 24/11), only to measure: Unity's splash screen off for this build, put back as it was once
            // built (as the icon): whether the start's 2.2 s without a line are the splash's (the player idle, nothing loading).
            var splash = PlayerSettings.SplashScreen.show;
            var splashLogo = PlayerSettings.SplashScreen.showUnityLogo;
            var unsplashed = args.Contains("-sinPresentacion");
            if (unsplashed)
            {
                PlayerSettings.SplashScreen.show = false;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                Debug.Log($"[red] app ({target}): -sinPresentacion, sin la presentación de Unity (antes: {splash}, logo {splashLogo}), solo para medir");
            }
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = output, target = target, options = options });
            }
            finally
            {
                unicon();
                if (unsplashed)
                {
                    PlayerSettings.SplashScreen.show = splash;
                    PlayerSettings.SplashScreen.showUnityLogo = splashLogo;
                }
            }
            var summary = report.summary;
            // Left on another platform, everyone's next run would import the assets again for it (and capture on it).
            if (EditorUserBuildSettings.activeBuildTarget != before) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(before), before);
            Debug.Log($"[red] app ({target}): {summary.result} en {summary.totalTime.TotalSeconds:F0} s · {summary.totalSize / (1024 * 1024)} MB{(packed ? " (LZ4HC)" : " sin comprimir")}{(development ? " · de desarrollo" : "")} · {summary.totalErrors} errores · {string.Join(", ", scenes)} → {output}");
            if (summary.result == BuildResult.Succeeded) return output;
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return null;
        }

        /// <summary>
        /// What is missing of what the figures are made of, or null: every preset the game draws (UnitViews.PresetsUsed) in
        /// FigureCatalog, every weapon and piece on the weapon it draws (Anims.json's, the accessories, the sheath) there with
        /// its prefab, and every Sidekick part the wardrobe names (Wardrobe.json), with the base model, in a Resources
        /// folder (Sidekick's runtime loads them from there by name). Arte takes what isn't used out of the app (26/10).
        /// </summary>
        private static string? Figures()
        {
            var catalog = FigureCatalog.Load();
            if (catalog == null) return "no hay Resources/FigureCatalog.asset";
            var presets = UnitViews.PresetsUsed.Where((n) => catalog.Preset(n) == null).ToList();
            if (presets.Count > 0) return $"FigureCatalog no tiene {presets.Count} de los presets que se dibujan: {string.Join(", ", presets)}";
            // The weapons and what goes on them (Producto's review of the 0.14.4, cuadro 148; the catalog is Arte's): every
            // model Anims.json's weapons name, and the accessories and the sheath UnitViews.Arms draws, in the catalog with
            // its prefab. A prefab missing in the clean copy comes back null: a version without them, and nothing said.
            var arms = Anims.Weapons.Select((w) => w.fbx).Concat(new[] { UnitViews.SilencerModel, UnitViews.ScopeModel, UnitViews.ShortScopeModel, UnitViews.KnifeSheath })
                .Where((n) => !string.IsNullOrEmpty(n)).Distinct().ToList();
            var unarmed = arms.Where((n) => catalog.Weapon(n) == null).OrderBy((n) => n).ToList();
            if (unarmed.Count > 0) return $"FigureCatalog no tiene {unarmed.Count} de las {arms.Count} armas y piezas del arma que se dibujan, o falta su prefab: {string.Join(", ", unarmed.Take(12))}{(unarmed.Count > 12 ? "…" : "")}";
            var wardrobe = JToken.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Wardrobe.json"));
            var parts = new HashSet<string>(wardrobe.SelectTokens("..parts[*]").Select((t) => (string?)t ?? "")) { "SK_BaseModel" };
            var there = new HashSet<string>(AssetDatabase.FindAssets("t:Model", new[] { "Assets/Synty/SidekickCharacters/Resources" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(Path.GetFileNameWithoutExtension));
            var gone = parts.Where((p) => !there.Contains(p)).OrderBy((p) => p).ToList();
            if (gone.Count > 0) return $"faltan en Resources {gone.Count} piezas de Sidekick que nombra el armario: {string.Join(", ", gone.Take(12))}{(gone.Count > 12 ? "…" : "")}";
            // More there than the wardrobe names (Synty imported again without FigureCatalogMake.Run after it): the app is
            // right, but carries them, some 176 MB.
            var spare = there.Count((p) => !parts.Contains(p));
            if (spare > 0) Debug.LogWarning($"[red] figuras: {spare} piezas de Sidekick en Resources que no nombra el armario van en la app; FigureCatalogMake.Run las saca (Trim)");
            Debug.Log($"[red] figuras: {catalog.presetNames.Length} presets en el catálogo, los {UnitViews.PresetsUsed.Count()} que se dibujan; las {parts.Count} piezas del armario en Resources; las {arms.Count} armas y piezas del arma");
            return null;
        }

        /// <summary>Where unity/tools/sonido.sh leaves the sound, with its manifiesto.json.</summary>
        private const string SoundFolder = "Assets/Bastion/Client/Audio/Resources/Generado";

        /// <summary>
        /// What is wrong with the sound, or null: every file of Generado/manifiesto.json there with the hash it was written
        /// with (docs/UNITY-SONIDO.md §3). Without it the app would play silent, or a sound out of step with its manifest.
        /// </summary>
        private static string? Sound()
        {
            var manifest = Path.Combine(SoundFolder, "manifiesto.json");
            if (!File.Exists(manifest)) return $"no hay sonido ({manifest})";
            if (!(JObject.Parse(File.ReadAllText(manifest))["archivos"] is JObject files) || files.Count == 0) return $"{manifest} no dice qué archivos lleva";
            using var sha = SHA256.Create();
            foreach (var p in files)
            {
                var path = Path.Combine(SoundFolder, p.Key);
                if (!File.Exists(path)) return $"falta {path}";
                var hash = string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select((b) => b.ToString("x2")));
                if (hash != (string?)p.Value?["sha256"]) return $"{path} no es el que se generó (su hash no es el del manifiesto)";
            }
            Debug.Log($"[red] sonido: {files.Count} archivos, como se generaron");
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

        /// <summary>What a command of the Mac's wrote (UTF-8: Mono's default isn't, and «Bastión» came back «Basti??n»), or null if it failed (nothing fails for it).</summary>
        private static string? Said(string tool, string arguments)
        {
            var start = new ProcessStartInfo(tool, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Result : null;
        }

        /// <summary>A command of the Mac's, waited for (in `directory`, if said); if it fails, what it said goes to the log and the build fails.</summary>
        internal static bool Ran(string tool, string arguments, string? directory = null)
        {
            var start = new ProcessStartInfo(tool, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory ?? "" };
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
