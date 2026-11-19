using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23.2 Mordido (Arturo, 19/11, cuadros 73 y 87; .dev-snapshots/gameplay/g23/MORDIDO.md): a Rescate at the hospital
    /// whose survivor is bitten. From their finding, Setup.BiteTurns of the squad's turns; the map's one antiviral, in one
    /// medicine cabinet or shelf (never out of reach, never in a room without a door), used on them from beside cures them;
    /// a medicine doesn't. Count out, they turn where they stand; the squad may go, with no prize. La Lata never takes them
    /// uncured, so they never turn in it. The antiviral is never lost, and never comes home.
    /// </summary>
    public class RunBittenTests
    {
        private static (CampaignState C, GameState S) Bitten(int seed = 12) => LaunchAt(NodeKinds.Bitten, node: (n) =>
        {
            n.Site = Places.Hospital;
            n.Seed = seed;
            n.Modifier = null;
        });

        private static Unit Soldier(GameState s) => s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive);

        private static Unit Survivor(GameState s) => s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);

        /// <summary>A free tile beside `p` to stand on, its level.</summary>
        private static Pos Beside(GameState s, Pos p) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z })
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null);

        private static Pos PieceOf(GameState s) => Grid.PosOf(s, s.Mission.AntiviralAt!.Value);

        /// <summary>Found and talked to: one of the squad's, its count running.</summary>
        private static Unit Found(GameState s)
        {
            var b = Survivor(s);
            Events.ApplyEvent(s, new SurvivorFoundEvent { Unit = b.Id, By = Soldier(s).Id });
            Events.ApplyEvent(s, new VipRescuedEvent { Unit = b.Id, By = Soldier(s).Id, Owner = 0 });
            return b;
        }

        /// <summary>`u` beside the antiviral's piece, searched: the antiviral in its pack.</summary>
        private static void TakeTheAntiviral(GameState s, Unit u)
        {
            var piece = PieceOf(s);
            u.Pos = Grid.Dirs8.Select((d) => new Pos { X = piece.X + d.X, Y = piece.Y + d.Y, Z = piece.Z })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && (Grid.UnitAt(s, q) == null || Grid.UnitAt(s, q) == u) && Looting.ReachesToSearch(s, q, piece));
            u.Ap = 2;
            var r = Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = piece });
            Assert.True(r.Ok, r.Error);
        }

        private static CommandResult EndTurn(GameState s) => Exec(s, new EndTurnCommand { Ready = true });

        [Fact]
        public void OnTheMapsRowsTwoToFiveAtTheHospital()
        {
            // Arturo, cuadro 90 («Como está, y lo pruebo yo»): on the maps after the 0.13.4, as the other specials.
            Assert.Contains(NodeKinds.Bitten, RunMap.Specials);
            var nodes = Enumerable.Range(1, 500).SelectMany((seed) => RunMap.Generate(1, seed)).ToList();
            var bitten = nodes.Where((n) => n.Kind == NodeKinds.Bitten).ToList();
            // Rows 2–5, one special out of ten.
            Assert.All(bitten, (n) => Assert.InRange(n.Row, 2, RunMap.NodeRows));
            double share = bitten.Count / (double)nodes.Count((n) => n.Row >= 2 && n.Row <= RunMap.NodeRows);
            Assert.InRange(share, 0.015, 0.04);
            // Always the hospital (RunMap.SpecialFor), whatever the seed; no survivor loose besides its own; a Rescate's fight.
            Assert.All(bitten, (n) => Assert.Equal(Places.Hospital, n.Site));
            Assert.All(bitten, (n) => Assert.False(RunMap.LooseSurvivor(n)));
            Assert.Equal(MissionKinds.Rescue, Runs.MissionKindOf(NodeKinds.Bitten));
            var node = new RunNode { Id = "n3-1", Row = 3, Kind = NodeKinds.Bitten, Seed = 4242, Site = Places.Town };
            Assert.Equal(Places.Hospital, RunMap.SpecialFor(node));
        }

        [Fact]
        public void TheAntiviralIsInOnePieceTheSquadGetsTo()
        {
            foreach (var seed in Enumerable.Range(1, 30))
            {
                var (_, s) = Bitten(seed);
                Assert.True(s.Mission.Bitten);
                Assert.Null(s.Mission.BiteTurns);
                Assert.Equal(GearKinds.Antiviral, s.Mission.Antiviral!.Kind);
                Assert.Equal(AntiviralPlaces.Piece, Looting.AntiviralWhere(s));
                var p = PieceOf(s);
                // A cabinet or a shelf, never a cart; one to search, standing, not out of reach.
                Assert.Contains(Looting.PieceAt(s, p), new[] { SearchPieces.MedicalCabinet, SearchPieces.MedicalShelf });
                Assert.Contains(s.Mission.AntiviralAt!.Value, s.Mission.Pieces!);
                Assert.True(SearchPieces.Standing(s, p, Looting.PieceAt(s, p)!));
                Assert.True(Looting.IsSearchable(s, p));
                Assert.DoesNotContain(s.Mission.AntiviralAt.Value, s.Mission.OutOfReach ?? new List<int>());
                // Searched from a tile the squad walks to.
                var reach = new HashSet<int>(Paths.ComputeReach(s, Soldier(s), 1_000_000, ignore: (_) => true).Cost.Keys);
                Assert.Contains(Grid.Dirs8, (d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z } is Pos q && Grid.InBounds(s, q.X, q.Y) && reach.Contains(Grid.NodeOf(s, q)) && Looting.ReachesToSearch(s, q, p));
                // In no room without a door.
                var room = s.Medical!.SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).FirstOrDefault((r) => r.Z == p.Z && p.X >= r.X && p.Y >= r.Y && p.X < r.X + r.W && p.Y < r.Y + r.H);
                if (room != null)
                    Assert.Contains(Enumerable.Range(room.Y - 1, room.H + 2).SelectMany((y) => Enumerable.Range(room.X - 1, room.W + 2).Select((x) => new Pos { X = x, Y = y, Z = p.Z })),
                        (q) => Grid.InBounds(s, q.X, q.Y) && (Grid.KindAt(s, q) == TileKinds.Door || Grid.KindAt(s, q) == TileKinds.DoorOpen));
                // The bitten, the Rescate's survivor.
                Assert.Equal(Survivor(s).Id, s.Mission.Survivor);
                Assert.False(Looting.AntiviralLost(s));
            }
        }

        [Fact]
        public void TheCountRunsFromTheirFindingOnly()
        {
            var (_, s) = Bitten();
            var b = Survivor(s);
            // Not found: no count, turn after turn.
            Assert.True(EndTurn(s).Ok);
            Assert.Null(s.Mission.BiteTurns);
            Assert.True(b.Alive);
            Events.ApplyEvent(s, new SurvivorFoundEvent { Unit = b.Id, By = Soldier(s).Id });
            Assert.Equal(Setup.BiteTurns, s.Mission.BiteTurns);
            Assert.Equal(15, Setup.BiteTurns);
            // One less a squad's turn over.
            Assert.True(EndTurn(s).Ok);
            Assert.Equal(Setup.BiteTurns - 1, s.Mission.BiteTurns);
        }

        [Fact]
        public void CountOutTheyTurnAndTheSquadGoesWithoutAPrize()
        {
            var (c, s) = Bitten();
            var b = Found(s);
            var at = Events.CopyPos(b.Pos);
            s.Mission.BiteTurns = 1;
            Assert.True(EndTurn(s).Ok);
            Assert.Equal(0, s.Mission.BiteTurns);
            Assert.False(b.Alive);
            Assert.True(b.Reanimated);
            Assert.Contains(s.Units, (z) => z.Alive && z.Team == Teams.Alien && z.Template == TemplateIds.Zombie && Grid.Distance(z.Pos, at) <= 1.5);
            // Gone with La Lata: not won (no reward, no recruit).
            Assert.False(Rules.RunsRescue(s));
            Assert.False(Rules.EvacWins(s));
            var had = c.Soldiers.Count;
            var report = EndFight(c, s, Outcomes.Defeat, EndReasons.ObjectiveLost);
            Assert.Null(report.Run!.Reward);
            Assert.Equal(had, c.Soldiers.Count);
        }

        [Fact]
        public void TheAntiviralFromItsPieceCuresThemAMedicineDoesnt()
        {
            var (_, s) = Bitten();
            var b = Found(s);
            var u = Soldier(s);
            // A medicine: wounds, not the bite.
            b.Hp = b.MaxHp;
            TakeTheAntiviral(s, u);
            Assert.NotNull(Looting.AntiviralOf(u));
            Assert.Null(s.Mission.Antiviral);
            Assert.Equal(AntiviralPlaces.Pack, Looting.AntiviralWhere(s));
            // Not from afar.
            var far = Exec(s, new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = Looting.AntiviralOf(u)!.Id, To = b.Id });
            if (!Aim.InMeleeReach(s, u.Pos, b.Pos)) Assert.False(far.Ok);
            // Beside them: cured.
            u.Pos = Beside(s, b.Pos);
            u.Ap = 2;
            var r = Exec(s, new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = Looting.AntiviralOf(u)!.Id, To = b.Id });
            Assert.True(r.Ok, r.Error);
            Assert.True(s.Mission.Cured);
            Assert.Null(Looting.AntiviralOf(u));
            Assert.Equal(AntiviralPlaces.Used, Looting.AntiviralWhere(s));
            // No count any more: never turns.
            var left = s.Mission.BiteTurns;
            s.Mission.BiteTurns = 0;
            Assert.True(EndTurn(s).Ok);
            Assert.True(b.Alive);
            Assert.False(b.Reanimated);
            Assert.False(Looting.AntiviralLost(s));
        }

        [Fact]
        public void AMedicineIsNoAntiviral()
        {
            var (c, s) = Bitten();
            var b = Found(s);
            var u = Soldier(s);
            var maker = new RunLoot.Maker(s.Run!.NextGear);
            var med = maker.Make(GearKinds.Medicine, null)[0];
            s.Run.NextGear = maker.Next;
            var fit = Gears.FirstFit(u.Pack!, med)!;
            Gears.Place(u.Pack!, med, fit.X, fit.Y, fit.Turned);
            u.Pos = Beside(s, b.Pos);
            u.Ap = 2;
            b.Hp = b.MaxHp;
            var placed = u.Pack!.Items.First((p) => p.Gear.Kind == GearKinds.Medicine);
            var refused = Exec(s, new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = placed.Gear.Id, To = b.Id });
            Assert.False(refused.Ok);
            Assert.Equal(Lore.BiteText.MedicineNo, refused.Error);
            b.Hp = b.MaxHp - 2;
            var healed = Exec(s, new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = placed.Gear.Id, To = b.Id });
            Assert.True(healed.Ok, healed.Error);
            Assert.Null(s.Mission.Cured);
            // Nor the antiviral on someone else.
            TakeTheAntiviral(s, u);
            var other = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive && x.Id != u.Id);
            other.Pos = Beside(s, u.Pos);
            u.Ap = 2;
            Assert.False(Exec(s, new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = Looting.AntiviralOf(u)!.Id, To = other.Id }).Ok);
        }

        [Fact]
        public void LaLataNeverTakesThemUncuredSoTheyNeverTurnInIt()
        {
            var (_, s) = Bitten();
            var b = Found(s);
            var z = s.Mission.EvacZone[0];
            var tile = new Pos { X = z.X, Y = z.Y, Z = 0 };
            // Uncured: no seat for them; La Lata gone, they stay behind on the field, and nothing turns in it.
            Assert.False(Rules.WouldBoard(s, b, tile));
            b.Pos = Events.CopyPos(tile);
            Assert.False(Rules.EvacWins(s));
            var u = Soldier(s);
            u.Pos = Events.CopyPos(Grid.PosOf(s, Grid.NodeOf(s, new Pos { X = s.Mission.EvacZone[1].X, Y = s.Mission.EvacZone[1].Y, Z = 0 })));
            var sim = new Sim(s, new Rng(1), new[] { 0 });
            sim.DepartEvac();
            Assert.False(b.Evacuated);
            Assert.NotEqual(true, b.Aboard);
            Assert.True(b.Lost);
            Assert.NotEqual(Outcomes.Victory, s.Outcome);
        }

        [Fact]
        public void CuredTheyBoardAndItIsWon()
        {
            var (_, s) = Bitten();
            var b = Found(s);
            s.Mission.Cured = true;
            var z = s.Mission.EvacZone[0];
            var tile = new Pos { X = z.X, Y = z.Y, Z = 0 };
            Assert.True(Rules.WouldBoard(s, b, tile));
            Events.ApplyEvent(s, new BoardedEvent { Unit = b.Id });
            Assert.True(b.Aboard);
            // Not done till La Lata goes (the Rescate's ObjectiveDone untouched: UI's «en La Lata» lines).
            Assert.False(s.Mission.ObjectiveDone);
            Assert.True(Rules.EvacWins(s));
            Assert.True(Rules.BittenSafe(s));
            new Sim(s, new Rng(1), new[] { 0 }).DepartEvac();
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact]
        public void TheSquadCanAlwaysGoLeavingThem()
        {
            // Producto, 19/11: the bitten not cured can't get in; the rest in, La Lata goes by itself, leaving them (no prize).
            var (_, s) = Bitten();
            var b = Found(s);
            foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive).ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.True(b.Alive);
            Assert.True(Rules.AllAboard(s));
            new Sim(s, new Rng(1), new[] { 0 }).CheckOutcome();
            Assert.True(s.Mission.EvacDone);
            Assert.True(b.Lost);
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            // The antiviral in a pack of theirs, the same: they go.
            var (_, s2) = Bitten();
            var b2 = Found(s2);
            var u2 = Soldier(s2);
            TakeTheAntiviral(s2, u2);
            foreach (var u in s2.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive).ToList()) Events.ApplyEvent(s2, new BoardedEvent { Unit = u.Id });
            new Sim(s2, new Rng(1), new[] { 0 }).CheckOutcome();
            Assert.Equal(Outcomes.Defeat, s2.Outcome);
            Assert.True(b2.Lost);
        }

        [Fact]
        public void TheAntiviralIsNeverLostNorHome()
        {
            var (c, s) = Bitten();
            Found(s);
            var u = Soldier(s);
            // Its piece blown to low cover: there still (SearchSpot).
            var piece = PieceOf(s);
            Assert.NotNull(Looting.SearchSpot(s, piece));
            TakeTheAntiviral(s, u);
            Assert.False(Looting.AntiviralLost(s));
            var gear = Looting.AntiviralOf(u)!;
            // Its carrier falls: on the floor with the pack (G16).
            Events.ApplyEvent(s, new DiedEvent { Unit = u.Id });
            Assert.Contains(s.FloorGear!, (d) => d.Gear.Id == gear.Id);
            Assert.Equal(AntiviralPlaces.Floor, Looting.AntiviralWhere(s));
            Assert.False(Looting.AntiviralLost(s));
            // Never home: out of every kit.
            var other = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive);
            Gears.Place(other.Pack!, Gears.CloneGear(gear), Gears.FirstFit(other.Pack!, gear)!.X, Gears.FirstFit(other.Pack!, gear)!.Y, false);
            EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.DoesNotContain(c.Soldiers.Where((x) => x.Kit != null).SelectMany((x) => x.Kit!.Pack.Items), (p) => p.Gear.Kind == GearKinds.Antiviral);
        }

        [Fact]
        public void ItsPieceBlownStillGivesIt()
        {
            var (_, s) = Bitten();
            var piece = PieceOf(s);
            var kind = Looting.PieceAt(s, piece)!;
            // A blast leaves it low (SearchPieces.Standing: a tall piece so is searched no more).
            Events.ApplyEvent(s, new TileChangedEvent { Pos = piece, Kind = Grid.KindAt(s, piece) == TileKinds.High ? TileKinds.Low : TileKinds.Floor });
            Assert.False(SearchPieces.Standing(s, piece, kind));
            Assert.Equal(kind, Looting.SearchSpot(s, piece));
            Assert.False(Looting.AntiviralLost(s));
        }

        [Fact]
        public void ARunStartedBeforeHasNone()
        {
            var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "g23", "expedicion-0132.json"));
            var c = new Bastion.Net.EngineCampaignRules().MigrateCampaign(Bastion.Net.Json.Read<CampaignState>(text));
            Assert.DoesNotContain(c.Run!.Nodes, (n) => n.Kind == NodeKinds.Bitten);
        }
    }
}
