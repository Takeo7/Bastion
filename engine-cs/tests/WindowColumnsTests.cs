using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The windows of a building in columns (playtest note 17, decided by Arturo on 7/11; WindowColumns): on each side one
    /// set of columns, as far apart on every storey, centred on the side; the ground floor uses them too, but at a door
    /// and beside it. What the client draws the storeys of a façade by (WallLayout.Storeys).
    /// </summary>
    public class WindowColumnsTests
    {
        [Fact(DisplayName = "window columns › centred on the side, `every` apart, never nearer a corner than its third tile; an odd tile over at the east or south end")]
        public void CentredOnTheSide()
        {
            Assert.Equal(new[] { 2, 5 }, WindowColumns.Along(8, 3));
            Assert.Equal(new[] { 3, 6 }, WindowColumns.Along(10, 3));
            Assert.Equal(new[] { 3 }, WindowColumns.Along(7, 3));
            Assert.Equal(new[] { 2, 4, 6 }, WindowColumns.Along(9, 2));
            // Two over (u 2 to 5, columns 2 and 4): the one left at the end, u 5.
            Assert.Equal(new[] { 2, 4 }, WindowColumns.Along(8, 2));
            Assert.Equal(new[] { 2 }, WindowColumns.Along(5, 2));
            Assert.Empty(WindowColumns.Along(4, 2));
            Assert.Equal(3, WindowColumns.Every(true));
            Assert.Equal(2, WindowColumns.Every(false));
            // A building from (10, 20) to (17, 29): its north side 8 tiles, its east side 10.
            Assert.True(WindowColumns.IsColumn(10, 20, 17, 29, 0, 12, 20, 2));
            Assert.False(WindowColumns.IsColumn(10, 20, 17, 29, 0, 13, 20, 2));
            Assert.True(WindowColumns.IsColumn(10, 20, 17, 29, 1, 17, 26, 3));
            Assert.False(WindowColumns.IsColumn(10, 20, 17, 29, 1, 17, 25, 3));
            Assert.True(WindowColumns.ByOpening(TileKinds.Wall, TileKinds.Door, TileKinds.Wall));
            Assert.True(WindowColumns.ByOpening(TileKinds.Door, TileKinds.Wall, TileKinds.Wall));
            Assert.False(WindowColumns.ByOpening(TileKinds.Wall, TileKinds.Window, TileKinds.Wall));
        }

        private const int Indoor = 2;

        private static bool Glass(string? kind) => kind == TileKinds.Window || kind == TileKinds.WindowBroken;

        private static bool Solid(string? kind) => kind == TileKinds.Wall || Glass(kind);

        /// <summary>
        /// The buildings of `l` with a storey over the ground, by their tiles walls included: on level 1, each inside (through
        /// doors, furniture and the stairwell; not walls or windows) with a ring of wall and windows round it.
        /// </summary>
        private static List<(int X0, int Y0, int X1, int Y1)> Storeyed(MissionLayout l)
        {
            var tiles = l.Levels![0].Tiles;
            var (w, h) = (l.Width, l.Height);
            var seen = new bool[w * h];
            var out_ = new List<(int, int, int, int)>();
            for (var i = 0; i < tiles.Count; i++)
            {
                if (seen[i] || tiles[i] == null || Solid(tiles[i]) || tiles[i] == TileKinds.Roof) continue;
                var (x0, y0, x1, y1) = (int.MaxValue, int.MaxValue, -1, -1);
                var queue = new List<int> { i };
                seen[i] = true;
                for (var k = 0; k < queue.Count; k++)
                {
                    var (x, y) = (queue[k] % w, queue[k] / w);
                    (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
                    foreach (var d in Grid.Dirs4)
                    {
                        var (qx, qy) = (x + d.X, y + d.Y);
                        if (qx < 0 || qy < 0 || qx >= w || qy >= h || seen[qy * w + qx] || Solid(tiles[qy * w + qx])) continue;
                        seen[qy * w + qx] = true;
                        queue.Add(qy * w + qx);
                    }
                }
                (x0, y0, x1, y1) = (x0 - 1, y0 - 1, x1 + 1, y1 + 1);
                if (x0 < 0 || y0 < 0 || x1 >= w || y1 >= h) continue;
                var ring = Enumerable.Range(x0, x1 - x0 + 1).SelectMany((x) => new[] { y0 * w + x, y1 * w + x })
                    .Concat(Enumerable.Range(y0, y1 - y0 + 1).SelectMany((y) => new[] { y * w + x0, y * w + x1 }));
                if (ring.All((j) => Solid(tiles[j]))) out_.Add((x0, y0, x1, y1));
            }
            return out_;
        }

        [Fact(DisplayName = "window columns › towns, cities and estates: on every storey the windows of a side are in its columns, and under each one upstairs there is one on the ground, but by a door")]
        public void GeneratedMapsKeepTheirColumns()
        {
            var maps = Enumerable.Range(1, 10).Select((k) => (Kind: Places.OldTownSeed(k * 7919) ? "town" : "city", Seed: k * 7919, Site: (string?)null))
                .Concat(Enumerable.Range(1, 4).Select((k) => (Kind: "industrial", Seed: k * 7919, Site: (string?)Sites.Industrial)))
                .ToList();
            Assert.True(maps.Count((m) => m.Kind == "town") >= 3 && maps.Count((m) => m.Kind == "city") >= 3);
            int buildings = 0, upper = 0, overWindow = 0, overDoor = 0, partition = 0;
            foreach (var (kind, seed, site) in maps)
            {
                var l = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Site = site, Kind = MissionKinds.Hack, PodSizes = new List<int> { 2, 3, 3, 2 }, Seed = seed });
                var (w, h) = (l.Width, l.Height);
                var levels = new List<IReadOnlyList<string?>> { l.Tiles.Cast<string?>().ToList() };
                levels.AddRange(l.Levels!.Select((v) => (IReadOnlyList<string?>)v.Tiles));
                string? At(int z, int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? null : levels[z][y * w + x];
                foreach (var (x0, y0, x1, y1) in Storeyed(l))
                {
                    buildings++;
                    var home = l.Homes?.Any((b) => b.X == x0 && b.Y == y0 && b.W == x1 - x0 + 1 && b.H == y1 - y0 + 1) == true;
                    var every = WindowColumns.Every(home);
                    // Its storeys: the levels its ring of wall stands on.
                    var top = 1;
                    while (top + 1 < levels.Count && Enumerable.Range(x0, x1 - x0 + 1).All((x) => Solid(At(top + 1, x, y0)) && Solid(At(top + 1, x, y1)))) top++;
                    // A hall's stairs in a house (MakeBuilding: no window beside them).
                    var halls = (l.Homes ?? new List<HomeSite>()).SelectMany((b) => b.Rooms).Where((r) => r.Z == 0 && r.Use == HomeUses.Hall).ToList();
                    for (var s = 0; s < 4; s++)
                    {
                        var n = s % 2 == 0 ? x1 - x0 + 1 : y1 - y0 + 1;
                        (int X, int Y) Tile(int u) => s == 0 ? (x0 + u, y0) : s == 1 ? (x1, y0 + u) : s == 2 ? (x0 + u, y1) : (x0, y0 + u);
                        var label = $"{kind}/{seed}: {x0},{y0}–{x1},{y1} side {s}";
                        for (var z = 0; z <= top; z++)
                        {
                            for (var u = 1; u < n - 1; u++)
                            {
                                var (x, y) = Tile(u);
                                if (!Glass(At(z, x, y))) continue;
                                Assert.True(WindowColumns.IsColumn(x0, y0, x1, y1, s, x, y, every), $"{label}: a window at {x},{y},{z} off its columns");
                                if (z == 0) continue;
                                upper++;
                                var d = Grid.Dirs4[s];
                                var along = Grid.Dirs4[(s + 1) % 4];
                                var (ox, oy) = (x + d.X, y + d.Y);
                                var (ix, iy) = (x - d.X, y - d.Y);
                                if (Glass(At(0, x, y))) overWindow++;
                                else if (WindowColumns.ByOpening(At(0, x, y), At(0, x + along.X, y + along.Y), At(0, x - along.X, y - along.Y))) overDoor++;
                                else
                                {
                                    // The ground's other ways of having none: a wall outside or the map's edge, another building
                                    // the wall is shared with, a wall between rooms with no room to move (counted), a hall's stairs.
                                    var outside = At(0, ox, oy) == null || At(0, ox, oy) == TileKinds.Wall || l.Ground[oy * w + ox] == Indoor;
                                    var meets = At(0, ix, iy) == TileKinds.Wall || At(0, ix, iy) == TileKinds.Door;
                                    if (meets && !outside) partition++;
                                    var hall = halls.Any((r) => ix >= r.X && iy >= r.Y && ix < r.X + r.W && iy < r.Y + r.H);
                                    Assert.True(outside || meets || hall, $"{label}: a window at {x},{y},{z} over a bare wall");
                                }
                            }
                        }
                    }
                }
            }
            Assert.True(buildings > 50, $"{buildings} buildings");
            Assert.True(upper > 800, $"{upper} windows upstairs");
            // Most stand over a window or by a door; a wall between rooms takes very few.
            Assert.True(overWindow + overDoor > 0.75 * upper, $"{overWindow} over a window, {overDoor} by a door, of {upper}");
            Assert.True(partition < 0.05 * upper, $"{partition} of {upper} over a wall between rooms");
        }
    }
}
