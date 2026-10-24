using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The map from a recorded state (engine-cs/golden/step1/*.json, the engine's own states of nine missions), as setups
    /// for Shots: built by MapBuilder under the camera of 0.10.1's capture view (over the middle, 40 tiles off). BASTION_MAP
    /// names the state (city-7 unless said).
    /// </summary>
    public static class MapLab
    {
        public static void Day() => Build(true);

        public static void Night() => Build(false);

        /// <summary>A recorded state by its name (engine-cs/golden/step1/&lt;name&gt;.json).</summary>
        internal static GameState Golden(string name)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../../../engine-cs/golden/step1/{name}.json"));
            var file = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            return Bastion.Net.Json.Read<GameState>(file["state"].ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>The game's light (Bastion.Client.Mood), by day or by night.</summary>
        internal static void Lighting(bool day) => Bastion.Client.Mood.Show(day ? Bastion.Client.Mood.Day : Bastion.Client.Mood.Night);

        /// <summary>The map of `state` in a MapView of its own ("Mapa").</summary>
        internal static MapView Map(GameState state, string name)
        {
            var view = new GameObject("Mapa").AddComponent<MapView>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var builder = view.Build(state, Prefabs(), Materials());
            Debug.Log($"[map] {name}: {state.Width}×{state.Height}, {builder.Placed} props and {builder.Walls?.Laid} wall pieces in {clock.ElapsedMilliseconds} ms; missing: {string.Join(", ", builder.Missing.Concat(builder.Walls?.Missing ?? Enumerable.Empty<string>()).OrderBy(m => m))}");
            return view;
        }

        static void Build(bool day)
        {
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            var state = Golden(name);
            Lighting(day);
            var view = Map(state, name);
            var root = view.transform;
            // BASTION_LEVEL: the storey in view (UI's controller decides it in the game).
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_LEVEL"), out int level)) view.ShowLevel(level);
            // The cutaway's focus (UI's controller sets it in the game): BASTION_SELECT=1 the squad's first soldier selected,
            // BASTION_HOVER=x,y,z the cursor there, BASTION_ROOFS=1 the roofs on (the camera drawn back).
            var cutaway = view.GetComponent<Cutaway>();
            if (cutaway != null)
            {
                if (Environment.GetEnvironmentVariable("BASTION_SELECT") == "1") cutaway.Focus.selected = state.Units.FirstOrDefault(u => u.Alive && u.Team == "xcom")?.Id;
                var hover = Environment.GetEnvironmentVariable("BASTION_HOVER")?.Split(',');
                if (hover != null && hover.Length >= 2)
                {
                    // The cursor's storey the active one, as the controller has it.
                    cutaway.Focus.hover = new Pos { X = int.Parse(hover[0]), Y = int.Parse(hover[1]), Z = hover.Length > 2 ? int.Parse(hover[2]) : 0 };
                    cutaway.Focus.level = cutaway.Focus.hover.Z;
                }
                cutaway.Focus.roofs = Environment.GetEnvironmentVariable("BASTION_ROOFS") == "1";
                Debug.Log($"[map] cutaway focus: selected {cutaway.Focus.selected ?? "-"}, hover {(cutaway.Focus.hover == null ? "-" : $"{cutaway.Focus.hover.X},{cutaway.Focus.hover.Y},{cutaway.Focus.hover.Z}")}");
            }
            // BASTION_HIDE=glass,signs,shutters: those left out of the picture (to tell what draws what).
            var hide = (Environment.GetEnvironmentVariable("BASTION_HIDE") ?? "").Split(',');
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                bool glass = r.sharedMaterial != null && r.sharedMaterial.name == "Cristal";
                bool sign = r.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("sign") || t.name == "motelSign");
                bool shutter = r.GetComponentsInParent<Transform>().Any(t => t.name == "rollerShutter");
                if ((glass && hide.Contains("glass")) || (sign && hide.Contains("signs")) || (shutter && hide.Contains("shutters"))) r.enabled = false;
            }
            // Lights brought in with the models (none should be: the map's light is the scene's).
            var lights = root.GetComponentsInChildren<UnityEngine.Light>(true);
            if (lights.Length > 0) Debug.LogWarning("[map] lights in the map: " + string.Join("; ", lights.GroupBy(l => $"{l.transform.parent?.name}/{l.name} {l.type} {l.color}").Select(g => $"{g.Key} ×{g.Count()}")));
            // The cars' models and materials, to check their paints.
            var cars = root.GetComponentsInChildren<Transform>().Where(t => t.parent != null && t.parent.name == "Decorado" && t.name.StartsWith("car"));
            Debug.Log("[map] cars: " + string.Join("; ", cars.GroupBy(c => c.name + " " + string.Join("+", c.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m != null ? m.name : "-").Distinct())).Select(g => $"{g.Key} ×{g.Count()}")));

            // 0.10.1's capture view (reference.ts VIEWS.capture): over the middle, 40 tiles off, the camera's first turn.
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            var target = new Vector3(state.Width * MapSpace.Tile / 2, 0, -state.Height * MapSpace.Tile / 2);
            MapSpace.Aim(camera, target, 40);
            // Its background the haze, the fill light along it.
            Lighting(day);
            // BASTION_CHANGE=0|1: the map as a mission changes it (a door opened, a car's tile cleared, an upper floor's tile
            // given way), the camera on the door; with 1 the changes made (MapView.Rebuild), with 0 not, to compare.
            string change = Environment.GetEnvironmentVariable("BASTION_CHANGE");
            if (change != null)
            {
                int door = state.Tiles.FindIndex(t => t == "door");
                var next = Golden(name);
                var car = next.Decor?.FindIndex(d => Bastion.Engine.Decor.DecorOf(d)?.Kind == "carFront") ?? -1;
                if (door >= 0) next.Tiles[door] = "doorOpen";
                if (car >= 0) next.Tiles[car] = "floor";
                int broken = next.Levels != null && next.Levels.Count > 0 ? next.Levels[0].Tiles.FindIndex(t => t == "floor") : -1;
                if (broken >= 0) next.Levels[0].Tiles[broken] = null;
                if (change == "1")
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    int redone = view.Rebuild(next);
                    Debug.Log($"[map] changes: door {door}, car {car}, floor {broken}: {redone} tiles made again in {watch.ElapsedMilliseconds} ms");
                }
                if (door >= 0) MapSpace.Aim(camera, MapSpace.World(door % state.Width, door / state.Width), 12);
            }
            // BASTION_SQUAD=1: the game's camera on the squad, 24 tiles off.
            if (Environment.GetEnvironmentVariable("BASTION_SQUAD") == "1")
            {
                var squad = state.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => MapSpace.TileToWorld(state, u.Pos)).ToList();
                if (squad.Count > 0) MapSpace.Aim(camera, squad.Aggregate(Vector3.zero, (a, b) => a + b) / squad.Count, 24);
            }
            // BASTION_FOG=1: the fog of war as the squad sees from where it stands in the state.
            if (Environment.GetEnvironmentVariable("BASTION_FOG") == "1")
            {
                var fog = new GameObject("Niebla").AddComponent<Fog>();
                fog.Reset(state);
                fog.Show(state, true);
                // BASTION_FOG=all: everything in sight (0.10.1's reference block); the lit tiles and the night as they are.
                if (Environment.GetEnvironmentVariable("BASTION_FOG_ALL") == "1") fog.SeeAll();
                var squad = state.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => $"{u.Id} ({u.Pos.X},{u.Pos.Y},{u.Pos.Z})");
                Debug.Log($"[map] fog: {fog.Counts().sight} tiles in sight, {fog.Counts().known} seen of {state.Width * state.Height}; squad {string.Join(", ", squad)}");
            }
            // BASTION_AT=x,y,distance[,storey]: on tile (x, y) (on that storey's floor), so many tiles off (reference.ts:
            // rig.focus on the tile's middle).
            var at = Environment.GetEnvironmentVariable("BASTION_AT")?.Split(',');
            if (at != null && at.Length >= 3)
                MapSpace.Aim(camera, MapSpace.World(int.Parse(at[0]), int.Parse(at[1]), at.Length > 3 ? int.Parse(at[3]) : 0), float.Parse(at[2], System.Globalization.CultureInfo.InvariantCulture));
            // BASTION_SHADOWTEST=x,y: a cube 2 m over tile (x, y), to see whose shadow falls where.
            var shadowTest = Environment.GetEnvironmentVariable("BASTION_SHADOWTEST")?.Split(',');
            if (shadowTest != null && shadowTest.Length >= 2)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = MapSpace.World(int.Parse(shadowTest[0]), int.Parse(shadowTest[1])) + Vector3.up * 2.5f;
                cube.transform.localScale = Vector3.one * 0.8f;
            }
            // BASTION_NAMED=rug: every model whose name starts so, its box in the world.
            string named = Environment.GetEnvironmentVariable("BASTION_NAMED");
            if (named != null)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(named) && t.parent != null && t.parent.name == "Decorado"))
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    var box = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds(t.position, Vector3.zero);
                    Debug.Log($"[map] {t.name} at tile {MapSpace.WorldToTile(box.center)}: y {box.min.y:0.###}–{box.max.y:0.###} m, active {t.gameObject.activeInHierarchy}, {rs.Length} renderers");
                }
            // BASTION_CARS=1: every car the map has, its facing and which way its model's long side lies.
            if (Environment.GetEnvironmentVariable("BASTION_CARS") == "1" && state.Decor != null)
                foreach (Transform car in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("car") && t.parent != null && t.parent.name == "Decorado"))
                {
                    var box = car.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    var tile = MapSpace.WorldToTile(box.center);
                    var thing = Bastion.Engine.Decor.ObjectAt(state.Decor, state.Width, tile.y * state.Width + tile.x);
                    Debug.Log($"[map] car {car.name} at {tile.x},{tile.y}: facing {thing?.Facing.ToString() ?? "?"} ({thing?.Tiles.Count ?? 0} tiles), long side {(box.size.x > box.size.z ? "east–west" : "north–south")}");
                }
            // BASTION_THING=i: what the engine makes of the object over ground tile i (its tiles, anchor, facing).
            if (int.TryParse(Environment.GetEnvironmentVariable("BASTION_THING"), out int thingAt) && state.Decor != null)
            {
                var thing = Bastion.Engine.Decor.ObjectAt(state.Decor, state.Width, thingAt);
                Debug.Log(thing == null ? $"[map] no object at {thingAt}" : $"[map] object at {thingAt}: {thing.Kind} facing {thing.Facing}, anchor {thing.Anchor}, tiles {string.Join(" ", thing.Tiles)}");
                // What stands there: each model's box in the world (its long side along x is east–west).
                var here = MapSpace.World(thingAt % state.Width, thingAt / state.Width);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (Vector3.Distance(new Vector3(r.bounds.center.x, 0, r.bounds.center.z), new Vector3(here.x, 0, here.z)) < 3 * MapSpace.Tile && r.transform.root == root.root)
                        Debug.Log($"[map]   {r.transform.parent?.name}/{r.name}: centre {r.bounds.center}, size {r.bounds.size}");
            }
            // BASTION_FIND=<decor kind>: close on the first tile of the ground's decor of that kind, 10 tiles off.
            string find = Environment.GetEnvironmentVariable("BASTION_FIND");
            if (find != null && state.Decor != null)
            {
                int i = state.Decor.FindIndex(d => Bastion.Engine.Decor.DecorOf(d)?.Kind == find);
                if (i < 0) Debug.LogWarning($"[map] no {find} on the ground");
                else
                {
                    MapSpace.Aim(camera, MapSpace.World(i % state.Width, i / state.Width), 10);
                    Debug.Log($"[map] {find} at ({i % state.Width},{i / state.Width})");
                }
            }
            // BASTION_TOP=1: straight down on the whole map, north up (to lay it over 0.10.1's own).
            if (Environment.GetEnvironmentVariable("BASTION_TOP") == "1")
            {
                camera.orthographic = true;
                camera.orthographicSize = state.Height * MapSpace.Tile / 2;
                camera.transform.SetPositionAndRotation(target + Vector3.up * 200, Quaternion.Euler(90, 0, 0));
            }
        }

        /// <summary>A Synty material by its name, the first time it is asked for.</summary>
        internal static Func<string, Material> Materials()
        {
            var loaded = new Dictionary<string, Material>();
            return name =>
            {
                if (loaded.TryGetValue(name, out var m)) return m;
                string guid = AssetDatabase.FindAssets($"{name} t:Material", new[] { "Assets/Synty" })
                    .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
                return loaded[name] = guid != null ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)) : null;
            };
        }

        /// <summary>
        /// 0.10.1's models (from the packs' source FBX) that Unity's packs make otherwise: the street pot with its plant is
        /// SM_Prop_Pot_Street_01's prefab (the plant a child of it); the clinic's gurney and bed, rigged in the source, are
        /// still meshes here.
        /// </summary>
        static readonly Dictionary<string, string> UnityNames = new Dictionary<string, string>
        {
            ["SM_Prop_Pot_Street_Plant_01"] = "SM_Prop_Pot_Street_01",
            ["SK_Prop_Bed_Gurney_01"] = "SM_Prop_Bed_Gurney_01",
            ["SK_Prop_Bed_Hospital_01"] = "SM_Prop_Bed_Hospital_01",
            // The caravan (the Bastion's first stage: basePieces.ts), rigged in the source for its door.
            ["SK_Veh_Caravan_01"] = "SM_Veh_Caravan_01",
        };

        /// <summary>
        /// The quarter turns a model stands from its source FBX, by where it comes from (MapArt.UnityTurn): Apocalypse's
        /// Unity package, one; any other package's, or a source FBX in Extra (as it is, or cut by Blender), two.
        /// </summary>
        internal static int UnityTurnOf(string path) => path.StartsWith("Assets/Synty/PolygonApocalypse/") ? 1 : 2;

        /// <summary>The packs' folders in Assets/Synty, by MapArt.json's pack names.</summary>
        static readonly Dictionary<string, string> PackFolders = new Dictionary<string, string>
        {
            ["apocalypse"] = "PolygonApocalypse", ["city"] = "PolygonCity", ["town"] = "PolygonTown", ["office"] = "PolygonOffice",
            ["military"] = "PolygonMilitary", ["construction"] = "PolygonConstruction", ["farm"] = "PolygonFarm", ["nature"] = "PolygonNature",
            ["alpine"] = "PolygonNatureBiomes", ["industrial"] = "PolygonMapsMilitaryWarehouse", ["woodland"] = "PolygonWoodland",
        };

        /// <summary>
        /// Every Synty prefab by its name (a model where there is no prefab), loaded the first time it is asked for; or by a
        /// prop's key (MapArt.ModelKey, `pack:fbx`), from that pack's folder, since several packs have models of the same
        /// name (Farm's and Apocalypse's wooden gate). The source's own models (SourceProps, in Extra) come before the
        /// packages' of the same name: Unity's can hold other parts that 0.10.1 draws apart (the house's glass, the trap's jaws).
        /// </summary>
        internal static Func<string, GameObject> Prefabs()
        {
            var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            // A name's models, the one asked by name alone first (as it always was: Extra's, else the last prefab, else the last model).
            var candidates = new Dictionary<string, List<string>>();
            foreach (string p in Enumerable.Reverse(models).Where(p => p.StartsWith("Assets/Synty/Extra/")).Concat(Enumerable.Reverse(prefabs)).Concat(Enumerable.Reverse(models)))
            {
                string name = Path.GetFileNameWithoutExtension(p);
                if (!candidates.TryGetValue(name, out var list)) candidates[name] = list = new List<string>();
                if (!list.Contains(p)) list.Add(p);
            }
            string PathOf(string asked)
            {
                int colon = asked.IndexOf(':');
                string pack = colon >= 0 ? asked.Substring(0, colon) : null;
                // Some of MapArt's names carry the pack's folder (Props/…): the file's name is what is looked up.
                string file = Path.GetFileName(colon >= 0 ? asked.Substring(colon + 1) : asked);
                if (UnityNames.TryGetValue(file, out var other)) file = other;
                if (!candidates.TryGetValue(file, out var list)) return null;
                if (pack == null) return list[0];
                return list.FirstOrDefault(p => p.StartsWith("Assets/Synty/Extra/"))
                    ?? (PackFolders.TryGetValue(pack, out var folder) ? list.FirstOrDefault(p => p.StartsWith($"Assets/Synty/{folder}/")) : null)
                    ?? list[0];
            }
            var loaded = new Dictionary<string, GameObject>();
            return name =>
            {
                if (name == null) return null;
                if (!loaded.TryGetValue(name, out var prefab))
                {
                    string p = PathOf(name);
                    loaded[name] = prefab = p != null ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
                }
                return prefab;
            };
        }
    }
}
