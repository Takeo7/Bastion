using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G14 (Arturo, 18/11: «en casas que están en ruinas no se aplica lo de las casas, se ven normal»): a ruin (Mapgen's
    /// Blocks.Ruin) has no storeys, so nothing in it is to find: who is in it shows to whoever has a line to them, as out in
    /// the open; the walking range goes in from the first turn; its walls still stand and stop sight. A whole building of
    /// the same map hides as before.
    /// </summary>
    public class RuinTests
    {
        /// <summary>
        /// The ruins of a map as the client finds them (BuildingPlan.FindRuins): walls of no building (no storeys), a few
        /// tiles apart at most along a row or a column, round a space at least 5 tiles a side and at most 17. Each, its tiles
        /// inside.
        /// </summary>
        private static List<List<int>> RuinsOf(GameState s)
        {
            int w = s.Width, h = s.Height;
            var storeys = s.Storeys!;
            bool Loose(int i) => s.Tiles[i] == TileKinds.Wall && storeys[i] == 0;
            var seen = new bool[w * h];
            var ruins = new List<List<int>>();
            for (var start = 0; start < w * h; start++)
            {
                if (seen[start] || !Loose(start)) continue;
                var tiles = new List<int> { start };
                seen[start] = true;
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                for (var k = 0; k < tiles.Count; k++)
                {
                    int x = tiles[k] % w, y = tiles[k] / w;
                    (x0, y0, x1, y1) = (System.Math.Min(x0, x), System.Math.Min(y0, y), System.Math.Max(x1, x), System.Math.Max(y1, y));
                    for (var d = -4; d <= 4; d++)
                    {
                        foreach (var (nx, ny) in new[] { (x + d, y), (x, y + d) })
                        {
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            var j = ny * w + nx;
                            if (seen[j] || !Loose(j)) continue;
                            seen[j] = true;
                            tiles.Add(j);
                        }
                    }
                }
                // (A farmhouse's or a cabin's: not a yard's walls running on.)
                if (x1 - x0 < 4 || y1 - y0 < 4 || x1 - x0 > 16 || y1 - y0 > 16) continue;
                var inside = new List<int>();
                for (var y = y0 + 1; y < y1; y++) for (var x = x0 + 1; x < x1; x++) inside.Add(y * w + x);
                ruins.Add(inside);
            }
            return ruins;
        }

        private static Pos PosOf(GameState s, int i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };

        private static bool Free(GameState s, int i) => Grid.IsWalkable(s.Tiles[i]) && Flights.CanStopAt(s, PosOf(s, i)) && Grid.UnitAt(s, PosOf(s, i)) == null;

        /// <summary>A forest Saqueo with a ruin and a whole building not found: the map, a tile in the ruin, one in the building.</summary>
        private static (GameState S, List<int> Ruin, int InRuin, int InHouse) Map()
        {
            for (var seed = 1; seed <= 60; seed++)
            {
                var (_, s) = RunInteriorTests.At(Places.Country, NodeKinds.Normal, seed);
                if (s.Storeys == null) continue;
                var known = new HashSet<int>(s.Discovered ?? new List<int>());
                var house = Enumerable.Range(0, s.Width * s.Height).Where((i) => s.Storeys[i] > 0 && s.Tiles[i] == TileKinds.Floor && !known.Contains(i) && Free(s, i)).ToList();
                if (house.Count == 0) continue;
                foreach (var ruin in RuinsOf(s))
                {
                    var spot = ruin.Where((i) => s.Tiles[i] == TileKinds.Floor && Free(s, i)).ToList();
                    if (spot.Count == 0) continue;
                    return (s, ruin, spot[spot.Count / 2], house.First((i) => Buildings.Hidden(s, PosOf(s, i))));
                }
            }
            throw new System.InvalidOperationException("no forest map with a ruin and a whole house");
        }

        [Fact]
        public void ARuinIsSeenAndWalkedAsTheOpen()
        {
            var (s, ruin, at, house) = Map();
            var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            var zombie = s.Units.First((u) => u.Team == Teams.Alien);
            // Nothing of it is a building's: no storeys inside it.
            Assert.All(ruin, (i) => Assert.Equal(0, s.Storeys![i]));
            zombie.Pos = PosOf(s, at);
            // From outside it, never in, with a line to them: shown.
            var from = Enumerable.Range(0, s.Width * s.Height).Where((i) => !ruin.Contains(i) && Free(s, i) && Grid.Distance(PosOf(s, i), zombie.Pos) <= 10).Select((i) => PosOf(s, i))
                .FirstOrDefault((p) =>
                {
                    soldier.Pos = p;
                    return Los.CanSee(s, soldier, zombie);
                });
            Assert.NotNull(from);
            soldier.Pos = from!;
            Assert.False(Buildings.Hidden(s, zombie.Pos));
            Assert.DoesNotContain(at, s.Discovered ?? new List<int>());
            // The walking range goes in: nothing of it veiled (the client's FoundInside), from the first turn.
            var mask = Enumerable.Repeat(true, s.Width * s.Height * Grid.LevelCount(s)).ToArray();
            Buildings.Veil(s, mask);
            Assert.All(ruin, (i) => Assert.True(mask[i]));
            // No window to look in by: nothing seen from beside one.
            Assert.DoesNotContain(Buildings.SidePeeks(s), ruin.Contains);
            // A whole building of the same map hides who is in it, as before.
            zombie.Pos = PosOf(s, house);
            Assert.True(Buildings.Hidden(s, zombie.Pos));
            Assert.False(mask[house]);
        }

        [Fact]
        public void ItsWallsStillStandAndNoRoofIsLeft()
        {
            // Producto, 18/11: a stretch of its wall still standing stops sight and covers as any wall; from above, nothing
            // over it any more (the roof a ruin's storeys put there: Grid.RoofSide).
            var tried = 0;
            for (var seed = 1; seed <= 60 && tried == 0; seed++)
            {
                var (_, s) = RunInteriorTests.At(Places.Country, NodeKinds.Normal, seed);
                foreach (var ruin in RuinsOf(s)) tried += Stretches(s, ruin);
            }
            Assert.True(tried > 0, "no stretch of a ruin's wall with a tile either side");
            // No roof over it: a line crossing it high goes over nothing; a whole building's, over its roof.
            var (m, ruins, _, house) = Map();
            Assert.All(ruins, (i) => Assert.Null(Grid.RoofSide(m, i % m.Width, i / m.Width, 1.5)));
            Assert.Equal(Grid.RoofSides.Over, Grid.RoofSide(m, house % m.Width, house / m.Width, m.Storeys![house] + 0.5));
        }

        /// <summary>Each stretch of `ruin`'s wall three long with a tile free either side: no line across it, a wall's cover. How many.</summary>
        private static int Stretches(GameState s, List<int> ruin)
        {
            var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            var zombie = s.Units.First((u) => u.Team == Teams.Alien);
            bool Wall(int x, int y) => Grid.InBounds(s, x, y) && s.Tiles[Grid.TileIndex(s, x, y)] == TileKinds.Wall;
            var tried = 0;
            foreach (var i in ruin.Where((i) => Free(s, i)))
            {
                var inside = PosOf(s, i);
                foreach (var d in Grid.Dirs4)
                {
                    int wx = inside.X + d.X, wy = inside.Y + d.Y;
                    var t = new Vec2 { X = d.Y, Y = -d.X };
                    if (!Wall(wx, wy) || !Wall(wx + t.X, wy + t.Y) || !Wall(wx - t.X, wy - t.Y)) continue;
                    var o = new Pos { X = wx + d.X, Y = wy + d.Y, Z = 0 };
                    if (!Grid.InBounds(s, o.X, o.Y) || ruin.Contains(Grid.TileIndex(s, o.X, o.Y)) || !Free(s, Grid.TileIndex(s, o.X, o.Y))) continue;
                    soldier.Pos = o;
                    zombie.Pos = inside;
                    tried++;
                    // Behind the stretch: no line, either way; its cover a wall's.
                    Assert.False(Los.CanSee(s, soldier, zombie), $"{o.X},{o.Y} → {inside.X},{inside.Y}");
                    Assert.False(Los.CanSee(s, zombie, soldier));
                    Assert.Equal(Grid.CoverOf(TileKinds.Wall), Grid.CoverAt(s, inside, new Vec2 { X = wx, Y = wy }));
                    Assert.Equal(Grid.CoverOf(TileKinds.Wall), Grid.CoverAt(s, o, new Vec2 { X = wx, Y = wy }));
                }
            }
            return tried;
        }
    }
}
