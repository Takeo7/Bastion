#nullable enable
// mapgen.ts in C#, its part from `interface Storey` (Core.cs) to upperFloors: the storeys above the ground.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        // ------------------------------------------------------ storeys above the ground

        // docs/FASE6-PLAN.md, step 5. Every building gets its storeys for real, on
        // canvases of their own laid over the ground: walls, rooms, windows, furniture
        // and stairs up from the floor below; on top, its roof. Houses and barns have
        // a pitched roof nobody stands on (decision 2); the rest a flat one, with a
        // parapet, reached by the stairs when the building has two or three storeys,
        // and by a ladder up its side on about half of those of one (decision 1). A
        // ruin has no roof. Drawn on a seed of its own, after everything else: the
        // ground only changes where stairs and ladders go.

        // interface Storey { c; slab } is in Core.cs (Storey).

        /// <summary>A pitched roof nobody stands on: houses and barns (decision 2).</summary>
        private static bool Pitched(Building b) => !b.Flat && (b.Furnish == Furnishes.Home || b.Furnish == Furnishes.Barn);

        /// <summary>What the rooms of a building's upper storeys hold: a shop's stockroom, the rest as below.</summary>
        private static readonly IReadOnlyDictionary<string, string> Upstairs = new Dictionary<string, string>
        {
            [Furnishes.Home] = Furnishes.Home,
            [Furnishes.Shop] = Furnishes.Store,
            [Furnishes.Office] = Furnishes.Office,
            [Furnishes.Lab] = Furnishes.Lab,
            [Furnishes.Store] = Furnishes.Store,
            [Furnishes.Barn] = Furnishes.Barn,
            [Furnishes.Bare] = Furnishes.Bare,
            [Furnishes.Clinic] = Furnishes.Clinic,
        };

        /// <summary>From one window to the next on an upper storey: shops and offices glazed, houses less.</summary>
        private static int UpstairsWindows(Building b) => b.Furnish == Furnishes.Home ? 3 : 2;

        /// <summary>
        /// Windows along the outer walls of storey `lc` of building `b`, like windows()
        /// on the ground: never at a corner, beside another opening or where a wall
        /// meets it from inside, and never onto another building's storey (`occupied`).
        /// </summary>
        private static void UpperWindows(Canvas lc, Rect b, int every, bool[] occupied)
        {
            for (var s = 0; s < 4; s++)
            {
                var n = SideLen(b, s);
                var anchor = (int)Math.Floor((n - 1) / 2.0);
                var back = Grid.Dirs4[Opposite(s)];
                var lateral = Grid.Dirs4[(s + 1) % 4];
                for (var u = 2; u <= n - 3; u++)
                {
                    if (Math.Abs(u - anchor) % every != 0) continue;
                    var p = LocalPt(b, s, u, 0);
                    var q = Add(p, Grid.Dirs4[s]);
                    if (lc.Get(p.X, p.Y) != TileKinds.Wall || lc.Get(p.X + back.X, p.Y + back.Y) != TileKinds.Floor) continue;
                    if (!new[] { Add(p, lateral), V(p.X - lateral.X, p.Y - lateral.Y) }.All((r) => lc.Get(r.X, r.Y) == TileKinds.Wall || lc.Get(r.X, r.Y) == TileKinds.Window)) continue;
                    if (!lc.In(q.X, q.Y) || occupied[lc.I(q.X, q.Y)]) continue;
                    lc.Force(p.X, p.Y, TileKinds.Window);
                }
            }
        }

        /// <summary>
        /// Whether every place one can stand on, on storey `st` within `r`, joins up
        /// with `from` (orthogonal steps; windows are no way through).
        /// </summary>
        private static bool StoreyJoined(Storey st, Rect r, Vec2 from)
        {
            // st.slab[i] past its ends is undefined in TypeScript: false.
            bool Open(Vec2 p) => InRect(r, p) && Js.At(st.Slab, st.C.I(p.X, p.Y), false) && Grid.IsWalkable(st.C.Get(p.X, p.Y)) && st.C.Get(p.X, p.Y) != TileKinds.Window;
            if (!Open(from)) return false;
            var seen = new JsSet<int>(new[] { st.C.I(from.X, from.Y) });
            var queue = new List<Vec2> { from };
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs4)
                {
                    var q = Add(p, d);
                    if (!Open(q) || seen.Has(st.C.I(q.X, q.Y))) continue;
                    seen.Add(st.C.I(q.X, q.Y));
                    queue.Add(q);
                }
            }
            return TilesIn(r).All((p) => !Open(p) || seen.Has(st.C.I(p.X, p.Y)));
        }

        /// <summary>
        /// Clears the furniture that cuts storey `st` of building `r` in two, nearest
        /// to what `from` reaches first, until every place one can stand on there joins
        /// up again (furnishRoom keeps doorways clear, not narrow rooms).
        /// </summary>
        private static void UnblockStorey(Storey st, Rect r, Vec2 from)
        {
            for (var guard = 0; guard < 20 && !StoreyJoined(st, r, from); guard++)
            {
                var blocked = TilesIn(Inset(r, 1)).FirstOrDefault((p) =>
                {
                    var k = st.C.Get(p.X, p.Y);
                    return (k == TileKinds.Low || k == TileKinds.High) && Grid.Dirs4.Any((d) => Grid.IsWalkable(st.C.Get(p.X + d.X, p.Y + d.Y)));
                });
                if (blocked == null) return;
                // A bed or a table over several tiles goes whole.
                ClearObject(st.C, blocked);
            }
        }

        /// <summary>Clear only clinical cover at the edge of an isolated pocket, including after a stairwell is opened above it.</summary>
        private static void ClearMedicalAisles(Storey st, Rect r, Vec2 from)
        {
            var c = st.C;
            var cells = TilesIn(Inset(r, 1));
            bool Open(Vec2 p) => InRect(r, p) && Js.At(st.Slab, c.I(p.X, p.Y), false) && Grid.IsWalkable(c.Get(p.X, p.Y)) && c.Get(p.X, p.Y) != TileKinds.Window;
            for (var guard = 0; guard < r.W * r.H; guard++)
            {
                var seen = new JsSet<int>();
                var queue = new List<Vec2> { from };
                while (queue.Count > 0)
                {
                    var p = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);
                    var i = c.I(p.X, p.Y);
                    if (seen.Has(i) || !Open(p)) continue;
                    seen.Add(i);
                    foreach (var d in Grid.Dirs4) queue.Add(Add(p, d));
                }
                var isolated = cells.Where((p) => Open(p) && !seen.Has(c.I(p.X, p.Y))).ToList();
                if (isolated.Count == 0) return;
                var cover = cells.Where((p) => Js.At(st.Slab, c.I(p.X, p.Y), false) && (c.Get(p.X, p.Y) == TileKinds.Low || c.Get(p.X, p.Y) == TileKinds.High) && Grid.Dirs4.Any((d) => seen.Has(c.I(p.X + d.X, p.Y + d.Y)))).ToList();
                // isolated is never empty here: Math.min over it is a number.
                var pick = Best(cover, (p) => isolated.Min((q) => Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y)));
                if (pick == null) return;
                ClearObject(c, pick);
            }
        }

        /// <summary>A flight of stairs placeStairs laid: its tiles (the lowest first), the way it climbs, its landing and its corridor.</summary>
        private sealed class LaidFlight
        {
            public readonly List<Vec2> Tiles;
            public readonly int Up;
            public readonly Vec2 Landing;
            /// <summary>The side (a DIRS4 index) of the flight its box's other column lies on: the corridor beside it, or the next flight.</summary>
            public readonly int Side;

            public LaidFlight(List<Vec2> tiles, int up, Vec2 landing, int side)
            {
                Tiles = tiles;
                Up = up;
                Landing = landing;
                Side = side;
            }
        }

        /// <summary>How long a flight of stairs is (docs/MAPAS.md §4.4; Arturo, 19/10): 3 tiles, 2 in a small house.</summary>
        private const int Flight = 3;
        private const int ShortFlight = 2;
        /// <summary>A building this narrow inside or narrower is a small one: a flight of 2 where 3 has no room, always in a house.</summary>
        private const int SmallBuilding = 5;

        /// <summary>
        /// The lengths the flights of building `b`'s stairs may have, the first one
        /// that fits (docs/MAPAS.md §4.4: «2 donde no cabe»): 3; 2 in a small house,
        /// and where 3 has no room in another house (its rooms are small), in a small
        /// shop or in a short hospital.
        /// </summary>
        private static List<int> FlightLengths(Building b)
        {
            var inner = Inset(b.Outer, 1);
            var small = Math.Min(inner.W, inner.H) <= SmallBuilding;
            if (b.Furnish == Furnishes.Home) return small ? new List<int> { ShortFlight } : new List<int> { Flight, ShortFlight };
            // A short hospital's stairwell has room for a flight of two only (medicalPlan).
            return small || b.MedicalKind == MedicalSiteKinds.Hospital ? new List<int> { Flight, ShortFlight } : new List<int> { Flight };
        }

        /// <summary>What lies on the floor and is never cleared for the stairs: the special loot boxes.</summary>
        private static readonly HashSet<string> Boxes = new HashSet<string> { DecorKinds.LootWeapons, DecorKinds.LootMedical, DecorKinds.LootAmmo, DecorKinds.LootFood };

        /// <summary>
        /// Whether every place one can stand on in building `r` of storey `c` (with
        /// `slab`: a storey above the ground) that was joined to another still is,
        /// with `closed` taken out of the way (the tiles of a flight: nobody steps on
        /// or off them sideways) and `cleared` furniture gone. Through doors, not
        /// windows.
        /// </summary>
        private static bool JoinedStill(Canvas c, bool[]? slab, Rect r, List<Vec2> closed, JsSet<int> cleared)
        {
            var shut = new JsSet<int>(closed.Select((p) => c.I(p.X, p.Y)));
            JsMap<int, int> Pieces(bool after)
            {
                bool Open(Vec2 p)
                {
                    if (!InRect(r, p)) return false;
                    var i = c.I(p.X, p.Y);
                    if ((slab != null && !Js.At(slab, i, false)) || (after && shut.Has(i))) return false;
                    var kind = after && cleared.Has(i) ? TileKinds.Floor : c.Get(p.X, p.Y);
                    return Grid.IsWalkable(kind) && kind != TileKinds.Window && kind != TileKinds.WindowBroken;
                }
                var piece = new JsMap<int, int>();
                var n = 0;
                foreach (var p in TilesIn(r))
                {
                    if (piece.Has(c.I(p.X, p.Y)) || !Open(p)) continue;
                    piece.Set(c.I(p.X, p.Y), n);
                    var queue = new List<Vec2> { p };
                    while (queue.Count > 0)
                    {
                        var q = queue[queue.Count - 1];
                        queue.RemoveAt(queue.Count - 1);
                        foreach (var d in Grid.Dirs4)
                        {
                            var t = Add(q, d);
                            if (!Open(t) || piece.Has(c.I(t.X, t.Y))) continue;
                            piece.Set(c.I(t.X, t.Y), n);
                            queue.Add(t);
                        }
                    }
                    n++;
                }
                return piece;
            }
            var before = Pieces(false);
            var after = Pieces(true);
            var into = new JsMap<int, int>();
            foreach (var kv in before)
            {
                if (!after.TryGetValue(kv.Key, out var now)) continue;
                if (!into.TryGetValue(kv.Value, out var seen)) into.Set(kv.Value, now);
                else if (seen != now) return false;
            }
            return true;
        }

        /// <summary>
        /// A flight of stairs up through building `r` from the floor below (`lower`:
        /// the ground canvas, or a storey) to storey `upper` (docs/MAPAS.md, phase 2):
        /// `length` tiles in a line the way it climbs, from its foot to its landing a
        /// storey up, with a hole over all of it (the stairwell). Its box is two
        /// columns wide (§4.4): beside the flight, all along from the foot to the
        /// landing, a corridor that is no wall on either floor; the flight's other
        /// side may be one. Over a flight from the storey below (`prev`), first the
        /// way back in the corridor's column, the other way (a return stair: its
        /// tiles kept clear there, `ahead`); else anywhere it fits. On free floor
        /// first; else what furniture stands there is taken away (never a loot box,
        /// nor what `avoid` holds). Never where the hole would cut that storey in two,
        /// nor where the flight would cut the floor below (nobody steps off its
        /// sides), and in a house's bathroom (`shun`) only if nowhere else fits.
        /// Returns the flight, or null if no place fits.
        /// </summary>
        private static LaidFlight? PlaceStairs(Canvas lower, bool[]? lowerSlab, Storey upper, Rect r, Rng rng, JsSet<int> avoid, int length, IReadOnlyList<Rect>? zones = null, IReadOnlyList<Rect>? shun = null, LaidFlight? prev = null, JsSet<int>? ahead = null, bool aligned = false)
        {
            var shunList = shun ?? new List<Rect>();
            var aheadSet = ahead ?? new JsSet<int>();
            var inner = Inset(r, 1);
            bool Wallish(string? k) => k == null || k == TileKinds.Wall || k == TileKinds.Window || k == TileKinds.WindowBroken || k == TileKinds.Roof;
            bool Doorway(Vec2 p) => Grid.Dirs4.Any((d) =>
            {
                var t = lower.Get(p.X + d.X, p.Y + d.Y);
                return t == TileKinds.Door || t == TileKinds.DoorOpen;
            });
            // lower.decor[i] past its ends is undefined in TypeScript: no decor.
            int? DecorAt(int i) => i >= 0 && i < lower.Decor.Count ? lower.Decor[i] : (int?)null;
            // Beside a loot box or a terminal, opened from beside it.
            bool ByBox(Vec2 p) => Grid.Dirs8.Any((d) => lower.Get(p.X + d.X, p.Y + d.Y) == TileKinds.Console || Boxes.Contains(Decor.DecorOf(DecorAt(lower.I(p.X + d.X, p.Y + d.Y)))?.Kind ?? DecorKinds.Part));
            // Furniture on tile i of the floor below that may be taken away for the stairs: no loot box, nothing kept.
            bool Movable(int i)
            {
                // lower.tiles[i] past its ends is undefined in TypeScript: no furniture.
                if (i < 0 || i >= lower.Tiles.Count) return false;
                var k = lower.Tiles[i];
                var look = Decor.DecorOf(lower.Decor[i]);
                return (k == TileKinds.Low || k == TileKinds.High) && !lower.Keep[i] && !avoid.Has(i) && !(look != null && Boxes.Contains(look.Kind)) && look?.Kind != DecorKinds.Stairs;
            }
            // How a place is looked for, from the best kind to the least: with its corridor, on bare floor, on floor kept
            // clear (not by a door), clearing furniture (TypeScript's interface Pass { corridor; kept; clear }).
            // Whether tile p of the floor below can be a step: plain floor (kept clear for it, `ahead`, or as `pass` lets), or furniture to clear.
            bool Below(Vec2 p, (bool Corridor, bool Kept, bool Clear) pass)
            {
                var i = lower.I(p.X, p.Y);
                if ((lowerSlab != null && !Js.At(lowerSlab, i, false)) || lower.ElevAt(p.X, p.Y) != 0) return false;
                if (lower.Get(p.X, p.Y) == TileKinds.Floor && lower.Decor[i] == 0 && (!lower.Keep[i] || aheadSet.Has(i) || (pass.Kept && !Doorway(p) && !ByBox(p)))) return true;
                return pass.Clear && Movable(i);
            }
            // The flight with its lowest tile on `at`, climbing `up`, its corridor on `side`, if it may go there as `pass` lets.
            LaidFlight? Fits(Vec2 at, int up, int side, (bool Corridor, bool Kept, bool Clear) pass)
            {
                var u = Grid.Dirs4[up];
                var v = Grid.Dirs4[side];
                var tiles = new List<Vec2>();
                for (var n = 0; n < length; n++) tiles.Add(V(at.X + u.X * n, at.Y + u.Y * n));
                var foot = V(at.X - u.X, at.Y - u.Y);
                var landing = Add(tiles[length - 1], u);
                var whole = new List<Vec2> { foot };
                whole.AddRange(tiles);
                whole.Add(landing);
                if (!whole.All((p) => InRect(inner, p))) return null;
                if (zones != null && zones.Count > 0 && !tiles.All((p) => zones.Any((z) => InRect(z, p)))) return null;
                // Never over where the flight from the storey below comes up: one has to step off it there.
                if (prev != null && tiles.Any((p) => p.X == prev.Landing.X && p.Y == prev.Landing.Y)) return null;
                foreach (var p in tiles)
                {
                    var i = lower.I(p.X, p.Y);
                    if (avoid.Has(i) || !Below(p, pass) || !Js.At(upper.Slab, i, false) || upper.C.Get(p.X, p.Y) != TileKinds.Floor || upper.C.Keep[i]) return null;
                }
                var fi = lower.I(foot.X, foot.Y);
                var bare = lower.Get(foot.X, foot.Y) == TileKinds.Floor && lower.Decor[fi] == 0 && (lowerSlab == null || Js.At(lowerSlab, fi, false));
                if (!bare && !(pass.Clear && Movable(fi) && (lowerSlab == null || Js.At(lowerSlab, fi, false)))) return null;
                if (upper.C.Get(landing.X, landing.Y) != TileKinds.Floor || upper.C.Keep[upper.C.I(landing.X, landing.Y)]) return null;
                // Storeys laid out alike (a clinic's, drawn after): the landing over floor, in the stairwell's room.
                if (aligned && (Wallish(lower.Get(landing.X, landing.Y)) || lower.Get(landing.X, landing.Y) == TileKinds.Door || (zones != null && zones.Count > 0 && !zones.Any((z) => InRect(z, landing))))) return null;
                // The corridor beside it, from the foot to the landing: no wall on either floor.
                if (pass.Corridor)
                {
                    foreach (var p in whole)
                    {
                        var q = Add(p, v);
                        var j = lower.I(q.X, q.Y);
                        if (!InRect(inner, q) || Wallish(lower.Get(q.X, q.Y)) || (lowerSlab != null && !Js.At(lowerSlab, j, false)) || !Js.At(upper.Slab, j, false) || Wallish(upper.C.Get(q.X, q.Y))) return null;
                    }
                }
                return new LaidFlight(tiles, up, landing, side);
            }
            // Lays `flight` if the floors stay joined, clearing what furniture stands on its foot and its tiles; false if not.
            bool Lay(LaidFlight flight)
            {
                var tiles = flight.Tiles;
                var up = flight.Up;
                var landing = flight.Landing;
                var u = Grid.Dirs4[up];
                var foot = V(tiles[0].X - u.X, tiles[0].Y - u.Y);
                // Furniture in the way goes, whole (a bed over two tiles).
                var cleared = new JsSet<int>();
                var stepped = new List<Vec2> { foot };
                stepped.AddRange(tiles);
                foreach (var p in stepped)
                {
                    var i = lower.I(p.X, p.Y);
                    if (lower.Get(p.X, p.Y) != TileKinds.Low && lower.Get(p.X, p.Y) != TileKinds.High) continue;
                    foreach (var j in Decor.ObjectAt(lower.Decor, lower.Width, i)?.Tiles ?? new List<int> { i }) cleared.Add(j);
                }
                if (cleared.Any((j) => !Movable(j))) return false;
                foreach (var p in tiles) upper.Slab[upper.C.I(p.X, p.Y)] = false;
                if (!StoreyJoined(upper, r, landing) || !JoinedStill(lower, lowerSlab, r, tiles, cleared))
                {
                    foreach (var p in tiles) upper.Slab[upper.C.I(p.X, p.Y)] = true;
                    return false;
                }
                foreach (var j in cleared)
                {
                    lower.Tiles[j] = TileKinds.Floor;
                    lower.Decor[j] = 0;
                }
                for (var n = 0; n < tiles.Count; n++)
                {
                    var p = tiles[n];
                    lower.Force(p.X, p.Y, TileKinds.Ladder);
                    lower.Dress(p, n == tiles.Count - 1 ? DecorKinds.Stairs : DecorKinds.Part, up);
                    lower.Reserve(p.X, p.Y);
                    upper.C.Reserve(p.X, p.Y);
                    foreach (var d in Grid.Dirs4) upper.C.Reserve(p.X + d.X, p.Y + d.Y);
                }
                lower.Reserve(foot.X, foot.Y);
                upper.C.Reserve(landing.X, landing.Y);
                return true;
            }
            var passes = new List<(bool Corridor, bool Kept, bool Clear)>
            {
                (true, false, false),
                (true, false, true),
                (true, true, true),
                (false, true, true),
            };
            // The way back over the last flight, in its box's other column: its corridor (beside it, the stairwell below).
            if (prev != null && prev.Tiles.Count == length)
            {
                var turn = Add(prev.Tiles[length - 1], Grid.Dirs4[prev.Side]);
                foreach (var pass in passes.Where((q) => !q.Corridor).ToList())
                {
                    var back = Fits(turn, Opposite(prev.Up), Opposite(prev.Side), pass);
                    if (back != null && Lay(back)) return back;
                }
            }
            // Out of a house's bathroom if it can be (#15).
            int Shunned(LaidFlight f) => f.Tiles.Any((p) => shunList.Any((q) => InRect(q, p))) ? 1 : 0;
            foreach (var pass in passes)
            {
                var found = new List<(LaidFlight Flight, double Key)>();
                foreach (var at in TilesIn(inner))
                {
                    for (var up = 0; up < 4; up++)
                    {
                        foreach (var side in new[] { (up + 1) % 4, (up + 3) % 4 })
                        {
                            var f = Fits(at, up, side, pass);
                            if (f != null) found.Add((f, rng.Next()));
                        }
                    }
                }
                Js.Sort(found, (a, b) =>
                {
                    double d = Shunned(a.Flight) - Shunned(b.Flight);
                    return d != 0 ? d : a.Key - b.Key;
                });
                foreach (var item in found) if (Lay(item.Flight)) return item.Flight;
            }
            return null;
        }

        /// <summary>
        /// A ladder up the side of building `r`'s one-storey flat roof: on open ground
        /// the squad can reach, against a plain stretch of wall, clear of the doors.
        /// The roof's parapet opens where it comes up. False if nowhere fits.
        /// </summary>
        private static bool PlaceLadder(Canvas c, Storey roof, Rect r, Rng rng, JsSet<int> avoid, JsSet<int> reach)
        {
            var spots = new List<(Vec2 Wall, Vec2 Foot)>();
            for (var s = 0; s < 4; s++)
            {
                for (var u = 2; u <= SideLen(r, s) - 3; u++)
                {
                    var wall = LocalPt(r, s, u, 0);
                    var foot = Add(wall, Grid.Dirs4[s]);
                    var lateral = Grid.Dirs4[(s + 1) % 4];
                    var alongWall = new[] { wall, Add(wall, lateral), V(wall.X - lateral.X, wall.Y - lateral.Y) };
                    var i = c.I(foot.X, foot.Y);
                    if (!c.In(foot.X, foot.Y) || !c.Free(foot.X, foot.Y) || c.Decor[i] != 0 || c.GroundAt(foot.X, foot.Y) == Indoor || !reach.Has(i) || avoid.Has(i)) continue;
                    if (!alongWall.All((p) => c.Get(p.X, p.Y) == TileKinds.Wall)) continue;
                    if (Grid.Dirs8.Any((d) => c.Get(foot.X + d.X, foot.Y + d.Y) == TileKinds.Door)) continue;
                    spots.Add((wall, foot));
                }
            }
            if (spots.Count == 0) return false;
            var spot = rng.Pick(spots);
            c.Force(spot.Foot.X, spot.Foot.Y, TileKinds.Ladder);
            c.Reserve(spot.Foot.X, spot.Foot.Y);
            roof.C.Force(spot.Wall.X, spot.Wall.Y, TileKinds.Floor);
            return true;
        }

        /// <summary>A building in upperFloors' `all`: its storeys (`n`), whether its flat roof is walked on, the storey its stairs reach, its last flight and what it keeps clear, the rooms of the storey drawn.</summary>
        private sealed class EntryUpstairs
        {
            public readonly Building B;
            public readonly int N;
            public bool Walkable;
            public int Reached;
            public Vec2? Landing;
            public LaidFlight? Flight;
            public JsSet<int> Ahead = new JsSet<int>();
            public List<Rect> Rooms = new List<Rect>();
            /// <summary>`rooms` where medicalPlan drew them (TypeScript keeps its MedicalRoom[] in `rooms`).</summary>
            public List<MedicalRoom> MedicalRooms = new List<MedicalRoom>();
            public List<List<Rect>>? Flats;

            public EntryUpstairs(Building b, int n)
            {
                B = b;
                N = n;
            }
        }

        /// <summary>
        /// The storeys above the ground of every building on `c` (`heights`: how many
        /// each stands), bottom up, and where a group of the living could be posted
        /// up there (`perch`: a few places together on an upper floor, far from
        /// `squad`). `avoid`: ground tiles already taken (spawns, pods, objective, evac,
        /// lights); `reach`: what the squad reaches on the ground.
        /// </summary>
        private static (List<Storey> Storeys, List<Pos> Perch) UpperFloors(Canvas c, List<int> heights, int seed, JsSet<int> avoid, JsSet<int> reach, Pt squad)
        {
            var rng = new Rng(seed ^ 0x57a125);
            var area = c.Width * c.Height;
            // heights[j] past its end is undefined in TypeScript, and `undefined >= 1` false: left out.
            var all = c.Buildings
                .Select((b, j) => new EntryUpstairs(b, j < heights.Count ? heights[j] : 0))
                .Where((e) => !e.B.Ruined && e.N >= 1)
                .ToList();
            // Whether building `e` has stairs up to storey k: to each storey but its top, and onto a flat roof over two or three.
            bool Climbs(EntryUpstairs e, int k) => e.N > k || (e.N == k && k >= 2 && e.Walkable);
            // Flat roofs: reached by the stairs over two or three storeys; over one, by a ladder half the time.
            foreach (var e in all) e.Walkable = !Pitched(e.B) && !e.B.Skylit && (e.N >= 2 || rng.Next() < 0.5);
            var top = 0;
            foreach (var e in all) top = Math.Max(top, e.N);
            var storeys = new List<Storey>();
            for (var n = 0; n < top; n++) storeys.Add(new Storey(new Canvas(c.Width, c.Height), new bool[area]));
            for (var k = 1; k <= top; k++)
            {
                var st = storeys[k - 1];
                var lc = st.C;
                var occupied = new bool[area];
                foreach (var e in all) if (e.N >= k) foreach (var p in TilesIn(e.B.Outer)) if (c.In(p.X, p.Y)) occupied[c.I(p.X, p.Y)] = true;
                // The roofs of those that end here, then the walls of those that go on (a wall two share stands over the lower one's roof).
                foreach (var e in all.Where((x) => x.N == k).ToList())
                {
                    foreach (var p in TilesIn(e.B.Outer))
                    {
                        if (!c.In(p.X, p.Y)) continue;
                        lc.Force(p.X, p.Y, e.Walkable ? TileKinds.Floor : TileKinds.Roof);
                        st.Slab[c.I(p.X, p.Y)] = true;
                    }
                }
                var going = all.Where((x) => x.N > k).ToList();
                foreach (var e in going)
                {
                    var r = e.B.Outer;
                    foreach (var p in TilesIn(r))
                    {
                        if (!c.In(p.X, p.Y)) continue;
                        var edge = p.X == r.X || p.Y == r.Y || p.X == r.X + r.W - 1 || p.Y == r.Y + r.H - 1;
                        lc.Force(p.X, p.Y, edge ? TileKinds.Wall : TileKinds.Floor);
                        lc.Ground[c.I(p.X, p.Y)] = Indoor;
                        st.Slab[c.I(p.X, p.Y)] = true;
                    }
                }
                // Up from the storey below, before this storey is split into rooms (docs/MAPAS.md §4.4: the stair box first, its
                // walls keep off the stairwell): to each storey of those that go on, and to the flat roofs over two or three.
                var lower = k == 1 ? c : storeys[k - 2].C;
                var lowerSlab = k == 1 ? null : storeys[k - 2].Slab;
                foreach (var e in all)
                {
                    e.Landing = null;
                    // No way up to the storey below: none here either.
                    if (!Climbs(e, k) || e.Reached != k - 1) continue;
                    List<Rect>? zones = e.B.MedicalKind == MedicalSiteKinds.Hospital
                        ? e.B.MedicalRooms?.Where((r) => r.Use == MedicalUses.Stairs).Select((r) => Rect(r.X, r.Y, r.W, r.H)).ToList()
                        : e.B.FlatsCore != null ? new List<Rect> { e.B.FlatsCore }
                        : e.B.HouseStairs != null ? new List<Rect> { e.B.HouseStairs }
                        : null;
                    var kept = (bool[])lc.Keep.Clone();
                    var prev = e.Flight;
                    var shun = e.B.HomeRooms?.Where((r) => (r.Use == global::Bastion.Engine.HomeUses.Bath || r.Use == global::Bastion.Engine.HomeUses.Toilet || r.Use == global::Bastion.Engine.HomeUses.Pantry) && r.Z == k - 1).Select((r) => Rect(r.X, r.Y, r.W, r.H)).ToList();
                    // A building's flights are all one length: the first one's.
                    e.Flight = null;
                    // (TypeScript's `length`: C# has a `length` below in this block.)
                    foreach (var flightLength in prev != null ? new List<int> { prev.Tiles.Count } : FlightLengths(e.B))
                    {
                        // `??=`: no more tries once one is laid.
                        if (e.Flight == null) e.Flight = PlaceStairs(lower, lowerSlab, st, e.B.Outer, rng, k == 1 ? avoid : new JsSet<int>(), flightLength, zones, shun, prev, e.Ahead, !string.IsNullOrEmpty(e.B.MedicalKind) || e.B.FlatsCore != null || e.B.HouseHall != null);
                    }
                    e.Landing = e.Flight?.Landing;
                    if (e.Landing == null) continue;
                    e.Reached = k;
                    // The next flight up goes back over this one, in its box's other column (docs/MAPAS.md §4.4): kept clear of furniture,
                    // and so are the ways round the box's ends, beyond them and beside that column, lest the next flight close them.
                    e.Ahead = new JsSet<int>();
                    if (!Climbs(e, k + 1)) continue;
                    var flight = e.Flight!;
                    var v = Grid.Dirs4[flight.Side];
                    var u = Grid.Dirs4[flight.Up];
                    var first = flight.Tiles[0];
                    Vec2 End(int n) => V(first.X + u.X * n, first.Y + u.Y * n);
                    var length = flight.Tiles.Count;
                    var ends = new[] { End(-2), Add(End(-2), v), Add(Add(End(-1), v), v), End(length + 1), Add(End(length + 1), v), Add(Add(End(length), v), v) };
                    var round = flight.Tiles.Select((q) => Add(q, v)).ToList();
                    round.Add(Add(e.Landing, v));
                    round.AddRange(ends);
                    foreach (var p in round)
                    {
                        if (!InRect(Inset(e.B.Outer, 1), p)) continue;
                        // Kept for something else (a doorway), it stays so; kept round the stairwell, it is the way back.
                        if (lc.Get(p.X, p.Y) != TileKinds.Floor || kept[lc.I(p.X, p.Y)]) continue;
                        lc.Reserve(p.X, p.Y);
                        e.Ahead.Add(lc.I(p.X, p.Y));
                    }
                }
                foreach (var e in going)
                {
                    e.Rooms = new List<Rect>();
                    e.MedicalRooms = new List<MedicalRoom>();
                    if (!string.IsNullOrEmpty(e.B.MedicalKind))
                    {
                        e.MedicalRooms = MedicalPlan(lc, e.B.Outer, e.B.Front!.Value, rng, k, e.B.MedicalKind == MedicalSiteKinds.Hospital, e.B.MedicalKind == MedicalSiteKinds.Ward, new List<int?> { null, null, null, null });
                    }
                    else if (e.B.FlatsCore != null)
                    {
                        // A block of flats: the same core and two flats on every storey.
                        var (core, flats) = FlatsPlan(lc, e.B.Outer, e.B.Front!.Value, rng, true);
                        e.Rooms = new List<Rect> { core };
                        e.Rooms.AddRange(flats.SelectMany((f) => f));
                        e.Flats = flats;
                    }
                    else if (e.B.HouseHall != null)
                    {
                        // A house's storey round its hall, the stairs in it (the hall itself no room of the home up here).
                        HallPlan2(lc, e.B.Outer, e.B.Front!.Value, rng, e.Rooms, true);
                    }
                    else SplitRooms(lc, Inset(e.B.Outer, 1), rng, e.Rooms, e.B.Furnish == Furnishes.Home ? 6 : 8, e.B.Furnish != Furnishes.Home, true);
                    var outer = e.B.Outer;
                    var others = new bool[area];
                    for (var i = 0; i < area; i++) others[i] = occupied[i] && !InRect(outer, V(i % c.Width, i / c.Width));
                    UpperWindows(lc, e.B.Outer, UpstairsWindows(e.B), others);
                }
                foreach (var e in going)
                {
                    if (e.Landing == null) continue;
                    if (!string.IsNullOrEmpty(e.B.MedicalKind))
                    {
                        foreach (var room in e.MedicalRooms) FurnishMedical(lc, room, rng, e.B.MedicalKind == MedicalSiteKinds.Clinic ? null : e.B.MedicalKind);
                        e.B.MedicalRooms!.AddRange(e.MedicalRooms);
                    }
                    else if (e.B.FlatsCore != null && e.Flats != null)
                    {
                        // Each flat its own home again. (The core is the block's hall on the ground floor; up here its flights stand
                        // under the stairwell of the storey above: no room of a home.)
                        foreach (var flat in e.Flats)
                        {
                            var (own, uses, strip) = FlatUses(lc, flat);
                            for (var j = 0; j < own.Count; j++)
                            {
                                var room = own[j];
                                FurnishRoom(lc, room, room == strip ? new Rng(Mix(room.X, room.Y)) : rng, Furnishes.Home, uses[j], e.B.HomeWear ?? 0, uses[j] == global::Bastion.Engine.HomeUses.Living, false);
                            }
                            e.B.HomeRooms!.AddRange(own.Select((r, j) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = k, Use = uses[j] }));
                        }
                    }
                    else if (e.B.HomeRooms != null)
                    {
                        // A house's storey above: bedrooms, a bathroom (cut off the biggest room if none is small), maybe a study.
                        Rect? strip = null;
                        if (!e.Rooms.Any((r) => r.W * r.H <= 9))
                        {
                            // reduce from rooms[0]: undefined for none.
                            Rect? big = e.Rooms.Count > 0 ? e.Rooms[0] : null;
                            foreach (var r in e.Rooms) if (r.W * r.H > big!.W * big.H) big = r;
                            var cut = big != null ? SplitOff(lc, big, 2) : null;
                            if (cut != null)
                            {
                                // indexOf: by reference.
                                var at = e.Rooms.FindIndex((r) => ReferenceEquals(r, big));
                                e.Rooms.RemoveAt(at);
                                e.Rooms.InsertRange(at, new[] { cut.Value.Item1, cut.Value.Item2 });
                                strip = cut.Value.Item2;
                            }
                        }
                        var uses = HomeUses(e.Rooms, null, k);
                        for (var j = 0; j < e.Rooms.Count; j++)
                        {
                            var room = e.Rooms[j];
                            FurnishRoom(lc, room, room == strip ? new Rng(Mix(room.X, room.Y)) : rng, Furnishes.Home, uses[j], e.B.HomeWear ?? 0);
                        }
                        e.B.HomeRooms.AddRange(e.Rooms.Select((r, j) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = k, Use = uses[j] }));
                    }
                    else foreach (var room in e.Rooms) FurnishRoom(lc, room, rng, Upstairs[e.B.Furnish]);
                    if (!string.IsNullOrEmpty(e.B.MedicalKind)) ClearMedicalAisles(st, e.B.Outer, e.Landing);
                    else UnblockStorey(st, e.B.Outer, e.Landing);
                }
                // A flat roof one walks on has a parapet round it (half cover); one nobody reaches is just a roof.
                foreach (var e in all.Where((x) => x.N == k && x.Walkable).ToList())
                {
                    var reached = k == 1 || e.Landing != null;
                    var r = e.B.Outer;
                    foreach (var p in TilesIn(r))
                    {
                        if (!c.In(p.X, p.Y) || lc.Get(p.X, p.Y) != TileKinds.Floor) continue;
                        var edge = p.X == r.X || p.Y == r.Y || p.X == r.X + r.W - 1 || p.Y == r.Y + r.H - 1;
                        if (!reached) lc.Force(p.X, p.Y, TileKinds.Roof);
                        else if (edge) lc.Force(p.X, p.Y, TileKinds.Low);
                    }
                    if (reached && k == 1 && !PlaceLadder(c, st, r, rng, avoid, reach))
                    {
                        foreach (var p in TilesIn(r)) if (c.In(p.X, p.Y) && st.Slab[c.I(p.X, p.Y)] && (lc.Get(p.X, p.Y) == TileKinds.Floor || lc.Get(p.X, p.Y) == TileKinds.Low)) lc.Force(p.X, p.Y, TileKinds.Roof);
                    }
                }
                // Storeys nobody can reach (no room for stairs): only a roof over them.
                foreach (var e in going)
                {
                    if (e.Landing != null) continue;
                    foreach (var p in TilesIn(e.B.Outer)) if (c.In(p.X, p.Y) && st.Slab[c.I(p.X, p.Y)]) lc.Force(p.X, p.Y, TileKinds.Roof);
                }
            }
            // The spare circulation bay becomes a furnished waiting area if no flight or landing uses it.
            foreach (var e in all.Where((e) => e.B.MedicalKind == MedicalSiteKinds.Hospital).ToList())
            {
                var rooms = e.B.MedicalRooms ?? new List<MedicalRoom>();
                for (var m = 0; m < rooms.Count; m++)
                {
                    var r = rooms[m];
                    if (r.Use != MedicalUses.Stairs) continue;
                    var st = r.Z != 0 ? Js.At<Storey?>(storeys, r.Z - 1, null) : null;
                    var floor = st?.C ?? c;
                    var bay = Rect(r.X, r.Y, r.W, r.H);
                    // decor[i] and slab[i] past their ends are undefined in TypeScript.
                    var active = TilesIn(bay).Any((p) => Decor.DecorOf(Js.At(floor.Decor, c.I(p.X, p.Y), 0))?.Kind == DecorKinds.Stairs || (st != null && !Js.At(st.Slab, c.I(p.X, p.Y), false)));
                    if (!active)
                    {
                        if (r.Z == 0) foreach (var p in TilesIn(bay)) if (avoid.Has(c.I(p.X, p.Y))) floor.Reserve(p.X, p.Y);
                        r.Use = MedicalUses.Waiting;
                        FurnishCareRoom(floor, r, rng, MedicalSiteKinds.Hospital);
                    }
                }
            }
            // Later stairwells can isolate a tile behind a bed on the floor below.
            foreach (var e in all.Where((e) => !string.IsNullOrEmpty(e.B.MedicalKind)).ToList())
            {
                for (var k = 1; k < e.N; k++)
                {
                    var lower = k == 1 ? c : storeys[k - 2].C;
                    var i = -1;
                    for (var t = 0; t < lower.Decor.Count; t++)
                    {
                        if (Decor.DecorOf(lower.Decor[t])?.Kind == DecorKinds.Stairs && InRect(e.B.Outer, V(t % c.Width, t / c.Width)))
                        {
                            i = t;
                            break;
                        }
                    }
                    if (i < 0) continue;
                    var d = Grid.Dirs4[Decor.DecorOf(lower.Decor[i])!.Facing];
                    ClearMedicalAisles(storeys[k - 1], e.B.Outer, V(i % c.Width + d.X, i / c.Width + d.Y));
                }
            }
            // Somewhere up there for a group of the living to be posted (decision 7): far from the squad, by a window.
            var perch = new List<Pos>();
            var upstairs = Shuffle(all.Where((e) => e.N >= 2 && e.Reached >= 1 && Dist(Centre(e.B.Outer), squad) >= PodDistance).ToList(), rng);
            foreach (var e in upstairs)
            {
                var st = storeys[0];
                // Never on a flight of stairs (docs/MAPAS.md, phase 2: nobody stops there).
                bool Stand(Vec2 p) => InRect(e.B.Outer, p) && Js.At(st.Slab, c.I(p.X, p.Y), false) && Grid.CanStandOn(st.C.Get(p.X, p.Y)) && st.C.Get(p.X, p.Y) != TileKinds.Ladder && !st.C.Keep[c.I(p.X, p.Y)];
                var byWindow = TilesIn(Inset(e.B.Outer, 1)).Where((p) => Stand(p) && Grid.Dirs4.Any((d) => st.C.Get(p.X + d.X, p.Y + d.Y) == TileKinds.Window)).ToList();
                if (byWindow.Count == 0) continue;
                var anchor = rng.Pick(byWindow);
                var seen = new JsSet<int>(new[] { c.I(anchor.X, anchor.Y) });
                // queue.shift(): read from the front.
                var queue = new List<Vec2> { anchor };
                var head = 0;
                while (head < queue.Count && perch.Count < 4)
                {
                    var p = queue[head++];
                    if (Stand(p)) perch.Add(new Pos { X = p.X, Y = p.Y, Z = 1 });
                    foreach (var d in Grid.Dirs4)
                    {
                        var q = Add(p, d);
                        if (seen.Has(c.I(q.X, q.Y)) || !InRect(e.B.Outer, q) || !Js.At(st.Slab, c.I(q.X, q.Y), false) || !Grid.IsWalkable(st.C.Get(q.X, q.Y)) || st.C.Get(q.X, q.Y) == TileKinds.Window) continue;
                        seen.Add(c.I(q.X, q.Y));
                        queue.Add(q);
                    }
                }
                break;
            }
            return (storeys, perch);
        }
    }
}
