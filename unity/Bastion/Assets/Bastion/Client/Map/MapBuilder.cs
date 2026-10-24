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

        /// <summary>Where a unit on place p stands (unitViews.ts standAt): its tile's middle on its floor, or on the treads of a flight of stairs.</summary>
        public static Vector3 StandAt(GameState s, Pos p) => TileToWorld(s, p, StairLift(s, p));

        /// <summary>
        /// How high over its tile's floor a unit on place p stands (unitViews.ts stairLift): on the treads of a building's
        /// flight of stairs (engine Flights), else 0. Metres.
        /// </summary>
        public static float StairLift(GameState s, Pos p)
        {
            if (s == null || p == null || p.X < 0 || p.Y < 0 || p.X >= s.Width || p.Y >= s.Height) return 0;
            var on = Flights.FlightAt(s, new Pos { X = p.X, Y = p.Y, Z = p.Z });
            return on == null ? 0 : FlightHeight(on.Step + 0.5f, on.Flight.Tiles.Count);
        }

        /// <summary>
        /// How high the feet are over the floor of a flight of `n` tiles, `a` tiles up it from the back edge of its lowest
        /// tile (unitViews.ts flightHeight): half a riser over the treads' front edges, from its foot up to its landing's
        /// floor. Its treads as BuildingSurfaces lays them: nine for a flight of one tile, six a tile for longer ones.
        /// </summary>
        public static float FlightHeight(float a, int n)
        {
            int treads = n == 1 ? 9 : 6 * n;
            return Mathf.Clamp(a / n * Storey + Storey / (2 * treads), 0, Storey);
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

        /// <summary>
        /// From 0.10.1's frame to Unity's: there x east, y up, z south (the map's rows), in metres here; Unity's z is north.
        /// A matrix that places something in that frame, as Unity's.
        /// </summary>
        public static Matrix4x4 FromGame(Matrix4x4 game) => Matrix4x4.Scale(new Vector3(1, 1, -1)) * game;

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
    /// The map from the state, 0.10.1's mapView.ts: the ground (UI's GroundView), the buildings in Synty's kit pieces
    /// (WallLayout, WallsView) with their floors, roofs and stairs (BuildingSurfaces), and the decor as Synty's props as
    /// 0.10.1 picked, turned and fitted them. `prefabOf` gives a prop's model by its Synty name.
    /// </summary>
    public sealed class MapBuilder
    {
        GameState s;
        readonly Func<string, GameObject> prefabOf;
        readonly Func<string, Material> materialOf;
        readonly Transform root;
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
        BuildingPlan plan;
        WallsView wallsView;
        Transform[] props = Array.Empty<Transform>();
        /// <summary>The storeys' floors (with their stairs) and the decks of roofs one walks on, made again when a slab is blown through; the parapets.</summary>
        readonly List<GameObject> upper = new List<GameObject>();
        GameObject parapets;
        /// <summary>What the rooms have besides their cover (HomeFurnishing: rugs, lamps, chairs round a table…).</summary>
        readonly List<GameObject> furnished = new List<GameObject>();
        /// <summary>The wrecks whose headlights are left on by night (mapView.ts: one in four), in Unity's metres.</summary>
        readonly Dictionary<int, NightLights.Glow> headlights = new Dictionary<int, NightLights.Glow>();
        /// <summary>The ground, its textures and marks (UI's GroundView: ground.ts).</summary>
        GroundView ground;
        /// <summary>
        /// The base's pieces, on its siege's map and on its own screen (BasePieces; null on any other map), and the map as
        /// the ground and the buildings see it: without what BasePieces draws (TerrainOf; the state itself with no base).
        /// </summary>
        PieceMap pieces;
        GameState terrain;
        /// <summary>The pieces as drawn, and each one's look (BasePieces.PieceKey) when they were: drawn again when one changes.</summary>
        readonly List<GameObject> pieceArt = new List<GameObject>();
        string piecesDrawn;
        /// <summary>The base's pinar past the map's edge (BaseWoods.Wood), made once a map.</summary>
        GameObject beyond;
        /// <summary>How tall and big each prop's model stands as placed (mapView.ts topOf and sizeOf), in tiles, once measured.</summary>
        readonly Dictionary<string, PropMeasure> measured = new Dictionary<string, PropMeasure>();
        int holesMade;
        readonly Dictionary<int, (Vector3 outer, Vector3 inner)> looks = new Dictionary<int, (Vector3, Vector3)>();
        /// <summary>What stands on each tile of each storey (Key), and the tiles each thing stands on: what goes when they change.</summary>
        readonly Dictionary<long, List<GameObject>> onTile = new Dictionary<long, List<GameObject>>();
        readonly Dictionary<GameObject, List<long>> tilesOf = new Dictionary<GameObject, List<long>>();

        static long Key(int k, int i) => ((long)k << 32) | (uint)i;

        /// <summary>The buildings as 0.10.1 read them off the map (buildings.ts BuildingPlan), on the state as it is now.</summary>
        public BuildingPlan Plan => plan;
        /// <summary>The walls' pieces laid, and those not in the catalog.</summary>
        public WallsView Walls => wallsView;

        public void Build()
        {
            int levels = 1 + (s.Levels?.Count ?? 0);
            Storeys = new Transform[levels];
            walls = new Transform[levels];
            props = new Transform[levels];
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
            Terrain();
            plan = new BuildingPlan(terrain);
            ground = new GroundView();
            ground.Build(terrain, plan, Storeys[0]);
            BuildingParts();
            wallsView = new WallsView(walls, s.Width, (parent, id) => Place(parent, id, Vector3.zero, 0), LooksOf, plan);
            wallsView.Lay(WallLayout.Of(terrain, plan));
            for (int k = 0; k < levels; k++)
            {
                int n = TilesOf(k).Count;
                var done = new HashSet<int>();
                for (int i = 0; i < n; i++) Decor(k, i, done);
            }
            DrawPieces();
            Woods(null);
            Beyond();
            Furnish();
            Night();
        }

        /// <summary>
        /// The base's pines (mapView.ts dressed, 'basePines') on the tiles of `only` (Key; all of them if null), each on its
        /// storey and gone when its tile changes, and where one was felled its stump. None but on the base's map.
        /// </summary>
        void Woods(HashSet<long> only)
        {
            if (pieces == null) return;
            bool PieceAt(int i) => pieces.at[i] >= 0;
            foreach (var p in BaseWoods.Pines(s, PieceAt, i => pieces.yard.Has(i)).Concat(BaseWoods.Stumps(s, PieceAt)))
            {
                if (only != null && !only.Contains(Key(p.z, p.tile))) continue;
                var o = PlaceGame(props[p.z], p.id, p.matrix);
                if (o == null) continue;
                if (!p.shadows) foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Own(o, p.z, new[] { p.tile });
            }
        }

        /// <summary>
        /// The base's pinar past the edge (mapView.ts baseWood, farWood): as thick as along it, out to where the haze has it
        /// all, its tracks' ways left open; none of it casts a shadow. Out to BaseWoods.Near its pines are models of their
        /// own, as the map's; further, as 0.10.1 instanced them, they are merged into a mesh for each patch of the wood
        /// (WoodPatch tiles a side) and material, so what is out of view is still left out: a few hundred renderers for
        /// thousands of models (Gameplay, 24/10). Made once a map (nobody fells nor builds out there).
        /// </summary>
        void Beyond()
        {
            DestroyNow(beyond);
            beyond = null;
            if (pieces == null || ground?.Map == null) return;
            beyond = new GameObject("Bosque");
            beyond.transform.SetParent(root, false);
            var flip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            var parts = new Dictionary<string, List<(Mesh mesh, int sub, Material material, Matrix4x4 local)>>();
            var lots = new Dictionary<(int x, int z, Material material), List<CombineInstance>>();
            int near = 0, far = 0;
            foreach (var p in BaseWoods.Wood(terrain, ground.Map, id => Measure(id).top))
            {
                if (!p.far)
                {
                    var o = PlaceGame(beyond.transform, p.id, p.matrix);
                    if (o == null) continue;
                    foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    near++;
                    continue;
                }
                if (!parts.TryGetValue(p.id, out var of)) parts[p.id] = of = PartsOf(p.id);
                var at = flip * WallsView.InMetres(p.matrix) * flip;
                var patch = (Mathf.FloorToInt(p.x / (float)WoodPatch), Mathf.FloorToInt(p.y / (float)WoodPatch));
                foreach (var (mesh, sub, material, local) in of)
                {
                    var key = (patch.Item1, patch.Item2, material);
                    if (!lots.TryGetValue(key, out var lot)) lots[key] = lot = new List<CombineInstance>();
                    lot.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = at * local });
                }
                far++;
            }
            foreach (var kv in lots)
            {
                var mesh = new Mesh { name = $"Pinar {kv.Key.x},{kv.Key.z} {kv.Key.material.name}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var o = new GameObject(mesh.name);
                o.transform.SetParent(beyond.transform, false);
                o.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = o.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key.material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Debug.Log($"[map] the pinar past the edge: {near} pines near, {far} far in {lots.Count} meshes");
        }

        /// <summary>The side of a patch of the far wood merged into one mesh, in tiles.</summary>
        const int WoodPatch = 8;

        /// <summary>Prop `id`'s meshes as it is placed (Place), each part's submesh with its material and where it stands in the prop.</summary>
        List<(Mesh mesh, int sub, Material material, Matrix4x4 local)> PartsOf(string id)
        {
            var list = new List<(Mesh, int, Material, Matrix4x4)>();
            var o = Place(root, id, Vector3.zero, 0);
            if (o == null) return list;
            o.transform.SetParent(null, false);
            o.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Placed--;
            foreach (var r in o.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var materials = r.sharedMaterials;
                var local = o.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int k = 0; k < mesh.subMeshCount && materials.Length > 0; k++) list.Add((mesh, k, materials[Mathf.Min(k, materials.Length - 1)], local));
            }
            DestroyNow(o);
            return list;
        }

        /// <summary>The base's pieces on the map as it is now, and the map without them (mapView.ts build: pieceMap, terrainOf).</summary>
        void Terrain()
        {
            pieces = BasePieces.MapOf(s);
            terrain = pieces != null ? BasePieces.TerrainOf(s, pieces) : s;
        }

        /// <summary>
        /// The base's pieces (mapView.ts rebuild; basePieces.ts drawPiece), each at its wear, its sandbags as a siege's
        /// barricade's; all of them drawn again when one changed (some hundred models).
        /// </summary>
        void DrawPieces()
        {
            string key = pieces == null ? "" : string.Join(";", pieces.spots.Select(spot => BasePieces.PieceKey(s, spot)));
            if (key == piecesDrawn) return;
            piecesDrawn = key;
            foreach (var o in pieceArt) DestroyNow(o);
            pieceArt.Clear();
            if (pieces == null) return;
            var placements = new List<PiecePlacement>();
            foreach (var spot in pieces.spots)
            {
                if (!BasePieces.Drawn.Contains(spot.piece.Piece)) continue;
                int wear = BasePieces.PieceWear(s, spot);
                if (spot.piece.Piece == PieceIds.Sandbags) BasePieces.DrawSandbags(s, pieces, spot, wear, placements);
                else BasePieces.DrawPiece(s, pieces, spot, wear, Measure, placements);
            }
            foreach (var p in placements)
            {
                var o = PlaceGame(props[0], p.id, p.matrix);
                if (o == null) continue;
                if (!p.shadows) foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pieceArt.Add(o);
            }
            Debug.Log($"[map] the base's pieces: {string.Join(", ", placements.GroupBy(p => p.id).Select(g => $"{g.Key} ×{g.Count()}"))}");
        }

        /// <summary>Prop `id` measured as it is placed (mapView.ts topOf, sizeOf): its parts' highest point and their box, in tiles.</summary>
        PropMeasure Measure(string id)
        {
            if (measured.TryGetValue(id, out var m)) return m;
            var o = Place(root, id, Vector3.zero, 0);
            if (o == null) return measured[id] = PropMeasure.Unloaded;
            o.transform.SetParent(null, false);
            o.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Placed--;
            bool any = o.GetComponentInChildren<Renderer>() != null;
            var b = BoundsIn(o, o.transform);
            DestroyNow(o);
            float top = b.max.y / MapSpace.Tile;
            m = any ? new PropMeasure(top > 0 ? top : 0.3, b.size.x / MapSpace.Tile, b.size.y / MapSpace.Tile, b.size.z / MapSpace.Tile) : PropMeasure.Unloaded;
            return measured[id] = m;
        }

        /// <summary>The night's lamps (UI's street lights) and the headlights left on, for NightLights.</summary>
        void Night()
        {
            // Those whose car has gone (its tile changed) go with it.
            foreach (int anchor in headlights.Keys.ToList())
                if (s.Decor == null || anchor >= s.Decor.Count || Bastion.Engine.Decor.DecorOf(s.Decor[anchor])?.Kind != "carFront") headlights.Remove(anchor);
            (root.GetComponent<NightLights>() ?? root.gameObject.AddComponent<NightLights>()).Show(ground?.Lamps, headlights.Values.ToList());
        }

        /// <summary>
        /// What a house's, a shop's, a barn's rooms have besides their cover (homeFurnishing.ts, mapView.ts): cosmetic, on
        /// clear floor only, fading with its storey; none where the wall it stood by was blown down. Made again whole
        /// when the map changes (a few hundred models).
        /// </summary>
        void Furnish()
        {
            foreach (var o in furnished) DestroyNow(o);
            furnished.Clear();
            var all = HomeFurnishing.Homes(s).Concat(HomeFurnishing.Shops(s, plan.Buildings)).Concat(HomeFurnishing.Barns(s, plan.Buildings)).Concat(HomeFurnishing.OwnTiles(s));
            foreach (var f in all)
            {
                int i = f.y * s.Width + f.x;
                if (f.z < 0 || f.z >= Storeys.Length || (f.z > 0 && s.Levels?[f.z - 1]?.Tiles == null) || plan.Breached(i, f.z)) continue;
                var o = PlaceGame(props[f.z], f.id, f.matrix);
                if (o == null) continue;
                if (!f.cast) foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                int b = i >= 0 && i < plan.Owner.Length ? plan.Owner[i] : -1;
                if (f.z > 0 && b >= 0) Cutaway.Current?.Track(o, b, f.z);
                furnished.Add(o);
            }
        }

        /// <summary>
        /// The map brought to `next` (the same map: a door opened, cover blown away, a floor given way; mapView.ts
        /// rebuild): only the tiles that changed are made again, with whatever stood over them too (a car over a tile
        /// that changed), and the storeys' floors if a slab was blown through. How many tiles were made again; -1 if `next` has
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
            Terrain();
            plan?.Follow(terrain);
            if (changed.Count == 0) return 0;
            ground?.Rebuild(terrain, changed.Where(key => key >> 32 == 0).Select(key => (int)(uint)key).ToList());
            // The walls laid again whole; only the chunks whose pieces changed are made again.
            wallsView?.Lay(WallLayout.Of(terrain, plan));
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
            // A slab blown through since: the storeys' floors and decks again, with its hole; the parapets as they are.
            int holes = Holes();
            if (holes != holesMade)
            {
                holesMade = holes;
                var (storeys, decks) = BuildingSurfaces.Upper(terrain, plan);
                Upper(storeys, decks);
            }
            Parapets();
            DrawPieces();
            Furnish();
            Night();
            var done = new Dictionary<int, HashSet<int>>();
            foreach (int k in redo.Select(key => (int)(key >> 32)).Distinct()) done[k] = new HashSet<int>();
            foreach (long key in redo)
            {
                int k = (int)(key >> 32), i = (int)(uint)key;
                Decor(k, i, done[k]);
            }
            Woods(redo);
            return redo.Count;
        }

        /// <summary>`o` noted as standing on those tiles of storey k.</summary>
        void Own(GameObject o, int k, IEnumerable<int> tiles)
        {
            if (o == null) return;
            var keys = tiles.Select(i => Key(k, i)).ToList();
            // On a building's storey: it fades with the storey in the cutaway (floorFadePatch).
            int first = tiles.FirstOrDefault();
            if (k > 0 && plan != null && first >= 0 && first < plan.Owner.Length && plan.Owner[first] >= 0) Cutaway.Current?.Track(o, plan.Owner[first], k);
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

        int GroundAt(int i) => s.Ground != null && i < s.Ground.Count ? s.Ground[i] : 0;

        /// <summary>Storey k's tiles (the ground's, or a level's: null where it has no slab).</summary>
        IList<string> TilesOf(int k) => TilesOf(s, k);

        static IList<string> TilesOf(GameState state, int k) => k == 0 ? (IList<string>)state.Tiles : state.Levels[k - 1].Tiles;

        List<int> DecorOf(int k) => DecorOf(s, k);

        static List<int> DecorOf(GameState state, int k) => (k == 0 ? state.Decor : state.Levels[k - 1].Decor) ?? new List<int>();

        /// <summary>A building's colours on its walls (BuildingLooks: outer and inner), once a building.</summary>
        (Vector3 outer, Vector3 inner) LooksOf(int b)
        {
            if (looks.TryGetValue(b, out var l)) return l;
            var of = BuildingLooks.Of(plan.Buildings[b]);
            return looks[b] = (new Vector3((float)of.outer.r, (float)of.outer.g, (float)of.outer.b), new Vector3((float)of.inner.r, (float)of.inner.g, (float)of.inner.b));
        }

        /// <summary>
        /// The buildings' floors, roofs, stairs and the tops of raised ground (BuildingSurfaces, buildings.ts buildStatic),
        /// what stands on the roofs (roofProps) and the parapets of flats (layParapets), in Bastion/Building's swatches:
        /// the ground floor's and raised ground's on the ground storey, each storey's floor and stairs on its own (so the
        /// storey in view hides what is above it), the roofs on Roofs, a deck one walks on on its storey.
        /// </summary>
        void BuildingParts()
        {
            var set = BuildingSurfaces.Of(terrain, plan);
            // The cutaway of these buildings, measured to their roofs (Building.top, just set).
            (root.GetComponent<Cutaway>() ?? root.gameObject.AddComponent<Cutaway>()).Reset(plan);
            var catalog = PieceCatalog.Load();
            Draw(Storeys[0], "Suelos", set.Floors, catalog?.floors, false);
            Draw(Storeys[0], "Terreno alto", set.Raised, catalog?.floors, false, weathered: true);
            Draw(Storeys[0], "Macizos", set.Solid, catalog?.floors, true);
            Draw(Roofs, "Tejados", set.Roofs, catalog?.roofs, true, fades: true, weathered: true);
            holesMade = Holes();
            Upper(set.Storeys, set.Decks);
            Parapets();
            foreach (var b in plan.Buildings)
            {
                if (b.kind == BuildingKinds.Ruin) continue;
                var parent = b.walkableRoof ? Storeys[Mathf.Clamp(b.storeys, 0, Storeys.Length - 1)] : Roofs;
                foreach (var r in BuildingSurfaces.RoofProps(b, (x, y) => BuildingSurfaces.DeckAt(plan, b, x, y)))
                    Cutaway.Current?.Track(PlaceGame(parent, r.id, Matrix4x4.Translate(new Vector3((float)r.x, (float)r.y, (float)r.z)) * Pieces.RotationY((float)r.yaw)), b.index, b.storeys);
            }
        }

        /// <summary>How many slabs have been blown through (BuildingPlan.Holed).</summary>
        int Holes()
        {
            int n = 0;
            for (int k = 1; k <= (s.Levels?.Count ?? 0); k++)
                for (int i = 0; i < s.Width * s.Height; i++)
                    if (plan.Holed(i, k)) n++;
            return n;
        }

        /// <summary>The storeys' floors and stairs, each on its storey (what is above the top one with it), and the decks; those there were go.</summary>
        void Upper(Surfaces storeys, Surfaces decks)
        {
            foreach (var o in upper) DestroyNow(o);
            upper.Clear();
            var catalog = PieceCatalog.Load();
            for (int k = 0; k < Storeys.Length; k++)
            {
                int level = k;
                bool Here(int l) => l == level || (level == Storeys.Length - 1 && l > level);
                upper.Add(Draw(Storeys[k], "Plantas", storeys, catalog?.floors, true, Here, true));
                upper.Add(Draw(Storeys[k], "Azoteas", decks, catalog?.roofs, true, Here, true, true));
            }
            upper.RemoveAll(o => o == null);
        }

        /// <summary>
        /// The parapets along the roof edges of flats (buildings.ts layParapets): on raised flats' low cover with nothing
        /// else on it, brick to the parapet's height and a stone coping, on every side above lower ground.
        /// </summary>
        void Parapets()
        {
            DestroyNow(parapets);
            int w = s.Width, h = s.Height;
            int Elev(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || y * w + x >= s.Elev.Count ? 0 : s.Elev[y * w + x];
            var surfaces = new Surfaces();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    bool holds = plan.Raised[i] == BuildingPlan.RaisedFlats && s.Tiles[i] == TileKinds.Low
                        && (s.Decor == null || i >= s.Decor.Count || Bastion.Engine.Decor.DecorOf(s.Decor[i]) == null);
                    if (!holds) continue;
                    int e = Elev(x, y);
                    foreach (var d in Bastion.Engine.Grid.Dirs4)
                    {
                        if (Elev(x + d.X, y + d.Y) >= e) continue;
                        double ex = x + 0.5 + d.X * 0.5, ez = y + 0.5 + d.Y * 0.5, top = e * BuildingSurfaces.Storey;
                        double ax = d.X != 0 ? 0 : 0.5, az = d.X != 0 ? 0.5 : 0;
                        double ix = -d.X * BuildingSurfaces.ParapetThick, iz = -d.Y * BuildingSurfaces.ParapetThick;
                        var lo = new Vec3d(Math.Min(ex - ax, ex + ix), top, Math.Min(ez - az, ez + iz));
                        var hi = new Vec3d(Math.Max(ex + ax, ex + ix), top + BuildingSurfaces.Parapet, Math.Max(ez + az, ez + iz));
                        surfaces.Box(lo, hi, Swatch.Flat, BuildingSurfaces.Brick, -1);
                        surfaces.Box(new Vec3d(lo.x - 0.03, hi.y, lo.z - 0.03), new Vec3d(hi.x + 0.03, hi.y + 0.07, hi.z + 0.03), Swatch.Flat, BuildingSurfaces.Stone, -1);
                    }
                }
            parapets = Draw(walls[0], "Pretiles", surfaces, PieceCatalog.Load()?.floors, true);
        }

        /// <summary>
        /// Surfaces as a mesh in Unity's world (metres, z the other way, so every triangle turned round to keep its front),
        /// with what Bastion/Building's swatches read: the plane (TEXCOORD0), the patch (1), the colour (2), and with
        /// `fades` the building (+1) and storey they fade with in the cutaway (3), and whether they are `weathered` (roofs:
        /// weatherPatch); only the triangles of the storeys `keep`
        /// takes, if it is given. Null if there are none.
        /// </summary>
        static GameObject Draw(Transform parent, string name, Surfaces from, Material material, bool shadows, Func<int, bool> keep = null, bool fades = false, bool weathered = false)
        {
            if (from == null || from.Empty || material == null) return null;
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var planes = new List<Vector2>();
            var patches = new List<Vector4>();
            var colours = new List<Vector4>();
            var cuts = new List<Vector4>();
            int n = from.Vertices;
            var corners = new int[3];
            for (int t = 0; t + 2 < n; t += 3)
            {
                if (keep != null && !keep(Mathf.RoundToInt(from.Levels[t]))) continue;
                corners[0] = t;
                corners[1] = t + 2;
                corners[2] = t + 1;
                foreach (int v in corners)
                {
                    positions.Add(new Vector3(from.Position[v * 3] * MapSpace.Tile, from.Position[v * 3 + 1] * MapSpace.Tile, -from.Position[v * 3 + 2] * MapSpace.Tile));
                    normals.Add(new Vector3(from.Normal[v * 3], from.Normal[v * 3 + 1], -from.Normal[v * 3 + 2]));
                    planes.Add(new Vector2(from.Plane[v * 2], from.Plane[v * 2 + 1]));
                    patches.Add(new Vector4(from.Patch[v * 4], from.Patch[v * 4 + 1], from.Patch[v * 4 + 2], from.Patch[v * 4 + 3]));
                    colours.Add(new Vector4(from.Color[v * 3], from.Color[v * 3 + 1], from.Color[v * 3 + 2], 1));
                    cuts.Add(new Vector4(from.Roof[v], from.Levels[v], fades ? 1 : 0, weathered ? 1 : 0));
                }
            }
            if (positions.Count == 0) return null;
            var mesh = new Mesh { name = name, indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, planes);
            mesh.SetUVs(1, patches);
            mesh.SetUVs(2, colours);
            mesh.SetUVs(3, cuts);
            mesh.SetTriangles(Enumerable.Range(0, positions.Count).ToArray(), 0);
            mesh.RecalculateBounds();
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return o;
        }

        /// <summary>A catalog model at 0.10.1's matrix (tiles): its placement turned into Unity's (S·m·S, metres) over the model as Place sets it up.</summary>
        GameObject PlaceGame(Transform parent, string id, Matrix4x4 game)
        {
            var o = Place(parent, id, Vector3.zero, 0);
            if (o == null) return null;
            var flip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            var m = flip * WallsView.InMetres(game) * flip;
            o.transform.localPosition = m.GetPosition();
            o.transform.localRotation = m.rotation;
            o.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
            return o;
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
            // The base's pieces are drawn whole (DrawPieces), and the Bastion's yard with it.
            if (k == 0 && pieces != null && ((pieces.at[i] >= 0 && BasePieces.Drawn.Contains(pieces.spots[pieces.at[i]].piece.Piece)) || pieces.yard.Has(i))) return;
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
            if (Fountain(k, i, look, tiles, decor, done)) return;
            // The base's pines are its own (Woods).
            if (pieces != null && tile == "high" && look.Kind == DecorKinds.Tree) return;
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
            if (Furnished(k, i, look, low, turn, floor)) return;
            var ids = MapArt.Set(set);
            string id = ids[Mathf.Min(ids.Length - 1, Mathf.FloorToInt(MapArt.Hash(x, y, 1) * ids.Length))];
            Own(Place(props[k], id, new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), -yaw), k, new[] { i });
        }

        /// <summary>A square's fountain (mapView.ts FOUNTAIN): its rim's pieces low, the statue in the middle of a big one high.</summary>
        static readonly Dictionary<string, string> FountainParts = new Dictionary<string, string> { ["fountain"] = "low", ["fountainCorner"] = "low", ["statue"] = "high" };

        /// <summary>
        /// A square's fountain (mapView.ts): the basin whole over its 3 × 3 (the statue its top) or its 2 × 2, while all of
        /// it stands, from the middle of its block; shot up, its pieces are rubble (MapArt.DecorSet). By the forest road the
        /// statue is the lumberjack's.
        /// </summary>
        bool Fountain(int k, int i, DecorLook look, IList<string> tiles, IList<int> decor, HashSet<int> done)
        {
            if (!FountainParts.ContainsKey(look.Kind) || (look.Kind == "statue" && s.Site == "roadside")) return false;
            int w = s.Width, h = s.Height;
            var block = new List<int> { i };
            var seen = new HashSet<int> { i };
            for (int n = 0; n < block.Count; n++)
            {
                int tx = block[n] % w, ty = block[n] / w;
                foreach (var d in Bastion.Engine.Grid.Dirs4)
                {
                    int nx = tx + d.X, ny = ty + d.Y, j = ny * w + nx;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen.Contains(j) || j >= decor.Count) continue;
                    var other = Bastion.Engine.Decor.DecorOf(decor[j]);
                    if (other == null || !FountainParts.ContainsKey(other.Kind)) continue;
                    seen.Add(j);
                    block.Add(j);
                }
            }
            int x0 = block.Min(t => t % w), y0 = block.Min(t => t / w);
            int bw = block.Max(t => t % w) - x0 + 1, bh = block.Max(t => t / w) - y0 + 1;
            bool whole = block.All(t => t < tiles.Count && tiles[t] == FountainParts[Bastion.Engine.Decor.DecorOf(decor[t]).Kind]);
            if (!whole || bw != bh || bw < 2) return false;
            int anchor = (y0 + bh / 2) * w + x0 + bw / 2;
            if (!done.Add(anchor)) return true;
            var basin = Place(props[k], "fountainWhole", new Vector3((x0 + bw / 2f) * MapSpace.Tile, k * MapSpace.Storey, -(y0 + bh / 2f) * MapSpace.Tile), 0);
            if (basin != null) basin.transform.localScale = Vector3.one * (bw / 3f);
            Own(basin, k, block);
            return true;
        }

        /// <summary>A supermarket's shelving as stocked as it is: the odds of each look (full, gaps, bare, empty) by stock (mapView.ts SHELF_LOOKS).</summary>
        static readonly float[][] ShelfLooks = { new[] { 0.6f, 0.4f, 0, 0 }, new[] { 0.1f, 0.3f, 0.4f, 0.2f }, new[] { 0, 0.1f, 0.45f, 0.45f } };
        static readonly string[] Gondolas = { "gondola", "gondolaGaps", "gondolaBare", "gondolaEmpty" };
        static readonly string[] WallShelves = { "wallShelf", "wallShelfGaps", "wallShelfBare", "wallShelfEmpty" };
        /// <summary>A grocer's shelves: full, picked over or stripped, these odds.</summary>
        static readonly float[] GrocerShelf = { 0.3f, 0.45f, 0.25f };
        /// <summary>The shelving stretched up from its models' height at a tile long (to 1.35 m), a row's low end to 0.75; a wall shelf this far back (tiles).</summary>
        const float ShelfTall = 1.36f, EndTall = 1.35f, WallShelfBack = 0.28f;

        /// <summary>
        /// Furniture as its building is (mapView.ts addDecor): a shop's counter its own; a supermarket's shelving, gondolas
        /// with goods both ways, along its walls wall shelves, a row's low end a display turned to the checkouts; a
        /// grocer's (its FOOD sign) the supermarket's gondolas; tall shelves a house's or an office's (or a bookshop's)
        /// bookcases, else shelving; a tall cabinet. Not yet: what fell off a supermarket's shelves.
        /// </summary>
        bool Furnished(int k, int i, DecorLook look, bool low, int turn, float floor)
        {
            if (look.Kind != "counter" && look.Kind != "shelf" && look.Kind != "cabinet") return false;
            int x = i % s.Width, y = i / s.Width;
            float cx = x + 0.5f, cz = y + 0.5f;
            int b = plan != null && i < plan.Owner.Length ? plan.Owner[i] : -1;
            var bd = b >= 0 && b < plan.Buildings.Count ? plan.Buildings[b] : null;
            string id;
            float tall = 1;
            int quarter = turn;
            if (look.Kind == "counter")
            {
                if (!low) return false;
                id = bd != null && bd.kind != BuildingKinds.House ? "shopCounter" : "counter";
            }
            else if (look.Kind == "shelf" && bd?.market != null)
            {
                var back = Bastion.Engine.Grid.Dirs4[(look.Facing + 2) % 4];
                int at = (y + back.Y) * s.Width + x + back.X;
                string behind = at >= 0 && at < s.Tiles.Count ? s.Tiles[at] : null;
                bool wall = behind == "wall" || behind == "window" || behind == "windowBroken";
                var odds = ShelfLooks[MarketStock(x, y)];
                float pick = MapArt.Hash(x, y, 80);
                int n = 0;
                while (n < 3 && pick >= odds[n]) pick -= odds[n++];
                id = low ? "endDisplay" : (wall ? WallShelves : Gondolas)[n];
                quarter = low ? (4 - bd.market.Value) % 4 : turn;
                tall = low ? EndTall : ShelfTall;
                float push = wall && !low ? WallShelfBack : 0;
                cx += back.X * push;
                cz += back.Y * push;
            }
            else if (look.Kind == "shelf" && !low && bd?.kind == BuildingKinds.Shop && WallLayout.ShopSign(s, bd) == "grocer")
            {
                float pick = MapArt.Hash(x, y, 80);
                int n = 0;
                while (n < 2 && pick >= GrocerShelf[n]) pick -= GrocerShelf[n++];
                id = Gondolas[n];
                tall = ShelfTall;
            }
            else if (!low)
            {
                bool books = bd != null && (bd.kind == BuildingKinds.House || bd.kind == BuildingKinds.Office || (bd.kind == BuildingKinds.Shop && ShopTradeIn(bd) == "books") || (bd.kind == BuildingKinds.Hall && bd.look < 0.3));
                id = look.Kind == "shelf" ? (books ? "bookshelf" : "shelfHigh") : "cabinet";
            }
            else return false;
            var placed = Place(props[k], id, new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), -quarter * 90);
            if (placed != null) placed.transform.localScale = new Vector3(1, tall, 1);
            Own(placed, k, new[] { i });
            return true;
        }

        /// <summary>A supermarket's stock where (x, y) is (mapView.ts marketStock): 0 stocked, 1 stocked but searched since, 2 stripped.</summary>
        int MarketStock(int x, int y)
        {
            var m = s.Markets?.FirstOrDefault(q => x >= q.X && x < q.X + q.W && y >= q.Y && y < q.Y + q.H);
            if (m == null || m.Stock != MarketStocks.Full) return 2;
            LootRoom room = null;
            for (int n = 0; n < m.W * m.H && room == null; n++) room = Looting.RoomAt(s, new Pos { X = m.X + n % m.W, Y = m.Y + n / m.W, Z = 0 });
            return room != null && s.RoomsSearched != null && s.RoomsSearched.Contains(room.Id) ? 1 : 0;
        }

        /// <summary>A shop's trade as its floor shows it (homeFurnishing.ts shopTradeIn), else by its looks.</summary>
        string ShopTradeIn(Building b)
        {
            for (int y = b.y0; y <= b.y1; y++)
                for (int x = b.x0; x <= b.x1; x++)
                {
                    int i = y * s.Width + x;
                    string kind = s.Decor != null && i < s.Decor.Count ? Bastion.Engine.Decor.DecorOf(s.Decor[i])?.Kind : null;
                    if (kind == "cafeTable") return "cafe";
                    if (kind == "readingTable") return "books";
                    if (kind == "drinksFridge") return "grocer";
                }
            return b.look < 0.3 ? "books" : b.look < 0.55 ? "cafe" : "grocer";
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
            if (placed != null && thing.Kind == "carFront")
            {
                Repaint(placed, Mathf.FloorToInt(MapArt.Hash(ax, ay, 13) * CarPaints.Length) % CarPaints.Length);
                // Some wrecks still have a battery: their headlights on, by night, at the nose.
                if (MapArt.Hash(ax, ay, 9) < 0.25f)
                {
                    var d = Bastion.Engine.Grid.Dirs4[thing.Facing];
                    float nose = (MapArt.FootprintOf(id)?.fit ?? 2.95f) / 2 - 0.055f;
                    headlights[thing.Anchor] = new NightLights.Glow
                    {
                        at = new Vector3((cx + d.X * nose) * MapSpace.Tile, floor + 0.32f * MapSpace.Tile, -(cz + d.Y * nose) * MapSpace.Tile),
                        facing = new Vector3(d.X, 0, -d.Y), power = 1,
                    };
                }
            }
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
        /// <summary>Each windy material (Nature's trees and vines sway with the game's clock), and its copy that stands still.</summary>
        static readonly Dictionary<Material, Material> stills = new Dictionary<Material, Material>();
        static readonly string[] Winds = { "_Big_Wind_Amount", "_Small_Wind_Amount", "_Big_WindAmount", "_Small_WindAmount", "_Leaves_WindAmount", "_Leaves_Wind_Amount" };

        /// <summary>
        /// A model's windy materials swapped for still ones: 0.10.1's trees don't sway, and in play mode the sway of
        /// SyntyStudios_Trees came out as no number (24/10: the pines black there, in the clearing and in a town's square;
        /// SSAO took it round them, and the props near them came out red). Each material's copy is made once.
        /// </summary>
        static void Still(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                bool swapped = false;
                for (int k = 0; k < materials.Length; k++)
                {
                    var m = materials[k];
                    if (m == null || !Winds.Any(m.HasProperty)) continue;
                    if (!stills.TryGetValue(m, out var still) || still == null)
                    {
                        still = new Material(m) { name = m.name + " (quieto)" };
                        foreach (string w in Winds) if (still.HasProperty(w)) still.SetFloat(w, 0);
                        stills[m] = still;
                    }
                    materials[k] = still;
                    swapped = true;
                }
                if (swapped) r.sharedMaterials = materials;
            }
        }

        GameObject Place(Transform parent, string id, Vector3 at, float yaw, MapArt.Footprint footprint = null)
        {
            var art = MapArt.Prop(id);
            if (art == null || string.IsNullOrEmpty(art.fbx))
            {
                Missing.Add(id);
                return null;
            }
            var prefab = prefabOf(MapArt.ModelKey(art));
            if (prefab == null)
            {
                Missing.Add($"{id} ({art.pack}/{art.fbx})");
                return null;
            }
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(prefab, holder, false);
            Still(model);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Its box as it stands, then the longest side to `fit` tiles; centred (or on its origin) and on the floor.
            var bounds = BoundsIn(model, holder);
            var size = bounds.size;
            float fit = footprint != null && art.fit > 0 ? footprint.fit : art.fit;
            float along = fit > 0 ? fit * MapSpace.Tile / Mathf.Max(size.x, size.z, 1e-4f) : 1;
            float across = (art.stretch ? 1 : along) * (art.thick > 0 ? art.thick : 1) * (footprint?.thin ?? 1);
            var scale = size.x >= size.z ? new Vector3(along, across, across) : new Vector3(across, across, along);
            // A deck one stands on, one storey up whatever its width took (a guard tower's on its legs).
            if (art.deck > 0) scale.y = MapSpace.Storey / art.deck;
            if (art.tall > 0) scale.y = art.tall * MapSpace.Tile / Mathf.Max(size.y, 1e-4f);
            var centre = art.origin ? Vector3.zero : bounds.center;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, scale);
            model.transform.localPosition = new Vector3(-centre.x * scale.x, -bounds.min.y * scale.y - art.sink * MapSpace.Tile, -centre.z * scale.z);
            // Its own turn to face north (0.10.1's quarter turns, the other way round here), then where it goes.
            var turned = new GameObject("turn").transform;
            turned.SetParent(holder, false);
            turned.localRotation = Quaternion.Euler(0, -(art.yaw - MapArt.UnityTurn(art)) * 90, 0);
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
    }
}
