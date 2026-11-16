#nullable enable
// The expeditions' maps in the generator (new in C#; Gameplay, 16/11): La Lata near the squad on a normal node
// (GenOptions.EvacNear), and the hospital only inside (H7, Sites.Hospital). Nothing here runs for any other map.
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>
        /// La Lata's walk from where the squad comes in on an expedition's normal node (GenOptions.EvacNear), in steps
        /// (Gameplay, 16/11: «15–25 tiles of path»; at a service area it was some 50, and with knives only the horde caught
        /// them).
        /// </summary>
        public static readonly IReadOnlyList<int> NearWalk = new[] { 15, 25 };
        /// <summary>A way out's bus further than this from the squad is not near (AvenueEvac): the evacuation is carved instead.</summary>
        private const int NearMost = 30;

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
            return FirstEvac(c, spots.Select((t) => t.P));
        }

        /// <summary>
        /// The first of `centres` whose 3 × 3 round it can be an evacuation with its bus beside it (ParkBus), cleared and
        /// kept: the evacuation; empty if none.
        /// </summary>
        private static List<Vec2> FirstEvac(Canvas c, IEnumerable<Vec2> centres)
        {
            foreach (var p in centres)
            {
                var zone = TilesIn(Rect(p.X - 1, p.Y - 1, 3, 3));
                if (ParkBus(c, zone, new Rng(0), true) == null) continue;
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
    }
}
