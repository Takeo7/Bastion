using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// T24 (Arturo, 18/11: «cuando llegas a la casilla de la lata mejor entras en la lata, ese soldado se salva ya»; the box
    /// and Producto): a soldier whose walk ends on a tile of La Lata's zone with a seat free gets in, safe, off the field; it
    /// may come down in its turn for an action; La Lata goes when called, by itself only with all the living in, and waits
    /// full with someone out; no seat, no getting in; an item carried in counts; the recruit only if in when it goes.
    /// </summary>
    public class LaLataAboardTests
    {
        private static List<Unit> Squaddies(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();

        /// <summary>A run's fight alone with `level` of the squad upgrade (0: 2 seats; 1: 4), the squad as many as the seats.</summary>
        private static (CampaignState C, GameState S) Fight(int level = 1, string kind = NodeKinds.Normal, int seed = 1)
        {
            return LaunchAt(kind, seed, before: (c) =>
            {
                c.Upgrades![UpgradeIds.Squad] = level;
                c.Squad = Runs.DefaultSquad(c, c.Players);
            }, node: (n) => { while (RunMap.LooseSurvivor(n)) n.Seed++; });
        }

        /// <summary>A tile next to `zone`'s tile `z`, free, that can be stood on and is not in the zone: a step away from it.</summary>
        private static Pos Beside(GameState s, Vec2 z)
        {
            foreach (var d in Grid.Dirs8)
            {
                var p = new Pos { X = z.X + d.X, Y = z.Y + d.Y, Z = 0 };
                if (Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && !Abilities.InEvacZone(s, p)) return p;
            }
            throw new System.InvalidOperationException("nowhere beside La Lata");
        }

        /// <summary>`u` a step from La Lata's zone tile `i`, walked onto it.</summary>
        private static CommandResult WalkIn(GameState s, Unit u, int i, int slot = 0)
        {
            var z = s.Mission.EvacZone[i];
            u.Pos = Beside(s, z);
            u.Ap = 2;
            u.Moves = null;
            return Exec(s, Move(u.Id, z.X, z.Y), slot: slot);
        }

        [Fact]
        public void EndingAWalkOnItsTileGetsIn()
        {
            var (_, s) = Fight();
            var u = Squaddies(s)[0];
            Assert.True(Rules.LaLataHere(s));
            Assert.Equal(4, Rules.SeatsFree(s));
            var z = s.Mission.EvacZone[0];
            u.Pos = Beside(s, z);
            Assert.True(Rules.WouldBoard(s, u, new Pos { X = z.X, Y = z.Y, Z = 0 }));
            var r = Exec(s, Move(u.Id, z.X, z.Y));
            Assert.True(r.Ok, r.Error);
            Assert.Single(Of<BoardedEvent>(r.Events), (e) => e.Unit == u.Id);
            Assert.True(u.Aboard);
            Assert.False(u.Alive);
            Assert.True(u.Evacuated);
            Assert.Equal(3, Rules.SeatsFree(s));
            // Its tile is free for the next; nobody sees it nor can hit it.
            Assert.Null(Grid.UnitAt(s, new Pos { X = z.X, Y = z.Y, Z = 0 }));
            Assert.DoesNotContain(u, Grid.Combatants(s, Teams.Xcom));
            // La Lata hasn't gone: the rest are out.
            Assert.Null(s.Outcome);
            Assert.Null(s.Mission.EvacDone);
        }

        [Fact]
        public void WalkingOverItDoesntGetIn()
        {
            var (_, s) = Fight();
            var u = Squaddies(s)[0];
            // Set down on a tile of it (no walk ended there), and walked off it: nothing; walked back on: in.
            var z = s.Mission.EvacZone[0];
            u.Pos = new Pos { X = z.X, Y = z.Y, Z = 0 };
            var off = Beside(s, z);
            var r = Exec(s, Move(u.Id, off.X, off.Y));
            Assert.True(r.Ok, r.Error);
            Assert.Empty(Of<BoardedEvent>(r.Events));
            Assert.True(u.Alive);
            Assert.False(Rules.Boards(s, u));
            var back = Exec(s, Move(u.Id, z.X, z.Y));
            Assert.True(back.Ok, back.Error);
            Assert.Single(Of<BoardedEvent>(back.Events));
        }

        [Fact]
        public void AllTheLivingInItGoesByItself()
        {
            var (c, s) = Fight();
            var squad = Squaddies(s);
            for (var i = 0; i < squad.Count; i++)
            {
                var r = WalkIn(s, squad[i], i);
                Assert.True(r.Ok, r.Error);
                if (i < squad.Count - 1) Assert.Null(s.Outcome);
                else
                {
                    Assert.Single(Of<EvacDepartedEvent>(r.Events));
                    Assert.Equal(Outcomes.Victory, s.Outcome);
                }
            }
            Assert.All(squad, (u) => Assert.True(u.Evacuated));
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.Empty(report.Run!.Fallen);
        }

        [Fact]
        public void FullAndSomeoneOutItWaitsAndNoSeatNoGettingIn()
        {
            // Two seats, three of the squad (one more for the test): two in, the third on a tile of it stays out and La
            // Lata waits for a call.
            var (_, s) = Fight(level: 0);
            var squad = Squaddies(s);
            Assert.Equal(2, squad.Count);
            var third = Events.CloneUnit(squad[1]);
            third.Id = "s9";
            third.CampaignId = "c99";
            third.Pos = new Pos { X = 0, Y = 0, Z = 0 };
            s.Units.Add(third);
            // A third tile for it to stand on, of the zone (as on a map without the vehicle's place).
            s.Mission.EvacZone = s.Mission.EvacZone.Concat(new[] { new Vec2 { X = Beside(s, s.Mission.EvacZone[0]).X, Y = Beside(s, s.Mission.EvacZone[0]).Y } }).ToList();
            Assert.True(WalkIn(s, squad[0], 0).Ok);
            Assert.True(WalkIn(s, squad[1], 1).Ok);
            Assert.Equal(0, Rules.SeatsFree(s));
            Assert.Null(s.Outcome);
            var z = s.Mission.EvacZone[2];
            third.Pos = Grid.Dirs8.Select((d) => new Pos { X = z.X + d.X, Y = z.Y + d.Y, Z = 0 })
                .First((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && !Abilities.InEvacZone(s, p));
            third.Ap = 2;
            var r = Exec(s, Move(third.Id, z.X, z.Y));
            Assert.True(r.Ok, r.Error);
            Assert.Empty(Of<BoardedEvent>(r.Events));
            Assert.True(third.Alive);
            Assert.Null(s.Outcome);
            // One inside calls it: it goes, the third lost.
            var gone = Exec(s, new EvacCommand { Unit = squad[0].Id });
            Assert.True(gone.Ok, gone.Error);
            Assert.True(third.Lost);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact]
        public void ComingDownCostsAnActionToAFreeTileOfIt()
        {
            var (_, s) = Fight();
            var squad = Squaddies(s);
            Assert.True(WalkIn(s, squad[0], 0).Ok);
            var u = squad[0];
            // Its turn comes round: its actions, to come down with.
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = s.Turn + 1 });
            Assert.Equal(Content.ApPerTurnOf(u), u.Ap);
            var free = Rules.AlightTiles(s);
            Assert.NotEmpty(free);
            Assert.Equal("Solo a una casilla libre junto a " + Lore.Bus.Name + ".", Rules.AlightRefusal(s, u, 0, new[] { 0 }, new Pos { X = 0, Y = 0, Z = 0 }));
            var ap = u.Ap;
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Alight, Tile = free[0] });
            Assert.True(r.Ok, r.Error);
            Assert.Single(Of<AlightedEvent>(r.Events));
            Assert.True(u.Alive);
            Assert.Null(u.Aboard);
            Assert.False(u.Evacuated);
            Assert.True(Grid.SamePos(free[0], u.Pos));
            Assert.Equal(ap - 1, u.Ap);
            Assert.Null(u.Moves);
            Assert.Equal(4, Rules.SeatsFree(s));
            // Not in La Lata: nothing to come down from.
            Assert.Equal("No está en " + Lore.Bus.Name + ".", Rules.AlightRefusal(s, u, 0, new[] { 0 }, free[0]));
        }

        [Fact]
        public void AnItemCarriedInCountsAndStays()
        {
            var (_, s) = Fight(kind: NodeKinds.Recovery);
            var u = Squaddies(s)[0];
            s.Mission.Item!.Carrier = u.Id;
            s.Mission.Item.Pos = null;
            Assert.True(WalkIn(s, u, 0).Ok);
            Assert.True(s.Mission.ObjectiveDone);
            Assert.True(s.Mission.Item.Evacuated);
            Assert.Null(s.Mission.Item.Carrier);
            // Down again, it stays in La Lata, done.
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = s.Turn + 1 });
            Assert.True(Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Alight, Tile = Rules.AlightTiles(s)[0] }).Ok);
            Assert.True(s.Mission.ObjectiveDone);
            Assert.Null(s.Mission.Item.Carrier);
        }

        [Fact]
        public void InAnAsedioNotTillItComes()
        {
            var (_, s) = Fight(kind: NodeKinds.Hold);
            Assert.NotNull(s.Mission.HoldUntil);
            Assert.False(Rules.LaLataHere(s));
            var r = WalkIn(s, Squaddies(s)[0], 0);
            Assert.True(r.Ok, r.Error);
            Assert.Empty(Of<BoardedEvent>(r.Events));
        }

        [Fact]
        public void TheEnemiesDontSeeNorHitWhoIsIn()
        {
            var (_, s) = Fight();
            var u = Squaddies(s)[0];
            Assert.True(WalkIn(s, u, 0).Ok);
            var dead = s.Units.First((x) => x.Team == Teams.Alien);
            Assert.False(Los.CanSee(s, dead, u));
            Assert.DoesNotContain(u, Grid.Combatants(s, Teams.Xcom));
        }

        [Fact]
        public void WithoutTheVehiclesPlaceTheZoneIsCutToTheSeats()
        {
            // Red, 18/11: a service area, map seed 5111, had 9 tiles for 2 seats.
            var (c, s) = LaunchAt(NodeKinds.Rescue, 1, before: (x) =>
            {
                x.Upgrades![UpgradeIds.Squad] = 0;
                x.Squad = Runs.DefaultSquad(x, x.Players);
            }, node: (n) => { n.Site = Places.Roadside; n.Seed = 5111; });
            Assert.Equal(2, s.Mission.Seats);
            Assert.Equal(2, s.Mission.EvacZone.Count);
        }

        [Fact]
        public void ATwoPlayerRescateIsPlayedThroughByComingDown()
        {
            // Producto, 18/11 (Red's finding): Ana's soldier and the survivor aboard the motorbike's two seats, Pepe's soldier
            // out with none; Ana gets the command though all hers are in, brings the survivor down, and Pepe's gets in.
            var c = Two(3);
            c.Upgrades![UpgradeIds.Squad] = 0;
            c.Squad = Runs.DefaultSquad(c, c.Players);
            GoTo(c, NodeKinds.Rescue);
            var s = new EngineCampaignRules().LaunchMission(c, new Rng(103))!.Value.state;
            var both = new[] { 0, 1 };
            var ana = Squaddies(s).Single((u) => u.Owner == 0);
            var pepe = Squaddies(s).Single((u) => u.Owner == 1);
            var vip = s.Units.Single((u) => u.Id == s.Mission.Survivor);
            // Talked to by Ana's (as RunRescueTests): hers to command.
            vip.Captive = false;
            vip.Owner = 0;
            s.Mission.SurvivorFound = true;
            s.Command = 0;
            Assert.True(WalkIn(s, ana, 0).Ok);
            var r = WalkIn(s, vip, 1);
            Assert.True(r.Ok, r.Error);
            Assert.True(vip.Aboard);
            Assert.Equal(0, Rules.SeatsFree(s));
            Assert.Null(s.Outcome);
            // Pepe's, on a tile of it now free of nobody but full: stays out, La Lata waits.
            s.Mission.EvacZone = s.Mission.EvacZone.Concat(new[] { new Vec2 { X = Beside(s, s.Mission.EvacZone[0]).X, Y = Beside(s, s.Mission.EvacZone[0]).Y } }).ToList();
            s.Command = 1;
            var full = WalkIn(s, pepe, 2, slot: 1);
            Assert.True(full.Ok, full.Error);
            Assert.True(pepe.Alive);
            Assert.Null(s.Outcome);
            // A new turn of the squad: Ana's two inside have their actions, so she has a turn.
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = s.Turn + 1, Command = 1 });
            var sim = NewSim(s, 1, 0, 1);
            Assert.True(sim.HasActions(0));
            Assert.Contains(sim.OpeningCommander(2), both);
            // Pepe passes her the command; she brings the survivor down.
            var pass = Exec(s, new PassCommand(), slot: 1, connected: both);
            Assert.True(pass.Ok, pass.Error);
            Assert.Equal(0, s.Command);
            var tile = Rules.AlightTiles(s).First((p) => !Grid.SamePos(p, pepe.Pos));
            var down = Exec(s, new AbilityCommand { Unit = vip.Id, Ability = AbilityIds.Alight, Tile = tile }, slot: 0, connected: both);
            Assert.True(down.Ok, down.Error);
            Assert.True(vip.Alive);
            Assert.Equal(1, Rules.SeatsFree(s));
            // Back to Pepe: his soldier steps off and on again, and is in.
            if (s.Command != 1) Assert.True(Exec(s, new PassCommand(), slot: 0, connected: both).Ok);
            var off = Beside(s, s.Mission.EvacZone[0]);
            if (Grid.UnitAt(s, off) != null) off = Grid.Dirs8.Select((d) => new Pos { X = pepe.Pos.X + d.X, Y = pepe.Pos.Y + d.Y, Z = 0 }).First((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && !Abilities.InEvacZone(s, p));
            pepe.Pos = off;
            pepe.Ap = 2;
            pepe.Moves = null;
            var zone = s.Mission.EvacZone.First((z) => Grid.UnitAt(s, new Pos { X = z.X, Y = z.Y, Z = 0 }) == null);
            var inAgain = Exec(s, Move(pepe.Id, zone.X, zone.Y), slot: 1, connected: both);
            Assert.True(inAgain.Ok, inAgain.Error);
            Assert.True(pepe.Aboard);
            Assert.Equal(0, Rules.SeatsFree(s));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AsManyTilesAsSeatsFree(int level)
        {
            // Producto, 18/11: the zone shown and got in by, as many tiles as seats free; each one in shuts its own; full,
            // none. Down again, its tile is open again.
            var (_, s) = LaunchAt(NodeKinds.Normal, 2, before: (c) =>
            {
                c.Upgrades![UpgradeIds.Squad] = level;
                while (c.Soldiers.Count((x) => x.Alive) < Runs.SeatsOf(c)) Runs.NewSoldier(c, 0, new Rng(c.Soldiers.Count));
                c.Squad = Runs.DefaultSquad(c, c.Players);
            }, node: (n) => { n.Site = Places.Country; while (RunMap.LooseSurvivor(n)) n.Seed++; });
            var seats = s.Mission.Seats!.Value;
            var squad = Squaddies(s);
            Assert.Equal(seats, squad.Count);
            Assert.Equal(seats, Rules.OpenTiles(s).Count);
            for (var i = 0; i < seats - 1; i++)
            {
                var open = Rules.OpenTiles(s)[0];
                var u = squad[i];
                u.Pos = Beside(s, open);
                u.Ap = 2;
                u.Moves = null;
                var r = Exec(s, Move(u.Id, open.X, open.Y));
                Assert.True(r.Ok, r.Error);
                Assert.True(u.Aboard, $"{seats} seats, the {i + 1}th");
                Assert.Equal(seats - i - 1, Rules.OpenTiles(s).Count);
                Assert.Equal(Rules.SeatsFree(s), Rules.OpenTiles(s).Count);
                // Its tile shut: nobody else gets in there.
                Assert.False(Abilities.InEvacZone(s, new Pos { X = open.X, Y = open.Y, Z = 0 }));
            }
            // Down again: its tile open again.
            var first = squad[0];
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = s.Turn + 1 });
            var before = Rules.OpenTiles(s).Count;
            Assert.True(Exec(s, new AbilityCommand { Unit = first.Id, Ability = AbilityIds.Alight, Tile = Rules.AlightTiles(s)[0] }).Ok);
            Assert.Equal(before + 1, Rules.OpenTiles(s).Count);
        }

        [Fact]
        public void InAnAsedioWhoWaitsOnItsTilesGetsInWhenItComes()
        {
            // Red, 18/11 (N9-A-DOS): La Lata come, those on its tiles get in by themselves; all in, it goes.
            var (_, s) = Fight(kind: NodeKinds.Hold);
            var squad = Squaddies(s);
            var zone = Rules.OpenTiles(s);
            Assert.True(zone.Count >= squad.Count);
            for (var i = 0; i < squad.Count; i++) squad[i].Pos = new Pos { X = zone[i].X, Y = zone[i].Y, Z = 0 };
            foreach (var e in s.Units.Where((u) => u.Team == Teams.Alien)) e.Alive = false;
            s.Turn = s.Mission.HoldUntil!.Value - 1;
            var sim = NewSim(s);
            sim.RunAlienTurn();
            Assert.Equal(s.Mission.HoldUntil, s.Turn);
            Assert.All(squad, (u) => Assert.True(u.Aboard == true || u.Evacuated));
            Assert.Contains(sim.Events, (e) => e is BoardedEvent);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact]
        public void EvacuarOnlyFromInside()
        {
            // Producto, 18/11: in a run, only one in La Lata calls it, whatever the stage.
            var (_, s) = Fight();
            var squad = Squaddies(s);
            var open = Rules.OpenTiles(s)[0];
            squad[0].Pos = new Pos { X = open.X, Y = open.Y, Z = 0 };
            var r = Exec(s, new EvacCommand { Unit = squad[0].Id });
            Assert.False(r.Ok);
            Assert.Equal("Solo desde dentro de " + Lore.Bus.Name + ".", r.Error);
            Assert.True(WalkIn(s, squad[1], 1).Ok);
            var ok = Exec(s, new EvacCommand { Unit = squad[1].Id });
            Assert.True(ok.Ok, ok.Error);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact]
        public void ASurvivorNobodyTalkedToIsntLeftBehind()
        {
            // Red, 18/11: a loose survivor never talked to stays where they were when La Lata goes; nobody of the squad lost.
            var seed = 1;
            while (!RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Normal, Row = 1, Seed = seed })) seed++;
            var (_, s) = LaunchAt(NodeKinds.Normal, 1, before: (c) =>
            {
                c.Upgrades![UpgradeIds.Squad] = 1;
                c.Squad = Runs.DefaultSquad(c, c.Players);
            }, node: (n) => { n.Seed = seed; n.Site = Places.Town; });
            var loose = s.Units.Single((u) => u.Id == s.Mission.Survivor);
            Assert.True(loose.Captive);
            var squad = Squaddies(s);
            for (var i = 0; i < squad.Count; i++)
            {
                var r = WalkIn(s, squad[i], i);
                Assert.True(r.Ok, r.Error);
                if (i == squad.Count - 1) Assert.DoesNotContain(Of<LeftBehindEvent>(r.Events), (e) => e.Unit == loose.Id);
            }
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.Null(loose.Lost);
        }
    }
}
