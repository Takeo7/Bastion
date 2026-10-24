#nullable enable
// flights.ts: a building's flights of stairs.
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Bastion.Engine
{
    /// <summary>A building's flight of stairs.</summary>
    public sealed class Flight
    {
        /// <summary>The level its tiles stand on: its landing is a storey up.</summary>
        public int Z { get; set; }
        /// <summary>The way it climbs (a DIRS4 index).</summary>
        public int Up { get; set; }
        /// <summary>Its tiles from the lowest up (the last, the top, dressed 'stairs').</summary>
        public List<Vec2> Tiles { get; set; } = new List<Vec2>();
    }

    /// <summary>flightAt's answer: `{ flight: Flight; step: number }`, the flight and which of its tiles (0 the lowest).</summary>
    public sealed class FlightSpot
    {
        public Flight Flight { get; set; } = new Flight();
        public int Step { get; set; }
    }

    // docs/MAPAS.md, phase 2: a building's stairs are a flight of several tiles
    // (3, 2 in small houses: Arturo, 19/10), 38° as real ones, not a storey up in
    // one tile. Its tiles are `ladder` tiles on the storey it climbs from, in a
    // line the way it climbs: the top one dressed 'stairs' facing that way (as
    // the one-tile flights of before), each below it a 'part' facing it too
    // (decor.ts objectTiles). The foot is the tile behind the lowest, on that
    // storey; the landing the tile in front of the top, a storey up; over the
    // flight the storey above has a hole (the stairwell).
    //
    // One goes on a flight only from its foot, the way it climbs, and off it
    // only at its landing (and back down), never off its sides past the
    // banisters; nobody stops on it (Arturo, 19/10: «solo se entra por el pie y
    // nadie se para a mitad»). A flight without parts is one of before, of one
    // tile: it keeps the rules it had (docs/FASE7.md §2), so a mission saved
    // before, and «La torre», play as they did.
    public static class Flights
    {
        /// <summary>The longest flight there is (docs/MAPAS.md §4.4).</summary>
        public const int MaxFlight = 3;

        /// <summary>The decor of level z, if any.</summary>
        private static IReadOnlyList<int>? DecorOn(GameState s, int z)
        {
            return z != 0 ? Grid.LevelOf(s, z - 1)?.Decor : s.Decor;
        }

        private static int? CodeAt(IReadOnlyList<int> decor, int i)
        {
            return i >= 0 && i < decor.Count ? decor[i] : (int?)null;
        }

        private static Pos P(int x, int y, int z)
        {
            return new Pos { X = x, Y = y, Z = z };
        }

        /// <summary>
        /// The flight of stairs place `p` is a tile of, and which of its tiles it is
        /// (0 the lowest), or null: one of a single tile too (one of before). On a map
        /// without storeys (as a mission saved before them), none: there is nothing to
        /// climb to.
        /// </summary>
        public static FlightSpot? FlightAt(GameState s, Pos p)
        {
            if (s.Levels == null || Grid.KindAt(s, p) != TileKinds.Ladder) return null;
            var decor = DecorOn(s, p.Z);
            if (decor == null) return null;
            // Up the parts to the top.
            var top = new Vec2 { X = p.X, Y = p.Y };
            var look = Decor.DecorOf(CodeAt(decor, Grid.TileIndex(s, p.X, p.Y)));
            var facings = new List<int>();
            for (var k = 0; look?.Kind == DecorKinds.Part && k < MaxFlight; k++)
            {
                facings.Add(look.Facing);
                var d = Grid.Dirs4[look.Facing];
                top = new Vec2 { X = top.X + d.X, Y = top.Y + d.Y };
                if (!Grid.InBounds(s, top.X, top.Y) || Grid.KindAt(s, P(top.X, top.Y, p.Z)) != TileKinds.Ladder) return null;
                look = Decor.DecorOf(CodeAt(decor, Grid.TileIndex(s, top.X, top.Y)));
            }
            if (look?.Kind != DecorKinds.Stairs || facings.Any((f) => f != look.Facing)) return null;
            var up = look.Facing;
            var dir = Grid.Dirs4[up];
            // Down from the top: the parts behind it that climb to it.
            var tiles = new List<Vec2> { top };
            for (var n = 1; n < MaxFlight; n++)
            {
                var q = new Vec2 { X = top.X - dir.X * n, Y = top.Y - dir.Y * n };
                if (!Grid.InBounds(s, q.X, q.Y) || Grid.KindAt(s, P(q.X, q.Y, p.Z)) != TileKinds.Ladder) break;
                var part = Decor.DecorOf(CodeAt(decor, Grid.TileIndex(s, q.X, q.Y)));
                if (part?.Kind != DecorKinds.Part || part.Facing != up) break;
                tiles.Insert(0, q);
            }
            var step = tiles.Count - 1 - facings.Count;
            return step < 0 ? null : new FlightSpot { Flight = new Flight { Z = p.Z, Up = up, Tiles = tiles }, Step = step };
        }

        /// <summary>flightAt for a flight of several tiles; null on one of a single tile (one of before), as on anything else.</summary>
        public static FlightSpot? LongFlightAt(GameState s, Pos p)
        {
            var at = FlightAt(s, p);
            return at != null && at.Flight.Tiles.Count > 1 ? at : null;
        }

        /// <summary>Where one steps onto a flight from: the tile behind its lowest, on its level.</summary>
        public static Pos FlightFoot(Flight f)
        {
            var d = Grid.Dirs4[f.Up];
            return P(f.Tiles[0].X - d.X, f.Tiles[0].Y - d.Y, f.Z);
        }

        /// <summary>Where one steps off a flight at the top: the tile in front of it, a storey up.</summary>
        public static Pos FlightLanding(Flight f)
        {
            var d = Grid.Dirs4[f.Up];
            var top = f.Tiles[f.Tiles.Count - 1];
            return P(top.X + d.X, top.Y + d.Y, f.Z + 1);
        }

        /// <summary>Whether place `p` is on a flight of several tiles, where nobody stops.</summary>
        public static bool MidFlight(GameState s, Pos p)
        {
            var q = P(p.X, p.Y, p.Z);
            return Grid.KindAt(s, q) == TileKinds.Ladder && LongFlightAt(s, q) != null;
        }

        /// <summary>
        /// Whether a unit can end a move on (or be put on) place `p`: what stands
        /// there lets it (grid.ts canStandOn: not a window's frame), and it is not on
        /// a flight of stairs of several tiles.
        /// </summary>
        public static bool CanStopAt(GameState s, Pos p)
        {
            var q = P(p.X, p.Y, p.Z);
            var kind = Grid.KindAt(s, q);
            return Grid.CanStandOn(kind) && (kind != TileKinds.Ladder || LongFlightAt(s, q) == null);
        }

        /// <summary>
        /// Where what comes to rest on place `p` (a body's gear, the objective)
        /// stays: on `p`, or off a flight of several tiles at its nearer end (its
        /// foot, or its landing), where one can stop and pick it up.
        /// </summary>
        public static Pos RestingPlace(GameState s, Pos p)
        {
            var on = Grid.KindAt(s, p) == TileKinds.Ladder ? LongFlightAt(s, p) : null;
            if (on == null) return P(p.X, p.Y, p.Z);
            return on.Step * 2 < on.Flight.Tiles.Count - 1 ? FlightFoot(on.Flight) : FlightLanding(on.Flight);
        }

        /// <summary>
        /// Whether the slab of storey place `p` (≥ 1) bears a flight of several
        /// tiles: one of its tiles, its foot or its landing. A grenade never breaks it
        /// (docs/MAPAS.md, phase 2): no flight is left hanging, nor half of one.
        /// </summary>
        public static bool BearsFlight(GameState s, Pos p)
        {
            if (p.Z < 1) return false;
            if (MidFlight(s, p)) return true;
            foreach (var d in Grid.Dirs4)
            {
                // Its foot: the flight starts beside it, on its level, climbing away from it.
                var next = P(p.X + d.X, p.Y + d.Y, p.Z);
                var on = Grid.InBounds(s, next.X, next.Y) && Grid.KindAt(s, next) == TileKinds.Ladder ? LongFlightAt(s, next) : null;
                if (on != null && on.Step == 0 && Grid.Dirs4[on.Flight.Up].X == d.X && Grid.Dirs4[on.Flight.Up].Y == d.Y) return true;
                // Its landing: the top of a flight beside it, a storey down, climbing towards it.
                var below = P(p.X + d.X, p.Y + d.Y, p.Z - 1);
                var under = Grid.InBounds(s, below.X, below.Y) && Grid.KindAt(s, below) == TileKinds.Ladder ? LongFlightAt(s, below) : null;
                if (under != null && under.Step == under.Flight.Tiles.Count - 1 && Grid.Dirs4[under.Flight.Up].X == -d.X && Grid.Dirs4[under.Flight.Up].Y == -d.Y) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether tile `i` of one level (its `tiles` and `decor`, `width` wide) is on
        /// a flight of several tiles: a part of one, or a top with one behind it. For
        /// what works on a level's arrays (furnishing.ts), without a state.
        /// </summary>
        public static bool LongFlightTile(IReadOnlyList<string?> tiles, IReadOnlyList<int>? decor, int width, int i)
        {
            if (Js.At(tiles, i, null) != TileKinds.Ladder) return false;
            var look = Decor.DecorOf(decor != null ? CodeAt(decor, i) : null);
            if (look?.Kind == DecorKinds.Part) return true;
            if (look?.Kind != DecorKinds.Stairs) return false;
            var d = Grid.Dirs4[look.Facing];
            var x = (i % width) - d.X;
            var y = (int)System.Math.Floor((double)i / width) - d.Y;
            if (x < 0 || y < 0 || x >= width || y * width >= tiles.Count) return false;
            var behind = Decor.DecorOf(decor != null ? CodeAt(decor, y * width + x) : null);
            return Js.At(tiles, y * width + x, null) == TileKinds.Ladder && behind?.Kind == DecorKinds.Part && behind.Facing == look.Facing;
        }

        /// <summary>Maps by the levels they were built with, and whether they have flights of several tiles.</summary>
        private static readonly ConditionalWeakTable<List<Level>, object> Known = new ConditionalWeakTable<List<Level>, object>();

        /// <summary>
        /// Whether the map has flights of stairs of several tiles (docs/MAPAS.md,
        /// phase 2), as generated maps do since; a map without (one saved before, a
        /// hand-made one) plays as it did.
        /// </summary>
        public static bool HasLongFlights(GameState s)
        {
            if (s.Levels == null) return false;
            if (Known.TryGetValue(s.Levels, out var seen)) return (bool)seen;
            var decors = new List<IReadOnlyList<int>?> { s.Decor };
            decors.AddRange(s.Levels.Select((l) => (IReadOnlyList<int>?)l.Decor));
            var out_ = false;
            for (var z = 0; z < decors.Count && !out_; z++)
            {
                var decor = decors[z];
                if (decor == null) continue;
                for (var i = 0; i < decor.Count; i++)
                {
                    if (Decor.DecorOf(decor[i])?.Kind != DecorKinds.Stairs) continue;
                    if (LongFlightAt(s, P(i % s.Width, (int)System.Math.Floor((double)i / s.Width), z)) != null)
                    {
                        out_ = true;
                        break;
                    }
                }
            }
            Known.AddOrUpdate(s.Levels, out_);
            return out_;
        }
    }
}
