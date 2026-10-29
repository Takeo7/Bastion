#nullable enable
// mapgen.ts in C#: the facility (a factory in a fenced compound), the country (the outskirts' wilds, the service area on
// a forest road, the woods round them) and the opening of dead ends out of doors (openDeadEnds).
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        // ---------------------------------------------------------------- facility

        // A factory (playtest 2026-10-15, #17), not a grid of streets: two yard lanes
        // split the compound in four zones that each do something. By the squad, the
        // entrance and the offices; at the far end, the main hall the factory is named
        // after, its loading docks onto a manoeuvre yard; on one side a container
        // yard; on the other, by the evac, the silos and the car park. Rows of pallets
        // and drums along the lanes, and no stretch of yard without something to get
        // behind.

        /// <summary>A silo, a 3 × 3 block of `high` tiles (docs/MAPAS.md §4.2, phase 5).</summary>
        private static readonly IReadOnlyList<Part> Silo = Dressed(TilesIn(Rect(0, 0, 3, 3)).Select((p) => P(p.X, p.Y, TileKinds.High)).ToList(), DecorKinds.Silo);

        /// <summary>What a factory's yards are strewn with, drawn facing north: pallets and drums.</summary>
        private static IReadOnlyList<IReadOnlyList<Part>> YardCover => yardCover ??= new IReadOnlyList<Part>[]
        {
            Dressed(Barricade, DecorKinds.Pallets),
            Dressed(new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low) }, DecorKinds.Drums),
            Dressed(Corner, DecorKinds.Drums, DecorKinds.Pallets),
            new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Pallets)), P(1, 0, TileKinds.Low, new Look(DecorKinds.Drums)) },
        };
        // Made on first use: it is built from Core.cs's BARRICADE and CORNER, and the static fields of a partial class's
        // files are set up in no promised order.
        private static IReadOnlyList<IReadOnlyList<Part>>? yardCover;

        /// <summary>
        /// The main hall on `b`, its front on side `front`: a 'hall' with two or three
        /// loading docks (gaps of two) in its front wall, the loading floor clear behind
        /// them but for pallets, aisles of shelves in the middle and machines at the
        /// back. A lorry backs up to every dock but one.
        /// </summary>
        private static Building MainHall(Canvas c, Rect b, int front, Rng rng, Style style)
        {
            var hall = MakeBuilding(c, b, rng, Plans.Hall, front, style, Furnishes.Store);
            // Under skylights, not a roof to climb onto: from up there the whole yard and its objective would be one's own.
            hall.Skylit = true;
            var inner = Inset(b, 1);
            var across = SideLen(inner, front);
            var depth = DepthOf(inner, front);
            var L = SideLen(b, front);
            var docks = new List<int>();
            // A dock every seven tiles or so of its front.
            var n = L >= 24 ? rng.Range(3, 4) : rng.Range(2, 3);
            for (var k = 0; k < n; k++)
            {
                var u = (int)Js.Round((double)((k + 1) * L) / (n + 1)) - 1;
                if (u < 2 || u + 1 > L - 3) continue;
                foreach (var du in new[] { 0, 1 })
                {
                    var p = LocalPt(b, front, u + du, 0);
                    c.Force(p.X, p.Y, TileKinds.Floor);
                }
                docks.Add(u);
            }
            // The loading floor is kept clear; past it, the shelves on one side and the machines on the other.
            var machines = rng.Int(2) == 0;
            for (var v = 0; v < depth; v++)
            {
                for (var u = 0; u < across; u++)
                {
                    var p = LocalPt(inner, front, u, v);
                    var i = c.I(p.X, p.Y);
                    if (Decor.DecorOf(c.Decor[i])?.Kind != DecorKinds.Shelf) continue;
                    if (v < 3)
                    {
                        c.Force(p.X, p.Y, TileKinds.Floor);
                        c.Decor[i] = 0;
                    }
                    else if ((u < across / 2.0) == machines)
                    {
                        c.Force(p.X, p.Y, TileKinds.High);
                        c.Dress(p, DecorKinds.Machine, front);
                    }
                }
            }
            // Pallets on the loading floor, between the docks.
            for (var u = 1; u < across - 3; u += 4)
            {
                if (docks.Any((d) => Math.Abs(d - (u + 1)) <= 2)) continue;
                if (rng.Next() < 0.6) Group(c, LocalPt(inner, front, u, 1), Dressed(u % 8 == 1 ? Barricade : new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low) }, DecorKinds.Pallets));
            }
            var empty = rng.Int(Math.Max(1, docks.Count));
            for (var k = 0; k < docks.Count; k++)
            {
                if (k == empty) continue;
                var u = docks[k];
                // As long as the yard lets it be (decor.ts FOOTPRINT): 2 × 6, else 2 × 4.
                foreach (var size in new[] { Decor.Footprint.Truck, Decor.Footprint.TruckShort })
                {
                    var wide = size[0];
                    var deep = size[1];
                    var parts = new List<Part>();
                    for (var du = 0; du < wide; du++)
                    {
                        for (var d = 2; d < 2 + deep; d++)
                        {
                            var p = LocalPt(b, front, u + du, -d);
                            parts.Add(P(p.X, p.Y, TileKinds.High, new Look(DecorKinds.Truck, front)));
                        }
                    }
                    if (Group(c, V(0, 0), parts)) break;
                }
            }
            return hall;
        }

        /// <summary>Runs of pallets and drums along both kerbs of a lane, with gaps to cross them.</summary>
        private static void LaneRows(Canvas c, Rect lane, bool vertical, Rng rng)
        {
            var length = vertical ? lane.H : lane.W;
            var width = vertical ? lane.W : lane.H;
            foreach (var side in new[] { -1, width })
            {
                for (var t = rng.Range(2, 5); t + 3 < length - 2; t += rng.Range(6, 9))
                {
                    if (rng.Next() < 0.3) continue;
                    var look = rng.Next() < 0.5 ? DecorKinds.Pallets : DecorKinds.Drums;
                    var parts = new[] { 0, 1, 2 }.Select((k) => vertical ? P(lane.X + side, lane.Y + t + k, TileKinds.Low, new Look(look, 1)) : P(lane.X + t + k, lane.Y + side, TileKinds.Low, new Look(look, 0))).ToList();
                    Group(c, V(0, 0), parts);
                }
            }
        }

        /// <summary>No stretch of the yards more than six tiles across without something to get behind (#17).</summary>
        private static void CoverGaps(Canvas c, Rect r, Rng rng)
        {
            bool Bare(Vec2 p)
            {
                for (var dy = -3; dy <= 3; dy++)
                {
                    for (var dx = -3; dx <= 3; dx++) if (c.In(p.X + dx, p.Y + dy) && c.Get(p.X + dx, p.Y + dy) != TileKinds.Floor) return false;
                }
                return true;
            }
            foreach (var p in Shuffle(TilesIn(r), rng))
            {
                if (c.Free(p.X, p.Y) && c.GroundAt(p.X, p.Y) != Indoor && Bare(p)) GroupNear(c, p, rng, YardCover, 1, 0, 8);
            }
        }

        /// <summary>
        /// The factory's four zones inside `inner` (its yard less a walk along the
        /// fence), the squad's corner at its top left. Returns where the objective
        /// goes: in the main hall, or out in the yard before it.
        /// </summary>
        private static Rect Factory(Canvas c, Rect inner, Rng rng, Style style, string role)
        {
            var W = inner.W;
            var H = inner.H;
            var a = rng.Range(13, 16);
            var b = rng.Range(13, 16);
            var laneH = Rect(inner.X, inner.Y + a, W, 3);
            var laneV = Rect(inner.X + b, inner.Y, 3, H);
            c.Paint(laneH, Yard);
            c.Paint(laneV, Yard);
            var nw = Rect(inner.X, inner.Y, b, a);
            var ne = Rect(inner.X + b + 3, inner.Y, W - b - 3, a);
            var sw = Rect(inner.X, inner.Y + a + 3, b, H - a - 3);
            var se = Rect(inner.X + b + 3, inner.Y + a + 3, W - b - 3, H - a - 3);

            // The main hall at the far end (docs/MAPAS.md §4.2: 24–30 along its docks, 16–20 deep), its docks onto the
            // manoeuvre yard and one of the lanes, a lorry of 2 × 6 backed onto each: facing the lane it fits along best.
            var fits = new[] { 0, 3 }.Select((s) => (S: s, W: Math.Min(rng.Range(24, 30), SideLen(se, s) - 2), D: Math.Min(rng.Range(16, 20), DepthOf(se, s) - 9))).ToList();
            var (front, w, d) = BestOf(fits, (f) => -(Math.Min(f.W, 24) + Math.Min(f.D, 16) * 1.5) + rng.Next() * 0.1)!.Value;
            var L = SideLen(se, front);
            var D = DepthOf(se, front);
            var u0 = L - w - 1;
            c.Paint(Local(se, front, 0, 0, L, D - d), Yard);
            var hall = MainHall(c, Local(se, front, u0, D - d, w, d), front, rng, style);
            var apron = Local(se, front, u0, Math.Max(0, D - d - 6), w, Math.Min(6, D - d));

            // The offices by the entrance, at the corner of the lanes, their door onto one of them.
            var ow = Math.Min(rng.Range(8, 10), nw.W - 3);
            var od = Math.Min(7, nw.H - 3);
            var offices = MakeBuilding(c, Rect(nw.X + nw.W - 1 - ow, nw.Y + nw.H - 1 - od, ow, od), rng, Plans.Rooms, rng.Int(2) == 0 ? 1 : 2, style, Furnishes.Office);
            if (rng.Next() < 0.5) offices.Storeys = 2;

            // The container yard on one side.
            ContainerYard(c, sw, rng.Int(2) == 0 ? 0 : 1, rng);

            // On the other, by the evac's corner (kept open): the car park along the lane, the silos down by the other one.
            var park = Rect(ne.X + 1, ne.Y + 1, Math.Min(9, ne.W - 9), ne.H - 2);
            c.Paint(park, Yard);
            Parking(c, park, rng, 3, false);
            // (3 × 3 each, a tile apart: decor.ts BLOCK_KINDS never touch.)
            var silos = rng.Range(2, 3);
            for (var k = 0; k < silos; k++) Group(c, V(ne.X + ne.W - 4 - 4 * k, ne.Y + ne.H - 4), Silo);
            GroupNear(c, V(ne.X + ne.W - 4 - 4 * silos, ne.Y + ne.H - 2), rng, new IReadOnlyList<Part>[] { Dressed(Crates, DecorKinds.Drums), Dressed(Pair, DecorKinds.Drums, DecorKinds.Tank) }, 1);

            LaneRows(c, laneH, false, rng);
            LaneRows(c, laneV, true, rng);
            CoverGaps(c, inner, rng);
            return role == Roles.Indoor ? hall.Outer : apron;
        }

        /// <summary>Rows and columns from here on lie inside a facility's fence, whatever the width of the apron (squad outside).</summary>
        private const int FacilityInside = 7;

        /// <summary>A factory's map (docs/MAPAS.md §4.8, phase 5).</summary>
        private const int FactorySize = 72;

        /// <summary>
        /// Facility (docs/MAPAS.md phase 5, §5.10): a walled compound with gates, on a map of 72 closed round. A road comes in
        /// from the squad's side to the apron, which runs along the fence and out by the far side: the lorries' way, and the
        /// bus's. Gates onto the apron, a guard post by each; cars and lorries along it. Round the compound and the roads a
        /// strip of scrub, as wide as it happens to be, and past it the outline: wood, lots, containers. Inside, a factory
        /// (factory); the evacuation by the apron's way out.
        /// </summary>
        private static (Rect Objective, bool Inside) Facility(Canvas c, Rng rng, Style style, string role)
        {
            var W = c.Width;
            var H = c.Height;
            const int Apron = 5;
            var ya = rng.Range(10, 13);
            var fx0 = rng.Range(5, 8);
            var fy0 = ya + Apron;
            var fx1 = W - 1 - rng.Range(9, 12);
            var fy1 = H - 1 - rng.Range(5, 8);
            var sx = rng.Range(fx0 + 8, fx0 + 18);
            var entry = Rect(sx, 0, 5, ya);
            var apron = Rect(fx0 - 1, ya, W - fx0 + 1, Apron);
            c.Paint(entry, Road);
            c.Paint(apron, Road);
            var compound = Rect(fx0, fy0, fx1 - fx0 + 1, fy1 - fy0 + 1);
            // The fence round it.
            for (var x = fx0; x <= fx1; x++) foreach (var y in new[] { fy0, fy1 }) c.Force(x, y, TileKinds.Wall);
            for (var y = fy0; y <= fy1; y++) foreach (var x in new[] { fx0, fx1 }) c.Force(x, y, TileKinds.Wall);
            // Gates: two onto the apron, one on the far side; a booth behind a barrier outside each.
            var gates = new List<(int Side, int At)>
            {
                (0, rng.Range(fx0 + 6, fx0 + 14)),
                (0, rng.Range(fx1 - 16, fx1 - 8)),
                (1, rng.Range(fy0 + 8, fy1 - 10)),
            };
            foreach (var (side, at) in gates)
            {
                for (var k = 0; k < 3; k++)
                {
                    var q = side == 0 ? V(at + k, fy0) : V(fx1, at + k);
                    c.Force(q.X, q.Y, TileKinds.Floor);
                    c.Paint(Rect(q.X, q.Y, 1, 1), Road);
                    foreach (var d in new[] { -1, 0, 1 }) c.Reserve(q.X + (side == 1 ? d : 0), q.Y + (side == 0 ? d : 0));
                }
                var post = side == 0 ? V(at + rng.Pick(new[] { -3, 4 }), fy0 - 2) : V(fx1 + 2, at + rng.Pick(new[] { -3, 4 }));
                GroupNear(c, post, rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Barricade, DecorKinds.Kiosk), Dressed(Corner, DecorKinds.Barricade, DecorKinds.Kiosk) }, 1);
            }
            // What is played: the compound, the roads, and scrub round them one to four tiles deep; the rest shut.
            var S = Math.Max(W, H);
            var noise = ValueNoise(S, 7, rng);
            var core = new byte[W * H];
            foreach (var r in new[] { compound, entry, apron }) foreach (var p in TilesIn(r)) if (c.In(p.X, p.Y)) core[c.I(p.X, p.Y)] = 1;
            var reach = new int[W * H];
            for (var i = 0; i < reach.Length; i++) reach[i] = -1;
            var queue = new List<int>();
            for (var i = 0; i < core.Length; i++)
            {
                if (core[i] == 0) continue;
                reach[i] = 0;
                queue.Add(i);
            }
            for (var k = 0; k < queue.Count; k++)
            {
                var i = queue[k];
                var x = i % W;
                var y = (i - x) / W;
                foreach (var d in Grid.Dirs8)
                {
                    if (!c.In(x + d.X, y + d.Y) || reach[c.I(x + d.X, y + d.Y)] >= 0) continue;
                    reach[c.I(x + d.X, y + d.Y)] = reach[i] + 1;
                    queue.Add(c.I(x + d.X, y + d.Y));
                }
            }
            // Which side of the map a tile lies off, for what shuts it: wood or lots to the north and west, containers or wood
            // to the south, containers or lots past the far side.
            var shut = new[]
            {
                rng.Pick(new[] { FrameKinds.Forest, FrameKinds.Lot }),
                rng.Pick(new[] { FrameKinds.Containers, FrameKinds.Lot }),
                rng.Pick(new[] { FrameKinds.Forest, FrameKinds.Containers }),
                rng.Pick(new[] { FrameKinds.Forest, FrameKinds.Lot }),
            };
            int OffSide(int x, int y)
            {
                var d = new[] { y, W - 1 - x, H - 1 - y, x };
                return Array.IndexOf(d, d.Min());
            }
            var masks = shut.Select((_) => new byte[W * H]).ToList();
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    var i = y * W + x;
                    var edge = Math.Min(Math.Min(x, y), Math.Min(W - 1 - x, H - 1 - y));
                    var road = c.Ground[i] == Road;
                    var scrub = reach[i] <= 1 + Math.Floor(noise[y * S + x] * 4);
                    if (road || (scrub && edge >= 2)) continue;
                    masks[OffSide(x, y)][i] = 1;
                    c.Tiles[i] = TileKinds.Wall;
                    c.Ground[i] = Out;
                    c.Decor[i] = 0;
                    c.Keep[i] = true;
                }
            }
            for (var s = 0; s < masks.Count; s++)
            {
                foreach (var r in TownPlans.RectsOf(masks[s], W, H, 1)) c.Frame.Add(new FramePiece { Kind = shut[s], X = r.X, Y = r.Y, W = r.W, H = r.H });
            }
            // The squad comes in by the road from the north; the bus leaves by the apron's far end.
            c.Squad = V(sx + 2, 4);
            c.SquadSide = 0;
            c.EvacArea = null;
            var way = new PlanRoad { R = apron, Vertical = false };
            c.Exits = new List<Entry> { new Entry { Road = way, Side = 1 } };
            c.Lines = new List<PlanRoad> { way };
            foreach (var p in TilesIn(Rect(c.Squad.X - 2, c.Squad.Y - 2, 5, 5))) c.Reserve(p.X, p.Y);
            // Along the apron, a few parked cars and a lorry or two.
            ParkCars(c, new List<PlanRoad> { new PlanRoad { R = apron, Vertical = false }, new PlanRoad { R = entry, Vertical = true } }, rng, 0.15);
            LorryNear(c, new Pt(apron.X + apron.W * (0.3 + rng.Next() * 0.4), apron.Y + 2), rng, true, 3, 10);
            // Inside, a walk along the fence (two tiles).
            var inner = Rect(fx0 + 3, fy0 + 3, fx1 - fx0 - 5, fy1 - fy0 - 5);
            return (Factory(c, inner, rng, style, role), false);
        }

        // ------------------------------------------------------------------- wilds

        /// <summary>Smooth value noise in [0, 1]; `cell` is the feature size in tiles.</summary>
        private static double[] ValueNoise(int size, int cell, Rng rng)
        {
            var g = (int)Math.Ceiling((double)size / cell) + 2;
            var lattice = new double[g * g];
            for (var k = 0; k < lattice.Length; k++) lattice[k] = rng.Next();
            var ox = rng.Next() * cell;
            var oy = rng.Next() * cell;
            double Smooth(double t) => t * t * (3 - 2 * t);
            var out_ = new double[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var fx = (x + ox) / cell;
                    var fy = (y + oy) / cell;
                    var ix = (int)Math.Floor(fx);
                    var iy = (int)Math.Floor(fy);
                    var tx = Smooth(fx - ix);
                    var ty = Smooth(fy - iy);
                    double Lat(int i, int j) => lattice[(iy + j) * g + ix + i];
                    var top = Lat(0, 0) + (Lat(1, 0) - Lat(0, 0)) * tx;
                    var bottom = Lat(0, 1) + (Lat(1, 1) - Lat(0, 1)) * tx;
                    out_[y * size + x] = top + (bottom - top) * ty;
                }
            }
            return out_;
        }

        /// <summary>How far a country track runs straight in from the edge at either end (docs/MAPAS.md §5.10: the way in, the bus's way out, past the wood).</summary>
        private const int TrackTail = 22;
        /// <summary>The verge each side of a country way in or out, kept open through the trees (countryWays), and how far in from the edge.</summary>
        private const int Mouth = 2;
        private const int MouthDepth = 12;

        /// <summary>How deep the country shuts its edges (docs/MAPAS.md §5.10): mostly a few tiles of wood, here and there deeper.</summary>
        private static readonly IReadOnlyList<((int Lo, int Hi) Range, double W)> CountryDepth = new[]
        {
            ((2, 3), 40.0),
            ((4, 7), 40.0),
            ((8, 11), 20.0),
        };

        /// <summary>
        /// The country's outline (docs/MAPAS.md §5.10): along every side a band of wood, rocks here and there, as deep as its
        /// knots say (COUNTRY_DEPTH, every 8–14 tiles, straight between them); along one side now and then water (a river, a
        /// reservoir) or a ravine, deeper and even, never where the squad comes in or a road goes out. The roads stay open
        /// through it: the ways in. Every tile out of play is a wall on ground OUT, in pieces of the frame by kind.
        /// </summary>
        private static void ShutCountry(Canvas c, Rng rng)
        {
            var W = c.Width;
            var H = c.Height;
            var kinds = new List<string>(); // FrameKinds
            var kindOf = new short[W * H];
            for (var i = 0; i < kindOf.Length; i++) kindOf[i] = -1;
            int Index(string k)
            {
                if (kinds.Contains(k)) return kinds.IndexOf(k);
                kinds.Add(k);
                return kinds.Count - 1;
            }
            int Along(int s) => s % 2 == 0 ? W : H;
            Vec2 At(int s, int u, int d) => s == 0 ? V(u, d) : s == 1 ? V(W - 1 - d, u) : s == 2 ? V(u, H - 1 - d) : V(d, u);
            var roadSides = new HashSet<int>(c.Exits.Select((e) => e.Side));
            if (c.SquadSide != null) roadSides.Add(c.SquadSide.Value);
            var free = new[] { 0, 1, 2, 3 }.Where((s) => !roadSides.Contains(s)).ToList();
            var special = free.Count != 0 && rng.Next() < 0.45 ? rng.Pick(free) : -1;
            var specialKind = rng.Next() < 0.6 ? FrameKinds.Water : FrameKinds.Ravine;
            bool InMouth(Vec2 p) => c.Mouths.Any((m) => InRect(m, p));
            void Close(Vec2 p, int k)
            {
                var i = p.Y * W + p.X;
                if (p.X < 0 || p.Y < 0 || p.X >= W || p.Y >= H || kindOf[i] >= 0 || c.Ground[i] == Road || InMouth(p)) return;
                kindOf[i] = (short)k;
            }
            foreach (var s in new[] { 0, 1, 2, 3 })
            {
                var n = Along(s);
                var knots = new List<(int U, int D)>();
                for (var u = 0; u < n + 14; u += rng.Range(8, 14))
                {
                    var (lo, hi) = s == special ? (5, 9) : Weighted(rng, CountryDepth.Select((o) => (o.Range, o.W)).ToList());
                    knots.Add((u, rng.Range(lo, hi)));
                }
                var k = 0;
                var runEnd = 0;
                var kind = Index(FrameKinds.Forest);
                for (var u = 0; u < n; u++)
                {
                    while (knots[k + 1].U <= u) k++;
                    var (u0, d0) = knots[k];
                    var (u1, d1) = knots[k + 1];
                    var depth = (int)Js.Round(d0 + (double)((d1 - d0) * (u - u0)) / (u1 - u0));
                    if (u >= runEnd)
                    {
                        runEnd = u + rng.Range(8, 16);
                        kind = Index(s == special ? specialKind : rng.Next() < 0.2 ? FrameKinds.Rocks : FrameKinds.Forest);
                    }
                    for (var t = 0; t < depth; t++) Close(At(s, u, t), kind);
                }
            }
            // Nothing played by the edge but the ways in (rules.ts EDGE_BAND: where the dead come in), nothing standing in them.
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    if (Math.Min(Math.Min(x, y), Math.Min(W - 1 - x, H - 1 - y)) >= 2 || kindOf[y * W + x] >= 0 || c.Ground[y * W + x] == Road) continue;
                    if (InMouth(V(x, y))) c.Reserve(x, y);
                    else Close(V(x, y), Index(FrameKinds.Forest));
                }
            }
            for (var k = 0; k < kinds.Count; k++)
            {
                var mask = new byte[W * H];
                for (var i = 0; i < kindOf.Length; i++)
                {
                    if (kindOf[i] != k) continue;
                    mask[i] = 1;
                    c.Tiles[i] = TileKinds.Wall;
                    c.Ground[i] = Out;
                    c.Decor[i] = 0;
                    c.Keep[i] = true;
                }
                foreach (var r in TownPlans.RectsOf(mask, W, H, 1)) c.Frame.Add(new FramePiece { Kind = kinds[k], X = r.X, Y = r.Y, W = r.W, H = r.H });
            }
        }

        /// <summary>
        /// A country map's ways (docs/MAPAS.md §5.10) from its road or track (`line`, `width` wide round it): the squad comes
        /// in by its first end, four tiles in; the bus leaves by its last, on the straight stretch before the edge.
        /// </summary>
        private static void CountryWays(Canvas c, List<Vec2> line, int width, int tail)
        {
            var W = c.Width;
            var H = c.Height;
            var start = line[0];
            var end = line[line.Count - 1];
            // The tiles round a point of the line: from it (a track) or either side of it (a road three wide).
            var lo = width == 3 ? -1 : 0;
            c.SquadSide = start.Y == 0 ? 0 : 3;
            c.Squad = start.Y == 0 ? V(start.X, 4) : V(4, start.Y);
            var side = end.Y == H - 1 ? 2 : 1;
            var vertical = side == 2;
            var r = vertical ? Rect(end.X + lo, H - 1 - tail, width, tail + 1) : Rect(W - 1 - tail, end.Y + lo, tail + 1, width);
            var road = new PlanRoad { R = r, Vertical = vertical };
            c.Exits = new List<Entry> { new Entry { Road = road, Side = side } };
            c.Lines = new List<PlanRoad> { road };
            // Their mouths, the road and a verge of MOUTH each side through the trees: by the edge, room for a horde to come in
            // by (rules.ts chooseDropZone); by the bus, a way on past its nose (avenueEvac).
            var way = start.Y == 0 ? Rect(start.X + lo - Mouth, 0, width + 2 * Mouth, MouthDepth) : Rect(0, start.Y + lo - Mouth, MouthDepth, width + 2 * Mouth);
            c.Mouths = new List<Rect> { way, vertical ? Rect(r.X - Mouth, r.Y, r.W + 2 * Mouth, r.H) : Rect(r.X, r.Y - Mouth, r.W, r.H + 2 * Mouth) };
            c.EvacArea = null;
            foreach (var p in TilesIn(Rect(c.Squad.X - 2, c.Squad.Y - 2, 5, 5))) c.Reserve(p.X, p.Y);
        }

        /// <summary>
        /// Centre line of a dirt track from the squad's side to the far side, bending twice on the way, on a map of W × H; it
        /// runs straight in from the edge, TRACK_TAIL tiles, at either end.
        /// </summary>
        private static List<Vec2> TrackLine(int W, int H, Rng rng)
        {
            var fromTop = rng.Int(2) == 0;
            var start = fromTop ? V(rng.Range(8, 16), 0) : V(0, rng.Range(8, 16));
            var end = rng.Int(2) == 0 ? V(rng.Range((int)Js.Round(W * 0.55), W - 9), H - 1) : V(W - 1, rng.Range((int)Js.Round(H * 0.55), H - 9));
            var s1 = fromTop ? V(start.X, TrackTail) : V(TrackTail, start.Y);
            var e1 = end.Y == H - 1 ? V(end.X, H - 1 - TrackTail) : V(W - 1 - TrackTail, end.Y);
            var len = Dist(s1, e1);
            // -(e1.y - s1.y) as a double: -0 where they are level, as JavaScript has it.
            var nx = -(double)(e1.Y - s1.Y) / len;
            var ny = (double)(e1.X - s1.X) / len;
            var sign = rng.Int(2) == 0 ? 1 : -1;
            var pts = new List<Pt> { s1 };
            var bends = new[] { 0.3, 0.65 };
            for (var k = 0; k < bends.Length; k++)
            {
                var t = bends[k];
                var off = (k == 0 ? sign : -sign) * rng.Range(3, 7);
                pts.Add(new Pt(s1.X + (e1.X - s1.X) * t + nx * off, s1.Y + (e1.Y - s1.Y) * t + ny * off));
            }
            pts.Add(e1);
            var out_ = new List<Vec2>();
            void Step(Vec2 q)
            {
                var last = out_.Count > 0 ? out_[out_.Count - 1] : null;
                if (last == null || last.X != q.X || last.Y != q.Y) out_.Add(q);
            }
            // In from the edge, straight.
            for (var k = 0; k <= TrackTail; k++) Step(fromTop ? V(start.X, k) : V(k, start.Y));
            for (var k = 0; k < pts.Count - 1; k++)
            {
                var p0 = pts[Math.Max(0, k - 1)];
                var p1 = pts[k];
                var p2 = pts[k + 1];
                var p3 = pts[Math.Min(pts.Count - 1, k + 2)];
                for (var t = 0.0; t < 1; t += 0.05)
                {
                    var t2 = t * t;
                    var t3 = t2 * t;
                    double F(double a, double b, double cc, double d) => 0.5 * (2 * b + (-a + cc) * t + (2 * a - 5 * b + 4 * cc - d) * t2 + (-a + 3 * b - 3 * cc + d) * t3);
                    Step(V(Clamp((int)Js.Round(F(p0.X, p1.X, p2.X, p3.X)), 0, W - 1), Clamp((int)Js.Round(F(p0.Y, p1.Y, p2.Y, p3.Y)), 0, H - 1)));
                }
            }
            // Out to the edge, straight.
            for (var k = TrackTail; k >= 0; k--) Step(end.Y == H - 1 ? V(end.X, H - 1 - k) : V(W - 1 - k, end.Y));
            return out_;
        }

        /// <summary>Open, untouched ground over `r` and a margin around it.</summary>
        private static bool RoomFor(Canvas c, Rect r, int margin) => c.Vacant(Rect(r.X - margin, r.Y - margin, r.W + 2 * margin, r.H + 2 * margin));

        /// <summary>Distance (in steps) from every tile to the nearest tile of `from`.</summary>
        private static double[] DistanceField(Canvas c, IEnumerable<int> from)
        {
            var out_ = new double[c.Tiles.Count];
            for (var i = 0; i < out_.Length; i++) out_[i] = double.PositiveInfinity;
            var queue = new List<int>();
            foreach (var i in from)
            {
                out_[i] = 0;
                queue.Add(i);
            }
            for (var k = 0; k < queue.Count; k++)
            {
                var i = queue[k];
                var x = i % c.Width;
                var y = (i - x) / c.Width;
                foreach (var d in Grid.Dirs4)
                {
                    if (!c.In(x + d.X, y + d.Y)) continue;
                    var j = c.I(x + d.X, y + d.Y);
                    if (out_[j] <= out_[i] + 1) continue;
                    out_[j] = out_[i] + 1;
                    queue.Add(j);
                }
            }
            return out_;
        }

        /// <summary>
        /// The best vacant spot within `radius` of `target` for a building `lu` wide
        /// (along its front) and `ld` deep whose front door looks onto the track a few
        /// tiles away (`maxGap` at most).
        /// </summary>
        private static (Rect R, int Front)? SiteNear(Canvas c, Pt target, int lu, int ld, double[] toTrack, Rng rng, int radius = 9, int maxGap = 5)
        {
            var options = new List<(Rect R, int Front, double Key)>();
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    for (var front = 0; front < 4; front++)
                    {
                        var w = front % 2 == 0 ? lu : ld;
                        var h = front % 2 == 0 ? ld : lu;
                        var r = Rect((int)Js.Round(target.X + dx - w / 2.0), (int)Js.Round(target.Y + dy - h / 2.0), w, h);
                        var door = Add(LocalPt(r, front, (int)Math.Floor(lu / 2.0), 0), Grid.Dirs4[front]);
                        if (!c.In(door.X, door.Y)) continue;
                        var gap = toTrack[c.I(door.X, door.Y)];
                        if (gap > maxGap) continue;
                        options.Add((r, front, Js.Hypot(dx, dy) * 0.6 + gap + rng.Next()));
                    }
                }
            }
            Js.Sort(options, (a, b) => a.Key - b.Key);
            foreach (var o in options) if (RoomFor(c, o.R, 1)) return (o.R, o.Front);
            return null;
        }

        /// <summary>A fenced field with furrows and a couple of hay bales, its gate towards `gate`.</summary>
        private static void Field(Canvas c, Rect r, Rng rng, int gate)
        {
            var alongX = r.W >= r.H;
            foreach (var p in TilesIn(Inset(r, 1))) if ((alongX ? p.Y - r.Y : p.X - r.X) % 2 == 0) c.Ground[c.I(p.X, p.Y)] = Road;
            var gates = new List<int> { gate };
            if (rng.Next() < 0.5) gates.Add(Opposite(gate));
            for (var s = 0; s < 4; s++)
            {
                var n = SideLen(r, s);
                var g = (int)Math.Floor(n / 2.0) + rng.Range(-1, 0);
                var gated = gates.Contains(s);
                for (var u = 0; u < n; u++)
                {
                    if (gated && (u == g || u == g + 1)) continue;
                    Put(c, LocalPt(r, s, u, 0), TileKinds.Low, new Look(gated && (u == g - 1 || u == g + 2) ? DecorKinds.GatePost : DecorKinds.Fence, s));
                }
            }
            if (r.W * r.H >= 80) GroupNear(c, Centre(r), rng, new IReadOnlyList<Part>[] { new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Hay)), P(1, 0, TileKinds.Low, new Look(DecorKinds.Hay)) } }, 2, 0, 6);
            foreach (var p in TilesIn(Inset(r, 1))) c.Reserve(p.X, p.Y);
        }

        /// <summary>A hill one storey high whose outline follows a blob, climbed by ladders.</summary>
        private static bool Hill(Canvas c, Vec2 at, Rng rng)
        {
            var count = rng.Range(3, 4);
            var circles = new List<(double X, double Y, double R)>();
            for (var k = 0; k < count; k++)
            {
                var cx = at.X + (k != 0 ? rng.Range(-3, 3) : 0);
                var cy = at.Y + (k != 0 ? rng.Range(-3, 3) : 0);
                circles.Add((cx, cy, 1.8 + rng.Next() * 1.8));
            }
            bool InBlob(int x, int y) => circles.Any((q) => Js.Hypot(x - q.X, y - q.Y) <= q.R);
            var blob = TilesIn(Rect(at.X - 7, at.Y - 7, 15, 15)).Where((p) => InBlob(p.X, p.Y)).ToList();
            // No spurs one tile wide.
            bool Has(List<Vec2> set, int x, int y) => set.Any((q) => q.X == x && q.Y == y);
            var whole = blob;
            blob = whole.Where((p) => Grid.Dirs4.Count((d) => Has(whole, p.X + d.X, p.Y + d.Y)) >= 2).ToList();
            if (blob.Count < 14) return false;
            foreach (var p in blob) foreach (var d in Grid.Dirs8) if (!c.Free(p.X + d.X, p.Y + d.Y) || c.GroundAt(p.X + d.X, p.Y + d.Y) != Open) return false;
            foreach (var p in blob) c.Force(p.X, p.Y, TileKinds.Floor, 1);
            // Ladders on the side facing the squad and on the far side.
            foreach (var target in new[] { V(0, 0), V(c.Width, c.Height) })
            {
                var feet = new List<Vec2>();
                foreach (var p in blob)
                {
                    foreach (var d in Grid.Dirs4)
                    {
                        var f = Add(p, d);
                        var beyond = Add(f, d);
                        if (!Has(blob, f.X, f.Y) && c.Free(f.X, f.Y) && c.Free(beyond.X, beyond.Y)) feet.Add(f);
                    }
                }
                var foot = Best(feet, (q) => Dist(q, target) + rng.Next() * 3);
                if (foot == null) continue;
                c.Force(foot.X, foot.Y, TileKinds.Ladder);
                c.Reserve(foot.X, foot.Y);
                foreach (var d in Grid.Dirs4) c.Reserve(foot.X + d.X, foot.Y + d.Y);
            }
            GroupNear(c, at, rng, Outcrops, 1, 1, 8);
            return true;
        }

        /// <summary>
        /// Wilds: a dirt track from the squad's corner to the far side, a farmstead or
        /// ruins beside it near the objective and a cabin halfway, fenced fields, hills,
        /// groves with clearings between them and rock outcrops.
        /// </summary>
        private static Rect Wilds(Canvas c, Rng rng, Style style, string role)
        {
            var W = c.Width;
            var H = c.Height;
            var far = new Pt(W * 0.76, H * 0.76);
            var track = TrackLine(W, H, rng);
            var onTrack = new List<int>();
            foreach (var p in track)
            {
                foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                {
                    var x = p.X + dx;
                    var y = p.Y + dy;
                    if (!c.In(x, y) || c.Keep[c.I(x, y)]) continue;
                    c.Ground[c.I(x, y)] = Road;
                    c.Reserve(x, y);
                    onTrack.Add(c.I(x, y));
                }
            }
            // In by the track and out by it, the country shut round (docs/MAPAS.md §5.10).
            CountryWays(c, track, 2, TrackTail);
            ShutCountry(c, rng);
            var toTrack = DistanceField(c, onTrack);
            var near = Best(track, (q) => Dist(q, far))!;
            var target = new Pt((near.X + far.X) / 2, (near.Y + far.Y) / 2);
            Rect? objective = null;
            Rect? farm = null;
            if (role == Roles.Indoor)
            {
                // Farmstead: the farmhouse and the barn side by side, both onto the track, a farmyard between.
                var house = SiteNear(c, target, style.House, style.HouseDepth, toTrack, rng);
                if (house != null)
                {
                    var hs = house.Value;
                    objective = MakeBuilding(c, hs.R, rng, Plans.Rooms, hs.Front, style, Furnishes.Home).Outer;
                    var along = Grid.Dirs4[(hs.Front + 1) % 4];
                    var bw = Math.Min(style.Hall, 14);
                    var bd = Math.Min(style.Hall - 2, 12);
                    var shift = (int)Math.Ceiling(style.House / 2.0 + 3 + bw / 2.0);
                    var m = Centre(hs.R);
                    var behind = Grid.Dirs4[Opposite(hs.Front)];
                    var spots = Shuffle(new List<int> { 1, -1 }, rng).Select((k) => V(along.X * shift * k, along.Y * shift * k)).ToList();
                    spots.Add(V(behind.X * (shift + 1), behind.Y * (shift + 1)));
                    foreach (var off in spots)
                    {
                        var barn = SiteNear(c, Add(m, off), bw, bd, toTrack, rng, 6) ?? SiteNear(c, Add(m, off), 12, 10, toTrack, rng, 6);
                        if (barn == null) continue;
                        MakeBuilding(c, barn.Value.R, rng, Plans.Hall, barn.Value.Front, style, Furnishes.Barn);
                        // Hay in the farmyard: a stack and a few bales.
                        GroupNear(c, new Pt(m.X + off.X / 2.0, m.Y + off.Y / 2.0), rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Hay), Dressed(Barricade, DecorKinds.Hay) }, 2);
                        break;
                    }
                    farm = objective;
                }
            }
            else
            {
                // Ruins of a farmhouse in a clearing, the objective in or around them.
                var site = SiteNear(c, target, style.House + 2, style.HouseDepth + 1, toTrack, rng);
                if (site != null)
                {
                    var st = site.Value;
                    Ruin(c, MakeBuilding(c, st.R, rng, Plans.Rooms, st.Front, style, Furnishes.Bare), rng);
                    objective = Rect(st.R.X - 3, st.R.Y - 3, st.R.W + 6, st.R.H + 6);
                }
            }
            // Halfway along: a cabin, sometimes in ruins.
            var halfway = track[(int)Js.Round(track.IndexOf(near) * 0.45)];
            var cabinSite = SiteNear(c, halfway, style.House, style.HouseDepth, toTrack, rng, 7);
            if (cabinSite != null)
            {
                var cabin = MakeBuilding(c, cabinSite.Value.R, rng, Plans.Rooms, cabinSite.Value.Front, style, Furnishes.Home);
                if (rng.Next() < 0.35) Ruin(c, cabin, rng);
                farm ??= cabin.Outer;
            }
            // Fields near the farm, gates towards it, and one more out in the open.
            var fieldAt = new List<Pt> { farm != null ? Centre(farm) : far, new Pt(rng.Range(14, W - 14), rng.Range(14, H - 14)) };
            foreach (var at in fieldAt)
            {
                var w = rng.Range(10, 13);
                var h = rng.Range(7, 9);
                var options = TilesIn(Rect((int)Js.Round(at.X) - 14, (int)Js.Round(at.Y) - 14, 29, 29))
                    .Select((q) => (R: Rect(q.X - (int)Math.Floor(w / 2.0), q.Y - (int)Math.Floor(h / 2.0), w, h), Key: Dist(q, at) + rng.Next() * 4))
                    .ToList()
                    .Where((o) => Dist(Centre(o.R), at) >= 8)
                    .ToList();
                Js.Sort(options, (a, b) => a.Key - b.Key);
                Rect? spot = null;
                foreach (var o in options)
                {
                    if (!RoomFor(c, o.R, 1)) continue;
                    spot = o.R;
                    break;
                }
                if (spot == null) continue;
                var m = Centre(spot);
                var gate = Math.Abs(at.X - m.X) > Math.Abs(at.Y - m.Y) ? (at.X > m.X ? 1 : 3) : at.Y > m.Y ? 2 : 0;
                Field(c, spot, rng, gate);
            }
            Woods(c, rng, objective != null ? Centre(objective) : far);
            return objective ?? Rect((int)Js.Round(far.X) - 6, (int)Js.Round(far.Y) - 6, 12, 12);
        }

        /// <summary>A wrecked lorry (2 × 6 where it fits, else 2 × 4: decor.ts FOOTPRINT), drawn nose north.</summary>
        private static readonly IReadOnlyList<Part> Truck = Dressed(TilesIn(Rect(0, 0, Decor.Footprint.Truck[0], Decor.Footprint.Truck[1])).Select((p) => P(p.X, p.Y, TileKinds.High)).ToList(), DecorKinds.Truck);
        private static readonly IReadOnlyList<Part> TruckShort = Dressed(TilesIn(Rect(0, 0, Decor.Footprint.TruckShort[0], Decor.Footprint.TruckShort[1])).Select((p) => P(p.X, p.Y, TileKinds.High)).ToList(), DecorKinds.Truck);

        /// <summary>A lorry near `at` (groupNear): a long one where it fits, else one as long as they were.</summary>
        private static bool LorryNear(Canvas c, Pt at, Rng rng, bool turned, int spread, int tries) =>
            GroupNear(c, at, rng, new IReadOnlyList<Part>[] { turned ? Turn(Truck, 1) : Truck }, spread, 0, tries) || GroupNear(c, at, rng, new IReadOnlyList<Part>[] { turned ? Turn(TruckShort, 1) : TruckShort }, spread, 0, tries);
        private static readonly IReadOnlyList<IReadOnlyList<Part>> LogPiles = new IReadOnlyList<Part>[]
        {
            Dressed(new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low) }, DecorKinds.LogPile),
            Dressed(TilesIn(Rect(0, 0, 2, 2)).Select((p) => P(p.X, p.Y, TileKinds.Low)).ToList(), DecorKinds.LogPile),
        };
        private static readonly IReadOnlyList<Part> WaterTower = Dressed(TilesIn(Rect(0, 0, 2, 2)).Select((p) => P(p.X, p.Y, TileKinds.High)).ToList(), DecorKinds.WaterTower);
        /// <summary>A shipping container (2 × 5), drawn doors north.</summary>
        private static readonly IReadOnlyList<Part> Container = Dressed(TilesIn(Rect(0, 0, 2, 5)).Select((p) => P(p.X, p.Y, TileKinds.High)).ToList(), DecorKinds.Container);
        /// <summary>A fallen wire fence, five tiles in a row.</summary>
        private static readonly IReadOnlyList<Part> Wire = Dressed(TilesIn(Rect(0, 0, 5, 1)).Select((p) => P(p.X, p.Y, TileKinds.Low)).ToList(), DecorKinds.Wire);
        /// <summary>A broken wooden fence: four posts and a gap.</summary>
        private static readonly IReadOnlyList<Part> BrokenFence = Dressed(new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low), P(3, 0, TileKinds.Low), P(4, 0, TileKinds.Low) }, DecorKinds.Fence);

        /// <summary>
        /// The service area's road: straight stretches and right-angle bends, from an
        /// edge near the squad's corner (like trackLine) to one of the far edges, with
        /// one or two bends. Returns its middle line, tile by tile.
        /// </summary>
        private static List<Vec2> RoadLine(int W, int H, Rng rng)
        {
            var fromTop = rng.Int(2) == 0;
            var start = fromTop ? V(rng.Range(8, 16), 0) : V(0, rng.Range(8, 16));
            var toBottom = rng.Int(2) == 0;
            var end = toBottom ? V(rng.Range((int)Js.Round(W * 0.55), W - 9), H - 1) : V(W - 1, rng.Range((int)Js.Round(H * 0.55), H - 9));
            var turn = rng.Range((int)Js.Round((fromTop ? H : W) * 0.3), (int)Js.Round((fromTop ? H : W) * 0.55));
            // Down from the top (or right from the left) to the bend, then across, then on to the end.
            var corners =
                fromTop
                    ? toBottom
                        ? new List<Vec2> { start, V(start.X, turn), V(end.X, turn), end }
                        : new List<Vec2> { start, V(start.X, end.Y), end }
                    : toBottom
                        ? new List<Vec2> { start, V(end.X, start.Y), end }
                        : new List<Vec2> { start, V(turn, start.Y), V(turn, end.Y), end };
            var out_ = new List<Vec2> { V(start.X, start.Y) };
            for (var k = 1; k < corners.Count; k++)
            {
                var to = corners[k];
                var last = out_[out_.Count - 1];
                var p = V(last.X, last.Y);
                while (p.X != to.X || p.Y != to.Y)
                {
                    p.X += Js.Sign(to.X - p.X);
                    if (p.X == to.X) p.Y += Js.Sign(to.Y - p.Y);
                    out_.Add(V(p.X, p.Y));
                }
            }
            return out_;
        }

        /// <summary>
        /// The service area on a forest road (playtest 2026-10-14, #25, after Synty's
        /// Woodland Apocalypse): by a road through the pines, a filling station, a
        /// diner and a motel, the water tower, a sawmill with its log piles and lorries,
        /// the woodcutter's statue and the big signs. The cover is what such a place
        /// has: lorries, log piles, pumps, fences. The objective is the diner (indoors)
        /// or the forecourt (in the open).
        /// </summary>
        private static Rect Roadside(Canvas c, Rng rng, Style style, string role)
        {
            var track = RoadLine(c.Width, c.Height, rng);
            // A real road, three tiles wide.
            var onTrack = new List<int>();
            foreach (var p in track)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var x = p.X + dx;
                        var y = p.Y + dy;
                        if (!c.In(x, y) || c.Keep[c.I(x, y)] || c.Ground[c.I(x, y)] == Road) continue;
                        c.Ground[c.I(x, y)] = Road;
                        c.Reserve(x, y);
                        onTrack.Add(c.I(x, y));
                    }
                }
            }
            // In by the road and out by its last stretch, the country shut round (docs/MAPAS.md §5.10).
            var last = track[track.Count - 1];
            var straight = 0;
            while (straight + 1 < track.Count && (track[track.Count - 2 - straight].X == last.X || track[track.Count - 2 - straight].Y == last.Y)) straight++;
            CountryWays(c, track, 3, Math.Min(straight, 26));
            ShutCountry(c, rng);
            var toTrack = DistanceField(c, onTrack);
            Vec2 At(double t) => track[Math.Min(track.Count - 1, (int)Js.Round((track.Count - 1) * t))];
            // The first place along the road, in the order of `ts`, with room for it.
            (Rect R, int Front)? FindLot(double[] ts, int lu, int ld, int radius = 10, int maxGap = 5)
            {
                foreach (var t in ts)
                {
                    var found = SiteNear(c, At(t), lu, ld, toTrack, rng, radius, maxGap);
                    if (found != null) return found;
                }
                return null;
            }
            // Forecourts, yards and car parks are asphalt.
            void Pave(Rect r)
            {
                foreach (var q in TilesIn(r)) if (c.In(q.X, q.Y) && c.GroundAt(q.X, q.Y) == Open) c.Ground[c.I(q.X, q.Y)] = Yard;
            }
            Rect? objective = null;
            // The filling station, its forecourt onto the road, and the woodcutter by it.
            var station = FindLot(new[] { 0.62, 0.55, 0.7 }, 15, 11);
            if (station != null)
            {
                var st = station.Value;
                GasStation(c, st.R, st.Front, rng, style);
                Pave(st.R);
                if (role == Roles.Open) objective = st.R;
                GroupNear(c, Add(Centre(st.R), new Pt(rng.Range(-8, 8), rng.Range(-8, 8))), rng, new IReadOnlyList<Part>[] { new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Statue)) } }, 2, 0, 10);
            }
            // The diner beside it: one storey, the counter and the tables.
            var dinerSite = FindLot(new[] { 0.72, 0.66, 0.8 }, 9, 7);
            if (dinerSite != null)
            {
                var diner = MakeBuilding(c, dinerSite.Value.R, rng, Plans.Shop, dinerSite.Value.Front, style, Furnishes.Shop);
                diner.Storeys = 1;
                if (role == Roles.Indoor) objective = diner.Outer;
                GroupNear(c, Centre(dinerSite.Value.R), rng, new IReadOnlyList<Part>[] { WaterTower }, 9, 0, 30);
            }
            // The sawmill on the near stretch: the shed at the back, a yard of logs and lorries onto the road.
            var mill = FindLot(new[] { 0.42, 0.5, 0.33, 0.22 }, 13, 16) ?? FindLot(new[] { 0.42, 0.3, 0.55 }, 13, 13);
            if (mill != null)
            {
                var ml = mill.Value;
                var d = DepthOf(ml.R, ml.Front);
                var hd = d >= 16 ? 10 : 9;
                MakeBuilding(c, Local(ml.R, ml.Front, 0, d - hd, 13, hd), rng, Plans.Hall, ml.Front, style, Furnishes.Store);
                var yard = Local(ml.R, ml.Front, 0, 0, 13, d - hd);
                Pave(yard);
                for (var k = 0; k < 3; k++) GroupNear(c, LocalPt(ml.R, ml.Front, rng.Range(1, 11), rng.Range(1, Math.Max(1, d - hd - 2))), rng, LogPiles, 2, 0, 8);
                LorryNear(c, Centre(yard), rng, false, 3, 12);
            }
            // The motel further on, its car park onto the road.
            // Off the road down a drive if there's no room on it.
            var motelSite = FindLot(new[] { 0.84, 0.92, 0.76, 0.5, 0.3, 0.15 }, 15, 13, 12) ?? FindLot(new[] { 0.84, 0.92, 0.6, 0.4, 0.2 }, 13, 12, 14, 9);
            if (motelSite != null)
            {
                var wings = Motel(c, motelSite.Value.R, motelSite.Value.Front, rng);
                Pave(motelSite.Value.R);
                if (role == Roles.Indoor) objective ??= wings.Count > 0 ? wings[0].Outer : null;
            }
            // Behind the place, a broken fence and a fallen one.
            var behind = new[] { motelSite, dinerSite, station, mill }.FirstOrDefault((l) => l != null);
            if (behind != null)
            {
                var bh = behind.Value;
                var back = LocalPt(bh.R, bh.Front, (int)Math.Floor(SideLen(bh.R, bh.Front) / 2.0), DepthOf(bh.R, bh.Front) + 3);
                GroupNear(c, back, rng, new IReadOnlyList<Part>[] { BrokenFence }, 4, 0, 16);
                GroupNear(c, back, rng, new IReadOnlyList<Part>[] { Wire }, 6, 0, 16);
            }
            // Lorries left on the shoulder, and the big signs by the road.
            foreach (var t in new[] { 0.3, 0.78 }) LorryNear(c, At(t), rng, false, 4, 16);
            foreach (var t in new[] { 0.22, 0.55, 0.92 })
            {
                var near = TilesIn(Rect(At(t).X - 4, At(t).Y - 4, 9, 9)).Where((q) => c.In(q.X, q.Y) && toTrack[c.I(q.X, q.Y)] == 2 && c.Free(q.X, q.Y)).ToList();
                var p = near.Count != 0 ? rng.Pick(near) : null;
                if (p == null) continue;
                var towards = FindIndex(Grid.Dirs4, (d) => c.In(p.X + d.X, p.Y + d.Y) && toTrack[c.I(p.X + d.X, p.Y + d.Y)] < 2);
                Group(c, p, new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Sign, Math.Max(0, towards))) });
            }
            // A cabin on the way, sometimes in ruins.
            var cabinSite = FindLot(new[] { 0.25, 0.15 }, style.House, style.HouseDepth);
            if (cabinSite != null)
            {
                var cabin = MakeBuilding(c, cabinSite.Value.R, rng, Plans.Rooms, cabinSite.Value.Front, style, Furnishes.Home);
                if (rng.Next() < 0.35) Ruin(c, cabin, rng);
            }
            var centreAt = objective != null ? Centre(objective) : (Pt)At(0.7);
            Woods(c, rng, centreAt, (1, 4, 3));
            return objective ?? Rect((int)Js.Round(centreAt.X) - 6, (int)Js.Round(centreAt.Y) - 6, 12, 12);
        }

        /// <summary>
        /// The country around: hills, groves where the noise is high (kept away from
        /// the squad, the evacuation and `objCentre`), rock outcrops and fallen logs.
        /// </summary>
        private static void Woods(Canvas c, Rng rng, Pt objCentre, (int Hills, int Outcrops, int Logs)? most = null)
        {
            var limit = most ?? (3, 8, 4);
            var W = c.Width;
            var H = c.Height;
            var S = Math.Max(W, H);
            // Hills.
            var hills = 0;
            for (var k = 0; k < 40 && hills < limit.Hills; k++) if (Hill(c, V(rng.Range(8, W - 9), rng.Range(8, H - 9)), rng)) hills++;
            // Groves where the noise is high, kept away from the squad, the evacuation and the objective.
            var big = ValueNoise(S, 10, rng);
            var small = ValueNoise(S, 4, rng);
            // Where the evacuation goes: the north-east corner, or a track's far end.
            var exit = c.Exits.Count != 0 ? Centre(c.Exits[0].Road.R) : new Pt(W - 1, 0);
            double Value(int x, int y)
            {
                var v = 0.75 * big[y * S + x] + 0.25 * small[y * S + x];
                v -= 0.5 * Math.Max(0, 1 - Dist(new Pt(x, y), c.Squad) / 13);
                v -= 0.5 * Math.Max(0, 1 - Dist(new Pt(x, y), exit) / 10);
                v -= 0.35 * Math.Max(0, 1 - Dist(new Pt(x, y), objCentre) / 9);
                return v;
            }
            var land = TilesIn(Rect(0, 0, W, H)).Where((q) => c.GroundAt(q.X, q.Y) == Open && c.Get(q.X, q.Y) == TileKinds.Floor).ToList();
            var sorted = land.Select((q) => Value(q.X, q.Y)).ToList();
            Js.Sort(sorted, (a, b) => a - b);
            var T = Js.At(sorted, (int)Math.Floor(sorted.Count * 0.7), 0.6);
            foreach (var p in land)
            {
                var level = c.ElevAt(p.X, p.Y);
                if (!c.Free(p.X, p.Y, level)) continue;
                var v = Value(p.X, p.Y);
                var roll = rng.Next();
                if (v > T)
                {
                    // Dense trees with undergrowth, thicker towards the heart of the grove.
                    if (roll < 0.42 + Math.Min(0.3, (v - T) * 4))
                    {
                        c.Force(p.X, p.Y, TileKinds.High);
                        c.Dress(p, DecorKinds.Tree);
                    }
                    else if (roll > 0.85)
                    {
                        c.Force(p.X, p.Y, TileKinds.Low);
                        c.Dress(p, DecorKinds.Bush);
                    }
                }
                else if (v > T - 0.025 && roll < 0.15)
                {
                    c.Force(p.X, p.Y, TileKinds.High);
                    c.Dress(p, DecorKinds.Tree);
                }
            }
            // Rock outcrops and fallen logs out in the open.
            var outcrops = new List<Vec2>();
            for (var k = 0; k < 160 && outcrops.Count < limit.Outcrops; k++)
            {
                var p = V(rng.Range(2, W - 3), rng.Range(2, H - 3));
                if (Value(p.X, p.Y) > T - 0.06 || outcrops.Any((o) => Dist(o, p) < 7) || Dist(p, c.Squad) < 6) continue;
                if (Group(c, p, Turn(rng.Pick(Outcrops), rng.Int(4)))) outcrops.Add(p);
            }
            for (int k = 0, logs = 0; k < 80 && logs < limit.Logs; k++)
            {
                var p = V(rng.Range(2, W - 3), rng.Range(2, H - 3));
                var v = Value(p.X, p.Y);
                if (v < T - 0.1 || v > T - 0.02) continue;
                if (Group(c, p, Turn(Dressed(rng.Pick(new IReadOnlyList<Part>[] { Barricade, new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low) } }), DecorKinds.Log), rng.Int(4)))) logs++;
            }
        }

        // ------------------------------------------------------------ connectivity

        /// <summary>What may be cleared to open a dead end outdoors: one-tile things that grow or lie about, never a car, the bus or a wall.</summary>
        private static readonly HashSet<string> Soft = new HashSet<string>
        {
            DecorKinds.Tree, DecorKinds.Bush, DecorKinds.Rock, DecorKinds.StreetTree, DecorKinds.Bollard, DecorKinds.Planter, DecorKinds.Bin, DecorKinds.Fence,
            DecorKinds.Wire, DecorKinds.Crates, DecorKinds.Barricade, DecorKinds.Pallets, DecorKinds.Drums, DecorKinds.Rubble,
        };

        /// <summary>
        /// Opens the dead ends out of doors (playtest 16/10 #20: «muchos caminos
        /// acaban en callejones, incluso entre árboles»): a tile of open ground with
        /// a single way in loses the tree, bush or crate that closes it, straight
        /// ahead first, if past it the way goes on. Where nothing lies past it (a
        /// notch in a wood), the notch grows over instead, tile by tile, with what
        /// closes it; and so do the little holes in a wood nobody could reach, before
        /// connect() carves a lane to them that would lead nowhere. Walls and what
        /// stands over several tiles stay: an alley between two buildings is still
        /// one. The ways in and out are the movement rules' (a drop, a ladder).
        /// </summary>
        private static void OpenDeadEnds(Canvas c, Vec2 start)
        {
            var s = c.AsState();
            bool Walk(int x, int y, int e) => c.In(x, y) && Grid.IsWalkable(c.Get(x, y)) && c.ElevAt(x, y) == e;
            // A step either way (a drop goes one way only); stepCost looks at the tile it goes to, so the other must be walkable too.
            bool Linked(int x, int y, Vec2 d) =>
                c.In(x + d.X, y + d.Y) && Grid.IsWalkable(c.Get(x + d.X, y + d.Y)) && (Paths.StepCost(s, x, y, d.X, d.Y) != null || Paths.StepCost(s, x + d.X, y + d.Y, -d.X, -d.Y) != null);
            bool IsSoft(int x, int y)
            {
                if (!c.In(x, y) || c.Keep[c.I(x, y)] || c.GroundAt(x, y) == Indoor) return false;
                var kind = c.Get(x, y);
                if (kind != TileKinds.Low && kind != TileKinds.High) return false;
                // Unnamed low clutter is soft too; an unnamed tall block may be part of a lorry or a tower.
                var look = Decor.DecorOf(c.Decor[c.I(x, y)]);
                return look != null ? Soft.Contains(look.Kind) : kind == TileKinds.Low;
            }
            var loot = new HashSet<string> { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood };
            bool KeepsOpen(int x, int y) =>
                c.Keep[c.I(x, y)] ||
                c.GroundAt(x, y) != Open ||
                Grid.Dirs8.Any((d) =>
                {
                    var nx = x + d.X;
                    var ny = y + d.Y;
                    if (!c.In(nx, ny)) return false;
                    var kind = c.Get(nx, ny);
                    var look = Decor.DecorOf(c.Decor[c.I(nx, ny)]);
                    return kind == TileKinds.Door || kind == TileKinds.DoorOpen || kind == TileKinds.Window || kind == TileKinds.WindowBroken || kind == TileKinds.Ladder || kind == TileKinds.Console || (look != null && loot.Contains(look.Kind));
                });
            // Grows `p` over with what closes it, if only wood, bush and the like, the map's edge or a drop do.
            bool GrowOver(int x, int y, List<Vec2> ways)
            {
                var sides = Grid.Dirs4.Where((d) => !ways.Contains(d)).Select((d) => V(x + d.X, y + d.Y)).ToList();
                var growth = sides.Where((p) => IsSoft(p.X, p.Y)).ToList();
                bool Drop(Vec2 p) => !c.In(p.X, p.Y) || Grid.IsWalkable(c.Get(p.X, p.Y));
                if (growth.Count == 0 || growth.Count + sides.Count(Drop) < sides.Count) return false;
                var like = growth[0];
                c.Force(x, y, c.Get(like.X, like.Y));
                c.Decor[c.I(x, y)] = c.Decor[c.I(like.X, like.Y)];
                return true;
            }
            // Holes in a wood out of everyone's reach, a few tiles at most: grown over, not joined.
            var reach = Flood(c, start);
            var done = new HashSet<int>();
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                if (reach.Has(i) || done.Contains(i) || !Grid.IsWalkable(c.Tiles[i])) continue;
                var x = i % c.Width;
                var hole = new List<int>(Flood(c, V(x, (i - x) / c.Width), (j) => !reach.Has(j)));
                foreach (var j in hole) done.Add(j);
                var tiles = hole.Select((j) => V(j % c.Width, (int)Math.Floor((double)j / c.Width))).ToList();
                if (hole.Count > HoleMax || tiles.Any((p) => c.Get(p.X, p.Y) != TileKinds.Floor || KeepsOpen(p.X, p.Y))) continue;
                for (var pass = 0; pass < HoleMax; pass++)
                {
                    foreach (var p in tiles) if (c.Get(p.X, p.Y) == TileKinds.Floor) GrowOver(p.X, p.Y, Grid.Dirs4.Where((d) => Linked(p.X, p.Y, d)).ToList());
                }
            }
            for (var pass = 0; pass < 4; pass++)
            {
                var opened = 0;
                for (var y = 0; y < c.Height; y++)
                {
                    for (var x = 0; x < c.Width; x++)
                    {
                        if (c.Get(x, y) != TileKinds.Floor || c.GroundAt(x, y) == Indoor) continue;
                        var e = c.ElevAt(x, y);
                        var ways = Grid.Dirs4.Where((d) => Linked(x, y, d)).ToList();
                        if (ways.Count != 1) continue;
                        var back = ways[0];
                        var ahead = Grid.Dirs4.First((d) => d.X == -back.X && d.Y == -back.Y);
                        var order = new List<Vec2> { ahead };
                        order.AddRange(Grid.Dirs4.Where((o) => o != back && o != ahead));
                        foreach (var d in order)
                        {
                            var q = V(x + d.X, y + d.Y);
                            if (!IsSoft(q.X, q.Y) || c.ElevAt(q.X, q.Y) != e) continue;
                            if (!Grid.Dirs4.Any((o) => !(q.X + o.X == x && q.Y + o.Y == y) && Walk(q.X + o.X, q.Y + o.Y, e))) continue;
                            c.Force(q.X, q.Y, TileKinds.Floor);
                            c.Decor[c.I(q.X, q.Y)] = 0;
                            opened++;
                            break;
                        }
                    }
                }
                if (opened == 0) break;
            }
            // What couldn't be opened, being only wood or bush all round, grows over (nothing to reach there).
            for (var pass = 0; pass < 12; pass++)
            {
                var grown = 0;
                for (var y = 0; y < c.Height; y++)
                {
                    for (var x = 0; x < c.Width; x++)
                    {
                        if (c.Get(x, y) != TileKinds.Floor || KeepsOpen(x, y)) continue;
                        var ways = Grid.Dirs4.Where((d) => Linked(x, y, d)).ToList();
                        if (ways.Count == 1 && GrowOver(x, y, ways)) grown++;
                    }
                }
                if (grown == 0) return;
            }
        }
    }
}
