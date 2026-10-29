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
    public sealed partial class MapBuilder
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

        /// <summary>The Synty materials the map asks `materialOf` for (the cars' paints, the containers' colours: MapArt.Atlases), for the game's catalog (MapCatalog).</summary>
        public static IEnumerable<string> MaterialsUsed => CarPaints.Concat(WholePaints.Values.SelectMany(p => p.paints)).Where(m => m != null).Concat(MapArt.Atlases.Values.Select(a => a.to));
        const string CarRed = "PolygonApocalypse_01_A";
        /// <summary>
        /// The whole cars' paints, by pack (Unity's own, Ana 28/10): the material their models come in, and its alternates
        /// (Synty's own: City's and Town's atlases, Apocalypse's vehicle sheets), one of them by the car's tile; without, all
        /// of a pack came in the same blue. Not City's and Town's lime green and orange (_03_C, _01_B) nor Apocalypse's purple
        /// (Vehicle_04): too loud for a town left behind (Gameplay 28/10).
        /// </summary>
        static readonly Dictionary<string, (string from, string[] paints)> WholePaints = new Dictionary<string, (string, string[])>
        {
            ["city"] = ("PolygonCity_01_A", new[] { null, "PolygonCity_01_C", "PolygonCity_02_A", "PolygonCity_02_B", "PolygonCity_03_A", "PolygonCity_04_A", "PolygonCity_04_B" }),
            ["town"] = ("PolygonTown_01_A", new[] { null, "PolygonTown_01_C", "PolygonTown_02_A", "PolygonTown_02_B", "PolygonTown_03_A", "PolygonTown_04_A", "PolygonTown_04_B" }),
            ["apocalypse"] = ("Vehicle_Standard_01", new[] { null, "Vehicle_01", "Vehicle_02", "Vehicle_03", "Vehicle_05", "Vehicle_06" }),
        };
        /// <summary>The street's wrecks (MapArt.json's carFront, 0.10.1's): the others are whole cars (Unity's own).</summary>
        static readonly HashSet<string> Wrecks = new HashSet<string> { "carWreck", "carRusted", "carSuv", "carOpen" };
        static readonly bool CarsLogged = Environment.GetEnvironmentVariable("BASTION_COCHES") == "1";

        /// <summary>A car in paint `share` (0 to 1) of its own: a wreck's (CarPaints) or a whole one's by its pack (WholePaints).</summary>
        void Repaint(GameObject car, string id, float share)
        {
            string from = CarRed, paint;
            if (Wrecks.Contains(id)) paint = CarPaints[Mathf.FloorToInt(share * CarPaints.Length) % CarPaints.Length];
            else
            {
                var paints = WholePaintsOf(id);
                if (paints.Length == 0) return;
                paint = paints[Mathf.FloorToInt(share * paints.Length) % paints.Length];
                if (!PaintWhole(car, id, paint, materialOf)) Missing.Add($"{id} ({paint})");
                return;
            }
            if (paint == null || materialOf == null) return;
            var to = materialOf(paint);
            if (to == null)
            {
                Missing.Add($"{id} ({paint})");
                return;
            }
            foreach (var r in car.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) if (mats[k] != null && mats[k].name.StartsWith(from)) mats[k] = to;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>The paints whole car `id` comes in (null: its model's own), by its pack; none, not a whole car.</summary>
        public static string[] WholePaintsOf(string id) =>
            !Wrecks.Contains(id) && MapArt.Prop(id) is PropArt art && art.pack != null && WholePaints.TryGetValue(art.pack, out var whole) ? whole.paints : Array.Empty<string>();

        /// <summary>
        /// Whole cars worn (Bastion/Car, Gameplay 28/10: «parecen recién salidos del concesionario»): each paint's copy with
        /// its colour taken down and dust over it. Off, Synty's paint as it is (a lab's, to set them side by side).
        /// </summary>
        public static bool Worn = true;
        static readonly Dictionary<Material, Material> worn = new Dictionary<Material, Material>();

        /// <summary>Whole car `car` (prop `id`) in paint `paint` (null: its own), worn; false if the paint isn't there.</summary>
        public static bool PaintWhole(GameObject car, string id, string paint, Func<string, Material> materialOf)
        {
            if (!(MapArt.Prop(id) is PropArt art) || art.pack == null || !WholePaints.TryGetValue(art.pack, out var whole)) return false;
            var to = paint != null && materialOf != null ? materialOf(paint) : null;
            if (paint != null && to == null) return false;
            foreach (var r in car.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++)
                    if (mats[k] != null && mats[k].name.StartsWith(whole.from))
                    {
                        var own = to ?? mats[k];
                        mats[k] = Worn ? WornOf(own) : own;
                    }
                r.sharedMaterials = mats;
            }
            return true;
        }

        /// <summary>Paint `paint`'s worn copy (Kit/Car with its textures), made the first time.</summary>
        static Material WornOf(Material paint)
        {
            if (worn.TryGetValue(paint, out var m) && m != null) return m;
            m = KitMaterials.New(KitMaterials.Car, paint.name + " (gastado)");
            if (m == null) return paint;
            m.SetTexture("_BaseMap", paint.HasProperty("_Albedo_Map") ? paint.GetTexture("_Albedo_Map") : paint.mainTexture);
            var normals = paint.HasProperty("_Normal_Map") ? paint.GetTexture("_Normal_Map") : null;
            if (normals != null) m.SetTexture("_BumpMap", normals);
            m.SetFloat("_NormalAmount", normals != null && paint.HasProperty("_Normal_Amount") ? paint.GetFloat("_Normal_Amount") : 0);
            return worn[paint] = m;
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
        /// <summary>What the base's pieces put where, as last drawn (0.10.1's tiles: the clearing's fires and seats, Settlement).</summary>
        public IReadOnlyList<PiecePlacement> PiecesPlaced => piecesPlaced;
        List<PiecePlacement> piecesPlaced = new List<PiecePlacement>();
        /// <summary>A siege's doors' and barricades' dressing as drawn (BasePieces.DrawSiege), and what it was: drawn again when it changes.</summary>
        readonly List<GameObject> siegeArt = new List<GameObject>();
        string siegeDrawn;
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
            MedicalFinish();
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
            DrawSiege();
            DrawDropped();
            Woods(null);
            Beyond();
            Furnish();
            Dressing();
            Night();
            // A winter's map: the snow on what faces up (snow.ts).
            if (Mood.Snowy(s))
            {
                snow = SnowCover.Make(root, s, plan, prefabOf);
                SnowCover.Mark(root, plan, s, GroundPlane());
            }
        }

        /// <summary>The snow on a winter's map (SnowCover), or none.</summary>
        SnowCover snow;

        /// <summary>The ground's own plane (UI's GroundView «Suelo»): it has its own snow.</summary>
        Transform GroundPlane() => Storeys.Length > 0 ? Storeys[0].GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Suelo") : null;

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
                r.sharedMaterial = Windy(kv.Key.material) ? InWind(kv.Key.material, 0) : kv.Key.material;
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
            piecesPlaced = new List<PiecePlacement>();
            if (pieces == null) return;
            var placements = new List<PiecePlacement>();
            foreach (var spot in pieces.spots)
            {
                if (!BasePieces.Drawn.Contains(spot.piece.Piece)) continue;
                int wear = BasePieces.PieceWear(s, spot);
                if (spot.piece.Piece == PieceIds.Sandbags) BasePieces.DrawSandbags(s, pieces, spot, wear, placements);
                else BasePieces.DrawPiece(s, pieces, spot, wear, Measure, placements);
            }
            piecesPlaced = placements;
            foreach (var p in placements)
            {
                var o = PlaceGame(props[0], p.id, p.matrix);
                if (o == null) continue;
                if (!p.shadows) foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pieceArt.Add(o);
            }
            Debug.Log($"[map] the base's pieces: {string.Join(", ", placements.GroupBy(p => p.id).Select(g => $"{g.Key} ×{g.Count()}"))}");
        }

        /// <summary>
        /// A siege's doors and barricades on the hand-made map (mapView.ts build: sandbags, fallenDoor, a door's grit), as
        /// worn as they are; all drawn again when one wears a stage further or gives way (a few dozen models).
        /// </summary>
        void DrawSiege()
        {
            var placements = new List<PiecePlacement>();
            BasePieces.DrawSiege(s, pieces, plan?.Owner, placements);
            string key = string.Join(";", placements.Select(p => p.id + p.matrix.ToString("F3")));
            if (key == siegeDrawn) return;
            siegeDrawn = key;
            foreach (var o in siegeArt) DestroyNow(o);
            siegeArt.Clear();
            foreach (var p in placements)
            {
                var o = PlaceGame(props[0], p.id, p.matrix);
                if (o == null) continue;
                if (!p.shadows) foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                siegeArt.Add(o);
            }
            if (placements.Count > 0) Debug.Log($"[map] the siege's doors and barricades: {string.Join(", ", placements.GroupBy(p => p.id).Select(g => $"{g.Key} ×{g.Count()}"))}");
        }

        /// <summary>
        /// A piece alone (clearingArt.ts pieceModel, pieceGroup): its models as BasePieces draws them, whole, turned `rot`
        /// (at `level` if a building), round its footprint's middle at `parent`'s origin (metres); sandbags as their
        /// barricade's. None casts a shadow. For the one about to go down, a line of them, the partner's in hand and a
        /// proposal (Settlement.Ghost). Null if it has no model.
        /// </summary>
        public GameObject PieceAlone(string id, int rot, int level, Transform parent)
        {
            if (!Bastion.Engine.Base.Pieces.TryGetValue(id, out var def)) return null;
            int across = def.Size.Count > 0 ? def.Size[0] : 1, deep = def.Size.Count > 1 ? def.Size[1] : 1;
            int w = rot % 2 != 0 ? deep : across, h = rot % 2 != 0 ? across : deep;
            int width = w + 2, height = h + 2;
            var piece = new BasePiece { Id = "ghost", Piece = id, X = 1, Y = 1, Rot = rot, Hp = def.Hp };
            if (def.Kind == PieceKinds.Building) piece.Level = level;
            if (def.Charges != null) piece.Charges = def.Charges;
            var alone = new GameState
            {
                Width = width,
                Height = height,
                Tiles = Enumerable.Repeat(TileKinds.Floor, width * height).ToList(),
                Elev = Enumerable.Repeat(0, width * height).ToList(),
                Base = new SiegeBase { Pieces = new List<BasePiece> { piece } },
            };
            var map = BasePieces.MapOf(alone);
            if (map == null || map.spots.Count == 0) return null;
            var spot = map.spots[0];
            var placements = new List<PiecePlacement>();
            if (id == PieceIds.Sandbags) BasePieces.DrawSandbags(alone, map, spot, 0, placements);
            else BasePieces.DrawPiece(alone, map, spot, 0, Measure, placements);
            var root = new GameObject("Pieza sola " + id);
            root.transform.SetParent(parent, false);
            // Its footprint's middle (0.10.1's tiles, z south) at the origin.
            var inner = new GameObject("modelos").transform;
            inner.SetParent(root.transform, false);
            inner.localPosition = new Vector3(-(float)spot.x * MapSpace.Tile, 0, (float)spot.z * MapSpace.Tile);
            foreach (var p in placements)
            {
                var o = PlaceGame(inner, p.id, p.matrix);
                if (o == null) continue;
                foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        /// <summary>Prop `id`'s model alone under `parent`, set up as the map places it (fitted, turned to face north, on its floor); null if it has none.</summary>
        public GameObject PropAlone(string id, Transform parent)
        {
            var o = Place(parent, id, Vector3.zero, 0);
            if (o == null) return null;
            Placed--;
            o.transform.localPosition = Vector3.zero;
            o.transform.localRotation = Quaternion.identity;
            return o;
        }

        /// <summary>
        /// Prop `id`'s parts' box in its own frame as placed (0.10.1's tiles: x east, y up, z south), as clearingArt.ts propBox
        /// read it; empty if it has no model.
        /// </summary>
        public ClearingLife.PropBox BoxOf(string id)
        {
            if (boxes.TryGetValue(id, out var b)) return b;
            var o = Place(root, id, Vector3.zero, 0);
            if (o == null) return boxes[id] = ClearingLife.PropBox.Empty;
            o.transform.SetParent(null, false);
            o.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Placed--;
            bool any = o.GetComponentInChildren<Renderer>() != null;
            var u = BoundsIn(o, o.transform);
            DestroyNow(o);
            if (!any) return boxes[id] = ClearingLife.PropBox.Empty;
            float k = MapSpace.Tile;
            // (Unity's z is 0.10.1's the other way.)
            return boxes[id] = new ClearingLife.PropBox(u.min.x / k, u.min.y / k, -u.max.z / k, u.max.x / k, u.max.y / k, -u.min.z / k);
        }

        readonly Dictionary<string, ClearingLife.PropBox> boxes = new Dictionary<string, ClearingLife.PropBox>();

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

        readonly List<GameObject> dressed = new List<GameObject>();
        /// <summary>The clinics' scenery that lies flat (mapView.ts MEDICAL_FLAT: no shadow), and what lies about at all (MEDICAL_SCATTER).</summary>
        static readonly HashSet<string> MedicalFlat = new HashSet<string> { "bloodPool", "bloodDrops", "floorGrime", "scatteredPapers" };
        static readonly HashSet<string> MedicalScatter = new HashSet<string> { "bloodPool", "bloodDrops", "floorGrime", "scatteredPapers", "medicalWaste", "overturnedChair", "fallenIV", "spilledSupplies", "rumpledLinen" };

        /// <summary>
        /// The clinics' and the houses' dressing (mapView.ts, playtest 18/10 #15: the details, the bodies, blood and mess left
        /// in them; GameState medical and homes, Dressing): each kept off where what it lies on or hangs from is gone, a
        /// bed's frame and a screen over a bed's two tiles, a body a Synty character baked lying (a prop), the rest
        /// MedicalDetails' shapes, merged by storey and building. Made again whole when the map changes.
        /// </summary>
        Material dirtMaterial;

        void Dressing()
        {
            foreach (var o in dressed) DestroyNow(o);
            dressed.Clear();
            var faces = new Dictionary<(int k, int b, bool cast, bool dirt), List<(MedicalDetails.Face face, Matrix4x4 at)>>();
            float storey = MapSpace.Storey / MapSpace.Tile;
            var material = Resources.Load<Material>("FrameShape");
            // The floor's grime see-through (mapView.ts clinicalDirt: opacity 0.24, no depth written), over the tiles.
            if (dirtMaterial == null && material != null)
            {
                dirtMaterial = new Material(material) { name = "Suciedad", renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent, hideFlags = HideFlags.DontSave };
                dirtMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dirtMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dirtMaterial.SetFloat("_ZWrite", 0);
                dirtMaterial.SetColor("_BaseColor", new Color(1, 1, 1, 0.24f));
                dirtMaterial.SetOverrideTag("RenderType", "Transparent");
            }
            var sites = (s.Medical ?? new List<MedicalSite>()).Select(m => (m.X, m.Y, m.W, m.H, m.Dressing)).Concat((s.Homes ?? new List<HomeSite>()).Select(h => (h.X, h.Y, h.W, h.H, h.Dressing)));
            foreach (var (sx, sy, sw, sh, d) in sites.Where(l => l.Dressing != null).SelectMany(l => l.Dressing.Select(d => (l.X, l.Y, l.W, l.H, d))))
            {
                int i = d.Y * s.Width + d.X;
                if (d.Z < 0 || d.Z >= Storeys.Length || i < 0 || i >= s.Width * s.Height) continue;
                var tiles = TilesOf(d.Z);
                var decor = DecorOf(d.Z);
                string tile = i < tiles.Count ? tiles[i] : null;
                if (tile == null) continue;
                var look = i < decor.Count ? Bastion.Engine.Decor.DecorOf(decor[i]) : null;
                bool bed = d.Kind == "bareBed" || d.Kind == "coveredBed";
                bool body = d.Kind == "corpseCivilian" || d.Kind == "corpseStaff" || d.Kind == "coveredCorpse";
                bool wall = d.Kind == "wallCross" || d.Kind == "cautionSign" || d.Kind == "safeSign" || d.Kind == "wallBlood";
                bool scatter = MedicalScatter.Contains(d.Kind), flat = MedicalFlat.Contains(d.Kind);
                if (bed && (tile != "low" || look?.Kind != "hospitalBed")) continue;
                if (body && tile != "floor") continue;
                if (scatter && tile != "floor" && tile != "low" && tile != "high") continue;
                if (!scatter && !bed && !body && !wall && tile != "low" && tile != "high") continue;
                var normal = Bastion.Engine.Grid.Dirs4[d.Facing];
                int wx = d.X - normal.X, wy = d.Y - normal.Y, wi = wy * s.Width + wx;
                if (wall && (wx < 0 || wy < 0 || wx >= s.Width || wy >= s.Height || wi >= tiles.Count || tiles[wi] != "wall")) continue;
                // On a bed over two tiles, its stripped frame or its body over the whole of it, the screen along it.
                var thing = bed || d.Kind == "privacyScreen" ? Bastion.Engine.Decor.ObjectAt(decor, s.Width, i) : null;
                float yaw = (4 - d.Facing) % 4 * 90f, up = d.Z * storey + (float)d.Height;
                var turn = Quaternion.AngleAxis(yaw, Vector3.up);
                Matrix4x4 at;
                if (thing != null && thing.Tiles.Count == 2)
                {
                    float lx = (thing.Tiles[0] % s.Width + thing.Tiles[1] % s.Width) / 2f, ly = (thing.Tiles[0] / s.Width + thing.Tiles[1] / s.Width) / 2f;
                    var middle = new Vector3(lx + 0.5f, up, ly + 0.5f);
                    at = Matrix4x4.TRS(middle, turn, bed ? new Vector3(1.5f, 1.15f, 1.75f) : new Vector3(1, 1, 1.9f));
                    if (d.Kind == "coveredBed") dressed.Add(Tracked(PlaceGame(props[d.Z], "ivStand", Matrix4x4.TRS(middle, turn, Vector3.one) * Matrix4x4.Translate(new Vector3(-0.45f, 0, 0.6f))), i, d.Z));
                }
                else
                {
                    at = Matrix4x4.TRS(new Vector3(d.X + 0.5f, up, d.Y + 0.5f), turn, Vector3.one);
                    if (d.Kind == "coveredBed") dressed.Add(Tracked(PlaceGame(props[d.Z], "ivStand", at * Matrix4x4.Translate(new Vector3(-0.31f, 0, 0.22f))), i, d.Z));
                }
                if (d.Kind == "corpseCivilian" || d.Kind == "corpseStaff")
                {
                    dressed.Add(Tracked(PlaceGame(props[d.Z], d.Kind, at), i, d.Z));
                    continue;
                }
                int b = i < plan.Owner.Length ? plan.Owner[i] : -1;
                if (wall)
                {
                    // On its own, to go with its wall in the cutaway: an outer wall's the way it looks out (mapView.ts aMedicalWall).
                    bool outside = wx <= sx || wy <= sy || wx >= sx + sw - 1 || wy >= sy + sh - 1;
                    var fixture = Shapes($"Pared {d.Kind}", props[d.Z], MedicalDetails.Of(d.Kind, d.Variant ?? 0).Select(f => (f, at)), material, false);
                    if (fixture != null && b >= 0) Cutaway.Current?.TrackFixture(fixture, b, d.Z, outside ? new Vector2(-normal.X, -normal.Y) : Vector2.zero);
                    if (fixture != null) dressed.Add(fixture);
                    continue;
                }
                var key = (d.Z, b, !flat, d.Kind == "floorGrime");
                if (!faces.TryGetValue(key, out var list)) faces[key] = list = new List<(MedicalDetails.Face, Matrix4x4)>();
                foreach (var f in MedicalDetails.Of(d.Kind, d.Variant ?? 0)) list.Add((f, at));
            }
            foreach (var group in faces)
            {
                var o = Shapes($"Vestido {group.Key.b}{(group.Key.dirt ? " suciedad" : "")}", props[group.Key.k], group.Value, group.Key.dirt ? dirtMaterial : material, group.Key.cast);
                if (o == null) continue;
                if (group.Key.k > 0 && group.Key.b >= 0) Cutaway.Current?.Track(o, group.Key.b, group.Key.k);
                dressed.Add(o);
            }
        }

        /// <summary>MedicalDetails' faces, each placed by its matrix (0.10.1's frame, tiles), as one mesh under `parent`; null if none.</summary>
        static GameObject Shapes(string name, Transform parent, IEnumerable<(MedicalDetails.Face face, Matrix4x4 at)> faces, Material material, bool cast)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            foreach (var (face, at) in faces)
            {
                // In Unity's frame (metres, z north): the mirror turns the winding round (FrameView.Shapes).
                var p = face.points.Select(v => at.MultiplyPoint3x4(v)).Select(v => new Vector3(v.x * MapSpace.Tile, v.y * MapSpace.Tile, -v.z * MapSpace.Tile)).ToArray();
                if (p.Length < 3) continue;
                var n = -Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                int start = vertices.Count;
                foreach (var v in p)
                {
                    vertices.Add(v);
                    normals.Add(n);
                    colours.Add(face.colour);
                }
                for (int k = 1; k + 1 < p.Length; k++)
                {
                    triangles.Add(start);
                    triangles.Add(start + k + 1);
                    triangles.Add(start + k);
                }
            }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return o;
        }

        /// <summary>`o` on storey k of tile i's building fading with it in the cutaway (as Furnish's), and `o` back.</summary>
        GameObject Tracked(GameObject o, int i, int k)
        {
            int b = i >= 0 && i < plan.Owner.Length ? plan.Owner[i] : -1;
            if (o != null && k > 0 && b >= 0) Cutaway.Current?.Track(o, b, k);
            return o;
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
            // Searched since (mapView.ts searchedSeen): the furniture opened, its contents before it, a car's boot up; a
            // room searched, its supermarket's shelves as stocked as they are now.
            int area = next.Width * next.Height;
            foreach (int place in (next.Searched ?? new List<int>()).Except(s.Searched ?? new List<int>()))
                if (place / area < levels) changed.Add(Key(place / area, place % area));
            if ((next.RoomsSearched?.Count ?? 0) != (s.RoomsSearched?.Count ?? 0))
                foreach (var m in next.Markets ?? new List<MarketSite>())
                    for (int y = m.Y; y < m.Y + m.H; y++)
                        for (int x = m.X; x < m.X + m.W; x++)
                            if (x >= 0 && y >= 0 && x < next.Width && y < next.Height) changed.Add(Key(0, y * next.Width + x));
            s = next;
            Terrain();
            plan?.Follow(terrain);
            // (A piece or a barricade worn a stage further changes no tile: the 'smashed' event.)
            DrawPieces();
            DrawSiege();
            DrawDropped();
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
            Furnish();
            Dressing();
            Night();
            var done = new Dictionary<int, HashSet<int>>();
            foreach (int k in redo.Select(key => (int)(key >> 32)).Distinct()) done[k] = new HashSet<int>();
            foreach (long key in redo)
            {
                int k = (int)(key >> 32), i = (int)(uint)key;
                Decor(k, i, done[k]);
            }
            Woods(redo);
            if (snow != null) SnowCover.Mark(root, plan, s, GroundPlane());
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
            // A hack's terminal (mapView.ts addDecor 'console': the objective of a hack): its desk, its back to the wall beside it.
            if (tile == "console")
            {
                int cx0 = i % s.Width, cy0 = i / s.Width;
                string At(int x, int y) => x >= 0 && y >= 0 && x < s.Width && y < s.Height && y * s.Width + x < tiles.Count ? tiles[y * s.Width + x] : null;
                bool alongX = BuildingPlan.IsWallLike(At(cx0 - 1, cy0)) || BuildingPlan.IsWallLike(At(cx0 + 1, cy0));
                var desks = MapArt.Set("console");
                if (desks.Length > 0) Own(Place(props[k], desks[0], new Vector3((cx0 + 0.5f) * MapSpace.Tile, floor, -(cy0 + 0.5f) * MapSpace.Tile), alongX ? 0 : -90), k, new[] { i });
                return;
            }
            if (tile != "low" && tile != "high") return;
            // The base's pieces are drawn whole (DrawPieces), and the Bastion's yard with it.
            if (k == 0 && pieces != null && ((pieces.at[i] >= 0 && BasePieces.Drawn.Contains(pieces.spots[pieces.at[i]].piece.Piece)) || pieces.yard.Has(i))) return;
            // A siege's barricade: its sandbags as worn as it is (DrawSiege).
            if (k == 0 && tile == "low" && s.Mission?.Siege != null && BasePieces.MadeAs(s, i % s.Width, i / s.Width) == 'b') return;
            var look = Bastion.Engine.Decor.DecorOf(decor[i]);
            if (look == null) return;
            // An object over several tiles: one model over all of them, from its anchor (mapView.ts drawThing).
            var thing = Bastion.Engine.Decor.ObjectAt(decor, s.Width, i);
            if (thing != null && thing.Tiles.Count > 1 && !OldBlock(thing, decor))
            {
                if (!done.Add(thing.Anchor)) return;
                // Whatever still stands on its tiles goes (made again with it).
                foreach (int j in thing.Tiles)
                    if (onTile.TryGetValue(Key(k, j), out var standing))
                        foreach (var o in standing.Where(o => o != null && o.transform.parent == props[k]).ToList()) Drop(o);
                Own(Thing(props[k], thing, floor), k, thing.Tiles);
                return;
            }
            if (Block(k, i, look, tiles, decor, done)) return;
            if (Fountain(k, i, look, tiles, decor, done)) return;
            // The base's pines are its own (Woods).
            if (pieces != null && tile == "high" && look.Kind == DecorKinds.Tree) return;
            if (look.Kind == "part") return;
            // A clinic's bed on a tile of its own that its dressing draws (stripped, or a body under its sheet): nothing more.
            if (look.Kind == "hospitalBed" && tile == "low" && BedVariant(k, i)) return;
            // A clinic's own furniture (mapView.ts medicalFurniture), the tall ones on high cover, the rest on low.
            if (MedicalDetails.IsFurniture(look.Kind) && tile == (MedicalDetails.FurnitureHigh(look.Kind) ? "high" : "low"))
            {
                Clinical(k, i, look, floor);
                return;
            }
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
            // A loot box (mapView.ts lootBox): shut with its lid on, or searched open.
            if (LootBox(k, i, look.Kind, turn, floor)) return;
            // Searched furniture (mapView.ts addDecor): opened, its contents on the floor in front of it.
            if (SearchedAt(k, i) && !look.Kind.StartsWith("loot"))
            {
                var front = Bastion.Engine.Grid.Dirs4[MapArt.Facing.Contains(look.Kind) ? look.Facing : Mathf.FloorToInt(MapArt.Hash(x, y, 2) * 4)];
                var middle = new Vector3((x + 0.5f) * MapSpace.Tile, floor, -(y + 0.5f) * MapSpace.Tile);
                GameObject opened = null;
                if (look.Kind == "wardrobe" && !low) opened = OpenWardrobe(Place(props[k], "wardrobeOpen", middle, -turn * 90), x + 0.5f, y + 0.5f, front);
                else if (look.Kind == "crates" && low) opened = Place(props[k], "crateOpen", middle, -turn * 90);
                Own(opened, k, new[] { i });
                Ransack(k, i, look.Kind, front, floor);
                if (opened != null) return;
            }
            if (look.Kind == "windowBarricade")
            {
                BarricadeWindow(k, i, x, y, floor, look.Facing);
                return;
            }
            if (Stacked(k, i, look, low, turn, floor)) return;
            if (Furnished(k, i, look, low, turn, floor)) return;
            var ids = MapArt.Set(set);
            string id = ids[Mathf.Min(ids.Length - 1, Mathf.FloorToInt(MapArt.Hash(x, y, 1) * ids.Length))];
            Own(Place(props[k], id, new Vector3(cx * MapSpace.Tile, floor, -cz * MapSpace.Tile), -yaw), k, new[] { i });
            // A fence's rails, from its middle to each fence or gate post beside it still standing (boards in town, wire elsewhere).
            if (look.Kind == "fence" || look.Kind == "gatePost")
            {
                var rails = MapArt.Set(s.Biome == "city" ? "fenceBoards" : "fenceRails");
                for (int f = 0; f < 4; f++)
                {
                    var d = Bastion.Engine.Grid.Dirs4[f];
                    if (!FenceAt(x + d.X, y + d.Y) || rails.Length == 0) continue;
                    Own(Place(props[k], rails[0], new Vector3((x + 0.5f) * MapSpace.Tile, floor, -(y + 0.5f) * MapSpace.Tile), -((4 - f) % 4) * 90), k, new[] { i });
                }
            }
        }

        /// <summary>Whether a clinic's dressing draws the bed anchored on tile i of storey k itself (mapView.ts bedVariants: stripped, or a body under its sheet).</summary>
        bool BedVariant(int k, int i) =>
            (s.Medical ?? new List<MedicalSite>()).Any(m => m.Dressing != null && m.Dressing.Any(d => (d.Kind == "bareBed" || d.Kind == "coveredBed") && d.Z == k && d.Y * s.Width + d.X == i));

        /// <summary>
        /// A clinic's own furniture on tile i of storey k (mapView.ts dressed: medicalFurniture), turned the way it faces, a
        /// little askew as worn as its building is (medicalWear); a dark one now and then; the cart with its kit on top.
        /// </summary>
        void Clinical(int k, int i, DecorLook look, float floor)
        {
            int x = i % s.Width, y = i / s.Width;
            var faces = MedicalDetails.Furniture(look.Kind, MapArt.Hash(x, y, 73 + k) < 0.6f);
            int owner = plan != null && i < plan.Owner.Length ? plan.Owner[i] : -1;
            double wear = owner >= 0 && owner < plan.Buildings.Count ? plan.Buildings[owner].medicalWear ?? 0 : 0;
            float yaw = (4 - look.Facing) % 4 * (Mathf.PI / 2) + (MapArt.Hash(x, y, 19) - 0.5f) * (float)wear * 0.7f;
            var at = Matrix4x4.TRS(new Vector3(x + 0.5f, floor / MapSpace.Tile, y + 0.5f), Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up), Vector3.one);
            var o = Shapes($"Mueble {look.Kind}", props[k], faces.Select(f => (f, at)), Resources.Load<Material>("FrameShape"), true);
            Own(o, k, new[] { i });
            if (look.Kind == "medicalCart") Own(PlaceGame(props[k], "clinicalKit", at * Matrix4x4.Translate(new Vector3(-0.08f, 0.59f, -0.03f))), k, new[] { i });
        }

        /// <summary>A fence or gate post still standing on tile (x, y) (mapView.ts fenceAt: the ground's): the fences beside it run their rails to it.</summary>
        bool FenceAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
            int i = y * s.Width + x;
            if (i >= s.Tiles.Count || s.Tiles[i] != "low" || s.Decor == null || i >= s.Decor.Count) return false;
            string kind = Bastion.Engine.Decor.DecorOf(s.Decor[i])?.Kind;
            return kind == "fence" || kind == "gatePost";
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
        /// What mapView.ts addDecor builds of more than one prop, or stood otherwise: pallets stacked, three for low cover and
        /// six for high, each a little askew; high crates a big one with a smaller one on it, askew; a house's television
        /// on its stand; a wardrobe or a cabinet low cover knocked over on its face (#15); the factory's genset, high,
        /// stood taller (1.9).
        /// </summary>
        bool Stacked(int k, int i, DecorLook look, bool low, int turn, float floor)
        {
            int x = i % s.Width, y = i / s.Width;
            float cx = x + 0.5f, cz = y + 0.5f, T = MapSpace.Tile;
            // (0.10.1's frame in tiles to Unity's: z the other way, and the turns with it.)
            Vector3 At(float ax, float ay, float az) => new Vector3(ax * T, floor + ay * T, -az * T);
            float yaw = turn * 90;
            switch (look.Kind)
            {
                case "pallets":
                    for (int n = 0, of = low ? 3 : 6; n < of; n++)
                    {
                        float R(int salt) => MapArt.Hash(x, y, 40 + n * 3 + salt) - 0.5f;
                        Own(Place(props[k], "pallet", At(cx + R(0) * 0.06f, n * 0.19f, cz + R(1) * 0.06f), -(yaw + R(2) * 0.12f * Mathf.Rad2Deg)), k, new[] { i });
                    }
                    return true;
                case "crates" when !low:
                    Own(Place(props[k], "crates", At(cx, 0, cz), -yaw), k, new[] { i });
                    Own(Place(props[k], "crate", At(cx + (MapArt.Hash(x, y, 11) - 0.5f) * 0.12f, (float)Measure("crates").top, cz + (MapArt.Hash(x, y, 12) - 0.5f) * 0.12f),
                        -(yaw + (MapArt.Hash(x, y, 13) - 0.5f) * 0.6f * Mathf.Rad2Deg)), k, new[] { i });
                    return true;
                case "tvStand" when low:
                    Own(Place(props[k], "tvStand", At(cx, 0, cz), -yaw), k, new[] { i });
                    Own(Place(props[k], "television", At(cx, (float)Measure("tvStand").top, cz), -(yaw + (MapArt.Hash(x, y, 16) - 0.5f) * 0.2f * Mathf.Rad2Deg)), k, new[] { i });
                    return true;
                case "wardrobe" when low:
                case "cabinet" when low:
                {
                    var d = Bastion.Engine.Grid.Dirs4[look.Facing];
                    var ids = MapArt.Set(look.Kind);
                    if (ids.Length == 0) return false;
                    string id = ids[Mathf.Min(ids.Length - 1, Mathf.FloorToInt(MapArt.Hash(x, y, 1) * ids.Length))];
                    var o = Place(props[k], id, At(cx + d.X * 0.35f, 0.28f, cz + d.Y * 0.35f), 0);
                    // (Euler(π/2, yaw, 0, 'YXZ') there: about y, then onto its face about x.)
                    if (o != null) o.transform.rotation = Quaternion.AngleAxis(-(yaw + (MapArt.Hash(x, y, 17) - 0.5f) * 0.3f * Mathf.Rad2Deg), Vector3.up) * Quaternion.AngleAxis(-90, Vector3.right);
                    Own(o, k, new[] { i });
                    return true;
                }
                case "machine" when !low:
                {
                    var o = Place(props[k], "machine", At(cx, 0, cz), -yaw);
                    if (o != null) o.transform.localScale = new Vector3(1, 1.9f, 1);
                    Own(o, k, new[] { i });
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Furniture as its building is (mapView.ts addDecor): a shop's counter its own; a supermarket's shelving, gondolas
        /// with goods both ways, along its walls wall shelves, a row's low end a display turned to the checkouts; a
        /// grocer's (its FOOD sign) the supermarket's gondolas; tall shelves a house's or an office's (or a bookshop's)
        /// bookcases, else shelving; a tall cabinet. Under a supermarket's shelving, what fell off it (Litter).
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
            // (A supermarket's shelving: how stocked, against a wall, from its tile's middle; its litter round it.)
            int stock = -1;
            bool against = false;
            float mx = cx, mz = cz;
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
                stock = MarketStock(x, y);
                against = wall;
                var odds = ShelfLooks[stock];
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
            if (stock >= 0) Litter(k, i, mx, mz, floor, look.Facing, against, stock);
            return true;
        }

        /// <summary>
        /// A loot box (mapView.ts lootBox; LOOT_KINDS) on tile i of storey k, turned `turn` quarters: its box with its lid
        /// (and what sits on it) on, shut; searched, open, the lid and what was in it on the floor before it.
        /// </summary>
        bool LootBox(int k, int i, string kind, int turn, float floor)
        {
            if (kind != "lootWeapons" && kind != "lootMedical" && kind != "lootAmmo" && kind != "lootFood") return false;
            bool open = SearchedAt(k, i);
            float x = i % s.Width + 0.5f, z = i / s.Width + 0.5f, yaw = turn * Mathf.PI / 2, c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            float R(int salt) => MapArt.Hash((double)x, z, 70 + salt);
            // A prop at (dx, dz) from the tile's middle in the box's own frame (tiles, 0.10.1's), `dy` up, turned `more` (rad).
            GameObject Put(string id, float dx, float dz, float dy = 0, float more = 0, bool shadows = true)
            {
                float px = x + dx * c + dz * sn, pz = z - dx * sn + dz * c;
                var o = Place(props[k], id, new Vector3(px * MapSpace.Tile, floor + dy * MapSpace.Tile, -pz * MapSpace.Tile), -(yaw + more) * Mathf.Rad2Deg);
                Own(shadows ? o : Flat(o), k, new[] { i });
                return o;
            }
            var box = Put(kind, 0, 0);
            // How high the box stands as placed (tiles), for what goes on it.
            var parts = box != null ? box.GetComponentsInChildren<Renderer>() : new Renderer[0];
            float top = parts.Length > 0 ? (parts.Max(r => r.bounds.max.y) - floor) / MapSpace.Tile : 0.3f;
            switch (kind)
            {
                case "lootWeapons":
                    if (open) Put("lootWeaponsLid", 0.05f, 0.5f, 0, 0.25f + R(1) * 0.3f, false);
                    else Put("lootWeaponsLid", 0, 0, top * 0.98f);
                    break;
                case "lootMedical":
                    if (open)
                    {
                        Put("lootMedicalKit", -0.25f, 0.5f, 0, R(1) * 2);
                        Put("lootPills", 0.2f, 0.45f, 0, R(2) * 6, false);
                        Put("lootPills", 0.32f, 0.58f, 0, R(3) * 6, false);
                    }
                    else Put("lootMedicalKit", 0, 0, top, 0.2f);
                    break;
                case "lootAmmo":
                    if (open)
                    {
                        Put("lootAmmoLid", 0.1f, 0.5f, 0, 0.3f + R(1) * 0.3f, false);
                        Put("lootAmmoCanOpen", -0.3f, 0.52f, 0, R(2) * 3);
                    }
                    else
                    {
                        Put("lootAmmoLid", 0, 0, top * 0.98f);
                        Put("lootAmmoCan", 0, 0, top + 0.02f, 0.4f);
                    }
                    break;
                case "lootFood":
                    if (open)
                    {
                        Put("lootFoodTop", -0.2f, 0.52f, 0, R(1) * 2);
                        Put("lootCan", 0.18f, 0.45f, 0, R(2) * 6, false);
                        Put("lootCan2", 0.3f, 0.58f, 0, R(3) * 6, false);
                        Put("lootRice", 0.05f, 0.62f, 0, R(4) * 6, false);
                    }
                    else Put("lootFoodTop", 0.04f, -0.03f, top, 0.35f);
                    break;
            }
            return true;
        }

        /// <summary>Whether tile i of storey k was searched (GameState.Searched, by node: grid.ts nodeOf).</summary>
        bool SearchedAt(int k, int i) => s.Searched != null && s.Searched.Contains(k * s.Width * s.Height + i);

        /// <summary>
        /// What a search leaves in front of a piece of furniture (mapView.ts ransack; `front` the way it faces): drawers
        /// pulled out and dropped, papers, clothes, opened boxes.
        /// </summary>
        void Ransack(int k, int i, string kind, Vec2 front, float floor)
        {
            int x = i % s.Width, y = i / s.Width;
            float R(int salt) => MapArt.Hash(x, y, 60 + salt);
            void Drop(string id, float along, float @out, int salt, bool shadows = false)
            {
                // (On 0.10.1's plane, tiles, z south: out the way it faces, along across it.)
                float px = x + 0.5f + front.X * @out - front.Y * along, pz = y + 0.5f + front.Y * @out + front.X * along;
                var o = Place(props[k], id, new Vector3(px * MapSpace.Tile, floor + 0.003f * MapSpace.Tile, -pz * MapSpace.Tile), -R(salt) * 360);
                Own(shadows ? o : Flat(o), k, new[] { i });
            }
            switch (kind)
            {
                case "desk":
                case "cabinet":
                case "counter":
                    Drop("drawer", (R(1) - 0.5f) * 0.4f, 0.55f, 2, true);
                    Drop("papers", (R(3) - 0.5f) * 0.5f, 0.6f, 4);
                    break;
                case "wardrobe":
                case "bed":
                    Drop("laundry", (R(1) - 0.5f) * 0.4f, 0.55f, 2);
                    if (kind == "bed") Drop("papers", (R(3) - 0.5f) * 0.5f, 0.5f, 4);
                    break;
                case "shelf":
                case "rack":
                    Drop("cardboard", (R(1) - 0.5f) * 0.4f, 0.55f, 2, true);
                    Drop("papers", (R(3) - 0.5f) * 0.5f, 0.6f, 4);
                    break;
                case "crates":
                    Drop("crateLid", (R(1) - 0.5f) * 0.3f, 0.62f, 2);
                    break;
                case "table":
                    Drop("papers", (R(3) - 0.5f) * 0.5f, 0.5f, 4);
                    break;
            }
        }

        /// <summary>
        /// A wardrobe someone has searched (mapView.ts openWardrobe): its doors swung wide out to the room, past square,
        /// and its drawers pulled out, (cx, cz) its tile's middle (tiles); `wardrobe` Town's with its parts apart, back.
        /// </summary>
        static GameObject OpenWardrobe(GameObject wardrobe, float cx, float cz, Vec2 front)
        {
            if (wardrobe == null) return null;
            // Its frame in Unity's world: out the way it faces, and along it (0.10.1's piece x: the facing's (y, −x), z flipped).
            var @out = new Vector3(front.X, 0, -front.Y);
            var along = new Vector3(front.Y, 0, front.X);
            var parts = wardrobe.GetComponentsInChildren<Renderer>().ToList();
            for (int k = 0; k < parts.Count; k++)
            {
                var part = parts[k].transform;
                var box = parts[k].bounds;
                var corners = new[] { box.min, box.max, new Vector3(box.min.x, box.min.y, box.max.z), new Vector3(box.max.x, box.min.y, box.min.z) };
                float lo = corners.Min(c => Vector3.Dot(c, along)), hi = corners.Max(c => Vector3.Dot(c, along)), face = corners.Max(c => Vector3.Dot(c, @out));
                Vector3 At(float a) => along * a + @out * face + Vector3.up * box.center.y;
                // A door hinged at its outer edge, the front: from there to its other edge now 1.9 rad round, out to the room.
                void Swing(Vector3 hinge, Vector3 was, float side)
                {
                    var now = Mathf.Cos(1.9f) * side * along + Mathf.Sin(1.9f) * @out;
                    part.RotateAround(hinge, Vector3.up, Vector3.SignedAngle(was, now, Vector3.up));
                }
                if (part.name.EndsWith("Door_01")) Swing(At(lo), along, 1);
                else if (part.name.EndsWith("Door_02")) Swing(At(hi), -along, -1);
                else if (part.name.Contains("Drawer")) part.position += @out * ((0.1f + MapArt.Hash(k, Mathf.RoundToInt(cx * 7 + cz), 3) * 0.18f) * MapSpace.Tile);
            }
            return wardrobe;
        }

        /// <summary>How much falls off a supermarket's shelving by its stock (mapView.ts SHELF_LITTER).</summary>
        static readonly float[] ShelfLitter = { 0.9f, 1.3f, 1.6f };

        /// <summary>
        /// What fell off a supermarket's shelving into the aisle (mapView.ts addDecor), either side of a gondola (a wall
        /// shelf's, its front), the more the emptier: papers and wrappers, a few cans rolled about lying on their sides, and
        /// once it was searched or stripped, boxes torn down. (cx, cz) the tile's middle (tiles), its front `facing`.
        /// </summary>
        void Litter(int k, int i, float cx, float cz, float floor, int facing, bool wall, int stock)
        {
            int x = i % s.Width, y = i / s.Width;
            var front = Bastion.Engine.Grid.Dirs4[facing];
            int litter = (int)Math.Floor(ShelfLitter[stock] * MapArt.Hash(x, y, 81) * 2);
            for (int n = 0; n < litter; n++)
            {
                float R(int salt) => MapArt.Hash(x, y, 82 + n * 7 + salt);
                float side = wall || R(0) < 0.5f ? 1 : -1;
                float @out = side * (0.62f + R(1) * 0.22f), along = (R(2) - 0.5f) * 0.8f;
                // (On 0.10.1's plane, tiles, z south; up in tiles over the floor.)
                Vector3 At(float dx, float dz, float up) => new Vector3((cx + front.X * (@out + dz) - front.Y * (along + dx)) * MapSpace.Tile, floor + up * MapSpace.Tile, -(cz + front.Y * (@out + dz) + front.X * (along + dx)) * MapSpace.Tile);
                float what = R(3);
                if (what < 0.3f && stock > 0)
                {
                    var box = Place(props[k], R(4) < 0.5f ? "cardboard" : "cardboard2", At(0, 0, 0), -R(4) * 360);
                    if (box != null) box.transform.localScale = Vector3.one * 0.8f;
                    Own(box, k, new[] { i });
                }
                else if (what < 0.65f) Own(Flat(Place(props[k], "papers", At(0, 0, 0.003f), -R(4) * 360)), k, new[] { i });
                else
                    for (int c = 0; c < 2 + (int)Math.Floor(R(4) * 2); c++)
                    {
                        var can = Place(props[k], $"canFallen{(n + c) % 3 + 1}", At((R(5 + c) - 0.5f) * 0.25f, (R(6 + c) - 0.5f) * 0.18f, 0.025f), 0);
                        // On its side, rolled any way (three's Euler(π/2, θ, 0, 'YXZ'), its z the other way).
                        if (can != null) can.transform.rotation = Quaternion.Euler(-90, -R(7 + c) * 360, 0);
                        Own(Flat(can), k, new[] { i });
                    }
            }
        }

        /// <summary>`o` casting no shadow (what lies flat, as 0.10.1 adds it), and `o` back.</summary>
        static GameObject Flat(GameObject o)
        {
            if (o != null) foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return o;
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
                    // Searched (its boot or its glovebox), a wreck is the one with the boot lid up; a whole one (Unity's own,
                    // Ana 28/10) has none and stays as it is.
                    if (Wrecks.Contains(id) && thing.Tiles.Any(j => SearchedAt(Mathf.RoundToInt(floor / MapSpace.Storey), j))) id = "carOpen";
                    break;
                case "bed":
                    id = x1 - x0 == y1 - y0 ? "bedDouble" : "bed";
                    break;
                case "hospitalBed":
                    // A clinic's bed stripped or with a body under its sheet is its dressing's (Dressing), not Synty's.
                    if (BedVariant(Mathf.RoundToInt(floor / MapSpace.Storey), thing.Anchor)) return null;
                    id = "hospitalBed";
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
            if (placed != null && thing.Kind == "hospitalBed")
            {
                // The slim drip by its head, within its tiles (mapView.ts drawThing), going with the bed.
                var at = Matrix4x4.TRS(new Vector3(cx, floor / MapSpace.Tile, cz), Quaternion.AngleAxis((4 - thing.Facing) % 4 * 90f, Vector3.up), Vector3.one);
                var drip = PlaceGame(parent, "ivStand", at * Matrix4x4.Translate(new Vector3(0.45f, 0, 0.6f)));
                if (drip != null) drip.transform.SetParent(placed.transform, true);
            }
            if (placed != null && thing.Kind == "carFront")
            {
                // (BASTION_COCHES=1: each car's long side against its two tiles', to tell a model turned the wrong way.)
                if (CarsLogged)
                {
                    var box = BoundsIn(placed, parent);
                    bool tilesAlongX = x1 - x0 > y1 - y0, modelAlongX = box.size.x > box.size.z;
                    Debug.Log($"[coches] {id} at {ax},{ay} facing {thing.Facing}: {(tilesAlongX == modelAlongX ? "along" : "ACROSS")} ({box.size.x:F2} × {box.size.z:F2} m)");
                }
                Repaint(placed, id, MapArt.Hash(ax, ay, 13));
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
        /// <summary>
        /// How much the trees sway, of what Nature's materials have (Arturo, 24/10: a soft wind, at the base and in combat;
        /// 0.10.1's stood still): their sway's amounts times this, its speeds times Calmer. Set before a map is made.
        /// </summary>
        public static float Wind = 0.5f;
        const float Calmer = 0.7f;
        static readonly string[] WindAmounts = { "_Big_Wind_Amount", "_Small_Wind_Amount" };
        static readonly string[] WindSpeeds = { "_Big_Wind_Speed", "_Small_Wind_Speed" };
        /// <summary>Each windy material (Nature's trees and vines sway by the game's clock): its copy in this wind, and its copy still.</summary>
        static readonly Dictionary<(Material, float), Material> breezes = new Dictionary<(Material, float), Material>();
        static readonly Dictionary<Material, Material> stills = new Dictionary<Material, Material>();
        /// <summary>A copy's own material (its windy original), for Still.</summary>
        static readonly Dictionary<Material, Material> sources = new Dictionary<Material, Material>();

        static bool Windy(Material m) => m != null && WindAmounts.Any(m.HasProperty);

        /// <summary>A windy material in a wind `k` times its own (0: still), made once.</summary>
        static Material InWind(Material m, float k)
        {
            if (sources.TryGetValue(m, out var own)) m = own;
            if (k == 0 ? stills.TryGetValue(m, out var made) && made != null : breezes.TryGetValue((m, k), out made) && made != null) return made;
            made = new Material(m) { name = m.name + (k == 0 ? " (quieto)" : " (brisa)") };
            foreach (string a in WindAmounts) if (made.HasProperty(a)) made.SetFloat(a, m.GetFloat(a) * k);
            foreach (string s in WindSpeeds) if (k > 0 && made.HasProperty(s)) made.SetFloat(s, m.GetFloat(s) * Calmer);
            if (k == 0) stills[m] = made;
            else breezes[(m, k)] = made;
            sources[made] = m;
            return made;
        }

        /// <summary>
        /// A model's windy materials in the map's wind (Wind). Nature's tree shader sways each vertex by its place in its
        /// own model: the far wood, merged into patches (Beyond), would sway another way, so it stands still (InWind 0);
        /// out there, in the haze, it doesn't show.
        /// </summary>
        static void Breeze(GameObject model, float k)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                bool swapped = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!Windy(materials[i])) continue;
                    materials[i] = InWind(materials[i], k);
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
            var holder = Prop(parent, id, art, prefab, footprint).transform;
            holder.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            Repaint(holder.gameObject, art);
            Placed++;
            return holder.gameObject;
        }

        /// <summary>A prop painted from another texture than its model's (MapArt.Atlases: a container's colour), its materials of that name swapped.</summary>
        void Repaint(GameObject prop, PropArt art)
        {
            if (art.atlas == null || !MapArt.Atlases.TryGetValue(art.atlas, out var swap) || materialOf == null) return;
            var to = materialOf(swap.to);
            if (to == null)
            {
                Missing.Add($"{art.atlas} ({swap.to})");
                return;
            }
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                bool swapped = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith(swap.from)) continue;
                    materials[i] = to;
                    swapped = true;
                }
                if (swapped) r.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// One of MapArt's props as the map places it (its fit, stretch, deck, height, sink, its own turn and shift), under
        /// `parent` at its origin: what stands on a tile's middle facing north (0.10.1's −z); null without its model.
        /// </summary>
        public static GameObject Prop(Transform parent, string id, Func<string, GameObject> prefabOf)
        {
            var art = MapArt.Prop(id);
            var prefab = art != null && !string.IsNullOrEmpty(art.fbx) ? prefabOf?.Invoke(MapArt.ModelKey(art)) : null;
            return prefab != null ? Prop(parent, id, art, prefab, null) : null;
        }

        static GameObject Prop(Transform parent, string id, PropArt art, GameObject prefab, MapArt.Footprint footprint)
        {
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(prefab, holder, false);
            Breeze(model, Wind);
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
