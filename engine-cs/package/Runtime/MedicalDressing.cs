#nullable enable
// medicalDressing.ts: what lies about in a medical centre.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class MedicalDressing
    {
        /// <summary>A place on a floor and a way (a wall's side): `{ ...p, facing }`.</summary>
        private sealed class Facing
        {
            public Pos P = new Pos();
            public int F;
        }

        private static readonly IReadOnlyList<int> HospitalBodies = new[] { 1, 2, 3, 5, 5, 10, 10 };
        private static readonly IReadOnlyList<int> ClinicBodies = new[] { 1, 1, 2, 3, 5, 10 };
        private static readonly IReadOnlyList<string> Corpses = new[] { MedicalDetailKinds.CorpseCivilian, MedicalDetailKinds.CorpseStaff, MedicalDetailKinds.CoveredCorpse };

        /// <summary>
        /// A separate seed leaves rooms, objectives and combat rolls unchanged. What
        /// stands up in the mess takes its tile where `strew` lets it
        /// (medicalAbandonment); without it, nothing changes the map.
        /// </summary>
        public static List<MedicalDetail> DressMedical(MedicalSite site, int width, List<string> tiles, List<int> decor, List<Level> levels, int seed, IReadOnlyList<Vec2>? reserved = null, Func<Pos, bool>? strew = null)
        {
            reserved ??= new List<Vec2>();
            var rng = new Rng(unchecked((uint)(seed ^ 0x4d454449 ^ (site.X * 65537 + site.Y))));
            var details = new List<MedicalDetail>();
            var used = new HashSet<(int, int, int)>();
            // `z ? levels[z - 1]! : { tiles, decor }`. The ground's tiles are never null.
            List<string?> groundTiles = tiles!;
            List<string?> TilesOf(int z) => z != 0 ? levels[z - 1].Tiles : groundTiles;
            List<int>? DecorOf(int z) => z != 0 ? levels[z - 1].Decor : decor;
            var reservedFloor = new HashSet<(int, int, int)>(reserved.Select((p) => (p.X, p.Y, 0)));
            void Add(string kind, int x, int y, int z, int? facing = null, double height = 0)
            {
                var f = facing ?? rng.Int(4);
                details.Add(new MedicalDetail { Kind = kind, X = x, Y = y, Z = z, Facing = f, Height = height });
                used.Add((x, y, z));
            }
            List<T> Shuffled<T>(List<T> xs)
            {
                for (var i = xs.Count - 1; i > 0; i--) { var j = rng.Int(i + 1); var t = xs[i]; xs[i] = xs[j]; xs[j] = t; }
                return xs;
            }
            List<Pos> Cells(MedicalRoom r)
            {
                var ps = new List<Pos>();
                for (var y = r.Y; y < r.Y + r.H; y++) for (var x = r.X; x < r.X + r.W; x++) ps.Add(new Pos { X = x, Y = y, Z = r.Z });
                return ps;
            }
            DecorLook? LookAt(Pos p) => Decor.DecorOf(Js.At(DecorOf(p.Z), p.Y * width + p.X, 0));
            var rooms = (site.Rooms ?? new List<MedicalRoom>()).Where((r) => r.Use != MedicalUses.Stairs).ToList();
            var beds = Shuffled(rooms.SelectMany(Cells).Where((p) => LookAt(p)?.Kind == DecorKinds.HospitalBed).ToList());
            var bodyTarget = rng.Pick(site.Kind == MedicalSiteKinds.Hospital ? HospitalBodies : ClinicBodies);
            // Some of the dead stay in beds. Others have fallen in the rooms; no body is a unit or obstacle.
            var bedBodies = Math.Min(Math.Min((int)Math.Floor(bodyTarget / 2.0), beds.Count), site.Kind == MedicalSiteKinds.Hospital ? 3 : 1);
            for (var i = 0; i < beds.Count; i++)
            {
                var p = beds[i];
                var facing = LookAt(p)!.Facing;
                if (i < bedBodies) Add(MedicalDetailKinds.CoveredBed, p.X, p.Y, p.Z, facing);
                else if (rng.Next() < 0.24) Add(MedicalDetailKinds.BareBed, p.X, p.Y, p.Z, facing);
            }
            var floor = Shuffled(rooms.SelectMany(Cells).Where((p) => Js.At(TilesOf(p.Z), p.Y * width + p.X, null) == TileKinds.Floor
                && !reservedFloor.Contains((p.X, p.Y, p.Z))
                // Keep thresholds and stair approaches visually clear as well as traversable.
                && !Grid.Dirs4.Any((d) =>
                {
                    var k = Js.At(TilesOf(p.Z), (p.Y + d.Y) * width + p.X + d.X, null);
                    return k == TileKinds.Door || k == TileKinds.Ladder;
                })).ToList());
            var bodies = bedBodies;
            foreach (var p in floor)
            {
                if (bodies >= bodyTarget) break;
                if (used.Contains((p.X, p.Y, p.Z))) continue;
                Add(rng.Pick(Corpses), p.X, p.Y, p.Z);
                bodies++;
            }
            // Even the smallest centre gets a body if its only floor is beside a doorway.
            if (bodies == 0)
            {
                var p = rooms.SelectMany(Cells).FirstOrDefault((p) => Js.At(TilesOf(p.Z), p.Y * width + p.X, null) == TileKinds.Floor && !reservedFloor.Contains((p.X, p.Y, p.Z)));
                if (p != null) Add(MedicalDetailKinds.CoveredCorpse, p.X, p.Y, p.Z);
            }
            foreach (var r in rooms)
            {
                var ps = Cells(r);
                var ds = DecorOf(r.Z);
                DecorLook? Look(Pos p) => Decor.DecorOf(Js.At(ds, p.Y * width + p.X, 0));
                var bed = ps.Find((p) => Look(p)?.Kind == DecorKinds.HospitalBed);
                var exam = ps.Find((p) =>
                {
                    var kind = Look(p)?.Kind;
                    return kind == DecorKinds.ExamTable || kind == DecorKinds.Gurney;
                });
                // A folded screen fits inside the existing bed's tile: it never takes another cover cell.
                var screen = bed ?? exam;
                if (screen != null && (bed != null || rng.Next() < 0.8)) Add(MedicalDetailKinds.PrivacyScreen, screen.X, screen.Y, r.Z, Look(screen)!.Facing);
                foreach (var p in ps)
                {
                    var look = Look(p);
                    if (look == null) continue;
                    if (look.Kind == DecorKinds.BedsideTable)
                    {
                        Add(MedicalDetailKinds.MedicalSupplies, p.X, p.Y, r.Z, look.Facing, 0.53);
                        if (rng.Next() < 0.7) Add(MedicalDetailKinds.Luggage, p.X, p.Y, r.Z, look.Facing);
                    }
                    else if (look.Kind == DecorKinds.MedicalCart) Add(MedicalDetailKinds.MedicalSupplies, p.X, p.Y, r.Z, look.Facing, 0.60);
                    else if (look.Kind == DecorKinds.WaitingTable || look.Kind == DecorKinds.ConsultDesk) Add(MedicalDetailKinds.Books, p.X, p.Y, r.Z, look.Facing, look.Kind == DecorKinds.WaitingTable ? 0.40 : 0.79);
                    else if (look.Kind == DecorKinds.MedicalCabinet && rng.Next() < 0.65) Add(MedicalDetailKinds.Crutches, p.X, p.Y, r.Z, look.Facing);
                }
                var walls = ps.SelectMany((p) => Grid.Dirs4.SelectMany((d, facing) => Js.At(TilesOf(r.Z), (p.Y - d.Y) * width + p.X - d.X, null) == TileKinds.Wall ? new[] { new Facing { P = p, F = facing } } : new Facing[0])).ToList();
                var wall = Shuffled(walls).FirstOrDefault();
                if (wall != null) Add(r.Use == MedicalUses.Ward ? MedicalDetailKinds.WallCross : r.Use == MedicalUses.Waiting || r.Use == MedicalUses.Reception || r.Use == MedicalUses.Nurses ? MedicalDetailKinds.SafeSign : MedicalDetailKinds.CautionSign, wall.P.X, wall.P.Y, r.Z, wall.F);
            }
            var out_ = new List<MedicalDetail>(details);
            out_.AddRange(MedicalAbandonment.Abandon(site, width, tiles, decor, levels, seed, reserved, details, strew));
            return out_;
        }
    }
}
