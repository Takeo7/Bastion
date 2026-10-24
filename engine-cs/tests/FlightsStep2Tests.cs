using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/flights.test.ts, the tests FlightsStep1Tests leaves out: those that need rules.ts, ai.ts,
    /// abilities.ts or events.ts. «is no place to stop» is here whole, with its lines on riseSpot. «a walk stopped on it…»
    /// leaves out its second half: it puts its own spottedFrom on the Sim, and the C# Sim takes none. `toEqual` on the map's
    /// lists is Step2Json.Diff; a mission saved and loaded is a JSON round trip through the C# classes.
    /// </summary>
    public class FlightsStep2Tests
    {
        // docs/MAPAS.md, phase 2: a building's stairs are a flight of several tiles (3, 2 in small houses), climbed from its
        // foot to its landing a storey up, where nobody stops (Arturo, 19/10: «solo se entra por el pie y nadie se para a mitad»).

        /// <summary>
        /// A two-storey hall (x 1–12, y 1–5) with a flight of three up its middle row, climbing east: its tiles (4,3), (5,3)
        /// and (6,3), its foot (3,3), its landing (7,3) on the first floor, the stairwell over the flight. A door at (12,3)
        /// to the street.
        /// </summary>
        private static GameState Hall(int length = 3)
        {
            var s = StateFrom(new[]
            {
                "..............",
                ".############.",
                ".#..........#.",
                ".#..........+.",
                ".#..........#.",
                ".############.",
                "..............",
            });
            WithLevels(s, new[]
            {
                new[]
                {
                    "              ",
                    " ############ ",
                    " #..........# ",
                    " #..........# ",
                    " #..........# ",
                    " ############ ",
                    "              ",
                },
            });
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            for (var n = 0; n < length; n++)
            {
                var i = Grid.TileIndex(s, 6 - n, 3);
                s.Tiles[i] = TileKinds.Ladder;
                s.Decor[i] = Decor.DecorCode(n == 0 ? DecorKinds.Stairs : DecorKinds.Part, 1);
                s.Levels![0].Tiles[i] = null;
            }
            return s;
        }

        /// <summary>Whoever of `s` stands on a flight of several tiles (nobody should).</summary>
        private static List<string> OnFlights(GameState s)
        {
            return s.Units.Where((u) => u.Alive && Flights.MidFlight(s, u.Pos)).Select((u) => $"{u.Id}@{u.Pos.X},{u.Pos.Y},{u.Pos.Z}").ToList();
        }

        /// <summary>An active pod of `units`.</summary>
        private static void Active(GameState s, string id, params Unit[] units)
        {
            Pod(s, id, units);
        }

        // ------------------------------------------------------------ a flight of stairs of several tiles

        [Fact(DisplayName = "a flight of stairs of several tiles › is no place to stop: never a destination, nor a path end, a step-out or a place to rise or drop things")]
        public void IsNoPlaceToStop()
        {
            // FlightsStep1Tests has this test without its lines on riseSpot (abilities.ts): here it is whole.
            var s = Hall();
            var u = AddUnit(s, "s1", TemplateIds.Assault, 2, 3);
            var reach = Paths.ComputeReach(s, u, 100);
            // Walked across (the way up passes it), never stopped on.
            foreach (var x in new[] { 4, 5, 6 })
            {
                Assert.True(reach.Cost.Has(Grid.NodeOf(s, P(x, 3))));
                Assert.False(Flights.CanStopAt(s, P(x, 3)));
                Assert.Null(Paths.BuildPath(s, u, reach, P(x, 3)));
            }
            var ends = Paths.Destinations(s, u, reach).Select((d) => d.Pos).ToList();
            Assert.DoesNotContain(ends, (p) => Flights.MidFlight(s, p));
            HasPlace(ends, P(3, 3));
            HasPlace(ends, P(7, 3, 1));
            SamePlaces(new[] { P(3, 3), P(4, 3), P(5, 3), P(6, 3), P(7, 3, 1), P(8, 3, 1) }, Paths.BuildPath(s, u, reach, P(8, 3, 1)));
            // A move that can't reach the landing ends short of the flight: its foot is as far as it goes.
            var @short = Paths.ComputeReach(s, u, 3 * Paths.CostOrtho);
            Assert.DoesNotContain(Paths.Destinations(s, u, @short).Select((d) => d.Pos), (p) => p.X > 3 && p.Y == 3);
            // What a soldier falling on it carried lands at its nearer end.
            SamePlace(P(3, 3), Flights.RestingPlace(s, P(4, 3)));
            SamePlace(P(7, 3, 1), Flights.RestingPlace(s, P(6, 3)));
            // A body on it rises at its nearer free end.
            var body = AddUnit(s, "a1", TemplateIds.Zombie, 5, 3, 0, (v) => v.Alive = false);
            SamePlace(P(7, 3, 1), Abilities.RiseSpot(s, body));
            body.Pos = P(4, 3);
            SamePlace(P(3, 3), Abilities.RiseSpot(s, body));
            AddUnit(s, "s2", TemplateIds.Assault, 3, 3);
            SamePlace(P(7, 3, 1), Abilities.RiseSpot(s, body));
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › a walk stopped on it goes back down to where it could stop; a patrol that sees the squad from it stops off it")]
        public void AWalkStoppedOnItGoesBackDown()
        {
            var s = Hall();
            var u = AddUnit(s, "s1", TemplateIds.Assault, 2, 3);
            AddUnit(s, "a9", TemplateIds.Zombie, 0, 6);
            var path = Paths.BuildPath(s, u, Paths.ComputeReach(s, u, 100), P(9, 3, 1))!;
            // Somebody comes onto the landing after the walk was planned.
            var other = AddUnit(s, "s2", TemplateIds.Assault, 7, 3, 1);
            var client = Events.CloneState(s);
            var sim = NewSim(s, 1);
            sim.MoveAlong(u, path, 1);
            SamePlace(P(3, 3), u.Pos);
            Assert.Empty(OnFlights(s));
            foreach (var e in sim.Events) Events.ApplyEvent(client, e);
            SamePlace(P(3, 3), client.Units.Find((x) => x.Id == "s1")!.Pos);
            s.Units = s.Units.Where((x) => x != other).ToList();
            u.Pos = P(11, 2);

            // A zombie of a pod on patrol going up: it sees a soldier from the flight's second tile, and stops on the landing.
            // Left out: the test makes the zombie see the soldier from there with a spottedFrom of its own on the Sim
            // (`patrol.spottedFrom = (_, p) => (p.x >= 5 ? u : undefined)`), and Sim.SpottedFrom is no hook in C#.
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › holds its slabs: a grenade breaks neither its landing, nor its foot nor its tiles upstairs")]
        public void HoldsItsSlabs()
        {
            var s = Hall();
            // A storey more: a flight from the first floor to a roof, over the landing's floor.
            s.Levels!.Add(new Level { Tiles = s.Levels[0].Tiles.Select((k) => k ?? TileKinds.Floor).ToList<string?>() });
            Assert.True(Flights.BearsFlight(s, P(7, 3, 1)));
            Assert.False(Abilities.BreaksFloor(s, AbilityIds.Grenade, P(7, 3, 1)));
            Assert.False(Flights.BearsFlight(s, P(8, 3, 1)));
            Assert.True(Abilities.BreaksFloor(s, AbilityIds.Grenade, P(8, 3, 1)));
            // One upstairs: its foot and its tiles bear it too.
            int I(int x) => Grid.TileIndex(s, x, 4);
            s.Levels[0].Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            foreach (var (x, kind) in new[] { (5, DecorKinds.Part), (6, DecorKinds.Part), (7, DecorKinds.Stairs) })
            {
                s.Levels[0].Tiles[I(x)] = TileKinds.Ladder;
                s.Levels[0].Decor![I(x)] = Decor.DecorCode(kind, 1);
                s.Levels[1].Tiles[I(x)] = null;
            }
            Assert.True(new[] { 4, 5, 6, 7 }.All((x) => Flights.BearsFlight(s, P(x, 4, 1))));
            Assert.True(Flights.BearsFlight(s, P(8, 4, 2)));
            Assert.False(Abilities.BreaksFloor(s, AbilityIds.Grenade, P(5, 4, 1)));
        }

        // ------------------------------------------------------------ the dead and the living on flights

        [Fact(DisplayName = "the dead and the living on flights › a horde upstairs comes down the flight to a soldier below, and nobody ends a turn on it")]
        public void AHordeUpstairsComesDownTheFlight()
        {
            var s = Hall();
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 10, 4);
            var dead = new[]
            {
                AddUnit(s, "a1", TemplateIds.Zombie, 10, 2, 1),
                AddUnit(s, "a2", TemplateIds.Zombie, 11, 4, 1),
                AddUnit(s, "a3", TemplateIds.Zombie, 9, 4, 1),
                AddUnit(s, "a4", TemplateIds.Zombie, 2, 2, 1),
            };
            Active(s, "p1", dead);
            foreach (var z in dead) z.Heard = Events.CopyPos(soldier.Pos);
            var down = 0;
            for (var turn = 0; turn < 6; turn++)
            {
                soldier.Hp = soldier.MaxHp;
                var sim = NewSim(s, turn);
                foreach (var z in dead) z.Ap = 2;
                sim.RunAlienTurn();
                var on = OnFlights(s);
                Assert.True(on.Count == 0, $"turn {turn}: {string.Join(" ", on)}");
                down = dead.Count((z) => z.Alive && z.Pos.Z == 0);
                if (down >= 2) break;
            }
            Assert.True(down >= 2, $"{down} down");
        }

        [Fact(DisplayName = "the dead and the living on flights › a living enemy upstairs goes round by the flight to a soldier below it, and ends no move on it")]
        public void ALivingEnemyUpstairsGoesRoundByTheFlight()
        {
            var s = Hall();
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 10, 4);
            var lancer = AddUnit(s, "a1", TemplateIds.Lancer, 10, 3, 1);
            Active(s, "p1", lancer);
            lancer.Heard = Events.CopyPos(soldier.Pos);
            for (var turn = 0; turn < 4 && lancer.Pos.Z == 1; turn++)
            {
                lancer.Ap = 2;
                NewSim(s, turn).RunAlienTurn();
                Assert.Empty(OnFlights(s));
            }
            Assert.Equal(0, Grid.HeightAt(s, lancer.Pos));
        }

        [Fact(DisplayName = "the dead and the living on flights › the Hinchado, too slow to climb a flight of three in one action, waits at its foot and climbs it with both")]
        public void TheHinchadoWaitsAtItsFoot()
        {
            var s = Hall();
            s.ActiveTeam = Teams.Alien;
            s.Mission.Siege = new Siege { Hold = 99 };
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 10, 3, 1);
            var bloater = AddUnit(s, "a1", TemplateIds.Bloater, 2, 2);
            Active(s, "p1", bloater);
            for (var turn = 0; turn < 4 && bloater.Pos.Z == 0; turn++)
            {
                soldier.Hp = soldier.MaxHp;
                bloater.Ap = 2;
                NewSim(s, turn).RunAlienTurn();
                var on = OnFlights(s);
                Assert.True(on.Count == 0, $"turn {turn}: {string.Join(" ", on)}");
            }
            Assert.Equal(1, bloater.Pos.Z);
        }

        // ------------------------------------------------------------ maps of before

        [Fact(DisplayName = "maps of before › a mission saved with flights of one tile plays as it did: on them from any side at the bottom, a climb, a place to stand")]
        public void AMissionSavedWithFlightsOfOneTilePlaysAsItDid()
        {
            var s = Hall(1);
            var saved = Setup.NormalizeMission(Events.CloneState(s));
            Assert.False(Flights.HasLongFlights(saved));
            Assert.Null(Flights.LongFlightAt(saved, P(6, 3)));
            Assert.Single(Flights.FlightAt(saved, P(6, 3))!.Flight.Tiles);
            Assert.True(Flights.CanStopAt(saved, P(6, 3)));
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(saved, P(6, 2), P(6, 3)));
            Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(saved, P(6, 3), P(7, 3, 1)));
            Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(saved, P(7, 3, 1), P(6, 3)));
            var u = AddUnit(saved, "s1", TemplateIds.Assault, 2, 2);
            AddUnit(saved, "a1", TemplateIds.Zombie, 0, 6);
            var r = Exec(saved, Move("s1", 6, 3));
            Assert.True(r.Ok, r.Error);
            SamePlace(P(6, 3), u.Pos);
            var up = Exec(saved, Move("s1", 9, 3, 1));
            Assert.True(up.Ok, up.Error);
            SamePlace(P(9, 3, 1), u.Pos);
        }

        [Fact(DisplayName = "maps of before › «La torre» keeps its flights of one tile; a generated mission saved and loaded keeps its flights whole")]
        public void LaTorreKeepsItsFlightsOfOneTile()
        {
            var torre = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("torre"), Slots = new List<int> { 0 }, Rng = new Rng(1) });
            Assert.False(Flights.HasLongFlights(torre));
            var gen = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 707), Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Hack });
            Assert.True(Flights.HasLongFlights(gen));
            var loaded = Setup.NormalizeMission(Read<GameState>(Step2Json.Element(gen)));
            foreach (var (what, a, b) in new (string, object?, object?)[] { ("tiles", gen.Tiles, loaded.Tiles), ("levels", gen.Levels, loaded.Levels), ("decor", gen.Decor, loaded.Decor) })
            {
                var diff = Step2Json.Diff(Step2Json.Element(a), Step2Json.Element(b), what);
                Assert.True(diff == null, diff);
            }
            // Nobody starts on one.
            Assert.Empty(OnFlights(gen));
        }
    }
}
