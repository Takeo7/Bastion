using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Client.Map
{
    // 0.10.1's buildings.ts, its floors and roofs: the ground floors (with a house's rooms and its zaguán), the storeys'
    // floors and their stairs, the gabled and flat roofs, the decks of roofs one walks on, the tops of raised blocks and
    // solid wall masses, and what stands on each roof. Pure data, no meshes and no UnityEngine (it runs in a plain dotnet
    // harness): each Surfaces holds the vertex attributes three.js's Float32 buffers held, the triangles unrolled. The
    // math is three's, in doubles, each value made a float only as it goes into a buffer.

    /// <summary>A point or a direction in world tiles (THREE.Vector3's doubles): x east, y up, z south.</summary>
    public readonly struct Vec3d
    {
        public readonly double x, y, z;

        public Vec3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>`clone().negate()`.</summary>
        public Vec3d Negate() => new Vec3d(-x, -y, -z);

        /// <summary>`clone().addScaledVector(v, s)`.</summary>
        public Vec3d AddScaled(Vec3d v, double s) => new Vec3d(x + v.x * s, y + v.y * s, z + v.z * s);
    }

    /// <summary>
    /// A merged mesh of quads and triangles painted from swatches (buildings.ts Surfaces), as its vertex attributes:
    /// position (3 a vertex), normal (3), aPlane (2: where on its swatch, in periods), aSwatch (4: the swatch's rect),
    /// color (3, linear), aRoof (1: the building's index + 1, 0 none) and aLevel (1: the storey it stands on).
    /// </summary>
    public sealed class Surfaces
    {
        public readonly List<float> Position = new List<float>();
        public readonly List<float> Normal = new List<float>();
        public readonly List<float> Plane = new List<float>();
        public readonly List<float> Patch = new List<float>();
        public readonly List<float> Color = new List<float>();
        public readonly List<float> Roof = new List<float>();
        public readonly List<float> Levels = new List<float>();
        /// <summary>The storey what is added now stands on: above the one in view, it fades in the cutaway (roofs: their building's top).</summary>
        public int Level;

        static readonly int[] Triangle = { 0, 1, 2 };
        static readonly int[] TriangleBack = { 2, 1, 0 };
        static readonly int[] Quad = { 0, 1, 2, 0, 2, 3 };
        static readonly int[] QuadBack = { 3, 2, 0, 2, 1, 0 };

        /// <summary>Nothing added.</summary>
        public bool Empty => Position.Count == 0;

        /// <summary>Vertices added (three a triangle).</summary>
        public int Vertices => Position.Count / 3;

        /// <summary>
        /// A flat polygon (3 or 4 corners in order round it), with its swatch coordinates per corner (in tiles; divided by
        /// the period here). Its front is the side `facing` points to (null: counter-clockwise corners).
        /// </summary>
        public void Poly(Vec3d[] corners, (double u, double v)[] planes, Swatch sw, Rgb tint, int building, Vec3d? facing = null)
        {
            Vec3d c0 = corners[0], c1 = corners[1], c2 = corners[2];
            double e1x = c1.x - c0.x, e1y = c1.y - c0.y, e1z = c1.z - c0.z;
            double e2x = c2.x - c0.x, e2y = c2.y - c0.y, e2z = c2.z - c0.z;
            // crossVectors, then normalize: times 1 over its length (`length() || 1`).
            double nx = e1y * e2z - e1z * e2y;
            double ny = e1z * e2x - e1x * e2z;
            double nz = e1x * e2y - e1y * e2x;
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            double inverse = 1 / (length == 0 || double.IsNaN(length) ? 1 : length);
            nx *= inverse;
            ny *= inverse;
            nz *= inverse;
            int[] tri = corners.Length == 3 ? Triangle : Quad;
            if (facing.HasValue && nx * facing.Value.x + ny * facing.Value.y + nz * facing.Value.z < 0)
            {
                tri = corners.Length == 3 ? TriangleBack : QuadBack;
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
            foreach (int k in tri)
            {
                var c = corners[k];
                Position.Add((float)c.x);
                Position.Add((float)c.y);
                Position.Add((float)c.z);
                Normal.Add((float)nx);
                Normal.Add((float)ny);
                Normal.Add((float)nz);
                Plane.Add((float)(planes[k].u / sw.period));
                Plane.Add((float)(planes[k].v / sw.period));
                Patch.AddRange(sw.rect);
                Color.Add((float)tint.r);
                Color.Add((float)tint.g);
                Color.Add((float)tint.b);
                Roof.Add(building + 1);
                Levels.Add(Level);
            }
        }

        /// <summary>A box with the swatch on every face (a parapet, a ridge): its top, its four sides, and its bottom if asked.</summary>
        public void Box(Vec3d min, Vec3d max, Swatch sw, Rgb tint, int building, bool bottom = false)
        {
            Vec3d a = min, b = max;
            static Vec3d V(double x, double y, double z) => new Vec3d(x, y, z);
            // Top, then the four sides (planes in world tiles along each face).
            Poly(new[] { V(a.x, b.y, a.z), V(a.x, b.y, b.z), V(b.x, b.y, b.z), V(b.x, b.y, a.z) }, new[] { (a.x, a.z), (a.x, b.z), (b.x, b.z), (b.x, a.z) }, sw, tint, building);
            Poly(new[] { V(a.x, a.y, b.z), V(b.x, a.y, b.z), V(b.x, b.y, b.z), V(a.x, b.y, b.z) }, new[] { (a.x, a.y), (b.x, a.y), (b.x, b.y), (a.x, b.y) }, sw, tint, building);
            Poly(new[] { V(b.x, a.y, a.z), V(a.x, a.y, a.z), V(a.x, b.y, a.z), V(b.x, b.y, a.z) }, new[] { (b.x, a.y), (a.x, a.y), (a.x, b.y), (b.x, b.y) }, sw, tint, building);
            Poly(new[] { V(b.x, a.y, b.z), V(b.x, a.y, a.z), V(b.x, b.y, a.z), V(b.x, b.y, b.z) }, new[] { (b.z, a.y), (a.z, a.y), (a.z, b.y), (b.z, b.y) }, sw, tint, building);
            Poly(new[] { V(a.x, a.y, a.z), V(a.x, a.y, b.z), V(a.x, b.y, b.z), V(a.x, b.y, a.z) }, new[] { (a.z, a.y), (b.z, a.y), (b.z, b.y), (a.z, b.y) }, sw, tint, building);
            if (bottom) Poly(new[] { V(a.x, a.y, a.z), V(b.x, a.y, a.z), V(b.x, a.y, b.z), V(a.x, a.y, b.z) }, new[] { (a.x, a.z), (b.x, a.z), (b.x, b.z), (a.x, b.z) }, sw, tint, building);
        }
    }

    /// <summary>BuildingSurfaces.Of's: floors and roofs, the tops of raised blocks and of the posts at their corners, the storeys' floors with their stairs, the decks of roofs one walks on.</summary>
    public sealed class SurfaceSet
    {
        /// <summary>The ground floors (each building's, a house's rooms and its zaguán): they don't fade.</summary>
        public Surfaces Floors;
        /// <summary>Gabled and flat roofs: they fade in the cutaway, from their building's top storey.</summary>
        public Surfaces Roofs;
        /// <summary>The walkable tops of flats and platforms, and of solid wall masses.</summary>
        public Surfaces Raised;
        /// <summary>The posts at raised blocks' outer corners.</summary>
        public Surfaces Solid;
        /// <summary>Real storeys' floors and the stairs: they fade with the floors above the one in view.</summary>
        public Surfaces Storeys;
        /// <summary>The decks of roofs one can walk on, weathered as roofs.</summary>
        public Surfaces Decks;
    }

    /// <summary>Something standing on a roof (buildings.ts RoofProp): which prop (PropId), where its foot is (world tiles), turned how (radians about +y).</summary>
    public sealed class RoofProp
    {
        public readonly string id;
        public readonly double x, y, z, yaw;

        public RoofProp(string id, double x, double y, double z, double yaw)
        {
            this.id = id;
            this.x = x;
            this.y = y;
            this.z = z;
            this.yaw = yaw;
        }
    }

    /// <summary>The geometry of buildings' floors, roofs, stairs and raised ground (buildings.ts, BuildingsView.buildStatic and what it calls).</summary>
    public static class BuildingSurfaces
    {
        /// <summary>A storey's height in tiles, as a double (coords.ts STOREY; BuildingPlan.Storey is the float).</summary>
        public const double Storey = 2.3;
        /// <summary>syntyManifest.ts SYNTY_SCALE, KIT_SCALE: world units per metre of a kit piece is their product.</summary>
        const double SyntyScale = 1 / 1.5;
        const double KitScale = 1.2;
        const double Kit = SyntyScale * KitScale;
        /// <summary>Walls are drawn this much thicker than Synty's 0.2 m.</summary>
        const double Thick = 1.75;
        /// <summary>Half a wall's thickness, in tiles.</summary>
        public const double WallHalf = 0.1 * Thick * Kit;

        /// <summary>Raised blocks: their brick (the apartments' own colour), the stone of copings and the tar on their roofs.</summary>
        public static readonly Rgb Brick = Rgb.Hex(0x985c4e);
        public static readonly Rgb Stone = Rgb.Hex(0x968770);
        /// <summary>The walkable top of a block of flats: pale roofing, weathered.</summary>
        public static readonly Rgb Tar = Rgb.Hex(0xbdb1a6);
        /// <summary>The treads of a flight of stairs a tile (docs/MAPAS.md, phase 2), and of a flight of one tile (of before) across it.</summary>
        public const int FlightSteps = 6;
        public const int StairSteps = 9;
        /// <summary>A building's stairs: dark wood.</summary>
        public static readonly Rgb StairWood = Rgb.Hex(0x6e4c36);
        /// <summary>A medical building's stairs.</summary>
        public static readonly Rgb MedicalStairs = Rgb.Hex(0x79888b);
        /// <summary>Platforms' steel decks and posts, and the tops of solid walls.</summary>
        public static readonly Rgb Deck = Rgb.Hex(0x5c6064);
        public static readonly Rgb Steel = Rgb.Hex(0x3c4044);
        public static readonly Rgb Concrete = Rgb.Hex(0x8a8a86);
        /// <summary>A parapet's height (a little under the low cover's, which counts from the soldier's feet) and thickness.</summary>
        public const double Parapet = 0.82;
        public const double ParapetThick = 0.2;
        /// <summary>
        /// Pitch of house roofs and of the low roofs of halls: `Math.tan(THREE.MathUtils.degToRad(30))` and of 14°, as V8
        /// works them out. .NET's Math.Tan is the C runtime's: the last bit off for both on the Mac.
        /// </summary>
        public const double Pitch = 0.5773502691896257;
        public const double HallPitch = 0.24932800284318068;
        /// <summary>The height of the Sala de radio's lattice mast (Apocalypse's SM_Bld_RadioTower_01, 6.8 m), where its head goes.</summary>
        public const double RadioMast = 6.8 * SyntyScale;

        static Vec3d V(double x, double y, double z) => new Vec3d(x, y, z);

        /// <summary>A level rectangle from (ax, az) to (bx, bz) at height y, its swatch in world tiles: the floors', decks' and tops' quad.</summary>
        static void Slab(Surfaces @out, double ax, double az, double bx, double bz, double y, Swatch sw, Rgb tint, int building) =>
            @out.Poly(new[] { V(ax, y, az), V(ax, y, bz), V(bx, y, bz), V(bx, y, az) }, new[] { (ax, az), (ax, bz), (bx, bz), (bx, az) }, sw, tint, building);

        /// <summary>`tiles?.[i]`: null past its end, or without tiles.</summary>
        static string TileIn(IList<string> tiles, int i) => tiles != null && i >= 0 && i < tiles.Count ? tiles[i] : null;

        /// <summary>`decor[i]`: null past its end.</summary>
        static int? CodeAt(IList<int> decor, int i) => i >= 0 && i < decor.Count ? decor[i] : (int?)null;

        /// <summary>
        /// A gabled roof over the walls' middle lines of b: two slopes of shingles with eaves, a thickness, and siding
        /// gables at the ends. Returns the height of its top.
        /// </summary>
        public static double GableRoof(Surfaces @out, Building b, double pitch, Rgb roofTint, Rgb wallTint)
        {
            bool along = b.x1 - b.x0 >= b.y1 - b.y0;
            // In the roof's frame: u along the ridge, v across; c the middle.
            double cx = (b.x0 + b.x1) / 2.0 + 0.5;
            double cz = (b.y0 + b.y1) / 2.0 + 0.5;
            double L = (along ? b.x1 - b.x0 : b.y1 - b.y0) / 2.0;
            double W = (along ? b.y1 - b.y0 : b.x1 - b.x0) / 2.0;
            const double eave = 0.36;
            const double gableOut = 0.22;
            const double thick = 0.1;
            double h0 = b.@base + Storey * b.storeys;
            double H(double v) => h0 + thick + (W - Math.Abs(v)) * pitch;
            Vec3d P(double u, double y, double v) => along ? V(cx + u, y, cz + v) : V(cx + v, y, cz + u);
            double U = L + gableOut;
            double E = W + eave;
            // World direction of local (u, v): what each face turns its front to.
            Vec3d D(double u, double y, double v) => along ? V(u, y, v) : V(v, y, u);
            var dark = roofTint.Scale(0.7);
            foreach (int side in new[] { 1, -1 })
            {
                // The slope: its swatch runs along the ridge (u) and down the slope (v).
                double run = E * Js.Hypot(1, pitch);
                var top = new[] { P(-U, H(0), 0), P(U, H(0), 0), P(U, H(E), side * E), P(-U, H(E), side * E) };
                @out.Poly(top, new[] { (-U, 0.0), (U, 0.0), (U, run), (-U, run) }, Swatch.Shingles, roofTint, b.index, D(0, 1, side * pitch));
                // The eave's edge and the ends of the slope (rakes).
                var fascia = new[] { P(-U, H(E) - thick, side * E), P(U, H(E) - thick, side * E), top[2], top[3] };
                @out.Poly(fascia, new[] { (-U, 0.0), (U, 0.0), (U, thick), (-U, thick) }, Swatch.Shingles, dark, b.index, D(0, 0, side));
                foreach (int end in new[] { -1, 1 })
                {
                    var rake = new[] { P(end * U, H(0) - thick, 0), P(end * U, H(E) - thick, side * E), P(end * U, H(E), side * E), P(end * U, H(0), 0) };
                    @out.Poly(rake, new[] { (0.0, 0.0), (run, 0.0), (run, thick), (0.0, thick) }, Swatch.Shingles, dark, b.index, D(end, 0, 0));
                    // Gables: siding from the top of the wall up under the ridge, in the end walls' plane.
                    var gable = new[] { P(end * (L + WallHalf), h0, side * W), P(end * (L + WallHalf), h0, 0), P(end * (L + WallHalf), H(0) - thick, 0) };
                    @out.Poly(gable, new[] { (side * W, h0), (0.0, h0), (0.0, H(0) - thick) }, Swatch.Siding, wallTint, b.index, D(end, 0, 0));
                }
            }
            // A ridge cap.
            double ridge = H(0);
            const double half = 0.07;
            var lo = along ? V(cx - U, ridge - 0.02, cz - half) : V(cx - half, ridge - 0.02, cz - U);
            var hi = along ? V(cx + U, ridge + 0.06, cz + half) : V(cx + half, ridge + 0.06, cz + U);
            @out.Box(lo, hi, Swatch.Shingles, roofTint.Scale(0.6), b.index);
            return ridge + 0.06;
        }

        /// <summary>A flat roof over b: a slab of tar on the walls and a parapet round it (both flat colours). Returns the height of its top.</summary>
        public static double FlatRoof(Surfaces @out, Building b, Rgb tar, Rgb trim)
        {
            double h0 = b.@base + Storey * b.storeys;
            double x0 = b.x0 + 0.5 - WallHalf - 0.02;
            double x1 = b.x1 + 0.5 + WallHalf + 0.02;
            double z0 = b.y0 + 0.5 - WallHalf - 0.02;
            double z1 = b.y1 + 0.5 + WallHalf + 0.02;
            double slab = h0 + 0.04;
            Slab(@out, x0, z0, x1, z1, slab, Swatch.Flat, tar, b.index);
            const double t = 0.16;
            double h = slab + 0.32;
            var lip = trim.Scale(0.9);
            @out.Box(V(x0 - 0.02, h0 - 0.12, z0 - 0.02), V(x1 + 0.02, h, z0 + t), Swatch.Flat, lip, b.index);
            @out.Box(V(x0 - 0.02, h0 - 0.12, z1 - t), V(x1 + 0.02, h, z1 + 0.02), Swatch.Flat, lip, b.index);
            @out.Box(V(x0 - 0.02, h0 - 0.12, z0 + t), V(x0 + t, h, z1 - t), Swatch.Flat, lip, b.index);
            @out.Box(V(x1 - t, h0 - 0.12, z0 + t), V(x1 + 0.02, h, z1 - t), Swatch.Flat, lip, b.index);
            return h;
        }

        /// <summary>The storey a piece of building `b` at height `base` stands on (0: the ground floor, or no building).</summary>
        public static int StoreyOf(Building b, double @base) => b != null ? (int)Math.Max(0, Js.Round((@base - b.@base) / Storey)) : 0;

        /// <summary>
        /// Floors and roofs (BuildingsView.buildStatic): every building's but a ruin's, the storeys' with the stairs, and
        /// the raised ground's. Sets each building's `top` to its roof's (a roof one walks on: its parapet's).
        /// </summary>
        public static SurfaceSet Of(GameState s, BuildingPlan plan)
        {
            var floors = new Surfaces();
            var roofs = new Surfaces();
            // Real storeys (GameState.levels): their floors and the stairs, which fade with the floors above the one in view;
            // the decks of roofs one can walk on, weathered like any roof.
            var storeys = new Surfaces();
            var decks = new Surfaces();
            int w = s.Width;
            foreach (var b in plan.Buildings)
            {
                if (b.kind == BuildingKinds.Ruin) continue;
                var looks = BuildingLooks.Of(b);
                // The floor runs to the walls' middle lines (the outer half of a wall tile is the street's).
                double x0 = b.x0 + 0.5, x1 = b.x1 + 0.5, z0 = b.y0 + 0.5, z1 = b.y1 + 0.5, y = b.@base + 0.004;
                Slab(floors, x0, z0, x1, z1, y, looks.floorSwatch, looks.floor, -1);
                // A house's rooms each with its floor, over the house's (#15).
                var rooms = HomeRooms(s, b);
                foreach (var r in rooms)
                {
                    if (r.Z != 0) continue;
                    var f = BuildingLooks.HomeFloor(r);
                    Slab(floors, r.X, r.Y, r.X + r.W, r.Y + r.H, y + 0.002, f.swatch, f.tint, -1);
                }
                // A zaguán (docs/MAPAS.md §5.4): a house's floor in none of its rooms is the way through it, paved in stone out
                // to its openings.
                var ground = rooms.Where((r) => r.Z == 0).ToList();
                if (b.kind == BuildingKinds.House && ground.Count > 0)
                {
                    for (int ty = b.y0; ty <= b.y1; ty++)
                    {
                        for (int tx = b.x0; tx <= b.x1; tx++)
                        {
                            if (TileIn(s.Tiles, ty * w + tx) != TileKinds.Floor || ground.Any((r) => tx >= r.X && tx < r.X + r.W && ty >= r.Y && ty < r.Y + r.H)) continue;
                            // Under the walls beside it to their middle lines, as the house's floor runs (the index as TS takes it:
                            // past a row's end it is the next row's).
                            double Under(int dx, int dy) => TileIn(s.Tiles, (ty + dy) * w + tx + dx) == TileKinds.Wall ? 0.5 : 0;
                            double ax = Math.Max(tx - Under(-1, 0), x0), bx = Math.Min(tx + 1 + Under(1, 0), x1);
                            double az = Math.Max(ty - Under(0, -1), z0), bz = Math.Min(ty + 1 + Under(0, 1), z1);
                            Slab(floors, ax, az, bx, bz, y + 0.002, Swatch.Panels, BuildingLooks.ZaguanFloor, -1);
                        }
                    }
                }
                if (b.leveled) StoreyFloors(plan, b, storeys, looks);
                roofs.Level = b.storeys;
                double roof;
                if (b.walkableRoof) roof = RoofDeck(plan, b, decks, looks);
                else if (b.kind == BuildingKinds.House) roof = GableRoof(roofs, b, Pitch, looks.shingles, looks.outer);
                else if (b.kind == BuildingKinds.Barn) roof = GableRoof(roofs, b, HallPitch * 1.8, looks.shingles, looks.outer);
                else roof = FlatRoof(roofs, b, looks.tar, looks.trim);
                b.top = (float)roof;
            }
            Stairs(s, plan, storeys);
            var solid = new Surfaces();
            var raised = new Surfaces();
            RaisedSurfaces(s, plan, raised, solid);
            return new SurfaceSet { Floors = floors, Roofs = roofs, Raised = raised, Solid = solid, Storeys = storeys, Decks = decks };
        }

        /// <summary>
        /// The storeys' floors (with their stairs) and the decks of roofs one walks on, made again as the slabs are now
        /// (BuildingsView.redoUpper): a slab blown through leaves its hole (docs/FASE7.md §5). `plan` follows `s` (plan.Follow).
        /// </summary>
        public static (Surfaces storeys, Surfaces decks) Upper(GameState s, BuildingPlan plan)
        {
            var storeys = new Surfaces();
            var decks = new Surfaces();
            foreach (var b in plan.Buildings)
            {
                if (b.kind == BuildingKinds.Ruin) continue;
                var looks = BuildingLooks.Of(b);
                if (b.leveled) StoreyFloors(plan, b, storeys, looks);
                if (b.walkableRoof) RoofDeck(plan, b, decks, looks);
            }
            Stairs(s, plan, storeys);
            return (storeys, decks);
        }

        /// <summary>The rooms of the house on building b's ground, if it is one (GameState.homes); none otherwise.</summary>
        static List<HomeRoom> HomeRooms(GameState s, Building b)
        {
            var home = s.Homes?.FirstOrDefault((h) => b.x0 >= h.X && b.y0 >= h.Y && b.x1 < h.X + h.W && b.y1 < h.Y + h.H);
            return home?.Rooms ?? new List<HomeRoom>();
        }

        /// <summary>
        /// The floors of a building's storeys below its top, over every tile that has a slab (not over a stairwell), to the
        /// walls' middle lines like the ground floor's.
        /// </summary>
        static void StoreyFloors(BuildingPlan plan, Building b, Surfaces @out, Looks looks)
        {
            double x0 = b.x0 + 0.5, x1 = b.x1 + 0.5, z0 = b.y0 + 0.5, z1 = b.y1 + 0.5;
            var rooms = HomeRooms(plan.S, b);
            for (int k = 1; k < b.storeys; k++)
            {
                @out.Level = k;
                double y = b.@base + k * Storey + 0.004;
                for (int ty = b.y0; ty <= b.y1; ty++)
                {
                    for (int tx = b.x0; tx <= b.x1; tx++)
                    {
                        string kind = plan.KindAt(tx, ty, k);
                        if (kind == null || kind == TileKinds.Roof) continue;
                        double ax = Math.Max(tx, x0), bx = Math.Min(tx + 1, x1), az = Math.Max(ty, z0), bz = Math.Min(ty + 1, z1);
                        if (bx <= ax || bz <= az) continue;
                        // A house's room its floor (#15), else the house's.
                        var room = rooms.FirstOrDefault((r) => r.Z == k && tx >= r.X && tx < r.X + r.W && ty >= r.Y && ty < r.Y + r.H);
                        var f = room != null ? BuildingLooks.HomeFloor(room) : (swatch: looks.floorSwatch, tint: looks.floor);
                        Slab(@out, ax, az, bx, bz, y, f.swatch, f.tint, b.index);
                    }
                }
            }
        }

        /// <summary>
        /// A roof one can walk on (its top storey has floor): a deck of tar out to the walls' outer faces, open where stairs
        /// come up through it, with a parapet along the edge wherever the rules put low cover there. Returns the height of
        /// its top.
        /// </summary>
        static double RoofDeck(BuildingPlan plan, Building b, Surfaces @out, Looks looks)
        {
            int k = b.storeys;
            @out.Level = k;
            double y = b.@base + k * Storey + 0.04;
            double x0 = b.x0 + 0.5 - WallHalf - 0.02, x1 = b.x1 + 0.5 + WallHalf + 0.02, z0 = b.y0 + 0.5 - WallHalf - 0.02, z1 = b.y1 + 0.5 + WallHalf + 0.02;
            var lip = looks.trim.Scale(0.9);
            for (int ty = b.y0; ty <= b.y1; ty++)
            {
                for (int tx = b.x0; tx <= b.x1; tx++)
                {
                    string kind = plan.KindAt(tx, ty, k);
                    if (kind == null) continue;
                    double ax = Math.Max(tx, x0), bx = Math.Min(tx + 1, x1), az = Math.Max(ty, z0), bz = Math.Min(ty + 1, z1);
                    Slab(@out, ax, az, bx, bz, y, Swatch.Flat, looks.tar, b.index);
                    // The parapet: on the deck's edge, along each side of this tile that is the roof's edge.
                    if (kind != TileKinds.Low) continue;
                    const double t = ParapetThick;
                    double top = y + Parapet;
                    if (ty == b.y0) @out.Box(V(ax, y - 0.12, z0), V(bx, top, z0 + t), Swatch.Flat, lip, b.index);
                    if (ty == b.y1) @out.Box(V(ax, y - 0.12, z1 - t), V(bx, top, z1), Swatch.Flat, lip, b.index);
                    if (tx == b.x0) @out.Box(V(x0, y - 0.12, az), V(x0 + t, top, bz), Swatch.Flat, lip, b.index);
                    if (tx == b.x1) @out.Box(V(x1 - t, y - 0.12, az), V(x1, top, bz), Swatch.Flat, lip, b.index);
                }
            }
            // The deck's edge, seen from the street: a band over the walls' tops.
            @out.Box(V(x0, y - 0.14, z0), V(x1, y + 0.02, z0 + 0.04), Swatch.Flat, lip, b.index);
            @out.Box(V(x0, y - 0.14, z1 - 0.04), V(x1, y + 0.02, z1), Swatch.Flat, lip, b.index);
            @out.Box(V(x0, y - 0.14, z0), V(x0 + 0.04, y + 0.02, z1), Swatch.Flat, lip, b.index);
            @out.Box(V(x1 - 0.04, y - 0.14, z0), V(x1, y + 0.02, z1), Swatch.Flat, lip, b.index);
            return y + Parapet;
        }

        /// <summary>
        /// A building's stairs (docs/MAPAS.md, phase 2; engine Flights): a flight of ladder tiles in a line, its top one
        /// dressed 'stairs' facing the way it climbs and the rest its parts, its steps from the back edge of its lowest tile
        /// up to the landing a storey higher in front of the top, FlightSteps a tile; a flight of one (of before) has
        /// StairSteps across its tile. A banister on each side and a railing round the stairwell above, open only to the
        /// landing; none against a wall.
        /// </summary>
        static void Stairs(GameState s, BuildingPlan plan, Surfaces @out)
        {
            int w = s.Width, h = s.Height;
            const double half = 0.4;
            IList<string> TilesOf(int k) => k == 0 ? s.Tiles : s.Levels != null && k >= 1 && k <= s.Levels.Count ? s.Levels[k - 1].Tiles : null;
            // A wall (or a door, a window) on storey k at (x, y): no railing needed against it.
            bool Walled(int x, int y, int k)
            {
                string t = x < 0 || y < 0 || x >= w || y >= h ? TileKinds.Wall : TileIn(TilesOf(k), y * w + x);
                return t == TileKinds.Wall || t == TileKinds.Window || t == TileKinds.WindowBroken || t == TileKinds.Door || t == TileKinds.DoorOpen;
            }
            for (int k = 0; k <= (s.Levels?.Count ?? 0); k++)
            {
                IList<string> tiles = k != 0 ? s.Levels[k - 1].Tiles : s.Tiles;
                IList<int> decor = k != 0 ? s.Levels[k - 1].Decor : s.Decor;
                if (decor == null) continue;
                for (int i = 0; i < w * h; i++)
                {
                    var look = TileIn(tiles, i) == TileKinds.Ladder ? Bastion.Engine.Decor.DecorOf(CodeAt(decor, i)) : null;
                    if (look?.Kind != DecorKinds.Stairs) continue;
                    int x = i % w;
                    int y = (i - x) / w;
                    var run = Flights.FlightAt(s, new Pos { X = x, Y = y, Z = k })?.Flight.Tiles ?? new List<Vec2> { new Vec2 { X = x, Y = y } };
                    int n = run.Count;
                    int steps = n == 1 ? StairSteps : FlightSteps * n;
                    int b = plan.Owner[i];
                    var bd = b >= 0 && b < plan.Buildings.Count ? plan.Buildings[b] : null;
                    double @base = (bd != null ? bd.@base : 0) + k * Storey;
                    var d = Grid.Dirs4[look.Facing];
                    // The flight's middle: along the climb from it, -n/2 at the foot's edge, n/2 at the landing's.
                    double cx = (run[0].X + x) / 2.0 + 0.5;
                    double cz = (run[0].Y + y) / 2.0 + 0.5;
                    var wood = bd?.kind == BuildingKinds.Medical ? MedicalStairs : bd != null ? StairWood : Steel;
                    @out.Level = k;
                    for (int t = 0; t < steps; t++)
                    {
                        // Along the climb, from the lowest tile's back edge to the top's front.
                        double u0 = -n / 2.0 + (t * n) / (double)steps;
                        double u1 = -n / 2.0 + ((t + 1) * n) / (double)steps;
                        double top = @base + ((t + 1) / (double)steps) * Storey;
                        double ax = d.X != 0 ? cx + Math.Min(u0 * d.X, u1 * d.X) : cx - half;
                        double bx = d.X != 0 ? cx + Math.Max(u0 * d.X, u1 * d.X) : cx + half;
                        double az = d.Y != 0 ? cz + Math.Min(u0 * d.Y, u1 * d.Y) : cz - half;
                        double bz = d.Y != 0 ? cz + Math.Max(u0 * d.Y, u1 * d.Y) : cz + half;
                        // Wooden treads are horizontal boards. Filling every step down
                        // to the ground made the back of the flight look like a vertical floor.
                        @out.Box(V(ax, bd != null ? top - 0.055 : @base, az), V(bx, top, bz), Swatch.Flat, t % 2 != 0 ? wood : wood.Scale(0.92), b, bd != null);
                    }
                    if (bd != null) StairStringers(@out, cx, cz, n, d, @base, wood.Scale(0.72), b);
                    Banisters(plan, @out, run, k, d, @base, steps, wood.Scale(1.25), b, Walled);
                }
            }
        }

        /// <summary>Two sloping wooden beams carry the horizontal treads of a flight of `n` tiles about (cx, cz), leaving the risers open.</summary>
        static void StairStringers(Surfaces @out, double cx, double cz, int n, Vec2 d, double @base, Rgb tint, int b)
        {
            Vec3d At(double u, double v, double h) => V(cx + d.X * u - d.Y * v, h, cz + d.Y * u + d.X * v);
            var up = V(0, 1, 0);
            var across = V(-d.Y, 0, d.X);
            var along = V(d.X, 0, d.Y);
            double h0 = @base - 0.055;
            double h1 = @base + Storey - 0.055;
            foreach (double v in new[] { -0.32, 0.32 })
            {
                var a = At(-n / 2.0, v - 0.04, h0);
                var b0 = At(-n / 2.0, v + 0.04, h0);
                var c = At(n / 2.0, v + 0.04, h1);
                var d0 = At(n / 2.0, v - 0.04, h1);
                Vec3d Lower(Vec3d p) => p.AddScaled(up, -0.16);
                var faces = new (Vec3d[] corners, Vec3d facing)[]
                {
                    (new[] { a, b0, c, d0 }, up),
                    (new[] { Lower(a), Lower(d0), Lower(c), Lower(b0) }, up.Negate()),
                    (new[] { b0, Lower(b0), Lower(c), c }, across),
                    (new[] { a, d0, Lower(d0), Lower(a) }, across.Negate()),
                    (new[] { a, Lower(a), Lower(b0), b0 }, along.Negate()),
                    (new[] { d0, c, Lower(c), Lower(d0) }, along),
                };
                foreach (var (corners, facing) in faces) @out.Poly(corners, corners.Select((p) => (p.x + p.z, p.y)).ToArray(), Swatch.Flat, tint, b, facing);
            }
        }

        /// <summary>
        /// The railings of the flight on tiles `run` (the lowest first) of storey k, climbing towards `d` from `base`: along
        /// each side, beside each of its tiles that has no wall there, a handrail sloping up with the treads, on balusters,
        /// and a newel post at the foot; round the stairwell a storey up, on each of its edges but the landing's, a rail on
        /// posts at the floor's height (none against a wall, nor beside the next flight up, whose own banister stands there).
        /// </summary>
        static void Banisters(BuildingPlan plan, Surfaces @out, IList<Vec2> run, int k, Vec2 d, double @base, int steps, Rgb tint, int b, Func<int, int, int, bool> walled)
        {
            const double RAIL = 0.9;
            const double T = 0.035;
            int n = run.Count;
            double cx = (run[0].X + run[n - 1].X) / 2.0 + 0.5;
            double cz = (run[0].Y + run[n - 1].Y) / 2.0 + 0.5;
            // A box from (along, across, height) to (along, across, height), in the stairs' own axes: `along` up the climb, `across` to its left.
            void Box(double u0, double v0, double h0, double u1, double v1, double h1)
            {
                double ax = cx + d.X * u0 - d.Y * v0;
                double az = cz + d.Y * u0 + d.X * v0;
                double bx = cx + d.X * u1 - d.Y * v1;
                double bz = cz + d.Y * u1 + d.X * v1;
                @out.Box(V(Math.Min(ax, bx), h0, Math.Min(az, bz)), V(Math.Max(ax, bx), h1, Math.Max(az, bz)), Swatch.Flat, tint, b);
            }
            // A point in the stairs' axes.
            Vec3d At(double u, double v, double h) => V(cx + d.X * u - d.Y * v, h, cz + d.Y * u + d.X * v);
            var across = V(-d.Y, 0, d.X);
            var up = V(0, 1, 0);
            // The handrail's height over the flight at `u`, parallel to its slope.
            double Rail(double u) => @base + ((u + n / 2.0) / n) * Storey + RAIL;
            @out.Level = k;
            foreach (int side in new[] { 1, -1 })
            {
                double v = side * 0.44;
                for (int j = 0; j < n; j++)
                {
                    var tile = run[j];
                    // Against a wall on that side, none.
                    if (walled(tile.X - d.Y * side, tile.Y + d.X * side, k)) continue;
                    // The handrail beside this tile: one bar along the slope.
                    double a = -n / 2.0 + j, z = -n / 2.0 + j + 1;
                    double ha = Rail(a), hz = Rail(z);
                    var faces = new (Vec3d[] corners, Vec3d facing)[]
                    {
                        (new[] { At(a, v - T, ha), At(a, v + T, ha), At(z, v + T, hz), At(z, v - T, hz) }, up),
                        (new[] { At(a, v - T, ha - 0.05), At(z, v - T, hz - 0.05), At(z, v + T, hz - 0.05), At(a, v + T, ha - 0.05) }, up.Negate()),
                        (new[] { At(a, v + T, ha - 0.05), At(z, v + T, hz - 0.05), At(z, v + T, hz), At(a, v + T, ha) }, across),
                        (new[] { At(a, v - T, ha - 0.05), At(a, v - T, ha), At(z, v - T, hz), At(z, v - T, hz - 0.05) }, across.Negate()),
                    };
                    foreach (var (corners, facing) in faces) @out.Poly(corners, corners.Select((c) => (c.x + c.z, c.y)).ToArray(), Swatch.Flat, tint, b, facing);
                    // Balusters on every third tread, and a newel post at the foot.
                    for (int m = (int)Math.Ceiling((j * steps) / (double)n); m < ((j + 1) * steps) / (double)n; m++)
                    {
                        if (m % 3 != 1 || m >= steps - 1) continue;
                        double u = -n / 2.0 + ((m + 0.5) * n) / steps;
                        Box(u - T, v - T, @base + ((m + 1) / (double)steps) * Storey, u + T, v + T, Rail(u) - 0.03);
                    }
                    if (j == 0) Box(-n / 2.0, v - T, @base, -n / 2.0 + 2 * T, v + T, ha + 0.04);
                }
            }
            // The stairwell above, open to the landing (the tile the top faces).
            @out.Level = k + 1;
            double floor = @base + Storey;
            // A railing from (ua, va) to (ub, vb), with a post at each end.
            void Railing(double ua, double va, double ub, double vb, (double u, double v)[] posts)
            {
                Box(ua, va, floor + RAIL - 0.05, ub, vb, floor + RAIL);
                Box(ua, va, floor + 0.42, ub, vb, floor + 0.46);
                foreach (var (u, vv) in posts) Box(u - T, vv - T, floor, u + T, vv + T, floor + RAIL);
            }
            // Whether the stairwell's edge towards (x, y) needs no railing: a wall there, or the next flight's foot of the way back.
            bool Shut(int x, int y) => walled(x, y, k + 1) || plan.KindAt(x, y, k + 1) == TileKinds.Ladder;
            var lowest = run[0];
            if (!Shut(lowest.X - d.X, lowest.Y - d.Y)) Railing(-n / 2.0, -0.5, -n / 2.0 + 2 * T, 0.5, new[] { (-n / 2.0, -0.5), (-n / 2.0, 0.5) });
            foreach (int dv in new[] { 1, -1 })
            {
                for (int j = 0; j < n; j++)
                {
                    var tile = run[j];
                    if (Shut(tile.X - d.Y * dv, tile.Y + d.X * dv)) continue;
                    double ua = -n / 2.0 + j, ub = -n / 2.0 + j + 1;
                    Railing(ua, dv * 0.5 - T, ub, dv * 0.5 + T, new[] { (ua, dv * 0.5), (ub, dv * 0.5) });
                }
            }
        }

        /// <summary>The walkable tops of flats (tar) and platforms (a steel deck), posts at their outer corners (brick, steel), and the top of solid wall masses.</summary>
        static void RaisedSurfaces(GameState s, BuildingPlan plan, Surfaces tops, Surfaces solid)
        {
            int w = s.Width, h = s.Height;
            int Elev(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return 0;
                int i = y * w + x;
                return s.Elev != null && i < s.Elev.Count ? s.Elev[i] : 0;
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (plan.Massive[i] != 0)
                    {
                        double top = (Elev(x, y) + 1) * Storey;
                        Slab(tops, x, y, x + 1, y + 1, top, Swatch.Flat, Concrete, -1);
                    }
                    byte style = plan.Raised[i];
                    if (style != BuildingPlan.RaisedFlats && style != BuildingPlan.RaisedPlatform) continue;
                    int e = Elev(x, y);
                    double deckTop = e * Storey + 0.003;
                    var deck = style == BuildingPlan.RaisedFlats ? Tar : Deck;
                    Slab(tops, x, y, x + 1, y + 1, deckTop, Swatch.Flat, deck, -1);
                    // Outer corners: two lower sides that meet.
                    for (int k = 0; k < 4; k++)
                    {
                        var a = Grid.Dirs4[k];
                        var b = Grid.Dirs4[(k + 1) % 4];
                        if (Elev(x + a.X, y + a.Y) >= e || Elev(x + b.X, y + b.Y) >= e) continue;
                        int low = Math.Max(Elev(x + a.X, y + a.Y), Elev(x + b.X, y + b.Y));
                        double cx = x + 0.5 + (a.X + b.X) * 0.5;
                        double cz = y + 0.5 + (a.Y + b.Y) * 0.5;
                        double r = style == BuildingPlan.RaisedFlats ? 0.09 : 0.06;
                        solid.Box(V(cx - r, low * Storey, cz - r), V(cx + r, e * Storey + 0.08, cz + r), Swatch.Flat, style == BuildingPlan.RaisedFlats ? Brick : Steel, -1);
                    }
                }
            }
        }

        /// <summary>
        /// Where a roof one walks on takes props (buildWalls' `deck`): its top storey's floor at (x, y), with a slab all round
        /// it (off the stairwell's edge and its railing).
        /// </summary>
        public static bool DeckAt(BuildingPlan plan, Building b, int x, int y) =>
            plan.KindAt(x, y, b.storeys) == TileKinds.Floor && Grid.Dirs8.All((d) => plan.KindAt(x + d.X, y + d.Y, b.storeys) != null);

        /// <summary>
        /// What stands on the roof of b, always the same for a building (buildings.ts roofProps): a chimney on some houses;
        /// air conditioners, vents and a dish on flat roofs, the stair housing on offices, skylights in a row on warehouses.
        /// On a roof one walks on, only what's been left up there, where `open` says (DeckAt; null: nowhere). Needs b.top
        /// (Of sets it).
        /// </summary>
        public static List<RoofProp> RoofProps(Building b, Func<int, int, bool> open = null)
        {
            var @out = new List<RoofProp>();
            double R(int salt) => BuildingPlan.Hash(b.x0, b.y0, 40 + salt);
            bool along = b.x1 - b.x0 >= b.y1 - b.y0;
            double cx = (b.x0 + b.x1) / 2.0 + 0.5;
            double cz = (b.y0 + b.y1) / 2.0 + 0.5;
            if (b.kind == BuildingKinds.House && !b.walkableRoof)
            {
                if (R(0) < 0.65)
                {
                    // On the ridge near one end, half sunk into the roof.
                    double L = (along ? b.x1 - b.x0 : b.y1 - b.y0) / 2.0 - 1.1;
                    double u = (R(1) < 0.5 ? -1 : 1) * L;
                    const double v = 0.35;
                    @out.Add(new RoofProp("chimney", along ? cx + u : cx + v, b.top - 1.0, along ? cz + v : cz + u, along ? 0 : Math.PI / 2));
                }
                return @out;
            }
            if (b.kind == BuildingKinds.Barn || b.kind == BuildingKinds.Ruin) return @out;
            double y = b.@base + Storey * b.storeys + 0.04;
            if (b.radio)
            {
                // A siege's Sala de radio (docs/FASE12.md §10): Apocalypse's lattice mast in the middle of the roof, its head of
                // aerials on top, a dish by the parapet.
                @out.Add(new RoofProp("radioMast", cx, y, cz, 0));
                @out.Add(new RoofProp("radioMastTop", cx, y + RadioMast, cz, 0));
                @out.Add(new RoofProp("roofDish", b.x0 + 1.5, y, b.y1 - 1, Math.PI * 0.75));
                return @out;
            }
            // A roof one walks on keeps clear: only what's been left up there, never over the stairwell.
            bool walk = b.walkableRoof;
            // Free spots on the roof, a tile in from the parapet, taken as things are put down.
            var taken = new HashSet<(int, int)>();
            bool Place(string id, double x, double z, double w, double d, double yaw)
            {
                var cells = new List<(int, int)>();
                for (int i = (int)Math.Floor(x - w / 2); i <= Math.Floor(x + w / 2 - 0.01); i++) for (int j = (int)Math.Floor(z - d / 2); j <= Math.Floor(z + d / 2 - 0.01); j++) cells.Add((i, j));
                if (x - w / 2 < b.x0 + 1.2 || x + w / 2 > b.x1 - 0.2 || z - d / 2 < b.y0 + 1.2 || z + d / 2 > b.y1 - 0.2 || cells.Any(taken.Contains)) return false;
                foreach (var c in cells) taken.Add(c);
                @out.Add(new RoofProp(id, x, y, z, yaw));
                return true;
            }
            (double x, double z) Spot(int salt) => (b.x0 + 1.7 + R(salt) * (b.x1 - b.x0 - 2.4), b.y0 + 1.7 + R(salt + 1) * (b.y1 - b.y0 - 2.4));
            if (b.kind == BuildingKinds.Hall && !walk && BuildingPlan.IsShed(b.kit))
            {
                // A shed's roof light: flat skylights end to end down the middle (the model's corner is its origin).
                const double size = 2.5 * SyntyScale;
                int L = along ? b.x1 - b.x0 : b.y1 - b.y0;
                int n = Math.Max(1, (int)Math.Floor((L - 3) / size));
                for (int k = 0; k < n; k++)
                {
                    double u = (k - n / 2.0) * size;
                    double x = along ? cx + u : cx - size / 2;
                    double z = along ? cz - size / 2 : cz + u;
                    @out.Add(new RoofProp("skylight", x, y, z, 0));
                    for (int i = (int)Math.Floor(x); i <= Math.Floor(x + size); i++) for (int j = (int)Math.Floor(z); j <= Math.Floor(z + size); j++) taken.Add((i, j));
                }
            }
            else if (b.kind == BuildingKinds.Hall && !walk)
            {
                // Skylights down the middle.
                int n = (int)Math.Floor((along ? b.x1 - b.x0 : b.y1 - b.y0) / 3.0);
                for (int k = 0; k < n; k++)
                {
                    double u = (k - (n - 1) / 2.0) * 3;
                    Place("roofSkylight", along ? cx + u : cx, along ? cz : cz + u, along ? 1.4 : 0.9, along ? 0.9 : 1.4, along ? 0 : Math.PI / 2);
                }
            }
            if (b.kind == BuildingKinds.Office && !walk)
            {
                var (x, z) = Spot(2);
                Place("roofAccess", x, z, 2.4, 2.4, Math.Floor(R(4) * 4) * (Math.PI / 2));
            }
            int area = (b.x1 - b.x0) * (b.y1 - b.y0);
            // A billboard on some shops and offices, at the roof's edge facing the default view (south or east).
            if ((b.kind == BuildingKinds.Shop || b.kind == BuildingKinds.Office) && !walk && area >= 30 && R(60) < 0.45)
            {
                bool south = R(61) < 0.5;
                double bx = south ? cx + (R(62) - 0.5) * 2 : b.x1 - 0.4;
                double bz = south ? b.y1 - 0.4 : cz + (R(62) - 0.5) * 2;
                // The frame is long along z and faces x: turned a quarter for the south edge.
                double yaw = south ? -Math.PI / 2 : 0;
                @out.Add(new RoofProp("billboard", bx, y, bz, yaw));
                const double off = 0.32;
                @out.Add(new RoofProp(R(63) < 0.5 ? "billboardSign" : "billboardSign2", bx + (south ? 0 : off), y + 0.7, bz + (south ? off : 0), yaw));
            }
            // What's been left up there: weeds and moss, rubble, bags, a pallet, a tyre (they take no room); on a
            // roof nobody walks, a crate too (second look against the capture: its roofs are cluttered).
            var clutter = new List<string> { "tuftLow", "tuft", "grassBlob", "trashBag", "pallet", "pebbles", "tire", "trashBag2", "cardboard", "papers", "grassBlob", "pebbles" };
            if (!walk) clutter.AddRange(new[] { "crate", "crateOpen" });
            int litter = (int)Math.Min(16, Js.Round(area / 8.0));
            for (int k = 0; k < litter; k++)
            {
                var (x, z) = Spot(80 + k * 2);
                if (walk && !(open != null && open((int)Math.Floor(x), (int)Math.Floor(z)))) continue;
                @out.Add(new RoofProp(clutter[(int)Math.Floor(R(120 + k) * clutter.Count)], x, y + 0.003, z, R(140 + k) * Math.PI * 2));
            }
            if (walk) return @out;
            int count = (int)Math.Min(6, Math.Max(2, Js.Round(area / 22.0)));
            var kinds = new[] { "roofAircon", "roofAirconLong", "roofVent", "roofAircon", "roofVent", "roofDish" };
            for (int k = 0, tries = 0; k < count && tries < 24; tries++)
            {
                string id = kinds[(k + (int)Math.Floor(R(5) * 3)) % kinds.Length];
                var (x, z) = Spot(10 + tries * 2);
                bool turned = R(30 + tries) < 0.5;
                double big = id == "roofAirconLong" ? 1.6 : 1;
                if (Place(id, Js.Round(x) + 0.5, Js.Round(z) + 0.5, turned ? 1 : big, turned ? big : 1, (turned ? Math.PI / 2 : 0) + (R(50 + tries) < 0.5 ? Math.PI : 0))) k++;
            }
            return @out;
        }
    }
}
