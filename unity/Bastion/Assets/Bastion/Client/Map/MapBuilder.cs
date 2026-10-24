using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// Where a tile is in the world (docs/UNITY-ARTE.md §7): east +x, north +z, 1.5 m a tile; a floor's height its
    /// elevation (GameState.elev) or its storey (Pos.Z) times a storey, 2.3 tiles (0.10.1's coords.ts groundAt).
    /// </summary>
    public static class MapSpace
    {
        public const float Tile = 1.5f;
        public const float Storey = 2.3f * Tile;

        /// <summary>The middle of tile (x, y) on storey `level`, on its slab (no elevation: a building's storeys).</summary>
        public static Vector3 World(float x, float y, int level = 0) => new Vector3(x * Tile + Tile / 2, level * Storey, -(y * Tile + Tile / 2));

        /// <summary>The height of place p's floor: on storey z its slab; on the ground the tile's elevation.</summary>
        public static float FloorAt(GameState s, Pos p)
        {
            if (p.Z > 0) return p.Z * Storey;
            if (p.X < 0 || p.Y < 0 || p.X >= s.Width || p.Y >= s.Height) return 0;
            int i = p.Y * s.Width + p.X;
            return (s.Elev != null && i < s.Elev.Count ? s.Elev[i] : 0) * Storey;
        }

        /// <summary>The middle of place p's tile where one stands on it, `height` above.</summary>
        public static Vector3 TileToWorld(GameState s, Pos p, float height = 0)
        {
            var v = World(p.X, p.Y);
            v.y = FloorAt(s, p) + height;
            return v;
        }

        /// <summary>
        /// The game's camera (0.10.1's camera.ts): 52° down, 40° of view, `distance` tiles from `target`, turned `yaw`
        /// degrees from the south (0.10.1 starts at 45°: from the south-east, looking north-west).
        /// </summary>
        public static void Aim(Camera camera, Vector3 target, float distance, float yaw = 45)
        {
            float d = distance * Tile;
            float pitch = 52 * Mathf.Deg2Rad;
            float h = Mathf.Cos(pitch) * d;
            camera.fieldOfView = 40;
            camera.transform.position = target + new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * h, Mathf.Sin(pitch) * d, -Mathf.Cos(yaw * Mathf.Deg2Rad) * h);
            camera.transform.LookAt(target);
        }

        /// <summary>The tile under a point of the world (its x and y; the storey is the caller's).</summary>
        public static Vector2Int WorldToTile(Vector3 v) => new Vector2Int(Mathf.FloorToInt(v.x / Tile), Mathf.FloorToInt(-v.z / Tile));

        /// <summary>
        /// The tile a ray (the cursor's) meets on storey `level`'s slab, without colliders: the plane at that floor's
        /// height. On the ground, where elevation rises (a roof walked on), the first tile along the ray whose floor it is.
        /// </summary>
        public static bool Pick(GameState s, Ray ray, int level, out Pos place)
        {
            place = null;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            if (level > 0 || s.Elev == null || s.Elev.All(e => e == 0))
            {
                float t = (level * Storey - ray.origin.y) / ray.direction.y;
                if (t < 0) return false;
                var tile = WorldToTile(ray.origin + ray.direction * t);
                if (tile.x < 0 || tile.y < 0 || tile.x >= s.Width || tile.y >= s.Height) return false;
                place = new Pos { X = tile.x, Y = tile.y, Z = level };
                return true;
            }
            // From the highest floor down: the first whose height the ray meets over a tile of that elevation.
            foreach (int e in s.Elev.Distinct().OrderByDescending(e => e))
            {
                float t = (e * Storey - ray.origin.y) / ray.direction.y;
                if (t < 0) continue;
                var tile = WorldToTile(ray.origin + ray.direction * t);
                if (tile.x < 0 || tile.y < 0 || tile.x >= s.Width || tile.y >= s.Height) continue;
                if (s.Elev[tile.y * s.Width + tile.x] != e) continue;
                place = new Pos { X = tile.x, Y = tile.y, Z = 0 };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The map from the state, a first pass of 0.10.1's mapView.ts: the ground a colour by what its tiles are, the
    /// buildings' walls as blocks of their height (Synty's kits come next), and the decor as Synty's props as 0.10.1
    /// picked, turned and fitted them. `prefabOf` gives a prop's model by its Synty name.
    /// </summary>
    public sealed class MapBuilder
    {
        GameState s;
        readonly Func<string, GameObject> prefabOf;
        readonly Func<string, Material> materialOf;
        readonly Transform root;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        /// <summary>Props asked for that have no model (a pack not imported, a name not found), once each.</summary>
        public readonly HashSet<string> Missing = new HashSet<string>();
        public int Placed { get; private set; }

        /// <summary>`prefabOf` gives a Synty model by its name, `materialOf` a Synty material by its (the cars' paints).</summary>
        public MapBuilder(GameState state, Transform root, Func<string, GameObject> prefabOf, Func<string, Material> materialOf = null)
        {
            s = state;
            this.root = root;
            this.prefabOf = prefabOf;
            this.materialOf = materialOf;
        }

        /// <summary>
        /// A parked car's paint (0.10.1's CAR_PAINTS, playtest 20/10 #19: «todos los coches son rojos»): Apocalypse's own red,
        /// or its texture's alternates, two greys, a blue, an olive, a mustard. 0.10.1 repainted only the red texels with
        /// those colours; here the alternate's material whole, which is what they were taken from.
        /// </summary>
        static readonly string[] CarPaints = { null, "PolygonApocalypse_01_B", "PolygonApocalypse_01_C", "PolygonApocalypse_02_A", "PolygonApocalypse_03_A", "PolygonApocalypse_04_A" };

        /// <summary>The Synty materials the map asks `materialOf` for (the cars' paints), for the game's catalog (MapCatalog).</summary>
        public static IEnumerable<string> MaterialsUsed => CarPaints.Where(m => m != null);
        const string CarRed = "PolygonApocalypse_01_A";

        void Repaint(GameObject car, int paint)
        {
            if (CarPaints[paint] == null || materialOf == null) return;
            var to = materialOf(CarPaints[paint]);
            if (to == null) return;
            foreach (var r in car.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) if (mats[k] != null && mats[k].name.StartsWith(CarRed)) mats[k] = to;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Each storey's objects (0 the ground: its floor, walls and decor; above, each slab and what is on it).</summary>
        public Transform[] Storeys { get; private set; } = Array.Empty<Transform>();
        /// <summary>The buildings' roofs, over each one's top storey.</summary>
        public Transform Roofs { get; private set; }

        Transform[] walls = Array.Empty<Transform>();
        Transform[] props = Array.Empty<Transform>();
        GameObject[] slabs = Array.Empty<GameObject>();
        /// <summary>What stands on each tile of each storey (Key), and the tiles each thing stands on: what goes when they change.</summary>
        readonly Dictionary<long, List<GameObject>> onTile = new Dictionary<long, List<GameObject>>();
        readonly Dictionary<GameObject, List<long>> tilesOf = new Dictionary<GameObject, List<long>>();

        static long Key(int k, int i) => ((long)k << 32) | (uint)i;

        public void Build()
        {
            int levels = 1 + (s.Levels?.Count ?? 0);
            Storeys = new Transform[levels];
            walls = new Transform[levels];
            props = new Transform[levels];
            slabs = new GameObject[levels];
            for (int k = 0; k < levels; k++)
            {
                Storeys[k] = new GameObject($"Planta {k}").transform;
                Storeys[k].SetParent(root, false);
                walls[k] = new GameObject("Paredes").transform;
                walls[k].SetParent(Storeys[k], false);
                props[k] = new GameObject("Decorado").transform;
                props[k].SetParent(Storeys[k], false);
            }
            Roofs = new GameObject("Tejados").transform;
            Roofs.SetParent(root, false);
            Ground(Storeys[0]);
            for (int k = 0; k < levels; k++)
            {
                if (k > 0) slabs[k] = Slab(Storeys[k], k);
                int n = TilesOf(k).Count;
                for (int i = 0; i < n; i++) Wall(k, i);
                var done = new HashSet<int>();
                for (int i = 0; i < n; i++) Decor(k, i, done);
            }
            RoofSlabs();
        }

        /// <summary>
        /// The map brought to `next` (the same map: a door opened, cover blown away, a floor given way; mapView.ts
        /// rebuild): only the tiles that changed are made again, with whatever stood over them too (a car over a tile
        /// that changed), and the slab of a storey whose tiles changed. How many tiles were made again; -1 if `next` has
        /// other storeys (build it whole).
        /// </summary>
        public int Update(GameState next)
        {
            int levels = 1 + (next.Levels?.Count ?? 0);
            if (levels != Storeys.Length || next.Width != s.Width || next.Height != s.Height) return -1;
            var changed = new HashSet<long>();
            for (int k = 0; k < levels; k++)
            {
                IList<string> before = TilesOf(k), after = TilesOf(next, k);
                List<int> was = DecorOf(k), now = DecorOf(next, k);
                int n = Mathf.Max(before.Count, after.Count);
                for (int i = 0; i < n; i++)
                {
                    string a = i < before.Count ? before[i] : null, b = i < after.Count ? after[i] : null;
                    int d = i < was.Count ? was[i] : -1, e = i < now.Count ? now[i] : -1;
                    if (a != b || d != e) changed.Add(Key(k, i));
                }
            }
            s = next;
            if (changed.Count == 0) return 0;
            // What stood on a changed tile goes, and every tile it stood on is made again.
            var redo = new HashSet<long>(changed);
            foreach (long key in changed)
            {
                if (!onTile.TryGetValue(key, out var standing)) continue;
                foreach (var o in standing.ToList())
                {
                    redo.UnionWith(tilesOf[o]);
                    Drop(o);
                }
            }
            var done = new Dictionary<int, HashSet<int>>();
            foreach (int k in redo.Select(key => (int)(key >> 32)).Distinct())
            {
                if (k > 0)
                {
                    DestroyNow(slabs[k]);
                    slabs[k] = Slab(Storeys[k], k);
                }
                done[k] = new HashSet<int>();
            }
            foreach (long key in redo)
            {
                int k = (int)(key >> 32), i = (int)(uint)key;
                Wall(k, i);
                Decor(k, i, done[k]);
            }
            return redo.Count;
        }

        /// <summary>`o` noted as standing on those tiles of storey k.</summary>
        void Own(GameObject o, int k, IEnumerable<int> tiles)
        {
            if (o == null) return;
            var keys = tiles.Select(i => Key(k, i)).ToList();
            tilesOf[o] = keys;
            foreach (long key in keys)
            {
                if (!onTile.TryGetValue(key, out var list)) onTile[key] = list = new List<GameObject>();
                list.Add(o);
            }
        }

        void Drop(GameObject o)
        {
            if (tilesOf.TryGetValue(o, out var keys))
                foreach (long key in keys)
                    if (onTile.TryGetValue(key, out var list)) list.Remove(o);
            tilesOf.Remove(o);
            DestroyNow(o);
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        static bool IsWall(string t) => t == "wall" || t == "window" || t == "windowBroken" || t == "door" || t == "doorOpen";

        int GroundAt(int i) => s.Ground != null && i < s.Ground.Count ? s.Ground[i] : 0;

        /// <summary>Storey k's tiles (the ground's, or a level's: null where it has no slab).</summary>
        IList<string> TilesOf(int k) => TilesOf(s, k);

        static IList<string> TilesOf(GameState state, int k) => k == 0 ? (IList<string>)state.Tiles : state.Levels[k - 1].Tiles;

        List<int> DecorOf(int k) => DecorOf(s, k);

        static List<int> DecorOf(GameState state, int k) => (k == 0 ? state.Decor : state.Levels[k - 1].Decor) ?? new List<int>();

        /// <summary>One plane under the map, a texel a tile: out of play dark, streets asphalt, insides floor, yards dirt, the rest by the biome.</summary>
        void Ground(Transform storey)
        {
            var texture = new Texture2D(s.Width, s.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Ground" };
            var open = s.Biome == Biomes.Wilds ? new Color(0.42f, 0.5f, 0.3f) : s.Biome == Biomes.Facility ? new Color(0.58f, 0.58f, 0.56f) : new Color(0.72f, 0.7f, 0.66f);
            for (int y = 0; y < s.Height; y++)
            {
                for (int x = 0; x < s.Width; x++)
                {
                    int i = y * s.Width + x;
                    int g = GroundAt(i);
                    var c = g == 4 ? new Color(0.17f, 0.18f, 0.2f) : g == 1 ? new Color(0.27f, 0.28f, 0.3f) : g == 2 ? new Color(0.6f, 0.55f, 0.48f) : g == 3 ? new Color(0.45f, 0.4f, 0.32f) : open;
                    // The texture's rows go up from the bottom; the map's go south from the top, so row y is texel row height-1-y.
                    texture.SetPixel(x, s.Height - 1 - y, c);
                }
            }
            texture.Apply();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Suelo";
            ground.transform.SetParent(storey, false);
            ground.transform.SetPositionAndRotation(new Vector3(s.Width * MapSpace.Tile / 2, 0, -s.Height * MapSpace.Tile / 2), Quaternion.Euler(90, 0, 0));
            ground.transform.localScale = new Vector3(s.Width * MapSpace.Tile, s.Height * MapSpace.Tile, 1);
            var m = new Material(Lit("ground", Color.white)) { mainTexture = texture };
            ground.GetComponent<Renderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
        }

        /// <summary>Storey k's slab: a quad over each tile it has (not its walls), in one mesh.</summary>
        GameObject Slab(Transform storey, int k)
        {
            var tiles = TilesOf(k);
            return Quads(storey, "Losa", Lit("slab", new Color(0.6f, 0.55f, 0.48f)), k * MapSpace.Storey + 0.02f, i => i < tiles.Count && tiles[i] != null && !IsWall(tiles[i]));
        }

        /// <summary>Over each building's top storey, its roof: a quad on every tile of it (GameState.storeys), in one mesh.</summary>
        void RoofSlabs()
        {
            if (s.Storeys == null) return;
            int top = s.Storeys.Where((n, i) => GroundAt(i) != 4).DefaultIfEmpty(0).Max();
            for (int n = 1; n <= top; n++)
            {
                int storeys = n;
                Quads(Roofs, $"Tejado {n}", Lit("roof", new Color(0.4f, 0.4f, 0.42f)), n * MapSpace.Storey + 0.02f, i => s.Storeys[i] == storeys && GroundAt(i) != 4);
            }
        }

        GameObject Quads(Transform parent, string name, Material material, float height, Func<int, bool> on)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < s.Width * s.Height; i++)
            {
                if (!on(i)) continue;
                int x = i % s.Width, y = i / s.Width;
                int v = vertices.Count;
                vertices.Add(new Vector3(x * MapSpace.Tile, height, -y * MapSpace.Tile));
                vertices.Add(new Vector3((x + 1) * MapSpace.Tile, height, -y * MapSpace.Tile));
                vertices.Add(new Vector3((x + 1) * MapSpace.Tile, height, -(y + 1) * MapSpace.Tile));
                vertices.Add(new Vector3(x * MapSpace.Tile, height, -(y + 1) * MapSpace.Tile));
                triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = material;
            return o;
        }

        /// <summary>
        /// Tile i of storey k as a wall, window or door, a block a storey tall (the outline left out); an open door a
        /// gap, until Synty's kits.
        /// </summary>
        void Wall(int k, int i)
        {
            var tiles = TilesOf(k);
            if (i >= tiles.Count) return;
            string t = tiles[i];
            if (t == null || !IsWall(t) || t == "doorOpen" || (k == 0 && GroundAt(i) == 4)) return;
            int x = i % s.Width, y = i / s.Width;
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = t;
            block.transform.SetParent(walls[k], false);
            block.transform.localPosition = MapSpace.World(x, y, k) + Vector3.up * MapSpace.Storey / 2;
            block.transform.localScale = new Vector3(MapSpace.Tile, MapSpace.Storey, MapSpace.Tile);
            block.GetComponent<Renderer>().sharedMaterial = t.StartsWith("window")
                ? Lit("window", new Color(0.45f, 0.55f, 0.62f))
                : t.StartsWith("door") ? Lit("door", new Color(0.45f, 0.33f, 0.25f)) : Lit("wall", new Color(0.66f, 0.64f, 0.6f));
            UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            Own(block, k, new[] { i });
        }

        /// <summary>
        /// Tile i of storey k's decor (mapView.ts addProp): a cover tile's prop, picked from its set, turned, let off the
        /// grid, fitted; or the object over several tiles it is part of, once (`done`: the anchors already drawn).
        /// </summary>
        void Decor(int k, int i, HashSet<int> done)
        {
            var tiles = TilesOf(k);
            var decor = DecorOf(k);
            if (i >= tiles.Count || i >= decor.Count) return;
            float floor = k * MapSpace.Storey;
            string tile = tiles[i];
            if (tile != "low" && tile != "high") return;
            var look = Bastion.Engine.Decor.DecorOf(decor[i]);
            if (look == null) return;
            // An object over several tiles: one model over all of them, from its anchor (mapView.ts drawThing).
            var thing = Bastion.Engine.Decor.ObjectAt(decor, s.Width, i);
            if (thing != null && thing.Tiles.Count > 1)
            {
                if (!done.Add(thing.Anchor)) return;
                // Whatever still stands on its tiles goes (made again with it).
                foreach (int j in thing.Tiles)
                    if (onTile.TryGetValue(Key(k, j), out var standing))
                        foreach (var o in standing.Where(o => o != null && o.transform.parent == props[k]).ToList()) Drop(o);
                Own(Thing(props[k], thing, floor), k, thing.Tiles);
                return;
            }
            if (look.Kind == "part") return;
            bool low = tile == "low";
            string set = MapArt.DecorSet(look.Kind, low, s.Biome, s.Site);
            if (set == null) return;
            int x = i % s.Width, y = i / s.Width;
            float cx = x + 0.5f, cz = y + 0.5f;
            int turn = MapArt.Facing.Contains(look.Kind) ? (4 - look.Facing) % 4 : Mathf.FloorToInt(MapArt.Hash(x, y, 2) * 4);
            int wiggle = look.Kind == "tree" && low ? 1 : MapArt.Wiggle.TryGetValue(look.Kind, out int w) ? w : 0;
            float shift = wiggle == 2 ? 0.07f : wiggle == 1 ? 0.035f : 0;
            // 0.10.1's turn, counter-clockwise from above in tiles with y south; Unity's is the other way round.
            float yaw = wiggle == 2 ? MapArt.Hash(x, y, 7) * 360 : turn * 90 + (wiggle == 1 ? (MapArt.Hash(x, y, 6) - 0.5f) * 0.16f * Mathf.Rad2Deg : 0);
            cx += (MapArt.Hash(x, y, 4) - 0.5f) * 2 * shift;
            cz += (MapArt.Hash(x, y, 5) - 0.5f) * 2 * shift;
            var ids = MapArt.Set(set);
            string id = ids[Mathf.Min(ids.Length - 1, Mathf.FloorToInt(MapArt.Hash(x, y, 1) * ids.Length))];
            Own(Place(props[k], id, new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), -yaw), k, new[] { i });
        }

        /// <summary>An object over several tiles, from its anchor: its model in the middle of its tiles, turned the way it faces.</summary>
        GameObject Thing(Transform parent, DecorObject thing, float floor)
        {
            int x0 = thing.Tiles.Min(t => t % s.Width), x1 = thing.Tiles.Max(t => t % s.Width);
            int y0 = thing.Tiles.Min(t => t / s.Width), y1 = thing.Tiles.Max(t => t / s.Width);
            int ax = thing.Anchor % s.Width, ay = thing.Anchor / s.Width;
            string id;
            switch (thing.Kind)
            {
                case "carFront":
                    var cars = MapArt.Set("carFront");
                    id = cars[Mathf.Min(cars.Length - 1, Mathf.FloorToInt(MapArt.Hash(ax, ay, 1) * cars.Length))];
                    break;
                case "bed":
                    id = x1 - x0 == y1 - y0 ? "bedDouble" : "bed";
                    break;
                case "bench":
                    // Along its two tiles, its seat the way it faces; a broken one now and then.
                    id = MapArt.Hash(x0, y0, 31) < 0.3f ? "parkBenchBroken" : "parkBench";
                    break;
                case "sirenCar":
                    // The sabotage's siren car is its unit's (the relay, unitViews.ts sirenOf), not the map's.
                    return null;
                case "shed":
                case "washingLine":
                case "vegPatch":
                    return Garden(parent, thing, x0, y0, x1 - x0 + 1, y1 - y0 + 1, ax, ay, floor);
                default:
                    id = thing.Kind;
                    break;
            }
            float cx = (x0 + x1 + 1) / 2f, cz = (y0 + y1 + 1) / 2f;
            var placed = Place(parent, id, new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), -((4 - thing.Facing) % 4) * 90, MapArt.FootprintOf(id));
            if (placed != null && thing.Kind == "carFront") Repaint(placed, Mathf.FloorToInt(MapArt.Hash(ax, ay, 13) * CarPaints.Length) % CarPaints.Length);
            return placed;
        }

        /// <summary>
        /// A block's back garden (mapView.ts drawThing; docs/MAPAS.md §5.4): a lean-to, a washing line or a raised bed over
        /// its tiles, its long side along theirs. The lean-to and the line scaled whole, the bed to its tiles (its height
        /// as it is), each a tenth of a tile in from their edges.
        /// </summary>
        GameObject Garden(Transform parent, DecorObject thing, int x0, int y0, int w, int h, int ax, int ay, float floor)
        {
            string id = thing.Kind == "shed" ? "bLeanTo" : thing.Kind == "washingLine" ? "bWashingLine" : "bGardenBed";
            var placed = Place(parent, id, Vector3.zero, 0);
            if (placed == null) return null;
            var size = BoundsIn(placed, placed.transform).size / MapSpace.Tile;
            bool alongX = size.x >= size.z;
            float ml = alongX ? size.x : size.z, ms = alongX ? size.z : size.x;
            float fl = (Mathf.Max(w, h) - 0.2f) / Mathf.Max(ml, 1e-4f);
            float fs = (Mathf.Min(w, h) - 0.2f) / Mathf.Max(ms, 1e-4f);
            float k = thing.Kind == "washingLine" ? fl : Mathf.Min(fl, fs);
            placed.transform.localScale = thing.Kind == "vegPatch" ? (alongX ? new Vector3(fl, 1, fs) : new Vector3(fs, 1, fl)) : Vector3.one * k;
            // 0.10.1's turn (counter-clockwise from above), the other way round here.
            float turn = (alongX == w >= h ? 0 : 90) + (MapArt.Hash(ax, ay, 3) < 0.5f ? 0 : 180);
            float cx = x0 + w / 2f, cz = y0 + h / 2f;
            placed.transform.SetPositionAndRotation(new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), Quaternion.Euler(0, -turn, 0));
            return placed;
        }

        /// <summary>
        /// A prop on the floor at `at`, turned `yaw` degrees (Unity's), fitted as 0.10.1 (syntyProps.ts prepare); over
        /// several tiles, to its footprint (mapView.ts fitted).
        /// </summary>
        GameObject Place(Transform parent, string id, Vector3 at, float yaw, MapArt.Footprint footprint = null)
        {
            var art = MapArt.Prop(id);
            if (art == null || string.IsNullOrEmpty(art.fbx))
            {
                Missing.Add(id);
                return null;
            }
            var prefab = prefabOf(art.fbx);
            if (prefab == null)
            {
                Missing.Add($"{id} ({art.pack}/{art.fbx})");
                return null;
            }
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(prefab, holder, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Its box as it stands, then the longest side to `fit` tiles; centred (or on its origin) and on the floor.
            var bounds = BoundsIn(model, holder);
            var size = bounds.size;
            float fit = footprint != null && art.fit > 0 ? footprint.fit : art.fit;
            float along = fit > 0 ? fit * MapSpace.Tile / Mathf.Max(size.x, size.z, 1e-4f) : 1;
            float across = (art.stretch ? 1 : along) * (art.thick > 0 ? art.thick : 1) * (footprint?.thin ?? 1);
            var scale = size.x >= size.z ? new Vector3(along, across, across) : new Vector3(across, across, along);
            if (art.tall > 0) scale.y = art.tall * MapSpace.Tile / Mathf.Max(size.y, 1e-4f);
            var centre = art.origin ? Vector3.zero : bounds.center;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, scale);
            model.transform.localPosition = new Vector3(-centre.x * scale.x, -bounds.min.y * scale.y - art.sink * MapSpace.Tile, -centre.z * scale.z);
            // Its own turn to face north (0.10.1's quarter turns, the other way round here), then where it goes.
            var turned = new GameObject("turn").transform;
            turned.SetParent(holder, false);
            turned.localRotation = Quaternion.Euler(0, -art.yaw * 90, 0);
            if (art.shift != null && art.shift.Length >= 2) turned.localPosition = new Vector3(art.shift[0] * MapSpace.Tile, 0, -art.shift[1] * MapSpace.Tile);
            model.transform.SetParent(turned, false);
            holder.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            Placed++;
            return holder.gameObject;
        }

        static Bounds BoundsIn(GameObject model, Transform space)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = new Bounds(space.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                var rb = r.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = rb.center + Vector3.Scale(rb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    b.Encapsulate(space.InverseTransformPoint(corner));
                }
            }
            return b;
        }

        Material Lit(string key, Color colour)
        {
            if (materials.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key, color = colour };
            m.SetFloat("_Smoothness", 0.05f);
            return materials[key] = m;
        }
    }
}
