using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Los.TeamView by each viewer's own sight, kept while the map stays (Arte, 7/11: some 30 ms a move): the same to the
    /// bit as the whole team's at once (TeamVisibility), as viewers move one by one, fall and the map changes.
    /// </summary>
    public class TeamViewTests
    {
        [Fact(DisplayName = "the squad's view › kept viewer by viewer, it is the whole team's to the bit as they move, fall and the map changes")]
        public void KeptViewerByViewerItIsTheWholeTeams()
        {
            foreach (var biome in Biomes.All)
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, 5), Slots = new List<int> { 0 }, Rng = new Rng(5) });
                void Same(string when) => Assert.True(Los.TeamView(s, Teams.Xcom).SequenceEqual(Los.TeamVisibility(s, Teams.Xcom, Los.ViewReach(s))), $"{biome}: {when}");
                Same("as it starts");
                var squad = s.Units.Where((u) => u.Alive && u.Team == Teams.Xcom).ToList();
                foreach (var u in squad.Take(3))
                {
                    var reach = Paths.ComputeReach(s, u, Paths.MoveBudget(u));
                    var to = Paths.Destinations(s, u, reach).OrderBy((d) => d.Cost).Last().Pos;
                    u.Pos = new Pos { X = to.X, Y = to.Y, Z = to.Z };
                    Same($"{u.Id} moved");
                }
                squad[0].Alive = false;
                Same("one fell");
                var wall = Enumerable.Range(0, s.Tiles.Count).First((i) => s.Tiles[i] == TileKinds.Wall);
                s.Tiles[wall] = TileKinds.Floor;
                Same("a wall blown through");
            }
        }
    }
}
