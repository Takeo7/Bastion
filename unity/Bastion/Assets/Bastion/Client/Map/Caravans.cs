using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The camping's caravans (runs, H9 §5, CampUses.Trailer: a building of 4 × 8 or 4 × 9 tiles, its one room of 2 × 6 or
    /// 2 × 7, its door on a long side) as Apocalypse's mobile homes, not of kit pieces (WallLayout and the roofs leave them
    /// out): its shell (its walls inside and out, its floor and roof) and its glass; inside, the pack's furniture, each
    /// piece the engine marks where it marks it (Furnish). Fitted as every building's walls stand, on its wall tiles' middle lines (across, about 1.5 times its own
    /// width; along, its ends on its end walls'), sunk to its floor, turned so its doorway is on the engine's door's side,
    /// mirrored or not so that it is nearest the door (the engine lays its door and windows where the model has them:
    /// Mapgen.TrailerOpenings, Arte's template of 17/11; CampingLab.Remolques measured the models). Each doorway its leaf (the model's inside door): the
    /// engine's door's open or shut as its tile, the others shut. In
    /// Bastion/Building with each triangle's cut, as the pieces' (BuildingCut.hlsl): what stands above its eaves fades with
    /// its roof; a wall (its window, its door) drops with the walls that face the camera.
    /// </summary>
    public static class Caravans
    {
        /// <summary>
        /// A mobile home as measured (the prefab's metres, its length along z): its walls' outer faces across (±halfWide) and
        /// at its ends (z0, z1), its floor, its eaves (what is higher is the roof), its top; its doorways' middles along it,
        /// all on side `doorSide` (−1 the −x one, 1 the +x one); its shell and glass, and its inside door (the leaves).
        /// </summary>
        internal sealed class Model
        {
            public string name, shell, glass, leaf;
            /// <summary>Its inside (the pack's: sofa, kitchen, a table with benches, the bed), and the box (x0, x1, z0, z1) of what is left out of it (its partitions).</summary>
            public string inside;
            public Vector4 noPartition;
            /// <summary>
            /// Its pieces the engine marks (its boxes, x0, x1, z0, z1; Mapgen.TrailerInside, Arte's template of 17/11): each drawn
            /// only with its mark in the caravan. The rest of the inside (against the walls, no cover: the kitchen and the
            /// chest of the six's, the sofa, the tall cupboard) always.
            /// </summary>
            public (string mark, Vector4 box)[] marked;
            public float halfWide, z0, z1, floor, eaves, top;
            public int doorSide;
            public float[] doors;
        }

        /// <summary>
        /// _01 (a pitched roof, two doorways on its −x side) for a room six tiles long; _02 (a flat roof with skylights, one
        /// doorway on its +x side) for seven. Their doorways have no leaf: the inside door's goes in each.
        /// </summary>
        static readonly Model[] Models =
        {
            new Model
            {
                name = "SM_Bld_Trailer_01", shell = "SM_Bld_Trailer_01", glass = "SM_Bld_Trailer_Glass_01", leaf = "SM_Bld_Trailer_Interior_Door_01",
                inside = "SM_Bld_Trailer_Interior_01", noPartition = new Vector4(-1.5f, 1.5f, -3.55f, -1.17f),
                marked = new[] { (CampDetailKinds.TrailerBed, new Vector4(-1.49f, 0.08f, -4.53f, -2.61f)), (CampDetailKinds.TrailerTable, new Vector4(-1.52f, -0.3f, -0.16f, 1.88f)) },
                halfWide = 1.53f, z0 = -4.52f, z1 = 4.56f, floor = 0.7f, eaves = 3.3f, top = 3.91f, doorSide = -1, doors = new[] { -1.68f, 2.58f },
            },
            new Model
            {
                name = "SM_Bld_Trailer_02", shell = "SM_Bld_Trailer_02", glass = "SM_Bld_Trailer_Glass_02", leaf = "SM_Bld_Trailer_Interior_Door_02",
                inside = "SM_Bld_Trailer_Interior_02", noPartition = new Vector4(-1.52f, 0.19f, -3.22f, -1.13f),
                marked = new[]
                {
                    (CampDetailKinds.TrailerBed, new Vector4(-1.51f, 0.66f, -5.64f, -4.01f)), (CampDetailKinds.TrailerDrawers, new Vector4(-1.51f, 0.08f, -3.68f, -3.14f)),
                    (CampDetailKinds.TrailerCounter, new Vector4(-1.51f, 0.24f, -1.18f, 2.91f)), (CampDetailKinds.TrailerTable, new Vector4(-1.54f, -0.32f, 2.86f, 4.9f)),
                },
                halfWide = 1.5f, z0 = -5.62f, z1 = 5.0f, floor = 0.7f, eaves = 2.95f, top = 3.73f, doorSide = 1, doors = new[] { -2.63f },
            },
        };

        /// <summary>
        /// A caravan's storeys as the fog takes its roof (Fog's shells: the roof from 2.3 tiles a storey, less 0.15): under a
        /// storey, so its roof, lower than a house's, is the street's along it and not its unfound inside's.
        /// </summary>
        public const float FogStoreys = 0.8f;

        /// <summary>How far over the ground its floor stands (tiles), as a house's floor's (BuildingSurfaces' slabs).</summary>
        const float Lift = 0.004f;

        /// <summary>(BuildingCut.hlsl's ON_ROOF: what fades with the roof and never drops.)</summary>
        const float OnRoof = 9;

        /// <summary>BASTION_CARAVANAS=1: each caravan's fit in the log (its doorways and windows on the engine's tiles).</summary>
        static readonly bool Logs = Environment.GetEnvironmentVariable("BASTION_CARAVANAS") == "1";

        /// <summary>The rooms of state `s`'s caravans (a camping's of use `trailer`); none on any other map.</summary>
        public static List<InteriorRoom> Rooms(GameState s) =>
            s?.Interiors == null ? new List<InteriorRoom>()
                : s.Interiors.Where(site => site?.Kind == InteriorKinds.Camping).SelectMany(site => site.Rooms).Where(r => r.Use == CampUses.Trailer && r.Z == 0).ToList();

        /// <summary>Whether state `s` is a camping's (GameState.Interiors).</summary>
        public static bool Any(GameState s) => s?.Interiors != null && s.Interiors.Exists(site => site?.Kind == InteriorKinds.Camping);

        /// <summary>Every tile of state `s`'s caravans, their walls with them (Fog: FogStoreys).</summary>
        public static IEnumerable<int> Tiles(GameState s)
        {
            foreach (var r in Rooms(s))
                for (int y = r.Y - 1; y <= r.Y + r.H; y++)
                    for (int x = r.X - 1; x <= r.X + r.W; x++)
                        if (x >= 0 && y >= 0 && x < s.Width && y < s.Height) yield return y * s.Width + x;
        }

        /// <summary>
        /// Whether building b is a camping's caravan (GameState.Interiors: a camping's room of use `trailer` with its walls
        /// round it), and the side its door is on (DIRS4, the outline's side with the most doors; with none, a long side); null if not.
        /// </summary>
        public static int? DoorSide(GameState s, Building b)
        {
            if (s.Interiors == null) return null;
            bool caravan = s.Interiors.Any(site => site?.Kind == InteriorKinds.Camping && site.Rooms.Any(r =>
                r.Use == CampUses.Trailer && r.Z == 0 && b.x0 == r.X - 1 && b.y0 == r.Y - 1 && b.x1 == r.X + r.W && b.y1 == r.Y + r.H));
            if (!caravan) return null;
            var doors = new int[4];
            for (int x = b.x0; x <= b.x1; x++)
                for (int y = b.y0; y <= b.y1; y++)
                {
                    string t = BuildingPlan.TileAt(s, x, y);
                    if (t != TileKinds.Door && t != TileKinds.DoorOpen) continue;
                    if (y == b.y0) doors[0]++;
                    if (x == b.x1) doors[1]++;
                    if (y == b.y1) doors[2]++;
                    if (x == b.x0) doors[3]++;
                }
            int most = Array.IndexOf(doors, doors.Max());
            if (doors[most] > 0) return most;
            return b.y1 - b.y0 >= b.x1 - b.x0 ? 1 : 2;
        }

        /// <summary>Its door's tile on its door's side (the first one), or null.</summary>
        static Vec2 DoorOf(GameState s, Building b, int side)
        {
            for (int x = b.x0; x <= b.x1; x++)
                for (int y = b.y0; y <= b.y1; y++)
                {
                    bool on = side == 0 ? y == b.y0 : side == 1 ? x == b.x1 : side == 2 ? y == b.y1 : x == b.x0;
                    string t = BuildingPlan.TileAt(s, x, y);
                    if (on && (t == TileKinds.Door || t == TileKinds.DoorOpen)) return new Vec2 { X = x, Y = y };
                }
            return null;
        }

        /// <summary>
        /// How a model stands on building b: from the prefab's metres to 0.10.1's tiles (x east, y up, z south), the model, its
        /// axes and how tall it stands (tiles); which of its doorways is the engine's door, and the stretch of its door's
        /// the engine's door's tile.
        /// </summary>
        public sealed class Fit
        {
            internal Model model;
            public Matrix4x4 tiles;
            public int doorway;
            public Vec2 door;
            public float top, sy, kA;
            public Vector2 A, L;
            public float flipA, flipL;
            /// <summary>How far (m, the prefab's) its doorway's middle is from the engine's door's (0: on it, as the engine lays it).</summary>
            public float miss;
        }

        /// <summary>Building b's fit (DoorSide's `side`).</summary>
        public static Fit FitOf(GameState s, Building b, int side)
        {
            bool alongY = b.y1 - b.y0 >= b.x1 - b.x0;
            int length = (alongY ? b.y1 - b.y0 : b.x1 - b.x0) - 1;
            var model = length >= 7 ? Models[1] : Models[0];
            var A = alongY ? new Vector2(1, 0) : new Vector2(0, 1);
            var L = alongY ? new Vector2(0, 1) : new Vector2(1, 0);
            var mid = new Vector2((b.x0 + b.x1 + 1) / 2f, (b.y0 + b.y1 + 1) / 2f);
            float start = alongY ? b.y0 : b.x0, end = alongY ? b.y1 : b.x1;
            float halfAcross = (alongY ? b.x1 - b.x0 : b.y1 - b.y0) / 2f;
            var out_ = Bastion.Engine.Grid.Dirs4[side];
            float flipA = Mathf.Sign(Vector2.Dot(new Vector2(out_.X, out_.Y), A)) * model.doorSide;
            if (flipA == 0) flipA = 1;
            // Along: its ends on its end walls' middle lines (k tiles a metre), mirrored or not as puts a doorway nearest the
            // door (on it, as the engine lays it).
            float k = (end - start) / (model.z1 - model.z0);
            float bestFlip = 1, miss = 0;
            int bestDoor = 0;
            var door = DoorOf(s, b, side);
            if (door != null)
            {
                float T = Vector2.Dot(new Vector2(door.X + 0.5f, door.Y + 0.5f), L);
                miss = float.MaxValue;
                foreach (float flip in new[] { 1f, -1f })
                {
                    float e0 = flip > 0 ? model.z0 : -model.z1;
                    float target = flip * ((T - (start + 0.5f)) / k + e0);
                    for (int d = 0; d < model.doors.Length; d++)
                        if (Mathf.Abs(target - model.doors[d]) < miss - 1e-4f) (miss, bestFlip, bestDoor) = (Mathf.Abs(target - model.doors[d]), flip, d);
                }
            }
            float kA = halfAcross / model.halfWide;
            float e0Best = bestFlip > 0 ? model.z0 : -model.z1;
            float a = start + 0.5f - e0Best * k;
            // Tall as it is stretched along (its doorways and windows as tall as wide).
            float sy = k;
            float midA = Vector2.Dot(mid, A);
            var m = new Matrix4x4();
            m.SetColumn(0, new Vector4(A.x * flipA * kA, 0, A.y * flipA * kA, 0));
            m.SetColumn(1, new Vector4(0, sy, 0, 0));
            m.SetColumn(2, new Vector4(L.x * bestFlip * k, 0, L.y * bestFlip * k, 0));
            var origin = A * midA + L * a;
            m.SetColumn(3, new Vector4(origin.x, b.@base + Lift - model.floor * sy, origin.y, 1));
            return new Fit
            {
                model = model, tiles = m, doorway = bestDoor, door = door, top = (model.top - model.floor) * sy + Lift, sy = sy, kA = kA, A = A, L = L, flipA = flipA,
                flipL = bestFlip, miss = miss,
            };
        }

        /// <summary>How tall caravan b stands over its floor (tiles: Building.top, the cutaway's box).</summary>
        public static float TopOf(GameState s, Building b) => FitOf(s, b, b.trailer ?? 1).top;

        /// <summary>The top of both models' mattresses (the prefab's metres).</summary>
        const float BedTop = 1.34f;

        /// <summary>The middle of the top of caravan b's bed, its mattress's (0.10.1's tiles: x, up, z): what lies on it (MapBuilder.CampScenery). Null without its bed (no trailerBed mark in it).</summary>
        public static Vector3? BedOf(GameState s, Building b)
        {
            if (!MarksIn(s, b).Contains(CampDetailKinds.TrailerBed)) return null;
            var fit = FitOf(s, b, b.trailer ?? 1);
            var bed = fit.model.marked.First(m => m.mark == CampDetailKinds.TrailerBed).box;
            return fit.tiles.MultiplyPoint3x4(new Vector3((bed.x + bed.y) / 2, BedTop, (bed.z + bed.w) / 2));
        }

        /// <summary>The materials, found once: Apocalypse's atlas in Bastion/Building (the bunker's pieces'), and the windows' glass.</summary>
        static Material shell, glass;

        static bool Materials()
        {
            if (shell != null && glass != null) return true;
            var catalog = PieceCatalog.Load();
            if (catalog == null) return false;
            glass = catalog.glass;
            shell = catalog.pieces.Where(p => p?.materials != null).SelectMany(p => p.materials).FirstOrDefault(m => m != null && m.name == "PolygonApocalypse_01_A");
            return shell != null && glass != null;
        }

        /// <summary>A part of a model: its mesh read once (CaravanArt's: readable), where it stands in the prefab, its box there.</summary>
        internal sealed class Part
        {
            public Vector3[] positions, normals;
            public Vector2[] uvs;
            public int[] triangles;
            public Matrix4x4 local;
            public Bounds box;
        }

        static readonly Dictionary<string, Part> read = new Dictionary<string, Part>();

        internal static Part PartOf(Model model, string name, ICollection<string> missing)
        {
            string key = model.name + "/" + name;
            if (read.TryGetValue(key, out var known)) return known;
            var prefab = CaravanArt.Load()?.Model(model.name);
            var f = prefab != null ? prefab.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == name) : null;
            var mesh = f != null ? f.sharedMesh : null;
            if (mesh == null || (!Application.isEditor && !mesh.isReadable))
            {
                missing?.Add($"caravana ({name}: {(prefab == null ? "CaravanArt" : mesh == null ? "no mesh" : "not readable")})");
                return read[key] = null;
            }
            var triangles = new List<int>();
            for (int k = 0; k < mesh.subMeshCount; k++) triangles.AddRange(mesh.GetTriangles(k));
            var local = prefab.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
            var b = mesh.bounds;
            var box = new Bounds(local.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int c = 0; c < 8; c++) box.Encapsulate(local.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return read[key] = new Part { positions = mesh.vertices, normals = mesh.normals, uvs = mesh.uv, triangles = triangles.ToArray(), local = local, box = box };
        }

        /// <summary>
        /// Building b's caravan (DoorSide's `side`), drawn under `parent`: one mesh of its shell, one of its glass, one of its
        /// doors' leaves (CaravanView, which makes those again as its door opens). Null if its model isn't there (in `missing`).
        /// </summary>
        public static GameObject Make(Transform parent, GameState s, Building b, int side, ICollection<string> missing)
        {
            if (!Materials())
            {
                missing?.Add("caravana (PieceCatalog: PolygonApocalypse_01_A, glass)");
                return null;
            }
            var fit = FitOf(s, b, side);
            var model = fit.model;
            var body = PartOf(model, model.shell, missing);
            if (body == null) return null;
            var panes = PartOf(model, model.glass, missing);
            var o = new GameObject($"Caravana {b.x0},{b.y0}");
            o.transform.SetParent(parent, false);
            float lit = BuildingPlan.Hash(b.x0, b.y0, 93) < 0.35 ? 1 : 0;
            // (Its roof casts no shadow, as a flat roof's doesn't (Bastion/Building's swatches): cut open, its inside had
            // the roof's shade over it by day, all dark.)
            var shellBatch = new Batch();
            var roofBatch = new Batch();
            Add(shellBatch, body, Matrix4x4.identity, fit, b, 0, roofBatch);
            Draw(o.transform, "Casco", shellBatch.Make($"Caravana {b.x0},{b.y0}"), shell, true);
            Draw(o.transform, "Techo", roofBatch.Make($"Caravana {b.x0},{b.y0} techo"), shell, false);
            if (panes != null)
            {
                var glassBatch = new Batch();
                Add(glassBatch, panes, Matrix4x4.identity, fit, b, lit);
                Draw(o.transform, "Cristales", glassBatch.Make($"Caravana {b.x0},{b.y0} cristales"), glass, false);
            }
            // Its inside, the pack's (Arturo, 17/11), where the engine marks its furniture (Mapgen.TrailerInside); in an older
            // save, the engine's furniture as a house's. The labs' BASTION_CARAVANA_INTERIOR, whatever the marks.
            var marks = MarksIn(s, b);
            if (marks.Count > 0 || Inside == "ancho" || Inside == "aparte") Furnish(o.transform, fit, b, missing, marks);
            var view = o.AddComponent<CaravanView>();
            view.Set(fit, b, PartOf(model, model.leaf, missing));
            view.Doors(s);
            if (Logs) Log(s, b, fit, panes);
            return o;
        }

        /// <summary>A corner: its place and normal in the prefab, its uv.</summary>
        struct Corner
        {
            public Vector3 p, n;
            public Vector2 uv;
        }

        /// <summary>
        /// Part `part`'s triangles into `batch`, placed by `placed` (in the prefab's frame) and the fit, each with its cut: what
        /// stands above the eaves the roof's (into `roofBatch` if given); what stands within ShellNear of a wall's plane that
        /// wall's; the rest inside. `each`: a triangle's own place in the prefab (a piece of the inside), or null to leave it out.
        /// </summary>
        static void Add(Batch batch, Part part, Matrix4x4 placed, Fit fit, Building b, float lit, Batch roofBatch = null, Func<int, Matrix4x4?> each = null, Func<int, bool> free = null)
        {
            var model = fit.model;
            var world = Matrix4x4.Scale(new Vector3(MapSpace.Tile, MapSpace.Tile, -MapSpace.Tile)) * fit.tiles;
            var turnWorld = world.inverse.transpose;
            var inPrefab = placed * part.local;
            var turnPrefab = inPrefab.inverse.transpose;
            bool mirrored = (world * inPrefab).determinant < 0;
            float floorM = b.@base * MapSpace.Tile;
            float cz = (model.z0 + model.z1) / 2, hl = (model.z1 - model.z0) / 2;
            var p = part.positions;
            var t = part.triangles;
            var poly = new List<Corner>(3);
            bool loose = false;
            void Emit(List<Corner> corners, Vector3 c)
            {
                Vector4 cut, more;
                var into = batch;
                if (c.y > model.eaves - 0.05f && !loose)
                {
                    into = roofBatch ?? batch;
                    cut = new Vector4(b.index + 1, OnRoof, OnRoof, floorM);
                    more = new Vector4(1, 0, 0, 0);
                }
                else
                {
                    float toSide = model.halfWide - Mathf.Abs(c.x), toEnd = hl - Mathf.Abs(c.z - cz);
                    Vector2 outward = Vector2.zero;
                    if (Mathf.Min(toSide, toEnd) < ShellNear && !loose) outward = toSide <= toEnd ? fit.A * fit.flipA * Mathf.Sign(c.x) : fit.L * fit.flipL * Mathf.Sign(c.z - cz);
                    cut = new Vector4(b.index + 1, outward.x, outward.y, floorM);
                    more = new Vector4(0, 0, lit, 0);
                }
                for (int k = 1; k + 1 < corners.Count; k++)
                    foreach (int n in mirrored ? new[] { k + 1, k, 0 } : new[] { 0, k, k + 1 })
                    {
                        var q = corners[n];
                        into.Add(world.MultiplyPoint3x4(q.p), turnWorld.MultiplyVector(q.n).normalized, q.uv, cut, more);
                    }
            }
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                poly.Clear();
                loose = free != null && free(k / 3);
                // (A triangle of its own place, `each`'s: a piece of the inside kept to its proportions; null, left out.)
                var at = inPrefab;
                var turnAt = turnPrefab;
                if (each != null)
                {
                    var own = each(k / 3);
                    if (own == null) continue;
                    at = own.Value * inPrefab;
                    turnAt = at.inverse.transpose;
                }
                for (int n = 0; n < 3; n++)
                {
                    int i = t[k + n];
                    poly.Add(new Corner
                    {
                        p = at.MultiplyPoint3x4(p[i]), n = turnAt.MultiplyVector(i < part.normals.Length ? part.normals[i] : Vector3.up).normalized,
                        uv = i < part.uvs.Length ? part.uvs[i] : Vector2.zero,
                    });
                }
                Emit(poly, (poly[0].p + poly[1].p + poly[2].p) / 3);
            }
        }

        /// <summary>
        /// BASTION_CARAVANA_INTERIOR (the labs, for Arturo's choice of 17/11: the pack's inside instead of the engine's
        /// furniture): «ancho», the pack's inside widened with its caravan; «aparte», each piece of it kept to its own
        /// proportions (as tall and as long as its caravan is stretched), standing against the wall it is nearer, but those
        /// that go from wall to wall. Its partitions (Model.noPartition) left out.
        /// </summary>
        static readonly string Inside = Environment.GetEnvironmentVariable("BASTION_CARAVANA_INTERIOR");

        /// <summary>The pack's inside of building b's caravan (Inside), with the inside door shut where it stands.</summary>
        static void Furnish(Transform parent, Fit fit, Building b, ICollection<string> missing, HashSet<string> marks)
        {
            var model = fit.model;
            var inside = PartOf(model, model.inside, missing);
            var door = PartOf(model, model.leaf, missing);
            if (inside == null) return;
            var (island, boxes) = Islands(inside);
            // Across, pressed back to as wide as it is long (its widening undone), against its wall.
            float squeeze = fit.sy / fit.kA;
            Matrix4x4 Kept(Bounds box)
            {
                if (Inside != "aparte" || box.size.x > 1.4f * model.halfWide) return Matrix4x4.identity;
                float wall = box.center.x < 0 ? -model.halfWide : model.halfWide;
                return Matrix4x4.Translate(new Vector3(wall, 0, 0)) * Matrix4x4.Scale(new Vector3(squeeze, 1, 1)) * Matrix4x4.Translate(new Vector3(-wall, 0, 0));
            }
            bool Partition(Bounds box) =>
                box.min.x >= model.noPartition.x && box.max.x <= model.noPartition.y && box.min.z >= model.noPartition.z && box.max.z <= model.noPartition.w;
            // A piece the engine marks, without its mark here (in the labs, with none at all, every piece).
            bool Unmarked(Bounds box) =>
                marks.Count > 0 && model.marked.Any(m => !marks.Contains(m.mark) && box.min.x >= m.box.x && box.max.x <= m.box.y && box.min.z >= m.box.z && box.max.z <= m.box.w);
            // The tall cupboard (and its lintel and door): in the room, it goes down with any wall that does, lidded.
            bool Tall(Bounds box) => box.min.y < model.floor + 0.2f && box.max.y > model.eaves - 0.6f && !Partition(box);
            var kept = boxes.Select(box => Partition(box) || Unmarked(box) ? (Matrix4x4?)null : Kept(box)).ToArray();
            var free = boxes.Select(box => Tall(box) || (box.min.y > 2.5f && box.size.x < 0.2f)).ToArray();
            var batch = new Batch();
            Add(batch, inside, Matrix4x4.identity, fit, b, 0, null, tri => kept[island[tri]], tri => free[island[tri]]);
            if (door != null) Add(batch, door, Matrix4x4.identity, fit, b, 0, null, _ => Kept(door.box), _ => true);
            foreach (var box in boxes.Where(box => Tall(box) && box.size.x > 0.3f && box.size.z > 0.3f))
                Lid(batch, fit, b, Kept(box), box);
            Draw(parent, "Dentro", batch.Make($"Caravana {b.x0},{b.y0} dentro"), shell, true);
        }

        /// <summary>The kinds of the engine's marks inside caravan b (trailerBed, trailerTable…: GameState.Interiors' camping).</summary>
        static HashSet<string> MarksIn(GameState s, Building b)
        {
            var kinds = new HashSet<string>();
            foreach (var site in s.Interiors ?? new List<InteriorSite>())
                if (site?.Kind == InteriorKinds.Camping && site.Dressing != null)
                    foreach (var d in site.Dressing)
                        if (d.Kind.StartsWith("trailer") && d.Kind != CampDetailKinds.Trailer && d.X > b.x0 && d.X < b.x1 && d.Y > b.y0 && d.Y < b.y1) kinds.Add(d.Kind);
            return kinds;
        }

        /// <summary>
        /// A lid over a tall piece of the inside (its box in the prefab, `placed` as the piece is), a wall's cap: plain grey,
        /// and down with the piece to where the cut takes it (BuildingCut.hlsl's cap). The pack's cupboard has none: cut
        /// open, it was a hollow box.
        /// </summary>
        static void Lid(Batch batch, Fit fit, Building b, Matrix4x4 placed, Bounds box)
        {
            var world = Matrix4x4.Scale(new Vector3(MapSpace.Tile, MapSpace.Tile, -MapSpace.Tile)) * fit.tiles * placed;
            var cut = new Vector4(b.index + 1, 0, 0, b.@base * MapSpace.Tile);
            var more = new Vector4(0, 1, 0, 0);
            float y = box.max.y - 0.01f;
            var corners = new[] { new Vector3(box.min.x, y, box.min.z), new Vector3(box.max.x, y, box.min.z), new Vector3(box.max.x, y, box.max.z), new Vector3(box.min.x, y, box.max.z) };
            var up = world.inverse.transpose.MultiplyVector(Vector3.up).normalized;
            var p = corners.Select(q => world.MultiplyPoint3x4(q)).ToArray();
            // (Its front up, as Unity winds a face seen from above: clockwise.)
            bool upward = Vector3.Cross(p[1] - p[0], p[2] - p[0]).y > 0;
            foreach (var tri in upward ? new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 3 } } : new[] { new[] { 0, 2, 1 }, new[] { 0, 3, 2 } })
                foreach (int n in tri) batch.Add(p[n], up, Vector2.zero, cut, more);
        }

        /// <summary>A part's pieces that share no corner (welded by place, a millimetre): each triangle's piece, each piece's box in the prefab.</summary>
        static (int[] island, List<Bounds> boxes) Islands(Part part)
        {
            var v = part.positions;
            var key = new int[v.Length];
            var at = new Dictionary<Vector3Int, int>();
            for (int k = 0; k < v.Length; k++)
            {
                var q = Vector3Int.RoundToInt(v[k] * 1000);
                if (!at.TryGetValue(q, out int id)) at[q] = id = at.Count;
                key[k] = id;
            }
            var parent = Enumerable.Range(0, at.Count).ToArray();
            int Find(int a)
            {
                while (parent[a] != a) a = parent[a] = parent[parent[a]];
                return a;
            }
            var t = part.triangles;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                int a = Find(key[t[k]]);
                parent[Find(key[t[k + 1]])] = a;
                parent[Find(key[t[k + 2]])] = Find(a);
            }
            var island = new int[t.Length / 3];
            var index = new Dictionary<int, int>();
            var boxes = new List<Bounds>();
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                int root = Find(key[t[k]]);
                if (!index.TryGetValue(root, out int n))
                {
                    index[root] = n = boxes.Count;
                    boxes.Add(new Bounds(part.local.MultiplyPoint3x4(v[t[k]]), Vector3.zero));
                }
                island[k / 3] = n;
                var box = boxes[n];
                for (int c = 0; c < 3; c++) box.Encapsulate(part.local.MultiplyPoint3x4(v[t[k + c]]));
                boxes[n] = box;
            }
            return (island, boxes);
        }

        /// <summary>How near a wall's plane (m, the model's) a triangle is that wall's.</summary>
        const float ShellNear = 0.4f;

        /// <summary>
        /// The leaves in a caravan's doorways (`leaf` the model's inside door, as it stands in the prefab): each shut in its
        /// doorway's wall, the engine's door's swung in a quarter turn on its hinge if its tile is open.
        /// </summary>
        internal static Mesh Leaves(Fit fit, Building b, Part leaf, bool open)
        {
            var model = fit.model;
            var batch = new Batch();
            var box = leaf.box;
            float wide = box.size.z, x = model.doorSide * (model.halfWide - 0.02f);
            for (int d = 0; d < model.doors.Length; d++)
            {
                // Shut: its middle onto its doorway's, in the wall's plane.
                float z = model.doors[d];
                var shut = Matrix4x4.Translate(new Vector3(x - box.center.x, 0, z - box.center.z));
                var placed = shut;
                if (open && d == fit.doorway)
                {
                    // Swung in (towards −doorSide across) about its hinge, the end at its doorway's lower z.
                    var hinge = new Vector3(x, 0, z - wide / 2);
                    placed = Matrix4x4.Translate(hinge) * Matrix4x4.Rotate(Quaternion.Euler(0, -model.doorSide * 90, 0)) * Matrix4x4.Translate(-hinge) * shut;
                }
                Add(batch, leaf, placed, fit, b, 0);
            }
            return batch.Make($"Caravana {b.x0},{b.y0} puertas");
        }

        static GameObject Draw(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return o;
        }

        internal static Material Shell => shell;

        /// <summary>BASTION_CARAVANAS=1: where its doorways and windows fall on the engine's tiles, and the engine's own.</summary>
        static void Log(GameState s, Building b, Fit fit, Part panes)
        {
            Vec2 TileOf(float x, float z)
            {
                var p = fit.tiles.MultiplyPoint3x4(new Vector3(x, fit.model.floor + 1, z));
                return new Vec2 { X = Mathf.FloorToInt(p.x), Y = Mathf.FloorToInt(p.z) };
            }
            var doors = fit.model.doors.Select((z, d) => { var t = TileOf(fit.model.doorSide * fit.model.halfWide * 1.05f, z); return $"{t.X},{t.Y}{(d == fit.doorway ? "*" : "")}"; });
            var windows = new List<string>();
            if (panes != null)
            {
                var t = panes.triangles;
                for (int k = 0; k + 2 < t.Length; k += 3)
                {
                    var c = (panes.local.MultiplyPoint3x4(panes.positions[t[k]]) + panes.local.MultiplyPoint3x4(panes.positions[t[k + 1]]) + panes.local.MultiplyPoint3x4(panes.positions[t[k + 2]])) / 3;
                    var w = TileOf(c.x * 1.05f, c.z * 1.01f);
                    string at = $"{w.X},{w.Y}";
                    if (!windows.Contains(at)) windows.Add(at);
                }
            }
            var engine = new List<string>();
            for (int y = b.y0; y <= b.y1; y++)
                for (int x = b.x0; x <= b.x1; x++)
                {
                    string t = BuildingPlan.TileAt(s, x, y);
                    if (t == TileKinds.Window || t == TileKinds.WindowBroken || t == TileKinds.Door || t == TileKinds.DoorOpen) engine.Add($"{x},{y} {t}");
                }
            Debug.Log($"[caravana] {b.x0},{b.y0}–{b.x1},{b.y1} {fit.model.name}, door side {b.trailer}, along ×{fit.flipL} {fit.sy * MapSpace.Tile:0.00} m a metre, its doorway's middle {fit.miss:0.00} m (the model's) off the door's; doorways {string.Join(" ", doors)}; windows {string.Join(" ", windows)}; the engine's {string.Join("; ", engine)}");
        }

        /// <summary>A mesh put together a corner at a time, with Bastion/Building's channels (WallsView's: colours white, the cut, the storey).</summary>
        internal sealed class Batch
        {
            readonly List<Vector3> positions = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Vector4> cuts = new List<Vector4>(), mores = new List<Vector4>();
            readonly Dictionary<(Vector3, Vector3, Vector2, Vector4, Vector4), int> at = new Dictionary<(Vector3, Vector3, Vector2, Vector4, Vector4), int>();
            readonly List<int> triangles = new List<int>();

            public void Add(Vector3 position, Vector3 normal, Vector2 uv, Vector4 cut, Vector4 more)
            {
                var key = (position, normal, uv, cut, more);
                if (!at.TryGetValue(key, out int i))
                {
                    at[key] = i = positions.Count;
                    positions.Add(position);
                    normals.Add(normal);
                    uvs.Add(uv);
                    cuts.Add(cut);
                    mores.Add(more);
                }
                triangles.Add(i);
            }

            public Mesh Make(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                var white = Enumerable.Repeat(Vector4.one, positions.Count).ToList();
                mesh.SetUVs(2, white);
                mesh.SetUVs(3, white);
                mesh.SetUVs(4, cuts);
                mesh.SetUVs(5, mores);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// A caravan as drawn: its fit and its doors' leaves, made again when its door opens or shuts (MapBuilder's Rebuild,
        /// with the walls).
        /// </summary>
        public sealed class CaravanView : MonoBehaviour
        {
            Fit fit;
            Building building;
            Part leaf;
            bool? open;
            GameObject leaves;

            internal void Set(Fit fit, Building b, Part leaf)
            {
                this.fit = fit;
                building = b;
                this.leaf = leaf;
            }

            /// <summary>Its leaves as its door's tile is in state `s`: made again only if that changed.</summary>
            public void Doors(GameState s)
            {
                if (leaf == null || fit == null) return;
                bool now = fit.door != null && BuildingPlan.TileAt(s, fit.door.X, fit.door.Y) == TileKinds.DoorOpen;
                if (open == now && leaves != null) return;
                open = now;
                if (leaves != null)
                {
                    var mesh = leaves.GetComponent<MeshFilter>().sharedMesh;
                    if (Application.isPlaying) Destroy(leaves);
                    else DestroyImmediate(leaves);
                    if (mesh != null)
                    {
                        if (Application.isPlaying) Destroy(mesh);
                        else DestroyImmediate(mesh);
                    }
                }
                leaves = Draw(transform, "Puertas", Leaves(fit, building, leaf, now), Shell, true);
            }
        }
    }
}
