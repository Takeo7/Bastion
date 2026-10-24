#nullable enable
// homeDressing.ts: what lies about in a house.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class HomeDressing
    {
        /// <summary>A place on a floor and a way (a trail, a wall's side): `{ x, y, z, facing }`.</summary>
        private sealed class Facing
        {
            public Pos P = new Pos();
            public int F;
        }

        /// <summary>
        /// What lies about in a house (playtest 2026-10-18, #15: «cadáveres, sangre,
        /// cosas desordenadas y muebles caídos»), the more the worse off it is (its
        /// wear): the dead where they fell, their blood and its trail, papers and
        /// books strewn about, clothes off the beds, a chair over, bags left packed
        /// by the door, grime along the walls and blood up them. The medical centres'
        /// details (types.ts MedicalDetail), drawn the same way. On a seed of its own:
        /// rooms, objectives and combat rolls are unchanged; never on a doorway's or a
        /// ladder's approach, nor where somebody stands. What stands up (a chair over,
        /// the bags) takes its tile, as cover (Arturo, 19/10: «que ocupen»), where
        /// `strew` lets it (furnishing.ts Furnisher.strew: it closes no way); where it
        /// doesn't, papers lie there instead. Without `strew`, nothing changes the map.
        /// </summary>
        public static List<MedicalDetail> DressHome(HomeSite site, int width, List<string> tiles, List<int> decor, List<Level> levels, int seed, IReadOnlyList<Vec2>? reserved = null, Func<Pos, bool>? strew = null)
        {
            reserved ??= new List<Vec2>();
            var rng = new Rng(unchecked((uint)(seed ^ 0x484f4d45 ^ (site.X * 40503 + site.Y * 977))));
            var details = new List<MedicalDetail>();
            var used = new HashSet<(int, int, int)>();
            // `(z ? levels[z - 1] : { tiles, decor })`: undefined past the map's storeys. The ground's tiles are never null.
            List<string?> groundTiles = tiles!;
            List<string?>? TilesOf(int z) => z != 0 ? (z - 1 >= 0 && z - 1 < levels.Count ? levels[z - 1].Tiles : null) : groundTiles;
            List<int>? DecorOf(int z) => z != 0 ? (z - 1 >= 0 && z - 1 < levels.Count ? levels[z - 1].Decor : null) : decor;
            bool HasLayer(int z) => z == 0 || (z - 1 >= 0 && z - 1 < levels.Count);
            var blocked = new HashSet<(int, int, int)>(reserved.Select((p) => (p.X, p.Y, 0)));
            void Add(string kind, Pos p, int? facing = null, double height = 0)
            {
                var f = facing ?? rng.Int(4);
                details.Add(new MedicalDetail { Kind = kind, X = p.X, Y = p.Y, Z = p.Z, Facing = f, Height = height, Variant = rng.Int(8) });
                used.Add((p.X, p.Y, p.Z));
            }
            List<T> Shuffled<T>(List<T> xs)
            {
                for (var i = xs.Count - 1; i > 0; i--)
                {
                    var j = rng.Int(i + 1);
                    var t = xs[i];
                    xs[i] = xs[j];
                    xs[j] = t;
                }
                return xs;
            }
            // `layer(z)?.tiles[y * width + x] ?? null`: x is not checked, so a tile off a row's end reads the next row's.
            string? TileAt(int x, int y, int z) => Js.At(TilesOf(z), y * width + x, null);
            List<Pos> Cells(HomeRoom r)
            {
                var ps = new List<Pos>();
                for (var y = r.Y; y < r.Y + r.H; y++) for (var x = r.X; x < r.X + r.W; x++) ps.Add(new Pos { X = x, Y = y, Z = r.Z });
                return ps;
            }
            // A room's open floor, clear of the ways through its doors and up its ladders.
            List<Pos> FloorOf(HomeRoom r) =>
                Cells(r).Where(
                    (p) =>
                        TileAt(p.X, p.Y, p.Z) == TileKinds.Floor &&
                        !blocked.Contains((p.X, p.Y, p.Z)) &&
                        !Grid.Dirs4.Any((d) =>
                        {
                            var k = TileAt(p.X + d.X, p.Y + d.Y, p.Z);
                            return k == TileKinds.Door || k == TileKinds.DoorOpen || k == TileKinds.Ladder;
                        })).ToList();
            var wear = site.Wear;
            var rooms = site.Rooms.Where((r) => HasLayer(r.Z)).ToList();
            // The dead: none or one in a kept house, up to three in a wrecked one; some on the beds.
            var bodies = wear < 0.35 ? (rng.Next() < 0.35 ? 1 : 0) : wear < 0.65 ? rng.Range(0, 2) : rng.Range(1, 3);
            var all = Shuffled(rooms.SelectMany(FloorOf).ToList());
            var placed = 0;
            foreach (var p in all)
            {
                if (placed >= bodies) break;
                if (used.Contains((p.X, p.Y, p.Z))) continue;
                Add(MedicalDetailKinds.CorpseCivilian, p);
                Add(MedicalDetailKinds.BloodPool, p);
                // The trail it left, a tile off.
                var trails = Grid.Dirs4.Select((d, facing) => new Facing { P = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z }, F = facing })
                    .Where((q) => TileAt(q.P.X, q.P.Y, q.P.Z) == TileKinds.Floor && !blocked.Contains((q.P.X, q.P.Y, q.P.Z))).ToList();
                var trail = Shuffled(trails).FirstOrDefault();
                if (trail != null && wear > 0.3) Add(MedicalDetailKinds.BloodDrops, trail.P, trail.F);
                placed++;
            }
            foreach (var r in rooms)
            {
                var floor = Shuffled(FloorOf(r).Where((p) => !used.Contains((p.X, p.Y, p.Z))).ToList());
                var ps = Cells(r);
                var ds = DecorOf(r.Z);
                // By what stands in it: clothes off a bed, books off a shelf or a desk.
                foreach (var p in ps)
                {
                    var look = Decor.DecorOf(Js.At(ds, p.Y * width + p.X, 0));
                    if (look == null) continue;
                    if (look.Kind == DecorKinds.Bed && rng.Next() < 0.35 + wear * 0.5) Add(MedicalDetailKinds.RumpledLinen, p, look.Facing);
                    if ((look.Kind == DecorKinds.Shelf || look.Kind == DecorKinds.Desk) && rng.Next() < wear)
                    {
                        var near = floor.Find((q) => !used.Contains((q.X, q.Y, q.Z)) && Math.Abs(q.X - p.X) + Math.Abs(q.Y - p.Y) == 1);
                        if (near != null) Add(MedicalDetailKinds.Books, near);
                    }
                }
                // Strewn about, the more the worse off it is.
                var mess = (int)Js.Round(wear * (r.Use == HomeUses.Bath || r.Use == HomeUses.Hall || r.Use == HomeUses.Toilet || r.Use == HomeUses.Pantry ? 1 : 3) + rng.Next() * 0.8);
                foreach (var p in floor.Take(mess).ToList())
                {
                    if (used.Contains((p.X, p.Y, p.Z))) continue;
                    var pick = rng.Next();
                    var kind = pick < 0.45 ? MedicalDetailKinds.ScatteredPapers : pick < 0.7 && (r.Use == HomeUses.Kitchen || r.Use == HomeUses.Living || r.Use == HomeUses.Study) ? MedicalDetailKinds.OverturnedChair : pick < 0.85 && r.Use == HomeUses.Bedroom ? MedicalDetailKinds.Luggage : MedicalDetailKinds.ScatteredPapers;
                    Add(kind != MedicalDetailKinds.ScatteredPapers && strew != null && !strew(p) ? MedicalDetailKinds.ScatteredPapers : kind, p);
                }
                // Grime along the walls; blood up one in a wrecked house.
                var edges = floor.Where((p) => p.X == r.X || p.Y == r.Y || p.X == r.X + r.W - 1 || p.Y == r.Y + r.H - 1).ToList();
                foreach (var p in edges.Take((int)Js.Round(wear * 3)).ToList()) Add(MedicalDetailKinds.FloorGrime, p);
                if (wear > 0.6 && rng.Next() < wear - 0.3)
                {
                    var walls = floor.SelectMany((p) => Grid.Dirs4.SelectMany((d, facing) => TileAt(p.X - d.X, p.Y - d.Y, p.Z) == TileKinds.Wall ? new[] { new Facing { P = p, F = facing } } : new Facing[0])).ToList();
                    var wall = Shuffled(walls).FirstOrDefault();
                    if (wall != null) Add(MedicalDetailKinds.WallBlood, wall.P, wall.F);
                }
            }
            return details;
        }
    }
}
