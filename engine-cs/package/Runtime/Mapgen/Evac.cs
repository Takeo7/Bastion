#nullable enable
// mapgen.ts, from HOLE_MAX to buildingStoreys: what the squad can reach (flood, connect), the clutter thinned, the
// placing (cluster), the evacuation and its bus (carveEvac, parkBus, avenueEvac), the loot boxes and the storeys.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>The biggest hole in a wood grown over rather than joined to the rest.</summary>
        private const int HoleMax = 6;

        /// <summary>Walkable tiles reachable from `start`, following the movement rules.</summary>
        private static JsSet<int> Flood(Canvas c, Vec2 start, Func<int, bool>? within = null)
        {
            var s = c.AsState();
            var seen = new JsSet<int>(new[] { c.I(start.X, start.Y) });
            var queue = new List<Vec2> { start };
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs8)
                {
                    if (Paths.StepCost(s, p.X, p.Y, d.X, d.Y) == null) continue;
                    var q = V(p.X + d.X, p.Y + d.Y);
                    var i = c.I(q.X, q.Y);
                    if (seen.Has(i) || (within != null && !within(i))) continue;
                    seen.Add(i);
                    queue.Add(q);
                }
            }
            return seen;
        }

        /// <summary>One change that links tile `i` (out of reach) to the reachable area, applied at once; false if none.</summary>
        private static bool LinkTile(Canvas c, int i, JsSet<int> reach)
        {
            var x = i % c.Width;
            var y = (i - x) / c.Width;
            var level = c.Elev[i];
            bool Reached(Vec2 p) => c.In(p.X, p.Y) && reach.Has(c.I(p.X, p.Y));
            // A blocking object (or a wall: a new door), one or two tiles thick: wood, bush or clutter, never part of a lorry or a tower.
            bool Clearable(Vec2 p)
            {
                var k = c.Get(p.X, p.Y);
                // Off the map k is 'wall' (and decor[i(p)] another tile's, or past the end): false either way.
                if (k != TileKinds.Low && k != TileKinds.High) return false;
                var look = Decor.DecorOf(c.Decor[c.I(p.X, p.Y)]);
                return look != null ? Soft.Contains(look.Kind) : k == TileKinds.Low;
            }
            foreach (var d in Grid.Dirs4)
            {
                var n = V(x + d.X, y + d.Y);
                if (!c.In(n.X, n.Y)) continue;
                var kind = c.Get(n.X, n.Y);
                // A stranded rooftop gets a ladder from the floor one storey down.
                if (level > 0 && Reached(n) && kind == TileKinds.Floor && c.ElevAt(n.X, n.Y) == level - 1)
                {
                    c.Force(n.X, n.Y, TileKinds.Ladder);
                    return true;
                }
                if (c.ElevAt(n.X, n.Y) != level || Grid.IsWalkable(kind) || c.GroundAt(n.X, n.Y) == Out) continue;
                var beyond = Add(n, d);
                if (Reached(beyond) && c.ElevAt(beyond.X, beyond.Y) == level && (kind == TileKinds.Wall || Clearable(n)))
                {
                    c.Force(n.X, n.Y, kind == TileKinds.Wall ? TileKinds.Door : TileKinds.Floor);
                    return true;
                }
                var further = Add(beyond, d);
                if (Clearable(n) && Clearable(beyond) && Reached(further) && c.ElevAt(beyond.X, beyond.Y) == level && c.ElevAt(further.X, further.Y) == level)
                {
                    c.Force(n.X, n.Y, TileKinds.Floor);
                    c.Force(beyond.X, beyond.Y, TileKinds.Floor);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Deep in a grove: clears the fewest objects (trees, rocks, crates) on one
        /// level that open a way from `pocket` to the reachable area.
        /// </summary>
        private static bool Carve(Canvas c, JsSet<int> pocket, JsSet<int> reach)
        {
            var cost = new JsMap<int, int>();
            var prev = new JsMap<int, int>();
            // A sparse array in TypeScript (`buckets[k + step] ??= []`): its holes are nulls here.
            var buckets = new List<List<int>?> { new List<int>(pocket) };
            foreach (var i in pocket) cost.Set(i, 0);
            int? PrevOf(int t) => prev.TryGetValue(t, out var before) ? before : (int?)null;
            for (var k = 0; k < buckets.Count; k++)
            {
                var bucket = buckets[k] ?? new List<int>();
                for (var n = 0; n < bucket.Count; n++)
                {
                    var i = bucket[n];
                    if (!cost.TryGetValue(i, out var own) || own != k) continue;
                    var x = i % c.Width;
                    var y = (i - x) / c.Width;
                    foreach (var d in Grid.Dirs4)
                    {
                        if (!c.In(x + d.X, y + d.Y)) continue;
                        var j = c.I(x + d.X, y + d.Y);
                        if (c.Elev[j] != c.Elev[i]) continue;
                        var kind = c.Tiles[j];
                        if (reach.Has(j) && Grid.IsWalkable(kind))
                        {
                            for (int? t = i; t != null && !pocket.Has(t.Value); t = PrevOf(t.Value)) if (!Grid.IsWalkable(c.Tiles[t.Value])) c.Tiles[t.Value] = TileKinds.Floor;
                            return true;
                        }
                        // Through wood, bush and clutter first; through a lorry or a tower only if there is no other way; never
                        // through what is kept (the bus, what stands for the evacuation) nor the outline.
                        var look = Decor.DecorOf(c.Decor[j]);
                        var soft = look != null ? Soft.Contains(look.Kind) : kind == TileKinds.Low;
                        var kept = !Grid.IsWalkable(kind) && (c.Keep[j] || c.Ground[j] == Out);
                        var step = kept ? -1 : Grid.IsWalkable(kind) ? 0 : kind == TileKinds.Low || kind == TileKinds.High ? (soft ? 1 : 3) : -1;
                        if (step < 0 || (cost.TryGetValue(j, out var known) && known <= k + step)) continue;
                        cost.Set(j, k + step);
                        prev.Set(j, i);
                        while (buckets.Count <= k + step) buckets.Add(null);
                        var into = buckets[k + step];
                        if (into == null) buckets[k + step] = into = new List<int>();
                        into.Add(j);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Opens up every walkable pocket the squad could not reach: a ladder for a
        /// stranded rooftop, a cleared object or a new door for a sealed ground area,
        /// a path cut through a thick grove. Each pass fixes every pocket it can.
        /// </summary>
        private static JsSet<int> Connect(Canvas c, Vec2 start)
        {
            for (var pass = 0; pass < 30; pass++)
            {
                var reach = Flood(c, start);
                var done = new JsSet<int>();
                var fixed_ = false;
                for (var i = 0; i < c.Tiles.Count; i++)
                {
                    if (reach.Has(i) || done.Has(i) || !Grid.IsWalkable(c.Tiles[i])) continue;
                    var x = i % c.Width;
                    var pocket = Flood(c, V(x, (i - x) / c.Width), (j) => !reach.Has(j));
                    foreach (var j in pocket) done.Add(j);
                    if (pocket.Any((j) => LinkTile(c, j, reach)) || Carve(c, pocket, reach)) fixed_ = true;
                }
                if (!fixed_) return reach;
            }
            return Flood(c, start);
        }

        /// <summary>Loose cover the XCOM days left everywhere (playtest 2026-10-14, #25): crates, barricades, bins and planters out in the streets and yards.</summary>
        private static readonly HashSet<string> Clutter = new HashSet<string> { DecorKinds.Crates, DecorKinds.Barricade, DecorKinds.Bin, DecorKinds.Planter };
        /// <summary>The share of those groups that stays: not all of them go.</summary>
        private const double ClutterKept = 0.5;

        /// <summary>
        /// Takes away half of the loose clutter outdoors, a whole group at a time (the
        /// pieces touching each other), on its own seed. Cars, trees, walls and what is
        /// indoors or up on a deck stay.
        /// </summary>
        private static void ThinClutter(Canvas c, int seed)
        {
            var rng = new Rng(seed ^ 0x7468696e);
            bool Loose(int i) =>
                (c.Tiles[i] == TileKinds.Low || c.Tiles[i] == TileKinds.High) && c.Ground[i] != Indoor && c.Elev[i] == 0 && Clutter.Contains(Decor.DecorOf(c.Decor[i])?.Kind ?? DecorKinds.Tree);
            var seen = new JsSet<int>();
            for (var i = 0; i < c.Tiles.Count; i++)
            {
                if (seen.Has(i) || !Loose(i)) continue;
                var group = new List<int> { i };
                seen.Add(i);
                for (var k = 0; k < group.Count; k++)
                {
                    var x = group[k] % c.Width;
                    var y = (int)Math.Floor((double)group[k] / c.Width);
                    foreach (var d in Grid.Dirs8)
                    {
                        if (!c.In(x + d.X, y + d.Y)) continue;
                        var j = c.I(x + d.X, y + d.Y);
                        if (!seen.Has(j) && Loose(j))
                        {
                            seen.Add(j);
                            group.Add(j);
                        }
                    }
                }
                if (rng.Next() < ClutterKept) continue;
                foreach (var j in group)
                {
                    c.Force(j % c.Width, (int)Math.Floor((double)j / c.Width), TileKinds.Floor);
                    c.Decor[j] = 0;
                }
            }
        }

        /// <summary>
        /// Decor stays only on what is still cover (making room and opening ways
        /// through clears some of it), and an object over several tiles only whole
        /// (tidyObjects).
        /// </summary>
        private static void TidyDecor(Canvas c)
        {
            TidyObjects(c);
            for (var i = 0; i < c.Tiles.Count; i++) if (c.Tiles[i] != TileKinds.Low && c.Tiles[i] != TileKinds.High) c.Decor[i] = 0;
        }

        // ---------------------------------------------------------------- placing

        /// <summary>Standing tiles near `anchor` (breadth-first, same level), excluding `taken`.</summary>
        private static List<Vec2> Cluster(Canvas c, JsSet<int> reach, Vec2 anchor, int count, JsSet<int> taken)
        {
            var level = c.ElevAt(anchor.X, anchor.Y);
            var out_ = new List<Vec2>();
            var seen = new JsSet<int>(new[] { c.I(anchor.X, anchor.Y) });
            var queue = new List<Vec2> { anchor };
            // queue.shift(): read from the front by an index.
            var head = 0;
            while (head < queue.Count && out_.Count < count)
            {
                var p = queue[head++];
                var i = c.I(p.X, p.Y);
                if (reach.Has(i) && Grid.CanStandOn(c.Tiles[i]) && !taken.Has(i) && c.Elev[i] == level) out_.Add(p);
                foreach (var d in Grid.Dirs8)
                {
                    var q = V(p.X + d.X, p.Y + d.Y);
                    if (!c.In(q.X, q.Y) || seen.Has(c.I(q.X, q.Y)) || Dist(q, anchor) > 3) continue;
                    seen.Add(c.I(q.X, q.Y));
                    if (Grid.IsWalkable(c.Tiles[c.I(q.X, q.Y)])) queue.Add(q);
                }
            }
            return out_;
        }

        /// <summary>Clears a 3×3 evacuation zone at ground level, outdoors, in `r`, as close to `corner` as it can.</summary>
        private static List<Vec2> CarveEvac(Canvas c, Rect r, Vec2 corner, Rng rng)
        {
            var options = new List<(int X, int Y, double Cost)>();
            for (var y = r.Y; y <= r.Y + r.H - 3; y++)
            {
                for (var x = r.X; x <= r.X + r.W - 3; x++)
                {
                    var cost = 0;
                    for (var dy = 0; dy < 3 && cost < 99; dy++)
                    {
                        for (var dx = 0; dx < 3; dx++)
                        {
                            var kind = c.Get(x + dx, y + dy);
                            if (!c.In(x + dx, y + dy) || c.ElevAt(x + dx, y + dy) != 0 || c.GroundAt(x + dx, y + dy) == Indoor || (kind != TileKinds.Floor && kind != TileKinds.Low && kind != TileKinds.High)) cost = 99;
                            else if (kind != TileKinds.Floor) cost++;
                        }
                    }
                    if (cost >= 99) continue;
                    // Out in the open rather than squeezed between buildings.
                    var cramped = 0;
                    for (var dy = -1; dy <= 3; dy++) for (var dx = -1; dx <= 3; dx++) if (c.In(x + dx, y + dy) && !Grid.IsWalkable(c.Get(x + dx, y + dy))) cramped++;
                    options.Add((x, y, cost + cramped * 0.3 + Dist(V(x + 1, y + 1), corner) * 0.08 + rng.Next() * 0.3));
                }
            }
            var pick = BestOf(options, (o) => o.Cost);
            if (pick == null) return new List<Vec2>();
            var zone = new List<Vec2>();
            for (var dy = 0; dy < 3; dy++)
            {
                for (var dx = 0; dx < 3; dx++)
                {
                    c.Force(pick.Value.X + dx, pick.Value.Y + dy, TileKinds.Floor);
                    c.Reserve(pick.Value.X + dx, pick.Value.Y + dy);
                    zone.Add(V(pick.Value.X + dx, pick.Value.Y + dy));
                }
            }
            return zone;
        }

        /// <summary>Tiles of map played for each special loot box, and the most a map has (docs/FASE8.md §B; docs/MAPAS.md §6: four on a town of 64).</summary>
        private const int TilesPerBox = 750;
        private const int MaxBoxes = 4;

        /// <summary>
        /// The special loot boxes (docs/FASE8.md §B): up to MAX_BOXES, one per
        /// TILES_PER_BOX of map, each of a different kind, inside buildings, far from
        /// the squad's corner and from each other, where they close no way (group).
        /// On a seed of their own, so the rest of the layout doesn't move.
        /// </summary>
        private static void PlaceLootBoxes(Canvas c, int seed)
        {
            var rng = new Rng(seed ^ 0x626f78);
            var count = Math.Min(MaxBoxes, (int)Math.Floor((double)c.Ground.Count((g) => g != Out) / TilesPerBox));
            var kinds = Shuffle(new List<string> { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood }, rng).Take(count).ToList();
            // Inside buildings first; where there aren't enough (the outskirts, a facility), out in yards and lots.
            var open = Enumerable.Range(0, c.Tiles.Count).Select((i) => V(i % c.Width, (int)Math.Floor((double)i / c.Width))).Where(
                (p) => c.Get(p.X, p.Y) == TileKinds.Floor && c.Free(p.X, p.Y) && Dist(p, c.Squad) >= 14
            ).ToList();
            var spots = new List<Vec2>();
            spots.AddRange(Shuffle(open.Where((p) => c.GroundAt(p.X, p.Y) == Indoor).ToList(), rng));
            spots.AddRange(Shuffle(open.Where((p) => c.GroundAt(p.X, p.Y) != Indoor).ToList(), rng));
            // Medicines only where medicines are kept (playtest 2026-10-15, #15): a shop or a lab, now and then a home; never in the street.
            // (A ruin's ground is out of doors: no shop, no lab, no home any more.)
            string? FurnishAt(Vec2 p) => c.Buildings.FirstOrDefault((b) => !b.Ruined && InRect(b.Outer, p))?.Furnish;
            var homeToo = rng.Int(5) == 0;
            bool Fits(string kind, Vec2 p)
            {
                if (kind != DecorKinds.LootMedical) return true;
                var f = FurnishAt(p);
                return f == Furnishes.Shop || f == Furnishes.Lab || (homeToo && f == Furnishes.Home);
            }
            var placed = new List<Vec2>();
            foreach (var kind in kinds)
            {
                var spot = spots.FirstOrDefault((p) => Fits(kind, p) && placed.All((q) => Dist(p, q) >= 12) && Group(c, p, new[] { P(0, 0, TileKinds.Low, new Look(kind)) }));
                if (spot != null) placed.Add(spot);
            }
        }

        /// <summary>
        /// The bus: two tiles wide, five long, what La Mula covers drawn (Arte, 16/10:
        /// 1.52 × 5.12 tiles). It was eight long, and past its ends stood a wall
        /// nobody could see (playtest 16/10 #22).
        /// </summary>
        private const int BusW = 2;
        private const int BusL = 5;

        /// <summary>
        /// Parks the bus beside the 3 × 3 evac `zone` (docs/PRUEBA-2026-10-14.md P1):
        /// along one of its sides, out in the open, its long side to the zone (the
        /// doors), on open ground only. It is solid (`high`, decor 'bus') and drives
        /// off along its length toward the nearer map edge, by its `lane` (19/10: it
        /// drove through what stood there): open ground out to the edge, where only
        /// what can be cleared stands (it is cleared, and kept clear); the client
        /// leaves the edge open there (dressing.ts dressEdges). Null if no side has
        /// room.
        /// </summary>
        private static BusPlace? ParkBus(Canvas c, List<Vec2> zone, Rng rng)
        {
            // Math.min(...[]) is Infinity: no side touches an empty zone (NaN >= 3), none, and no draw.
            if (zone.Count == 0) return null;
            var x0 = zone.Min((p) => p.X);
            var y0 = zone.Min((p) => p.Y);
            // North, south (lying along x) and west, east (along y), beside the zone. Centred on it if it fits;
            // else slid along that side (a zone in a corner leaves no room on the map's side of it).
            var sides = new List<(Rect R, bool AlongX, int Slide)>();
            for (var slide = 0; slide <= BusL - 1; slide++)
            {
                foreach (var shift in slide != 0 ? new[] { slide, -slide } : new[] { 0 })
                {
                    var lead = (int)Math.Floor((BusL - 3) / 2.0) + shift;
                    sides.Add((Rect(x0 - lead, y0 - BusW, BusL, BusW), true, slide));
                    sides.Add((Rect(x0 - lead, y0 + 3, BusL, BusW), true, slide));
                    sides.Add((Rect(x0 - BusW, y0 - lead, BusW, BusL), false, slide));
                    sides.Add((Rect(x0 + 3, y0 - lead, BusW, BusL), false, slide));
                }
            }
            // The bus still has to touch the zone along at least three tiles (its doors open onto it).
            bool Touches(Rect r, bool alongX) => (alongX ? Math.Min(r.X + r.W, x0 + 3) - Math.Max(r.X, x0) : Math.Min(r.Y + r.H, y0 + 3) - Math.Max(r.Y, y0)) >= 3;
            bool Fits(Rect r) =>
                TilesIn(r).All((p) => c.In(p.X, p.Y) && c.ElevAt(p.X, p.Y) == 0 && c.GroundAt(p.X, p.Y) != Indoor && c.Free(p.X, p.Y) && new[] { TileKinds.Floor, TileKinds.Low, TileKinds.High }.Contains(c.Get(p.X, p.Y)));
            // Off toward the nearer end of the map along its length.
            int HeadingOf(Rect r, bool alongX)
            {
                var mid = Centre(r);
                return alongX ? (mid.X < c.Width / 2.0 ? 3 : 1) : mid.Y < c.Height / 2.0 ? 0 : 2;
            }
            // Its way out: the ground from its nose to the map's edge, as wide as it.
            List<Vec2> LaneOf(Rect r, bool alongX)
            {
                switch (HeadingOf(r, alongX))
                {
                    case 0:
                        return TilesIn(Rect(r.X, 0, r.W, r.Y));
                    case 1:
                        return TilesIn(Rect(r.X + r.W, r.Y, c.Width - r.X - r.W, r.H));
                    case 2:
                        return TilesIn(Rect(r.X, r.Y + r.H, r.W, c.Height - r.Y - r.H));
                    default:
                        return TilesIn(Rect(0, r.Y, r.X, r.H));
                }
            }
            // A parked car (any of its tiles), as a whole.
            List<int>? Car(Vec2 p)
            {
                var o = Decor.ObjectAt(c.Decor, c.Width, c.I(p.X, p.Y));
                return o != null && (o.Kind == DecorKinds.CarFront || o.Kind == DecorKinds.CarRear) ? o.Tiles : null;
            }
            // What can be cleared out of its way: open ground's clutter, trees, a parked car (whole).
            bool Clearable(Vec2 p)
            {
                var kind = c.Get(p.X, p.Y);
                if (kind == TileKinds.Floor) return true;
                if ((kind != TileKinds.Low && kind != TileKinds.High) || c.Keep[c.I(p.X, p.Y)]) return false;
                var look = Decor.DecorOf(c.Decor[c.I(p.X, p.Y)]);
                return look == null || Soft.Contains(look.Kind) || Car(p) != null;
            }
            bool LaneOpen(Rect r, bool alongX) => LaneOf(r, alongX).All((p) => c.ElevAt(p.X, p.Y) == 0 && c.GroundAt(p.X, p.Y) != Indoor && Clearable(p));
            var options = sides
                .Where((o) => Touches(o.R, o.AlongX) && Fits(o.R))
                .Select((o) => (o.R, o.AlongX, o.Slide, Cost: TilesIn(o.R).Count((p) => c.Get(p.X, p.Y) != TileKinds.Floor) + o.Slide * 0.3 + rng.Next() * 0.5))
                .ToList();
            // With its way out open if it can be; else as before, what stands in its way cleared where it can be.
            var out_ = options.Where((o) => LaneOpen(o.R, o.AlongX)).ToList();
            var pick = BestOf(out_.Count > 0 ? out_ : options, (o) => o.Cost);
            if (pick == null) return null;
            var chosen = pick.Value;
            var tiles = TilesIn(chosen.R);
            foreach (var p in tiles)
            {
                c.Force(p.X, p.Y, TileKinds.High);
                c.Dress(p, DecorKinds.Bus);
                c.Reserve(p.X, p.Y);
            }
            var lane = LaneOf(chosen.R, chosen.AlongX).Where((p) => c.ElevAt(p.X, p.Y) == 0 && c.GroundAt(p.X, p.Y) != Indoor && Clearable(p)).ToList();
            foreach (var p in lane)
            {
                var i = c.I(p.X, p.Y);
                // A car partly in the way goes whole.
                foreach (var j in Car(p) ?? new List<int> { i })
                {
                    c.Tiles[j] = TileKinds.Floor;
                    c.Decor[j] = 0;
                }
                c.Reserve(p.X, p.Y);
            }
            return new BusPlace { Tiles = tiles, Heading = HeadingOf(chosen.R, chosen.AlongX), Lane = lane };
        }

        /// <summary>A town's bus (docs/MAPAS.md §4.5): two tiles wide and nine long, as long as La Lata's bus is drawn.</summary>
        private static readonly IReadOnlyList<int> TownBus = new[] { 2, 9 };
        /// <summary>The evacuation beside it, along its doors: three wide (the kerb's lane and the pavement), seven long (§4.5).</summary>
        private static readonly IReadOnlyList<int> EvacStrip = new[] { 3, 7 };
        /// <summary>The walk to the evacuation from where the squad starts and from the objective (docs/MAPAS.md §5.7).</summary>
        private static readonly IReadOnlyList<int> EvacWalk = new[] { 40, 60 };

        /// <summary>Steps on foot (the movement rules, every way) from `from`, a tile or several, to every tile; -1 where it can't.</summary>
        private static int[] StepsFrom(Canvas c, List<Vec2> from)
        {
            var s = c.AsState();
            var steps = new int[c.Tiles.Count];
            for (var n = 0; n < steps.Length; n++) steps[n] = -1;
            var queue = new List<int>();
            foreach (var p in from)
            {
                if (!c.In(p.X, p.Y) || steps[c.I(p.X, p.Y)] >= 0) continue;
                steps[c.I(p.X, p.Y)] = 0;
                queue.Add(c.I(p.X, p.Y));
            }
            for (var k = 0; k < queue.Count; k++)
            {
                var i = queue[k];
                var x = i % c.Width;
                var y = (i - x) / c.Width;
                foreach (var d in Grid.Dirs8)
                {
                    if (!c.In(x + d.X, y + d.Y)) continue;
                    var j = c.I(x + d.X, y + d.Y);
                    if (steps[j] >= 0 || Paths.StepCost(s, x, y, d.X, d.Y) == null) continue;
                    steps[j] = steps[i] + 1;
                    queue.Add(j);
                }
            }
            return steps;
        }

        /// <summary>
        /// A town's evacuation (docs/MAPAS.md §4.5, §5.5): the bus parked against the kerb on its right, facing the way out of
        /// a street open to the edge (it leaves by it), on that street or on the avenue that runs straight out by it; the
        /// evacuation along its doors, the kerb's lane and the pavement, on a straight stretch clear of crossings. Where it is
        /// EVAC_WALK on foot from the squad and from the objective, as near as it can be. Whatever stands there (a parked car,
        /// a planter, a tree, a bench) goes, whole; so does what stands in the bus's way out to the edge. Null without a way
        /// out five wide or more but the squad's, or room on it.
        /// </summary>
        private static (List<Vec2> Zone, BusPlace Bus)? AvenueEvac(Canvas c, Rect objective, Rng rng, bool relaxed = false)
        {
            var fromSquad = StepsFrom(c, new List<Vec2> { c.Squad });
            var fromObjective = StepsFrom(c, TilesIn(objective).Where((p) => c.In(p.X, p.Y) && Grid.IsWalkable(c.Get(p.X, p.Y))).ToList());
            // Whatever stands on `p` that may go: a car, a planter, a tree, a bench (whole); null if nothing can stand there.
            // (An empty list, for bare floor, is truthy in TypeScript: tests are `!= null` here.)
            List<int>? Clearable(Vec2 p)
            {
                if (!c.In(p.X, p.Y) || c.ElevAt(p.X, p.Y) != 0) return null;
                var i = c.I(p.X, p.Y);
                if (c.Ground[i] == Out || c.Ground[i] == Indoor) return null;
                var kind = c.Tiles[i];
                // A road kept clear (a country track) is still the bus's to stand on.
                if (kind == TileKinds.Floor) return c.Keep[i] && c.Ground[i] != Road ? null : new List<int>();
                if (kind != TileKinds.Low && kind != TileKinds.High) return null;
                var o = Decor.ObjectAt(c.Decor, c.Width, i);
                if (o == null) return kind == TileKinds.Low ? new List<int> { i } : null;
                return Soft.Contains(o.Kind) || o.Kind == DecorKinds.CarFront || o.Kind == DecorKinds.CarRear || o.Kind == DecorKinds.Bench ? o.Tiles : null;
            }
            int Off(int n) => n < EvacWalk[0] ? EvacWalk[0] - n : n > EvacWalk[1] ? n - EvacWalk[1] : 0;
            // type Option = { cost; tiles; zone; verge; heading; ahead }
            var options = new List<(double Cost, List<Vec2> Tiles, List<Vec2> Zone, List<Vec2> Verge, int Heading, Func<List<Vec2>> Ahead)>();
            foreach (var exit in c.Exits)
            {
                var r = exit.Road.R;
                var vertical = exit.Road.Vertical;
                var width = vertical ? r.W : r.H;
                // A street of three or four is no way out for it (but when nothing else is: `relaxed`); a country track or road of two or three is.
                if (width < StreetWide && c.Lines.Count > 1 && !(relaxed && width >= 3)) continue;
                var heading = exit.Side;
                var a0 = vertical ? r.X : r.Y;
                // The street straight on from its way out: every stretch in line with it, joined.
                bool LineOf(PlanRoad road) => road.Vertical == vertical && (vertical ? road.R.X == r.X && road.R.W == r.W : road.R.Y == r.Y && road.R.H == r.H);
                var spans = c.Lines.Where(LineOf).Select((road) => vertical ? (road.R.Y, road.R.Y + road.R.H) : (road.R.X, road.R.X + road.R.W)).ToList();
                var t0 = vertical ? r.Y : r.X;
                var t1 = vertical ? r.Y + r.H : r.X + r.W;
                for (var grew = true; grew;)
                {
                    grew = false;
                    foreach (var (s0, s1) in spans)
                    {
                        if (s0 < t0 && s1 >= t0)
                        {
                            t0 = s0;
                            grew = true;
                        }
                        if (s1 > t1 && s0 <= t1)
                        {
                            t1 = s1;
                            grew = true;
                        }
                    }
                }
                // Keeping right: facing north or east, the kerb on its right is the far one across. On a street, by the kerb, the
                // evacuation the kerb's lane and the pavement; on a country track, on it, the evacuation the verge beside it. Out
                // of town (a road or two), on either side: no traffic to keep to the right of.
                var wide = width >= StreetWide;
                var right = heading == 0 || heading == 1;
                foreach (var high in wide && c.Lines.Count > 2 && !relaxed ? new[] { right } : new[] { right, !right })
                {
                    var busLanes = wide ? (high ? new[] { a0 + width - 3, a0 + width - 2 } : new[] { a0 + 1, a0 + 2 }) : high ? new[] { a0 + width - 2, a0 + width - 1 } : new[] { a0, a0 + 1 };
                    var stripLanes = wide ? (high ? new[] { a0 + width - 1, a0 + width, a0 + width + 1 } : new[] { a0 - 2, a0 - 1, a0 }) : high ? new[] { a0 + width, a0 + width + 1, a0 + width + 2 } : new[] { a0 - 3, a0 - 2, a0 - 1 };
                    var kerb = wide ? stripLanes[high ? 0 : 2] : -1;
                    Vec2 At(int a, int t) => vertical ? V(a, t) : V(t, a);
                    var forward = heading == 0 || heading == 3 ? -1 : 1;
                    var end = forward < 0 ? 0 : vertical ? c.Height : c.Width;
                    for (var t = t0 + 1; t + TownBus[1] < t1 - 1; t++)
                    {
                        var tiles = busLanes.SelectMany((a) => Enumerable.Range(0, TownBus[1]).Select((k) => At(a, t + k))).ToList();
                        var zone = stripLanes.SelectMany((a) => Enumerable.Range(0, EvacStrip[1]).Select((k) => At(a, t + 1 + k))).ToList();
                        if (!tiles.All((p) => Clearable(p) != null && c.GroundAt(p.X, p.Y) == Road)) continue;
                        // Clear of crossings: past the kerb's lane, pavement all along.
                        if (!zone.All((p) => Clearable(p) != null && ((vertical ? p.X : p.Y) == kerb || c.GroundAt(p.X, p.Y) != Road))) continue;
                        // Its way out, on the street all the way to the edge.
                        var nose = forward < 0 ? t - 1 : t + TownBus[1];
                        List<Vec2> Way(int k) => busLanes.Select((a) => At(a, k)).ToList();
                        var open = true;
                        for (var k = nose; open && k != end; k += forward) open = Way(k).All((p) => Clearable(p) != null && c.GroundAt(p.X, p.Y) == Road);
                        if (!open) continue;
                        // On a track it fills, the way on past its nose is joined to the rest only by the verge beside its front: open
                        // ground there, or nothing reaches it and it is shut with the trees round it (sealPockets), the bus facing them.
                        var verge = width <= busLanes.Length ? new[] { nose - forward, nose }.Select((k) => At(stripLanes[high ? 0 : 2], k)).ToList() : new List<Vec2>();
                        if (!verge.All((p) => Clearable(p) != null || (c.In(p.X, p.Y) && c.Get(p.X, p.Y) == TileKinds.Floor && c.GroundAt(p.X, p.Y) != Out && c.ElevAt(p.X, p.Y) == 0))) continue;
                        var mid = zone[(int)Math.Floor(zone.Count / 2.0)];
                        var a = fromSquad[c.I(mid.X, mid.Y)];
                        var b = fromObjective[c.I(mid.X, mid.Y)];
                        if (a < 0 || b < 0) continue;
                        options.Add((Off(a) * 2 + Off(b) * 2 + Math.Abs(a - b) * 0.1 + rng.Next(), tiles, zone, verge, heading, () =>
                        {
                            var lane = new List<Vec2>();
                            for (var k = nose; k != end; k += forward) lane.AddRange(Way(k));
                            return lane;
                        }));
                    }
                }
            }
            var pick = BestOf(options, (o) => o.Cost);
            if (pick == null) return null;
            var chosen = pick.Value;
            void Clear(Vec2 p)
            {
                foreach (var j in Clearable(p) ?? new List<int>())
                {
                    c.Tiles[j] = TileKinds.Floor;
                    c.Decor[j] = 0;
                }
            }
            foreach (var p in chosen.Tiles.Concat(chosen.Zone).Concat(chosen.Verge).ToList()) Clear(p);
            foreach (var p in chosen.Tiles)
            {
                c.Force(p.X, p.Y, TileKinds.High);
                c.Dress(p, DecorKinds.Bus, chosen.Heading);
                c.Reserve(p.X, p.Y);
            }
            foreach (var p in chosen.Zone.Concat(chosen.Verge).ToList())
            {
                c.Force(p.X, p.Y, TileKinds.Floor);
                c.Reserve(p.X, p.Y);
            }
            var lane = chosen.Ahead();
            foreach (var p in lane)
            {
                Clear(p);
                c.Reserve(p.X, p.Y);
            }
            return (chosen.Zone, new BusPlace { Tiles = chosen.Tiles, Heading = chosen.Heading, Lane = lane });
        }

        // ------------------------------------------------------- storeys and lights

        /// <summary>
        /// How tall each building stands, in storeys (docs/ALTURA-Y-NOCHE.md), by the
        /// rule the client drew them with: in town, shops and offices two or three,
        /// some houses two; halls, kiosks, motels and everything out of town, one.
        /// Every tile of a building (walls, doors, windows, the inside) gets its
        /// storeys; a wall two buildings share, the taller one's. Rolled on a seed of
        /// their own, so nothing else in the layout moves.
        /// </summary>
        private static List<int> StoreysOf(Canvas c, List<int> heights)
        {
            var out_ = Enumerable.Repeat(0, c.Tiles.Count).ToList();
            for (var j = 0; j < c.Buildings.Count; j++)
            {
                var b = c.Buildings[j];
                foreach (var p in TilesIn(b.Outer)) if (c.In(p.X, p.Y)) out_[c.I(p.X, p.Y)] = Math.Max(out_[c.I(p.X, p.Y)], heights[j]);
            }
            return out_;
        }

        /// <summary>How many storeys each building of c.buildings stands (storeysOf's rule), on its own seed.</summary>
        private static List<int> BuildingStoreys(Canvas c, string biome, int seed)
        {
            var rng = new Rng(seed ^ 0x5f0e75);
            return c.Buildings.Select((b) =>
            {
                var roll = rng.Next();
                return
                    b.Storeys ??
                    (biome != Biomes.City ? 1 : b.Furnish == Furnishes.Shop ? (roll < 0.55 ? 2 : 3) : b.Furnish == Furnishes.Office ? (roll < 0.6 ? 2 : 3) : b.Furnish == Furnishes.Home && roll < 0.3 ? 2 : 1);
            }).ToList();
        }
    }
}
