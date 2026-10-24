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

        /// <summary>A recorded state by its name (engine-cs/golden/step1/&lt;name&gt;.json).</summary>
        internal static GameState Golden(string name)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../../../engine-cs/golden/step1/{name}.json"));
            var file = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            return Bastion.Net.Json.Read<GameState>(file["state"].ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>The scene's sun and ambient light, by day or by night (until the game's own lighting).</summary>
        internal static void Lighting(bool day)
        {
            var sun = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Light>();
            sun.color = day ? new Color(1f, 0.94f, 0.84f) : new Color(0.6f, 0.72f, 1f);
            sun.intensity = day ? 1.6f : 0.35f;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.52f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.27f);
        }

        /// <summary>The map of `state` in a MapView of its own ("Mapa").</summary>
        internal static MapView Map(GameState state, string name)
        {
            var view = new GameObject("Mapa").AddComponent<MapView>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var builder = view.Build(state, Prefabs(), Materials());
            Debug.Log($"[map] {name}: {state.Width}×{state.Height}, {builder.Placed} props in {clock.ElapsedMilliseconds} ms; missing: {string.Join(", ", builder.Missing.OrderBy(m => m))}");
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
            // The cars' models and materials, to check their paints.
            var cars = root.GetComponentsInChildren<Transform>().Where(t => t.parent != null && t.parent.name == "Decorado" && t.name.StartsWith("car"));
            Debug.Log("[map] cars: " + string.Join("; ", cars.GroupBy(c => c.name + " " + string.Join("+", c.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m != null ? m.name : "-").Distinct())).Select(g => $"{g.Key} ×{g.Count()}")));

            // 0.10.1's capture view (reference.ts VIEWS.capture): over the middle, 40 tiles off, the camera's first turn.
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            var target = new Vector3(state.Width * MapSpace.Tile / 2, 0, -state.Height * MapSpace.Tile / 2);
            MapSpace.Aim(camera, target, 40);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
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
                var squad = state.Units.Where(u => u.Alive && u.Team == "xcom").Select(u => $"{u.Id} ({u.Pos.X},{u.Pos.Y},{u.Pos.Z})");
                Debug.Log($"[map] fog: {fog.Counts().sight} tiles in sight, {fog.Counts().known} seen of {state.Width * state.Height}; squad {string.Join(", ", squad)}");
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
        };

        /// <summary>Every Synty prefab by its name (a model where there is no prefab), loaded the first time it is asked for.</summary>
        internal static Func<string, GameObject> Prefabs()
        {
            var paths = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Synty" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                paths[Path.GetFileNameWithoutExtension(p)] = p;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                paths[Path.GetFileNameWithoutExtension(p)] = p;
            }
            var loaded = new Dictionary<string, GameObject>();
            return name =>
            {
                // Some of MapArt's names carry the pack's folder (Props/…): the file's name is what is looked up.
                if (!loaded.TryGetValue(name, out var prefab))
                {
                    string file = Path.GetFileName(name);
                    if (UnityNames.TryGetValue(file, out var other)) file = other;
                    loaded[name] = prefab = paths.TryGetValue(file, out var p) ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
                }
                return prefab;
            };
        }
    }
}
