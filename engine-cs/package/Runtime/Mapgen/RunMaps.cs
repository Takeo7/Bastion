#nullable enable
// The expeditions' maps in the generator (new in C#; Gameplay, 16/11): La Lata near the squad on a normal node
// (GenOptions.EvacNear), the hospital only inside (H7, Sites.Hospital), the offices and the shed only inside (H9,
// Sites.Offices, Sites.Shed, .dev-snapshots/gameplay/runs/H9-INTERIORES.md). Nothing here runs for any other map.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>
        /// La Lata's walk from where the squad comes in on an expedition's normal node (GenOptions.EvacNear), in steps: at mid
        /// distance, towards the middle of the map (Arturo, 17/11: «unas 30 casillas»; at a service area it was some 50, and
        /// with knives only the horde caught them; 15–25 on 16/11).
        /// </summary>
        public static readonly IReadOnlyList<int> NearWalk = new[] { 26, 34 };
        /// <summary>A way out's bus further than this from the squad is not near (AvenueEvac): the evacuation is carved instead.</summary>
        private const int NearMost = 40;
        /// <summary>How many of the best places NearEvac and AvenueEvac weigh with the bus standing (WalkWith), best first.</summary>
        private const int NearTries = 24;

        /// <summary>
        /// Steps on foot from where the squad comes in to the nearest of `zone`, with `bus` standing (solid for the reckoning,
        /// then as it was: a bus parked across the only track sent the walk round, 73 steps for 30); -1 if none.
        /// </summary>
        private static int WalkWith(Canvas c, List<Vec2> zone, List<Vec2> bus)
        {
            var kept = bus.Where((p) => c.In(p.X, p.Y)).Select((p) => (I: c.I(p.X, p.Y), K: c.Tiles[c.I(p.X, p.Y)])).ToList();
            foreach (var (i, _) in kept) c.Tiles[i] = TileKinds.High;
            var steps = StepsFrom(c, new List<Vec2> { c.Squad });
            foreach (var (i, k) in kept) c.Tiles[i] = k;
            var reached = zone.Where((z) => c.In(z.X, z.Y) && steps[c.I(z.X, z.Y)] >= 0).Select((z) => steps[c.I(z.X, z.Y)]).ToList();
            return reached.Count > 0 ? reached.Min() : -1;
        }

        /// <summary>Whether `p` is on the squad's half of the map: nearer where it comes in than the point across the map from it.</summary>
        private static bool SquadHalf(Canvas c, Vec2 p)
        {
            var across = V(c.Width - 1 - c.Squad.X, c.Height - 1 - c.Squad.Y);
            return Dist(p, c.Squad) <= Dist(p, across);
        }

        /// <summary>How far a walk of `n` steps is off NearWalk: 0 inside it.</summary>
        private static int OffNear(int n) => n < NearWalk[0] ? NearWalk[0] - n : n > NearWalk[1] ? n - NearWalk[1] : 0;

        /// <summary>
        /// La Lata near (GenOptions.EvacNear) where there is no way out for it near enough (AvenueEvac): a 3 × 3 evacuation
        /// cleared outdoors on the ground, on the squad's half, at NearWalk on foot from where it comes in, where its bus has
        /// room beside it (ParkBus); in the walk, the one with least to clear, then the nearest its middle. Empty if nowhere:
        /// then the map's own, as before.
        /// </summary>
        private static List<Vec2> NearEvac(Canvas c)
        {
            var steps = StepsFrom(c, new List<Vec2> { c.Squad });
            var mid = (NearWalk[0] + NearWalk[1]) / 2;
            // What a zone round `p` would clear (CarveEvac's rule): -1 where it can't be.
            int Clearing(Vec2 p)
            {
                var n = 0;
                foreach (var q in TilesIn(Rect(p.X - 1, p.Y - 1, 3, 3)))
                {
                    var kind = c.Get(q.X, q.Y);
                    if (!c.In(q.X, q.Y) || c.ElevAt(q.X, q.Y) != 0 || c.GroundAt(q.X, q.Y) == Indoor || c.GroundAt(q.X, q.Y) == Out || (kind != TileKinds.Floor && kind != TileKinds.Low && kind != TileKinds.High)) return -1;
                    if (kind != TileKinds.Floor) n++;
                }
                return n;
            }
            var spots = Enumerable.Range(0, steps.Length)
                .Where((i) => steps[i] > 0)
                .Select((i) => (P: V(i % c.Width, i / c.Width), Steps: steps[i]))
                .Where((t) => SquadHalf(c, t.P))
                .Select((t) => (t.P, t.Steps, Clear: Clearing(t.P)))
                .Where((t) => t.Clear >= 0)
                .OrderBy((t) => OffNear(t.Steps))
                .ThenBy((t) => t.Clear)
                .ThenBy((t) => Math.Abs(t.Steps - mid))
                .ThenBy((t) => c.I(t.P.X, t.P.Y))
                .ToList();
            return FirstEvac(c, spots.Select((t) => t.P), NearMost);
        }

        /// <summary>
        /// The first of `centres` whose 3 × 3 round it can be an evacuation with its bus beside it (ParkBus), cleared and
        /// kept: the evacuation; empty if none. With `most`, only where the walk to it with the bus standing is no longer
        /// (WalkWith), among the first NearTries with room.
        /// </summary>
        private static List<Vec2> FirstEvac(Canvas c, IEnumerable<Vec2> centres, int? most = null)
        {
            var tries = 0;
            foreach (var p in centres)
            {
                var zone = TilesIn(Rect(p.X - 1, p.Y - 1, 3, 3));
                var bus = ParkBus(c, zone, new Rng(0), true);
                if (bus == null) continue;
                if (most != null)
                {
                    if (++tries > NearTries) break;
                    var walk = WalkWith(c, zone, bus.Tiles);
                    if (walk < 0 || walk > most) continue;
                }
                foreach (var q in zone)
                {
                    c.Force(q.X, q.Y, TileKinds.Floor);
                    c.Reserve(q.X, q.Y);
                }
                return zone;
            }
            return new List<Vec2>();
        }

        // ------------------------------------------------------- the hospital only inside

        /// <summary>
        /// The hospital only inside (H7): the town's hospital (Town, docs/MAPAS.md §4.2: 16 across, 24–30 deep, two wings and
        /// the corridor between, three storeys), its main entrance on the strip in front of it (HospitalStrip deep: a street
        /// and its pavement), and all else out of play (the outline's ground, HospitalMargin round it).
        /// </summary>
        public static readonly IReadOnlyList<int> HospitalDepth = new[] { 24, 30 };
        public const int HospitalAcross = 16;
        public const int HospitalStrip = 6;
        public const int HospitalMargin = 2;
        /// <summary>The strip's street, its first rows (La Lata's); the rest, the pavement before the door.</summary>
        private const int HospitalStreet = 3;

        /// <summary>The map's size: the hospital at its deepest, the strip and the outline round it.</summary>
        public static (int Width, int Height) HospitalOnlySize => (HospitalAcross + 2 * HospitalMargin, HospitalStrip + HospitalDepth[1] + HospitalMargin);

        /// <summary>
        /// Lays the hospital only inside on `c` (sized HospitalOnlySize): the strip across the top, the hospital under it, its
        /// front (and main door) onto the strip, the rest out of play (a wall on the outline's ground, kept). The squad comes
        /// in on the strip before the main door. The hospital's outline: the objective's rect.
        /// </summary>
        private static Rect HospitalOnly(Canvas c, Rng rng, Style style)
        {
            var depth = rng.Range(HospitalDepth[0], HospitalDepth[1]);
            var footprint = Rect(HospitalMargin, HospitalStrip, HospitalAcross, depth);
            c.EvacArea = null;
            for (var y = 0; y < c.Height; y++)
            {
                for (var x = 0; x < c.Width; x++)
                {
                    var i = c.I(x, y);
                    if (y < HospitalStrip)
                    {
                        c.Tiles[i] = TileKinds.Floor;
                        c.Ground[i] = y < HospitalStreet ? Road : Open;
                    }
                    else if (!InRect(footprint, V(x, y)))
                    {
                        c.Tiles[i] = TileKinds.Wall;
                        c.Ground[i] = Out;
                        c.Keep[i] = true;
                    }
                }
            }
            var b = MakeBuilding(c, footprint, rng, Plans.Rooms, 0, style, Furnishes.Clinic, MedicalSiteKinds.Hospital);
            b.Storeys = 3;
            b.MedicalWear = 0.85;
            // Before the main door, the one nearest the middle of the front; the middle with none.
            var mid = footprint.X + footprint.W / 2;
            var doors = Enumerable.Range(footprint.X, footprint.W).Where((x) => c.Get(x, footprint.Y) == TileKinds.Door || c.Get(x, footprint.Y) == TileKinds.DoorOpen).OrderBy((x) => Math.Abs(x - mid)).ThenBy((x) => x).ToList();
            c.Squad = V(doors.Count > 0 ? doors[0] : mid, HospitalStrip - 3);
            foreach (var p in TilesIn(Rect(c.Squad.X - 2, c.Squad.Y - 2, 5, 5))) c.Reserve(p.X, p.Y);
            return b.Outer;
        }

        /// <summary>
        /// The hospital's evacuation (H7): on the strip, out of where the squad comes in, the nearest to it with the bus's room
        /// on the street. Empty if none.
        /// </summary>
        private static List<Vec2> HospitalEvac(Canvas c)
        {
            bool Fits(Vec2 p) => TilesIn(Rect(p.X - 1, p.Y - 1, 3, 3)).All((q) => c.In(q.X, q.Y) && q.Y < HospitalStrip && c.Get(q.X, q.Y) == TileKinds.Floor && c.Free(q.X, q.Y));
            var centres = TilesIn(Rect(0, 0, c.Width, HospitalStrip)).Where(Fits).OrderBy((p) => Dist(p, c.Squad)).ThenBy((p) => c.I(p.X, p.Y));
            return FirstEvac(c, centres);
        }

        // ------------------------------------------------- the offices and the shed only inside

        /// <summary>The offices (H9 §1): the city's office block (BigBuilding: 11–14 across, up to 18 deep, 2–3 storeys), at its biggest.</summary>
        public static readonly IReadOnlyList<int> OfficesAcross = new[] { 12, 14 };
        public static readonly IReadOnlyList<int> OfficesDepth = new[] { 16, 18 };
        public const int OfficesStrip = 6;
        /// <summary>The shed (H9 §2): the factory's main shed (Facility, MainHall: 24–30 along its docks, 16–20 deep); its strip deeper, the lorries back onto it.</summary>
        public static readonly IReadOnlyList<int> ShedAcross = new[] { 24, 30 };
        public static readonly IReadOnlyList<int> ShedDepth = new[] { 16, 20 };
        public const int ShedStrip = 10;

        public static (int Width, int Height) OfficesOnlySize => (OfficesAcross[1] + 2 * HospitalMargin, OfficesStrip + OfficesDepth[1] + HospitalMargin);
        public static (int Width, int Height) ShedOnlySize => (ShedAcross[1] + 2 * HospitalMargin, ShedStrip + ShedDepth[1] + HospitalMargin);

        /// <summary>
        /// A map that is one building (H9), as the hospital's (HospitalOnly): the strip across the top (`strip` deep: a street
        /// of HospitalStreet and what is before the front), the building on `footprint` under it, the rest out of play.
        /// </summary>
        private static void OneBuildingGround(Canvas c, Rect footprint, int strip, int beforeFront)
        {
            c.EvacArea = null;
            for (var y = 0; y < c.Height; y++)
            {
                for (var x = 0; x < c.Width; x++)
                {
                    var i = c.I(x, y);
                    if (y < strip)
                    {
                        c.Tiles[i] = TileKinds.Floor;
                        c.Ground[i] = y < HospitalStreet ? Road : beforeFront;
                    }
                    else if (!InRect(footprint, V(x, y)))
                    {
                        c.Tiles[i] = TileKinds.Wall;
                        c.Ground[i] = Out;
                        c.Keep[i] = true;
                    }
                }
            }
        }

        /// <summary>Where the squad comes in (H9): on the strip before the way in at `x`, three tiles out; kept clear round it.</summary>
        private static void SquadBefore(Canvas c, int x, int strip)
        {
            c.Squad = V(x, strip - 3);
            foreach (var p in TilesIn(Rect(c.Squad.X - 2, c.Squad.Y - 2, 5, 5))) c.Reserve(p.X, p.Y);
        }

        /// <summary>
        /// The offices only inside (H9 §1): the city's office block (BigBuilding's: a corridor two wide with offices off it,
        /// or open plan; two or three storeys; furnished as offices), its main door onto the strip. Its rooms' uses are worked
        /// out at the end (OfficeRooms).
        /// </summary>
        private static Rect OfficesOnly(Canvas c, Rng rng, Style style)
        {
            var across = rng.Range(OfficesAcross[0], OfficesAcross[1]);
            var depth = rng.Range(OfficesDepth[0], OfficesDepth[1]);
            var footprint = Rect(HospitalMargin + (OfficesAcross[1] - across) / 2, OfficesStrip, across, depth);
            OneBuildingGround(c, footprint, OfficesStrip, Open);
            var b = MakeBuilding(c, footprint, rng, rng.Next() < 0.7 ? Plans.Corridor : Plans.Open, 0, style, Furnishes.Office);
            b.Storeys = rng.Next() < 0.5 ? 3 : 2;
            b.InteriorKind = InteriorKinds.Offices;
            var mid = footprint.X + footprint.W / 2;
            var doors = Enumerable.Range(footprint.X, footprint.W).Where((x) => c.Get(x, footprint.Y) == TileKinds.Door || c.Get(x, footprint.Y) == TileKinds.DoorOpen).OrderBy((x) => Math.Abs(x - mid)).ThenBy((x) => x).ToList();
            var at = doors.Count > 0 ? doors[0] : mid;
            b.InteriorEntrance = V(at, footprint.Y);
            SquadBefore(c, at, OfficesStrip);
            return b.Outer;
        }

        /// <summary>
        /// The shed only inside (H9 §2): the factory's main shed (MainHall: docks in its front onto the strip, lorries backed
        /// onto all but one, the loading floor clear, shelves and machines, under skylights), one storey; the foreman's office
        /// in one back corner (the hall's own, or made) and the lockers in the other; a forklift on the loading floor and a
        /// container at the back where they fit. The squad comes in before the dock with no lorry.
        /// </summary>
        private static Rect ShedOnly(Canvas c, Rng rng, Style style)
        {
            var across = rng.Range(ShedAcross[0], ShedAcross[1]);
            var depth = rng.Range(ShedDepth[0], ShedDepth[1]);
            var footprint = Rect(HospitalMargin + (ShedAcross[1] - across) / 2, ShedStrip, across, depth);
            OneBuildingGround(c, footprint, ShedStrip, Yard);
            var hall = MainHall(c, footprint, 0, rng, style);
            hall.Storeys = 1;
            var inner = Inset(footprint, 1);
            // Its shelving tall (H9 §2: «estanterías altas, tapan»): what blocked the way blocks the sight too.
            foreach (var p in TilesIn(inner)) if (c.Get(p.X, p.Y) == TileKinds.Low && Decor.DecorOf(c.Decor[c.I(p.X, p.Y)])?.Kind == DecorKinds.Shelf) c.Force(p.X, p.Y, TileKinds.High);
            // The hall's office (HallPlan: a back corner, not always), and the lockers in the other corner.
            var office = hall.Rooms.Skip(1).Select((r) => (Rect?)r).FirstOrDefault();
            var right = office != null ? office.X > inner.X + inner.W / 2 : rng.Int(2) == 0;
            if (office == null)
            {
                office = CornerRoom(c, footprint, right, rng);
                FurnishRoom(c, office, rng, Furnishes.Office);
            }
            var lockers = CornerRoom(c, footprint, !right, rng);
            for (var u = 0; u < lockers.W - 1; u++) Put(c, V(lockers.X + u + (right ? 1 : 0), lockers.Y + lockers.H - 1), TileKinds.High, new Look(DecorKinds.Cabinet, 0));
            // A forklift on the loading floor, by a side wall.
            foreach (var u in new[] { 1, inner.W - 2 }) if (Put(c, V(inner.X + u, inner.Y + 1), TileKinds.High, new Look(DecorKinds.Forklift, 2))) break;
            // A container (2 × 4, Arte 17/11) in the middle of the floor, the shelves cleared round it (no room was ever
            // left for it between them: Arte, 17/11).
            var box = TilesIn(Rect(0, 0, 4, 2)).Select((q) => P(q.X, q.Y, TileKinds.High, new Look(DecorKinds.Container, 1))).ToList();
            var spot = V(inner.X + inner.W / 2 - 2, inner.Y + inner.H / 2 - 1);
            foreach (var p in TilesIn(Rect(spot.X - 1, spot.Y - 1, 6, 4)))
            {
                if (c.Get(p.X, p.Y) != TileKinds.High && c.Get(p.X, p.Y) != TileKinds.Low) continue;
                c.Force(p.X, p.Y, TileKinds.Floor);
                c.Decor[c.I(p.X, p.Y)] = 0;
            }
            Group(c, spot, box);
            // Crates and drums in turn along the side walls, between the loading bay and the corner rooms; in the loading bay's
            // corners, pallets and a crate or a drum.
            var lows = new[] { DecorKinds.Crates, DecorKinds.Drums };
            var turn = 0;
            foreach (var u in new[] { 0, inner.W - 1 })
            {
                for (var v = 4; v < inner.H - 6; v += 4) if (Put(c, V(inner.X + u, inner.Y + v), TileKinds.Low, new Look(lows[turn % 2], u == 0 ? 1 : 3))) turn++;
                Put(c, V(inner.X + u, inner.Y), TileKinds.Low, new Look(lows[turn++ % 2], 2));
                Put(c, V(inner.X + (u == 0 ? 1 : u - 1), inner.Y), TileKinds.Low, new Look(DecorKinds.Pallets, 2));
            }
            hall.InteriorKind = InteriorKinds.Shed;
            hall.InteriorRooms = new List<InteriorRoom>
            {
                Room(inner, ShedUses.Floor),
                Room(Rect(inner.X, inner.Y, inner.W, 3), ShedUses.LoadingBay),
                Room(office, ShedUses.Office),
                Room(lockers, ShedUses.Lockers),
            };
            // Before the dock with no lorry backed onto it: the opening in the front whose tile out is clear, the middle one first.
            var mid = footprint.X + footprint.W / 2;
            var ways = Enumerable.Range(footprint.X + 1, footprint.W - 2)
                .Where((x) => c.Get(x, footprint.Y) != TileKinds.Wall && c.Get(x, footprint.Y) != TileKinds.Window && c.Get(x, footprint.Y) != TileKinds.WindowBroken)
                .OrderBy((x) => c.Get(x, footprint.Y - 2) == TileKinds.Floor ? 0 : 1)
                .ThenBy((x) => Math.Abs(x - mid))
                .ThenBy((x) => x)
                .ToList();
            var at = ways.Count > 0 ? ways[0] : mid;
            hall.InteriorEntrance = V(at, footprint.Y);
            SquadBefore(c, at, ShedStrip);
            return hall.Outer;
        }

        private static InteriorRoom Room(Rect r, string use, int z = 0) => new InteriorRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = z, Use = use };

        /// <summary>
        /// A room of 4 × 4 in a back corner of a hall (front on the north, as HallPlan's office): what stood there cleared,
        /// its two walls and a door in its front one. The room.
        /// </summary>
        private static Rect CornerRoom(Canvas c, Rect b, bool right, Rng rng)
        {
            var inner = Inset(b, 1);
            var across = inner.W;
            var depth = inner.H;
            var u0 = right ? across - 4 : 0;
            var wallU = right ? across - 5 : 4;
            for (var v = depth - 5; v < depth; v++)
            {
                for (var u = Math.Min(u0, wallU); u <= Math.Max(u0 + 3, wallU); u++)
                {
                    var p = LocalPt(inner, 0, u, v);
                    c.Force(p.X, p.Y, TileKinds.Floor);
                    c.Decor[c.I(p.X, p.Y)] = 0;
                }
            }
            for (var v = depth - 4; v < depth; v++)
            {
                var p = LocalPt(inner, 0, wallU, v);
                c.Force(p.X, p.Y, TileKinds.Wall);
            }
            for (var k = 0; k < 5; k++)
            {
                var p = LocalPt(inner, 0, Math.Min(u0, wallU) + k, depth - 5);
                c.Force(p.X, p.Y, TileKinds.Wall);
            }
            Opening(c, LocalPt(inner, 0, u0 + 1 + rng.Int(2), depth - 5), 0, TileKinds.Door);
            return Local(inner, 0, u0, depth - 4, 4, 4);
        }

        /// <summary>La Lata on a strip `strip` deep (H9, as the hospital's HospitalEvac): out of where the squad comes in, the nearest to it with the bus's room.</summary>
        private static List<Vec2> StripEvac(Canvas c, int strip)
        {
            bool Fits(Vec2 p) => TilesIn(Rect(p.X - 1, p.Y - 1, 3, 3)).All((q) => c.In(q.X, q.Y) && q.Y < strip && c.Get(q.X, q.Y) == TileKinds.Floor && c.Free(q.X, q.Y));
            var centres = TilesIn(Rect(0, 0, c.Width, strip)).Where(Fits).OrderBy((p) => Dist(p, c.Squad)).ThenBy((p) => c.I(p.X, p.Y));
            return FirstEvac(c, centres);
        }

        // ----------------------------------------------------------- the police station

        /// <summary>The police station (H9 §3): some 18 × 24, its strip in front, a yard behind its back door out to the map's edge.</summary>
        public const int PoliceAcross = 18;
        public const int PoliceDepth = 24;
        public const int PoliceStrip = 6;
        public const int PoliceYard = 3;

        public static (int Width, int Height) PoliceOnlySize => (PoliceAcross + 2 * HospitalMargin, PoliceStrip + PoliceDepth + PoliceYard);

        /// <summary>
        /// The police station only inside (H9 §3), a building of its own on a plan of its own (inside, 16 × 22, the front on
        /// the north):
        /// the lobby (reception, its benches and a vending machine) behind the double main door, the counter (a wall with a
        /// window of two, its door) and the duty office behind it; a corridor of four to the back door and the yard; down its
        /// west side, against the outer wall, 3–5 cells of 2 × 3 (a bed and a toilet, their front of bars onto the corridor,
        /// solid walls between: Arturo, 17/11; the last one a row deeper where it meets the back wall) and the toilets after
        /// them; every wall one tile thick (Arte, 17/11); down its east side the armoury (racks, a locker, the cases and the best
        /// box), the evidence room (shelves), the interrogation room (its table), the lockers and the kitchen. One storey or
        /// two (offices up there; its stairs in a corner of the lobby). Two patrol cars on the strip, cover only (Arte: no
        /// boot to open). The cells are solid while the map is made (nothing joins them to the rest) and turn to floor and
        /// bars at the end (InteriorOf).
        /// </summary>
        private static Rect PoliceOnly(Canvas c, Rng rng, Style style)
        {
            var footprint = Rect(HospitalMargin, PoliceStrip, PoliceAcross, PoliceDepth);
            OneBuildingGround(c, footprint, PoliceStrip, Open);
            foreach (var p in TilesIn(Rect(0, footprint.Y + footprint.H, c.Width, PoliceYard)))
            {
                var i = c.I(p.X, p.Y);
                c.Tiles[i] = TileKinds.Floor;
                c.Ground[i] = Yard;
                c.Keep[i] = false;
            }
            var b = MakeBuilding(c, footprint, rng, Plans.Open, 0, style, Furnishes.Office);
            b.Storeys = rng.Int(2) == 0 ? 2 : 1;
            var inner = Inset(footprint, 1);
            // Its own plan: what the builder put inside goes, and every door in its outer walls.
            foreach (var p in TilesIn(inner))
            {
                c.Force(p.X, p.Y, TileKinds.Floor);
                c.Decor[c.I(p.X, p.Y)] = 0;
                c.Keep[c.I(p.X, p.Y)] = false;
            }
            c.Objects.RemoveAll((o) => o.Any((t) => InRect(footprint, V(t.I % c.Width, t.I / c.Width))));
            foreach (var p in TilesIn(footprint)) if (!InRect(inner, p) && (c.Get(p.X, p.Y) == TileKinds.Door || c.Get(p.X, p.Y) == TileKinds.DoorOpen)) c.Force(p.X, p.Y, TileKinds.Wall);
            Vec2 At(int ix, int iy) => V(inner.X + ix, inner.Y + iy);
            void Wall(int ix, int iy)
            {
                var p = At(ix, iy);
                c.Force(p.X, p.Y, TileKinds.Wall);
            }
            void Door(int ix, int iy, int side) => Opening(c, At(ix, iy), side, TileKinds.Door);
            Rect Area(int ix, int iy, int w, int h) => Rect(inner.X + ix, inner.Y + iy, w, h);
            void Thing(int ix, int iy, string kind, string look, int facing) => Put(c, At(ix, iy), kind, new Look(look, facing));
            var rooms = new List<InteriorRoom>();
            // The main door, double, onto the strip; the back door onto the yard.
            foreach (var ix in new[] { 4, 5 }) Opening(c, V(inner.X + ix, footprint.Y), 0, TileKinds.Door);
            Opening(c, V(inner.X + 6, footprint.Y + footprint.H - 1), 2, TileKinds.Door);
            // No window onto the cells' back.
            for (var y = inner.Y + 7; y < inner.Y + inner.H; y++) if (Grid.IsWindow(c.Get(footprint.X, y))) c.Force(footprint.X, y, TileKinds.Wall);
            // The counter: a wall with its window of two between the lobby and the duty office, and its door.
            for (var iy = 0; iy < 6; iy++) Wall(10, iy);
            foreach (var iy in new[] { 2, 3 })
            {
                var p = At(10, iy);
                c.Force(p.X, p.Y, TileKinds.Window);
            }
            b.Counter = At(10, 2);
            Door(10, 5, 1);
            for (var ix = 0; ix < 16; ix++) Wall(ix, 6);
            Door(6, 6, 0);
            rooms.Add(Room(Area(0, 0, 10, 6), PoliceUses.Reception));
            rooms.Add(Room(Area(11, 0, 5, 6), PoliceUses.Office));
            // The corridor's walls: to the west the cells' fronts and the walls between them, to the east the rooms'. Every wall
            // one tile thick (Arte, 17/11: a thicker one the client can't draw): the cells' backs are the outer wall, and the
            // corridor is four wide alongside them, the cell block's hall.
            for (var iy = 7; iy < 22; iy++)
            {
                Wall(3, iy);
                Wall(8, iy);
            }
            var cells = rng.Range(3, 5);
            b.Cells = new List<Rect>();
            b.Bars = new List<Vec2>();
            for (var k = 0; k < cells; k++)
            {
                var y0 = 7 + 3 * k;
                // The last one against the back wall: a row deeper, no wall of its own there.
                var deep = y0 + 2 >= 21 ? 22 - y0 : 2;
                if (deep == 2 && y0 + 2 < 22) for (var ix = 0; ix < 3; ix++) Wall(ix, y0 + 2);
                // A bed along its back, a toilet by it; the rest solid till the end.
                var bed = new[] { At(0, y0), At(0, y0 + 1) };
                foreach (var p in bed) c.Force(p.X, p.Y, TileKinds.Low);
                DressObject(c, bed, DecorKinds.Bed, 2);
                Thing(1, y0, TileKinds.Low, DecorKinds.Wc, 1);
                foreach (var p in TilesIn(Area(0, y0, 3, deep))) if (c.Get(p.X, p.Y) == TileKinds.Floor) c.Force(p.X, p.Y, TileKinds.High);
                b.Cells.Add(Area(0, y0, 3, deep));
                // No window round a cell (the outer wall is its back): nobody climbs in from the yard.
                foreach (var p in TilesIn(Inset(Area(0, y0, 3, deep), -1))) if (Grid.IsWindow(c.Get(p.X, p.Y))) c.Force(p.X, p.Y, TileKinds.Wall);
                b.Bars.Add(At(3, y0));
                b.Bars.Add(At(3, y0 + 1));
                rooms.Add(Room(Area(0, y0, 3, deep), PoliceUses.Cells));
            }
            var below = 7 + 3 * cells;
            if (below + 1 < 22)
            {
                var bath = Area(0, below, 3, 22 - below);
                Door(3, below, 3);
                FurnishRoom(c, bath, rng, Furnishes.Home, global::Bastion.Engine.HomeUses.Bath);
                rooms.Add(Room(bath, PoliceUses.Hygiene));
            }
            // East: the armoury, the evidence room, the interrogation room; the lockers and the kitchen at the end.
            foreach (var iy in new[] { 11, 15, 19 }) for (var ix = 9; ix < 16; ix++) Wall(ix, iy);
            for (var iy = 20; iy < 22; iy++) Wall(12, iy);
            Door(8, 8, 1);
            Door(8, 13, 1);
            Door(8, 17, 1);
            Door(8, 20, 1);
            Door(12, 21, 1);
            for (var iy = 7; iy < 11; iy++) Thing(15, iy, TileKinds.High, DecorKinds.Rack, 3);
            Thing(9, 7, TileKinds.High, DecorKinds.Cabinet, 1);
            Thing(11, 10, TileKinds.Low, DecorKinds.Crates, 0);
            Thing(13, 10, TileKinds.Low, DecorKinds.Crates, 0);
            Thing(12, 8, TileKinds.Low, DecorKinds.LootWeapons, 0);
            for (var iy = 12; iy < 15; iy++) Thing(15, iy, TileKinds.High, DecorKinds.Shelf, 3);
            foreach (var ix in new[] { 11, 12, 13 }) Thing(ix, 14, TileKinds.High, DecorKinds.Shelf, 0);
            foreach (var ix in new[] { 12, 13 }) Thing(ix, 17, TileKinds.Low, DecorKinds.Table, 0);
            foreach (var iy in new[] { 20, 21 }) Thing(11, iy, TileKinds.High, DecorKinds.Cabinet, 3);
            var kitchen = Area(13, 20, 3, 2);
            FurnishRoom(c, kitchen, rng, Furnishes.Home, global::Bastion.Engine.HomeUses.Kitchen);
            rooms.Add(Room(Area(9, 7, 7, 4), PoliceUses.Armory));
            rooms.Add(Room(Area(9, 12, 7, 3), PoliceUses.Evidence));
            rooms.Add(Room(Area(9, 16, 7, 3), PoliceUses.Interrogation));
            rooms.Add(Room(Area(9, 20, 3, 2), PoliceUses.Lockers));
            rooms.Add(Room(kitchen, PoliceUses.Kitchen));
            // The lobby: benches along its south wall, a vending machine by the counter; the duty office its desks.
            var bench = new[] { At(2, 5), At(3, 5) };
            if (bench.All((p) => c.Free(p.X, p.Y)))
            {
                foreach (var p in bench) c.Force(p.X, p.Y, TileKinds.Low);
                DressObject(c, bench, DecorKinds.Bench, 0);
            }
            Thing(9, 0, TileKinds.High, DecorKinds.DrinksFridge, 3);
            FurnishRoom(c, Area(11, 0, 5, 5), rng, Furnishes.Office);
            // Upstairs, offices; the stairs in the lobby's corner.
            if (b.Storeys > 1) b.HouseStairs = Area(0, 1, 2, 4);
            // Two patrol cars on the strip, at its ends.
            foreach (var x in new[] { 1, c.Width - 2 })
            {
                var car = new[] { V(x, 3), V(x, 4), V(x, 5) };
                if (!car.All((p) => c.Free(p.X, p.Y) && c.Get(p.X, p.Y) == TileKinds.Floor)) continue;
                foreach (var p in car) c.Force(p.X, p.Y, TileKinds.High);
                DressObject(c, car, DecorKinds.SirenCar, 0);
            }
            b.InteriorKind = InteriorKinds.Police;
            b.InteriorRooms = rooms;
            b.InteriorEntrance = V(inner.X + 4, footprint.Y);
            SquadBefore(c, inner.X + 4, PoliceStrip);
            return b.Outer;
        }

        // ------------------------------------------------------------ the shopping plaza

        /// <summary>The shopping plaza (H9 §4): its map's side; the square in the middle, the shops round it, an access street across.</summary>
        public const int PlazaSize = 48;
        private const int PlazaSquare = 20;
        private const int ShopDeep = 7;

        /// <summary>The shops' uses but the cinema's, one each (H9 §4), drawn in turn: a pharmacy always among them.</summary>
        private static readonly IReadOnlyList<string> ShopTurn = new[] { ShopUses.Pharmacy, ShopUses.Jewellery, ShopUses.Clothes, ShopUses.Electronics, ShopUses.Sports, ShopUses.Cafe, ShopUses.Toys, ShopUses.Empty };

        /// <summary>
        /// The shopping plaza (H9 §4), out of doors, ground floor only: a square of PlazaSquare (the town's: a fountain,
        /// planters, benches and kiosks, Plaza) with a shop row on each side, one storey each, their fronts glass onto it
        /// (windows: they let sight through and break with a shot or a blow, and with noise, as today's; their door double);
        /// an access street across it from edge to edge, La Lata at its north end (PlazaEvac), the squad coming in at its
        /// south. Nine shops: two either side of the street to the north and the south, three down each side, the cinema two
        /// of them (its lobby only: Arte, 17/11); the rest of what is drawn, out of play. Its square and its shops on the
        /// canvas for its site (PlazaSite). Its outline: the shops and the square, the objective's rect.
        /// </summary>
        private static Rect PlazaMap(Canvas c, Rng rng, Style style)
        {
            var sq = Rect((PlazaSize - PlazaSquare) / 2, (PlazaSize - PlazaSquare) / 2, PlazaSquare, PlazaSquare);
            var ring = Inset(sq, -ShopDeep);
            var mid = PlazaSize / 2;
            var street = Rect(mid - 3, 0, 6, PlazaSize);
            c.EvacArea = null;
            for (var y = 0; y < c.Height; y++)
            {
                for (var x = 0; x < c.Width; x++)
                {
                    var i = c.I(x, y);
                    var p = V(x, y);
                    if (InRect(sq, p) || InRect(street, p))
                    {
                        c.Tiles[i] = TileKinds.Floor;
                        c.Ground[i] = InRect(sq, p) || x == street.X || x == street.X + street.W - 1 ? Open : Road;
                    }
                    else
                    {
                        c.Tiles[i] = TileKinds.Wall;
                        c.Ground[i] = Out;
                        c.Keep[i] = true;
                    }
                }
            }
            Plaza(c, sq, rng, true);
            // The slots: north and south, either side of the street; three down each side; the cinema two of a side's.
            var slots = new List<(Rect R, int Front)>();
            foreach (var x0 in new[] { sq.X, street.X + street.W })
            {
                var w = x0 == sq.X ? street.X - sq.X : sq.X + sq.W - (street.X + street.W);
                slots.Add((Rect(x0, ring.Y, w, ShopDeep), 2));
                slots.Add((Rect(x0, sq.Y + sq.H, w, ShopDeep), 0));
            }
            var thirds = new[] { 7, 7, sq.H - 14 };
            var cinemaWest = rng.Int(2) == 0;
            foreach (var west in new[] { true, false })
            {
                var x0 = west ? ring.X : sq.X + sq.W;
                if (west == cinemaWest)
                {
                    slots.Add((Rect(x0, sq.Y, ShopDeep, thirds[0] + thirds[1]), west ? 1 : 3));
                    slots.Add((Rect(x0, sq.Y + thirds[0] + thirds[1], ShopDeep, thirds[2]), west ? 1 : 3));
                }
                else
                {
                    var y = sq.Y;
                    foreach (var h in thirds)
                    {
                        slots.Add((Rect(x0, y, ShopDeep, h), west ? 1 : 3));
                        y += h;
                    }
                }
            }
            var cinema = slots.First((x) => Math.Max(x.R.W, x.R.H) > 7 + 7 - 1);
            var turn = Shuffle(ShopTurn.ToList(), rng);
            // A pharmacy always.
            if (!turn.Take(slots.Count - 1).Contains(ShopUses.Pharmacy)) turn.Insert(0, ShopUses.Pharmacy);
            c.Square = sq;
            c.Shops = new List<PlazaShop>();
            var k = 0;
            foreach (var (r, front) in slots)
            {
                var use = r.Equals(cinema.R) ? ShopUses.Cinema : turn[k++];
                foreach (var p in TilesIn(r))
                {
                    c.Ground[c.I(p.X, p.Y)] = Open;
                    c.Keep[c.I(p.X, p.Y)] = false;
                }
                var b = MakeBuilding(c, r, rng, Plans.Shop, front, style, Furnishes.Shop);
                b.Storeys = 1;
                // Ground floor only (H9 §4): no roof to climb onto.
                b.Skylit = true;
                var inner = Inset(r, 1);
                var len = SideLen(r, front);
                // No window but the front's; the front all glass but its corners and its door, the door double.
                foreach (var p in TilesIn(r)) if (!InRect(inner, p) && Grid.IsWindow(c.Get(p.X, p.Y))) c.Force(p.X, p.Y, TileKinds.Wall);
                var doors = Enumerable.Range(1, len - 2).Where((u) => c.Get(LocalPt(r, front, u, 0).X, LocalPt(r, front, u, 0).Y) == TileKinds.Door).ToList();
                var door = doors.Count > 0 ? doors[0] : len / 2;
                var second = door + 1 <= len - 2 ? door + 1 : door - 1;
                foreach (var u in new[] { door, second }) Opening(c, LocalPt(r, front, u, 0), front, TileKinds.Door);
                for (var u = 1; u <= len - 2; u++)
                {
                    var p = LocalPt(r, front, u, 0);
                    if (c.Get(p.X, p.Y) == TileKinds.Wall) c.Force(p.X, p.Y, TileKinds.Window);
                }
                // What is inside, by its use.
                void Clear()
                {
                    foreach (var p in TilesIn(inner))
                    {
                        if (c.Keep[c.I(p.X, p.Y)] && c.Get(p.X, p.Y) == TileKinds.Floor) continue;
                        c.Force(p.X, p.Y, TileKinds.Floor);
                        c.Decor[c.I(p.X, p.Y)] = 0;
                    }
                    c.Objects.RemoveAll((o) => o.Any((t) => InRect(inner, V(t.I % c.Width, t.I / c.Width))));
                }
                var back = Opposite(front);
                if (use == ShopUses.Empty || use == ShopUses.Cinema || use == ShopUses.Cafe) Clear();
                if (use == ShopUses.Cinema)
                {
                    // The lobby: its counter across the back.
                    for (var u = 1; u < SideLen(inner, front) - 1; u++) Put(c, LocalPt(inner, front, u, DepthOf(inner, front) - 2), TileKinds.Low, new Look(DecorKinds.Counter, front));
                }
                else if (use == ShopUses.Cafe)
                {
                    Put(c, LocalPt(inner, back, 1, 0), TileKinds.Low, new Look(DecorKinds.Counter, front));
                    Put(c, LocalPt(inner, back, 2, 0), TileKinds.Low, new Look(DecorKinds.Counter, front));
                    foreach (var (u, d) in new[] { (1, 2), (3, 2) }) Put(c, LocalPt(inner, front, u, d), TileKinds.Low, new Look(DecorKinds.CafeTable, 0));
                }
                else if (use == ShopUses.Pharmacy)
                {
                    foreach (var p in TilesIn(inner)) if (Decor.DecorOf(c.Decor[c.I(p.X, p.Y)])?.Kind == DecorKinds.Shelf) c.Dress(p, DecorKinds.MedicalShelf, Decor.DecorOf(c.Decor[c.I(p.X, p.Y)])!.Facing);
                }
                // Looted: an empty shop's glass broken, and now and then another's.
                if (use == ShopUses.Empty || rng.Next() < 0.25)
                {
                    var glass = Enumerable.Range(1, len - 2).Select((u) => LocalPt(r, front, u, 0)).Where((p) => c.Get(p.X, p.Y) == TileKinds.Window).ToList();
                    if (glass.Count > 0)
                    {
                        var pane = glass[rng.Int(glass.Count)];
                        c.Force(pane.X, pane.Y, TileKinds.WindowBroken);
                    }
                }
                c.Shops.Add(new PlazaShop { Outer = r, Room = inner, Use = use, Door = LocalPt(r, front, door, 0), Front = front });
            }
            c.Squad = V(mid, PlazaSize - 4);
            c.SquadSide = 2;
            foreach (var p in TilesIn(Rect(c.Squad.X - 2, c.Squad.Y - 2, 5, 5))) c.Reserve(p.X, p.Y);
            return ring;
        }

        /// <summary>
        /// The plaza's La Lata (H9 §4): on its access street to the north, at its end by the square, where the bus has room:
        /// some 33 on foot from where the squad comes in (Arturo, 17/11: a Saqueo's, some 30).
        /// </summary>
        private static List<Vec2> PlazaEvac(Canvas c)
        {
            var mid = PlazaSize / 2;
            var centres = TilesIn(Rect(mid - 2, 1, 4, (PlazaSize - PlazaSquare) / 2 - ShopDeep + ShopDeep - 2)).OrderByDescending((p) => p.Y).ThenBy((p) => Math.Abs(p.X - mid)).ThenBy((p) => p.X);
            return FirstEvac(c, centres);
        }

        /// <summary>
        /// The plaza's site (H9 §4) in the map's own frame: its bounds (the square and its shops), each shop a room with its
        /// use, and its marks: on the furniture by the shop's use (display cases, racks, shelves, the counter and its till),
        /// as scenery inside (mannequins, the cinema's popcorn, posters, projector and sofas, broken glass in an empty one),
        /// and out of doors: an awning and a sign over each front, broken glass before a broken pane, the square's planters,
        /// two billboards by the street.
        /// </summary>
        private static InteriorSite PlazaSite(Canvas c, Func<Vec2, Vec2> T, int FW, List<string> tiles, List<int> decor, int seed, List<Vec2> keep)
        {
            Rect Turned(Rect r)
            {
                var a = T(V(r.X, r.Y));
                var z = T(V(r.X + r.W - 1, r.Y + r.H - 1));
                return Rect(Math.Min(a.X, z.X), Math.Min(a.Y, z.Y), Math.Abs(a.X - z.X) + 1, Math.Abs(a.Y - z.Y) + 1);
            }
            int Way(Vec2 from, Vec2 to)
            {
                var a = T(from);
                var b = T(to);
                return Math.Max(0, FindIndex(Grid.Dirs4, (d) => d.X == b.X - a.X && d.Y == b.Y - a.Y));
            }
            var ring = Inset(c.Square!, -ShopDeep);
            var bounds = Turned(ring);
            var site = new InteriorSite { Kind = InteriorKinds.Plaza, X = bounds.X, Y = bounds.Y, W = bounds.W, H = bounds.H };
            var rng = new Rng(seed ^ 0x91a2a);
            var kept = new HashSet<int>(keep.Select((p) => p.Y * FW + p.X));
            var marked = new HashSet<int>();
            void Mark(string kind, Vec2 p, int facing, bool floor = true)
            {
                site.Dressing.Add(new InteriorDetail { Kind = kind, X = p.X, Y = p.Y, Z = 0, Facing = facing });
                if (floor) marked.Add(p.Y * FW + p.X);
            }
            bool Free(Vec2 p)
            {
                var i = p.Y * FW + p.X;
                return i >= 0 && i < tiles.Count && tiles[i] == TileKinds.Floor && decor[i] == 0 && !kept.Contains(i) && !marked.Contains(i);
            }
            int? ByWall(Vec2 p)
            {
                if (!Free(p)) return null;
                for (var s = 0; s < 4; s++)
                {
                    var q = Add(p, Grid.Dirs4[s]);
                    var j = q.Y * FW + q.X;
                    if (j >= 0 && j < tiles.Count && tiles[j] == TileKinds.Wall) return Opposite(s);
                }
                return null;
            }
            void Some(Rect r, string kind, int count, bool wall)
            {
                var spots = TilesIn(r).Where((p) => wall ? ByWall(p) != null : Free(p)).ToList();
                for (var k = 0; k < count && spots.Count > 0; k++)
                {
                    var p = spots[rng.Int(spots.Count)];
                    spots.Remove(p);
                    if (!Free(p)) continue;
                    Mark(kind, p, wall ? ByWall(p)!.Value : rng.Int(4));
                }
            }
            foreach (var shop in c.Shops!)
            {
                var room = Turned(shop.Room);
                site.Rooms.Add(new InteriorRoom { X = room.X, Y = room.Y, W = room.W, H = room.H, Z = 0, Use = shop.Use });
                var outward = Way(shop.Door, Add(shop.Door, Grid.Dirs4[shop.Front]));
                // On the furniture.
                var counters = 0;
                var cases = 0;
                foreach (var p in TilesIn(room))
                {
                    var look = Decor.DecorOf(decor[p.Y * FW + p.X]);
                    if (look == null) continue;
                    string? kind = null;
                    if (look.Kind == DecorKinds.Counter)
                    {
                        if (shop.Use == ShopUses.Jewellery) kind = new[] { PlazaDetailKinds.RingDisplay, PlazaDetailKinds.WatchDisplay, PlazaDetailKinds.DisplayCase }[cases++ % 3];
                        else if (shop.Use == ShopUses.Electronics) kind = PlazaDetailKinds.DisplayCase;
                        else
                        {
                            kind = PlazaDetailKinds.Counter;
                            if (counters++ == 0) Mark(PlazaDetailKinds.Till, p, look.Facing, false);
                        }
                    }
                    else if (look.Kind == DecorKinds.Shelf || look.Kind == DecorKinds.MedicalShelf) kind = shop.Use == ShopUses.Clothes ? PlazaDetailKinds.ClothesRack : shop.Use == ShopUses.Jewellery ? PlazaDetailKinds.DisplayCase : PlazaDetailKinds.Shelf;
                    if (kind != null) Mark(kind, p, look.Facing, false);
                }
                // Scenery inside, by its use.
                switch (shop.Use)
                {
                    case ShopUses.Clothes:
                        Some(room, PlazaDetailKinds.Mannequin, 2, false);
                        break;
                    case ShopUses.Sports:
                        Some(room, PlazaDetailKinds.Mannequin, 1, false);
                        break;
                    case ShopUses.Cinema:
                        Some(room, PlazaDetailKinds.Popcorn, 1, true);
                        Some(room, PlazaDetailKinds.MoviePoster, 3, true);
                        Some(room, PlazaDetailKinds.Projector, 1, true);
                        Some(room, PlazaDetailKinds.Sofa, 2, false);
                        break;
                    case ShopUses.Cafe:
                        Some(room, PlazaDetailKinds.Sofa, 1, true);
                        break;
                    case ShopUses.Empty:
                        Some(room, PlazaDetailKinds.BrokenGlass, 2, false);
                        break;
                }
                // Out of doors: its awning before its door, its sign on its front by the door; broken glass before a broken pane.
                var before = T(Add(shop.Door, Grid.Dirs4[shop.Front]));
                if (Free(before)) Mark(PlazaDetailKinds.Awning, before, outward, false);
                var len = SideLen(shop.Outer, shop.Front);
                var sign = Enumerable.Range(1, len - 2).Select((u) => LocalPt(shop.Outer, shop.Front, u, 0)).FirstOrDefault((p) => !(p.X == shop.Door.X && p.Y == shop.Door.Y));
                if (sign != null) Mark(PlazaDetailKinds.ShopSign, T(sign), outward, false);
                for (var u = 1; u <= len - 2; u++)
                {
                    var pane = LocalPt(shop.Outer, shop.Front, u, 0);
                    var at = T(pane);
                    if (tiles[at.Y * FW + at.X] != TileKinds.WindowBroken) continue;
                    var outside = T(Add(pane, Grid.Dirs4[shop.Front]));
                    if (Free(outside)) Mark(PlazaDetailKinds.BrokenGlass, outside, rng.Int(4));
                }
            }
            // The square's planters; two billboards on the shops' walls by the street, facing it.
            foreach (var p in TilesIn(Turned(c.Square!))) if (Decor.DecorOf(decor[p.Y * FW + p.X])?.Kind == DecorKinds.Planter) Mark(PlazaDetailKinds.Planter, p, Decor.DecorOf(decor[p.Y * FW + p.X])!.Facing, false);
            var mid = PlazaSize / 2;
            foreach (var (x, toward) in new[] { (mid - 4, mid - 3), (mid + 3, mid + 2) })
            {
                var y = c.Square!.Y - ShopDeep / 2 - 1;
                Mark(PlazaDetailKinds.Billboard, T(V(x, y)), Way(V(x, y), V(toward, y)), false);
            }
            return site;
        }

        // ------------------------------------------------------------------ the camping

        /// <summary>The camping (H9 §5): its map's side and its clearing's radius.</summary>
        public const int CampingSize = 48;
        private const int ClearingRadius = 17;

        /// <summary>
        /// The camping (H9 §5), out of doors: a clearing in the pines (the base's: `high` tiles of `tree`, the client's
        /// BaseWoods), its edge uneven; a dirt road from the north edge to a turning place where La Lata waits (CampingEvac),
        /// the squad coming in by a path from the south, two more paths east and west (the horde's). In it: the fire pit in the
        /// middle, chairs round it; pitches with dome tents (2 × 2) and long ones (2 × 3), `low`, that cover; 3–5 caravans (4
        /// × 8 or 4 × 9, a room of 2 × 6 or 2 × 7 inside, their door on their side: buildings, found as a house is); a row of
        /// portable toilets; the military or quarantine area fenced, its tents (4 × 4 or 6 × 6, the quarantine one 4 × 5,
        /// `high`) at its back, room before them (a sabotage's ambulance: Alarms.Ambulance, Lore 17/11). Its parts and marks
        /// on the canvas for its site (CampingSite). Its rect: a sabotage's, the military area; else the clearing.
        /// </summary>
        private static Rect CampingMap(Canvas c, Rng rng, Style style, string kind)
        {
            var cx = c.Width / 2;
            var cy = c.Height / 2 + 1;
            var R = ClearingRadius;
            c.EvacArea = null;
            var jitter = Enumerable.Range(0, 7).Select((_) => rng.Range(-2, 1)).ToArray();
            bool Clearing(int x, int y)
            {
                var dy = y - cy;
                if (Math.Abs(dy) > R) return false;
                var half = (int)Math.Floor(Math.Sqrt(R * R - dy * dy)) + jitter[(y + 7 * 7) % 7];
                return Math.Abs(x - cx) <= half;
            }
            void Opened(Rect r, int ground)
            {
                foreach (var p in TilesIn(r))
                {
                    if (!c.In(p.X, p.Y)) continue;
                    var i = c.I(p.X, p.Y);
                    c.Tiles[i] = TileKinds.Floor;
                    c.Decor[i] = 0;
                    c.Ground[i] = ground;
                    c.Keep[i] = false;
                }
            }
            for (var y = 0; y < c.Height; y++)
            {
                for (var x = 0; x < c.Width; x++)
                {
                    var i = c.I(x, y);
                    c.Ground[i] = Open;
                    if (Clearing(x, y))
                    {
                        c.Tiles[i] = TileKinds.Floor;
                        continue;
                    }
                    c.Tiles[i] = TileKinds.High;
                    c.Decor[i] = Decor.DecorCode(DecorKinds.Tree, (x + y) % 4);
                    c.Keep[i] = true;
                }
            }
            // The road in from the north and its turning place; the paths.
            var turning = Rect(cx - 4, cy - R + 1, 9, 6);
            // (Five wide: La Lata's bus stands on it beside its zone and drives off along it.)
            Opened(Rect(cx - 2, 0, 5, turning.Y), Yard);
            Opened(turning, Yard);
            Opened(Rect(cx, cy + R - 3, 2, c.Height - (cy + R - 3)), Yard);
            Opened(Rect(0, cy, cx - R + 3, 2), Yard);
            Opened(Rect(cx + R - 3, cy - 4, c.Width - (cx + R - 3), 2), Yard);
            var parts = new List<(Rect R, string Use)>();
            var marks = new List<(Vec2 At, string Kind, int Facing)>();
            // What the camp stands, kept: nothing clears it (the bus's way out, a way linked through).
            void Block(Rect r, string tile)
            {
                foreach (var p in TilesIn(r))
                {
                    c.Force(p.X, p.Y, tile);
                    c.Reserve(p.X, p.Y);
                }
            }
            bool Room(Rect r) => TilesIn(r).All((p) => c.In(p.X, p.Y) && c.Get(p.X, p.Y) == TileKinds.Floor && c.Free(p.X, p.Y));
            // The military or quarantine area, fenced, its gate to the camp (west).
            var zone = Rect(cx + 3, cy - 9, 12, 9);
            Opened(zone, Open);
            foreach (var p in TilesIn(zone))
            {
                var edge = p.X == zone.X || p.Y == zone.Y || p.X == zone.X + zone.W - 1 || p.Y == zone.Y + zone.H - 1;
                var gate = p.X == zone.X && (p.Y == zone.Y + 4 || p.Y == zone.Y + 5);
                if (edge && !gate && Put(c, p, TileKinds.Low, new Look(DecorKinds.Fence, p.Y == zone.Y || p.Y == zone.Y + zone.H - 1 ? 0 : 1))) c.Reserve(p.X, p.Y);
            }
            var big = rng.Int(2) == 0 ? 4 : 6;
            var military = Rect(zone.X + zone.W - 1 - big, zone.Y + 1, big, Math.Min(big, zone.H - 2));
            var quarantine = Rect(military.X - 5, zone.Y + 1, 4, 5);
            Block(military, TileKinds.High);
            Block(quarantine, TileKinds.High);
            marks.Add((V(military.X, military.Y), CampDetailKinds.TentMilitary, 3));
            marks.Add((V(quarantine.X, quarantine.Y), CampDetailKinds.TentMilitary, 3));
            parts.Add((Inset(zone, 1), CampUses.Military));
            // The caravans, to the west, where the clearing has room: their door on their east side.
            var caravans = rng.Range(3, 5);
            var built = 0;
            foreach (var at in Enumerable.Range(0, 4).SelectMany((row) => Enumerable.Range(0, 3).Select((col) => V(cx - 16 + 5 * col, cy - 12 + 5 * row))))
            {
                if (built >= caravans) break;
                var deep = rng.Int(2) == 0 ? 8 : 9;
                var r = Rect(at.X, at.Y, 4, deep);
                if (!Room(Inset(r, -1))) continue;
                built++;
                var b = MakeBuilding(c, r, rng, Plans.Open, 1, style, Furnishes.Home);
                b.Storeys = 1;
                b.Flat = true;
                b.Skylit = true;
                parts.Add((Inset(r, 1), CampUses.Trailer));
                marks.Add((V(r.X, r.Y), CampDetailKinds.Trailer, 1));
            }
            // The fire pit in the middle, chairs round it.
            var fire = V(cx, cy);
            Opened(Rect(cx - 2, cy - 2, 5, 5), Open);
            Block(Rect(fire.X, fire.Y, 1, 1), TileKinds.Low);
            marks.Add((fire, CampDetailKinds.FirePit, 0));
            foreach (var s in new[] { 0, 1, 2, 3 }) marks.Add((V(fire.X + 2 * Grid.Dirs4[s].X, fire.Y + 2 * Grid.Dirs4[s].Y), CampDetailKinds.CampChair, Opposite(s)));
            parts.Add((Rect(cx - 2, cy - 2, 5, 5), CampUses.FirePit));
            // The portable toilets, a row of three.
            var toilets = Rect(cx - 4, cy - 12, 5, 1);
            if (Room(Inset(toilets, -1)))
            {
                foreach (var k in new[] { 0, 2, 4 })
                {
                    var p = V(toilets.X + k, toilets.Y);
                    Block(Rect(p.X, p.Y, 1, 1), TileKinds.High);
                    marks.Add((p, CampDetailKinds.ToiletPortable, 2));
                }
                parts.Add((toilets, CampUses.Toilets));
            }
            // The pitches, to the south-east and the north-west: a tent each, what campers leave round it.
            foreach (var at in new[] { V(cx + 3, cy + 3), V(cx + 9, cy + 3), V(cx + 3, cy + 9), V(cx + 9, cy + 9), V(cx - 9, cy - 16 + 3), V(cx + 3, cy - 16 + 1) })
            {
                var pitch = Rect(at.X, at.Y, 5, 5);
                if (!Room(pitch)) continue;
                var dome = rng.Int(2) == 0;
                var tent = Rect(pitch.X + 1, pitch.Y + 1, 2, dome ? 2 : 3);
                Block(tent, TileKinds.Low);
                marks.Add((V(tent.X, tent.Y), dome ? CampDetailKinds.TentDome : CampDetailKinds.TentLong, 2));
                var free = TilesIn(pitch).Where((p) => !InRect(tent, p)).ToList();
                foreach (var kindOf in new[] { CampDetailKinds.CampChair, CampDetailKinds.SleepingBag, CampDetailKinds.Cooler, CampDetailKinds.Clothesline })
                {
                    if (kindOf != CampDetailKinds.CampChair && rng.Next() < 0.4) continue;
                    var p = free[rng.Int(free.Count)];
                    free.Remove(p);
                    marks.Add((p, kindOf, rng.Int(4)));
                }
                if (rng.Next() < 0.3) marks.Add((free[rng.Int(free.Count)], CampDetailKinds.CorpseCamper, rng.Int(4)));
                parts.Add((pitch, CampUses.Pitch));
            }
            c.Clearing = Rect(cx - R - 2, cy - R, 2 * R + 5, 2 * R + 1);
            c.CampParts = parts;
            c.CampMarks = marks;
            // In by the south path: some 27 on foot from La Lata.
            c.Squad = V(cx, cy + R);
            c.SquadSide = 2;
            foreach (var p in TilesIn(Rect(c.Squad.X - 2, c.Squad.Y - 2, 5, 5))) c.Reserve(p.X, p.Y);
            return kind == MissionKinds.Sabotage ? Inset(zone, 1) : c.Clearing;
        }

        /// <summary>
        /// The camping's La Lata (H9 §5): at the road's turning place, beside the road (its bus on it, driving off up it), the
        /// deepest spot with the bus's room; else anywhere in the turning place.
        /// </summary>
        private static List<Vec2> CampingEvac(Canvas c)
        {
            var cx = c.Width / 2;
            var cy = c.Height / 2 + 1;
            var turning = Rect(cx - 4, cy - ClearingRadius + 1, 9, 6);
            var centres = TilesIn(turning).OrderBy((p) => Math.Abs(Math.Abs(p.X - cx) - 3)).ThenByDescending((p) => p.Y).ThenBy((p) => p.X);
            return FirstEvac(c, centres);
        }

        /// <summary>The camping's site (H9 §5) in the map's own frame: its bounds (the clearing), its parts by use, its marks.</summary>
        private static InteriorSite CampingSite(Canvas c, Func<Vec2, Vec2> T)
        {
            Rect Turned(Rect r)
            {
                var a = T(V(r.X, r.Y));
                var z = T(V(r.X + r.W - 1, r.Y + r.H - 1));
                return Rect(Math.Min(a.X, z.X), Math.Min(a.Y, z.Y), Math.Abs(a.X - z.X) + 1, Math.Abs(a.Y - z.Y) + 1);
            }
            var clip = Rect(Math.Max(0, c.Clearing!.X), Math.Max(0, c.Clearing.Y), Math.Min(c.Width, c.Clearing.X + c.Clearing.W) - Math.Max(0, c.Clearing.X), Math.Min(c.Height, c.Clearing.Y + c.Clearing.H) - Math.Max(0, c.Clearing.Y));
            var bounds = Turned(clip);
            var site = new InteriorSite { Kind = InteriorKinds.Camping, X = bounds.X, Y = bounds.Y, W = bounds.W, H = bounds.H };
            foreach (var (r, use) in c.CampParts!)
            {
                var t = Turned(r);
                site.Rooms.Add(new InteriorRoom { X = t.X, Y = t.Y, W = t.W, H = t.H, Z = 0, Use = use });
            }
            foreach (var (at, kind, facing) in c.CampMarks!)
            {
                var a = T(at);
                var b = T(Add(at, Grid.Dirs4[facing]));
                var f = Math.Max(0, FindIndex(Grid.Dirs4, (d) => d.X == b.X - a.X && d.Y == b.Y - a.Y));
                site.Dressing.Add(new InteriorDetail { Kind = kind, X = a.X, Y = a.Y, Z = 0, Facing = f, Variant = kind == CampDetailKinds.TentMilitary && site.Dressing.Any((d) => d.Kind == CampDetailKinds.TentMilitary) ? 1 : (int?)null });
            }
            return site;
        }

        // ------------------------------------------------------ rooms and marks, at the end

        /// <summary>
        /// A map that is one building's site (H9), in the map's own frame (`T`), on what stands at the end: its bounds, its
        /// rooms (the builder's, turned; an office block's worked out, OfficeRooms) and its marks (InteriorDressing).
        /// </summary>
        private static InteriorSite InteriorOf(Building b, Func<Vec2, Vec2> T, int FW, List<string> tiles, List<int> decor, List<Level> levels, List<int> storeys, int seed, List<Vec2> keep)
        {
            Rect Turned(Rect r)
            {
                var a = T(V(r.X, r.Y));
                var z = T(V(r.X + r.W - 1, r.Y + r.H - 1));
                return Rect(Math.Min(a.X, z.X), Math.Min(a.Y, z.Y), Math.Abs(a.X - z.X) + 1, Math.Abs(a.Y - z.Y) + 1);
            }
            var outer = Turned(b.Outer);
            var site = new InteriorSite { Kind = b.InteriorKind!, X = outer.X, Y = outer.Y, W = outer.W, H = outer.H };
            foreach (var r in b.InteriorRooms ?? new List<InteriorRoom>()) site.Rooms.Add(Room(Turned(Rect(r.X, r.Y, r.W, r.H)), r.Use, r.Z));
            // A police station's cells: floor and bars now, nothing having joined them to the rest.
            foreach (var r in b.Cells ?? new List<Rect>())
            {
                foreach (var p in TilesIn(r).Select(T))
                {
                    var i = p.Y * FW + p.X;
                    if (tiles[i] == TileKinds.High && decor[i] == 0) tiles[i] = TileKinds.Floor;
                }
            }
            foreach (var p in (b.Bars ?? new List<Vec2>()).Select(T)) tiles[p.Y * FW + p.X] = TileKinds.Bars;
            if (site.Kind == InteriorKinds.Offices) site.Rooms.AddRange(OfficeRooms(site, FW, tiles, decor, levels, storeys, b.InteriorEntrance != null ? T(b.InteriorEntrance) : null));
            // A police station's storey above: offices, and the toilets, a kitchen, the lockers in its small rooms; its stairs.
            if (site.Kind == InteriorKinds.Police)
            {
                site.Rooms.AddRange(OfficeRooms(site, FW, tiles, decor, levels, storeys, null, 1).Select((r) =>
                {
                    r.Use = r.Use == OfficeUses.Stairs ? PoliceUses.Stairs : r.Use == OfficeUses.Hygiene ? PoliceUses.Hygiene : r.Use == OfficeUses.Kitchen ? PoliceUses.Kitchen : r.Use == OfficeUses.Storage ? PoliceUses.Lockers : PoliceUses.Office;
                    return r;
                }));
            }
            site.Dressing = InteriorDressing(site, FW, tiles, decor, levels, storeys, seed, keep);
            // The counter: its mark on its window's first tile, facing the lobby (to the west on the plan).
            if (b.Counter != null)
            {
                var at = T(b.Counter);
                var toward = T(V(b.Counter.X - 1, b.Counter.Y));
                var facing = FindIndex(Grid.Dirs4, (d) => d.X == toward.X - at.X && d.Y == toward.Y - at.Y);
                site.Dressing.Add(new InteriorDetail { Kind = PoliceDetailKinds.Counter, X = at.X, Y = at.Y, Z = 0, Facing = Math.Max(0, facing) });
            }
            return site;
        }

        /// <summary>What stands on storey `z` at tile `i` of the finished map, and its decor; null and 0 where nothing is laid.</summary>
        private static string? KindOn(List<string> tiles, List<Level> levels, int z, int i) => z == 0 ? tiles[i] : z - 1 < levels.Count && i < levels[z - 1].Tiles.Count ? levels[z - 1].Tiles[i] : null;

        private static int DecorOn(List<int> decor, List<Level> levels, int z, int i) => z == 0 ? decor[i] : z - 1 < levels.Count && levels[z - 1].Decor != null && i < levels[z - 1].Decor!.Count ? levels[z - 1].Decor![i] : 0;

        private static bool Shut(string? kind) => kind == null || kind == TileKinds.Wall || kind == TileKinds.Window || kind == TileKinds.WindowBroken || kind == TileKinds.Bars;

        /// <summary>
        /// An office block's rooms by use (H9 §1, OfficeUses), on every storey: its rooms are its inside joined side by side,
        /// not through doors (as Buildings finds them on a map that is one building). On the ground floor, the one behind the
        /// main door is the reception (a big one, open plan, its reception the part within three tiles of the door); a
        /// corridor elsewhere is no room of these. Of the rest, a big one (30 tiles or more) is open plan; the biggest of the
        /// middling with two desks side by side, the meeting room, the others offices; the small ones (9 or fewer), in turn the toilets, the kitchen and
        /// the store. A storey with three offices or more and no small room makes its last two the toilets and the kitchen.
        /// The stairs, a rect of their own on the flight, in the room they stand in.
        /// </summary>
        /// <remarks>`from`: the storeys from it up (a police station's ground floor is its builder's); the stairs on all.</remarks>
        private static List<InteriorRoom> OfficeRooms(InteriorSite site, int FW, List<string> tiles, List<int> decor, List<Level> levels, List<int> storeys, Vec2? entrance, int from = 0)
        {
            var out_ = new List<InteriorRoom>();
            var bounds = Rect(site.X, site.Y, site.W, site.H);
            var smalls = new[] { OfficeUses.Hygiene, OfficeUses.Kitchen, OfficeUses.Storage };
            for (var z = 0; z <= levels.Count; z++)
            {
                bool Inside(Vec2 p) => InRect(bounds, p) && storeys[p.Y * FW + p.X] > z;
                bool Floor(Vec2 p)
                {
                    if (!Inside(p)) return false;
                    var k = KindOn(tiles, levels, z, p.Y * FW + p.X);
                    return !Shut(k) && k != TileKinds.Door && k != TileKinds.DoorOpen;
                }
                var seen = new HashSet<int>();
                var parts = new List<List<Vec2>>();
                foreach (var start in TilesIn(bounds))
                {
                    if (seen.Contains(start.Y * FW + start.X) || !Floor(start)) continue;
                    var part = new List<Vec2> { start };
                    seen.Add(start.Y * FW + start.X);
                    for (var k = 0; k < part.Count; k++)
                    {
                        foreach (var d in Grid.Dirs4)
                        {
                            var q = Add(part[k], d);
                            if (seen.Contains(q.Y * FW + q.X) || !Floor(q)) continue;
                            seen.Add(q.Y * FW + q.X);
                            part.Add(q);
                        }
                    }
                    parts.Add(part);
                }
                Rect Bounds(List<Vec2> part)
                {
                    int x0 = part.Min((p) => p.X), y0 = part.Min((p) => p.Y);
                    return Rect(x0, y0, part.Max((p) => p.X) - x0 + 1, part.Max((p) => p.Y) - y0 + 1);
                }
                var rooms = parts.Select((p) => (Part: p, R: Bounds(p))).ToList();
                bool Thin((List<Vec2> Part, Rect R) r) => Math.Min(r.R.W, r.R.H) <= 2 && r.Part.Count >= 6;
                bool AtDoor((List<Vec2> Part, Rect R) r) => z == 0 && entrance != null && r.Part.Any((p) => Math.Abs(p.X - entrance.X) + Math.Abs(p.Y - entrance.Y) == 1);
                // The meeting room: the biggest of the middling with two desks side by side, its table (Arte, 17/11: two tiles).
                bool Desk(Vec2 p) => Decor.DecorOf(DecorOn(decor, levels, z, p.Y * FW + p.X))?.Kind == DecorKinds.Desk;
                bool Paired(List<Vec2> part) => part.Any((p) => Desk(p) && Decor.DecorOf(DecorOn(decor, levels, z, p.Y * FW + p.X))!.Facing % 2 is var f && Desk(V(p.X + (f == 0 ? 1 : 0), p.Y + (f == 0 ? 0 : 1))));
                var middling = rooms.Where((r) => !Thin(r) && !AtDoor(r) && r.Part.Count > 9 && r.Part.Count < 30 && Paired(r.Part)).OrderByDescending((r) => r.Part.Count).ThenBy((r) => r.R.Y).ThenBy((r) => r.R.X).ToList();
                var small = 0;
                var storey = new List<InteriorRoom>();
                foreach (var r in rooms.OrderBy((x) => x.R.Y).ThenBy((x) => x.R.X))
                {
                    string? use;
                    if (AtDoor(r))
                    {
                        use = OfficeUses.Reception;
                        if (!Thin(r) && r.Part.Count >= 30)
                        {
                            // Open plan behind the door: its reception, the part by the door.
                            storey.Add(Room(r.R, OfficeUses.OpenPlan, z));
                            var near = r.Part.Where((p) => Math.Max(Math.Abs(p.X - entrance!.X), Math.Abs(p.Y - entrance.Y)) <= 3).ToList();
                            storey.Add(Room(Bounds(near), OfficeUses.Reception, z));
                            continue;
                        }
                    }
                    else if (Thin(r)) use = null;
                    else if (r.Part.Count >= 30) use = OfficeUses.OpenPlan;
                    else if (r.Part.Count <= 9) use = smalls[small++ % smalls.Length];
                    else use = middling.Count > 0 && middling[0].Part == r.Part ? OfficeUses.Meeting : OfficeUses.Office;
                    if (use != null) storey.Add(Room(r.R, use, z));
                }
                // Toilets and a kitchen where no room is small: the last two offices.
                var offices = storey.Where((r) => r.Use == OfficeUses.Office).ToList();
                if (small == 0 && offices.Count >= 3)
                {
                    offices[offices.Count - 1].Use = OfficeUses.Hygiene;
                    offices[offices.Count - 2].Use = OfficeUses.Kitchen;
                }
                if (z >= from) out_.AddRange(storey);
                // The stairs: each flight's tiles on this storey, a rect of their own.
                var flights = new HashSet<int>();
                IReadOnlyList<string?> layer = z == 0 ? tiles : levels[z - 1].Tiles;
                IReadOnlyList<int>? layerDecor = z == 0 ? decor : levels[z - 1].Decor;
                foreach (var p in TilesIn(bounds)) if (Inside(p) && Flights.LongFlightTile(layer, layerDecor, FW, p.Y * FW + p.X)) flights.Add(p.Y * FW + p.X);
                var done = new HashSet<int>();
                foreach (var i in flights.OrderBy((i) => i))
                {
                    if (done.Contains(i)) continue;
                    var run = new List<Vec2> { V(i % FW, i / FW) };
                    done.Add(i);
                    for (var k = 0; k < run.Count; k++)
                    {
                        foreach (var d in Grid.Dirs4)
                        {
                            var q = Add(run[k], d);
                            var j = q.Y * FW + q.X;
                            if (!InRect(bounds, q) || done.Contains(j) || !flights.Contains(j)) continue;
                            done.Add(j);
                            run.Add(q);
                        }
                    }
                    out_.Add(Room(Bounds(run), OfficeUses.Stairs, z));
                }
            }
            return out_;
        }

        /// <summary>
        /// A map that is one building's marks (H9; OfficeDetailKinds, ShedDetailKinds, PoliceDetailKinds), on its own seed: on the furniture that
        /// blocks or covers (its tile's decor: a desk, a cabinet, a shelf…), and as scenery on free floor by the rooms' uses
        /// (a whiteboard, a plant, papers, blood, a body…). Never on where the squad starts nor on what the mission puts down.
        /// </summary>
        private static List<InteriorDetail> InteriorDressing(InteriorSite site, int FW, List<string> tiles, List<int> decor, List<Level> levels, List<int> storeys, int seed, List<Vec2> keep)
        {
            var rng = new Rng(seed ^ (site.Kind == InteriorKinds.Shed ? 0x5bed : site.Kind == InteriorKinds.Police ? 0x9011ce : 0x0ff1ce));
            var police = site.Kind == InteriorKinds.Police;
            var area = tiles.Count;
            var out_ = new List<InteriorDetail>();
            var marked = new HashSet<int>();
            var kept = new HashSet<int>(keep.Select((p) => p.Y * FW + p.X));
            var bounds = Rect(site.X, site.Y, site.W, site.H);
            var shed = site.Kind == InteriorKinds.Shed;
            void Mark(string kind, int z, Vec2 p, int facing, bool floor = true, int? length = null)
            {
                out_.Add(new InteriorDetail { Kind = kind, X = p.X, Y = p.Y, Z = z, Facing = facing, Length = length });
                if (floor) marked.Add(z * area + p.Y * FW + p.X);
            }
            // A meeting table's second tile, taken with its first.
            var paired = new HashSet<int>();
            bool FreeFloor(int z, Vec2 p)
            {
                if (!InRect(bounds, p)) return false;
                var i = p.Y * FW + p.X;
                if (storeys[i] <= z || KindOn(tiles, levels, z, i) != TileKinds.Floor || DecorOn(decor, levels, z, i) != 0) return false;
                if (marked.Contains(z * area + i) || (z == 0 && kept.Contains(i))) return false;
                IReadOnlyList<string?> layer = z == 0 ? tiles : levels[z - 1].Tiles;
                return !Flights.LongFlightTile(layer, z == 0 ? decor : levels[z - 1].Decor, FW, i);
            }
            // Against a wall: free floor with a wall beside it, facing away from it.
            int? ByWall(int z, Vec2 p)
            {
                if (!FreeFloor(z, p)) return null;
                for (var s = 0; s < 4; s++)
                {
                    var q = Add(p, Grid.Dirs4[s]);
                    if (InRect(bounds, q) && KindOn(tiles, levels, z, q.Y * FW + q.X) == TileKinds.Wall) return Opposite(s);
                }
                return null;
            }
            InteriorRoom? UseAt(int z, Vec2 p) => site.Rooms.Where((r) => r.Z == z && r.Use != OfficeUses.Stairs && r.Use != ShedUses.LoadingBay && p.X >= r.X && p.Y >= r.Y && p.X < r.X + r.W && p.Y < r.Y + r.H).OrderBy((r) => r.W * r.H).FirstOrDefault();
            var floors = Math.Max(1, storeys.Where((k, i) => InRect(bounds, V(i % FW, i / FW))).DefaultIfEmpty(1).Max());
            // On the furniture.
            for (var z = 0; z < floors; z++)
            {
                foreach (var p in TilesIn(bounds))
                {
                    var i = p.Y * FW + p.X;
                    if (storeys[i] <= z) continue;
                    var look = Decor.DecorOf(DecorOn(decor, levels, z, i));
                    if (look == null) continue;
                    var use = UseAt(z, p)?.Use;
                    string? kind = null;
                    if (shed)
                    {
                        if (look.Kind == DecorKinds.Shelf) kind = ShedDetailKinds.Shelving;
                        else if (look.Kind == DecorKinds.Pallets) kind = ShedDetailKinds.Pallet;
                        else if (look.Kind == DecorKinds.Drums) kind = ShedDetailKinds.Drum;
                        else if (look.Kind == DecorKinds.Crates) kind = ShedDetailKinds.Crate;
                        else if (look.Kind == DecorKinds.Forklift) kind = ShedDetailKinds.Forklift;
                        else if (look.Kind == DecorKinds.Machine) kind = ShedDetailKinds.Workbench;
                        else if (look.Kind == DecorKinds.Cabinet && use == ShedUses.Lockers) kind = ShedDetailKinds.LockerRow;
                        // A container's mark on its first tile only: one model for all of it.
                        else if (look.Kind == DecorKinds.Container && Decor.DecorOf(DecorOn(decor, levels, z, i - 1))?.Kind != DecorKinds.Container && Decor.DecorOf(DecorOn(decor, levels, z, i - FW))?.Kind != DecorKinds.Container) kind = ShedDetailKinds.Container;
                        else if (look.Kind == DecorKinds.Desk) kind = null;
                    }
                    else if (police)
                    {
                        if (look.Kind == DecorKinds.Desk)
                        {
                            kind = PoliceDetailKinds.Desk;
                            if (rng.Next() < 0.7) Mark(PoliceDetailKinds.Computer, z, p, look.Facing, false);
                            for (var s = 0; s < 4; s++)
                            {
                                var q = Add(p, Grid.Dirs4[s]);
                                if (!FreeFloor(z, q)) continue;
                                Mark(PoliceDetailKinds.DeskChair, z, q, Opposite(s));
                                break;
                            }
                        }
                        else if (look.Kind == DecorKinds.Cabinet) kind = use == PoliceUses.Lockers ? PoliceDetailKinds.LockerRow : use == PoliceUses.Armory ? PoliceDetailKinds.WeaponLocker : PoliceDetailKinds.FilingCabinet;
                        else if (look.Kind == DecorKinds.Rack) kind = PoliceDetailKinds.WeaponRack;
                        else if (look.Kind == DecorKinds.Shelf && use == PoliceUses.Evidence) kind = PoliceDetailKinds.EvidenceShelf;
                        else if (look.Kind == DecorKinds.Crates && use == PoliceUses.Armory) kind = (p.X + p.Y) % 2 == 0 ? PoliceDetailKinds.GunCase : PoliceDetailKinds.AmmoCase;
                        else if (look.Kind == DecorKinds.Bench) kind = PoliceDetailKinds.Bench;
                        else if (look.Kind == DecorKinds.DrinksFridge) kind = PoliceDetailKinds.VendingMachine;
                        else if (look.Kind == DecorKinds.Table && use == PoliceUses.Interrogation) kind = PoliceDetailKinds.InterrogationTable;
                        else if (look.Kind == DecorKinds.Bed && use == PoliceUses.Cells) kind = PoliceDetailKinds.CellBed;
                        else if (look.Kind == DecorKinds.Wc && use == PoliceUses.Cells) kind = PoliceDetailKinds.CellToilet;
                    }
                    else
                    {
                        if (look.Kind == DecorKinds.Desk && use == OfficeUses.Meeting && paired.Contains(z * area + i)) continue;
                        // A meeting table, two desks side by side across their facing: one mark, its length 2 (Arte, 17/11).
                        var next = Add(p, look.Facing % 2 == 0 ? V(1, 0) : V(0, 1));
                        var pair = look.Kind == DecorKinds.Desk && use == OfficeUses.Meeting && InRect(bounds, next) && Decor.DecorOf(DecorOn(decor, levels, z, next.Y * FW + next.X))?.Kind == DecorKinds.Desk;
                        if (pair)
                        {
                            paired.Add(z * area + next.Y * FW + next.X);
                            Mark(OfficeDetailKinds.MeetingTable, z, p, look.Facing, false, 2);
                            continue;
                        }
                        if (look.Kind == DecorKinds.Desk)
                        {
                            kind = OfficeDetailKinds.Desk;
                            if (kind == OfficeDetailKinds.Desk)
                            {
                                if (rng.Next() < 0.7) Mark(OfficeDetailKinds.Computer, z, p, look.Facing, false);
                                // Its chair on the floor beside it, facing it.
                                for (var s = 0; s < 4; s++)
                                {
                                    var q = Add(p, Grid.Dirs4[s]);
                                    if (!FreeFloor(z, q)) continue;
                                    Mark(OfficeDetailKinds.DeskChair, z, q, Opposite(s));
                                    break;
                                }
                            }
                        }
                        else if (look.Kind == DecorKinds.Cabinet) kind = OfficeDetailKinds.FilingCabinet;
                    }
                    if (kind != null) Mark(kind, z, p, look.Facing, false);
                }
            }
            // Scenery by the rooms' uses.
            void Some(InteriorRoom r, string kind, int count, bool wall)
            {
                var spots = TilesIn(Rect(r.X, r.Y, r.W, r.H)).Where((p) => wall ? ByWall(r.Z, p) != null : FreeFloor(r.Z, p)).ToList();
                for (var k = 0; k < count && spots.Count > 0; k++)
                {
                    var p = spots[rng.Int(spots.Count)];
                    spots.Remove(p);
                    if (!FreeFloor(r.Z, p)) continue;
                    Mark(kind, r.Z, p, wall ? ByWall(r.Z, p)!.Value : rng.Int(4));
                }
            }
            foreach (var r in site.Rooms.OrderBy((x) => x.Z).ThenBy((x) => x.Y).ThenBy((x) => x.X))
            {
                if (shed) continue;
                if (police)
                {
                    switch (r.Use)
                    {
                        case PoliceUses.Reception:
                            Some(r, PoliceDetailKinds.Corkboard, 1, true);
                            Some(r, PoliceDetailKinds.WaterCooler, 1, true);
                            break;
                        case PoliceUses.Office:
                            Some(r, PoliceDetailKinds.Whiteboard, 1, true);
                            Some(r, PoliceDetailKinds.Corkboard, 1, true);
                            break;
                        case PoliceUses.Evidence:
                            Some(r, PoliceDetailKinds.EvidenceBag, rng.Range(2, 3), false);
                            break;
                        case PoliceUses.Interrogation:
                            Some(r, PoliceDetailKinds.Polygraph, 1, true);
                            break;
                    }
                    if (r.Use != PoliceUses.Stairs && r.Use != PoliceUses.Cells && rng.Next() < 0.25) Some(r, PoliceDetailKinds.BloodPool, 1, false);
                    continue;
                }
                switch (r.Use)
                {
                    case OfficeUses.Reception:
                        Some(r, OfficeDetailKinds.Plant, 1, true);
                        Some(r, OfficeDetailKinds.WaterCooler, 1, true);
                        Some(r, OfficeDetailKinds.ScatteredPapers, 1, false);
                        break;
                    case OfficeUses.OpenPlan:
                        Some(r, OfficeDetailKinds.Whiteboard, 1, true);
                        Some(r, OfficeDetailKinds.Copier, 1, true);
                        Some(r, OfficeDetailKinds.Plant, 1, true);
                        Some(r, OfficeDetailKinds.ScatteredPapers, rng.Range(2, 3), false);
                        break;
                    case OfficeUses.Office:
                        Some(r, OfficeDetailKinds.ScatteredPapers, rng.Range(1, 2), false);
                        if (rng.Next() < 0.5) Some(r, OfficeDetailKinds.Plant, 1, true);
                        break;
                    case OfficeUses.Meeting:
                        Some(r, OfficeDetailKinds.Whiteboard, 1, true);
                        Some(r, OfficeDetailKinds.ScatteredPapers, 1, false);
                        break;
                    case OfficeUses.Kitchen:
                        Some(r, OfficeDetailKinds.WaterCooler, 1, true);
                        break;
                    case OfficeUses.Storage:
                        Some(r, OfficeDetailKinds.ScatteredPapers, 1, false);
                        break;
                }
                if (r.Use != OfficeUses.Stairs && rng.Next() < 0.25) Some(r, OfficeDetailKinds.BloodPool, 1, false);
            }
            // A policeman fallen in the lobby or the duty office, now and then.
            if (police)
            {
                var duty = site.Rooms.Where((r) => r.Z == 0 && (r.Use == PoliceUses.Reception || r.Use == PoliceUses.Office)).ToList();
                if (duty.Count > 0 && rng.Next() < 0.7) Some(duty[rng.Int(duty.Count)], PoliceDetailKinds.CorpseOfficer, 1, false);
            }
            // A body on each storey, now and then, where people worked.
            if (!shed && !police)
            {
                for (var z = 0; z < floors; z++)
                {
                    var worked = site.Rooms.Where((r) => r.Z == z && (r.Use == OfficeUses.OpenPlan || r.Use == OfficeUses.Office)).ToList();
                    if (worked.Count > 0 && rng.Next() < 0.6) Some(worked[rng.Int(worked.Count)], OfficeDetailKinds.CorpseWorker, 1, false);
                }
            }
            return out_;
        }
    }
}
