using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bastion.Engine;
using UnityEngine;
using Trig = Bastion.Client.Map.HomeFurnishing.Trig;

namespace Bastion.Client.Map
{
    // 0.10.1's basePieces.ts: the new base (docs/FASE12.md §11) on the map, its pieces, for its siege (mapView) and for the
    // base's own screen (clearingArt, with the same MapView). buildings.ts and ground.ts see open, flat ground where a piece
    // stands (TerrainOf); this draws the piece, as worn as it is, or what is left of it: the caravan (the Bastion of its
    // stage, with its yard), what is built by hand, the tower and the lights, the traps and the turret, and the buildings at
    // their levels 1–3 or their ruins. What 0.10.1 gave PieceArt.put comes out as a list of PiecePlacement; what PieceArt.top
    // and size read off a loaded model comes in (PropMeasure). The sandbags are not drawn here: mapView draws them as a
    // siege's barricade's.
    // The math is three's in doubles (Matrix4.compose, Quaternion.setFromEuler 'YXZ' and setFromAxisAngle, makeRotationX/Z,
    // makeTranslation, makeBasis, scale, multiply), each element made a float only as it goes into the matrix; sines and
    // cosines V8's (HomeFurnishing.Trig), atan and atan2 Js.cs's. No Mathf: it runs in a plain dotnet harness.

    /// <summary>A piece on the map (basePieces.ts PieceSpot): its tiles, its middle and the tile that draws it (the one under its middle).</summary>
    public sealed class PieceSpot
    {
        public BasePiece piece;
        /// <summary>Its kind's (Base.Pieces); one built before the base grew, at the size it has (BasePiece.size).</summary>
        public PieceDef def;
        /// <summary>Its tiles (Base.CellsOf); the caravan's, once it is a building, the Bastion's footprint, all `wall`.</summary>
        public List<PlacedCell> cells;
        /// <summary>Its middle, in tiles (z is the engine's y).</summary>
        public double x, z;
        public int anchor;
        /// <summary>Its turn about y, three's way (counter-clockwise from above), for its `rot` quarter turns clockwise.</summary>
        public double yaw;
    }

    /// <summary>The map's pieces, and the one on each tile (basePieces.ts PieceMap).</summary>
    public sealed class PieceMap
    {
        public List<PieceSpot> spots;
        /// <summary>The spot on each tile, y * width + x: an index into `spots`; -1, none (an Int16Array).</summary>
        public short[] at;
        /// <summary>The Bastion's yard, by tile (Base.BastionYard): low cover in a siege, drawn with the Bastion, nothing else there.</summary>
        public JsSet<int> yard;
    }

    /// <summary>
    /// What drawing a piece reads off a prop's model as loaded (basePieces.ts PieceArt.top and size; mapView.ts topOf and
    /// sizeOf): how high it stands as placed (its parts' highest point; 0.3 while it isn't loaded), and its parts' box's size
    /// as placed ((1, 1, 1) while it isn't), in tiles, in the prop's own frame.
    /// </summary>
    public readonly struct PropMeasure
    {
        public readonly double top;
        public readonly double x, y, z;

        public PropMeasure(double top, double x, double y, double z)
        {
            this.top = top;
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>A model not loaded yet, as mapView.ts measures it.</summary>
        public static readonly PropMeasure Unloaded = new PropMeasure(0.3, 1, 1, 1);
    }

    /// <summary>
    /// A prop a piece is drawn with (what 0.10.1 gave PieceArt.put): which (PropId), the matrix that places it in 0.10.1's
    /// world, in tiles (x east, y up, z south), the same mathematical matrix three's Matrix4 was; whether it casts a shadow
    /// (put's `shadows`, true when not said); and the piece it is of: its spot (an index into PieceMap.spots, as into
    /// GameState.base.pieces) and the tile that draws it (PieceSpot.anchor: mapView redraws by it).
    /// </summary>
    public sealed class PiecePlacement
    {
        public string id;
        public Matrix4x4 matrix;
        public bool shadows;
        public int spot;
        public int anchor;
    }

    /// <summary>The base's pieces on the map (basePieces.ts pieceMap, terrainOf, pieceWear, pieceKey, drawPiece).</summary>
    public static class BasePieces
    {
        /// <summary>A storey's height in tiles (coords.ts STOREY).</summary>
        const double Storey = 2.3;

        /// <summary>The pieces drawn here (DRAWN): all of them.</summary>
        public static readonly HashSet<string> Drawn = new HashSet<string>(Base.PieceOrder);

        /// <summary>
        /// The tower's ladder (TOWER_LADDER): its foot this far from its tile's middle towards the deck, its top over the
        /// deck's edge, leant `lean` tiles towards the deck for each tile up (its tilt's tangent), `top` tall. The units
        /// climb it by the same numbers (unitViews.ts rungsOf).
        /// </summary>
        public const double TowerLadderFoot = 0.1;
        /// <summary>Math.tan(0.16) as V8 gives it (0x3FC4A815090D4ADD): fdlibm's, which the C runtime's tan need not be.</summary>
        public const double TowerLadderLean = 0.16137946073521095;
        public const double TowerLadderTop = Storey + 0.35;

        /// <summary>How tall a palisade, a wall and a gate stand (a little lower than a storey's walls).</summary>
        const double FenceHeight = 1.9;
        /// <summary>What a palisade, a wall or a gate joins up with: one another, the buildings, the tower.</summary>
        static readonly HashSet<string> Joins = new HashSet<string> { PieceIds.Palisade, PieceIds.Wall, PieceIds.Gate, PieceIds.Tower };
        static readonly HashSet<string> Hard = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.DoorOpen, TileKinds.Low, TileKinds.High };

        /// <summary>Grid.Dirs4 (UnityEngine has a Grid of its own).</summary>
        static readonly IReadOnlyList<Vec2> Dirs4 = Bastion.Engine.Grid.Dirs4;

        /// <summary>GameState.base as base.ts's functions take it (`s.base as unknown as BaseState`: they read its pieces and level).</summary>
        static BaseState AsBase(GameState s) => new BaseState { Width = s.Width, Height = s.Height, Pieces = s.Base.Pieces, Felled = s.Base.Felled, Level = s.Base.Level };

        /// <summary>pieceMap: the base's pieces on the map; null on a map with no base.</summary>
        public static PieceMap MapOf(GameState s)
        {
            if (s.Base == null) return null;
            int w = s.Width;
            var at = new short[w * s.Height];
            for (int i = 0; i < at.Length; i++) at[i] = -1;
            var @base = AsBase(s);
            var spots = new List<PieceSpot>();
            for (int k = 0; k < s.Base.Pieces.Count; k++)
            {
                var piece = s.Base.Pieces[k];
                // The caravan stands for the Bastion: the building of its stage, on its footprint.
                var cells = piece.Piece == PieceIds.Caravan && Base.BastionStage(@base) > 0
                    ? Base.BastionFootprint(@base).Select((c) => new PlacedCell { X = c.X, Y = c.Y, Tile = TileKinds.Wall }).ToList()
                    : Base.CellsOf(piece);
                foreach (var c in cells)
                {
                    // (Past the typed array's end a write goes nowhere.)
                    int i = c.Y * w + c.X;
                    if (i >= 0 && i < at.Length) at[i] = (short)k;
                }
                double x = (cells.Min((c) => c.X) + cells.Max((c) => c.X) + 1) / 2.0;
                double z = (cells.Min((c) => c.Y) + cells.Max((c) => c.Y) + 1) / 2.0;
                // A piece of a base saved before 20/10 keeps its old size (BasePiece.size) until it is levelled up or rebuilt.
                var kind = Base.Pieces[piece.Piece];
                var def = piece.Size == null ? kind : new PieceDef
                {
                    Id = kind.Id, Name = kind.Name, Kind = kind.Kind, Size = piece.Size, Cells = kind.Cells, Tile = kind.Tile, Cost = kind.Cost, Hp = kind.Hp,
                    Level = kind.Level, Unique = kind.Unique, Facility = kind.Facility, Charges = kind.Charges,
                };
                spots.Add(new PieceSpot
                {
                    piece = piece, def = def, cells = cells, x = x, z = z, anchor = (int)Math.Floor(z) * w + (int)Math.Floor(x),
                    // (-rot as JavaScript has it: -0 for 0.)
                    yaw = (-(double)piece.Rot * Math.PI) / 2,
                });
            }
            var yard = new JsSet<int>();
            foreach (var p in Base.BastionYard(@base)) yard.Add(p.Y * w + p.X);
            return new PieceMap { spots = spots, at = at, yard = yard };
        }

        /// <summary>GameState's properties, for TerrainOf's copy.</summary>
        static readonly PropertyInfo[] StateProperties = typeof(GameState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where((p) => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();

        /// <summary>
        /// terrainOf: the map as buildings.ts and ground.ts are to see it: what this draws taken off (open, flat ground), and
        /// the base's tracks (YARD) as the wilds' paths of packed earth, not a yard's tarmac. What it doesn't say is the
        /// state's own: 0.10.1's had the state as its prototype; this one is a shallow copy (the same lists, units, base…)
        /// with its own tiles, elev and ground.
        /// </summary>
        public static GameState TerrainOf(GameState s, PieceMap map)
        {
            var tiles = new List<string>(s.Tiles);
            var elev = new List<int>(s.Elev);
            foreach (var spot in map.spots)
            {
                if (!Drawn.Contains(spot.piece.Piece)) continue;
                foreach (var c in spot.cells)
                {
                    int i = c.Y * s.Width + c.X;
                    if (i < 0 || i >= tiles.Count) continue;
                    tiles[i] = TileKinds.Floor;
                    if (i < elev.Count) elev[i] = 0;
                }
            }
            foreach (int i in map.yard) if (i >= 0 && i < tiles.Count) tiles[i] = TileKinds.Floor;
            var ground = s.Ground?.Select((g) => g == 3 ? 1 : g).ToList();
            var @out = new GameState();
            foreach (var p in StateProperties) p.SetValue(@out, p.GetValue(s));
            @out.Tiles = tiles;
            @out.Elev = elev;
            @out.Ground = ground;
            return @out;
        }

        /// <summary>siegeDressing.ts wearStage: 0 whole, 1 knocked about, 2 about to go.</summary>
        static int WearStage(double left, double full) => left * 3 <= full ? 2 : left * 3 <= full * 2 ? 1 : 0;

        /// <summary>
        /// pieceWear: how worn a piece is: 0 whole, 1 knocked about, 2 about to go, 3 given way (its ruin). In a siege, by what
        /// its tiles still hold (GameState.breakables, against its health: the dead strike a tile), given way once none
        /// stands; on the base's screen, or for a piece the dead can't strike (a deck, a trap), by its health.
        /// </summary>
        public static int PieceWear(GameState s, PieceSpot spot)
        {
            if (spot.piece.Hp <= 0) return 3;
            var hard = spot.cells.Where((c) => (c.Elev ?? 0) == 0 && Hard.Contains(c.Tile)).ToList();
            if (s.Breakables == null || hard.Count == 0) return WearStage(spot.piece.Hp, spot.def.Hp);
            double least = double.PositiveInfinity;
            int standing = 0;
            foreach (var c in hard)
            {
                int i = c.Y * s.Width + c.X;
                if (i >= 0 && i < s.Tiles.Count && Hard.Contains(s.Tiles[i])) standing++;
                if (s.Breakables.TryGetValue(Bastion.Engine.Grid.NodeOf(s, new Pos { X = c.X, Y = c.Y, Z = 0 }), out int left)) least = Math.Min(least, left);
            }
            if (standing == 0) return 3;
            return WearStage(Math.Min(least, spot.piece.Hp), spot.def.Hp);
        }

        /// <summary>A trap's or a turret's uses left: in the siege's GameState.traps, or on the base's own piece.</summary>
        static int ChargesOf(GameState s, BasePiece p) => s.Traps != null ? (s.Traps.FirstOrDefault((t) => t.Piece == p.Id)?.Charges ?? 0) : (p.Charges ?? 0);

        /// <summary>pieceKey: what a piece looks like as drawn: a change in it (a level up, a stage of wear, a trap spent) redraws it.</summary>
        public static string PieceKey(GameState s, PieceSpot spot)
        {
            var p = spot.piece;
            int charges = ChargesOf(s, p);
            // A gate hangs by the gates beside it (DrawPiece): one put or taken away next to it redraws it too.
            string beside = p.Piece == PieceIds.Gate
                ? string.Concat(Dirs4.Select((d) => s.Base != null && s.Base.Pieces.Any((o) => o.Piece == PieceIds.Gate && o.X == p.X + d.X && o.Y == p.Y + d.Y) ? "1" : "0"))
                : "";
            string caravan = p.Piece == PieceIds.Caravan ? FormattableString.Invariant($"|{s.Base?.Level ?? 1}") : "";
            return FormattableString.Invariant($"{p.Piece}|{p.X},{p.Y},{p.Rot}|{p.Level ?? 0}|{PieceWear(s, spot)}|{charges}{caravan}{beside}");
        }

        /// <summary>Every piece of the map as drawn, each at its wear (as mapView and clearingArt draw them); none with no base.</summary>
        public static List<PiecePlacement> Placements(GameState s, Func<string, PropMeasure> measure)
        {
            var map = MapOf(s);
            return map == null ? new List<PiecePlacement>() : Placements(s, map, measure);
        }

        /// <summary>Every piece of `map` as drawn, each at its wear (PieceWear), in the order of its spots.</summary>
        public static List<PiecePlacement> Placements(GameState s, PieceMap map, Func<string, PropMeasure> measure)
        {
            var @out = new List<PiecePlacement>();
            foreach (var spot in map.spots) DrawPiece(s, map, spot, PieceWear(s, spot), measure, @out);
            return @out;
        }

        // ------------------------------------------------------------------ three's math, in doubles

        /// <summary>A THREE.Vector3's doubles.</summary>
        readonly struct V3
        {
            public readonly double x, y, z;

            public V3(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            /// <summary>crossVectors(a, b).</summary>
            public static V3 Cross(V3 a, V3 b) => new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

            /// <summary>addScaledVector(v, s).</summary>
            public V3 AddScaled(V3 v, double s) => new V3(x + v.x * s, y + v.y * s, z + v.z * s);

            /// <summary>applyAxisAngle(axis, angle): applyQuaternion(setFromAxisAngle(axis, angle)).</summary>
            public V3 ApplyAxisAngle(V3 axis, double angle)
            {
                double half = angle / 2, s = Trig.Sin(half);
                double qx = axis.x * s, qy = axis.y * s, qz = axis.z * s, qw = Trig.Cos(half);
                double tx = 2 * (qy * z - qz * y);
                double ty = 2 * (qz * x - qx * z);
                double tz = 2 * (qx * y - qy * x);
                return new V3(x + qw * tx + qy * tz - qz * ty, y + qw * ty + qz * tx - qx * tz, z + qw * tz + qx * ty - qy * tx);
            }
        }

        /// <summary>A THREE.Quaternion's doubles.</summary>
        readonly struct Quat
        {
            public readonly double x, y, z, w;

            Quat(double x, double y, double z, double w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }

            /// <summary>`setFromEuler(new THREE.Euler(ex, ey, ez, 'YXZ'))`.</summary>
            public static Quat Euler(double ex, double ey, double ez)
            {
                double c1 = Trig.Cos(ex / 2), c2 = Trig.Cos(ey / 2), c3 = Trig.Cos(ez / 2);
                double s1 = Trig.Sin(ex / 2), s2 = Trig.Sin(ey / 2), s3 = Trig.Sin(ez / 2);
                return new Quat(s1 * c2 * c3 + c1 * s2 * s3, c1 * s2 * c3 - s1 * c2 * s3, c1 * c2 * s3 - s1 * s2 * c3, c1 * c2 * c3 + s1 * s2 * s3);
            }
        }

        /// <summary>A THREE.Matrix4 in doubles: its elements column-major (te[0..3] the first column, te[12..14] the position).</summary>
        sealed class Mat
        {
            public readonly double[] te = new double[16];

            /// <summary>`set(…)`: the numbers row by row, as three takes them.</summary>
            static Mat Rows(double n11, double n12, double n13, double n14, double n21, double n22, double n23, double n24,
                double n31, double n32, double n33, double n34, double n41, double n42, double n43, double n44)
            {
                var m = new Mat();
                var te = m.te;
                te[0] = n11; te[4] = n12; te[8] = n13; te[12] = n14;
                te[1] = n21; te[5] = n22; te[9] = n23; te[13] = n24;
                te[2] = n31; te[6] = n32; te[10] = n33; te[14] = n34;
                te[3] = n41; te[7] = n42; te[11] = n43; te[15] = n44;
                return m;
            }

            /// <summary>`compose((px, py, pz), q, (sx, sy, sz))`.</summary>
            public static Mat Compose(double px, double py, double pz, Quat q, double sx, double sy, double sz)
            {
                double x = q.x, y = q.y, z = q.z, w = q.w;
                double x2 = x + x, y2 = y + y, z2 = z + z;
                double xx = x * x2, xy = x * y2, xz = x * z2;
                double yy = y * y2, yz = y * z2, zz = z * z2;
                double wx = w * x2, wy = w * y2, wz = w * z2;
                var m = new Mat();
                var te = m.te;
                te[0] = (1 - (yy + zz)) * sx;
                te[1] = (xy + wz) * sx;
                te[2] = (xz - wy) * sx;
                te[3] = 0;
                te[4] = (xy - wz) * sy;
                te[5] = (1 - (xx + zz)) * sy;
                te[6] = (yz + wx) * sy;
                te[7] = 0;
                te[8] = (xz + wy) * sz;
                te[9] = (yz - wx) * sz;
                te[10] = (1 - (xx + yy)) * sz;
                te[11] = 0;
                te[12] = px;
                te[13] = py;
                te[14] = pz;
                te[15] = 1;
                return m;
            }

            public static Mat RotationX(double theta)
            {
                double c = Trig.Cos(theta), s = Trig.Sin(theta);
                return Rows(1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0, 0, 0, 0, 1);
            }

            public static Mat RotationZ(double theta)
            {
                double c = Trig.Cos(theta), s = Trig.Sin(theta);
                return Rows(c, -s, 0, 0, s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
            }

            public static Mat Translation(double x, double y, double z) => Rows(1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, z, 0, 0, 0, 1);

            /// <summary>`makeBasis(x, y, z)`: the three as its first three columns.</summary>
            public static Mat Basis(V3 x, V3 y, V3 z) => Rows(x.x, y.x, z.x, 0, x.y, y.y, z.y, 0, x.z, y.z, z.z, 0, 0, 0, 0, 1);

            /// <summary>`scale((x, y, z))`: its columns scaled.</summary>
            public Mat Scale(double x, double y, double z)
            {
                te[0] *= x; te[4] *= y; te[8] *= z;
                te[1] *= x; te[5] *= y; te[9] *= z;
                te[2] *= x; te[6] *= y; te[10] *= z;
                te[3] *= x; te[7] *= y; te[11] *= z;
                return this;
            }

            public Mat Clone()
            {
                var m = new Mat();
                Array.Copy(te, m.te, 16);
                return m;
            }

            /// <summary>`multiply(b)`: this one times b (multiplyMatrices(this, b)), in its place.</summary>
            public Mat Multiply(Mat b)
            {
                var ae = te;
                var be = b.te;
                double a11 = ae[0], a12 = ae[4], a13 = ae[8], a14 = ae[12];
                double a21 = ae[1], a22 = ae[5], a23 = ae[9], a24 = ae[13];
                double a31 = ae[2], a32 = ae[6], a33 = ae[10], a34 = ae[14];
                double a41 = ae[3], a42 = ae[7], a43 = ae[11], a44 = ae[15];
                double b11 = be[0], b12 = be[4], b13 = be[8], b14 = be[12];
                double b21 = be[1], b22 = be[5], b23 = be[9], b24 = be[13];
                double b31 = be[2], b32 = be[6], b33 = be[10], b34 = be[14];
                double b41 = be[3], b42 = be[7], b43 = be[11], b44 = be[15];
                te[0] = a11 * b11 + a12 * b21 + a13 * b31 + a14 * b41;
                te[4] = a11 * b12 + a12 * b22 + a13 * b32 + a14 * b42;
                te[8] = a11 * b13 + a12 * b23 + a13 * b33 + a14 * b43;
                te[12] = a11 * b14 + a12 * b24 + a13 * b34 + a14 * b44;
                te[1] = a21 * b11 + a22 * b21 + a23 * b31 + a24 * b41;
                te[5] = a21 * b12 + a22 * b22 + a23 * b32 + a24 * b42;
                te[9] = a21 * b13 + a22 * b23 + a23 * b33 + a24 * b43;
                te[13] = a21 * b14 + a22 * b24 + a23 * b34 + a24 * b44;
                te[2] = a31 * b11 + a32 * b21 + a33 * b31 + a34 * b41;
                te[6] = a31 * b12 + a32 * b22 + a33 * b32 + a34 * b42;
                te[10] = a31 * b13 + a32 * b23 + a33 * b33 + a34 * b43;
                te[14] = a31 * b14 + a32 * b24 + a33 * b34 + a34 * b44;
                te[3] = a41 * b11 + a42 * b21 + a43 * b31 + a44 * b41;
                te[7] = a41 * b12 + a42 * b22 + a43 * b32 + a44 * b42;
                te[11] = a41 * b13 + a42 * b23 + a43 * b33 + a44 * b43;
                te[15] = a41 * b14 + a42 * b24 + a43 * b34 + a44 * b44;
                return this;
            }

            /// <summary>As floats: each element to the same row and column (m00 = te[0], m10 = te[1], … m23 = te[14]).</summary>
            public Matrix4x4 ToMatrix()
            {
                var m = new Matrix4x4();
                m.m00 = (float)te[0]; m.m10 = (float)te[1]; m.m20 = (float)te[2]; m.m30 = (float)te[3];
                m.m01 = (float)te[4]; m.m11 = (float)te[5]; m.m21 = (float)te[6]; m.m31 = (float)te[7];
                m.m02 = (float)te[8]; m.m12 = (float)te[9]; m.m22 = (float)te[10]; m.m32 = (float)te[11];
                m.m03 = (float)te[12]; m.m13 = (float)te[13]; m.m23 = (float)te[14]; m.m33 = (float)te[15];
                return m;
            }
        }

        /// <summary>A matrix at (x, y, z): turned `yaw` about y, then leant `roll` about its own z and `pitch` about its own x; scaled.</summary>
        static Mat At(double x, double y, double z, double yaw, double sx = 1, double sy = 1, double sz = 1, double roll = 0, double pitch = 0) =>
            Mat.Compose(x, y, z, Quat.Euler(pitch, yaw, roll), sx, sy, sz);

        // ------------------------------------------------------------------ drawing

        /// <summary>Stable pseudo-random value in [0, 1) (basePieces.ts hash: JavaScript's ^ on 32 bits, >>> 0).</summary>
        static double Hash(int x, int y, int salt)
        {
            unchecked
            {
                int h = (x + salt * 31) * 73856093 ^ y * 19349663 ^ salt * 83492791;
                return (uint)h % 1000 / 1000.0;
            }
        }

        /// <summary>Quarter turns (counter-clockwise from above) that make a prop modelled facing north face DIRS4[f].</summary>
        static int TurnTo(int f) => (4 - f) % 4;

        /// <summary>Math.sign: its zeros as they come (Js.Sign makes −0 +0).</summary>
        static double Sign(double v) => v > 0 ? 1 : v < 0 ? -1 : v;

        /// <summary>`v || 1`.</summary>
        static double Or1(double v) => v == 0 || double.IsNaN(v) ? 1 : v;

        /// <summary>siegeDressing.ts inwards: the way into the map's middle from (x, y), along its longer leg (x and z, ±1 or 0).</summary>
        static (double x, double z) Inwards(GameState s, int x, int y)
        {
            double dx = s.Width / 2.0 - (x + 0.5);
            double dz = s.Height / 2.0 - (y + 0.5);
            return Math.Abs(dx) > Math.Abs(dz) ? (Sign(dx), 0.0) : (0.0, Sign(dz));
        }

        /// <summary>What draws a piece (PieceArt): props put at their matrices into the list, and how tall and big a prop's model is.</summary>
        sealed class Art
        {
            readonly List<PiecePlacement> @out;
            readonly Func<string, PropMeasure> measure;
            readonly int spot;
            readonly int anchor;

            public Art(List<PiecePlacement> @out, Func<string, PropMeasure> measure, int spot, int anchor)
            {
                this.@out = @out;
                this.measure = measure;
                this.spot = spot;
                this.anchor = anchor;
            }

            public void Put(string id, Mat m, bool shadows = true) =>
                @out.Add(new PiecePlacement { id = id, matrix = m.ToMatrix(), shadows = shadows, spot = spot, anchor = anchor });

            public double Top(string id) => measure(id).top;

            public PropMeasure Size(string id) => measure(id);
        }

        /// <summary>
        /// A piece of sandbags as mapView.ts draws it (its siege barricade's `sandbags`, across the piece's turn): the wall
        /// of bags lower the more worn, and the bags knocked off, most out where the dead strike, a few in.
        /// </summary>
        public static void DrawSandbags(GameState s, PieceMap map, PieceSpot spot, int wear, List<PiecePlacement> @out) =>
            Sandbags(s, spot.piece.X, spot.piece.Y, 0, wear, spot.piece.Rot % 2 != 0 ? 0 : Math.PI / 2, new Art(@out, null, map.spots.IndexOf(spot), spot.anchor));

        /// <summary>mapView.ts sandbags: a barricade's bags on tile (x, y), `base` tiles up, across `yaw`, at `wear` (3: given way).</summary>
        static void Sandbags(GameState s, int x, int y, double @base, int wear, double yaw, Art art)
        {
            double R(int salt) => Hash(x, y, 80 + salt);
            art.Put("sandbags", At(x + 0.5, @base, y + 0.5, yaw + (R(0) - 0.5) * 0.08 * wear, 1, new[] { 1, 0.78, 0.5, 0.2 }[wear], 1));
            var into = Inwards(s, x, y);
            int loose = new[] { 0, 2, 4, 7 }[wear];
            for (int k = 0; k < loose; k++)
            {
                double side = R(10 + k) < 0.7 ? -1 : 1;
                double d = 0.32 + R(20 + k) * (wear == 3 ? 0.55 : 0.3);
                double t = (R(30 + k) - 0.5) * 0.9;
                double px = x + 0.5 + into.x * side * d + into.z * t;
                double pz = y + 0.5 + into.z * side * d + into.x * t;
                art.Put("sandbag", At(px, @base + (k > 3 ? 0.05 : 0.01), pz, R(40 + k) * Math.PI * 2), false);
            }
        }

        // ------------------------------------------------------------------ a siege's doors and barricades

        /// <summary>siegeDressing.ts madeAs: what tile (x, y) was when the hand-made map was drawn (its legend: '+' a door, 'b' a barricade), or none.</summary>
        public static char? MadeAs(GameState s, int x, int y)
        {
            if (s.MapId == null || !Maps.All.TryGetValue(s.MapId, out var map) || y < 0 || y >= map.Rows.Count) return null;
            string row = map.Rows[y];
            return x >= 0 && x < row.Length ? row[x] : (char?)null;
        }

        /// <summary>
        /// siegeDressing.ts wearOf: each ground tile with a door or barricade standing, and its wear (by what it holds left,
        /// GameState.breakables, against what it held: a base piece's health for its tiles). One given way is the map's
        /// remains (MadeAs), not worn.
        /// </summary>
        public static Dictionary<int, int> SiegeWear(GameState s)
        {
            var @out = new Dictionary<int, int>();
            if (s.Breakables == null) return @out;
            var full = new Dictionary<int, int>();
            foreach (var p in s.Base?.Pieces ?? new List<BasePiece>())
                foreach (var c in Bastion.Engine.Base.CellsOf(p)) full[c.Y * s.Width + c.X] = Bastion.Engine.Base.Pieces[p.Piece].Hp;
            for (int i = 0; i < s.Width * s.Height && i < s.Tiles.Count; i++)
            {
                if (!s.Breakables.TryGetValue(Bastion.Engine.Grid.NodeOf(s, new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 }), out int left) || s.Tiles[i] == TileKinds.Floor) continue;
                bool door = s.Tiles[i] == TileKinds.Door || s.Tiles[i] == TileKinds.DoorOpen;
                @out[i] = WearStage(left, full.TryGetValue(i, out int hp) ? hp : door ? Content.Siege.Door : Content.Siege.Barricade);
            }
            return @out;
        }

        /// <summary>
        /// A siege's doors and barricades on the hand-made map (mapView.ts build, docs/FASE12.md §10): each barricade's
        /// sandbags as worn as it is; one given way, its bags knocked flat and spread round; a gate or door given way, its
        /// leaf down flat, fallen in; grit at the foot of a door knocked about. Not on the base's pieces (drawn whole) nor
        /// its yard. `owner`, the buildings' plan's (a gate of the base's wall is bigger than a building's door).
        /// </summary>
        public static void DrawSiege(GameState s, PieceMap map, short[] owner, List<PiecePlacement> @out)
        {
            if (s.Mission?.Siege == null) return;
            var wear = SiegeWear(s);
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                {
                    int i = y * s.Width + x;
                    if (i >= s.Tiles.Count) continue;
                    string kind = s.Tiles[i];
                    double @base = (s.Elev != null && i < s.Elev.Count ? s.Elev[i] : 0) * Storey;
                    double cx = x + 0.5, cz = y + 0.5;
                    var art = new Art(@out, null, -1, i);
                    // A door knocked about: grit and flakes at its foot, out where the dead strike it.
                    if ((kind == TileKinds.Door || kind == TileKinds.DoorOpen) && wear.TryGetValue(i, out int worn) && worn > 0)
                    {
                        var into = Inwards(s, x, y);
                        for (int k = 0; k < worn + 1; k++)
                        {
                            double R(int salt) => Hash(x, y, 100 + k * 4 + salt);
                            double t = (R(0) - 0.5) * 0.8;
                            art.Put("pebbles", At(cx - into.x * (0.35 + R(1) * 0.25) + into.z * t, @base + 0.01, cz - into.z * (0.35 + R(1) * 0.25) + into.x * t, R(2) * Math.PI * 2), false);
                        }
                        if (worn == 2) art.Put("rubble1", At(cx - into.x * 0.45, @base, cz - into.z * 0.45, Hash(x, y, 120) * Math.PI * 2, 0.4, 0.35, 0.4));
                    }
                    if (kind == TileKinds.Wall || kind == TileKinds.Door || kind == TileKinds.DoorOpen || kind == TileKinds.Window || kind == TileKinds.WindowBroken) continue;
                    if (map != null && ((map.at[i] >= 0 && Drawn.Contains(map.spots[map.at[i]].piece.Piece)) || map.yard.Has(i))) continue;
                    var made = MadeAs(s, x, y);
                    double facing = (s.Decor != null && i < s.Decor.Count ? Bastion.Engine.Decor.DecorOf(s.Decor[i])?.Facing ?? 0 : 0) != 0 ? Math.PI / 2 : 0;
                    if (kind == TileKinds.Floor && @base == 0)
                    {
                        if (made == 'b') Sandbags(s, x, y, @base, 3, facing, art);
                        else if (made == '+') FallenDoor(s, x, y, @base, owner != null && i < owner.Length && owner[i] < 0 ? 1 : 0.8, art);
                    }
                    if (kind == TileKinds.Low && made == 'b') Sandbags(s, x, y, @base, wear.TryGetValue(i, out int stage) ? stage : 0, facing, art);
                }
        }

        /// <summary>mapView.ts fallenDoor: a gate or door given way, its leaf down flat, fallen in, the foot on its line, and grit off the posts.</summary>
        static void FallenDoor(GameState s, int x, int y, double @base, double big, Art art)
        {
            double R(int salt) => Hash(x, y, 90 + salt);
            var into = Inwards(s, x, y);
            art.Put("gateLeafDown", At(x + 0.5 + into.x * 0.06, @base + 0.09, y + 0.5 + into.z * 0.06, Js.Atan2(-into.x, -into.z) + (R(0) - 0.5) * 0.5, big, big, big, (R(1) - 0.5) * 0.08, -Math.PI / 2 + 0.05));
            foreach (int side in new[] { -1, 1 })
            {
                double px = x + 0.5 + into.z * side * 0.42 + into.x * (R(2 + side) - 0.3) * 0.3;
                double pz = y + 0.5 + into.x * side * 0.42 + into.z * (R(4 + side) - 0.3) * 0.3;
                art.Put("pebbles", At(px, @base + 0.01, pz, R(6 + side) * Math.PI * 2), false);
            }
        }

        /// <summary>
        /// drawPiece: draws one piece (but its sandbags: mapView's, as a siege's barricade's) on the ground, `wear` worn
        /// (PieceWear): the caravan, what is built by hand, the tower and the lights, the traps, the turret on the base's
        /// screen, the buildings. Its props go to the end of `out`.
        /// </summary>
        public static void DrawPiece(GameState s, PieceMap map, PieceSpot spot, int wear, Func<string, PropMeasure> measure, List<PiecePlacement> @out)
        {
            var art = new Art(@out, measure, map.spots.IndexOf(spot), spot.anchor);
            var piece = spot.piece;
            int w = s.Width;
            int x = piece.X;
            int y = piece.Y;
            double cx = spot.x;
            double cz = spot.z;
            double R(int salt) => Hash(x, y, 200 + salt);
            // The pieces this one joins with, round its tile (DIRS4 order).
            var joined = new bool[4];
            for (int f = 0; f < 4; f++)
            {
                int nx = x + Dirs4[f].X;
                int ny = y + Dirs4[f].Y;
                if (nx < 0 || ny < 0 || nx >= w || ny >= s.Height) continue;
                int j = map.at[ny * w + nx];
                if (j < 0) continue;
                var other = map.spots[j];
                joined[f] = other.piece.Hp > 0 && (Joins.Contains(other.piece.Piece) || other.def.Kind == PieceKinds.Building);
            }
            char along = joined[1] || joined[3] ? 'x' : joined[0] || joined[2] ? 'z' : piece.Rot % 2 != 0 ? 'x' : 'z';
            // Pushed in by the dead: the lean goes towards the middle of the map.
            var into = Inwards(s, x, y);
            switch (piece.Piece)
            {
                case PieceIds.Caravan:
                    DrawBastion(s, spot, wear, art, R);
                    break;
                case PieceIds.Palisade:
                {
                    // A half to each neighbour it joins (a lone one, along its line), leant in as it wears; fallen flat when it gives way.
                    double sy = FenceHeight / art.Top("palisade");
                    var ways = joined.Any((b) => b) ? Enumerable.Range(0, 4).Where((k) => joined[k]).ToArray() : along == 'x' ? new[] { 1, 3 } : new[] { 0, 2 };
                    foreach (int k in ways)
                    {
                        double yaw = (TurnTo(k) * Math.PI) / 2;
                        // Its own x is across the line: lean towards the middle of the map.
                        double side = Or1(Sign(Trig.Cos(yaw) * into.x - Trig.Sin(yaw) * into.z));
                        if (wear == 3)
                        {
                            if (R(k) < 0.6) art.Put("palisade", At(cx + into.x * 0.25, 0.22, cz + into.z * 0.25, yaw, 1, 0.5, 1, -side * (Math.PI / 2 - 0.12)));
                            continue;
                        }
                        double lean = new[] { 0, 0.07, 0.17 }[wear] * (0.7 + R(k + 4) * 0.6);
                        art.Put("palisade", At(cx, 0, cz, yaw, 1, sy * new[] { 1, 0.94, 0.85 }[wear], 1, -side * lean));
                    }
                    if (wear != 0)
                    {
                        for (int k = 0; k < wear * 2 + (wear == 3 ? 2 : 0); k++)
                            art.Put("pebbles", At(cx + (R(10 + k) - 0.5) * 0.8, 0.01, cz + (R(20 + k) - 0.5) * 0.8, R(30 + k) * 6.3), false);
                    }
                    break;
                }
                case PieceIds.Wall:
                {
                    // Military's T-walls, a full one along its line (two at a corner); knocked askew, then toppled.
                    double sy = FenceHeight / art.Top("milBarrierTall");
                    var lines = (joined[1] || joined[3]) && (joined[0] || joined[2]) ? new[] { 'x', 'z' } : new[] { along };
                    foreach (char line in lines)
                    {
                        double yaw = line == 'x' ? Math.PI / 2 : 0;
                        double side = Or1(Sign(Trig.Cos(yaw) * into.x - Trig.Sin(yaw) * into.z));
                        if (wear == 3)
                        {
                            art.Put("milBarrierTall", At(cx + into.x * 0.3, 0.2, cz + into.z * 0.3, yaw, 1, sy, 1, -side * (Math.PI / 2 - 0.05)));
                            continue;
                        }
                        art.Put("milBarrierTall", At(cx, 0, cz, yaw + (R(1) - 0.5) * 0.05 * wear, 1, sy, 1, -side * new[] { 0, 0.035, 0.09 }[wear]));
                    }
                    if (wear != 0)
                    {
                        double k = 0.35 + wear * 0.1;
                        art.Put(R(2) < 0.5 ? "rubble1" : "rubble2", At(cx - into.x * 0.4, 0, cz - into.z * 0.4, R(3) * 6.3, k, 0.3, k), wear == 3);
                    }
                    break;
                }
                case PieceIds.Gate:
                    DrawGate(s, map, spot, wear, art, R, along, into);
                    break;
                case PieceIds.Logs:
                {
                    double sy = new[] { 1, 0.85, 0.65 }[Math.Min(wear, 2)];
                    if (wear == 3)
                    {
                        for (int k = 0; k < 3; k++) art.Put("log", At(cx + (R(k) - 0.5) * 0.7, 0.02, cz + (R(k + 3) - 0.5) * 0.7, R(k + 6) * 6.3, 0.5, 0.5, 0.5));
                        break;
                    }
                    art.Put("woodStack", At(cx, 0, cz, spot.yaw + (R(0) - 0.5) * 0.1, 1, sy, 1));
                    break;
                }
                case PieceIds.Tower:
                {
                    // Military's guard tower without its hut and stairs (the siege's own), its deck the 2 × 2 a storey up; its
                    // open side to the ladder in front, leaning on the deck's edge.
                    var deck = spot.cells.Where((c) => (c.Elev ?? 0) != 0).ToList();
                    var ladder = spot.cells.FirstOrDefault((c) => c.Tile == TileKinds.Ladder);
                    double dx = deck.Aggregate(0.0, (a, c) => a + c.X + 0.5) / deck.Count;
                    double dz = deck.Aggregate(0.0, (a, c) => a + c.Y + 0.5) / deck.Count;
                    double fx = 0, fz = 1;
                    if (ladder != null)
                    {
                        fx = Sign(ladder.X + 0.5 - dx) * (Math.Abs(ladder.X + 0.5 - dx) > 0.75 ? 1 : 0);
                        fz = Sign(ladder.Y + 0.5 - dz) * (Math.Abs(ladder.Y + 0.5 - dz) > 0.75 ? 1 : 0);
                    }
                    double yaw = Js.Atan2(-fx, -fz);
                    if (wear == 3)
                    {
                        art.Put("rubble2", At(dx, 0, dz, R(0) * 6.3, 0.8, 0.5, 0.8));
                        break;
                    }
                    art.Put("guardTower", At(dx, 0, dz, yaw, 2.0 / 3, 1, 2.0 / 3, new[] { 0, 0.01, 0.025 }[wear]));
                    if (ladder != null)
                    {
                        // Its rungs across the way up (the model's x is the ladder's face), its top leant onto the deck's edge.
                        double face = Js.Atan2(fz, -fx);
                        art.Put("towerLadder", At(ladder.X + 0.5 - fx * TowerLadderFoot, 0, ladder.Y + 0.5 - fz * TowerLadderFoot, face, 1, TowerLadderTop / art.Top("towerLadder"), 1, -Js.Atan(TowerLadderLean)));
                    }
                    break;
                }
                case PieceIds.Floodlight:
                {
                    double tilt = new[] { 0, 0.05, 0.12 }[Math.Min(wear, 2)];
                    if (wear == 3)
                    {
                        art.Put("floodlight", At(cx, 0.12, cz, spot.yaw, 0.7, 0.7, 0.7, Math.PI / 2 - 0.06));
                        break;
                    }
                    art.Put("floodlight", At(cx, 0, cz, spot.yaw, 0.7, 0.7, 0.7, tilt * (R(0) < 0.5 ? -1 : 1)));
                    break;
                }
                case PieceIds.Wire:
                    art.Put("wireCoil", At(cx, 0, cz, spot.yaw + (R(0) - 0.5) * 0.4, 1, new[] { 1, 0.85, 0.65, 0.3 }[wear], 1));
                    break;
                case PieceIds.Turret:
                {
                    // In a siege it is a unit (unitViews' TurretRig); on the base's screen, the same gun standing, turned as built.
                    if (s.Units.Any((u) => u.Automaton != null && u.Automaton.Piece == piece.Id)) break;
                    if (wear == 3)
                    {
                        art.Put("turretTripod", At(cx, 0.08, cz, spot.yaw, 0.78, 0.78, 0.78, 1.25));
                        break;
                    }
                    // Its parts on one another as TurretRig puts them (syntyFigures.ts), its barrels (the model's +x) the way it was built facing.
                    const double k = 0.78;
                    double yaw = spot.yaw;
                    art.Put("turretTripod", At(cx, 0, cz, yaw, k, k, k));
                    art.Put("turretSwivel", At(cx, (1.0 / 1.5) * k, cz, yaw, k, k, k));
                    art.Put("turretGun", At(cx, (1.32 / 1.5) * k, cz, yaw, k, k, k));
                    art.Put("turretDrum", At(cx + Trig.Cos(yaw) * (0.1 / 1.5) * k, (1.62 / 1.5) * k, cz - Trig.Sin(yaw) * (0.1 / 1.5) * k, yaw, k, k, k));
                    break;
                }
                case PieceIds.Mine:
                case PieceIds.Spikes:
                case PieceIds.Snare:
                {
                    // Set, or spent (its uses gone, in the siege's GameState.traps or on the base's own piece).
                    bool spent = ChargesOf(s, piece) <= 0 || wear == 3;
                    if (piece.Piece == PieceIds.Mine)
                    {
                        if (spent) art.Put("crater", At(cx, -0.02, cz, R(0) * 6.3, 0.22, 0.5, 0.22), false);
                        else art.Put("mine", At(cx, -0.03, cz, R(0) * 6.3, 1.6, 1.6, 1.6), false);
                    }
                    else if (piece.Piece == PieceIds.Spikes)
                    {
                        // The pit, its stakes round it pointing up and out; broken and splashed once someone has fallen in.
                        art.Put("crater", At(cx, -0.06, cz, R(0) * 6.3, 0.2, 0.3, 0.2), false);
                        for (int k = 0; k < 6; k++)
                        {
                            double a = (k / 6.0) * Math.PI * 2 + R(1) * 1.2;
                            double d = 0.12 + R(10 + k) * 0.16;
                            double h = spent ? 0.18 + R(20 + k) * 0.12 : 0.32 + R(20 + k) * 0.12;
                            // Turned so its own z points out of the pit: tipped about its x, its top goes out.
                            art.Put("spikeStake", At(cx + Trig.Cos(a) * d, -0.05, cz + Trig.Sin(a) * d, Math.PI / 2 - a, 1.4, h, 1.4, 0, (spent ? 0.6 : 0.3) + R(30 + k) * 0.25));
                        }
                    }
                    else if (spent)
                    {
                        // Sprung: the two jaws shut on their hinge.
                        art.Put("bearJaw", At(cx, 0.02, cz, spot.yaw, 1, 1, 1, 1 * 1.35));
                        art.Put("bearJaw2", At(cx, 0.02, cz, spot.yaw, 1, 1, 1, -1 * 1.35));
                    }
                    else art.Put("bearTrap", At(cx, 0, cz, spot.yaw + (R(0) - 0.5) * 0.6), false);
                    break;
                }
                default:
                {
                    if (!Buildings.TryGetValue(piece.Piece, out var build)) break;
                    var lot = new Lot(art, spot.x, spot.z, spot.yaw, spot.def.Size[0], spot.def.Size[1]);
                    if (wear == 3) Ruin(lot, R);
                    else
                    {
                        build(lot, piece.Level ?? 1);
                        if (wear != 0) Debris(lot, wear, R);
                    }
                    break;
                }
            }
        }

        /// <summary>The gate (drawPiece's): planks by a palisade, the quarantine's sheet metal by a wall; swung open on its hinge, ajar as it wears, down once it gives way.</summary>
        static void DrawGate(GameState s, PieceMap map, PieceSpot spot, int wear, Art art, Func<int, double> r, char along, (double x, double z) into)
        {
            var piece = spot.piece;
            int w = s.Width;
            int x = piece.X;
            int y = piece.Y;
            double cx = spot.x;
            double cz = spot.z;
            bool metal = Dirs4.Any((d) =>
            {
                // (Not kept to the map: what is off it reads as no piece, past either end of the array.)
                int i = (y + d.Y) * w + x + d.X;
                int k = i >= 0 && i < map.at.Length ? map.at[i] : -1;
                return k >= 0 && map.spots[k].piece.Piece == PieceIds.Wall;
            });
            string id = metal ? "gateMetal" : "gateWood";
            double sy = FenceHeight / art.Top(id);
            double runX = along == 'x' ? 1 : 0, runZ = along == 'x' ? 0 : 1;
            // Across its line, into the base.
            double nX = along == 'x' ? 0 : Or1(Sign(into.x)), nZ = along == 'x' ? Or1(Sign(into.z)) : 0;
            if (wear == 3)
            {
                art.Put(id, At(cx + nX * 0.55, 0.05, cz + nZ * 0.55, Js.Atan2(runX, runZ) + (r(0) - 0.5) * 0.4, 1, sy, 1, Math.PI / 2));
                return;
            }
            // Hung on the end of its tile against what isn't another gate (19/10: in a row, never on the post between two
            // gates): a lone one or the row's first on its −run end, the row's last on its +run end; one between two slides,
            // shut in its place, open run along outside the next (which swings in).
            bool GateAt(int k)
            {
                int nx = x + (int)runX * k;
                int ny = y + (int)runZ * k;
                int j = nx < 0 || ny < 0 || nx >= w || ny >= s.Height ? -1 : map.at[ny * w + nx];
                return j >= 0 && map.spots[j].piece.Piece == PieceIds.Gate;
            }
            bool open = s.Tiles[y * w + x] == TileKinds.DoorOpen;
            double roll = new[] { 0, 0.04, 0.09 }[wear];
            if (GateAt(-1) && GateAt(1))
            {
                double slide = open ? 0.9 : 0;
                art.Put(id, At(cx + runX * slide - nX * slide * 0.16, 0, cz + runZ * slide - nZ * slide * 0.16, Js.Atan2(runX, runZ), 1, sy, 1, roll));
                return;
            }
            double end = GateAt(-1) ? 1 : -1;
            // From the hinge at that end of the tile, the leaf swung in: open, or ajar as it wears.
            double swing = open ? Math.PI / 2 - 0.15 : new[] { 0, 0.1, 0.28 }[wear];
            double vx = -end * runX * Trig.Cos(swing) + nX * Trig.Sin(swing);
            double vz = -end * runZ * Trig.Cos(swing) + nZ * Trig.Sin(swing);
            double hx = cx + end * runX * 0.5;
            double hz = cz + end * runZ * 0.5;
            art.Put(id, At(hx + vx * 0.5, 0, hz + vz * 0.5, Js.Atan2(vx, vz), 1, sy, 1, roll));
        }

        /// <summary>The camp stool's size: its seat a quarter of a tile up, where the hips of those sat on it come down to with their feet on the ground (clearingLife.ts).</summary>
        const double Stool = 0.7;

        /// <summary>The caravan's awning's scale and the barracón's water tower's (DrawBastion).</summary>
        const double Awning = 1.25;
        const double WaterTower = 0.66;

        /// <summary>
        /// The Bastion on its lot (playtest 18/10, #40; Base.BastionLot): the building of its stage (two levels each) on its
        /// footprint, its front to the lot's yard (south), the second level of a stage with more on it; worn as the siege
        /// leaves it. The caravan; the mobile home on its skirt (its porch, its dish); Military's plywood barracón on stilts
        /// (solar panels, sandbags, a floodlight); the house with its porch and chimney (sandbags on the porch, a mast).
        /// Round it, its yard: a fire with chairs by it, wood.
        /// </summary>
        static void DrawBastion(GameState s, PieceSpot spot, int wear, Art art, Func<int, double> r)
        {
            var @base = AsBase(s);
            int level = s.Base?.Level ?? 1;
            int stage = Base.BastionStage(level);
            bool more = level % 2 == 0;
            var plot = Base.BastionLot(@base);
            int w = Base.BastionStages[stage][0];
            int h = Base.BastionStages[stage][1];
            int lotW = plot?.W ?? w;
            int lotH = plot?.H ?? h;
            // The footprint's frame (u across, v deep, its front +v) and the lot's.
            var here = new Lot(art, spot.x, spot.z, stage != 0 ? 0 : spot.yaw, w, h);
            var yard = new Lot(art, plot != null ? plot.X + plot.W / 2.0 : spot.x, plot != null ? plot.Y + plot.H / 2.0 : spot.z, 0, lotW, lotH);
            double Lean(int k) => (r(k) < 0.5 ? -1 : 1) * new[] { 0, 0.012, 0.03, 0.06 }[wear];
            double sunk = wear == 3 ? -0.15 : 0;
            // The middle of tile (x, y) of the lot (x across, y deep, its front the last row), in the yard's frame.
            (double u, double v) T(double tx, double ty) => (tx + 0.5 - lotW / 2.0, ty + 0.5 - lotH / 2.0);
            switch (stage)
            {
                case 0:
                {
                    double tilt = new[] { 0, 0.012, 0.03, 0 }[wear];
                    art.Put("caravan", At(spot.x, wear == 3 ? -0.18 : 0, spot.z, spot.yaw, 1, 1, 1, tilt * (r(0) < 0.5 ? -1 : 1), wear == 3 ? 0.06 : 0));
                    if (more && wear < 3)
                    {
                        // Settled in: its steps, an aerial on the roof and the generator; by its end, its awning (playtest 20/10
                        // #13: «Caravana con toldo»), a tarp on poles over tiles 0–2 × 5–7 of the lot, a table and two chairs
                        // under it on 0–1 × 6–7 (base.ts BASTION_YARD), the generator on 7, 3.
                        here.Put("trailerSteps", 0.6, h / 2.0 + 0.25, 0, 0.8);
                        here.Put("bAerial", -1.1, 0, 0, 0.45, here.Top("caravan") - 0.05);
                        var awning = T(1, 6);
                        foreach (string id in new[] { "bAwning", "bAwningTarp" }) yard.Put(id, awning.u, awning.v, 1, Awning);
                        var table = T(0.5, 6);
                        yard.Fill("bLongTable", table.u, table.v, 1.8, 0.8);
                        foreach (int cx in new[] { 0, 1 })
                        {
                            var chair = T(cx, 7);
                            yard.Put("bCampChair", chair.u, chair.v - 0.1, 2 + (r(50 + cx) - 0.5) * 0.3, Stool);
                        }
                        var gen = T(7, 3);
                        yard.Put("bPowerGenerator", gen.u, gen.v, 1, 0.75);
                    }
                    break;
                }
                case 1:
                {
                    // On its skirt, the wheels hidden: it isn't going anywhere. Worn, its broken twin.
                    const double k = 0.94;
                    string body = wear >= 2 ? "mobileHomeWreck" : "mobileHome";
                    foreach (string id in new[] { body, wear >= 2 ? "mobileHomeWreckGlass" : "mobileHomeGlass", "mobileHomeSkirt" })
                    {
                        if (id == "mobileHomeSkirt" && wear == 3) continue;
                        var m = here.Matrix(-0.03 * k, -0.12 * k, 0, k, sunk);
                        if (wear != 0) m.Multiply(Mat.RotationZ(Lean(1)));
                        art.Put(id, m);
                    }
                    if (more && wear < 3)
                    {
                        here.Put("trailerDeck", 0.9, 1.36 * k + 0.56 * k, 0, k);
                        here.Put("bDish", 2.2, -0.5, 1, 0.7, 2.61 * k);
                        here.Put("trailerSteps", -1.9, 1.4 * k + 0.2, 0, 0.8);
                    }
                    break;
                }
                case 2:
                {
                    // Plywood on stilts, its steps to the front; a ruin once it gives way.
                    const double k = 0.93;
                    var m = here.Matrix(0, 1.475 * k, 0, k, sunk);
                    if (wear != 0 && wear < 3) m.Multiply(Mat.RotationZ(Lean(2)));
                    art.Put(wear == 3 ? "plywoodShedRuin" : "plywoodShed", m);
                    if (more && wear < 3)
                    {
                        // Grown (playtest 20/10 #13: «Albergue ampliado»): sandbags either side of its steps (3 and 5, 7); up the
                        // lot's free column its water tower (8, 0–1), taller than it, a floodlight on top, a solar panel and the
                        // generator (8, 2–3).
                        foreach (var (tx, ty, id, kk) in new[] { (8, 2, "bSolarPanel", 0.6), (8, 3, "bPowerGenerator", 0.7) })
                        {
                            var t = T(tx, ty);
                            yard.Put(id, t.u, t.v, 1, kk);
                        }
                        foreach (int tx in new[] { 3, 5 })
                        {
                            var t = T(tx, 7);
                            yard.Put("sandbags", t.u, t.v, 0, (0.95, 1, 1));
                        }
                        var tower = T(8, 0.5);
                        yard.Put("bWaterTower", tower.u, tower.v, 1, WaterTower);
                        yard.Put("bFloodlights", tower.u, tower.v + 0.3, 2.5, 0.4, yard.Top("bWaterTower") * WaterTower);
                    }
                    break;
                }
                default:
                {
                    // The house, its porch to the yard; given way, a heap of what it was.
                    const double k = 0.977;
                    if (wear == 3)
                    {
                        here.Put("ruinPile", 0, 0, 0, 0.82);
                        break;
                    }
                    var house = yard.Matrix(0.088 * k, -3 + 2.12 * k, -1, k, sunk);
                    if (wear != 0) house.Multiply(Mat.RotationX(Lean(3) * 0.5));
                    foreach (string id in new[] { "bastionHouse", "bastionHouseGlass" }) art.Put(id, house);
                    if (more)
                    {
                        // Fortified (playtest 20/10 #13: «Casa fortificada»): every window shut, iron sheets and planks over it; a
                        // wall of sandbags along the porch, its steps left open, and another across the lot's front row either
                        // side of the way in (1–2 and 6–7, 8); tank traps in the back corners (0 and 8, 0); the mast; a floodlight
                        // in the front corner (8, 8).
                        for (int i = 0; i < HouseWindows.Length; i++) Armour(art, house, HouseWindows[i], i, r);
                        foreach (double u in new[] { -2.4, -1.35, 1.35, 2.4 }) yard.Put("sandbags", u, 2.75, 0, (1.05, 0.9, 1));
                        foreach (int tx in new[] { 1, 2, 6, 7 })
                        {
                            var bags = T(tx, 8);
                            yard.Put("sandbags", bags.u, bags.v, 0, (1.02, 0.95, 1));
                        }
                        here.Put("bMast", 1.6, -0.8, 0, 0.6, 3.6);
                        foreach (int tx in new[] { 0, 8 })
                        {
                            var trap = T(tx, 0);
                            yard.Put("bTankTrap", trap.u, trap.v, r(70 + tx) * 4, 1.1);
                        }
                        var light = T(8, 8);
                        yard.Put("bFloodlights", light.u, light.v, 0.5, 0.6);
                    }
                    break;
                }
            }
            if (wear == 1 || wear == 2) Debris(here, wear, r);
            if (wear == 3 && stage != 0) Ruin(here, r);
            // The yard, by tile of the lot (base.ts BASTION_YARD): a fire with a chair either side of it, turned to it, where
            // the building leaves room (before the caravan, along the mobile home's front, in the barracón's front corner);
            // the wood in the front corner opposite.
            var camps = new[] { new[] { 5, 7, 4, 6 }, new[] { 5, 8, 4, 6 }, new[] { 7, 8, 6, 8 } };
            if (stage < camps.Length)
            {
                var camp = camps[stage];
                var fire = T(camp[0], camp[1]);
                yard.Put("bFirePit", fire.u, fire.v, 0, 0.75);
                foreach (int tx in new[] { camp[2], camp[3] })
                {
                    var chair = T(tx, camp[1]);
                    // (Its front is its own +z: a quarter turn per π/2 from it.)
                    yard.Put("bCampChair", chair.u, chair.v, Js.Atan2(fire.u - chair.u, fire.v - chair.v) / (Math.PI / 2) + (r(40 + tx) - 0.5) * 0.3, Stool);
                }
            }
            var wood = T(0, 8);
            yard.Put("bWoodPile", wood.u, wood.v, 1, 0.8);
        }

        /// <summary>
        /// A window of the house (SM_Bld_House_03, its own frame: bastionHouseGlass's panes, and those broken out of it): the
        /// middle of each, which way it looks (x or z, ±1).
        /// </summary>
        readonly struct HouseWindow
        {
            public readonly double x, y, z;
            public readonly char axis;
            public readonly int @out;

            public HouseWindow(double x, double y, double z, char axis, int @out)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.axis = axis;
                this.@out = @out;
            }
        }

        static readonly HouseWindow[] HouseWindows =
        {
            new HouseWindow(-1.71, 1.45, 1.66, 'x', -1),
            new HouseWindow(-1.71, 1.45, -1.67, 'x', -1),
            new HouseWindow(-1.71, 1.45, 0, 'x', -1),
            new HouseWindow(1.72, 1.45, -1.67, 'x', 1),
            new HouseWindow(0.83, 1.45, 2.55, 'z', 1),
            new HouseWindow(-0.83, 1.45, 2.55, 'z', 1),
            new HouseWindow(-0.83, 1.45, -2.55, 'z', -1),
            new HouseWindow(0.84, 1.45, -2.55, 'z', -1),
        };
        /// <summary>How wide and how tall the house's windows are (m).</summary>
        const double WindowWide = 0.62;
        const double WindowTall = 1;

        /// <summary>
        /// A window of the house shut against the dead, on `house` (its matrix): every other one an iron sheet over it, the
        /// rest three planks across it, each a little askew.
        /// </summary>
        static void Armour(Art art, Mat house, HouseWindow w, int i, Func<int, double> r)
        {
            var @out = w.axis == 'x' ? new V3(w.@out, 0, 0) : new V3(0, 0, w.@out);
            var up = new V3(0, 1, 0);
            var along = V3.Cross(up, @out);
            var at = new V3(w.x, w.y, w.z).AddScaled(@out, 0.12);
            if (i % 2 == 0)
            {
                // The sheet lies flat as it comes, its length across x: stood up, its face out.
                var size = art.Size("bIronSheet");
                var m = Mat.Basis(up, @out, V3.Cross(up, @out)).Scale((WindowTall + 0.15) / size.x, 1, (WindowWide + 0.12) / size.z);
                art.Put("bIronSheet", house.Clone().Multiply(Mat.Translation(at.x, at.y, at.z)).Multiply(m));
                return;
            }
            var plank = art.Size("bPlank");
            var rows = new[] { -0.32, 0, 0.32 };
            for (int n = 0; n < rows.Length; n++)
            {
                double tilt = (r(60 + i * 3 + n) - 0.5) * 0.5;
                var axis = along.ApplyAxisAngle(@out, tilt);
                var m = Mat.Basis(axis, @out, V3.Cross(axis, @out)).Scale((WindowWide + 0.25) / plank.x, 1, 1);
                var p = at.AddScaled(up, rows[n] * WindowTall).AddScaled(@out, n * 0.012);
                art.Put("bPlank", house.Clone().Multiply(Mat.Translation(p.x, p.y, p.z)).Multiply(m));
            }
        }

        /// <summary>
        /// A building's ground in its own frame, as it was built (before its turn): u across its width and v into its depth,
        /// in tiles from its middle; its front (+v) is south when it isn't turned.
        /// </summary>
        sealed class Lot
        {
            readonly Art art;
            readonly double x, z, yaw;
            public readonly double w, h;
            /// <summary>The design's scale (Scaled): a layout drawn for one size laid on a lot of another (a piece's size of before).</summary>
            double su = 1;
            double sv = 1;

            public Lot(Art art, double x, double z, double yaw, double w, double h)
            {
                this.art = art;
                this.x = x;
                this.z = z;
                this.yaw = yaw;
                this.w = w;
                this.h = h;
            }

            /// <summary>
            /// Laid out for a lot `dw` × `dh` (a piece's footprint, docs/MAPAS.md §4.9): on another (a piece kept at its size of
            /// before, BasePiece.size), where things go and how big what fills a space is, in proportion; and the models no
            /// bigger than they are.
            /// </summary>
            public Lot Scaled(double dw, double dh)
            {
                su = w / dw;
                sv = h / dh;
                return this;
            }

            /// <summary>How much smaller than designed the models go (Scaled).</summary>
            double Shrink => Math.Min(1, Math.Min(su, sv));

            (double x, double z) World(double u, double v)
            {
                double c = Trig.Cos(yaw);
                double s = Trig.Sin(yaw);
                u *= su;
                v *= sv;
                return (x + u * c + v * s, z - u * s + v * c);
            }

            /// <summary>`id` at (u, v), turned `turn` quarter turns (counter-clockwise from above), scaled `k`, `y` up.</summary>
            public void Put(string id, double u, double v, double turn = 0, double k = 1, double y = 0, bool shadows = true) =>
                art.Put(id, Matrix(u, v, turn, k, k, k, y), shadows);

            /// <summary>Put, scaled per axis.</summary>
            public void Put(string id, double u, double v, double turn, (double x, double y, double z) k, double y = 0, bool shadows = true) =>
                art.Put(id, Matrix(u, v, turn, k.x, k.y, k.z, y), shadows);

            /// <summary>The matrix Put puts a model at.</summary>
            public Mat Matrix(double u, double v, double turn = 0, double k = 1, double y = 0) => Matrix(u, v, turn, k, k, k, y);

            Mat Matrix(double u, double v, double turn, double kx, double ky, double kz, double y)
            {
                var p = World(u, v);
                // (Its height up the same: what stands on another model goes as high as that one's top is.)
                double s = Shrink;
                return At(p.x, y * s, p.z, yaw + (turn * Math.PI) / 2, kx * s, ky * s, kz * s);
            }

            /// <summary>
            /// `id` as big as it fits in a `fw` × `fd` space round (u, v), turned `turn`; but never lower than `tall` tiles (a
            /// shelter one walks into stands over a soldier's head, 1.34: overhanging its lot, until its piece grows, MAPAS.md
            /// §4.9); its scale.
            /// </summary>
            public double Fill(string id, double u, double v, double fw, double fd, double turn = 0, double y = 0, double tall = 0)
            {
                var size = art.Size(id);
                // (The space in the lot's own tiles; Put shrinks the model again by as much as the lot is smaller. The scale
                // it returns is the design's, as Put takes it.)
                double sw = (fw * su) / Shrink, sd = (fd * sv) / Shrink;
                bool turned = turn % 2 != 0;
                double k = Math.Max(Math.Min(sw / (turned ? size.z : size.x), sd / (turned ? size.x : size.z)), tall / size.y / Shrink);
                Put(id, u, v, turn, k, y);
                return k;
            }

            public double Top(string id) => art.Top(id);

            public PropMeasure Size(string id) => art.Size(id);
        }

        /// <summary>How tall a shelter one walks into stands at least (tiles: 2.6 m drawn, over a soldier's 2.01), and a lamp post's scale (4.4 m).</summary>
        const double Shelter = 1.75;
        const double Lamp = 0.9;

        /// <summary>
        /// The buildings (docs/FASE12.md §11.3), the base's installations of before in the space they take on the map: what
        /// they are at level 1, with more round them, or a bigger one, at levels 2 and 3. The same models as the old base's
        /// screen (strategy/bastion.ts FACILITY_ART). In a fight they are solid blocks: what is drawn fills their ground.
        /// </summary>
        static readonly Dictionary<string, Action<Lot, int>> Buildings = new Dictionary<string, Action<Lot, int>>
        {
            // Sala de radio (4 × 4): a shelter of scrap under a tarp with the set on a table, an aerial and a generator; a mast,
            // then a dish.
            [PieceIds.Radio] = (lot, level) =>
            {
                lot.Scaled(4, 4);
                double k = lot.Fill("bShackFrame", -0.45, -0.45, 2.9, 2.9, 0, 0, Shelter);
                lot.Put("bShackTarp", -0.45, -0.45, 0, k, 0.8 * k);
                double t = lot.Fill("bLongTable", -0.45, -0.4, 1.6, 0.8);
                lot.Put("bRadioSet", -0.45, -0.45, 1, 1, lot.Top("bLongTable") * t);
                lot.Put("bPowerGenerator", 1.35, 1.35, 1, 0.85);
                lot.Put(level >= 2 ? "bMast" : "bAerial", 1.45, -1.45, 0, level >= 2 ? 0.7 : 0.75);
                if (level >= 2) lot.Put("bSolarPanel", -0.9, 1.5, 1, 0.75);
                if (level >= 3)
                {
                    lot.Put("bDish", 1.5, 0.2, 1, 0.75);
                    lot.Put("bPowerBoxes", -1.65, 1.5, 0, 0.7);
                }
            },
            // Barracón (6 × 5): a field tent, its fire and a chair by it, the wood; an army tent and the field tent beside it;
            // washing out and a second chair.
            [PieceIds.Quarters] = (lot, level) =>
            {
                lot.Scaled(6, 5);
                if (level < 2)
                {
                    lot.Fill("bFieldTent", -1.0, -0.6, 3.6, 2.5);
                    lot.Put("bFirePit", 1.5, 1.3, 0, 0.8);
                    lot.Put("bCampChair", 0.7, 1.75, 1.3, Stool);
                    lot.Put("bWoodPile", 2.2, -1.5, 1, 0.9);
                    return;
                }
                lot.Fill("bArmyTent", -1.0, -0.4, 3.8, 3.6);
                lot.Fill("bFieldTent", 2.0, -0.6, 1.6, 3.0, 1);
                lot.Put("bFirePit", 1.6, 1.75, 0, 0.6);
                lot.Put("bWoodPile", -2.4, 1.85, 1, 0.8);
                if (level >= 3)
                {
                    lot.Put("bWashingLine", -0.9, 1.9, 0, 0.7);
                    lot.Put("bCampChair", 0.85, 1.9, 2.4, Stool);
                }
            },
            // Enfermería (6 × 5): a field tent with a first-aid box and a drip; the quarantine tent; a decontamination shower
            // and a gurney.
            [PieceIds.Infirmary] = (lot, level) =>
            {
                lot.Scaled(6, 5);
                if (level < 2)
                {
                    lot.Fill("bFieldTent", -0.9, -0.6, 3.6, 2.5);
                    lot.Put("bMedKit", 1.6, 1.3, 0, 0.8);
                    lot.Put("ivStand", 2.1, 0.3, 0, 0.85);
                    lot.Put("bJerrycan", 2.3, -1.5, 0.3, 1);
                    lot.Put("gurney", -0.9, 1.6, 1, 0.75);
                    return;
                }
                lot.Fill("bQuarantineTent", 0, -0.4, 5.2, 3.4);
                lot.Put("bMedKit", -2.2, 1.8, 0, 0.8);
                lot.Put("ivStand", 2.4, 1.6, 0, 0.85);
                if (level >= 3)
                {
                    lot.Put("bDeconShower", 1.4, 1.8, 0, 0.55);
                    lot.Put("gurney", -0.8, 1.9, 1, 0.75);
                }
            },
            // Taller (5 × 4): a lean-to over the bench, the tools and the tyres; a generator and the battery bench; floodlights.
            [PieceIds.Workshop] = (lot, level) =>
            {
                lot.Scaled(5, 4);
                lot.Fill("bLeanTo", 0, -0.4, 4.6, 3.0);
                lot.Put("bWorkbench", -1.2, -0.6, 0, 0.85);
                lot.Put("bToolbox", -0.3, 0.1, 0.3, 1);
                lot.Put("bTyres", 1.9, 1.4, 0, 0.9);
                if (level >= 2)
                {
                    lot.Put("bPowerGenerator", 1.6, -0.6, 1, 0.8);
                    lot.Put("bBatteryTable", 0.3, 1.5, 0, 0.8);
                }
                if (level >= 3)
                {
                    lot.Put("bFloodlights", -2.0, 1.5, 0.4, 0.45);
                    lot.Put("bJerrycan", 1.1, 1.6, 1.2, 1);
                }
            },
            // Huerto (7 × 6, low): four raised beds and a rain barrel; greens in them; a scarecrow and more barrels.
            [PieceIds.Garden] = (lot, level) =>
            {
                lot.Scaled(7, 6);
                foreach (double u in new[] { -2.55, -0.85, 0.85, 2.55 })
                {
                    double k = lot.Fill("bGardenBed", u, -0.5, 1.3, 4.2);
                    if (level >= 2)
                    {
                        for (int i = 0; i < 5; i++)
                            lot.Put(i % 2 != 0 ? "bGardenPlant2" : "bGardenPlant", u + (i % 2 != 0 ? 0.18 : -0.18), -2.1 + i * 0.8, i, 0.55, lot.Top("bGardenBed") * k);
                    }
                }
                lot.Put("bGardenBarrel", 3.0, 2.4, 0, 1.1);
                if (level >= 3)
                {
                    lot.Put("bScarecrow", -3.0, 2.3, 0.5, 0.6);
                    lot.Put("bGardenBarrel", 2.35, 2.5, 1, 1.1);
                }
            },
            // Cuarentena (4 × 4): a cage of mesh panels (two a side), its door on the front; a guard's chair and cans; then a lamp.
            [PieceIds.Quarantine] = (lot, level) =>
            {
                lot.Scaled(4, 4);
                double sx = 1.85 / lot.Size("cagePanel").x;
                foreach (double u in new[] { -0.92, 0.92 })
                {
                    lot.Put("cagePanel", u, -1.8, 0, (sx, 1, 1));
                    lot.Put("cagePanel", u, 1.8, 0, (sx, 1, 1));
                    lot.Put("cagePanel", -1.8, u, 1, (sx, 1, 1));
                    lot.Put("cagePanel", 1.8, u, 1, (sx, 1, 1));
                }
                lot.Put("bCampChair", 0.4, 0.3, 0.6, Stool);
                if (level >= 2)
                {
                    lot.Put("bJerrycan", -1.2, 1.2, 0.4, 1);
                    lot.Put("bJerrycan", -0.9, 1.3, 1.4, 1);
                }
                if (level >= 3) lot.Put("bLampPost", 1.6, 1.55, 2, Lamp);
            },
            // Camposanto (4 × 4, low): rows of crosses and grave sticks; a bench and a lamp; stone crosses at the head.
            [PieceIds.Memorial] = (lot, level) =>
            {
                lot.Scaled(4, 4);
                int rows = level >= 2 ? 3 : 2;
                for (int row = 0; row < rows; row++)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        bool stone = level >= 3 && row == 0;
                        bool cross = (i + row) % 3 != 0;
                        lot.Put(stone ? "bStoneCross" : cross ? "bCross" : "bGraveStick", -1.35 + i * 0.9, -1.4 + row * 1.0, 0, stone ? 1.8 : cross ? 2.1 : 1.1);
                    }
                }
                if (level >= 2)
                {
                    lot.Put("bBench", 0.8, 1.65, 2, 0.8);
                    lot.Put("bLampPost", -1.6, 1.6, 0, Lamp);
                }
            },
            // Campo de tiro (10 × 3, low): targets along the back, a firing line of sandbags; bottles on drums; floodlights.
            [PieceIds.Range] = (lot, level) =>
            {
                lot.Scaled(10, 3);
                int n = level + 2;
                for (int i = 0; i < n; i++) lot.Put(i % 2 != 0 ? "bTargetBoard" : "bTarget", -3.8 + (7.6 * i) / (n - 1), -0.9, 0, 0.9);
                foreach (double u in new[] { -3.6, -1.2, 1.2, 3.6 }) lot.Put("bSandbagWall", u, 0.85, 1, 0.75);
                if (level >= 2) lot.Put("bBottleRange", 4.5, -0.3, 1, 0.65);
                if (level >= 3) lot.Put("bFloodlights", -4.6, 0.3, 0.5, 0.45);
            },
            // Centro de entrenamiento (5 × 5): a tyre wall, a beam and sandbags; a climbing frame; dummies (targets) to strike.
            [PieceIds.Training] = (lot, level) =>
            {
                lot.Scaled(5, 5);
                lot.Fill("bTyreWall", 0, -1.9, 4.6, 0.7);
                lot.Put("bBalanceBeam", -1.0, 0.1, 0, 1.0);
                foreach (double v in new[] { -0.6, 0.8 }) lot.Put("bSandbagWall", 1.8, v, 0, 0.7);
                if (level >= 2) lot.Put("bClimbFrame", -1.5, 1.5, 0, 0.75);
                if (level >= 3) lot.Put("bTarget", 0.7, 1.6, 2, 0.8);
            },
            // Sala de mapas (6 × 6): an open army tent with a long table and the blackboard; another table; the radio and a
            // generator.
            [PieceIds.Simulation] = (lot, level) =>
            {
                lot.Scaled(6, 6);
                lot.Fill("bOpenTent", 0, -0.3, 5.0, 5.0, 0, 0, Shelter);
                lot.Put("bLongTable", -0.8, -0.7, 1, 0.8);
                lot.Put("bBlackboard", 1.3, -1.7, 0.3, 0.85);
                if (level >= 2) lot.Put("bLongTable", 0.9, 0.9, 1, 0.8);
                if (level >= 3)
                {
                    lot.Put("bRadioSet", -0.8, -0.7, 0, 1, 0.47 * 0.8);
                    lot.Put("bPowerGenerator", 2.4, 2.4, 0.3, 0.65);
                }
            },
            // Chapistería (7 × 5): the garage shed turned along its length, tyres and drums; cans and the battery bench; a fuel pump.
            [PieceIds.Garage] = (lot, level) =>
            {
                lot.Scaled(7, 5);
                lot.Fill("bGarageShed", -1.0, -0.2, 4.6, 4.3, 1);
                lot.Put("bTyres", 2.5, 1.6, 1, 0.9);
                lot.Put("bOilDrum", 2.7, -1.6, 0, 1);
                lot.Put("bOilDrum", 2.35, -1.8, 0, 1);
                if (level >= 2)
                {
                    lot.Put("bJerrycan", 2.6, -0.7, 0.6, 1);
                    lot.Put("bBatteryTable", 2.4, 0.4, 1, 0.8);
                }
                if (level >= 3) lot.Put("bFuelPump", 1.6, 1.9, 0, 0.8);
            },
            // Taller de oficios (4 × 4): a craft shed, the bench, a plank on sawhorses and wood stacked; a trestle; the timber
            // rack and an anvil.
            [PieceIds.Echoes] = (lot, level) =>
            {
                lot.Scaled(4, 4);
                lot.Fill("bGardenShed", -0.7, -0.7, 2.4, 2.4);
                lot.Put("bWorkbench", 1.15, -1.4, 0, 0.85);
                foreach (double u in new[] { -0.7, 0.5 }) lot.Put("bSawhorse", u, 1.25, 1, 1);
                lot.Put("bWoodPile", 1.6, 0.7, 1, 0.9);
                if (level >= 2) lot.Put("bTrestle", -1.55, 1.3, 0, 0.9);
                if (level >= 3)
                {
                    lot.Put("bPlankRack", 0.3, 1.7, 0, 0.6);
                    lot.Put("bAnvil", 1.2, 0.3, 0, 1);
                }
            },
        };

        /// <summary>A building knocked about: grit and bits at its foot, more and a heap of junk as it is about to go.</summary>
        static void Debris(Lot lot, int wear, Func<int, double> r)
        {
            int n = wear * 3;
            for (int k = 0; k < n; k++)
            {
                int side = (int)Math.Floor(r(50 + k) * 4);
                double t = r(60 + k) - 0.5;
                double u = side % 2 != 0 ? (side == 1 ? 1 : -1) * (lot.w / 2 + 0.05) : t * lot.w;
                double v = side % 2 != 0 ? t * lot.h : (side == 2 ? 1 : -1) * (lot.h / 2 + 0.05);
                lot.Put("pebbles", u, v, r(70 + k) * 4, 1, 0.01, false);
            }
            if (wear == 2) lot.Put("bJunkHeap", lot.w / 2 - 0.4, lot.h / 2 - 0.3, r(80) * 4, 0.45);
        }

        /// <summary>What is left of a building: heaps of burnt wreckage and junk over its ground, and grit round them.</summary>
        static void Ruin(Lot lot, Func<int, double> r)
        {
            lot.Fill("bBurntHeap", -lot.w / 4, 0, lot.w / 2, lot.h * 0.8, Math.Floor(r(90) * 4));
            lot.Fill("bJunkHeap", lot.w / 4, (r(91) - 0.5) * 0.4, lot.w / 2, lot.h * 0.7, Math.Floor(r(92) * 4));
            for (int k = 0; k < 4; k++) lot.Put("pebbles", (r(93 + k) - 0.5) * lot.w, (r(97 + k) - 0.5) * lot.h, r(101 + k) * 4, 1, 0.01, false);
        }
    }
}
