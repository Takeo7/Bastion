using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N1, B (Arturo 17/11, cuadro 22: «solo un poco»; .dev-snapshots/gameplay/n1): against the wall right beside a window,
    /// the squad sees the two rows behind the glass, and who is in them; the building isn't found, and further in nobody shows.
    /// </summary>
    public class WindowSideTests
    {
        /// <summary>A lone ground-floor window of a house not found, plain wall two either side, a room at least 3 deep behind it: the window, out and along.</summary>
        private static (GameState S, Pos Window, Vec2 Out, Vec2 Along) LoneWindow()
        {
            for (var seed = 1; seed <= 12; seed++)
            {
                var (_, s) = RunInteriorTests.At(Places.Town, NodeKinds.Normal, seed);
                var storeys = s.Storeys!;
                var known = new HashSet<int>(s.Discovered ?? new List<int>());
                bool Wall(int x, int y) => Grid.InBounds(s, x, y) && s.Tiles[Grid.TileIndex(s, x, y)] == TileKinds.Wall;
                bool Room(int x, int y) => Grid.InBounds(s, x, y) && storeys[Grid.TileIndex(s, x, y)] > 0 && Grid.IsWalkable(s.Tiles[Grid.TileIndex(s, x, y)]) && !known.Contains(Grid.TileIndex(s, x, y));
                for (var w = 0; w < s.Width * s.Height; w++)
                {
                    if (!Grid.IsWindow(s.Tiles[w]) || storeys[w] <= 0) continue;
                    int wx = w % s.Width, wy = w / s.Width;
                    foreach (var d in Grid.Dirs4)
                    {
                        var n = new Vec2 { X = -d.X, Y = -d.Y };
                        var t = new Vec2 { X = d.Y, Y = -d.X };
                        if (!Wall(wx + t.X, wy + t.Y) || !Wall(wx - t.X, wy - t.Y) || !Wall(wx + 2 * t.X, wy + 2 * t.Y) || !Wall(wx - 2 * t.X, wy - 2 * t.Y)) continue;
                        if (!Room(wx + d.X, wy + d.Y) || !Room(wx + 2 * d.X, wy + 2 * d.Y) || !Room(wx + 3 * d.X, wy + 3 * d.Y)) continue;
                        var side = new Pos { X = wx + n.X + t.X, Y = wy + n.Y + t.Y, Z = 0 };
                        if (!Grid.InBounds(s, side.X, side.Y) || storeys[Grid.TileIndex(s, side.X, side.Y)] > 0 || !Flights.CanStopAt(s, side) || Grid.UnitAt(s, side) != null) continue;
                        return (s, new Pos { X = wx, Y = wy, Z = 0 }, n, t);
                    }
                }
            }
            throw new System.InvalidOperationException("no lone window");
        }

        [Fact]
        public void FromBesideTheTwoRowsBehindTheGlassShowNotTheBuilding()
        {
            var (s, w, n, t) = LoneWindow();
            var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
            var zombie = s.Units.First((u) => u.Team == Teams.Alien);
            Pos Behind(int rows) => new Pos { X = w.X - n.X * rows, Y = w.Y - n.Y * rows, Z = 0 };
            // From afar, nothing.
            zombie.Pos = Behind(1);
            Assert.True(Buildings.Hidden(s, zombie.Pos));
            // Against the wall beside the window: one and two rows in show, and who stands there.
            soldier.Pos = new Pos { X = w.X + n.X + t.X, Y = w.Y + n.Y + t.Y, Z = 0 };
            var strip = Buildings.SideStrip(s, soldier.Pos);
            Assert.Contains(Grid.TileIndex(s, Behind(1).X, Behind(1).Y), strip);
            Assert.Contains(Grid.TileIndex(s, Behind(2).X, Behind(2).Y), strip);
            Assert.DoesNotContain(Grid.TileIndex(s, Behind(3).X, Behind(3).Y), strip);
            Assert.Equal(strip.OrderBy((i) => i), Buildings.SidePeeks(s).Where(strip.Contains).OrderBy((i) => i));
            Assert.False(Buildings.Hidden(s, zombie.Pos));
            Assert.True(Los.CanSee(s, soldier, zombie));
            var mask = new bool[s.Width * s.Height * Grid.LevelCount(s)];
            mask[Grid.TileIndex(s, Behind(1).X, Behind(1).Y)] = true;
            mask[Grid.TileIndex(s, Behind(3).X, Behind(3).Y)] = true;
            Buildings.Veil(s, mask);
            Assert.True(mask[Grid.TileIndex(s, Behind(1).X, Behind(1).Y)]);
            Assert.False(mask[Grid.TileIndex(s, Behind(3).X, Behind(3).Y)]);
            // Three rows in: hidden still. The building isn't found.
            zombie.Pos = Behind(3);
            Assert.True(Buildings.Hidden(s, zombie.Pos));
            Assert.DoesNotContain(Grid.TileIndex(s, Behind(1).X, Behind(1).Y), Buildings.FoundTiles(s));
            // Gone from beside it: nothing again.
            zombie.Pos = Behind(1);
            soldier.Pos = new Pos { X = w.X + 3 * n.X, Y = w.Y + 3 * n.Y, Z = 0 };
            if (Flights.CanStopAt(s, soldier.Pos)) Assert.True(Buildings.Hidden(s, zombie.Pos));
            // Right before the window, as ever: found.
            soldier.Pos = new Pos { X = w.X + n.X, Y = w.Y + n.Y, Z = 0 };
            Assert.Empty(Buildings.SideStrip(s, soldier.Pos));
            Assert.False(Buildings.Hidden(s, zombie.Pos, new[] { soldier.Pos }));
        }
    }
}
