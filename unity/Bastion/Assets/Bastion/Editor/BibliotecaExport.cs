using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bastion.Client;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Client.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// What the app carries of Synty's and Mixamo's, for the resource library (docs/BIBLIOTECA.md, its page and server by
    /// the Biblioteca session): unity/tools/biblioteca/usados.json, every asset of theirs that the build's scenes, the
    /// catalogs and Resources depend on, each with the disk's pack it comes from, its model and what pulls it in (a
    /// catalog's entry, the list that names it). Made again with each version:
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.BibliotecaExport.Run -quit
    /// </summary>
    public static class BibliotecaExport
    {
        /// <summary>From the repository's root.</summary>
        const string Out = "unity/tools/biblioteca/usados.json";

        /// <summary>
        /// Each folder of Assets/Synty by the pack on the external disk it comes from (its folder's name there); null for
        /// what is shared by many (PolygonGeneric, InterfaceCore: every pack brings its copy), ours (Extra: source models,
        /// some cut by Blender; Pieces: the buildings' pieces) or no art (the packs' tools): Extra's and Pieces' take the
        /// pack of the entry that pulls them in.
        /// </summary>
        static readonly Dictionary<string, string> Folders = new Dictionary<string, string>
        {
            ["AnimationBaseLocomotion"] = "ANIMATION - Base Locomotion", ["AnimationBowCombat"] = "ANIMATION - Bow Combat",
            ["AnimationEmotesAndTaunts"] = "ANIMATION - Emotes and Taunts", ["AnimationGoblinLocomotion"] = "ANIMATION - Goblin Locomotion",
            ["AnimationIdles"] = "ANIMATION - Idles", ["AnimationSwordCombat"] = "ANIMATION - Sword Combat",
            ["InterfaceApocalypseHUD"] = "INTERFACE - Apocalypse HUD", ["InterfaceDarkFantasyHUD"] = "INTERFACE - Dark Fantasy HUD",
            ["InterfaceMilitaryCombatHUD"] = "INTERFACE - Military Combat HUD", ["InterfaceModernMenus"] = "INTERFACE - Modern Menus",
            ["PolygonApocalypse"] = "POLYGON - Apocalypse Pack", ["PolygonBossZombies"] = "POLYGON - Boss Zombies",
            ["PolygonCity"] = "POLYGON - City Pack", ["PolygonCityZombies"] = "POLYGON - City Zombies Pack",
            ["PolygonConstruction"] = "POLYGON - Construction Pack", ["PolygonDog"] = "POLYGON - Dog Pack", ["PolygonFarm"] = "POLYGON - Farm Pack",
            ["PolygonGangWarfare"] = "POLYGON - Gang Warfare Pack", ["PolygonMapsMilitaryWarehouse"] = "POLYGON - Military Warehouse Map",
            ["PolygonMapsWoodlandApocalypse"] = "POLYGON - Woodland Apocalypse Map", ["PolygonMilitary"] = "POLYGON - Military Pack",
            ["PolygonNature"] = "POLYGON - Nature Pack", ["PolygonNatureBiomes"] = "POLYGON - Alpine Mountain - Nature Biome",
            ["PolygonOffice"] = "POLYGON - Office Pack", ["PolygonPalmCity"] = "POLYGON - Palm City", ["PolygonParticleFX"] = "POLYGON - Particle FX Pack",
            ["PolygonShops"] = "POLYGON - Shops Pack", ["PolygonTown"] = "POLYGON - Town Pack",
            ["SimpleCats"] = "SIMPLE - Cats", ["SimpleForestAnimal"] = "SIMPLE - Forest Animals",
            // (Its outfits and characters by their pack below; the species' parts, shaders and database came with Starter.)
            ["SidekickCharacters"] = "SIDEKICK - Starter",
            ["PolygonGeneric"] = null, ["InterfaceCore"] = null, ["Extra"] = null, ["Pieces"] = null, ["Tools"] = null, ["SyntyPackageHelper"] = null,
        };

        /// <summary>Mixamo's clips (assets-src/mixamo; Assets/Mixamo, by the game's id) go as a pack of their own.</summary>
        const string Mixamo = "Mixamo";

        /// <summary>A Sidekick pack's name on the disk by its folder in the project («ApocalypseOutlaws»: «SIDEKICK - Apocalypse Outlaws»).</summary>
        static string Sidekick(string folder) => folder == "HumanSpecies" || folder == "Humans" ? "SIDEKICK - Starter" : "SIDEKICK - " + Regex.Replace(folder, "(?<=[a-z])(?=[A-Z])", " ");

        /// <summary>The pack on the disk an asset of the project's comes from, or null (shared, ours, or not Synty's nor Mixamo's).</summary>
        static string PackOf(string path)
        {
            if (path.StartsWith("Assets/Mixamo/")) return Mixamo;
            if (!path.StartsWith("Assets/Synty/")) return null;
            var parts = path.Split('/');
            if (parts.Length < 3) return null;
            string folder = parts[2];
            if (folder == "SidekickCharacters" || path.StartsWith(UndeadFigure.Spare))
            {
                // Resources/Meshes/Outfits/<pack>/…, Meshes/Species/<species>/…, Characters/<pack>/…
                if (Array.IndexOf(parts, "Meshes") > 0) return PartPack(path);
                int at = Array.IndexOf(parts, "Characters");
                if (at > 0 && at + 2 < parts.Length) return Sidekick(parts[at + 1]);
            }
            return Folders.TryGetValue(folder, out var pack) ? pack : null;
        }

        /// <summary>
        /// A Sidekick part's pack on the disk by its model's path: …/Meshes/Outfits/<pack>/…, or a species' body (…/Species/
        /// Humans/…, which Starter and the tool bring; …/Species/Zombies/…, Apocalypse Zombies').
        /// </summary>
        static string PartPack(string path)
        {
            var parts = path.Split('/');
            int at = Array.IndexOf(parts, "Outfits");
            if (at >= 0 && at + 2 < parts.Length) return Sidekick(parts[at + 1]);
            at = Array.IndexOf(parts, "Species");
            if (at >= 0 && at + 2 < parts.Length && parts[at + 1] == "Zombies") return "SIDEKICK - Apocalypse Zombies";
            return "SIDEKICK - Starter";
        }

        /// <summary>The kind the library shows an asset as, by its file.</summary>
        static string KindOf(string path, string pack)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".prefab": return "prefab";
                case ".fbx": case ".obj":
                    return pack == Mixamo || (pack != null && pack.StartsWith("ANIMATION")) || path.Contains("/Animations/") ? "clip" : "modelo";
                case ".anim": return "clip";
                case ".png": case ".tga": case ".psd": case ".jpg": case ".jpeg": case ".tif": case ".tiff": case ".exr": return "textura";
                case ".mat": return "material";
                case ".wav": case ".ogg": case ".mp3": return "audio";
                case ".shader": case ".shadergraph": case ".shadersubgraph": case ".hlsl": case ".cginc": return "shader";
                default: return "otro";
            }
        }

        /// <summary>What pulls an asset in: the root (its index), in it the entry («models: apocalypse:SM_…»), the list that names it.</summary>
        struct From
        {
            public int root;
            public string at, list;
        }

        sealed class Used
        {
            public string path, pack, model, kind;
            public readonly List<From> from = new List<From>();
        }

        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var roots = new List<(string path, string kind)>();
            var used = new SortedDictionary<string, Used>(StringComparer.Ordinal);
            int unknown = 0;

            // An asset and all it depends on, pulled in by `from`; `pack`, the entry's own pack, for what is ours (Extra, Pieces).
            void Pull(UnityEngine.Object asset, From from, string pack)
            {
                if (asset == null) return;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path)) PullPath(path, from, pack);
            }
            void PullPath(string path, From from, string pack)
            {
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                {
                    if (!dep.StartsWith("Assets/Synty/") && !dep.StartsWith("Assets/Mixamo/")) continue;
                    if (dep.EndsWith(".cs") || dep.EndsWith(".dll") || dep.EndsWith(".asmdef")) continue;
                    if (!used.TryGetValue(dep, out var u))
                    {
                        string p = PackOf(dep);
                        string folder = dep.Split('/')[2];
                        if (p == null && (folder == "Extra" || folder == "Pieces")) p = pack;
                        if (p == null && !Folders.ContainsKey(folder)) unknown++;
                        used[dep] = u = new Used { path = dep, pack = p, model = Path.GetFileNameWithoutExtension(dep) };
                        u.kind = KindOf(dep, p);
                    }
                    else if (u.pack == null && pack != null && (dep.StartsWith("Assets/Synty/Extra/") || dep.StartsWith("Assets/Synty/Pieces/"))) u.pack = pack;
                    if (!u.from.Any(f => f.root == from.root && f.at == from.at)) u.from.Add(from);
                }
            }
            int Root(string path, string kind)
            {
                roots.Add((path, kind));
                return roots.Count - 1;
            }

            // The ids MapArt.json, Pieces.json and Anims.json give their packs.
            var ids = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in MapLab.PackFolders) ids[kv.Key] = Folders.TryGetValue(kv.Value, out var p) ? p : null;
            foreach (var kv in FigureCatalogMake.Packs) ids[kv.Key] = PackOf(kv.Value + "/x");
            ids["mixamo"] = Mixamo;
            string PackOfId(string key)
            {
                int colon = key == null ? -1 : key.IndexOf(':');
                return colon > 0 && ids.TryGetValue(key.Substring(0, colon), out var p) ? p : null;
            }

            // The lists that name things, by what they name.
            var mapArt = new HashSet<string>(MapArt.Models());
            var laLata = new HashSet<string>(LaLata.Models);
            var fauna = new HashSet<string>(Fauna.Models);
            var wardrobe = new HashSet<string>(JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Wardrobe.json"))["pieces"].SelectMany(p => p["parts"].Select(n => (string)n)));
            var undeadParts = new HashSet<string>(JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Undead.json")).Properties().SelectMany(p => p.Value["parts"].Select(n => (string)n[0])));
            var pieces = (JObject)JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Pieces.json"))["pieces"];
            var undead = UndeadFigure.Bodies.ToDictionary(b => b.id, b => b.preset);
            // A Sidekick preset's pack, by its folder under Characters.
            var presetPacks = Directory.GetDirectories("Assets/Synty/SidekickCharacters/Characters").SelectMany(d => Directory.GetDirectories(d).Select(c => (Path.GetFileName(c), Sidekick(Path.GetFileName(d)))))
                .GroupBy(c => c.Item1).ToDictionary(g => g.Key, g => g.First().Item2);
            // (The sky's cloud rings go in the map's catalog by an id of their own: their key is their model's.)
            var sky = MapCatalogMake.SkyModels.ToDictionary(m => m.id, m => m.key);
            string ModelList(string key) => mapArt.Contains(key) ? "MapArt" : laLata.Contains(key) ? "La Lata" : fauna.Contains(key) ? "Fauna"
                : key == SnowCover.Mound ? "Nieve" : sky.ContainsKey(key) ? "Cielo" : null;
            string ModelPack(string key) => PackOfId(sky.TryGetValue(key, out var model) ? model : key);

            // The build's scenes.
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
                PullPath(scene.path, new From { root = Root(scene.path, "escena") }, null);

            // The catalogs, entry by entry.
            const string Res = "Assets/Bastion/Client/Resources/";
            var map = AssetDatabase.LoadAssetAtPath<MapCatalog>(Res + "MapCatalog.asset");
            if (map != null)
            {
                int r = Root(Res + "MapCatalog.asset", "catalogo");
                for (int k = 0; k < map.models.Length && k < map.modelNames.Length; k++)
                    Pull(map.models[k], new From { root = r, at = "models: " + map.modelNames[k], list = ModelList(map.modelNames[k]) }, ModelPack(map.modelNames[k]));
                for (int k = 0; k < map.materials.Length && k < map.materialNames.Length; k++)
                    Pull(map.materials[k], new From { root = r, at = "materials: " + map.materialNames[k], list = "Mapa" }, null);
            }
            var figures = AssetDatabase.LoadAssetAtPath<FigureCatalog>(Res + "FigureCatalog.asset");
            if (figures != null)
            {
                int r = Root(Res + "FigureCatalog.asset", "catalogo");
                for (int k = 0; k < figures.presets.Length && k < figures.presetNames.Length; k++)
                {
                    string n = figures.presetNames[k];
                    string preset = undead.TryGetValue(n, out var living) ? living : n;
                    Pull(figures.presets[k], new From { root = r, at = "presets: " + n, list = "Undead" }, presetPacks.TryGetValue(preset, out var p) ? p : null);
                }
                for (int k = 0; k < figures.colourMaps.Length && k < figures.colourMapNames.Length; k++)
                    Pull(figures.colourMaps[k], new From { root = r, at = "colourMaps: " + figures.colourMapNames[k], list = "Undead" }, null);
                for (int k = 0; k < figures.clips.Length && k < figures.clipIds.Length; k++)
                    Pull(figures.clips[k], new From { root = r, at = "clips: " + figures.clipIds[k], list = "Anims" }, null);
                for (int k = 0; k < figures.weapons.Length && k < figures.weaponNames.Length; k++)
                    Pull(figures.weapons[k], new From { root = r, at = "weapons: " + figures.weaponNames[k], list = "Armas" }, PackOfId(figures.weaponNames[k]));
                for (int k = 0; k < figures.characters.Length && k < figures.characterNames.Length; k++)
                    Pull(figures.characters[k], new From { root = r, at = "characters: " + figures.characterNames[k], list = "Muertos especiales" }, null);
                for (int k = 0; k < figures.atlases.Length && k < figures.atlasNames.Length; k++)
                    Pull(figures.atlases[k], new From { root = r, at = "atlases: " + figures.atlasNames[k], list = "Muertos especiales" }, null);
            }
            var pieceCatalog = AssetDatabase.LoadAssetAtPath<PieceCatalog>(Res + "PieceCatalog.asset");
            if (pieceCatalog != null)
            {
                int r = Root(Res + "PieceCatalog.asset", "catalogo");
                foreach (var e in pieceCatalog.pieces.Where(e => e != null))
                {
                    string pack = pieces[e.id] is JObject piece && ids.TryGetValue((string)piece["pack"] ?? "", out var p) ? p : null;
                    Pull(e.model, new From { root = r, at = "pieces: " + e.id, list = "Piezas" }, pack);
                    foreach (var m in e.materials) Pull(m, new From { root = r, at = "pieces: " + e.id, list = "Piezas" }, pack);
                }
                Pull(pieceCatalog.glass, new From { root = r, at = "glass", list = "Piezas" }, null);
                Pull(pieceCatalog.floors, new From { root = r, at = "floors", list = "Piezas" }, null);
                Pull(pieceCatalog.roofs, new From { root = r, at = "roofs", list = "Piezas" }, null);
            }
            var fx = AssetDatabase.LoadAssetAtPath<FxArt>(Res + "FxArt.asset");
            if (fx != null)
            {
                int r = Root(Res + "FxArt.asset", "catalogo");
                foreach (var field in typeof(FxArt).GetFields().Where(f => typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)))
                    Pull(field.GetValue(fx) as UnityEngine.Object, new From { root = r, at = field.Name, list = "Fx" }, null);
            }
            var catalogs = new HashSet<string>(roots.Select(x => x.path));

            // Everything else in a Resources folder: the app carries it whether or not anyone loads it.
            foreach (string folder in Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories).Select(d => d.Replace('\\', '/')).OrderBy(d => d, StringComparer.Ordinal))
            {
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal))
                {
                    if (file.EndsWith(".meta") || Path.GetFileName(file).StartsWith(".") || catalogs.Contains(file)) continue;
                    // (A Resources folder inside another one is gone through with it.)
                    if (roots.Any(x => x.path == file)) continue;
                    string name = Path.GetFileNameWithoutExtension(file);
                    string list = wardrobe.Contains(name) ? "Armario" : undeadParts.Contains(name) ? "Undead" : null;
                    PullPath(file, new From { root = Root(file, "resources"), list = list }, null);
                }
            }

            // Written by hand, an entry a line (git's diffs then read by entry).
            var text = new StringBuilder();
            string J(object o) => JsonConvert.SerializeObject(o);
            text.Append("{\n");
            text.Append($" \"formato\": 1,\n \"hecho\": {J(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"))},\n \"version\": {J(PlayerSettings.bundleVersion)},\n");
            text.Append(" \"carpetas\": {\n");
            text.Append(string.Join(",\n", Folders.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"  {J(kv.Key)}: {J(kv.Value)}")));
            text.Append("\n },\n \"ids\": {\n");
            text.Append(string.Join(",\n", ids.Select(kv => $"  {J(kv.Key)}: {J(kv.Value)}")));
            text.Append("\n },\n \"raices\": [\n");
            text.Append(string.Join(",\n", roots.Select(x => "  " + J(new JObject { ["ruta"] = x.path, ["tipo"] = x.kind }))));
            text.Append("\n ],\n \"usados\": [\n");
            text.Append(string.Join(",\n", used.Values.Select(u =>
            {
                var o = new JObject { ["ruta"] = u.path, ["paquete"] = u.pack, ["modelo"] = u.model, ["clave"] = u.pack != null ? $"{u.pack}:{u.model}" : null, ["tipo"] = u.kind };
                o["desde"] = new JArray(u.from.Select(f =>
                {
                    var d = new JObject { ["raiz"] = f.root };
                    if (f.at != null) d["en"] = f.at;
                    if (f.list != null) d["lista"] = f.list;
                    return d;
                }));
                return "  " + o.ToString(Formatting.None);
            })));
            text.Append("\n ],\n");
            var sidekick = SidekickSection(root);
            text.Append($" \"sidekick\": {{\n  \"carpeta\": {J(Pictures)},\n");
            foreach (string list in new[] { "piezas", "presets", "hechos" })
            {
                text.Append($"  {J(list)}: [\n");
                text.Append(string.Join(",\n", ((JArray)sidekick[list]).Select(o => "   " + o.ToString(Formatting.None))));
                text.Append(list == "hechos" ? "\n  ]\n" : "\n  ],\n");
            }
            text.Append(" }\n}\n");
            string target = Path.Combine(root, Out);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, text.ToString());

            var byPack = used.Values.GroupBy(u => u.pack ?? "(compartido o nuestro)").OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}");
            Debug.Log($"[biblioteca] {Out}: {used.Count} assets from {roots.Count} roots ({roots.Count(x => x.kind == "escena")} scenes, {roots.Count(x => x.kind == "catalogo")} catalogs), {unknown} in folders not in the table; by pack: {string.Join(", ", byPack)}");
        }

        /// <summary>Where the pictures of Sidekick go, from the repository's root: off git, in the library's cache (as Synty's files).</summary>
        const string Pictures = "assets-src/biblioteca/unity/sidekick";
        const string Characters = "Assets/Synty/SidekickCharacters/Characters";
        const int Shot = 512;

        /// <summary>
        /// Sidekick's parts (every model of its packs in the project: in Resources, the ones the wardrobe names; the rest in
        /// UndeadFigure.Spare, FigureCatalogMake.Trim's), its presets (each .sk with its prefab) and the game's own made of them
        /// (UndeadFigure.Bodies, baked by FigureCatalogMake), each with a picture made here, as there are no source files to
        /// draw them from: a part on the colours of a preset that wears it (else of its pack's first), at the studio's light.
        /// The pictures already there are kept (BASTION_REHACER=1 makes them again; BASTION_MAX=n, only the first n parts).
        /// </summary>
        static JObject SidekickSection(string root)
        {
            bool again = Environment.GetEnvironmentVariable("BASTION_REHACER") == "1";
            string folder = Path.Combine(root, Pictures);
            Directory.CreateDirectory(Path.Combine(folder, "piezas"));
            Directory.CreateDirectory(Path.Combine(folder, "presets"));
            Directory.CreateDirectory(Path.Combine(folder, "hechos"));

            // The presets, off their .sk (as FigureCatalogMake.Undead): each part and its slot.
            var presets = new List<(string name, string pack, string dir, List<(string part, string slot)> parts)>();
            foreach (string sk in Directory.GetFiles(Characters, "*.sk", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
            {
                string dir = Path.GetDirectoryName(sk).Replace('\\', '/');
                var parts = new List<(string, string)>();
                string name = null;
                foreach (string raw in File.ReadLines(sk))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("- Name: ")) name = line.Substring(8).Trim();
                    else if (line.StartsWith("PartType: ") && name != null)
                    {
                        parts.Add((name, line.Substring(10).Trim()));
                        name = null;
                    }
                }
                presets.Add((Path.GetFileNameWithoutExtension(sk), Sidekick(Path.GetFileName(Path.GetDirectoryName(dir))), dir, parts));
            }
            Texture2D ColourMap((string name, string pack, string dir, List<(string, string)> parts) p) =>
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{p.dir}/Textures/T_{p.name}ColorMap.png");
            var wearers = presets.SelectMany(p => p.parts.Select(x => (x.part, x.slot, preset: p))).GroupBy(x => x.part).ToDictionary(g => g.Key, g => g.ToList());
            // A part's slot by the code in its name (SK_<line>_<kind>_<nn>_<nn><slot>_<species>: «02HAIR»), as the presets give them.
            string Code(string part)
            {
                var tokens = part.Split('_');
                return tokens.Length >= 6 ? tokens[4] : null;
            }
            var slots = wearers.Values.SelectMany(w => w).Where(x => Code(x.part) != null).GroupBy(x => Code(x.part)).ToDictionary(g => g.Key, g => g.First().slot);
            var wardrobe = JObject.Parse(File.ReadAllText("Assets/Bastion/Client/Resources/Wardrobe.json"))["pieces"]
                .SelectMany(p => p["parts"].Select(n => (part: (string)n, id: (string)p["id"]))).GroupBy(x => x.part).ToDictionary(g => g.Key, g => g.Select(x => x.id).Distinct().ToList());

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PortraitLab.EnsureTone();
            var material = Resources.Load<Material>("Materials/M_BaseMaterial") ?? throw new InvalidOperationException("no Sidekick's M_BaseMaterial in Resources");
            int made = 0, kept = 0, failed = 0;
            bool first = true;
            // A model's picture, unless it is there already: its file's name in the folder (null if nothing could be drawn).
            string Picture(string list, string name, Func<GameObject> make)
            {
                string file = $"{list}/{name}.png";
                string path = Path.Combine(folder, file);
                if (!again && File.Exists(path))
                {
                    kept++;
                    return file;
                }
                GameObject model = null;
                try
                {
                    model = make();
                    byte[] png = model != null ? Draw(model, list != "piezas") : null;
                    // (The first a batchmode run draws may come out without its light: drawn again.)
                    if (png != null && first)
                    {
                        first = false;
                        png = Draw(model, list != "piezas");
                    }
                    if (png == null)
                    {
                        failed++;
                        return null;
                    }
                    File.WriteAllBytes(path, png);
                    made++;
                    return file;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[biblioteca] {list}/{name}: {e.Message}");
                    failed++;
                    return null;
                }
                finally
                {
                    if (model != null) UnityEngine.Object.DestroyImmediate(model);
                }
            }

            var pieces = new JArray();
            var partPaths = new[] { UndeadFigure.Meshes, UndeadFigure.Spare }.Select(f => f.TrimEnd('/')).Where(AssetDatabase.IsValidFolder)
                .SelectMany(f => AssetDatabase.FindAssets("t:Model", new[] { f }).Select(AssetDatabase.GUIDToAssetPath))
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(p) != "SK_BaseModel")
                .Distinct().OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
            // (To try the pictures on a few: BASTION_MAX=n, the first n parts alone.)
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_MAX"), out int max)) partPaths = partPaths.Take(max).ToList();
            foreach (string path in partPaths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string pack = PartPack(path);
                var on = wearers.TryGetValue(name, out var w) ? w : null;
                string code = Code(name);
                string image = Picture("piezas", name, () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) return null;
                    var model = UnityEngine.Object.Instantiate(asset);
                    var colours = on != null ? ColourMap(on[0].preset) : presets.Where(p => p.pack == pack).Select(ColourMap).FirstOrDefault(t => t != null);
                    var own = new Material(material);
                    if (colours != null) own.SetTexture("_ColorMap", colours);
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(_ => own).ToArray();
                    return model;
                });
                var o = new JObject
                {
                    ["parte"] = name, ["paquete"] = pack, ["codigo"] = code, ["hueco"] = on?[0].slot ?? (code != null && slots.TryGetValue(code, out var slot) ? slot : null),
                    ["app"] = path.StartsWith(UndeadFigure.Meshes), ["presets"] = on == null ? new JArray() : new JArray(on.Select(x => x.preset.name).Distinct()),
                    ["armario"] = new JArray(wardrobe.TryGetValue(name, out var ids) ? ids : new List<string>()), ["imagen"] = image,
                };
                pieces.Add(o);
            }

            var presetList = new JArray();
            foreach (var p in presets)
            {
                string prefab = $"{p.dir}/{p.name}.prefab";
                string image = Picture("presets", p.name, () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                    return asset != null ? UnityEngine.Object.Instantiate(asset) : null;
                });
                presetList.Add(new JObject
                {
                    ["nombre"] = p.name, ["paquete"] = p.pack, ["partes"] = new JArray(p.parts.Select(x => new JArray(x.part, x.slot))),
                    ["juego"] = UnitViews.PresetsUsed.Contains(p.name), ["imagen"] = image,
                });
            }

            // The game's own: the living made dead (Extra/Undead, baked by FigureCatalogMake.Run).
            var undead = new JArray();
            foreach (var (id, preset, female) in UndeadFigure.Bodies)
            {
                string prefab = $"Assets/Synty/Extra/Undead/{id}.prefab";
                string image = Picture("hechos", id, () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                    return asset != null ? UnityEngine.Object.Instantiate(asset) : null;
                });
                var of = presets.FirstOrDefault(p => p.name == preset);
                undead.Add(new JObject { ["nombre"] = id, ["preset"] = preset, ["paquete"] = of.pack, ["mujer"] = female, ["imagen"] = image });
            }
            Debug.Log($"[biblioteca] Sidekick: {pieces.Count} parts, {presetList.Count} presets, {undead.Count} of the game's; pictures {made} made, {kept} kept, {failed} not drawn, in {Pictures}");
            return new JObject { ["piezas"] = pieces, ["presets"] = presetList, ["hechos"] = undead };
        }

        /// <summary>
        /// A model alone at the studio's light (PiecePicturesMake's, for a model), framed from the front, a little from
        /// its left and above (a whole figure lower and straighter), cut square round it: a 256 px PNG, or null.
        /// </summary>
        static byte[] Draw(GameObject model, bool whole)
        {
            model.SetActive(true);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Studio.Layer;
            var renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            if (renderers.Count == 0) return null;
            var box = renderers[0].bounds;
            foreach (var r in renderers) box.Encapsulate(r.bounds);
            if (box.size.sqrMagnitude < 1e-8f) return null;
            // (Sidekick's figures face +z.)
            var back = (whole ? new Vector3(0.35f, 0.15f, 1) : new Vector3(0.55f, 0.35f, 1)).normalized;
            const float fov = 22;
            var camera = new GameObject("Cámara de la biblioteca").AddComponent<Camera>();
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50;
            camera.aspect = 1;
            PiecePicturesMake.Frame(camera, box, back, fov, 0.04f);
            var target = new RenderTexture(Shot, Shot, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            Studio.Shoot(camera, target, new[] { new Studio.Lamp(0xfff0dc, 3.2f, new Vector3(-0.8f, 1.8f, 1.4f)), new Studio.Lamp(0xb4ccff, 1.2f, new Vector3(1.5f, 1, -1.2f)) },
                Studio.Hex(0xe2eaff), Studio.Hex(0x4a4036), 2.1f, Studio.Tone.Neutral, 1);
            var picture = PiecePicturesMake.Read(target);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            texture.SetPixels(PiecePicturesMake.Filled(picture, Shot, 0.04f));
            texture.Apply();
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }
    }
}
