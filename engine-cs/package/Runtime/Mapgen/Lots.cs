#nullable enable
// mapgen.ts in C#, its part from spaced() to cityLot(): the squares and car parks, the rows of houses and shops, the
// filling station, the motel, the building site, the checkpoint, the lots a block is cut into and the city's programs.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>`n` positions from `a` to `b` (inclusive) at an even spacing of about `step`, symmetric.</summary>
        private static List<int> Spaced(int a, int b, int step)
        {
            var n = Math.Max(1, (int)Js.Round((double)(b - a) / step));
            var out_ = new List<int>();
            for (var k = 0; k < n + 1; k++) out_.Add(a + (int)Js.Round((double)(k * (b - a)) / n));
            return out_;
        }

        /// <summary>The squad's corner (its spawns are the free tiles nearest it), and the corner the evacuation is carved in.</summary>
        private const int DeployArea = 12;
        private const int EvacArea = 14;
        /// <summary>Tiles along the map's edge kept clear, where the dead come in.</summary>
        private const int EdgeClear = 2;

        /// <summary>
        /// Whether new cover (a square's trees, a lot's rubble, the extra wrecks in
        /// the streets: the second look against Synty's captures) may stand on `p`:
        /// not in the squad's corner nor where the evacuation is carved, not by the
        /// map's edge, and nothing but floor and cover within `clear` tiles (walls,
        /// doors, windows, ladders). With 2, a tile always lies between it and a door
        /// or the foot of the ladder a building may get later against its wall.
        /// </summary>
        private static bool RoomForCover(Canvas c, Vec2 p, int clear)
        {
            if (Math.Abs(p.X - c.Squad.X) < DeployArea && Math.Abs(p.Y - c.Squad.Y) < DeployArea) return false;
            if (c.EvacArea != null && InRect(c.EvacArea, p)) return false;
            if (p.X < EdgeClear || p.Y < EdgeClear || p.X >= c.Width - EdgeClear || p.Y >= c.Height - EdgeClear) return false;
            for (var dy = -clear; dy <= clear; dy++)
            {
                for (var dx = -clear; dx <= clear; dx++)
                {
                    var k = c.Get(p.X + dx, p.Y + dy);
                    if (c.In(p.X + dx, p.Y + dy) && k != TileKinds.Floor && k != TileKinds.Low && k != TileKinds.High) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// What the second look added to a square or a car park that is neither the
        /// squad's nor the objective's (the captures' squares are planted and
        /// littered): on a square, two to four trees by its corners, a tile in from
        /// its edges and a tile clear of the fountain; on either, a heap or two of
        /// rubble, two tiles in from the edges (never on the row it opens onto the
        /// street by). Placed by position (mix): it draws nothing from the generator's
        /// rng, so the rest of a seed's layout only changes where it takes a tile.
        /// </summary>
        private static void PlantAndLitter(Canvas c, Rect r, bool trees)
        {
            var m = Centre(r);
            var cx = (int)Math.Floor(m.X);
            var cy = (int)Math.Floor(m.Y);
            if (trees && r.W >= 9 && r.H >= 9)
            {
                var want = 2 + (Mix(r.X, r.Y) % 3);
                var corners = new List<Vec2>
                {
                    V(r.X + 1, r.Y + 1),
                    V(r.X + r.W - 2, r.Y + 1),
                    V(r.X + 1, r.Y + r.H - 2),
                    V(r.X + r.W - 2, r.Y + r.H - 2),
                };
                Js.Sort(corners, (a, b) => Mix(a.X, a.Y) - Mix(b.X, b.Y));
                var planted = 0;
                foreach (var corner in corners)
                {
                    if (planted >= want) break;
                    // The corner's tile, or one a step or two further in (planters stand on some).
                    var inward = V(Js.Sign(cx - corner.X), Js.Sign(cy - corner.Y));
                    foreach (var (u, v) in new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2) })
                    {
                        var p = V(corner.X + inward.X * u, corner.Y + inward.Y * v);
                        // The fountain spans the middle 3 × 3: a tile clear of it.
                        if (Math.Max(Math.Abs(p.X - cx), Math.Abs(p.Y - cy)) < 3 || !RoomForCover(c, p, 2)) continue;
                        if (Group(c, p, new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Tree)) }))
                        {
                            planted++;
                            break;
                        }
                    }
                }
            }
            var heaps = 1 + (Mix(r.Y, r.X) % 2);
            var spots = TilesIn(Rect(r.X + 2, r.Y + 2, r.W - 4, r.H - 4));
            Js.Sort(spots, (a, b) => Mix(a.X, a.Y) - Mix(b.X, b.Y));
            var n = 0;
            foreach (var p in spots)
            {
                if (n >= heaps) break;
                var pair = Mix(p.Y, p.X) % 2 == 0 && p.X + 1 < r.X + r.W - 2;
                if (!RoomForCover(c, p, 2) || (pair && !RoomForCover(c, Add(p, V(1, 0)), 2))) continue;
                if (Group(c, p, pair ? new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Rubble)), P(1, 0, TileKinds.Low, new Look(DecorKinds.Rubble)) } : new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Rubble)) })) n++;
            }
        }

        /// <summary>
        /// A fountain, `n` tiles a side (3 or 2), from its north-west corner: the
        /// basin's rim, and on a big one the statue in its middle. An edge faces out
        /// of the fountain; a corner faces one of its two outer sides, the other one
        /// on its left (the north-west corner faces north).
        /// </summary>
        private static List<Part> Fountain(int n)
        {
            Look Rim(int k) => new Look(DecorKinds.FountainCorner, k);
            Look Edge(int k) => new Look(DecorKinds.Fountain, k);
            if (n == 2) return new List<Part> { P(0, 0, TileKinds.Low, Rim(0)), P(1, 0, TileKinds.Low, Rim(1)), P(0, 1, TileKinds.Low, Rim(3)), P(1, 1, TileKinds.Low, Rim(2)) };
            return new List<Part>
            {
                P(0, 0, TileKinds.Low, Rim(0)),
                P(1, 0, TileKinds.Low, Edge(0)),
                P(2, 0, TileKinds.Low, Rim(1)),
                P(0, 1, TileKinds.Low, Edge(3)),
                P(1, 1, TileKinds.High, new Look(DecorKinds.Statue)),
                P(2, 1, TileKinds.Low, Edge(1)),
                P(0, 2, TileKinds.Low, Rim(3)),
                P(1, 2, TileKinds.Low, Edge(2)),
                P(2, 2, TileKinds.Low, Rim(2)),
            };
        }

        /// <summary>A park bench facing `f` (a DIRS4 index): two tiles along its seat, the second on the left of one sitting on the first.</summary>
        private static List<Part> BenchOf(int f)
        {
            var along = Grid.Dirs4[(f + 1) % 4];
            return new List<Part> { P(0, 0, TileKinds.Low, new Look(DecorKinds.Bench, f)), P(along.X, along.Y, TileKinds.Low, new Look(DecorKinds.Bench, f)) };
        }

        /// <summary>
        /// Where a bench beside a fountain `n` tiles a side at `at` goes, on its side
        /// `side` (a DIRS4 index) a tile clear of the rim, facing it: the bench's
        /// first tile (benchOf) and the way it faces.
        /// </summary>
        private static (Vec2, int) BenchBy(Vec2 at, int n, int side)
        {
            var mid = n == 3 ? 1 : 0;
            switch (side)
            {
                case 0:
                    return (V(at.X + mid + 1, at.Y - 2), 2);
                case 1:
                    return (V(at.X + n + 1, at.Y + mid + 1), 3);
                case 2:
                    return (V(at.X + mid, at.Y + n + 1), 0);
                default:
                    return (V(at.X - 2, at.Y + mid), 1);
            }
        }

        /// <summary>
        /// A square: a fountain with a statue in the middle, planters mirrored around
        /// it on a regular grid, two benches facing the fountain and a kiosk or two
        /// by the edges; `extras`, its trees and rubble (plantAndLitter), all but on
        /// the squad's and the objective's.
        /// </summary>
        private static void Plaza(Canvas c, Rect r, Rng rng, bool extras = true)
        {
            var m = Centre(r);
            var cx = (int)Math.Floor(m.X);
            var cy = (int)Math.Floor(m.Y);
            if (r.W >= 9 && r.H >= 9) Group(c, V(cx - 1, cy - 1), Fountain(3));
            else if (r.W >= 6 && r.H >= 6) Group(c, V(cx, cy), Fountain(2));
            if (r.W >= 8 && r.H >= 8)
            {
                var alongX = r.W >= r.H;
                var len = r.W >= 12 && r.H >= 12 ? 3 : 2;
                foreach (var x in Spaced(r.X + 2, r.X + r.W - 3, 6))
                {
                    foreach (var y in Spaced(r.Y + 2, r.Y + r.H - 3, 6))
                    {
                        if (Math.Abs(x - cx) <= 3 && Math.Abs(y - cy) <= 3) continue;
                        // Mirrored: planters on the far side grow back towards the middle.
                        var x0 = alongX && x > cx ? x - len + 1 : x;
                        var y0 = !alongX && y > cy ? y - len + 1 : y;
                        var parts = new List<Part>();
                        for (var k = 0; k < len; k++) parts.Add(alongX ? P(k, 0, TileKinds.Low, new Look(DecorKinds.Planter, 0)) : P(0, k, TileKinds.Low, new Look(DecorKinds.Planter, 1)));
                        Group(c, V(x0, y0), parts);
                    }
                }
            }
            // Two benches facing the fountain from either end of the square's length, else across it (where the planters leave
            // room). Nothing drawn from the rng: the rest of a seed's layout stays as it was.
            if (r.W >= 9 && r.H >= 9)
            {
                var ends = r.W >= r.H ? new[] { 1, 3, 0, 2 } : new[] { 0, 2, 1, 3 };
                var benches = 0;
                foreach (var side in ends)
                {
                    var (p, f) = BenchBy(V(cx - 1, cy - 1), 3, side);
                    if (benches < 2 && Group(c, p, BenchOf(f))) benches++;
                }
            }
            var sides = Shuffle(new List<int> { 0, 1, 2, 3 }, rng).GetRange(0, rng.Range(1, 2));
            foreach (var side in sides)
            {
                var p = LocalPt(r, side, (int)Math.Floor(SideLen(r, side) / 2.0) + rng.Range(-1, 1), 1);
                Group(c, p, Turn(Dressed(Pair, DecorKinds.Bin, DecorKinds.Kiosk), rng.Int(4)));
            }
            if (extras) PlantAndLitter(c, r, true);
        }

        /// <summary>
        /// The squad's square (playtest 2026-10-18, #12: «mobiliario al azar»), laid
        /// out like any other round the corner the squad starts in, which stays
        /// clear: the fountain at its far end, two benches facing it from the squad's
        /// side (a bin by the first), and a tree with a planter at its foot on each
        /// of its corners along the map's edges. They are the squad's cover on its
        /// first move (generateLayout adds no posts where they stand). Nothing on the
        /// map's edges, where the dead come in, nor off the square. It draws from the
        /// rng what the plain square drew for its kiosks, so the rest of the town
        /// comes out as it did.
        /// </summary>
        private static void SquadSquare(Canvas c, Rect r, Rng rng)
        {
            foreach (var _ in Shuffle(new List<int> { 0, 1, 2, 3 }, rng).GetRange(0, rng.Range(1, 2)))
            {
                rng.Range(-1, 1);
                rng.Int(4);
            }
            bool Place(Vec2 p, IReadOnlyList<Part> parts) =>
                parts.All((t) => InRect(r, V(p.X + t.X, p.Y + t.Y)) && p.X + t.X >= EdgeClear && p.Y + t.Y >= EdgeClear) && Group(c, p, parts);
            // The biggest fountain that fits a tile in from the square's edges, as far from the corner as it goes, on its diagonal.
            foreach (var n in new[] { 3, 2 })
            {
                var spots = TilesIn(Rect(r.X + 1, r.Y + 1, r.W - n - 1, r.H - n - 1));
                Js.Sort(spots, (a, b) =>
                {
                    var far = b.X + b.Y - (a.X + a.Y);
                    return far != 0 ? far : Math.Abs(a.X - a.Y) - Math.Abs(b.X - b.Y);
                });
                Vec2? at = null;
                foreach (var p in spots)
                {
                    if (!Place(p, Fountain(n))) continue;
                    at = p;
                    break;
                }
                if (at == null) continue;
                // The squad's sides of it first: the squad's corner is the north-west one.
                var benches = 0;
                foreach (var side in new[] { 0, 3, 1, 2 })
                {
                    var (p, f) = BenchBy(at, n, side);
                    var along = Grid.Dirs4[(f + 1) % 4];
                    var withBin = BenchOf(f);
                    withBin.Add(P(2 * along.X, 2 * along.Y, TileKinds.Low, new Look(DecorKinds.Bin)));
                    if (benches < 2 && ((benches == 0 && Place(p, withBin)) || Place(p, BenchOf(f)))) benches++;
                }
                break;
            }
            // Where each tree would stand, the way further in along the edge (if the squad's corner is there), and its planter's side.
            var corners = new[]
            {
                (V(r.X + r.W - 2, r.Y + 1), V(0, 1), V(-1, 0)),
                (V(r.X + 1, r.Y + r.H - 2), V(1, 0), V(0, -1)),
            };
            foreach (var (corner, inward, foot) in corners)
            {
                for (var k = 0; k < 4; k++)
                {
                    if (Place(Add(corner, V(inward.X * k, inward.Y * k)), new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Tree)), P(foot.X, foot.Y, TileKinds.Low, new Look(DecorKinds.Planter)) })) break;
                }
            }
        }

        /// <summary>
        /// Car park: bays across the lot facing an aisle, two rows when deep enough, and a booth; `extras`, its rubble
        /// (plantAndLitter), but on the objective's. Its cars three tiles long (decor.ts FOOTPRINT), with a row clear behind.
        /// </summary>
        private static void Parking(Canvas c, Rect r, Rng rng, int front, bool extras = true)
        {
            var across = SideLen(r, front);
            var depth = DepthOf(r, front);
            if (across < 5 || depth < 4) return;
            var long_ = Decor.Footprint.Car[1];
            var rows = depth >= 2 * long_ + 5 ? new[] { 1, depth - long_ - 1 } : depth >= long_ + 4 ? new[] { 2 } : depth >= long_ + 2 ? new[] { 1 } : new int[0];
            foreach (var v in rows)
            {
                // Nose out: the first row faces into the lot, the second one back towards the street.
                var first = v == rows[0];
                var facing = first ? Opposite(front) : front;
                for (var u = 1; u < across - 1; u += 2)
                {
                    if (rng.Next() < 0.35) continue;
                    var car = new List<Vec2>();
                    for (var k = 0; k < long_; k++) car.Add(LocalPt(r, front, u, v + k));
                    if (first) car.Reverse();
                    PutObject(c, car, TileKinds.Low, DecorKinds.CarFront, facing);
                }
            }
            Group(c, LocalPt(r, front, rng.Int(2) == 0 ? 0 : across - 1, 0), new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Kiosk)) });
            if (extras) PlantAndLitter(c, r, false);
        }

        /// <summary>A row of houses or shops on the front of a lot, and another one on the back when the lot is deep.</summary>
        private static List<Building> RowOfBuildings(Canvas c, Rect lot, int front, bool[] roads, Rng rng, Style style, string plan, string furnish)
        {
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            var rear = Opposite(front); // TypeScript's `back` (the name is taken by each house's below)
            // A second row along the back when the lot is deep enough, back to back across an alley or the yards
            // between them. The houses take the depth there is: up to two tiles over the map's own in two rows, three in one.
            var two = D >= 2 * 7 + 1;
            var hd = two ? Clamp((int)Math.Floor((D - 1) / 2.0), 7, style.HouseDepth + 2) : Math.Min(D, style.HouseDepth + 3);
            var plot = Math.Min(style.House, L);
            if (plot < 7 || hd < 7) return new List<Building>();
            var rows = two ? new[] { front, rear } : new[] { front };
            // A single row of houses with room to spare stands back from the street behind a front garden.
            var setback = plan == Plans.Rooms && rows.Length == 1 && D - hd >= 4 ? 2 : 0;
            var out_ = new List<Building>();
            var n = Math.Max(1, (int)Math.Floor((double)(L + style.Gap) / (plot + style.Gap)));
            // The row fills its side: what is left over widens the houses, up to two tiles each (four for a house on its own).
            var hw = plot + Math.Min(n == 1 ? 4 : 2, (int)Math.Floor((double)(L - (n * plot + (n - 1) * style.Gap)) / n));
            foreach (var side in rows)
            {
                // In an old town every plot is its own width, with alleys of one or two between them.
                var widths = new List<int>();
                for (var k = 0; k < n; k++) widths.Add(style.Organic ? plot + rng.Range(0, 3) : hw);
                var gaps = new List<int>();
                for (var k = 0; k < n - 1; k++) gaps.Add(style.Organic ? rng.Range(1, 2) : style.Gap);
                int Sum() => widths.Sum() + gaps.Sum();
                while (Sum() > L)
                {
                    var k = widths.IndexOf(widths.Max());
                    // Math.max(...[]) is -Infinity, its indexOf -1 (never needed: a row of one is never too long).
                    var g = gaps.Count > 0 ? gaps.IndexOf(gaps.Max()) : -1;
                    if (widths[k] > 7) widths[k]--;
                    else gaps[g]--;
                }
                var spare = L - Sum();
                var u = (int)Math.Floor(spare / 2.0) + (spare > 1 ? rng.Range(-1, 1) : 0);
                u = Clamp(u, 0, spare);
                // A long row keeps a garden in one of its middle plots now and then, and a house row may end in a corner shop.
                var garden = n >= 3 && rng.Next() < 0.15 ? rng.Range(1, n - 2) : -1;
                var shop = plan == Plans.Rooms && rng.Next() < 0.3 ? rng.Pick(new[] { 0, n - 1 }) : -1;
                for (var k = 0; k < n; k++)
                {
                    var w = widths[k];
                    // Not every house lines up with the next: one now and then stands back from the street (more, and further, in an
                    // old town), on the room a single row has to spare behind it, or shallower.
                    var room = rows.Length == 1 ? D - hd : 0;
                    var most = Math.Min(style.Organic ? 2 : 1, room + hd - 7);
                    var back = setback == 0 && most > 0 && rng.Next() < (style.Organic ? 0.5 : 0.15) ? rng.Range(1, most) : 0;
                    var b = Local(lot, side, u, setback + back, w, hd - Math.Max(0, back - room));
                    if (k == garden) GroupNear(c, Centre(b), rng, new IReadOnlyList<Part>[] { Dressed(Corner, DecorKinds.Bush, DecorKinds.Tree), Dressed(Pair, DecorKinds.Bush, DecorKinds.Tree) }, 1, 0, 6);
                    else if (k == shop) out_.Add(MakeBuilding(c, Local(lot, side, u, 0, w, hd), rng, Plans.Shop, side, style, Furnishes.Shop));
                    else
                    {
                        out_.Add(MakeBuilding(c, b, rng, plan, side, style, furnish));
                        if (setback != 0) FrontGarden(c, lot, side, u, w, rng);
                    }
                    u += w + Js.At(gaps, k, 0);
                }
            }
            // Back yards: a fence down the middle between two rows (posts beside each gap), a tree or bins behind a single one.
            var yard = Local(lot, front, 0, hd, L, D - hd * rows.Length);
            if (rows.Length == 2 && DepthOf(yard, front) >= 3)
            {
                var v = (int)Math.Floor(DepthOf(yard, front) / 2.0);
                for (var u = 0; u < L; u++) if (u % 6 != 3) Put(c, LocalPt(yard, front, u, v), TileKinds.Low, new Look(u % 6 == 2 || u % 6 == 4 ? DecorKinds.GatePost : DecorKinds.Fence, front));
            }
            else if (DepthOf(yard, front) >= 3)
            {
                for (var u = 2; u < L - 2; u += hw + style.Gap) GroupNear(c, LocalPt(yard, front, u + 2, 1), rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Bin, DecorKinds.Tree), Dressed(Corner, DecorKinds.Bin, DecorKinds.Tree) }, 1, 0, 4);
            }
            return out_;
        }

        /// <summary>
        /// A house's front garden (its plot `u`…`u + width` of the lot's side `side`,
        /// two tiles deep): a fence along the pavement with a gate in front of the
        /// door, and a bush or a tree by the fence.
        /// </summary>
        private static void FrontGarden(Canvas c, Rect lot, int side, int u, int width, Rng rng)
        {
            var door = -1;
            for (var k = 0; k < width; k++)
            {
                var p = LocalPt(lot, side, u + k, 2);
                if (c.Get(p.X, p.Y) == TileKinds.Door) door = k;
            }
            if (door < 0) return;
            for (var k = 0; k < width; k++)
            {
                if (Math.Abs(k - door) == 0) continue;
                Put(c, LocalPt(lot, side, u + k, 0), TileKinds.Low, new Look(Math.Abs(k - door) == 1 ? DecorKinds.GatePost : DecorKinds.Fence, side));
            }
            // The path to the door stays clear; a bush in a corner of the garden.
            c.Reserve(LocalPt(lot, side, u + door, 1).X, LocalPt(lot, side, u + door, 1).Y);
            var corner = door > width / 2.0 ? 1 : width - 2;
            if (rng.Next() < 0.7) Put(c, LocalPt(lot, side, u + corner, 1), TileKinds.Low, new Look(DecorKinds.Bush));
        }

        /// <summary>
        /// A high street: rows of narrow shops wall to wall along the front of the lot,
        /// two or three to a row (a row is at most 16 tiles), an alley between rows.
        /// Each shop is its own building with its own door on the street; the walls
        /// they share have no openings.
        /// </summary>
        private static List<Building> HighStreet(Canvas c, Rect lot, int front, Rng rng, Style style)
        {
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            if (L < 7 || D < 7) return new List<Building>();
            // Deep enough to fill the lot (a shop floor and a stockroom), or two rows back to back across an alley.
            var two = D >= 2 * 7 + 1;
            var d = two ? Clamp((int)Math.Floor((D - 1) / 2.0), 7, 10) : Clamp(D - rng.Range(0, 1), 7, 12);
            var out_ = new List<Building>();
            var rows = two ? new[] { front, Opposite(front) } : new[] { front };
            foreach (var side in rows) out_.AddRange(ShopRow(c, lot, side, L, d, rng, style));
            return out_;
        }

        /// <summary>One row of shops wall to wall along side `side` of the lot.</summary>
        private static List<Building> ShopRow(Canvas c, Rect lot, int front, int L, int d, Rng rng, Style style)
        {
            var D = DepthOf(lot, front);
            var out_ = new List<Building>();
            var u = rng.Int(2);
            while (u + 7 <= L)
            {
                var room = Math.Min(16, L - u);
                // Shops 6 or 7 tiles wide (a counter fits), sharing a wall: a row of n is 1 + Σ(w − 1) long.
                var widths = new List<int>();
                var total = 1;
                for (var k = 0; k < 3; k++)
                {
                    var w = rng.Range(6, 7);
                    if (total + w - 1 > room) break;
                    widths.Add(w);
                    total += w - 1;
                }
                if (widths.Count == 0) break;
                if (total < 7) widths[0] = 7;
                // What is left of the row's room widens its shops, up to 9 tiles each.
                for (int extra = room - Math.Max(7, total), k = 0; extra > 0 && widths.Any((w) => w < 9); k = (k + 1) % widths.Count)
                {
                    if (widths[k] < 9)
                    {
                        widths[k]++;
                        extra--;
                    }
                }
                var shops = new List<Rect>();
                var x = u;
                foreach (var w in widths)
                {
                    var b = Local(lot, front, x, 0, w, d);
                    out_.Add(MakeBuilding(c, b, rng, Plans.Shop, front, style, Furnishes.Shop));
                    shops.Add(b);
                    x += w - 1;
                }
                // Party walls: a window or a door between two shops is walled up again.
                for (var k = 1; k < shops.Count; k++)
                {
                    var a = shops[k - 1];
                    var b = shops[k];
                    var x0 = Math.Max(a.X, b.X);
                    var y0 = Math.Max(a.Y, b.Y);
                    var x1 = Math.Min(a.X + a.W, b.X + b.W);
                    var y1 = Math.Min(a.Y + a.H, b.Y + b.H);
                    for (var y = y0; y < y1; y++) for (var x2 = x0; x2 < x1; x2++) if (c.Get(x2, y) != TileKinds.Wall) c.Force(x2, y, TileKinds.Wall);
                }
                // Bins and crates out the back.
                if (D - d >= 3) GroupNear(c, LocalPt(lot, front, u + (int)Math.Floor((x - u) / 2.0), d + 1), rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Bin, DecorKinds.Crates), Dressed(Barricade, DecorKinds.Bin) }, 2, 0, 4);
                // An alley to the next row, a tile wide, now and then two.
                u = x + 1 + (rng.Next() < 0.7 ? 1 : 2);
            }
            return out_;
        }

        /// <summary>
        /// A filling station: the shop at the back of the lot to one side, two pump
        /// islands on the forecourt (pumps: `tank` hints, the client draws them as
        /// pumps under a canopy), a car or two waiting at them.
        /// </summary>
        private static List<Building> GasStation(Canvas c, Rect lot, int front, Rng rng, Style style)
        {
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            if (L < 13 || D < 10) return new List<Building>();
            var left = rng.Int(2) == 0;
            var b = MakeBuilding(c, Local(lot, front, left ? 0 : L - 7, D - 7, 7, 7), rng, Plans.Shop, front, style, Furnishes.Shop);
            // The shop is a kiosk: one storey.
            b.Storeys = 1;
            // Islands across the forecourt, two tiles long, a lane either side.
            var first = left ? 9 : 2;
            foreach (var du in new[] { 0, 3 })
            {
                var u = first + du;
                if (u + 1 >= L) continue;
                foreach (var v in new[] { 2, 3 }) Put(c, LocalPt(lot, front, u, v), TileKinds.Low, new Look(DecorKinds.Tank, (front + 1) % 4));
                // A car at the pump now and then, in the lane beside it, its nose to the road (decor.ts FOOTPRINT: three tiles).
                if (rng.Next() < 0.5)
                {
                    List<Vec2> Car(int v)
                    {
                        var cells = new List<Vec2>();
                        for (var k = 0; k < Decor.Footprint.Car[1]; k++) cells.Add(LocalPt(lot, front, u + 1, v + k));
                        return cells;
                    }
                    if (!PutObject(c, Car(2), TileKinds.Low, DecorKinds.CarFront, front)) PutObject(c, Car(1), TileKinds.Low, DecorKinds.CarFront, front);
                }
            }
            return new List<Building> { b };
        }

        /// <summary>
        /// A motel: one or two wings of three rooms in a row at the back of the lot,
        /// every room with its own door and window onto the car park in front.
        /// </summary>
        private static List<Building> Motel(Canvas c, Rect lot, int front, Rng rng)
        {
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            if (L < 13 || D < 11) return new List<Building>();
            var out_ = new List<Building>();
            var wings = L >= 28 ? 2 : 1;
            var gap = wings == 2 ? Math.Min(L - 26, 4) : 0;
            var u = (int)Math.Floor((L - (13 * wings + gap)) / 2.0);
            for (var k = 0; k < wings; k++)
            {
                out_.Add(MotelWing(c, Local(lot, front, u, D - 7, 13, 7), front, rng));
                u += 13 + gap;
            }
            Parking(c, Local(lot, front, 0, 0, L, D - 8), rng, front);
            return out_;
        }

        /// <summary>Three motel rooms side by side (13 × 7, walls included), each opening on side `front`.</summary>
        private static Building MotelWing(Canvas c, Rect b, int front, Rng rng)
        {
            for (var y = b.Y; y < b.Y + b.H; y++)
            {
                for (var x = b.X; x < b.X + b.W; x++)
                {
                    var edge = x == b.X || y == b.Y || x == b.X + b.W - 1 || y == b.Y + b.H - 1;
                    c.Force(x, y, edge ? TileKinds.Wall : TileKinds.Floor, 0);
                    c.Ground[c.I(x, y)] = Indoor;
                }
            }
            var depth = DepthOf(b, front);
            var rooms = new List<Rect>();
            for (var k = 0; k < 3; k++)
            {
                // The wall to the next room: no door, each room is let on its own.
                if (k > 0) for (var v = 1; v < depth - 1; v++) c.Force(LocalPt(b, front, 4 * k, v).X, LocalPt(b, front, 4 * k, v).Y, TileKinds.Wall);
                var room = Local(b, front, 1 + 4 * k, 1, 3, depth - 2);
                rooms.Add(room);
                Opening(c, LocalPt(b, front, 1 + 4 * k, 0), front, TileKinds.Door);
                c.Force(LocalPt(b, front, 3 + 4 * k, 0).X, LocalPt(b, front, 3 + 4 * k, 0).Y, TileKinds.Window);
                FurnishRoom(c, room, rng, Furnishes.Home, global::Bastion.Engine.HomeUses.Bedroom);
            }
            var wing = new Building
            {
                Outer = b,
                Rooms = rooms,
                Furnish = Furnishes.Home,
                Storeys = 1,
                Flat = true,
                HomeWear = 0.5,
                HomeRooms = rooms.Select((r) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = 0, Use = global::Bastion.Engine.HomeUses.Bedroom }).ToList(),
            };
            c.Buildings.Add(wing);
            return wing;
        }

        /// <summary>
        /// A building site: barriers along the street with a gate, stacks of bricks,
        /// pallets and cement inside, a scaffold (a one-storey platform with a ladder)
        /// and the workmen's portaloo.
        /// </summary>
        private static void Site(Canvas c, Rect lot, int front, Rng rng)
        {
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            if (L < 8 || D < 8) return;
            var gate = rng.Range(2, L - 4);
            for (var u = 0; u < L; u++) if (u < gate || u > gate + 1) Put(c, LocalPt(lot, front, u, 0), TileKinds.Low, new Look(DecorKinds.Barricade, front));
            // The scaffold against the back of the lot, a ladder at one end.
            var box = Local(lot, front, rng.Range(1, Math.Max(1, L - 6)), D - 3, 5, 2);
            var fits = TilesIn(Rect(box.X - 1, box.Y - 1, box.W + 2, box.H + 2)).All((p) => c.Free(p.X, p.Y) || !c.In(p.X, p.Y));
            if (fits) Platform(c, box, rng, 1);
            for (var k = 0; k < 3; k++) GroupNear(c, LocalPt(lot, front, rng.Range(2, L - 3), rng.Range(2, D - 5)), rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Crates), Dressed(Crates, DecorKinds.Crates) }, 1, 0, 6);
            // The portaloo (the client draws a town's `pylon` as one).
            GroupNear(c, LocalPt(lot, front, L - 2, 2), rng, new IReadOnlyList<Part>[] { new[] { P(0, 0, TileKinds.High, new Look(DecorKinds.Pylon)) } }, 1, 0, 6);
        }

        /// <summary>
        /// A checkpoint across one street: barriers from kerb to kerb with a gap for
        /// one car, tall blocks at the ends.
        /// </summary>
        private static void Checkpoint(Canvas c, List<PlanRoad> roads, Rng rng)
        {
            var options = roads.Where((r) => (r.Vertical ? r.R.W : r.R.H) >= 3 && (r.Vertical ? r.R.H : r.R.W) >= 10).ToList();
            if (options.Count == 0 || rng.Next() < 0.4) return;
            var road = rng.Pick(options);
            var width = road.Vertical ? road.R.W : road.R.H;
            var length = road.Vertical ? road.R.H : road.R.W;
            var t = (int)Math.Floor(length / 2.0) + rng.Range(-2, 2);
            var gap = rng.Range(1, width - 2);
            var facing = road.Vertical ? 0 : 3;
            for (var lane = 0; lane < width; lane++)
            {
                if (lane == gap) continue;
                var p = road.Vertical ? V(road.R.X + lane, road.R.Y + t) : V(road.R.X + t, road.R.Y + lane);
                if (!c.Free(p.X, p.Y)) continue;
                var end = lane == 0 || lane == width - 1;
                c.Force(p.X, p.Y, end ? TileKinds.High : TileKinds.Low);
                c.Dress(p, end ? DecorKinds.Crates : DecorKinds.Barricade, facing);
            }
        }

        // interface Lot { r: Rect; roads: boolean[] } is Core.cs's Lot.

        /// <summary>How wide the road along side `s` of `r` is (its narrowest stretch), 0 if none.</summary>
        private static int RoadWidth(Canvas c, Rect r, int s)
        {
            var d = Grid.Dirs4[s];
            int? width = null; // Infinity
            for (var u = 0; u < SideLen(r, s); u++)
            {
                var p = Add(LocalPt(r, s, u, 0), d);
                var n = 0;
                while (c.In(p.X, p.Y) && c.GroundAt(p.X, p.Y) == Road)
                {
                    n++;
                    p = Add(p, d);
                }
                if (n != 0) width = width == null ? n : Math.Min(width.Value, n);
            }
            return width ?? 0;
        }

        /// <summary>
        /// The buildable part of a block: one tile less on every side but the map's
        /// edge, a sidewalk along streets and half an alley against a block behind.
        /// </summary>
        private static Rect LotOf(Canvas c, Rect block)
        {
            var lot = block;
            for (var s = 0; s < 4; s++)
            {
                var q = Add(LocalPt(block, s, 0, 0), Grid.Dirs4[s]);
                if (c.In(q.X, q.Y)) lot = Cut(lot, s, 1);
            }
            return lot;
        }

        /// <summary>
        /// A town block's buildable part, and the sides it keeps a sidewalk on: both
        /// sides of an avenue; one side of a street three tiles wide in a grid (the
        /// north or west one), one now and then in an old town; none on a lane (the
        /// houses stand right on it) or on the map's edge. Against the block behind,
        /// the east and south sides give up a tile for the alley between them, the
        /// other block builds up to it.
        /// </summary>
        private static (Rect Lot, bool[] Walks) TownLot(Canvas c, Rect block, bool[] roads, bool organic)
        {
            var lot = block;
            var walks = new[] { false, false, false, false };
            for (var s = 0; s < 4; s++)
            {
                var q = Add(LocalPt(block, s, 0, 0), Grid.Dirs4[s]);
                if (!c.In(q.X, q.Y)) continue;
                var street = roads[s] ? RoadWidth(c, block, s) : 0;
                var pave = !roads[s] ? s == 1 || s == 2 : street >= 4 || (street == 3 && (organic ? Mix(block.X + s, block.Y) % 3 == 0 : s == 1 || s == 2));
                if (!pave) continue;
                lot = Cut(lot, s, 1);
                walks[s] = roads[s];
            }
            return (lot, walks);
        }

        /// <summary>A long lot is often split in two, back to back with an alley (`alley` wide, else 1–2) between them; each half is at least `min` long.</summary>
        private static List<Lot> SplitLot(Rect lot, bool[] roads, Rng rng, int min, double chance, int? alley = null)
        {
            var alongX = lot.W >= lot.H;
            var span = alongX ? lot.W : lot.H;
            var gap = alley ?? rng.Range(1, 2);
            if (span < 2 * min + gap || Math.Min(lot.W, lot.H) < 7 || rng.Next() >= chance) return new List<Lot> { new Lot { R = lot, Roads = roads } };
            var k = Clamp((int)Js.Round((span - gap) / 2.0 + rng.Range(-2, 2)), min, span - min - gap);
            if (alongX)
            {
                return new List<Lot>
                {
                    new Lot { R = Rect(lot.X, lot.Y, k, lot.H), Roads = new[] { roads[0], false, roads[2], roads[3] } },
                    new Lot { R = Rect(lot.X + k + gap, lot.Y, lot.W - k - gap, lot.H), Roads = new[] { roads[0], roads[1], roads[2], false } },
                };
            }
            return new List<Lot>
            {
                new Lot { R = Rect(lot.X, lot.Y, lot.W, k), Roads = new[] { roads[0], roads[1], false, roads[3] } },
                new Lot { R = Rect(lot.X, lot.Y + k + gap, lot.W, lot.H - k - gap), Roads = new[] { false, roads[1], roads[2], roads[3] } },
            };
        }

        /// <summary>Piece `r` of lot `l`: it keeps the lot's roads on the sides it shares with it.</summary>
        private static Lot Piece(Lot l, Rect r)
        {
            var o = l.R;
            return new Lot { R = r, Roads = new[] { r.Y == o.Y && l.Roads[0], r.X + r.W == o.X + o.W && l.Roads[1], r.Y + r.H == o.Y + o.H && l.Roads[2], r.X == o.X && l.Roads[3] } };
        }

        /// <summary>
        /// The end of a lot nearest `to`, about `area` tiles (7 to 11 deep), and the
        /// rest of it beyond a one-tile alley; null when the rest would be too small
        /// to build on.
        /// </summary>
        private static (Lot, Lot)? EndOf(Lot l, Pt to, int area)
        {
            var m = Centre(l.R);
            var s = l.R.W >= l.R.H ? (to.X > m.X ? 1 : 3) : to.Y > m.Y ? 2 : 0;
            var across = SideLen(l.R, s);
            var depth = DepthOf(l.R, s);
            var n = Math.Min((int)Clamp(Js.Round((double)area / across), 7, 11), depth - 8);
            if (n < 7) return null;
            return (Piece(l, Local(l.R, s, 0, 0, across, n)), Piece(l, Local(l.R, s, 0, n + 1, across, depth - n - 1)));
        }

        /// <summary>
        /// A square `yard` tiles across in a corner of a lot (0 north-west, then
        /// clockwise: north-east, south-east, south-west), and the lots left round it
        /// beyond a one-tile alley: the rest of its width beside the square and the
        /// rest of its depth beyond it, when they are 7 or more across (else the
        /// square takes that whole side).
        /// </summary>
        private static (Rect Square, List<Lot> Lots) CornerLots(Lot lot, int corner, int yard)
        {
            var r = lot.R;
            var east = corner == 1 || corner == 2;
            var south = corner >= 2;
            var wide = r.W >= yard + 8;
            var deep = r.H >= yard + 8;
            var sw = wide ? yard : r.W;
            var sh = deep ? yard : r.H;
            var square = Rect(east ? r.X + r.W - sw : r.X, south ? r.Y + r.H - sh : r.Y, sw, sh);
            var lots = new List<Lot>();
            if (wide) lots.Add(Piece(lot, Rect(east ? r.X : r.X + yard + 1, r.Y, r.W - yard - 1, r.H)));
            if (deep) lots.Add(Piece(lot, Rect(square.X, south ? r.Y : r.Y + yard + 1, sw, r.H - yard - 1)));
            return (square, lots);
        }

        /// <summary>A small square on a corner of an old town's block: a fountain with a bench facing it, or trees and bushes, and a bin by the kerb.</summary>
        private static void Plazuela(Canvas c, Rect r, Rng rng)
        {
            var m = Centre(r);
            if (rng.Next() < 0.4)
            {
                var at = V((int)Math.Floor(m.X), (int)Math.Floor(m.Y));
                if (Group(c, at, Fountain(2)))
                {
                    // On the first side with room, by position: the rng draws as before.
                    for (var k = 0; k < 4; k++)
                    {
                        var (p, f) = BenchBy(at, 2, (Mix(r.X, r.Y) + k) % 4);
                        var parts = BenchOf(f);
                        if (parts.All((t) => InRect(r, V(p.X + t.X, p.Y + t.Y))) && Group(c, p, parts)) break;
                    }
                }
            }
            else
            {
                GroupNear(c, m, rng, new IReadOnlyList<Part>[] { Dressed(Corner, DecorKinds.Bush, DecorKinds.Tree), Dressed(Pair, DecorKinds.Planter, DecorKinds.Tree) }, 1, 0, 6);
            }
            var side = rng.Int(4);
            GroupNear(c, LocalPt(r, side, (int)Math.Floor(SideLen(r, side) / 2.0), 1), rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Bin, DecorKinds.Kiosk), new[] { P(0, 0, TileKinds.Low, new Look(DecorKinds.Bin)) } }, 1, 0, 4);
        }

        // -------------------------------------------------------------------- city

        // type Role and type CityProgram are Core.cs's Roles and CityPrograms.

        private static T Weighted<T>(Rng rng, IReadOnlyList<(T, double)> options)
        {
            var total = 0.0;
            foreach (var (_, w) in options) total += w;
            var roll = rng.Next() * total;
            foreach (var (item, w) in options)
            {
                roll -= w;
                if (roll < 0) return item;
            }
            return options[options.Count - 1].Item1;
        }

        /// <summary>`used` counts the programs already placed: besides the squad's square and the objective, two open lots at most (a square, a car park, a site, a filling station, a motel), one of each.</summary>
        private static string CityProgram(Rect lot, Rng rng, string? role, JsMap<string, int> used, bool organic = false, bool industrial = false)
        {
            var short_ = Math.Min(lot.W, lot.H);
            var long_ = Math.Max(lot.W, lot.H);
            if (industrial) return IndustrialProgram(lot, rng, role, used);
            if (role == Roles.Indoor) return short_ >= 10 && rng.Next() < 0.6 ? CityPrograms.Office : rng.Next() < 0.5 ? CityPrograms.Shops : CityPrograms.Houses;
            if (role == Roles.Open) return rng.Next() < 0.55 ? CityPrograms.Plaza : CityPrograms.Parking;
            // Open lots stay small and few: a big empty block reads as a hole in the city.
            var area = lot.W * lot.H;
            // Two at most on a map, besides the squad's square and the objective's.
            var few = new[] { CityPrograms.Plaza, CityPrograms.Parking, CityPrograms.Site, CityPrograms.GasStation, CityPrograms.Motel }.Sum((p) => used.Get(p, 0)) < 2;
            var options = new List<(string, double)>();
            if (few && used.Get(CityPrograms.Plaza, 0) == 0 && area <= 130) options.Add((CityPrograms.Plaza, 0.35));
            if (few && used.Get(CityPrograms.Parking, 0) == 0 && area <= 160) options.Add((CityPrograms.Parking, 0.35));
            // Smaller buildings, more of them (Synty's captures: rows of shops, a filling station, a motel, houses with gardens);
            // an old town is mostly houses and small shops, a few offices, flats and warehouses.
            var big = organic ? 0.45 : 1;
            if (short_ >= 7)
            {
                options.Add((CityPrograms.Houses, organic ? 4 : 2.5));
                options.Add((CityPrograms.Shops, 1));
                options.Add((CityPrograms.HighStreet, organic ? 1.2 : 2.5));
            }
            if (short_ >= 10) options.Add((CityPrograms.Office, 2 * big));
            if (short_ >= 9 && long_ >= 10) options.Add((CityPrograms.Flats, 2 * big));
            if (short_ >= 12) options.Add((CityPrograms.Warehouse, 1.2 * big));
            if (few && used.Get(CityPrograms.GasStation, 0) == 0 && short_ >= 10 && long_ >= 13 && area <= 180) options.Add((CityPrograms.GasStation, 0.25));
            if (few && used.Get(CityPrograms.Motel, 0) == 0 && short_ >= 11 && long_ >= 13) options.Add((CityPrograms.Motel, 0.9));
            if (few && used.Get(CityPrograms.Site, 0) == 0 && short_ >= 8 && area <= 150) options.Add((CityPrograms.Site, 0.4));
            if (options.Count == 0) options.Add((CityPrograms.Parking, 1));
            var pick = Weighted(rng, options);
            used.Set(pick, used.Get(pick, 0) + 1);
            return pick;
        }

        /// <summary>An industrial estate's lot (playtest 2026-10-14, #25): mostly warehouses and container yards, a few offices and workshops.</summary>
        private static string IndustrialProgram(Rect lot, Rng rng, string? role, JsMap<string, int> used)
        {
            var short_ = Math.Min(lot.W, lot.H);
            if (role == Roles.Indoor) return short_ >= 12 ? CityPrograms.Warehouse : short_ >= 10 ? CityPrograms.Office : CityPrograms.Shops;
            if (role == Roles.Open) return CityPrograms.Yard;
            var options = new List<(string, double)>();
            if (short_ >= 12) options.Add((CityPrograms.Warehouse, 3));
            if (short_ >= 10) options.Add((CityPrograms.Office, 0.6));
            if (short_ >= 6) options.Add((CityPrograms.Yard, 1.5));
            if (short_ >= 7) options.Add((CityPrograms.Shops, 0.4));
            if (used.Get(CityPrograms.Parking, 0) == 0) options.Add((CityPrograms.Parking, 0.4));
            if (used.Get(CityPrograms.Site, 0) == 0 && short_ >= 8) options.Add((CityPrograms.Site, 0.3));
            if (options.Count == 0) options.Add((CityPrograms.Parking, 1));
            var pick = Weighted(rng, options);
            used.Set(pick, used.Get(pick, 0) + 1);
            return pick;
        }

        /// <summary>What fills the rest of a lot beside or behind something else: a row of houses or shops; on an industrial estate, a yard.</summary>
        private static string Filler(Rng rng, Style style)
        {
            var houses = rng.Next() < 0.5;
            return style.Industrial ? CityPrograms.Yard : houses ? CityPrograms.Houses : CityPrograms.HighStreet;
        }

        /// <summary>
        /// A container yard (playtest 2026-10-14, #25): asphalt, rows of shipping
        /// containers lying along the street, two deep with a lane of two between rows
        /// and a gap between neighbours, now and then one missing. Most are fenced
        /// along the street with barbed wire, a gate of two in the middle between posts.
        /// </summary>
        private static void ContainerYard(Canvas c, Rect lot, int front, Rng rng)
        {
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            foreach (var p in TilesIn(lot)) if (c.In(p.X, p.Y) && c.GroundAt(p.X, p.Y) == Open) c.Ground[c.I(p.X, p.Y)] = Yard;
            var fenced = L >= 8 && D >= 6 && rng.Next() < 0.6;
            for (var v = fenced ? 2 : 1; v + 3 <= D; v += 4)
            {
                for (var u = 1; u + 6 <= L; u += 6)
                {
                    if (rng.Next() < 0.3) continue;
                    var box = Local(lot, front, u, v, 5, 2);
                    // Drawn 2 × 5 with its doors north: turned to lie along the street, its doors to one end.
                    var q = front % 2 == 0 ? (rng.Int(2) == 0 ? 1 : 3) : rng.Int(2) == 0 ? 0 : 2;
                    var parts = Turn(Container, q);
                    var x0 = parts.Min((t) => t.X);
                    var y0 = parts.Min((t) => t.Y);
                    Group(c, V(box.X - x0, box.Y - y0), parts);
                }
            }
            if (!fenced) return;
            var gate = (int)Math.Floor(L / 2.0) - 1;
            for (var u = 0; u < L; u++)
            {
                if (u == gate || u == gate + 1) continue;
                var post = u == 0 || u == L - 1 || u == gate - 1 || u == gate + 2;
                Put(c, LocalPt(lot, front, u, 0), post ? TileKinds.High : TileKinds.Low, new Look(post ? DecorKinds.GatePost : DecorKinds.Wire, front));
            }
        }

        /// <summary>
        /// Makes room beside a building `w` wide on the front of a lot: when what is
        /// left over along the front is wide enough, the building's part of the lot is
        /// at one end and a row of houses or shops fills the rest, beyond a one-tile
        /// alley. Returns the building's part (the whole lot if not) and that row.
        /// </summary>
        private static (Rect Own, List<Building> Row) Alongside(Canvas c, Lot lot, int front, int w, Rng rng, Style style)
        {
            var L = SideLen(lot.R, front);
            var D = DepthOf(lot.R, front);
            if (L - w - 1 < 7) return (lot.R, new List<Building>());
            var first = rng.Int(2) == 0;
            var rest = Piece(lot, Local(lot.R, front, first ? w + 1 : 0, 0, L - w - 1, D));
            var own = Local(lot.R, front, first ? 0 : L - w, 0, w, D);
            return (own, CityLot(c, rest.R, Filler(rng, style), rest.Roads, rng, style));
        }

        /// <summary>How deep and how long along the street the open programs go: bigger lots build up the rest, beyond an alley.</summary>
        private static readonly IReadOnlyDictionary<string, (int, int)> OpenSize = new Dictionary<string, (int, int)>
        {
            { CityPrograms.Plaza, (10, 12) },
            { CityPrograms.Parking, (10, 14) },
            { CityPrograms.Site, (10, 12) },
            { CityPrograms.GasStation, (11, 15) },
            { CityPrograms.Motel, (13, 15) },
        };

        /// <summary>Fills one city lot. Returns the walled buildings (the objective may go in one).</summary>
        private static List<Building> CityLot(Canvas c, Rect lot, string program, bool[] roads, Rng rng, Style style)
        {
            // An industrial estate has no homes: what would be houses or flats is a yard.
            if (style.Industrial && (program == CityPrograms.Houses || program == CityPrograms.HighStreet || program == CityPrograms.Flats)) program = CityPrograms.Yard;
            var front = FrontOf(c, lot, roads, rng);
            var L = SideLen(lot, front);
            var D = DepthOf(lot, front);
            if (OpenSize.TryGetValue(program, out var open))
            {
                // Open ground stays small: past its depth the back of the lot is built up, past its length one end.
                var whole = new Lot { R = lot, Roads = roads };
                var (depth, span) = open;
                if (D - depth - 1 >= 7)
                {
                    var ahead = Piece(whole, Local(lot, front, 0, 0, L, depth));
                    var behind = Piece(whole, Local(lot, front, 0, depth + 1, L, D - depth - 1));
                    var row = CityLot(c, behind.R, Filler(rng, style), behind.Roads, rng, style);
                    var all = new List<Building>(CityLot(c, ahead.R, program, ahead.Roads, rng, style));
                    all.AddRange(row);
                    return all;
                }
                if (L - span - 1 >= 7)
                {
                    var first = rng.Int(2) == 0;
                    var own = Piece(whole, Local(lot, front, first ? 0 : L - span, 0, span, D));
                    var rest = Piece(whole, Local(lot, front, first ? span + 1 : 0, 0, L - span - 1, D));
                    var row = CityLot(c, rest.R, Filler(rng, style), rest.Roads, rng, style);
                    var all = new List<Building>(CityLot(c, own.R, program, own.Roads, rng, style));
                    all.AddRange(row);
                    return all;
                }
            }
            switch (program)
            {
                case CityPrograms.Office:
                {
                    var w = Math.Min(style.Office, L);
                    var d = Math.Min(style.OfficeDepth, D);
                    if (w < 10 || d < 10) return CityLot(c, lot, CityPrograms.Houses, roads, rng, style);
                    var (own, row) = Alongside(c, new Lot { R = lot, Roads = roads }, front, w, rng, style);
                    var l = SideLen(own, front);
                    var b = MakeBuilding(c, Local(own, front, (int)Math.Floor((l - w) / 2.0), 0, w, d), rng, rng.Next() < 0.65 ? Plans.Corridor : Plans.Open, front, style, Furnishes.Office);
                    // Behind it a row of houses back to back, or a car park, or bins and crates out the back; planters and trees on the sides.
                    var behind = Piece(new Lot { R = lot, Roads = roads }, Local(own, front, 0, d + 1, l, D - d - 1));
                    if (D - d - 1 >= 7) row.AddRange(CityLot(c, behind.R, CityPrograms.Houses, behind.Roads, rng, style));
                    else if (D - d >= 6) Parking(c, behind.R, rng, Opposite(front));
                    else if (D - d >= 3) GroupNear(c, LocalPt(own, front, (int)Math.Floor(l / 2.0), d + 1), rng, new IReadOnlyList<Part>[] { Dressed(Barricade, DecorKinds.Bin), Dressed(Pair, DecorKinds.Bin, DecorKinds.Crates) }, 2);
                    foreach (var u in new[] { 1, l - 2 }) if ((int)Math.Floor((l - w) / 2.0) >= 3) GroupNear(c, LocalPt(own, front, u, (int)Math.Floor(d / 2.0)), rng, new IReadOnlyList<Part>[] { Dressed(Pair, DecorKinds.Planter, DecorKinds.Tree), Dressed(Corner, DecorKinds.Planter, DecorKinds.Tree) }, 1);
                    var all = new List<Building> { b };
                    all.AddRange(row);
                    return all;
                }
                case CityPrograms.Warehouse:
                {
                    var w = Math.Min(style.Hall, L);
                    var d = Math.Min(style.Hall - 1, D);
                    if (w < 12 || d < 12) return CityLot(c, lot, CityPrograms.Office, roads, rng, style);
                    var (own, row) = Alongside(c, new Lot { R = lot, Roads = roads }, front, w, rng, style);
                    var l = SideLen(own, front);
                    // The hall sits at the back, its loading bay onto a yard of containers and crates.
                    var yard = D - d;
                    var b = MakeBuilding(c, Local(own, front, (int)Math.Floor((l - w) / 2.0), yard, w, d), rng, Plans.Hall, front, style, Furnishes.Store);
                    if (yard >= 5)
                    {
                        var box = Local(own, front, 1, 1, 2, 5);
                        var fits = TilesIn(Rect(box.X - 1, box.Y - 1, box.W + 2, box.H + 2)).All((p) => c.Free(p.X, p.Y) || !c.In(p.X, p.Y));
                        if (yard >= 7 && fits && rng.Next() < 0.6) Platform(c, box, rng, 1);
                        GroupNear(c, LocalPt(own, front, l - 4, 2), rng, new IReadOnlyList<Part>[] { Dressed(Crates, DecorKinds.Crates) }, 1);
                    }
                    else if (yard >= 3)
                    {
                        GroupNear(c, LocalPt(own, front, 2, 1), rng, new IReadOnlyList<Part>[] { Dressed(Crates, DecorKinds.Crates), Dressed(Barricade, DecorKinds.Crates) }, 1);
                    }
                    var all = new List<Building> { b };
                    all.AddRange(row);
                    return all;
                }
                case CityPrograms.Flats:
                {
                    var w = Clamp(L - 2, 8, 12);
                    var d = Clamp(D - 3, 7, 10);
                    if (w > L || d > D) return CityLot(c, lot, CityPrograms.Houses, roads, rng, style);
                    // A row beside them when the lot is long (the terrace then always looks onto the courtyard).
                    var (own, row) = D - d >= 2 ? Alongside(c, new Lot { R = lot, Roads = roads }, front, w + 2, rng, style) : (lot, new List<Building>());
                    var l = SideLen(own, front);
                    var u0 = (int)Math.Floor((l - w) / 2.0);
                    var b = Local(own, front, u0, 0, w, d);
                    // The terrace looks onto the courtyard behind, or onto a side passage.
                    var back = Opposite(front);
                    var terrace = D - d >= 2 ? back : u0 >= 2 ? (front + 1) % 4 : front;
                    Flats(c, b, rng, terrace);
                    // A hedge and a tree in the courtyard.
                    if (D - d >= 4) GroupNear(c, LocalPt(own, front, (int)Math.Floor(l / 2.0) + rng.Range(-3, 3), D - 2), rng, new IReadOnlyList<Part>[] { Dressed(Barricade, DecorKinds.Bush), Dressed(Pair, DecorKinds.Bush, DecorKinds.Tree) }, 1);
                    return row;
                }
                case CityPrograms.Plaza:
                    Plaza(c, lot, rng);
                    return new List<Building>();
                case CityPrograms.Parking:
                    Parking(c, lot, rng, front);
                    return new List<Building>();
                case CityPrograms.Shops:
                    return RowOfBuildings(c, lot, front, roads, rng, style, Plans.Shop, Furnishes.Shop);
                case CityPrograms.HighStreet:
                {
                    var row = HighStreet(c, lot, front, rng, style);
                    return row.Count > 0 ? row : CityLot(c, lot, CityPrograms.Houses, roads, rng, style);
                }
                case CityPrograms.GasStation:
                {
                    var station = GasStation(c, lot, front, rng, style);
                    return station.Count > 0 ? station : CityLot(c, lot, CityPrograms.Parking, roads, rng, style);
                }
                case CityPrograms.Motel:
                {
                    var rooms = Motel(c, lot, front, rng);
                    return rooms.Count > 0 ? rooms : CityLot(c, lot, CityPrograms.Houses, roads, rng, style);
                }
                case CityPrograms.Site:
                    Site(c, lot, front, rng);
                    return new List<Building>();
                case CityPrograms.Yard:
                    ContainerYard(c, lot, front, rng);
                    return new List<Building>();
                case CityPrograms.Houses:
                {
                    // Too small for the map's houses: a row of narrow shops, and only then a square.
                    var row = RowOfBuildings(c, lot, front, roads, rng, style, Plans.Rooms, Furnishes.Home);
                    if (row.Count > 0) return row;
                    var shops = HighStreet(c, lot, front, rng, style);
                    if (shops.Count == 0) Plaza(c, lot, rng);
                    return shops;
                }
            }
            // Never reached (every CityProgram has its case): TypeScript's switch falls out with undefined.
            return new List<Building>();
        }
    }
}
