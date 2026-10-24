#nullable enable
// mapgen.ts in C#, its part on towns: the city of districts (city), a town on a plan (town: its plots, their buildings
// and back gardens, a park, the walk to the objective) and the industrial estate (estate).
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>`{ ...map }`: a copy of a map's style, for a town to change (organic, industrial).</summary>
        private static Style CopyStyleTowns(Style map)
        {
            return new Style
            {
                House = map.House,
                HouseDepth = map.HouseDepth,
                Gap = map.Gap,
                Office = map.Office,
                OfficeDepth = map.OfficeDepth,
                Hall = map.Hall,
                WindowEvery = map.WindowEvery,
                Furniture = map.Furniture,
                Organic = map.Organic,
                Industrial = map.Industrial,
            };
        }

        /// <summary>
        /// City: blocks of houses, shops, offices, flats and warehouses between
        /// streets, a square or a car park here and there. Half the towns are laid out
        /// on a grid; the other half are old towns, with streets that bend and run on
        /// a slant, smaller and uneven blocks, houses that don't line up and small
        /// squares on the corners.
        /// </summary>
        private static Rect City(Canvas c, Rng rng, Style map, string role, bool clinic = false, bool hospital = false, bool ward = false)
        {
            var S = c.Width;
            var organic = rng.Next() < 0.5 && !map.Industrial && !hospital;
            var style = CopyStyleTowns(map);
            style.Organic = organic;
            // Small blocks: an avenue, streets and lanes two tiles wide (on a lane the houses stand right on it).
            var plan = District(
                Rect(0, 0, S, S),
                rng,
                hospital
                    ? new SplitRules { MinBlock = 19, MaxBlock = 26, Widths = new[] { new[] { 3, 4 }, new[] { 3, 3 }, new[] { 2, 3 } }, Jog = 0.1, DeadEnd = 0.1, Stop = new double[] { 0, 0.2, 0.7, 1 } }
                    : organic
                    ? new SplitRules { MinBlock = 8, MaxBlock = 18, Widths = new[] { new[] { 3, 4 }, new[] { 2, 3 }, new[] { 2, 3 } }, Jog = 0.3, DeadEnd = 0.35, Stop = new double[] { 0, 0.15, 0.55, 0.9 }, Bend = new double[] { 1, 0.6, 0.4 }, Bends = new[] { 4, 3, 2 } }
                    : map.Industrial
                        ? // An industrial estate: big blocks and wide streets for the lorries.
                          new SplitRules { MinBlock = 13, MaxBlock = 24, Widths = new[] { new[] { 4, 4 }, new[] { 3, 4 }, new[] { 3, 3 } }, Jog = 0.2, DeadEnd = 0.2, Stop = new double[] { 0, 0.3, 0.75, 0.95 } }
                        : new SplitRules { MinBlock = 9, MaxBlock = 20, Widths = new[] { new[] { 4, 4 }, new[] { 3, 3 }, new[] { 2, 2 } }, Jog = 0.35, DeadEnd = 0.3, Stop = new double[] { 0, 0.2, 0.6, 0.9 } }
            );
            foreach (var road in plan.Roads) c.Paint(road.R, Road);
            var far = new Pt(S * 0.76, S * 0.76);
            var spawnBlock = plan.Blocks.Find((b) => InRect(b, V(0, 0)))!;
            // The objective's block: the one nearest the far corner, a big one (an office fits) if it is nearly as far.
            var objBlock =
                Best(
                    plan.Blocks.Where((b) => b != spawnBlock && Math.Min(b.W, b.H) >= (hospital ? 19 : ward ? 11 : 9)).ToList(),
                    (b) => Dist(Centre(b), far) - (Math.Min(b.W, b.H) >= (role == Roles.Indoor ? 12 : 11) ? 3 : 0)
                ) ?? Best(plan.Blocks, (b) => (b == spawnBlock ? double.PositiveInfinity : Dist(Centre(b), far)))!;
            var objective = objBlock;
            var used = new JsMap<string, int>();
            var squares = 0;
            foreach (var block in plan.Blocks)
            {
                var roads = new[] { 0, 1, 2, 3 }.Select((s) => FacesRoad(c, block, s)).ToArray();
                var (lot, walks) = TownLot(c, block, roads, organic);
                var lots = new List<Lot>();
                if (block == spawnBlock)
                {
                    // The squad's corner: an open square, with buildings round it if the block is big (on a small block, a narrower square).
                    var corner = CornerLots(new Lot { R = lot, Roads = roads }, 0, lot.W >= 16 || lot.H >= 16 ? 8 : 6);
                    if (style.Industrial) Parking(c, corner.Square, rng, 2, false);
                    else SquadSquare(c, corner.Square, rng);
                    lots.AddRange(corner.Lots);
                }
                else if (block != objBlock && InRect(block, V(S - 1, 0)))
                {
                    // The other corner on the squad's side keeps a yard for the evac zone (generateLayout carves it there), a few crates in it.
                    var tried = new[] { 6, 5 }.Select((yard) => CornerLots(new Lot { R = lot, Roads = roads }, 1, yard)).ToList();
                    var k = tried.FindIndex((o) => o.Lots.Count > 0);
                    var corner = k >= 0 ? tried[k] : CornerLots(new Lot { R = lot, Roads = roads }, 1, 6);
                    GroupNear(c, Centre(corner.Square), rng, new IReadOnlyList<Part>[] { Dressed(Crates, DecorKinds.Crates), Dressed(Pair, DecorKinds.Crates) }, 1, 0, 6);
                    lots.AddRange(corner.Lots);
                }
                else if (organic && block != objBlock && squares < 2 && lot.W >= 13 && lot.H >= 13 && rng.Next() < 0.5)
                {
                    // An old town's small square, on the block's corner at a crossroads if it has one.
                    var corners = new[] { 0, 1, 2, 3 }.Where((k) => roads[k] && roads[(k + 3) % 4]).ToList();
                    var corner = CornerLots(new Lot { R = lot, Roads = roads }, corners.Count > 0 ? rng.Pick(corners) : rng.Int(4), lot.W >= 14 && lot.H >= 14 ? 6 : 5);
                    Plazuela(c, corner.Square, rng);
                    foreach (var l in corner.Lots) lots.AddRange(SplitLot(l.R, l.Roads, rng, 8, 0.5, 1));
                    squares++;
                }
                else if (hospital && block == objBlock)
                {
                    lots.Add(new Lot { R = lot, Roads = roads });
                }
                else
                {
                    lots.AddRange(SplitLot(lot, roads, rng, 8, block == objBlock && role == Roles.Indoor ? 0 : 0.65, 1));
                }
                var objLot = block == objBlock ? Best(lots, (l) => Dist(Centre(l.R), far)) : null;
                foreach (var l in lots)
                {
                    if (l == objLot && role == Roles.Open)
                    {
                        // The objective's square or car park: the far end of its lot, the rest built up.
                        var ends = EndOf(l, far, 120);
                        var open = ends != null ? ends.Value.Item1 : l;
                        var rest = ends != null ? ends.Value.Item2 : null;
                        var program = CityProgram(open.R, rng, role, used, false, style.Industrial);
                        if (program == CityPrograms.Yard) ContainerYard(c, open.R, FrontOf(c, open.R, open.Roads, rng), rng);
                        else if (program == CityPrograms.Plaza) Plaza(c, open.R, rng, false);
                        else Parking(c, open.R, rng, FrontOf(c, open.R, open.Roads, rng), false);
                        if (rest != null) CityLot(c, rest.R, CityProgram(rest.R, rng, null, used, organic, style.Industrial), rest.Roads, rng, style);
                        objective = open.R;
                        continue;
                    }
                    var isObj = l == objLot;
                    if (isObj && (clinic || hospital))
                    {
                        var front = FrontOf(c, l.R, l.Roads, rng);
                        var across = Math.Min(SideLen(l.R, front), hospital ? 12 : 11);
                        var depth = Math.Min(DepthOf(l.R, front), hospital ? 22 : ward ? 13 : 11);
                        var footprint = Local(l.R, front, (int)Math.Floor((SideLen(l.R, front) - across) / 2.0), 0, across, depth);
                        var b = MakeBuilding(c, footprint, rng, Plans.Rooms, front, style, Furnishes.Clinic, hospital ? MedicalSiteKinds.Hospital : ward ? MedicalSiteKinds.Ward : MedicalSiteKinds.Clinic);
                        b.Storeys = hospital ? 3 : 1;
                        b.MedicalWear = hospital ? 0.85 : ward ? 0.45 : 0.08;
                        objective = b.Outer;
                        continue;
                    }
                    var built = CityLot(c, l.R, CityProgram(l.R, rng, isObj ? role : null, used, organic, style.Industrial), l.Roads, rng, style);
                    if (isObj) objective = Best(built, (b) => Dist(Centre(b.Outer), far))?.Outer ?? l.R;
                }
                Sidewalk(c, block, walks, style.Industrial ? style.Furniture * 3 : style.Furniture, TileKinds.Low);
            }
            // An industrial estate's streets are emptier: its cover is the containers.
            ParkCars(c, plan.Roads, rng, style.Industrial ? 0.2 : 0.4, style.Industrial ? 0.05 : 0.15);
            if (!style.Industrial) Checkpoint(c, plan.Roads, rng);
            return objective;
        }

        // -------------------------------------------------------------------- town

        /// <summary>A town lot's longest side: past it, the lot is cut in two (docs/MAPAS.md phase 3; the plots come with phase 4).</summary>
        private const int TownLotSize = 18;

        /// <summary>
        /// A town block's buildable part on a plan (docs/MAPAS.md §4.1), and the sides it keeps a pavement on: two tiles along
        /// a grid's streets and the avenue, one now and then along an old town's, none on an alley. Against the outline it
        /// builds right up to what is shut; against another block, the east and south sides give up a tile for the alley
        /// between them, as townLot.
        /// </summary>
        private static (Rect Lot, bool[] Walks) PlanLot(Canvas c, Rect block, bool[] roads)
        {
            var lot = block;
            var walks = new[] { false, false, false, false };
            for (var s = 0; s < 4; s++)
            {
                var q = Add(LocalPt(block, s, (int)Math.Floor(SideLen(block, s) / 2.0), 0), Grid.Dirs4[s]);
                if (!c.In(q.X, q.Y) || c.GroundAt(q.X, q.Y) == Out) continue;
                var street = roads[s] ? RoadWidth(c, block, s) : 0;
                var pave = !roads[s] ? (s == 1 || s == 2 ? 1 : 0) : street >= StreetWide ? 2 : street >= 3 && Mix(block.X + s, block.Y) % 2 == 0 ? 1 : 0;
                if (pave == 0) continue;
                lot = Cut(lot, s, pave);
                walks[s] = roads[s];
            }
            return (lot, walks);
        }

        /// <summary>A grid's street, five tiles of roadway or more: a pavement of two each side.</summary>
        private const int StreetWide = 5;

        /// <summary>A lot cut while any side is longer than TOWN_LOT: in two across that side, a passage of one or two between.</summary>
        private static List<Lot> HalveLots(Lot l, Rng rng)
        {
            var r = l.R;
            var alongX = r.W >= r.H;
            var span = alongX ? r.W : r.H;
            if (span <= TownLotSize) return new List<Lot> { l };
            var gap = rng.Range(1, 2);
            var k = Clamp((int)Js.Round((span - gap) / 2.0) + rng.Range(-2, 2), 8, span - gap - 8);
            var parts = alongX ? new[] { Rect(r.X, r.Y, k, r.H), Rect(r.X + k + gap, r.Y, r.W - k - gap, r.H) } : new[] { Rect(r.X, r.Y, r.W, k), Rect(r.X, r.Y + k + gap, r.W, r.H - k - gap) };
            var out_ = new List<Lot>();
            foreach (var p in parts) out_.AddRange(HalveLots(Piece(l, p), rng));
            return out_;
        }

        /// <summary>How deep a town block's row of buildings is along its street (docs/MAPAS.md §5.2: the plot's depth).</summary>
        private static readonly IReadOnlyList<int> RowDepth = new[] { 9, 12 };

        /// <summary>
        /// A town block's lots (docs/MAPAS.md §5.2–5.4, a first take before phase 4's plots): a row along each street it
        /// faces, ROW_DEPTH deep, the ones along the north and south first and whole, the others between them; each row cut
        /// into lots of TOWN_LOT at most. What is left in the middle is the block's courtyard (`yard`), or one more lot if it
        /// looks onto a street still.
        /// </summary>
        private static (List<Lot> Lots, Rect? Yard) TownLots(Lot l, Rng rng)
        {
            var lots = new List<Lot>();
            var rest = l.R;
            foreach (var s in new[] { 0, 2, 1, 3 })
            {
                if (!l.Roads[s]) continue;
                var d = rng.Range(RowDepth[0], RowDepth[1]);
                // Room for the row and a courtyard or the row across behind it; else the rest is one lot.
                if (DepthOf(rest, s) < d + 6) continue;
                lots.AddRange(HalveLots(Piece(l, Local(rest, s, 0, 0, SideLen(rest, s), d)), rng));
                // A passage of one behind the row, into the courtyard.
                rest = Cut(rest, s, d + 1);
            }
            if (lots.Count == 0) return (HalveLots(l, rng), null);
            var left = Piece(l, rest);
            if (left.Roads.Any((x) => x))
            {
                var all = new List<Lot>(lots);
                all.AddRange(HalveLots(left, rng));
                return (all, null);
            }
            return (lots, rest);
        }

        /// <summary>
        /// A plot (docs/MAPAS.md §5.2): a strip of a block's edge with its front on a street (`front`, the side of the lot it
        /// looks onto), as deep as the row; on a corner, a street on its end too.
        /// </summary>
        private sealed class Plot : Lot
        {
            public int Front;
            public bool Corner;
        }

        /// <summary>A plot's frontage, by quarter (docs/MAPAS.md §5.2): an old town's houses wall to wall, a grid's wider.</summary>
        private static class Frontage
        {
            public static readonly IReadOnlyList<int> Old = new[] { 7, 9 };
            public static readonly IReadOnlyList<int> Grid = new[] { 8, 10 };
        }

        /// <summary>Frontages from `lo` to `hi` along `L` tiles, end to end; the last one too narrow goes into the one before.</summary>
        private static List<int> Frontages(int L, int lo, int hi, Rng rng)
        {
            var out_ = new List<int>();
            var left = L;
            while (left > 0)
            {
                if (left <= hi)
                {
                    out_.Add(left);
                    break;
                }
                var w = Math.Min(rng.Range(lo, hi), left - lo);
                out_.Add(w);
                left -= w;
            }
            if (out_.Count > 1 && out_[out_.Count - 1] < lo)
            {
                var last = out_[out_.Count - 1];
                out_.RemoveAt(out_.Count - 1);
                out_[out_.Count - 1] += last;
            }
            return out_;
        }

        /// <summary>The courtyard behind a block's rows is this deep at least (tiles); less, and the rows stand back to back.</summary>
        private const int Courtyard = 3;
        /// <summary>How deep the rows round a courtyard may be at least (tiles).</summary>
        private const int RingDepth = 8;

        /// <summary>
        /// How deep the rows along two opposite streets go into a lot `D` deep (docs/MAPAS.md §5.2: the plot's depth, half
        /// the block or up to its courtyard): ROW_DEPTH each with a courtyard between if there is room; else back to back
        /// across a passage of one, as deep as they can; a lot too shallow for two, one row through it. With a street on one
        /// side only, one row.
        /// </summary>
        private static (int, int) RowDepths(int D, bool both, Rng rng)
        {
            var lo = RowDepth[0];
            var hi = RowDepth[1];
            // On one street only, deeper: the houses take up to two more, the rest is their yards (all of it, if what would be
            // left behind is thinner than five: no strip of nothing against what shuts the map).
            if (!both)
            {
                var d = Math.Min(D, rng.Range(lo, hi) + 2);
                return (D - d < 5 ? D : d, 0);
            }
            // Round a courtyard, rows of 8 or more (a terraced house's depth: docs/MAPAS.md §4.2, 8–11).
            if (D >= 2 * RingDepth + 1 + Courtyard)
            {
                var a = rng.Range(RingDepth, Math.Min(hi, D - RingDepth - 1 - Courtyard));
                return (a, rng.Range(RingDepth, Math.Min(hi, D - a - 1 - Courtyard)));
            }
            if (D >= 2 * 7 + 1)
            {
                var a = (int)Math.Floor((D - 1) / 2.0);
                return (a, D - 1 - a);
            }
            return (Math.Min(D, hi + 2), 0);
        }

        /// <summary>
        /// A town block's plots (docs/MAPAS.md §5.2): a row along each street it faces, the ones along the north and south
        /// first and whole (the corners are theirs; rowDepths), then those along the east and west between them, where the
        /// middle is long enough; each row cut into frontages of its quarter. What is left in the middle is the block's
        /// courtyard, or one more lot if it looks onto a street still (`rest`).
        /// </summary>
        private static (List<List<Plot>> Strips, Rect? Yard, List<Lot> RestLots, bool Courtyard) TownPlots(Lot l, Rng rng, bool organic)
        {
            var strips = new List<List<Plot>>();
            var lo = organic ? Frontage.Old[0] : Frontage.Grid[0];
            var hi = organic ? Frontage.Old[1] : Frontage.Grid[1];
            var rest = l.R;
            // The row `d` deep along side `s` of what is left, cut into plots; what is left behind it, past a passage.
            void Row(int s, int d)
            {
                var L = SideLen(rest, s);
                var strip = new List<Plot>();
                var u = 0;
                foreach (var w in Frontages(L, lo, hi, rng))
                {
                    var lot = Piece(l, Local(rest, s, u, 0, w, d));
                    // A corner: the plot at an end of its row with a street round that end too.
                    var corner = (u == 0 && lot.Roads[s % 2 == 0 ? 3 : 0]) || (u + w == L && lot.Roads[s % 2 == 0 ? 1 : 2]);
                    strip.Add(new Plot { R = lot.R, Roads = lot.Roads, Front = s, Corner = corner });
                    u += w;
                }
                strips.Add(strip);
                rest = DepthOf(rest, s) > d ? Cut(rest, s, Math.Min(DepthOf(rest, s), d + 1)) : Rect(rest.X, rest.Y, 0, 0);
            }
            foreach (var pair in new[] { (0, 2), (3, 1) })
            {
                var (a, b) = pair;
                // East and west rows only along the middle the north and south ones leave, if it is long enough to build on.
                if (a == 3 && strips.Count > 0 && Math.Min(rest.W, rest.H) < 6) break;
                var ways = new[] { l.Roads[a] && SideLen(rest, a) >= Narrowest, l.Roads[b] && SideLen(rest, b) >= Narrowest };
                if (!ways[0] && !ways[1]) continue;
                var D = DepthOf(rest, a);
                var (d0, d1) = RowDepths(D, ways[0] && ways[1], rng);
                if (d0 < 7) continue;
                var first = ways[0] ? a : b;
                Row(first, d0);
                if (d1 >= 7 && rest.W > 0) Row(first == a ? b : a, Math.Min(d1, DepthOf(rest, b)));
            }
            if (strips.Count == 0 || rest.W <= 0 || rest.H <= 0) return (strips, null, new List<Lot>(), false);
            var left = Piece(l, rest);
            if (left.Roads.Any((x) => x) && Math.Min(rest.W, rest.H) >= Narrowest) return (strips, null, HalveLots(left, rng), false);
            // A courtyard (rows round it on two sides or more), or the back gardens of one row against what shuts the map.
            return (strips, rest, new List<Lot>(), strips.Count >= 2);
        }

        /// <summary>
        /// The town's ground out of doors that nobody walks to from the squad's (through doors, round what stands about) but
        /// through a window, or not at all: shut with the outline as lots (sealPockets' rule, before the ways are cut and the
        /// stairs laid: a flight put before that window later would leave it shut).
        /// </summary>
        private static void SealTown(Canvas c, Vec2 from)
        {
            bool Walk(Vec2 p) => c.In(p.X, p.Y) && c.GroundAt(p.X, p.Y) != Out && c.ElevAt(p.X, p.Y) == 0 && Grid.IsWalkable(c.Get(p.X, p.Y)) && c.Get(p.X, p.Y) != TileKinds.Window && c.Get(p.X, p.Y) != TileKinds.WindowBroken;
            var seen = new byte[c.Tiles.Count];
            var queue = Walk(from) ? new List<Vec2> { from } : new List<Vec2>();
            if (queue.Count > 0) seen[c.I(from.X, from.Y)] = 1;
            for (var k = 0; k < queue.Count; k++)
            {
                foreach (var d in Grid.Dirs4)
                {
                    var q = Add(queue[k], d);
                    if (!Walk(q) || seen[c.I(q.X, q.Y)] != 0) continue;
                    seen[c.I(q.X, q.Y)] = 1;
                    queue.Add(q);
                }
            }
            if (queue.Count == 0) return;
            // Each piece of what is left, unless a building's door opens onto it (its way in: connect cuts one to it later).
            var mask = new byte[c.Tiles.Count];
            var any = false;
            bool Pocket(Vec2 p) => Walk(p) && seen[c.I(p.X, p.Y)] == 0 && c.GroundAt(p.X, p.Y) != Indoor;
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                var start = V(i % c.Width, (int)Math.Floor((double)i / c.Width));
                if (!Pocket(start)) continue;
                var piece = new List<Vec2> { start };
                seen[i] = 1;
                var door = false;
                for (var k = 0; k < piece.Count; k++)
                {
                    foreach (var d in Grid.Dirs4)
                    {
                        var q = Add(piece[k], d);
                        if (!c.In(q.X, q.Y)) continue;
                        if (c.Get(q.X, q.Y) == TileKinds.Door || c.Get(q.X, q.Y) == TileKinds.DoorOpen) door = true;
                        if (!Pocket(q)) continue;
                        seen[c.I(q.X, q.Y)] = 1;
                        piece.Add(q);
                    }
                }
                if (door) continue;
                any = true;
                foreach (var p in piece)
                {
                    var j = c.I(p.X, p.Y);
                    mask[j] = 1;
                    c.Tiles[j] = TileKinds.Wall;
                    c.Ground[j] = Out;
                    c.Decor[j] = 0;
                    c.Keep[j] = true;
                }
            }
            if (any) foreach (var r in TownPlans.RectsOf(mask, c.Width, c.Height, 1)) c.Frame.Add(new FramePiece { Kind = FrameKinds.Lot, X = r.X, Y = r.Y, W = r.W, H = r.H });
        }

        /// <summary>
        /// The ground behind a block's rows (docs/MAPAS.md §5.4): each plot's back garden, fenced off from the next along the
        /// plots' line (its own and the alley behind the row open), in it now and then a tree or bushes, a bin, crates, a
        /// wheelbarrow or tools; in a courtyard (`courtyard`: rows round it on two sides or more) the fences reach halfway, a
        /// lane down its middle; behind a single row against what shuts the map, all the way back.
        /// </summary>
        private static void BackGardens(Canvas c, List<List<Plot>> strips, Rect? yard, bool courtyard, Rng rng)
        {
            if (yard == null) return;
            foreach (var strip in strips)
            {
                var s = strip[0].Front;
                // The yard seen from this row: along it as the row runs, in from the side that faces the row's back.
                int Along(Rect r) => s % 2 == 0 ? r.X : r.Y;
                var u0 = Along(yard);
                var n = SideLen(yard, s);
                var deep = DepthOf(yard, s);
                var reach = courtyard ? (int)Math.Floor((deep - 1) / 2.0) : deep;
                // The plots' lines (where one plot ends and the next starts): fenced across what is left of each plot behind its
                // building, up to the passage of one behind the row (which stays open, the way along it). Only where the line
                // ends on a building's back: not down the gap between two houses apart, a way into the courtyard.
                var cuts = strip.Skip(1).Select((p) => Along(p.R) - u0).Where((u) => u > 0 && u < n).ToList();
                foreach (var u in cuts)
                {
                    var line = new List<Vec2>();
                    var p = LocalPt(yard, s, u, -2);
                    while (line.Count < 4 && c.Free(p.X, p.Y) && c.GroundAt(p.X, p.Y) == Open)
                    {
                        line.Add(p);
                        p = LocalPt(yard, s, u, -2 - line.Count);
                    }
                    if (c.Get(p.X, p.Y) == TileKinds.Wall) for (var k = 0; k < line.Count; k++) Put(c, line[k], TileKinds.Low, new Look(k == 0 ? DecorKinds.GatePost : DecorKinds.Fence, (s + 1) % 4));
                }
                // Round a courtyard, each garden behind a building shut from the lane by its back fence, its gate in the middle
                // (§5.4): along the plot's last row, where it is open ground up to the building's back (not a gap between houses).
                if (courtyard)
                {
                    var outline = new HashSet<string> { TileKinds.Wall, TileKinds.Window, TileKinds.WindowBroken, TileKinds.Door, TileKinds.DoorOpen };
                    bool Garden(int u)
                    {
                        for (var v = -2; v >= -6; v--)
                        {
                            var q = LocalPt(yard, s, u, v);
                            var t = c.Get(q.X, q.Y);
                            if (outline.Contains(t)) return v < -2;
                            if (c.GroundAt(q.X, q.Y) != Open || (t != TileKinds.Floor && t != TileKinds.Low && t != TileKinds.High)) return false;
                        }
                        return false;
                    }
                    var edges = new List<int> { 0 };
                    edges.AddRange(cuts);
                    edges.Add(n);
                    for (var k = 0; k + 1 < edges.Count; k++)
                    {
                        for (var u = edges[k]; u < edges[k + 1];)
                        {
                            if (!Garden(u))
                            {
                                u++;
                                continue;
                            }
                            var end = u;
                            while (end < edges[k + 1] && Garden(end)) end++;
                            if (end - u >= 3)
                            {
                                var gate = u + (int)Math.Floor((end - u) / 2.0);
                                for (var t = u; t < end; t++) if (t != gate) Put(c, LocalPt(yard, s, t, -2), TileKinds.Low, new Look(t == gate - 1 || t == gate + 1 ? DecorKinds.GatePost : DecorKinds.Fence, s));
                                c.Reserve(LocalPt(yard, s, gate, -2).X, LocalPt(yard, s, gate, -2).Y);
                            }
                            u = end;
                        }
                    }
                }
                if (reach < 2) continue;
                // And across the yard, and the gardens between them.
                foreach (var u in cuts)
                {
                    for (var v = 0; v < reach; v++) Put(c, LocalPt(yard, s, u, v), TileKinds.Low, new Look(v == 0 || v == reach - 1 ? DecorKinds.GatePost : DecorKinds.Fence, (s + 1) % 4));
                }
                {
                    var edges = new List<int> { 0 };
                    edges.AddRange(cuts);
                    edges.Add(n);
                    // Into the yard (its depth) and along the row, as DIRS4 indices.
                    var inward = Opposite(s);
                    var across = s % 2 == 0 ? 1 : 2;
                    for (var k = 0; k + 1 < edges.Count; k++)
                    {
                        var a = edges[k];
                        var b = edges[k + 1];
                        if (b - a < 3 || rng.Next() < 0.3) continue;
                        var roll = rng.Next();
                        // A shed at the back of the garden, a vegetable patch, a washing line across it (§5.4); else a tree or bushes, a
                        // bin, crates, a wheelbarrow or tools.
                        if (roll < 0.25 && b - a >= 5 && reach >= 4 && PutObject(c, Footprint(LocalPt(yard, s, a + 1, reach - 3), inward, across, Decor.Footprint.Shed[0], Decor.Footprint.Shed[1]), TileKinds.High, DecorKinds.Shed, s)) continue;
                        if (roll < 0.45 && b - a >= 5 && reach >= 4 && PutObject(c, Footprint(LocalPt(yard, s, b - 3, 1), inward, across, Decor.Footprint.VegPatch[0], Decor.Footprint.VegPatch[1]), TileKinds.Low, DecorKinds.VegPatch, inward)) continue;
                        if (roll < 0.6 && b - a >= 6 && reach >= 3 && PutObject(c, Footprint(LocalPt(yard, s, a + 2, 1), across, inward, Decor.Footprint.WashingLine[0], Decor.Footprint.WashingLine[1]), TileKinds.Low, DecorKinds.WashingLine, across)) continue;
                        var at = LocalPt(yard, s, (int)Math.Floor((a + b) / 2.0), Math.Min(reach - 1, 1 + rng.Int(Math.Max(1, reach - 1))));
                        GroupNear(c, at, rng, new IReadOnlyList<Part>[]
                        {
                            Dressed(Pair, DecorKinds.Bush, DecorKinds.Tree),
                            new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Bin)) },
                            Dressed(Crates, DecorKinds.Crates),
                            new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Wheelbarrow)) },
                            new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Tools)) },
                            Dressed(Corner, DecorKinds.Bush, DecorKinds.Tree),
                        }, 1, 0, 6);
                    }
                }
            }
        }

        /// <summary>
        /// A block's big building (docs/MAPAS.md §4.2), now and then: an office 11–14 along its street and as deep as the block
        /// (12–18), or on a grid's big block a hall 14–18 × 18–24, at one end of the block along a street north or south; past
        /// a passage of one, the rest of the block (`rest`) for its plots. Null if the block has no room for one and plots too.
        /// </summary>
        private static (Building Built, Lot RestLot)? BigBuilding(Canvas c, Lot l, Rng rng, Style style, bool organic)
        {
            var sides = new[] { 0, 2 }.Where((x) => l.Roads[x]).ToList();
            if (sides.Count == 0) return null;
            var s = sides.Count > 1 ? sides[rng.Int(2)] : sides[0];
            var L = SideLen(l.R, s);
            var D = DepthOf(l.R, s);
            var hall = !organic && D >= 18 && L >= 14 + 1 + Narrowest && rng.Next() < 0.35;
            if (!hall && (D < 12 || L < 11 + 1 + Narrowest)) return null;
            var w = hall ? rng.Range(14, Math.Min(18, L - 1 - Narrowest)) : rng.Range(11, Math.Min(14, L - 1 - Narrowest));
            var d = hall ? Math.Min(D, rng.Range(18, 24)) : Math.Min(D, 18);
            var first = rng.Int(2) == 0;
            var r = Local(l.R, s, first ? 0 : L - w, 0, w, d);
            var b = hall
                ? MakeBuilding(c, r, rng, Plans.Hall, s, style, Furnishes.Store)
                : MakeBuilding(c, r, rng, Math.Min(w, d) >= 12 && rng.Next() < 0.7 ? Plans.Corridor : Plans.Open, s, style, Furnishes.Office);
            // An office of two or three storeys; a hall of one, its roof reached by a ladder now and then (upperFloors).
            b.Storeys = hall ? 1 : rng.Next() < 0.5 ? 3 : 2;
            return (b, Piece(l, Local(l.R, s, first ? w + 1 : 0, 0, L - w - 1, D)));
        }

        /// <summary>The streets (north or south) a supermarket may look onto from lot `l`: room for it, 12 along and 10 deep.</summary>
        private static List<int> MarketSides(Lot l) => new[] { 0, 2 }.Where((s) => l.Roads[s] && SideLen(l.R, s) >= 12 && DepthOf(l.R, s) >= 10).ToList();

        /// <summary>
        /// A supermarket (playtest 20/10, #18: Ana, «los supermercados no tienen esa pinta»): one storey, 10–12 deep along a
        /// street north or south; 12–14 along it at one end of a long block, the rest of the block past a passage of one
        /// (`rest`) for its plots, or else the block's length, 16 at most, and no rest. Its whole front glazed but for the
        /// double door in the middle, the checkouts, the aisles and the fridges inside (furnishMarket), a staff door at the
        /// back. Behind it, when the block goes deeper, its car park past a passage of one, or else its yard, bins and pallets.
        /// </summary>
        private static (Building Built, Lot? RestLot)? Supermarket(Canvas c, Lot l, Rng rng, Style style)
        {
            var sides = MarketSides(l);
            if (sides.Count == 0) return null;
            var s = sides.Count > 1 ? sides[rng.Int(2)] : sides[0];
            var L = SideLen(l.R, s);
            var D = DepthOf(l.R, s);
            var long_ = L >= 12 + 1 + Narrowest;
            var w = long_ ? rng.Range(12, Math.Min(14, L - 1 - Narrowest)) : Math.Min(L, 16);
            var d = Math.Min(D, rng.Range(10, 12));
            var first = rng.Int(2) == 0;
            var u0 = long_ ? (first ? 0 : L - w) : (int)Math.Floor((L - w) / 2.0);
            var b = MakeBuilding(c, Local(l.R, s, u0, 0, w, d), rng, Plans.Market, s, style, Furnishes.Store);
            b.Storeys = 1;
            var back = Opposite(s);
            if (D - d - 1 >= 6)
            {
                Parking(c, Local(l.R, s, u0, d + 1, w, D - d - 1), rng, l.Roads[back] ? back : s);
            }
            else if (D - d >= 3)
            {
                var bins = new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Bin)), P(1, 0, TileKinds.Low, new Look(DecorKinds.Bin)) };
                GroupNear(c, LocalPt(l.R, s, u0 + 2, d + 1), rng, new IReadOnlyList<Part>[] { bins, new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Bin)) } }, 1, 0, 6);
                GroupNear(c, LocalPt(l.R, s, u0 + w - 3, d + 1), rng, new IReadOnlyList<Part>[] { Dressed(Crates, DecorKinds.Pallets), Dressed(Pair, DecorKinds.Pallets, DecorKinds.Crates) }, 1, 0, 6);
            }
            return (b, long_ ? Piece(l, Local(l.R, s, first ? w + 1 : 0, 0, L - w - 1, D)) : null);
        }

        /// <summary>What a plot holds (docs/MAPAS.md §5.3): a house, a shop with homes over it, an office or flats over two plots, or nothing built: `'house' | 'shop' | 'office' | 'flats' | 'gap'`.</summary>
        private static class PlotUse
        {
            public const string House = "house";
            public const string Shop = "shop";
            public const string Office = "office";
            public const string Flats = "flats";
            public const string Gap = "gap";
        }

        /// <summary>Two plots side by side as one.</summary>
        private static Plot Joined(Plot a, Plot b)
        {
            var x = Math.Min(a.R.X, b.R.X);
            var y = Math.Min(a.R.Y, b.R.Y);
            var r = Rect(x, y, Math.Max(a.R.X + a.R.W, b.R.X + b.R.W) - x, Math.Max(a.R.Y + a.R.H, b.R.Y + b.R.H) - y);
            return new Plot { R = r, Front = a.Front, Corner = a.Corner || b.Corner, Roads = a.Roads.Select((k, s) => k || b.Roads[s]).ToArray() };
        }

        /// <summary>
        /// What each plot of a row holds, by quarter (docs/MAPAS.md §5.3): an old town is houses wall to wall and small shops,
        /// now and then flats; a grid's rows are shops and houses standing apart, offices and flats over two plots. One plot in
        /// twelve or so stays a lot, a garden or a ruin.
        /// </summary>
        private static List<(Plot Plot, string Use)> PlotUses(List<Plot> strip, Rng rng, bool organic)
        {
            var out_ = new List<(Plot Plot, string Use)>();
            for (var k = 0; k < strip.Count; k++)
            {
                var p = strip[k];
                var next = k + 1 < strip.Count ? strip[k + 1] : null;
                var both = next != null ? Joined(p, next) : null;
                var L = both != null ? SideLen(both.R, p.Front) : 0;
                var D = DepthOf(p.R, p.Front);
                var options = organic
                    ? new List<(string, double)> { (PlotUse.House, 5.5), (PlotUse.Shop, 3), (PlotUse.Gap, 0.5) }
                    : new List<(string, double)> { (PlotUse.House, 2), (PlotUse.Shop, 3.5), (PlotUse.Gap, 0.5) };
                // (Each a tile clear of the building before it and of the next: an office of 11 needs 13, flats of 16 need 18.)
                if (both != null && L >= 13 && L <= 16 && D >= 9) options.Add((PlotUse.Office, organic ? 0.6 : 2.4));
                if (both != null && L >= 18 && D >= 8) options.Add((PlotUse.Flats, organic ? 2 : 4));
                var use = Weighted(rng, options);
                if ((use == PlotUse.Office || use == PlotUse.Flats) && both != null)
                {
                    out_.Add((both, use));
                    k++;
                }
                else out_.Add((p, use));
            }
            return out_;
        }

        /// <summary>A run of buildings wall to wall is this long at most (tiles): then a passage of one, into the courtyard.</summary>
        private const int Terrace = 16;
        /// <summary>A building on its own is this wide at least (tiles, walls included); one wall to wall, counting the wall it shares.</summary>
        private const int Narrowest = 7;

        /// <summary>
        /// Builds a block's rows of plots (docs/MAPAS.md §5.3): each plot its building, its own front on its street. Houses
        /// and shops wall to wall share the wall between them, and it has no openings: a run of them as deep as its first,
        /// TERRACE long at most, then a passage; a grid's houses stand apart, a tile or more from the next. Too narrow for
        /// NARROWEST, a building takes what it lacks from the plot after it, or its plot stays a garden. The depth goes back
        /// two more or fewer from one run to the next, and now and then a house stands a tile back; a corner shop stands a
        /// storey taller. Returns the walled buildings.
        /// </summary>
        private static List<Building> BuildPlots(Canvas c, List<List<Plot>> strips, Rng rng, Style style, bool organic, bool courtyard = false)
        {
            var built = new List<Building>();
            foreach (var strip in strips)
            {
                var s = strip[0].Front;
                // Along the row, from its first plot's start (local's u), the plots' depth.
                var frame = strip[0].R;
                int At(Rect r) => s % 2 == 0 ? r.X - frame.X : r.Y - frame.Y;
                var units = PlotUses(strip, rng, organic);
                // Round a courtyard, every row has a way through it (docs/MAPAS.md §5.4: two ways in at least): a gap between
                // houses, a plot left open, or else at its middle house a zaguán (its hall open through it), or a passage there
                // between the two runs.
                var through = !courtyard || units.Any((u) => u.Use == PlotUse.Gap || (u.Use == PlotUse.House && !organic));
                var houses = new List<int>();
                for (var k = 0; k < units.Count; k++) if (units[k].Use == PlotUse.House && k > 0) houses.Add(k);
                var opening = through ? -1 : houses.Count > 0 ? houses[(int)Math.Floor(houses.Count / 2.0)] : Math.Max(1, (int)Math.Floor(units.Count / 2.0));
                // Where the last building ends along the row (one past it), and that one if what comes next may share its wall;
                // the run wall to wall so far, how long and how deep.
                var edge = int.MinValue; // -Infinity in TypeScript: no building yet (only compared, and edge + 1 taken)
                Rect? shareable = null;
                var run = 0;
                var runDepth = 0;
                bool Garden(Rect r) => GroupNear(c, Centre(r), rng, new IReadOnlyList<Part>[] { Dressed(Corner, DecorKinds.Bush, DecorKinds.Tree), Dressed(Pair, DecorKinds.Bush, DecorKinds.Tree) }, 1, 0, 6);
                for (var k = 0; k < units.Count; k++)
                {
                    var (plot, use) = units[k];
                    var start = At(plot.R);
                    var L = SideLen(plot.R, s);
                    var end = start + L;
                    var D = DepthOf(plot.R, s);
                    // A plot with nothing to walk on all along its front (the street ends short of it, against what shuts the map):
                    // nobody would get to it. Shut with the outline, as a lot.
                    var reached = false;
                    for (var u = 0; u < L; u++)
                    {
                        var q = LocalPt(frame, s, start + u, -1);
                        if (c.In(q.X, q.Y) && c.GroundAt(q.X, q.Y) != Out && Grid.IsWalkable(c.Get(q.X, q.Y)))
                        {
                            reached = true;
                            break;
                        }
                    }
                    if (!reached)
                    {
                        foreach (var p in TilesIn(plot.R))
                        {
                            c.Tiles[c.I(p.X, p.Y)] = TileKinds.Wall;
                            c.Ground[c.I(p.X, p.Y)] = Out;
                            c.Decor[c.I(p.X, p.Y)] = 0;
                            c.Keep[c.I(p.X, p.Y)] = true;
                        }
                        c.Frame.Add(new FramePiece { Kind = FrameKinds.Lot, X = plot.R.X, Y = plot.R.Y, W = plot.R.W, H = plot.R.H });
                        shareable = null;
                        continue;
                    }
                    if (use == PlotUse.Gap)
                    {
                        // A lot: rubble and weeds, a car left; a garden: a tree and bushes; a house's ruin.
                        var roll = rng.Next();
                        if (roll < 0.4)
                        {
                            GroupNear(c, Centre(plot.R), rng, Rubble, 2, 0, 6);
                            PlantAndLitter(c, plot.R, false);
                        }
                        else if (roll < 0.7 || L < Narrowest || start <= edge) Garden(plot.R);
                        else
                        {
                            // Gutted: no furniture of a home left in it, its rubble ruin's own.
                            var b = MakeBuilding(c, Local(frame, s, start, 0, L, Math.Min(D, 9)), rng, Plans.Rooms, s, style, Furnishes.Bare);
                            Ruin(c, b, rng);
                            // Nothing stands above a ruin's open ground.
                            b.Storeys = 0;
                            edge = end;
                        }
                        shareable = null;
                        continue;
                    }
                    if (use == PlotUse.Flats || use == PlotUse.Office)
                    {
                        var from = Math.Max(start, edge + 1);
                        var room = end - from;
                        var w = use == PlotUse.Flats ? Math.Min(room - 1, 16) : Math.Min(room - 1, 14);
                        if (w < (use == PlotUse.Flats ? 16 : 11))
                        {
                            Garden(plot.R);
                            shareable = null;
                            continue;
                        }
                        var u = from + (int)Math.Floor((room - w) / 2.0);
                        if (use == PlotUse.Flats) built.Add(FlatsBlock(c, Local(frame, s, u, 0, w, Math.Min(D, 11)), rng, s, style));
                        else
                        {
                            // A corridor where it is deep enough for rooms either side of it; else one open floor.
                            var d = Math.Min(D, style.OfficeDepth + 2);
                            var b = MakeBuilding(c, Local(frame, s, u, 0, w, d), rng, d >= 10 && rng.Next() < 0.65 ? Plans.Corridor : Plans.Open, s, style, Furnishes.Office);
                            b.Storeys = plot.Corner || rng.Next() < 0.4 ? 3 : 2;
                            built.Add(b);
                        }
                        edge = u + w;
                        shareable = null;
                        continue;
                    }
                    {
                        // A house or a shop: wall to wall (a shop, an old town's house), over the wall it shares or a passage after the
                        // run; apart (a grid's house), a tile from the one before (the next keeps its own from it).
                        var terraced = use == PlotUse.Shop || organic;
                        // (Over a shared wall a plot too narrow takes what it lacks from the next: the run grows by that much too.) The
                        // opening's house wall to wall too where it has room for its hall (8 a side: building), as a zaguán.
                        var joins = terraced && shareable != null && edge == start && run + Math.Max(L, Narrowest - 1) <= Terrace;
                        var zaguan = joins && k == opening && use == PlotUse.House && Math.Min(Math.Min(L + 1, runDepth), D) >= 8;
                        var share = joins && (k != opening || zaguan);
                        var a = share ? start - 1 : Math.Max(start, edge + 1);
                        var b = end;
                        if (b - a < Narrowest)
                        {
                            // Too narrow: what it lacks from the plot after it, if that one keeps six; else a garden.
                            var lack = Narrowest - (b - a);
                            if (k + 1 < units.Count && units[k + 1].Use != PlotUse.Gap && SideLen(units[k + 1].Plot.R, s) - lack >= 6)
                            {
                                var n = units[k + 1].Plot;
                                n.R = Local(frame, s, At(n.R) + lack, 0, SideLen(n.R, s) - lack, DepthOf(n.R, s));
                                b += lack;
                            }
                            else
                            {
                                Garden(plot.R);
                                shareable = null;
                                continue;
                            }
                        }
                        // As deep as the plot, less two now and then, twelve at most: the rest of a deep plot is its back garden. With
                        // nothing to walk on behind the plot (what shuts the map), all of it: no strip of garden nobody gets to.
                        var behind = LocalPt(frame, s, (int)Math.Floor((a + b) / 2.0), D);
                        var open = c.In(behind.X, behind.Y) && c.GroundAt(behind.X, behind.Y) != Out && Grid.IsWalkable(c.Get(behind.X, behind.Y));
                        var depth = share ? Math.Min(runDepth, D) : open ? Clamp(Math.Min(D, RowDepth[1]) - rng.Range(0, 2), 7, D) : D;
                        // A tile back from the street now and then, only where a street is there (not what shuts the map).
                        var ahead = LocalPt(frame, s, (int)Math.Floor((a + b) / 2.0), -1);
                        var street = c.In(ahead.X, ahead.Y) && c.GroundAt(ahead.X, ahead.Y) != Out && c.Get(ahead.X, ahead.Y) != TileKinds.Wall;
                        var back = !terraced && street && depth > Narrowest && rng.Next() < 0.3 ? 1 : 0;
                        var footprint = Local(frame, s, a, back, b - a, depth - back);
                        // How tall: an old town's houses one or two storeys, three on a corner; a grid's, one or two; a corner shop,
                        // three. A house of two or more has its hall with the stairs (hallPlan2).
                        // (A house's stairs turn back in a hall of two only with room to spare: two storeys, its corner too, for now.)
                        int? storeys = zaguan ? 2 : use == PlotUse.House ? (organic ? (plot.Corner || rng.Next() < 0.6 ? 2 : 1) : rng.Next() < 0.35 ? 2 : 1) : plot.Corner ? 3 : (int?)null;
                        var house = MakeBuilding(c, footprint, rng, use == PlotUse.Shop ? Plans.Shop : Plans.Rooms, s, style, use == PlotUse.Shop ? Furnishes.Shop : Furnishes.Home, MedicalSiteKinds.Clinic, use == PlotUse.House && (storeys ?? 1) >= 2, zaguan);
                        // Without room for its hall, a house of one storey: its stairs never stand in a room.
                        if (storeys != null) house.Storeys = use == PlotUse.House && house.HouseHall == null ? 1 : storeys;
                        built.Add(house);
                        // The wall shared with the one before: no door, no window in it.
                        if (share && shareable != null)
                        {
                            var x0 = Math.Max(shareable.X, footprint.X);
                            var y0 = Math.Max(shareable.Y, footprint.Y);
                            var x1 = Math.Min(shareable.X + shareable.W, footprint.X + footprint.W);
                            var y1 = Math.Min(shareable.Y + shareable.H, footprint.Y + footprint.H);
                            for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) if (c.Get(x, y) != TileKinds.Wall) c.Force(x, y, TileKinds.Wall);
                        }
                        run = share ? run + (b - a - 1) : b - a;
                        runDepth = depth;
                        edge = b;
                        shareable = terraced ? footprint : null;
                    }
                }
            }
            return built;
        }

        /// <summary>
        /// A park (docs/MAPAS.md §4.1): trees every four or five tiles, paths through its middle both ways, a hedge along its
        /// edges broken where the paths come in, and benches by the crossing.
        /// </summary>
        private static void Park(Canvas c, Rect r, Rng rng)
        {
            var m = Centre(r);
            bool Path(int x, int y) => Math.Abs(x - m.X) < 1.5 || Math.Abs(y - m.Y) < 1.5;
            foreach (var x in Spaced(r.X + 2, r.X + r.W - 3, 5))
            {
                foreach (var y in Spaced(r.Y + 2, r.Y + r.H - 3, 5))
                {
                    var p = V(x + rng.Range(-1, 1), y + rng.Range(-1, 1));
                    if (!Path(p.X, p.Y)) Group(c, p, new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.StreetTree, 0)) });
                }
            }
            for (var s = 0; s < 4; s++)
            {
                var n = SideLen(r, s);
                for (var u = 1; u < n - 1; u++)
                {
                    var p = LocalPt(r, s, u, 0);
                    if (!Path(p.X, p.Y) && Mix(p.X, p.Y) % 5 != 0) Put(c, p, TileKinds.Low, new Look(DecorKinds.Bush, s));
                }
            }
            var benches = 0;
            foreach (var side in new[] { 0, 2, 1, 3 })
            {
                var (p, f) = BenchBy(V((int)Math.Floor(m.X) - 1, (int)Math.Floor(m.Y) - 1), 3, side);
                if (benches < 2 && Group(c, p, BenchOf(f))) benches++;
            }
        }

        /// <summary>How many steps it takes from `from` to every tile outside the blocks (streets, pavements, squares), on foot.</summary>
        private static int[] StreetWalk(Canvas c, Vec2 from, List<Rect> blocks)
        {
            var inBlock = new byte[c.Tiles.Count];
            foreach (var b in blocks) foreach (var p in TilesIn(b)) inBlock[c.I(p.X, p.Y)] = 1;
            var steps = new int[c.Tiles.Count];
            for (var i = 0; i < steps.Length; i++) steps[i] = -1;
            steps[c.I(from.X, from.Y)] = 0;
            var queue = new List<int> { c.I(from.X, from.Y) };
            for (var k = 0; k < queue.Count; k++)
            {
                var i = queue[k];
                var x = i % c.Width;
                var y = (i - x) / c.Width;
                foreach (var d in Grid.Dirs8)
                {
                    var nx = x + d.X;
                    var ny = y + d.Y;
                    if (!c.In(nx, ny)) continue;
                    var j = c.I(nx, ny);
                    if (steps[j] >= 0 || inBlock[j] != 0 || c.Ground[j] == Out) continue;
                    steps[j] = steps[i] + 1;
                    queue.Add(j);
                }
            }
            return steps;
        }

        /// <summary>The walk from the squad to the middle of the objective's block, the town unbuilt (docs/MAPAS.md §5.7: 60–80 on foot).</summary>
        private static readonly IReadOnlyList<int> ObjectiveWalk = new[] { 58, 76 };

        /// <summary>
        /// A town on a plan (docs/MAPAS.md §5, phase 3; townPlan.ts): a grid's or an old town's streets, the outline round it
        /// shut (§5.10), its blocks cut into lots for the city's programs, a park or a square, cars along the kerbs. The
        /// objective's block is the one the squad walks to along the streets in OBJECTIVE_WALK, the biggest if several are
        /// (an office or a clinic fits). Returns the objective's ground and the plan.
        /// </summary>
        private static (Rect Objective, TownPlan Plan) Town(Canvas c, Rng rng, Style map, string role, bool clinic, bool hospital, bool ward)
        {
            // A clinic, a ward or a hospital needs one of a grid's big blocks.
            var organic = !hospital && !clinic && rng.Next() < 0.5;
            var style = CopyStyleTowns(map);
            style.Organic = organic;
            // A hospital's town has the biggest blocks (docs/MAPAS.md §4.2: 16–20 × 24–30).
            var plan = TownPlans.TownPlan(c.Width, c.Height, rng, organic, hospital);
            foreach (var road in plan.Roads) c.Paint(road.R, Road);
            // Out of play: shut, kept, drawn as the frame.
            for (var i = 0; i < plan.Out.Length; i++)
            {
                if (!plan.Out[i]) continue;
                c.Tiles[i] = TileKinds.Wall;
                c.Ground[i] = Out;
                c.Keep[i] = true;
            }
            c.Squad = plan.Squad;
            c.SquadSide = plan.SquadEntry.Side;
            c.EvacArea = null;
            c.Frame.AddRange(plan.Frame);
            c.Exits = plan.Entries.Where((e) => e != plan.SquadEntry).Select((e) => new Entry { Road = e.Road, Side = e.Side }).ToList();
            c.Lines = plan.Roads;
            // Where the squad gathers stays clear.
            foreach (var p in TilesIn(Rect(plan.Squad.X - 2, plan.Squad.Y - 2, 5, 5))) if (c.In(p.X, p.Y) && c.GroundAt(p.X, p.Y) != Out) c.Reserve(p.X, p.Y);
            if (plan.Square != null) Plaza(c, plan.Square, rng);
            // The walk from the squad, the town still unbuilt: to a block's middle, what the objective in it will be about.
            var steps = StreetWalk(c, plan.Squad, new List<Rect>());
            double Approach(Rect b)
            {
                var m = Centre(b);
                var n = steps[c.I((int)Math.Floor(m.X), (int)Math.Floor(m.Y))];
                return n >= 0 ? n : double.PositiveInfinity;
            }
            // Whether what a block leaves to build on (its pavements off) holds the objective: an office, a clinic, a hospital.
            bool Roomy(Rect b)
            {
                var lot = PlanLot(c, b, new[] { 0, 1, 2, 3 }.Select((s) => FacesRoad(c, b, s)).ToArray()).Lot;
                var short_ = Math.Min(lot.W, lot.H);
                var long_ = Math.Max(lot.W, lot.H);
                return hospital ? short_ >= 13 && long_ >= 23 : ward ? short_ >= 12 && long_ >= 14 : clinic ? short_ >= 12 : short_ >= (role == Roles.Indoor ? 10 : 9);
            }
            var scored = plan.Blocks.Select((b) => (B: b, D: Approach(b), Size: Math.Min(b.W, b.H))).Where((o) => o.D < double.PositiveInfinity).ToList();
            var fits = scored.Where((o) => Roomy(o.B)).ToList();
            var lo = ObjectiveWalk[0];
            var hi = ObjectiveWalk[1];
            // In reach and roomy, the bigger; else roomy, the nearest that reach; else the biggest there is.
            var objBlock = (
                BestOf(fits.Where((o) => o.D >= lo && o.D <= hi).ToList(), (o) => -o.Size + rng.Next() * 2) ??
                BestOf(fits, (o) => Math.Abs(o.D - (lo + hi) / 2.0)) ??
                BestOf(scored, (o) => -o.Size)
            )!.Value.B;
            // Within it, the lot furthest from the squad.
            var toward = Centre(objBlock);
            var far = new Pt(toward.X + Js.Sign(toward.X - plan.Squad.X) * objBlock.W, toward.Y + Js.Sign(toward.Y - plan.Squad.Y) * objBlock.H);
            // A grid's park or square: a middle block (one not on the outline), not the objective's.
            // Only where there are blocks to spare (four more to build on), and never the biggest of them.
            var inner = plan.Blocks.Where((b) => b != objBlock && Math.Min(b.W, b.H) >= 14 && TilesIn(Rect(b.X - 1, b.Y - 1, b.W + 2, b.H + 2)).All((p) => c.In(p.X, p.Y) && c.GroundAt(p.X, p.Y) != Out)).ToList();
            var biggest = Best(plan.Blocks, (b) => -b.W * b.H);
            var spare = plan.Blocks.Where((b) => Math.Min(b.W, b.H) >= 9).Count() >= 6;
            var green = !organic && spare && rng.Next() < 0.5 ? Best(inner.Where((b) => b != biggest).ToList(), (b) => b.W * b.H) : null;
            // A supermarket on most towns (playtest 20/10, #18): one of the blocks left with room for it. Drawn by position, so
            // that a town without one is built as it was.
            Lot LotOfBlock(Rect b)
            {
                var roads = new[] { 0, 1, 2, 3 }.Select((s) => FacesRoad(c, b, s)).ToArray();
                return new Lot { R = PlanLot(c, b, roads).Lot, Roads = roads };
            }
            var shops = plan.Blocks.Where((b) => b != objBlock && b != green && MarketSides(LotOfBlock(b)).Count > 0).ToList();
            var roll = Mix(objBlock.X * 31 + plan.Squad.X, objBlock.Y * 17 + plan.Squad.Y);
            var market = roll % 5 != 0 ? Js.At<Rect?>(shops, (roll >> 3) % Math.Max(1, shops.Count), null) : null;
            var objective = objBlock;
            var used = new JsMap<string, int>();
            foreach (var block in plan.Blocks)
            {
                var roads = new[] { 0, 1, 2, 3 }.Select((s) => FacesRoad(c, block, s)).ToArray();
                var (lot, walks) = PlanLot(c, block, roads);
                if (lot.W < 5 || lot.H < 5)
                {
                    // A sliver between streets: paving, a tree or a bench.
                    PlantAndLitter(c, lot, false);
                    continue;
                }
                if (block == green)
                {
                    if (Math.Max(lot.W, lot.H) >= 18 && rng.Next() < 0.6) Park(c, lot, rng);
                    else Plaza(c, lot, rng);
                    Sidewalk(c, block, walks, style.Furniture, TileKinds.Low);
                    continue;
                }
                // Not the objective's block: plots along its streets with a building on each (docs/MAPAS.md §5.2–5.3); now and
                // then the lots of before instead, for the open programs (a square, a car park, a filling station, a motel, works)
                // and the warehouses.
                if (block != objBlock && (block == market || rng.Next() >= (organic ? 0.06 : 0.1)))
                {
                    // Now and then a big building at one end of the block (an office, a hall), the rest in plots; the supermarket's.
                    (Building Built, Lot? RestLot)? big = null;
                    if (block == market) big = Supermarket(c, new Lot { R = lot, Roads = roads }, rng, style);
                    else if (rng.Next() < (organic ? 0.12 : 0.35)) big = BigBuilding(c, new Lot { R = lot, Roads = roads }, rng, style, organic);
                    var (strips, yard, rest, courtyard) = big != null && big.Value.RestLot == null ? (new List<List<Plot>>(), (Rect?)null, new List<Lot>(), false) : TownPlots(big?.RestLot ?? new Lot { R = lot, Roads = roads }, rng, organic);
                    if (strips.Count > 0 || big != null)
                    {
                        BuildPlots(c, strips, rng, style, organic, courtyard);
                        BackGardens(c, strips, yard, courtyard, rng);
                        // The courtyard's open ground: the yard and the passage of one behind each row round it. Behind rows of one plot
                        // it is their gardens back to back, with no plot lines across it (§5.4): no courtyard.
                        if (yard != null && courtyard && strips.Where((st) => st.Count >= 2).Count() >= 2)
                        {
                            c.Courtyards.Add(strips.Aggregate(yard, (r, st) =>
                            {
                                var s = st[0].Front;
                                return Local(r, s, 0, -1, SideLen(r, s), DepthOf(r, s) + 1);
                            }));
                        }
                        // The middle, where it still looks onto a street: houses or shops, not one more open lot.
                        foreach (var l in rest) CityLot(c, l.R, Filler(rng, style), l.Roads, rng, style);
                        Sidewalk(c, block, walks, style.Furniture, TileKinds.Low);
                        continue;
                    }
                }
                var split = block == objBlock && (hospital || role == Roles.Indoor) ? (Lots: new List<Lot> { new Lot { R = lot, Roads = roads } }, Yard: (Rect?)null) : TownLots(new Lot { R = lot, Roads = roads }, rng);
                var lots = split.Lots;
                if (split.Yard != null) PlantAndLitter(c, split.Yard, true);
                var objLot = block == objBlock ? Best(lots, (l) => Dist(Centre(l.R), far)) : null;
                foreach (var l in lots)
                {
                    if (l == objLot && role == Roles.Open)
                    {
                        var ends = EndOf(l, far, 120);
                        var open = ends != null ? ends.Value.Item1 : l;
                        var rest = ends != null ? ends.Value.Item2 : null;
                        var program = CityProgram(open.R, rng, role, used);
                        if (program == CityPrograms.Plaza) Plaza(c, open.R, rng, false);
                        else Parking(c, open.R, rng, FrontOf(c, open.R, open.Roads, rng), false);
                        if (rest != null) CityLot(c, rest.R, CityProgram(rest.R, rng, null, used, organic), rest.Roads, rng, style);
                        objective = open.R;
                        continue;
                    }
                    var isObj = l == objLot;
                    if (isObj && (clinic || hospital))
                    {
                        // Their sizes (docs/MAPAS.md §4.2): a hospital 16 × 24–30, its two wings and the corridor of 2 between them
                        // running back from the street (rooms 5 deep off it: 4 × 5 for two beds, §4.3), from the street that leaves it
                        // the longest (a narrow lot's end); an infirmary 11–13 × 13–17; a clinic 11 × 11.
                        var deep = hospital ? new[] { 0, 1, 2, 3 }.Where((s) => l.Roads[s] && SideLen(l.R, s) >= 12).ToList() : new List<int>();
                        var front = deep.Count > 0 ? BestOf(deep, (s) => -Math.Min(DepthOf(l.R, s), 30) - Math.Min(SideLen(l.R, s), 16) / 2.0 + rng.Next() / 10)!.Value : FrontOf(c, l.R, l.Roads, rng);
                        var across = Math.Min(SideLen(l.R, front), hospital ? 16 : ward ? rng.Range(11, 13) : 11);
                        var depth = Math.Min(DepthOf(l.R, front), hospital ? rng.Range(24, 30) : ward ? rng.Range(13, 17) : 11);
                        var footprint = Local(l.R, front, (int)Math.Floor((SideLen(l.R, front) - across) / 2.0), 0, across, depth);
                        var b = MakeBuilding(c, footprint, rng, Plans.Rooms, front, style, Furnishes.Clinic, hospital ? MedicalSiteKinds.Hospital : ward ? MedicalSiteKinds.Ward : MedicalSiteKinds.Clinic);
                        b.Storeys = hospital ? 3 : 1;
                        b.MedicalWear = hospital ? 0.85 : ward ? 0.45 : 0.08;
                        objective = b.Outer;
                        continue;
                    }
                    var built = CityLot(c, l.R, CityProgram(l.R, rng, isObj ? role : null, used, organic), l.Roads, rng, style);
                    if (isObj) objective = Best(built, (b) => Dist(Centre(b.Outer), far))?.Outer ?? l.R;
                }
                Sidewalk(c, block, walks, style.Furniture, TileKinds.Low);
            }
            ParkCars(c, plan.Roads, rng, 0.4, 0.15);
            Checkpoint(c, plan.Roads, rng);
            SealTown(c, plan.Squad);
            return (objective, plan);
        }

        // ------------------------------------------------------------------ estate

        /// <summary>An industrial estate's side (docs/MAPAS.md §4.8, phase 5).</summary>
        private const int EstateSize = 72;
        /// <summary>An estate lot's longest side: a warehouse and its yard (docs/MAPAS.md §4.2: halls of 14–18 × 18–24).</summary>
        private const int EstateLot = 26;

        /// <summary>What a plan leaves on the canvas: its roads, its outline shut, where the squad comes in and the bus goes out.</summary>
        private static void TakePlan(Canvas c, TownPlan plan)
        {
            foreach (var road in plan.Roads) c.Paint(road.R, Road);
            for (var i = 0; i < plan.Out.Length; i++)
            {
                if (!plan.Out[i]) continue;
                c.Tiles[i] = TileKinds.Wall;
                c.Ground[i] = Out;
                c.Keep[i] = true;
            }
            c.Squad = plan.Squad;
            c.SquadSide = plan.SquadEntry.Side;
            c.EvacArea = null;
            c.Frame.AddRange(plan.Frame);
            c.Exits = plan.Entries.Where((e) => e != plan.SquadEntry).Select((e) => new Entry { Road = e.Road, Side = e.Side }).ToList();
            c.Lines = plan.Roads;
            foreach (var p in TilesIn(Rect(plan.Squad.X - 2, plan.Squad.Y - 2, 5, 5))) if (c.In(p.X, p.Y) && c.GroundAt(p.X, p.Y) != Out) c.Reserve(p.X, p.Y);
        }

        /// <summary>A lot cut while any side is longer than `most`, in two across that side, a lane of two between (an estate's).</summary>
        private static List<Lot> CutLots(Lot l, Rng rng, int most)
        {
            var r = l.R;
            var alongX = r.W >= r.H;
            var span = alongX ? r.W : r.H;
            if (span <= most) return new List<Lot> { l };
            var k = Clamp((int)Js.Round((span - 2) / 2.0) + rng.Range(-3, 3), 10, span - 12);
            var parts = alongX ? new[] { Rect(r.X, r.Y, k, r.H), Rect(r.X + k + 2, r.Y, r.W - k - 2, r.H) } : new[] { Rect(r.X, r.Y, r.W, k), Rect(r.X, r.Y + k + 2, r.W, r.H - k - 2) };
            var out_ = new List<Lot>();
            foreach (var p in parts) out_.AddRange(CutLots(Piece(l, p), rng, most));
            return out_;
        }

        /// <summary>
        /// An industrial estate on a plan (docs/MAPAS.md phase 5; townPlan.ts, `estate`): big blocks between streets for the
        /// lorries, closed round by container yards, fenced lots, works and sheds shut up, now and then the railway or the
        /// motorway along a side. Its lots are warehouses, container yards, workshops and an office or two (industrialProgram);
        /// the objective's block, as a town's, the one the squad walks to in OBJECTIVE_WALK.
        /// </summary>
        private static (Rect Objective, TownPlan Plan) Estate(Canvas c, Rng rng, Style map, string role)
        {
            var style = CopyStyleTowns(map);
            style.Industrial = true;
            style.Organic = false;
            var plan = TownPlans.TownPlan(c.Width, c.Height, rng, false, true, true);
            TakePlan(c, plan);
            var steps = StreetWalk(c, plan.Squad, new List<Rect>());
            double Approach(Rect b)
            {
                var m = Centre(b);
                var n = steps[c.I((int)Math.Floor(m.X), (int)Math.Floor(m.Y))];
                return n >= 0 ? n : double.PositiveInfinity;
            }
            bool Roomy(Rect b) => Math.Min(b.W, b.H) - 4 >= (role == Roles.Indoor ? 12 : 9);
            var scored = plan.Blocks.Select((b) => (B: b, D: Approach(b), Size: Math.Min(b.W, b.H))).Where((o) => o.D < double.PositiveInfinity).ToList();
            var fits = scored.Where((o) => Roomy(o.B)).ToList();
            var lo = ObjectiveWalk[0];
            var hi = ObjectiveWalk[1];
            var objBlock = (BestOf(fits.Where((o) => o.D >= lo && o.D <= hi).ToList(), (o) => -o.Size + rng.Next() * 2) ?? BestOf(fits, (o) => Math.Abs(o.D - (lo + hi) / 2.0)) ?? BestOf(scored, (o) => -o.Size))!.Value.B;
            var toward = Centre(objBlock);
            var far = new Pt(toward.X + Js.Sign(toward.X - plan.Squad.X) * objBlock.W, toward.Y + Js.Sign(toward.Y - plan.Squad.Y) * objBlock.H);
            var objective = objBlock;
            var used = new JsMap<string, int>();
            foreach (var block in plan.Blocks)
            {
                var roads = new[] { 0, 1, 2, 3 }.Select((s) => FacesRoad(c, block, s)).ToArray();
                var (lot, walks) = PlanLot(c, block, roads);
                if (lot.W < 6 || lot.H < 6)
                {
                    PlantAndLitter(c, lot, false);
                    continue;
                }
                var lots = block == objBlock && role == Roles.Indoor ? new List<Lot> { new Lot { R = lot, Roads = roads } } : CutLots(new Lot { R = lot, Roads = roads }, rng, EstateLot);
                var objLot = block == objBlock ? Best(lots, (l) => Dist(Centre(l.R), far)) : null;
                foreach (var l in lots)
                {
                    if (l == objLot && role == Roles.Open)
                    {
                        var ends = EndOf(l, far, 160);
                        var open = ends != null ? ends.Value.Item1 : l;
                        var rest = ends != null ? ends.Value.Item2 : null;
                        ContainerYard(c, open.R, FrontOf(c, open.R, open.Roads, rng), rng);
                        if (rest != null) CityLot(c, rest.R, IndustrialProgram(rest.R, rng, null, used), rest.Roads, rng, style);
                        objective = open.R;
                        continue;
                    }
                    var isObj = l == objLot;
                    var built = CityLot(c, l.R, IndustrialProgram(l.R, rng, isObj ? role : null, used), l.Roads, rng, style);
                    if (isObj) objective = Best(built, (b) => Dist(Centre(b.Outer), far))?.Outer ?? l.R;
                }
                Sidewalk(c, block, walks, style.Furniture * 3, TileKinds.Low);
            }
            // Its cover is the containers: the streets emptier than a town's.
            ParkCars(c, plan.Roads, rng, 0.2, 0.05);
            return (objective, plan);
        }
    }
}
