#nullable enable
// medicalAbandonment.ts: the mess a medical centre was left in. The function medicalAbandonment is Abandon here (a
// member can't have its class's name).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class MedicalAbandonment
    {
        /// <summary>A place on a floor and a way (a trail, a wall's side): `{ x, y, z, facing }`.</summary>
        private sealed class Facing
        {
            public Pos P = new Pos();
            public int F;
        }

        private static readonly HashSet<string> BodyKinds = new HashSet<string> { MedicalDetailKinds.CorpseCivilian, MedicalDetailKinds.CorpseStaff, MedicalDetailKinds.CoveredCorpse, MedicalDetailKinds.CoveredBed };
        private static readonly HashSet<string> SignKinds = new HashSet<string> { MedicalDetailKinds.WallCross, MedicalDetailKinds.SafeSign, MedicalDetailKinds.CautionSign };
        private static readonly IReadOnlyList<string> Litter = new[] { MedicalDetailKinds.ScatteredPapers, MedicalDetailKinds.MedicalWaste, MedicalDetailKinds.SpilledSupplies };

        /// <summary>
        /// Separate scenery roll: preserves the approved bodies, furniture and combat
        /// RNG. A chair over or a fallen drip takes its tile, as cover (Arturo, 19/10:
        /// «que ocupen»), where `strew` lets it (furnishing.ts Furnisher.strew: it
        /// closes no way); where it doesn't, papers lie there instead. Without
        /// `strew`, nothing changes the map.
        /// </summary>
        public static List<MedicalDetail> Abandon(MedicalSite site, int width, List<string> tiles, List<int> decor, List<Level> levels, int seed, IReadOnlyList<Vec2> reserved, List<MedicalDetail> existing, Func<Pos, bool>? strew = null)
        {
            var rng = new Rng(unchecked((uint)(seed ^ 0x424c4f4f ^ (site.X * 8191 + site.Y * 131))));
            var details = new List<MedicalDetail>();
            // `z ? levels[z - 1]! : { tiles, decor }`. The ground's tiles are never null.
            List<string?> groundTiles = tiles!;
            List<string?> TilesOf(int z) => z != 0 ? levels[z - 1].Tiles : groundTiles;
            string? TileAt(int z, int i) => Js.At(TilesOf(z), i, null);
            var blocked = new HashSet<(int, int, int)>(reserved.Select((p) => (p.X, p.Y, 0)));
            var bodies = existing.Where((d) => BodyKinds.Contains(d.Kind)).ToList();
            var occupied = new HashSet<(int, int, int)>(bodies.Select((p) => (p.X, p.Y, p.Z)));
            var intensity = rng.Range(2, site.Kind == MedicalSiteKinds.Clinic ? 4 : 6);
            void Add(string kind, Pos p, int? facing = null)
            {
                var f = facing ?? rng.Int(4);
                details.Add(new MedicalDetail { Kind = kind, X = p.X, Y = p.Y, Z = p.Z, Facing = f, Height = 0, Variant = rng.Int(8) });
            }
            List<T> Shuffled<T>(List<T> xs)
            {
                for (var i = xs.Count - 1; i > 0; i--) { var j = rng.Int(i + 1); var t = xs[i]; xs[i] = xs[j]; xs[j] = t; }
                return xs;
            }
            foreach (var body in bodies)
            {
                Add(MedicalDetailKinds.BloodPool, new Pos { X = body.X, Y = body.Y, Z = body.Z }, body.Facing);
                var room = site.Rooms?.Find((r) => r.Z == body.Z && body.X >= r.X && body.X < r.X + r.W && body.Y >= r.Y && body.Y < r.Y + r.H);
                if (room == null) continue;
                var neighbours = Grid.Dirs4.Select((d, facing) => new Facing { P = new Pos { X = body.X + d.X, Y = body.Y + d.Y, Z = body.Z }, F = facing })
                    .Where((q) => q.P.X >= room.X && q.P.X < room.X + room.W && q.P.Y >= room.Y && q.P.Y < room.Y + room.H && TileAt(q.P.Z, q.P.Y * width + q.P.X) == TileKinds.Floor && !blocked.Contains((q.P.X, q.P.Y, q.P.Z))).ToList();
                var trail = Shuffled(neighbours).FirstOrDefault();
                if (trail != null) Add(MedicalDetailKinds.BloodDrops, trail.P, trail.F);
            }
            foreach (var room in site.Rooms ?? new List<MedicalRoom>())
            {
                if (room.Use == MedicalUses.Stairs) continue;
                var floor = new List<Pos>();
                for (var y = room.Y; y < room.Y + room.H; y++) for (var x = room.X; x < room.X + room.W; x++)
                {
                    if (TileAt(room.Z, y * width + x) == TileKinds.Floor && !blocked.Contains((x, y, room.Z))) floor.Add(new Pos { X = x, Y = y, Z = room.Z });
                }
                var walls = floor.SelectMany((p) => Grid.Dirs4.SelectMany((d, facing) => TileAt(p.Z, (p.Y - d.Y) * width + p.X - d.X) == TileKinds.Wall ? new[] { new Facing { P = p, F = facing } } : new Facing[0])).ToList();
                // Grime gathers at room edges. Flat litter can cross a route, bulky objects cannot.
                var edges = Shuffled(floor.Where((p) => p.X == room.X || p.Y == room.Y || p.X == room.X + room.W - 1 || p.Y == room.Y + room.H - 1).ToList());
                foreach (var p in edges.Take(Math.Min(3, intensity)).ToList()) Add(MedicalDetailKinds.FloorGrime, p);
                var clear = Shuffled(floor.Where((p) => !occupied.Contains((p.X, p.Y, p.Z))
                    && !Grid.Dirs4.Any((d) =>
                    {
                        var k = TileAt(p.Z, (p.Y + d.Y) * width + p.X + d.X);
                        return k == TileKinds.Door || k == TileKinds.Ladder;
                    })).ToList());
                var some = clear.Take(intensity).ToList();
                for (var i = 0; i < some.Count; i++)
                {
                    var p = some[i];
                    if (i == 0) Add(MedicalDetailKinds.ScatteredPapers, p);
                    else if (i == 1) Add(room.Use == MedicalUses.Ward ? MedicalDetailKinds.RumpledLinen : MedicalDetailKinds.SpilledSupplies, p);
                    else if (i == 2 && edges.Any((e) => e.X == p.X && e.Y == p.Y)) Add(strew != null && !strew(p) ? MedicalDetailKinds.ScatteredPapers : room.Use == MedicalUses.Waiting || room.Use == MedicalUses.Reception ? MedicalDetailKinds.OverturnedChair : MedicalDetailKinds.FallenIV, p);
                    else Add(rng.Pick(Litter), p);
                }
                var nearby = bodies.Where((d) => d.Z == room.Z && d.X >= room.X && d.X < room.X + room.W && d.Y >= room.Y && d.Y < room.Y + room.H).ToList();
                int Distance(Pos p) => nearby.Min((d) => Math.Abs(d.X - p.X) + Math.Abs(d.Y - p.Y));
                var sorted = Shuffled(walls);
                Js.Sort(sorted, (a, b) => nearby.Count > 0 ? Distance(a.P) - Distance(b.P) : 0);
                var wall = sorted.Find((p) => !existing.Any((d) => SignKinds.Contains(d.Kind) && d.X == p.P.X && d.Y == p.P.Y && d.Z == p.P.Z && d.Facing == p.F));
                if (wall != null && (nearby.Count > 0 || rng.Next() < 0.25)) Add(MedicalDetailKinds.WallBlood, new Pos { X = wall.P.X, Y = wall.P.Y, Z = wall.P.Z }, wall.F);
            }
            return details;
        }
    }
}
