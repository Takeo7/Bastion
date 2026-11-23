using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// What the squad sees of an enemy (Los.CanSee: what can be aimed at) and what is drawn (Los.TeamVisibility: the fog,
    /// the figures) say the same (UI's probe, Producto 23/11): an enemy beside high cover peeks out of it, and is seen; its
    /// place was in the fog, and it acted with no figure.
    /// </summary>
    public class SeenAndDrawnTests
    {
        [Fact]
        public void AnEnemyPeekingOutBesideHighCoverIsDrawn()
        {
            // The soldier in line with a rock and the enemy behind it, at its side: the line to its place is cut, the one to
            // where it peeks out is not.
            var s = StateFrom(new[] { "..........", "..........", "....H.....", "..........", ".........." });
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 0, 2);
            var enemy = AddUnit(s, "a1", TemplateIds.Trooper, 5, 2);
            Assert.True(Los.CanSee(s, soldier, enemy));
            Assert.True(Los.TeamVisibility(s, Teams.Xcom)[Grid.NodeOf(s, enemy.Pos)]);
            // The fog round it as it was: only its place (the tile past it is still cut).
            Assert.False(Los.TeamVisibility(s, Teams.Xcom)[Grid.NodeOf(s, P(6, 2))]);
            // Gone, its place is fog again.
            enemy.Alive = false;
            Assert.False(Los.TeamVisibility(s, Teams.Xcom)[Grid.NodeOf(s, P(5, 2))]);
        }

        [Fact]
        public void BesideHighCoverOnTheMapsSeenIsDrawn()
        {
            // One of the dead moved beside every bit of high cover near the squad, on maps with rocks, walls and crates.
            var both = 0;
            foreach (var site in new[] { Places.Country, Places.Camping, Places.Factory, Places.Estate })
            {
                var (_, s) = LaunchAt(NodeKinds.Normal, 1, node: (n) => { n.Site = site; n.Seed = 501; n.Modifier = null; });
                var squad = s.Units.Where((u) => Los.IsViewer(u, Teams.Xcom)).ToList();
                var enemy = s.Units.First((u) => u.Team == Teams.Alien && u.Alive);
                var spots = 0;
                for (var y = 0; y < s.Height && spots < 40; y++)
                    for (var x = 0; x < s.Width && spots < 40; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = 0 };
                        if (!Flights.CanStopAt(s, p) || Grid.UnitAt(s, p) != null || !squad.Any((u) => Grid.Distance(u.Pos, p) <= 14)) continue;
                        if (!Grid.Dirs4.Any((d) => Grid.InBounds(s, x + d.X, y + d.Y) && Grid.KindAt(s, new Pos { X = x + d.X, Y = y + d.Y, Z = 0 }) == TileKinds.High)) continue;
                        spots++;
                        enemy.Pos = p;
                        var seen = squad.Any((u) => Los.CanSee(s, u, enemy));
                        var drawn = Los.TeamVisibility(s, Teams.Xcom)[Grid.NodeOf(s, p)];
                        Assert.True(seen == drawn, $"{site} {x},{y}: {(seen ? "se ve y no se dibuja" : "se dibuja y no se ve")}");
                        if (seen) both++;
                    }
            }
            Assert.True(both > 20, $"{both}");
        }
    }
}
