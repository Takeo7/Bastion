#nullable enable
// los.ts: lines of sight, who sees whom, light at night and the fog of war.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bastion.Engine
{
    public static class Los
    {
        /// <summary>
        /// The most windows a line of sight goes through (playtest 2026-10-16): from
        /// outside, through a window, what is inside; never on out through a second one.
        /// </summary>
        public const int MaxWindowsSeenThrough = 1;
        /// <summary>
        /// How near a window (tiles, any way) one end of a line must be for it to go
        /// through (Ana, 23/10 #21): who looks from right at the glass sees out, and
        /// who stands right at it is seen; from further off, only the glass.
        /// </summary>
        public const int WindowSightReach = 1;

        /// <summary>
        /// Whether a line from `a`, `ha` storeys up, to `b`, `hb` up (by default a
        /// unit's eyes and the body of whoever stands there, on their levels), sees
        /// through: on a map with storeys, through floors and ceilings only where they
        /// have a hole (lineThroughStoreys); without them, as it always did (lineFlat).
        /// Either way, through MAX_WINDOWS_SEEN_THROUGH windows at most, and only
        /// those within `windowReach` of `a` or `b` (WINDOW_SIGHT_REACH).
        /// </summary>
        /// <remarks>`ha` and `hb` null take their defaults, as undefined does in TypeScript.</remarks>
        public static bool LineClear(GameState s, Pos a, Pos b, double? ha = null, double? hb = null, double windowReach = WindowSightReach)
        {
            var eye = ha ?? Grid.HeightAt(s, Grid.ToPos(a)) + Grid.EyeHeight;
            var body = hb ?? Grid.HeightAt(s, Grid.ToPos(b)) + Grid.BodyHeight;
            return s.Levels != null ? LineThroughStoreys(s, a, b, eye, body, windowReach) : LineFlat(s, a, b, eye, body, windowReach);
        }

        /// <summary>
        /// Grid walk between tile centres in 3D: the line goes from height `ha` above
        /// tile `a` to height `hb` above tile `b` (absolute storeys) and is blocked by
        /// any tile whose opaque column it meets (blocksSightAt: what stands there, and
        /// the upper storeys of a tall building), and by a building's roof: it can't go
        /// from over the roof to under the ceiling between two of the building's tiles
        /// (roofSide). Start and end tiles never block, but they count for the roof.
        /// When the line passes exactly through a corner it is blocked only if both
        /// tiles beside the corner block (permissive, symmetric).
        /// </summary>
        private static bool LineFlat(GameState s, Vec2 a, Vec2 b, double ha, double hb, double windowReach)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var nx = Math.Abs(dx);
            var ny = Math.Abs(dy);
            var sx = Js.Sign(dx);
            var sy = Js.Sign(dy);
            var len2 = dx * dx + dy * dy;
            if (len2 == 0) len2 = 1;
            // Height of the line where it crosses tile (x, y).
            double LineAt(int x, int y)
            {
                var t = Math.Min(1, Math.Max(0, (double)((x - a.X) * dx + (y - a.Y) * dy) / len2));
                return ha + (hb - ha) * t;
            }
            bool Blocks(int x, int y) => Grid.BlocksSightAt(s, x, y, LineAt(x, y));
            // Where the line stood against the roof of the last building tile it crossed.
            var side = Grid.RoofSide(s, a.X, a.Y, ha);
            bool ThroughRoof(int x, int y, double h)
            {
                var now = Grid.RoofSide(s, x, y, h);
                var pierced = now != null && side != null && now != side;
                side = now;
                return pierced;
            }
            var cx = a.X;
            var cy = a.Y;
            var ix = 0;
            var iy = 0;
            var windows = 0;

            while (ix < nx || iy < ny)
            {
                var decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    if (Blocks(cx + sx, cy) && Blocks(cx, cy + sy)) return false;
                    cx += sx;
                    cy += sy;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    cx += sx;
                    ix++;
                }
                else
                {
                    cy += sy;
                    iy++;
                }
                if (cx == b.X && cy == b.Y) return !ThroughRoof(cx, cy, hb);
                if (Blocks(cx, cy) || ThroughRoof(cx, cy, LineAt(cx, cy))) return false;
                // A window counts where the line goes through it, on the ground floor (not over its roof).
                var i = cy * s.Width + cx;
                if (Grid.IsWindow(s.Tiles[i]) && LineAt(cx, cy) < Js.At(s.Elev, i, 0) + 1 && (FarFromWindow(cx, cy, a, b, windowReach) || ++windows > MaxWindowsSeenThrough)) return false;
            }
            return true;
        }

        /// <summary>
        /// The same walk on a map with storeys (docs/FASE6-PLAN.md), heights taken the
        /// same way. On each tile the line crosses, what stands there at its height
        /// stops it (opaqueAtIndex: the ground's objects; a storey's walls and closed doors,
        /// not its windows). Between two tiles, a floor or ceiling it passes through
        /// stops it: the slab of the tile where it crosses that height, the one it
        /// leaves or the one it enters. So does one between the two heights when the
        /// line goes straight up or down a single tile. Start and end tiles never
        /// block, but their slabs count; corners as lineFlat.
        /// </summary>
        private static bool LineThroughStoreys(GameState s, Vec2 a, Vec2 b, double ha, double hb, double windowReach)
        {
            if (a.X == b.X && a.Y == b.Y) return !SlabsBetween(s, a.X, a.Y, ha, hb);
            // Written for speed (the fog runs it tens of thousands of times): indices, no closures.
            var w = s.Width;
            var levels = s.Levels!;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var nx = Math.Abs(dx);
            var ny = Math.Abs(dy);
            var sx = Js.Sign(dx);
            var sy = Js.Sign(dy);
            double len2 = dx * dx + dy * dy;
            var rise = hb - ha;
            // The height of the line where it crosses the tile (ox, oy) away from `a` is
            // ha + rise * Math.min(1, Math.max(0, (ox * dx + oy * dy) / len2)), written
            // out where it is needed: a closure here costs more than the walk itself.
            var pi = a.Y * w + a.X;
            var ph = ha;
            var x = a.X;
            var y = a.Y;
            var ix = 0;
            var iy = 0;
            var windows = 0;
            while (ix < nx || iy < ny)
            {
                var decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    var ox = x - a.X;
                    var oy = y - a.Y;
                    if (
                        Grid.OpaqueAtIndex(s, y * w + x + sx, ha + rise * Math.Min(1, Math.Max(0, ((ox + sx) * dx + oy * dy) / len2))) &&
                        Grid.OpaqueAtIndex(s, (y + sy) * w + x, ha + rise * Math.Min(1, Math.Max(0, (ox * dx + (oy + sy) * dy) / len2)))
                    )
                        return false;
                    x += sx;
                    y += sy;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    x += sx;
                    ix++;
                }
                else
                {
                    y += sy;
                    iy++;
                }
                var i = y * w + x;
                var end = x == b.X && y == b.Y;
                var h = end ? hb : ha + rise * Math.Min(1, Math.Max(0, ((x - a.X) * dx + (y - a.Y) * dy) / len2));
                // Through a floor or ceiling between the last tile and this one: the slab of the tile where it crosses that height.
                if (h != ph)
                {
                    var lo = Math.Min(ph, h);
                    var hi = Math.Max(ph, h);
                    for (var k = (int)Math.Floor(lo + 1e-6) + 1; k < hi - 1e-6; k++)
                    {
                        var j = (k - ph) / (h - ph) < 0.5 ? pi : i;
                        if (k <= levels.Count && Grid.LevelTile(levels[k - 1], j) != null) return false;
                    }
                }
                if (end) return true;
                if (Grid.OpaqueAtIndex(s, i, h)) return false;
                // A window on the floor the line crosses at: the ground's within its first storey, else that storey's.
                var kk = (int)Math.Floor(h + 1e-6);
                if (
                    Grid.IsWindow(h < Js.At(s.Elev, i, 0) + 1 ? s.Tiles[i] : kk >= 1 ? Grid.LevelTile(Grid.LevelOf(s, kk - 1), i) : null) &&
                    (FarFromWindow(x, y, a, b, windowReach) || ++windows > MaxWindowsSeenThrough)
                )
                    return false;
                pi = i;
                ph = h;
            }
            return true;
        }

        /// <summary>Whether a window on (x, y) is further than `reach` from both ends of a line, `a` and `b`: it shows only the glass.</summary>
        private static bool FarFromWindow(int x, int y, Vec2 a, Vec2 b, double reach)
        {
            return Math.Max(Math.Abs(x - a.X), Math.Abs(y - a.Y)) > reach && Math.Max(Math.Abs(x - b.X), Math.Abs(y - b.Y)) > reach;
        }

        /// <summary>Whether a slab of tile (x, y) lies strictly between heights `a` and `b`.</summary>
        private static bool SlabsBetween(GameState s, int x, int y, double a, double b)
        {
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            for (var k = (int)Math.Floor(lo + 1e-6) + 1; k < hi - 1e-6; k++) if (Grid.SlabAt(s, x, y, k)) return true;
            return false;
        }

        /// <summary>
        /// Tiles a unit at `p` can look/shoot from: its own tile plus the XCOM-style
        /// "step out" tiles beside any adjacent full cover, on the same level.
        /// </summary>
        public static List<Pos> PeekOrigins(GameState s, Pos p)
        {
            var origins = new List<Pos> { p };
            var h = Grid.HeightAt(s, p);
            foreach (var d in Grid.Dirs4)
            {
                if (Grid.CoverAt(s, p, new Vec2 { X = p.X + d.X, Y = p.Y + d.Y }) != 2) continue;
                foreach (var side in new[] { (x: d.Y, y: -d.X), (x: -d.Y, y: d.X) })
                {
                    var q = new Pos { X = p.X + side.x, Y = p.Y + side.y, Z = p.Z };
                    if (Flights.CanStopAt(s, q) && Grid.HeightAt(s, q) == h && !origins.Any((o) => Grid.SamePos(o, q))) origins.Add(q);
                }
            }
            return origins;
        }

        /// <summary>Origin tile a viewer at `from` uses to see `to`, or null if there is no line of sight.</summary>
        public static Pos? SightOrigin(GameState s, Pos from, Pos to, double range)
        {
            if (Grid.Distance(from, to) > range) return null;
            var targets = PeekOrigins(s, to);
            foreach (var o in PeekOrigins(s, from))
            {
                foreach (var t in targets)
                {
                    if (LineClear(s, o, t)) return o;
                }
            }
            return null;
        }

        /// <summary>Tiles a unit sees by day: its kind's, plus Ojos del viento.</summary>
        public static int SightOf(Unit u)
        {
            return Content.Templates[u.Template].Sight + (u.Mods?.Sight ?? 0);
        }

        // ------------------------------------------------------------------ night

        /// <summary>
        /// The places (nodes, grid.ts nodeOf; on the ground y * width + x) a light on
        /// `tile`, `height` storeys above its floor, reaches: within `radius`, and in
        /// its line of sight (walls, roofs and floors keep it out). Worked out once,
        /// when the light goes on.
        /// </summary>
        public static List<int> LightTiles(GameState s, Pos tile, double radius, double height)
        {
            var out_ = new List<int>();
            var r = (int)Math.Ceiling(radius);
            var from = Grid.HeightAt(s, tile) + height;
            for (var y = tile.Y - r; y <= tile.Y + r; y++)
            {
                for (var x = tile.X - r; x <= tile.X + r; x++)
                {
                    if (!Grid.InBounds(s, x, y) || Js.Hypot(tile.X - x, tile.Y - y) > radius) continue;
                    // Light gets out through a window from anywhere in the room.
                    foreach (var p in Grid.PlacesOn(s, x, y)) if (Grid.SamePos(p, tile) || LineClear(s, tile, p, from, null, double.PositiveInfinity)) out_.Add(Grid.NodeOf(s, p));
                }
            }
            return out_;
        }

        /// <summary>Whether `p` is lit (GameState.lights): at night, whoever stands there is seen from as far as by day.</summary>
        public static bool IsLit(GameState s, Pos p)
        {
            var i = Grid.NodeOf(s, Grid.ToPos(p));
            return (s.Lights ?? new List<Light>()).Any((l) => Grid.Distance(l.Tile, p) <= l.Radius && (l.Tiles?.Contains(i) ?? true));
        }

        /// <summary>Every lit place, by node (on the ground, y * width + x), for looking at many places at once.</summary>
        public static bool[] LitMask(GameState s)
        {
            var mask = new bool[s.Width * s.Height * Grid.LevelCount(s)];
            foreach (var l in s.Lights ?? new List<Light>())
            {
                if (l.Tiles != null)
                {
                    foreach (var i in l.Tiles) mask[i] = true;
                    continue;
                }
                // A light saved before its tiles were worked out: its whole circle.
                var r = (int)Math.Ceiling(l.Radius);
                for (var y = Math.Max(0, l.Tile.Y - r); y <= Math.Min(s.Height - 1, l.Tile.Y + r); y++)
                {
                    for (var x = Math.Max(0, l.Tile.X - r); x <= Math.Min(s.Width - 1, l.Tile.X + r); x++)
                    {
                        if (Js.Hypot(l.Tile.X - x, l.Tile.Y - y) <= l.Radius) mask[y * s.Width + x] = true;
                    }
                }
            }
            return mask;
        }

        /// <summary>How far the living see in the dark (NIGHT in content.ts); the dead see as ever.</summary>
        public static int NightSight(Unit u)
        {
            var full = SightOf(u);
            return Content.Templates[u.Template].Undead ? full : Math.Min(full, Math.Max(Content.Night.MinSight, full - Content.Night.SightCut));
        }

        /// <summary>How far `viewer` sees something on `p`: its sight by day, or at night unless `p` is lit (`lit`: a litMask, to spare work).</summary>
        public static int SightTo(GameState s, Unit viewer, Pos p, IReadOnlyList<bool>? lit = null)
        {
            if (s.Mission?.Night != true) return SightOf(viewer);
            return (lit != null ? lit[Grid.NodeOf(s, Grid.ToPos(p))] : IsLit(s, p)) ? SightOf(viewer) : NightSight(viewer);
        }

        /// <summary>How close a soldier on `p` must be for `viewer` to notice it while the squad is hidden: halved at night, unless lit.</summary>
        public static double DetectionTo(GameState s, Unit viewer, Pos p)
        {
            var range = Content.Templates[viewer.Template].Detection;
            return s.Mission?.Night == true && !IsLit(s, p) ? range * Content.Night.Detection : range;
        }

        public static bool CanSee(GameState s, Unit viewer, Unit target, Pos? viewerPos = null)
        {
            var from = viewerPos ?? viewer.Pos;
            return InViewCone(viewer, target.Pos, from) && SightOrigin(s, from, target.Pos, SightTo(s, viewer, target.Pos)) != null;
        }

        /// <summary>How wide the enemies see, in degrees round where they face (Arturo, 17/10: «en cono y no en área»; the living too, 18/10 #46).</summary>
        public const int ViewCone = 120;
        /// <summary>Whoever is this close, a dead one notices whatever way it faces.</summary>
        public const double AwareRange = 1.5;

        /// <summary>
        /// Whether `p` lies in `viewer`'s field of view from `from`: the squad looks
        /// all round; the enemies, dead or living («tienen ojos igual», Arturo 18/10
        /// #46), only ahead of them, in VIEW_CONE, or right beside them. `facing`
        /// overrides the unit's own (a patrol on its way, before its move is written
        /// down). Without a facing, all round.
        /// </summary>
        /// <remarks>`from` and `facing` null take their defaults, as undefined does in TypeScript.</remarks>
        public static bool InViewCone(Unit viewer, Pos p, Pos? from = null, double? facing = null)
        {
            var at = from ?? viewer.Pos;
            var face = facing ?? viewer.Facing;
            if (viewer.Team == Teams.Xcom || face == null) return true;
            var dx = p.X - at.X;
            var dy = p.Y - at.Y;
            if (Js.Hypot(dx, dy) <= AwareRange) return true;
            var off = Math.Abs(((Js.Atan2(dy, dx) - face.Value + 3 * Math.PI) % (2 * Math.PI)) - Math.PI);
            return off <= (ViewCone / 2) * (Math.PI / 180);
        }

        /// <summary>The way from `a` to `b`, as a facing (radians); null when they are the same tile.</summary>
        public static double? FacingTo(Vec2 a, Vec2 b)
        {
            return a.X == b.X && a.Y == b.Y ? (double?)null : Js.Atan2(b.Y - a.Y, b.X - a.X);
        }

        /// <summary>Whether a viewer standing at `from` can see the tile `to` (no peeks on the target side).</summary>
        public static bool CanSeeTile(GameState s, Pos from, Pos to, double range)
        {
            if (Grid.Distance(from, to) > range) return false;
            return PeekOrigins(s, from).Any((o) => LineClear(s, o, to));
        }

        /// <summary>Units that give their team vision: alive, not the relay, not a captive VIP.</summary>
        public static bool IsViewer(Unit u, string team)
        {
            return u.Alive && u.Team == team && !u.Objective && !u.Captive;
        }

        /// <summary>
        /// How far the squad sees the ground, past where it sees enemies (Arturo, 21/10: «que se vea hasta el final… y
        /// luego un cono más pequeño de largo donde se detecten enemigos»; 22/10: a long street, to its end, to the map's
        /// edge): across the whole map, teamView.
        /// </summary>
        public static double ViewReach(GameState s)
        {
            return Js.Hypot(s.Width, s.Height);
        }

        /// <summary>
        /// Places in the line of sight of any living unit of `team`, as far as the map goes, day or night (Arturo, 21/10
        /// and 22/10): the ground the player sees, enemies or not. Enemies only show within teamVisibility.
        /// </summary>
        public static bool[] TeamView(GameState s, string team)
        {
            // Across the whole map it costs some 10 ms, and it changes only as the viewers move or the map does (a door, a
            // wall blown through): the last one is kept for whatever else changes (the dead on their turn).
            var viewers = s.Units.Where((u) => IsViewer(u, team)).Select((u) => $"{u.Id}@{u.Pos.X},{u.Pos.Y},{u.Pos.Z}");
            var key = $"{team}|{s.Width}x{s.Height}|{MapStamp(s)}|{string.Join(";", viewers)}";
            if (lastView?.Key != key) lastView = new LastView(key, TeamVisibility(s, team, ViewReach(s)));
            return (bool[])lastView.Mask.Clone();
        }

        /// <summary>
        /// The last teamView worked out and what from (teamView). One per thread: in the app the room runs the rules on
        /// its own thread while the client works out the fog on the main one (Net/Room.cs).
        /// </summary>
        [System.ThreadStatic]
        private static LastView? lastView;

        private sealed class LastView
        {
            public LastView(string key, bool[] mask)
            {
                Key = key;
                Mask = mask;
            }

            public string Key { get; }
            public bool[] Mask { get; }
        }

        /// <summary>A hash of what stops sight on the map: its tiles on every level and their heights.</summary>
        private static int MapStamp(GameState s)
        {
            var h = 0;
            unchecked
            {
                void Add(string? k)
                {
                    h = h * 31 + (k == null ? 1 : k[0] * 7 + k.Length);
                }
                foreach (var k in s.Tiles) Add(k);
                foreach (var level in s.Levels ?? new List<Level>()) foreach (var k in level.Tiles) Add(k);
                foreach (var e in s.Elev) h = h * 31 + e;
            }
            return h;
        }

        /// <summary>A viewer of teamVisibility: its origins, their eyes, and the square it looks over.</summary>
        private sealed class Look
        {
            public Unit U = null!;
            public double Range;
            public List<Pos> Origins = null!;
            public double[] Eyes = null!;
            public (int, int, int, double)[] Keys = null!;
            public int MinX;
            public int MaxX;
            public int MinY;
            public int MaxY;
        }

        /// <summary>By origin and eye height, how many viewers look from it, over what, and (shared) what was found blocked from it.</summary>
        private sealed class Tried
        {
            public int Viewers;
            public int X0;
            public int Y0;
            public int X1;
            public int Y1;
            public Blocked? Memo;
        }

        /// <summary>
        /// Places visible to any living unit of `team`, as a boolean mask by node
        /// (grid.ts nodeOf: the ground first, y * width + x, then each storey, where
        /// only its slabs are worked out). At night, the dark ones only from close by.
        /// On a map with storeys, the same lines as lineClear, walked on a SightGrid.
        /// With `reach`, every viewer looks that far instead, and the dark cuts nothing
        /// (teamView).
        /// </summary>
        public static bool[] TeamVisibility(GameState s, string team, double? reach = null)
        {
            var area = s.Width * s.Height;
            var levels = Grid.LevelCount(s);
            var mask = new bool[area * levels];
            var lit = s.Mission?.Night == true && reach == null ? LitMask(s) : null;
            // Worked out on every call: it is cheap next to the walks, and the state changes under it.
            var v = s.Levels != null ? new Viewing { G = SightGridOf(s), W = s.Width, Area = area, N = levels - 1, Elev = s.Elev, Mask = mask, Lit = lit } : null;
            // The viewers first, each with its origins and the square it looks over. A line found blocked is remembered
            // only for an origin that more than one viewer looks from (a viewer looks at each place once), and only over
            // their squares: no map-sized buffer per origin (docs/MAPAS.md §7.3).
            var looks = new List<Look>();
            var tried = new Dictionary<(int, int, int, double), Tried>();
            foreach (var u in s.Units)
            {
                if (!IsViewer(u, team)) continue;
                var range = reach ?? SightOf(u);
                var origins = PeekOrigins(s, u.Pos);
                var eyes = origins.Select((o) => Grid.HeightAt(s, o) + Grid.EyeHeight).ToArray();
                var look = new Look
                {
                    U = u, Range = range, Origins = origins, Eyes = eyes, Keys = origins.Select((o, k) => (o.X, o.Y, o.Z, eyes[k])).ToArray(),
                    MinX = Math.Max(0, (int)Math.Floor(u.Pos.X - range)), MaxX = Math.Min(s.Width - 1, (int)Math.Ceiling(u.Pos.X + range)),
                    MinY = Math.Max(0, (int)Math.Floor(u.Pos.Y - range)), MaxY = Math.Min(s.Height - 1, (int)Math.Ceiling(u.Pos.Y + range)),
                };
                looks.Add(look);
                if (v == null) continue;
                foreach (var key in look.Keys)
                {
                    if (!tried.TryGetValue(key, out var t)) tried[key] = new Tried { Viewers = 1, X0 = look.MinX, Y0 = look.MinY, X1 = look.MaxX, Y1 = look.MaxY, Memo = null };
                    else
                    {
                        t.Viewers++;
                        t.X0 = Math.Min(t.X0, look.MinX);
                        t.Y0 = Math.Min(t.Y0, look.MinY);
                        t.X1 = Math.Max(t.X1, look.MaxX);
                        t.Y1 = Math.Max(t.Y1, look.MaxY);
                    }
                }
            }
            foreach (var look in looks)
            {
                var u = look.U;
                var range = look.Range;
                var dark = lit != null ? NightSight(u) : range;
                if (v != null)
                {
                    v.Dark = dark;
                    v.Ox = look.Origins.Select((o) => o.X).ToArray();
                    v.Oy = look.Origins.Select((o) => o.Y).ToArray();
                    v.Eyes = look.Eyes;
                    v.BlockedBy = look.Keys.Select((key) =>
                    {
                        var t = tried[key];
                        if (t.Viewers < 2) return null;
                        var bw = t.X1 - t.X0 + 1;
                        var bh = t.Y1 - t.Y0 + 1;
                        return t.Memo ??= new Blocked { X0 = t.X0, Y0 = t.Y0, W = bw, H = bh, Data = new byte[bw * bh * levels] };
                    }).ToArray();
                }
                for (var y = look.MinY; y <= look.MaxY; y++)
                {
                    for (var x = look.MinX; x <= look.MaxX; x++)
                    {
                        // distance(u.pos, { x, y }), without the object.
                        var d = HypotOf(u.Pos.X - x, u.Pos.Y - y);
                        if (d > range) continue;
                        if (v != null)
                        {
                            SeeTile(v, x, y, d);
                            continue;
                        }
                        // Without storeys, the ground only.
                        var i = y * s.Width + x;
                        if (mask[i] || (d > dark && !(lit != null && lit[i]))) continue;
                        var target = new Pos { X = x, Y = y, Z = 0 };
                        var body = Js.At(s.Elev, i, 0) + Grid.BodyHeight;
                        for (var k = 0; k < look.Origins.Count; k++)
                        {
                            if (LineClear(s, look.Origins[k], target, look.Eyes[k], body))
                            {
                                mask[i] = true;
                                break;
                            }
                        }
                    }
                }
            }
            return mask;
        }

        /// <summary>Math.hypot of whole offsets up to HYPOT_SIDE - 1 a side, by |dy| * HYPOT_SIDE + |dx|: teamVisibility asks at every tile of a square.</summary>
        private const int HypotSide = 64;
        private static double[]? hypots;

        /// <summary>Math.hypot(dx, dy), the same value, from the table for whole offsets.</summary>
        private static double HypotOf(int dx, int dy)
        {
            var ax = Math.Abs(dx);
            var ay = Math.Abs(dy);
            if (ax >= HypotSide || ay >= HypotSide) return Js.Hypot(dx, dy);
            if (hypots == null)
            {
                var table = new double[HypotSide * HypotSide];
                for (var y = 0; y < HypotSide; y++) for (var x = 0; x < HypotSide; x++) table[y * HypotSide + x] = Js.Hypot(x, y);
                hypots = table;
            }
            return hypots[ay * HypotSide + ax];
        }

        /// <summary>
        /// What lineThroughStoreys reads, laid out flat for the fog: by tile, the top
        /// of the ground's opaque column (elevation plus what stands there); by storey
        /// and tile, the top of what stands on its slab (-Infinity without a slab), and
        /// whether there is a slab.
        /// </summary>
        private sealed class SightGrid
        {
            public double[] Ground = null!;
            public double[] Top = null!;
            public byte[] Slab = null!;
            /// <summary>By level (the ground first) and tile, a window there (MAX_WINDOWS_SEEN_THROUGH).</summary>
            public byte[] Window = null!;
            /// <summary>By tile, the ground's elevation: its windows count below one storey over it.</summary>
            public double[] Elev = null!;
        }

        /// <summary>
        /// The last SightGrid made, its buffers filled again for the next map of the same size (teamVisibility runs on every
        /// change). One per thread, as lastView.
        /// </summary>
        [System.ThreadStatic]
        private static (int Area, int N, SightGrid G)? lastGrid;

        private static SightGrid SightGridOf(GameState s)
        {
            var area = s.Width * s.Height;
            var n = s.Levels?.Count ?? 0;
            if (lastGrid == null || lastGrid.Value.Area != area || lastGrid.Value.N != n)
            {
                lastGrid = (area, n, new SightGrid { Ground = new double[area], Top = new double[area * n], Slab = new byte[area * n], Window = new byte[area * (n + 1)], Elev = new double[area] });
            }
            var g = lastGrid.Value.G;
            var ground = g.Ground;
            var window = g.Window;
            var elev = g.Elev;
            var top = g.Top;
            var slab = g.Slab;
            Array.Clear(window, 0, window.Length);
            for (var i = 0; i < area; i++)
            {
                elev[i] = Js.At(s.Elev, i, 0);
                ground[i] = elev[i] + Grid.GroundSightHeight(s, i);
                if (Grid.IsWindow(s.Tiles[i])) window[i] = 1;
            }
            for (var i = 0; i < top.Length; i++) top[i] = double.NegativeInfinity;
            Array.Clear(slab, 0, slab.Length);
            for (var k = 1; k <= n; k++)
            {
                var level = s.Levels![k - 1];
                for (var i = 0; i < area; i++)
                {
                    var kind = Grid.LevelTile(level, i);
                    if (kind == null) continue;
                    top[(k - 1) * area + i] = k + Grid.ObstacleHeight(kind);
                    slab[(k - 1) * area + i] = 1;
                    if (Grid.IsWindow(kind)) window[k * area + i] = 1;
                }
            }
            return g;
        }

        /// <summary>opaqueAtIndex on a SightGrid (`n` storeys of `area` tiles).</summary>
        private static bool GridOpaque(double[] ground, double[] top, int area, int n, int i, double h)
        {
            if (ground[i] > h + 1e-6) return true;
            var k = (int)Math.Floor(h + 1e-6);
            if (k < 1 || k > n) return false;
            return top[(k - 1) * area + i] > h + 1e-6;
        }

        /// <summary>lineThroughStoreys from tile (ax, ay) to (bx, by) on a SightGrid, step for step.</summary>
        private static bool GridLine(SightGrid g, int w, int area, int n, int ax, int ay, int bx, int by, double ha, double hb)
        {
            var ground = g.Ground;
            var top = g.Top;
            var slab = g.Slab;
            var window = g.Window;
            var elev = g.Elev;
            if (ax == bx && ay == by)
            {
                var lo0 = Math.Min(ha, hb);
                var hi0 = Math.Max(ha, hb);
                for (var k = (int)Math.Floor(lo0 + 1e-6) + 1; k < hi0 - 1e-6; k++) if (k >= 1 && k <= n && slab[(k - 1) * area + ay * w + ax] != 0) return false;
                return true;
            }
            var dx = bx - ax;
            var dy = by - ay;
            var nx = Math.Abs(dx);
            var ny = Math.Abs(dy);
            var sx = Js.Sign(dx);
            var sy = Js.Sign(dy);
            double len2 = dx * dx + dy * dy;
            var rise = hb - ha;
            var pi = ay * w + ax;
            var ph = ha;
            var x = ax;
            var y = ay;
            var ix = 0;
            var iy = 0;
            var windows = 0;
            while (ix < nx || iy < ny)
            {
                var decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    var ox = x - ax;
                    var oy = y - ay;
                    if (
                        GridOpaque(ground, top, area, n, y * w + x + sx, ha + rise * Math.Min(1, Math.Max(0, ((ox + sx) * dx + oy * dy) / len2))) &&
                        GridOpaque(ground, top, area, n, (y + sy) * w + x, ha + rise * Math.Min(1, Math.Max(0, (ox * dx + (oy + sy) * dy) / len2)))
                    )
                        return false;
                    x += sx;
                    y += sy;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    x += sx;
                    ix++;
                }
                else
                {
                    y += sy;
                    iy++;
                }
                var i = y * w + x;
                var end = x == bx && y == by;
                var h = end ? hb : ha + rise * Math.Min(1, Math.Max(0, ((x - ax) * dx + (y - ay) * dy) / len2));
                if (h != ph)
                {
                    var lo = Math.Min(ph, h);
                    var hi = Math.Max(ph, h);
                    for (var k = (int)Math.Floor(lo + 1e-6) + 1; k < hi - 1e-6; k++)
                    {
                        var j = (k - ph) / (h - ph) < 0.5 ? pi : i;
                        // k >= 1: below it TypeScript reads past the start of the array, undefined, which doesn't stop the line.
                        if (k >= 1 && k <= n && slab[(k - 1) * area + j] != 0) return false;
                    }
                }
                if (end) return true;
                if (GridOpaque(ground, top, area, n, i, h)) return false;
                var kk = h < elev[i] + 1 ? 0 : (int)Math.Floor(h + 1e-6);
                // farFromWindow, written out.
                if (
                    kk <= n &&
                    window[kk * area + i] != 0 &&
                    ((Math.Max(Math.Abs(x - ax), Math.Abs(y - ay)) > WindowSightReach && Math.Max(Math.Abs(x - bx), Math.Abs(y - by)) > WindowSightReach) || ++windows > MaxWindowsSeenThrough)
                )
                    return false;
                pi = i;
                ph = h;
            }
            return true;
        }

        /// <summary>One viewer of teamVisibility on a map with storeys: its origins, their eyes, and the lines already found blocked from each.</summary>
        private sealed class Viewing
        {
            public SightGrid G = null!;
            public int W;
            public int Area;
            public int N;
            public IReadOnlyList<int> Elev = null!;
            public bool[] Mask = null!;
            public bool[]? Lit;
            public int[] Ox = new int[0];
            public int[] Oy = new int[0];
            public double[] Eyes = new double[0];
            /// <summary>By origin, what was found blocked from it (null: no other viewer looks from there).</summary>
            public Blocked?[] BlockedBy = new Blocked?[0];
            public double Dark;
        }

        /// <summary>The places of a square (all levels) that lines from one origin were found not to reach.</summary>
        private sealed class Blocked
        {
            public int X0;
            public int Y0;
            public int W;
            public int H;
            /// <summary>By level, row and column of the square.</summary>
            public byte[] Data = null!;
        }

        /// <summary>teamVisibility's look at tile (x, y), `d` away: the ground, then each storey with a slab here.</summary>
        private static void SeeTile(Viewing v, int x, int y, double d)
        {
            var area = v.Area;
            var i = y * v.W + x;
            for (var z = 0; z <= v.N; z++)
            {
                if (z > 0 && v.G.Slab[(z - 1) * area + i] == 0) continue;
                var node = z * area + i;
                if (v.Mask[node] || (d > v.Dark && !(v.Lit != null && v.Lit[node]))) continue;
                var body = (z == 0 ? Js.At(v.Elev, i, 0) : z) + Grid.BodyHeight;
                for (var k = 0; k < v.Eyes.Length; k++)
                {
                    var blocked = v.BlockedBy[k];
                    var at = blocked != null ? (z * blocked.H + y - blocked.Y0) * blocked.W + x - blocked.X0 : 0;
                    if (blocked != null && blocked.Data[at] != 0) continue;
                    if (GridLine(v.G, v.W, area, v.N, v.Ox[k], v.Oy[k], x, y, v.Eyes[k], body))
                    {
                        v.Mask[node] = true;
                        break;
                    }
                    if (blocked != null) blocked.Data[at] = 1;
                }
            }
        }

        /// <summary>
        /// One floor of the cutaway (the floors above it hidden), tile by tile: the
        /// level the camera looks down on, the highest at or below `floor` with a slab
        /// there — or else the ground.
        /// </summary>
        public static int[] FloorLevels(GameState s, int floor)
        {
            var area = s.Width * s.Height;
            var out_ = new int[area];
            var top = Math.Min(floor, Grid.LevelCount(s) - 1);
            for (var i = 0; i < area; i++)
            {
                var z = top;
                while (z > 0 && Grid.LevelTile(s.Levels![z - 1], i) == null) z--;
                out_[i] = z;
            }
            return out_;
        }

        /// <summary>
        /// A mask by node (teamVisibility, litMask, the client's explored places) for
        /// one floor of the cutaway, tile by tile: on each tile, its value on the level
        /// floorLevels shows there.
        /// </summary>
        public static bool[] FloorView(GameState s, IReadOnlyList<bool> mask, int floor)
        {
            var area = s.Width * s.Height;
            return FloorLevels(s, floor).Select((z, i) => Js.At(mask, z * area + i, false)).ToArray();
        }

        /// <summary>Enemies of `team` that at least one living member of `team` can see.</summary>
        public static List<Unit> VisibleEnemies(GameState s, string team)
        {
            var viewers = s.Units.Where((u) => IsViewer(u, team)).ToList();
            return s.Units.Where((t) => t.Alive && t.Team != team && viewers.Any((v) => CanSee(s, v, t))).ToList();
        }
    }
}
