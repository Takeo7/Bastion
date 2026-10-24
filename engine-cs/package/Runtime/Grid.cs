#nullable enable
// grid.ts: tiles, levels, heights and cover on the map.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class Grid
    {
        private static Vec2 V(int x, int y)
        {
            return new Vec2 { X = x, Y = y };
        }

        /// <summary>Cardinal directions in N, E, S, W order (y grows southwards).</summary>
        public static readonly IReadOnlyList<Vec2> Dirs4 = new[]
        {
            V(0, -1),
            V(1, 0),
            V(0, 1),
            V(-1, 0),
        };

        public static readonly IReadOnlyList<Vec2> Dirs8 = Dirs4.Concat(new[]
        {
            V(1, -1),
            V(1, 1),
            V(-1, 1),
            V(-1, -1),
        }).ToArray();

        // Heights are measured in storeys. A wall is exactly one storey tall.
        /// <summary>Eye height of a unit above the floor it stands on.</summary>
        public const double EyeHeight = 0.7;
        /// <summary>Point on a target that a shot or a glance has to reach.</summary>
        public const double BodyHeight = 0.55;

        /// <summary>Height of what stands on a tile, above the tile's own elevation.</summary>
        private static readonly Dictionary<string, double> ObstacleHeights = new Dictionary<string, double>
        {
            [TileKinds.Floor] = 0,
            [TileKinds.Ladder] = 0,
            [TileKinds.DoorOpen] = 0,
            [TileKinds.Wall] = 1,
            [TileKinds.Door] = 1,
            [TileKinds.Console] = 0.8,
            [TileKinds.High] = 0.8,
            [TileKinds.Low] = 0.4,
            // A roof nobody stands on: the slab is all there is (docs/FASE6-PLAN.md).
            [TileKinds.Roof] = 0,
            // Windows are holes in a wall: they never stop a line of sight...
            [TileKinds.Window] = 0,
            [TileKinds.WindowBroken] = 0,
        };

        /// <summary>...but whoever stands beside one is protected by the wall around it.</summary>
        private static readonly Dictionary<string, double> CoverHeights = new Dictionary<string, double>(ObstacleHeights)
        {
            [TileKinds.Window] = 1,
            [TileKinds.WindowBroken] = 1,
        };

        public static bool InBounds(GameState s, int x, int y)
        {
            return x >= 0 && y >= 0 && x < s.Width && y < s.Height;
        }

        public static int TileIndex(GameState s, int x, int y)
        {
            return y * s.Width + x;
        }

        /// <summary>Out-of-bounds reads behave like an indestructible wall.</summary>
        public static string TileAt(GameState s, int x, int y)
        {
            return InBounds(s, x, y) ? s.Tiles[TileIndex(s, x, y)] : TileKinds.Wall;
        }

        /// <summary>Elevation (storeys) of the tile's floor.</summary>
        public static int ElevAt(GameState s, int x, int y)
        {
            return InBounds(s, x, y) ? Js.At(s.Elev, TileIndex(s, x, y), 0) : 0;
        }

        // ---------------------------------------------------------------- levels
        // Buildings of several storeys (docs/FASE6-PLAN.md): the ground is `tiles` and
        // `elev`, as ever; storey k is `levels[k - 1]`, k storeys up. A place is a
        // Pos: a tile and its level.

        /// <summary>The highest level a map can have: the roof of a three-storey building.</summary>
        public const int TopLevel = 3;

        /// <summary>Levels of the map, the ground included: 1 without storeys.</summary>
        public static int LevelCount(GameState s)
        {
            return 1 + (s.Levels?.Count ?? 0);
        }

        /// <summary>`s.levels?.[k]`: storey k + 1, or null past the map's.</summary>
        internal static Level? LevelOf(GameState s, int k)
        {
            return s.Levels != null && k >= 0 && k < s.Levels.Count ? s.Levels[k] : null;
        }

        /// <summary>`level.tiles[i] ?? null`.</summary>
        internal static string? LevelTile(Level? level, int i)
        {
            return level != null && i >= 0 && i < level.Tiles.Count ? level.Tiles[i] : null;
        }

        /// <summary>A place's index among all of the map's (the ground's first, so on the ground it is the tile's index).</summary>
        public static int NodeOf(GameState s, Pos p)
        {
            return (p.Z * s.Height + p.Y) * s.Width + p.X;
        }

        /// <summary>The place with index `node` (nodeOf).</summary>
        public static Pos PosOf(GameState s, int node)
        {
            var area = s.Width * s.Height;
            var z = (int)Math.Floor((double)node / area);
            var i = node - z * area;
            var x = i % s.Width;
            return new Pos { X = x, Y = (i - x) / s.Width, Z = z };
        }

        /// <summary>A place on the ground.</summary>
        public static Pos Ground(Vec2 p)
        {
            return new Pos { X = p.X, Y = p.Y, Z = 0 };
        }

        /// <summary>A place from the wire, the client or an older save, where a missing level is the ground (a Pos's Z is 0 then): a copy.</summary>
        public static Pos ToPos(Pos p)
        {
            return new Pos { X = p.X, Y = p.Y, Z = p.Z };
        }

        /// <summary>
        /// What stands on a place: on the ground the tile, as tileAt; on a storey what
        /// stands on its slab, or null where there is none (open air). Out of bounds,
        /// or above the map's storeys, like tileAt: a wall on the ground, air above.
        /// </summary>
        public static string? KindAt(GameState s, Pos p)
        {
            if (p.Z == 0) return TileAt(s, p.X, p.Y);
            if (!InBounds(s, p.X, p.Y)) return null;
            return LevelTile(LevelOf(s, p.Z - 1), TileIndex(s, p.X, p.Y));
        }

        /// <summary>Height (storeys) of a place's floor: the tile's elevation on the ground, k on storey k.</summary>
        public static int HeightAt(GameState s, Pos p)
        {
            return p.Z == 0 ? ElevAt(s, p.X, p.Y) : p.Z;
        }

        /// <summary>Whether storey `k` (≥ 1) has a slab on tile (x, y): a floor, a ceiling below it, or a roof.</summary>
        public static bool SlabAt(GameState s, int x, int y, int k)
        {
            if (k < 1 || !InBounds(s, x, y)) return false;
            return LevelTile(LevelOf(s, k - 1), TileIndex(s, x, y)) != null;
        }

        /// <summary>
        /// The places of tile (x, y) there is something on: the ground, and each storey
        /// with a slab there (docs/FASE6-PLAN.md).
        /// </summary>
        public static List<Pos> PlacesOn(GameState s, int x, int y)
        {
            var out_ = new List<Pos> { new Pos { X = x, Y = y, Z = 0 } };
            var i = TileIndex(s, x, y);
            if (s.Levels != null)
            {
                for (var k = 0; k < s.Levels.Count; k++)
                {
                    if (LevelTile(s.Levels[k], i) != null) out_.Add(new Pos { X = x, Y = y, Z = k + 1 });
                }
            }
            return out_;
        }

        /// <summary>
        /// Whether what happens on `a` (a blast, smoke, a song, an aura) reaches `b`,
        /// height-wise (docs/FASE6-PLAN.md): at most a storey apart, with no floor or
        /// ceiling between them in either's column — a floor stops a blast (decision
        /// 5). Without storeys, always: the map was flat for it.
        /// </summary>
        public static bool SameSpace(GameState s, Pos a, Pos b)
        {
            if (s.Levels == null) return true;
            var pa = ToPos(a);
            var pb = ToPos(b);
            var ha = HeightAt(s, pa);
            var hb = HeightAt(s, pb);
            if (Math.Abs(ha - hb) > 1 + 1e-6) return false;
            var lo = Math.Min(ha, hb);
            var hi = Math.Max(ha, hb);
            for (var k = (int)Math.Floor(lo + 1e-6) + 1; k <= hi + 1e-6; k++) if (SlabAt(s, pa.X, pa.Y, k) || SlabAt(s, pb.X, pb.Y, k)) return false;
            return true;
        }

        /// <summary>
        /// Storeys between two places, for how far a noise carries (decision 3 in
        /// FASE6-PLAN.md: +1 of distance a storey). None without storeys.
        /// </summary>
        public static int StoreysApart(GameState s, Pos a, Pos b)
        {
            return s.Levels != null ? (int)Math.Abs(Js.Round(HeightAt(s, ToPos(a))) - Js.Round(HeightAt(s, ToPos(b)))) : 0;
        }

        /// <summary>
        /// The level of tile (x, y) whose floor is at height `h` and can be walked on,
        /// or null: the ground when its elevation is `h`, else storey `h`.
        /// </summary>
        public static Pos? SurfaceAt(GameState s, int x, int y, int h)
        {
            if (!InBounds(s, x, y)) return null;
            if (ElevAt(s, x, y) == h && IsWalkable(TileAt(s, x, y))) return new Pos { X = x, Y = y, Z = 0 };
            if (h < 1 || h > TopLevel) return null; // h !== Math.floor(h): a whole number here.
            var kind = LevelTile(LevelOf(s, h - 1), TileIndex(s, x, y));
            return kind != null && IsWalkable(kind) ? new Pos { X = x, Y = y, Z = h } : null;
        }

        /// <summary>Units can walk across the tile (doors open, windows are vaulted); never open air.</summary>
        public static bool IsWalkable(string? kind)
        {
            return kind == TileKinds.Floor || kind == TileKinds.Ladder || kind == TileKinds.Door || kind == TileKinds.DoorOpen || kind == TileKinds.Window || kind == TileKinds.WindowBroken;
        }

        /// <summary>Units can end a move on the tile (nobody stands inside a window frame); by its kind alone: a flight of stairs is flights.ts canStopAt's.</summary>
        public static bool CanStandOn(string? kind)
        {
            return IsWalkable(kind) && kind != TileKinds.Window && kind != TileKinds.WindowBroken;
        }

        public static bool IsWindow(string? kind)
        {
            return kind == TileKinds.Window || kind == TileKinds.WindowBroken;
        }

        /// <summary>Top of the opaque column on a tile, in absolute storeys (raised floors are solid underneath).</summary>
        public static double SightTop(GameState s, int x, int y)
        {
            if (!InBounds(s, x, y)) return double.PositiveInfinity;
            return ElevAt(s, x, y) + GroundSightHeight(s, TileIndex(s, x, y));
        }

        /// <summary>
        /// How tall what stands on ground tile `i` is for sight: its kind's height, but for full cover lower than the eyes,
        /// the object's own (decor.ts decorSightHeight: Arturo, 22/10). Cover goes by the kind alone.
        /// </summary>
        public static double GroundSightHeight(GameState s, int i)
        {
            var kind = s.Tiles[i];
            return (kind == TileKinds.High ? Decor.DecorSightHeight(s.Decor, s.Width, i) : null) ?? ObstacleHeights[kind];
        }

        /// <summary>Storeys of the building a tile belongs to (GameState.storeys): 0 outside one, and on maps without them.</summary>
        public static int StoreysAt(GameState s, int x, int y)
        {
            return InBounds(s, x, y) ? Js.At(s.Storeys, TileIndex(s, x, y), 0) : 0;
        }

        /// <summary>
        /// Whether what stands on a tile stops a line of sight crossing it at height
        /// `h` (absolute storeys): below the top of its object, as ever, and in a
        /// building of two storeys or more, anywhere from the ground storey's ceiling
        /// up to its roof. So its walls and closed doors go all the way up, its windows
        /// and doorways are holes in the ground storey under a lintel, and above the
        /// ground storey its floors and ceilings fill the inside. The roof itself (also
        /// a one-storey building's) is lineClear's: see roofSide. A map without storeys
        /// is as it always was.
        /// </summary>
        public static bool BlocksSightAt(GameState s, int x, int y, double h)
        {
            if (SightTop(s, x, y) > h + 1e-6) return true;
            var n = StoreysAt(s, x, y);
            if (n < 2) return false;
            var @base = ElevAt(s, x, y);
            return h > @base + 1 - 1e-6 && @base + n > h + 1e-6;
        }

        /// <summary>RoofSide's answers: 'under' | 'over' (or null).</summary>
        public static class RoofSides
        {
            public const string Under = "under";
            public const string Over = "over";
        }

        /// <summary>
        /// Where a line at height `h` crossing a building's tile stands against it:
        /// 'under' its ground storey's ceiling, 'over' its roof, or null (no building,
        /// or inside its upper storeys, which blocksSightAt stops). A line that goes
        /// from one to the other between two tiles of a building went through its roof.
        /// </summary>
        public static string? RoofSide(GameState s, int x, int y, double h)
        {
            var n = StoreysAt(s, x, y);
            if (n < 1) return null;
            var @base = ElevAt(s, x, y);
            if (h < @base + 1 - 1e-6) return RoofSides.Under;
            return h > @base + n - 1e-6 ? RoofSides.Over : null;
        }

        /// <summary>
        /// On a map with storeys (docs/FASE6-PLAN.md), whether what stands on tile `i`
        /// (on the map: lineClear's walk) stops a line crossing it at height `h`: the
        /// ground and its object, as sightTop, and on each storey what stands on its
        /// slab, from the slab up — a wall or a closed door the whole storey, a window
        /// nothing (a hole on its floor). The slabs, floors and ceilings, are
        /// lineClear's: a line can only cross one between two tiles. `storeys` plays
        /// no part.
        /// </summary>
        public static bool OpaqueAtIndex(GameState s, int i, double h)
        {
            if (Js.At(s.Elev, i, 0) + GroundSightHeight(s, i) > h + 1e-6) return true;
            var k = (int)Math.Floor(h + 1e-6);
            if (k < 1 || s.Levels == null || k > s.Levels.Count) return false;
            var kind = LevelTile(s.Levels[k - 1], i);
            return kind != null && k + ObstacleHeights[kind] > h + 1e-6;
        }

        /// <summary>Height of what stands on a tile of `kind` above its floor (what opaqueAtIndex compares).</summary>
        public static double ObstacleHeight(string kind)
        {
            return ObstacleHeights[kind];
        }

        /// <summary>Cover offered by the tile `side` to a unit standing at elevation `standElev`.</summary>
        public static int CoverFrom(GameState s, double standElev, Vec2 side)
        {
            if (!InBounds(s, side.X, side.Y)) return 2;
            var kind = TileAt(s, side.X, side.Y);
            return CoverLevel(ElevAt(s, side.X, side.Y) + CoverHeights[kind] - standElev);
        }

        private static int CoverLevel(double rel)
        {
            if (rel >= 0.75) return 2;
            if (rel >= 0.3) return 1;
            return 0;
        }

        /// <summary>
        /// Cover the tile `side` gives a unit standing on `stand`, on its level
        /// (docs/FASE6-PLAN.md): what stands on the ground beside it (a raised block
        /// taller than the unit is a wall), or on the storey it stands on — its walls
        /// and windows full, furniture as on the ground, a roof's parapet half. Open
        /// air beside (a roof's edge) gives nothing. Without storeys, coverFrom.
        /// </summary>
        public static int CoverAt(GameState s, Pos stand, Vec2 side)
        {
            var h = HeightAt(s, stand);
            if (s.Levels == null || !InBounds(s, side.X, side.Y)) return CoverFrom(s, h, side);
            var top = ElevAt(s, side.X, side.Y) + CoverHeights[TileAt(s, side.X, side.Y)];
            for (var k = 1; k <= s.Levels.Count; k++)
            {
                var kind = LevelTile(s.Levels[k - 1], TileIndex(s, side.X, side.Y));
                if (kind != null && k <= h + 1e-6) top = Math.Max(top, k + CoverHeights[kind]);
            }
            return CoverLevel(top - h);
        }

        /// <summary>Flat cover value of a tile kind (used by map tooling and tests).</summary>
        public static int CoverOf(string kind)
        {
            var h = CoverHeights[kind];
            return h >= 0.75 ? 2 : h >= 0.3 ? 1 : 0;
        }

        /// <summary>Same tile, whatever the level (sameTile) — see samePos for places.</summary>
        public static bool SameTile(Vec2 a, Vec2 b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        /// <summary>Same place: same tile and same level.</summary>
        public static bool SamePos(Pos a, Pos b)
        {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static double Distance(Vec2 a, Vec2 b)
        {
            return Js.Hypot(a.X - b.X, a.Y - b.Y);
        }

        /// <summary>The living unit on a place (its level too; without one, on the ground).</summary>
        public static Unit? UnitAt(GameState s, Pos p)
        {
            var z = p.Z;
            return s.Units.Find((u) => u.Alive && u.Pos.X == p.X && u.Pos.Y == p.Y && u.Pos.Z == z);
        }

        public static Unit? GetUnit(GameState s, string id)
        {
            return s.Units.Find((u) => u.Id == id);
        }

        /// <summary>Living enemies that fight: the relay and captive VIPs don't count.</summary>
        public static List<Unit> Combatants(GameState s, string team)
        {
            return s.Units.FindAll((u) => u.Alive && u.Team == team && !u.Objective && !u.Captive);
        }
    }
}
