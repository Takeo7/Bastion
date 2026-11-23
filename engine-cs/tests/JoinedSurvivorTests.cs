using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// M7 (Arturo, cuadro 140 of 23/11; gameplay/notas-23-11/M5-M7.md): the survivor who joins in a stage (a Rescate's, or one
    /// loose on the map) is one of the squad: a recruit's pack from when they join, in the debrief with what they did, home
    /// with what they carry. Fallen after joining, in the debrief as fallen; left out of La Lata, as before; every player at
    /// the soldiers' cap, the log says so. A full squad in a Rescate, as before (they take a seat).
    /// </summary>
    public class JoinedSurvivorTests
    {
        /// <summary>A stage of `kind` (seed `seed`) launched with two of the squad and four seats, alone.</summary>
        private static (CampaignState C, GameState S) Fight(string kind, int seed, Func<RunNode, bool>? fits = null)
        {
            var c = Solo(seed);
            var run = Start(c, seed);
            run.Squad = run.Squad.Take(2).ToList();
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = null;
            n.Added = null;
            n.Guarded = null;
            n.Site = Places.Town;
            n.Difficulty = 1;
            if (fits != null)
                for (var k = 1; !fits(n); k++) n.Seed = 4000 + k;
            Assert.True(Ok(c, new ChooseNodeCommand { Node = n.Id }).Launch);
            return (c, new EngineCampaignRules().LaunchMission(c, new Rng(seed + 100))!.Value.state);
        }

        /// <summary>A Saqueo with a survivor loose on the map (N2).</summary>
        private static (CampaignState C, GameState S) Loose(int seed) => Fight(NodeKinds.Normal, seed, RunMap.LooseSurvivor);

        private static Unit Survivor(GameState s) => Grid.GetUnit(s, s.Mission.Survivor!)!;

        private static Unit Join(GameState s)
        {
            Events.ApplyEvent(s, new VipRescuedEvent { Unit = s.Mission.Survivor!, Owner = 0 });
            return Survivor(s);
        }

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.CampaignId != null && u.Team == Teams.Xcom).ToList();

        private static void Board(GameState s, IEnumerable<Unit> who)
        {
            foreach (var u in who.ToList()) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
        }

        [Fact(DisplayName = "se une › the survivor waits with a recruit's pack, empty, and the knife; captive, they can't use it")]
        public void TheSurvivorWaitsWithARecruitsPack()
        {
            foreach (var (c, s) in new[] { Fight(NodeKinds.Rescue, 3), Loose(5) })
            {
                var vip = Survivor(s);
                Assert.True(vip.Captive);
                Assert.NotNull(vip.Pack);
                Assert.Equal((Runs.PackW, Runs.PackRows(c)), (vip.Pack!.W, vip.Pack.H));
                Assert.Empty(vip.Pack.Items);
                Assert.Equal(FoundWeaponIds.Knife, vip.Kit?.Melee?.Type);
                Assert.Equal("No puedes dar órdenes a esa unidad.", RunPack.Refusal(s, new ItemCommand { Unit = vip.Id, Action = ItemActions.Drop, Gear = "g1" }, 0, new[] { 0 }));
                // The partner's copy of the state, as the event of their joining plays: the same pack, shown at once.
                var client = Events.CloneState(s);
                Events.ApplyEvent(client, new VipRescuedEvent { Unit = vip.Id, Owner = 0 });
                Assert.Equal(vip.Pack.H, Survivor(client).Pack!.H);
            }
        }

        [Fact(DisplayName = "se une › found, a comrade can hand them things: into their pack")]
        public void FoundTheyAreHandedThings()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var vip = Join(s);
            var giver = Squad(s)[0];
            giver.Pos = Grid.Dirs4.Select((d) => new Pos { X = vip.Pos.X + d.X, Y = vip.Pos.Y + d.Y, Z = vip.Pos.Z })
                .First((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null);
            var gear = Gears.NewGear(c, GearKinds.Mags, null);
            Assert.True(RunPack.Put(giver.Pack!, gear, 0, 0, false));
            var cmd = new ItemCommand { Unit = giver.Id, Action = ItemActions.Give, Gear = gear.Id, To = vip.Id };
            Assert.Null(RunPack.Refusal(s, cmd, 0, new[] { 0 }));
            Assert.True(Rules.ExecuteCommand(s, cmd, 0, new[] { 0 }, new Rng(0)).Ok);
            Assert.Contains(vip.Pack!.Items, (p) => p.Gear.Id == gear.Id);
        }

        [Fact(DisplayName = "se une › what they carry comes home in their pack; the knife numbered, the pack a recruit's")]
        public void WhatTheyCarryComesHome()
        {
            foreach (var (c, s) in new[] { Fight(NodeKinds.Rescue, 3), Loose(5) })
            {
                var vip = Join(s);
                var gear = Gears.NewGear(c, GearKinds.Mags, null);
                Assert.True(RunPack.Put(vip.Pack!, gear, 0, 0, false));
                Board(s, Squad(s).Append(vip));
                var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
                var joined = c.Soldiers.Single((x) => x.Id == report.Run!.Joined);
                Assert.Contains(joined.Kit!.Pack.Items, (p) => p.Gear.Id == gear.Id);
                Assert.Equal(Runs.PackRows(c), joined.Kit.Pack.H);
                Assert.Equal(FoundWeaponIds.Knife, joined.Kit.Melee?.Type);
                Assert.False(string.IsNullOrEmpty(joined.Kit.Melee!.Id));
                Assert.Contains(joined.Id, c.Run!.Squad);
            }
        }

        [Fact(DisplayName = "se une › in the debrief as one of the squad, once: their kills and what they did since they joined")]
        public void InTheDebriefWithWhatTheyDid()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var vip = Join(s);
            vip.Kills = 2;
            vip.Deeds = new Deeds { [DeedIds.BladeKill] = 2, [DeedIds.Swing] = 3 };
            Board(s, Squad(s).Append(vip));
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            var row = Assert.Single(report.Squad, (e) => e.Id == report.Run!.Joined);
            Assert.Equal(3, report.Squad.Count);
            Assert.Equal(2, row.Kills);
            Assert.Equal(2, row.Deeds![DeedIds.BladeKill]);
            var joined = c.Soldiers.Single((x) => x.Id == row.Id);
            Assert.Equal((2, 1), (joined.Kills, joined.Missions));
            Assert.Equal(vip.Name, row.Name);
        }

        [Fact(DisplayName = "se une › fallen after joining: in the debrief as fallen, and no soldier made")]
        public void FallenAfterJoining()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var vip = Join(s);
            vip.Alive = false;
            vip.Hp = 0;
            Board(s, Squad(s));
            var soldiers = c.Soldiers.Count;
            var report = EndFight(c, s, Outcomes.Defeat, EndReasons.Abandoned);
            var row = Assert.Single(report.Squad, (e) => e.Name == vip.Name);
            Assert.Equal((ReportStatuses.Dead, ""), (row.Status, row.Id));
            Assert.Null(report.Run!.Joined);
            Assert.Equal(soldiers, c.Soldiers.Count);
            // Among the fallen, as a soldier: what they dropped and nobody took up.
            Assert.Equal("", Assert.Single(report.Run.Fallen, (f) => f.Name == vip.Name).Id);
            Assert.Contains(c.Log, (e) => e.Text == $"{vip.Name} ha caído en combate.");
        }

        [Fact(DisplayName = "se une › left out of La Lata: in the debrief as left behind, not fallen, with what they carried lost; nobody made")]
        public void LeftOutOfLaLata()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var vip = Join(s);
            var mags = Gears.NewGear(c, GearKinds.Mags, null);
            Assert.True(RunPack.Put(vip.Pack!, mags, 0, 0, false));
            Board(s, Squad(s));
            // As La Lata going leaves them (Rules: LeftBehindEvent for the squad's not aboard, the found survivor among them).
            Events.ApplyEvent(s, new LeftBehindEvent { Unit = vip.Id });
            var soldiers = c.Soldiers.Count;
            var report = EndFight(c, s, Outcomes.Defeat, EndReasons.Abandoned);
            var row = Assert.Single(report.Squad, (e) => e.Name == vip.Name);
            Assert.Equal((ReportStatuses.Lost, ""), (row.Status, row.Id));
            var fallen = Assert.Single(report.Run!.Fallen, (f) => f.Name == vip.Name);
            Assert.Equal("", fallen.Id);
            Assert.Contains(fallen.Lost, (g) => g.Id == mags.Id);
            Assert.Contains(c.Log, (e) => e.Text == $"{vip.Name} se quedó fuera cuando salió {Lore.Bus.Name}.");
            Assert.DoesNotContain(c.Log, (e) => e.Text == $"{vip.Name} ha caído en combate.");
            Assert.Null(report.Run.Joined);
            Assert.Equal(soldiers, c.Soldiers.Count);
            // Never found, they were never of the squad: nothing.
            var (d, t) = Loose(5);
            var loose = Survivor(t);
            Board(t, Squad(t));
            var quiet = EndFight(d, t, Outcomes.Victory, EndReasons.Objective);
            Assert.DoesNotContain(quiet.Squad, (e) => e.Name == loose.Name);
            Assert.DoesNotContain(quiet.Run!.Fallen, (f) => f.Name == loose.Name);
        }

        [Fact(DisplayName = "se une › every player at the soldiers' cap: they don't stay, and the log says so")]
        public void AtTheCapTheLogSaysSo()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            for (var k = 0; c.Soldiers.Count((x) => x.Alive && x.Owner == 0) < Campaign.SoldierCap(c); k++) Runs.NewSoldier(c, 0, new Rng(50 + k));
            var vip = Join(s);
            Board(s, Squad(s).Append(vip));
            var soldiers = c.Soldiers.Count;
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Null(report.Run!.Joined);
            Assert.Equal(soldiers, c.Soldiers.Count);
            Assert.Contains(c.Log, (e) => e.Text == RunTexts.SurvivorNoRoom(vip.Name));
        }

        /// <summary>A Rescate with every player at the soldiers' cap, the survivor found.</summary>
        private static (CampaignState C, GameState S, Unit Vip) AtTheCap()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            for (var k = 0; c.Soldiers.Count((x) => x.Alive && x.Owner == 0) < Campaign.SoldierCap(c); k++) Runs.NewSoldier(c, 0, new Rng(50 + k));
            return (c, s, Join(s));
        }

        [Fact(DisplayName = "se une › at the cap, what they carry goes into the squad's packs and the finds; their knife goes with them")]
        public void AtTheCapWhatTheyCarryComesHome()
        {
            var (c, s, vip) = AtTheCap();
            var knife = vip.Kit!.Melee!;
            var bat = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Bat);
            Assert.True(RunPack.Put(vip.Pack!, bat, 0, 0, false));
            Board(s, Squad(s).Append(vip));
            Assert.False(Runs.SurvivorStays(c, s));
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Null(report.Run!.Joined);
            var home = RunsHelpers.Squad(c).SelectMany((x) => x.Kit!.Pack.Items).Select((p) => p.Gear).ToList();
            Assert.Contains(home, (g) => g.Id == bat.Id);
            Assert.DoesNotContain(home, (g) => g.Id == knife.Id);
            Assert.Contains(report.Run.Found, (g) => g.Id == bat.Id);
            Assert.Equal(vip.Name, report.Run.NoBed!.Name);
            Assert.Equal(bat.Id, Assert.Single(report.Run.NoBed.Gave).Id);
            Assert.Empty(report.Run.NoBed.Lost);
        }

        [Fact(DisplayName = "se une › at the cap, carrying nothing: nothing given, nothing lost, said all the same")]
        public void AtTheCapCarryingNothing()
        {
            var (c, s, vip) = AtTheCap();
            Board(s, Squad(s).Append(vip));
            var noBed = EndFight(c, s, Outcomes.Victory, EndReasons.Objective).Run!.NoBed!;
            Assert.Equal(vip.Name, noBed.Name);
            Assert.Empty(noBed.Gave);
            Assert.Empty(noBed.Lost);
        }

        [Fact(DisplayName = "se une › at the cap, with the packs full: what finds no room is lost and said, a pile in part")]
        public void AtTheCapWhatFindsNoRoomIsSaid()
        {
            var (c, s, vip) = AtTheCap();
            var squad = Squad(s);
            foreach (var u in squad)
            {
                u.Pack!.Items.Clear();
                for (var y = 0; y < u.Pack.H; y++)
                    if (u != squad[0] || y < u.Pack.H - 1) Assert.True(RunPack.Put(u.Pack, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle), 0, y, false));
            }
            // The last row of the first: a pile of 80 § (room for 20), a pistol, a full pile of magazines.
            var last = squad[0].Pack!.H - 1;
            Assert.True(RunPack.Put(squad[0].Pack!, Gears.NewGear(c, GearKinds.Supplies, null, 80), 0, last, false));
            Assert.True(RunPack.Put(squad[0].Pack!, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol), 1, last, false));
            Assert.True(RunPack.Put(squad[0].Pack!, Gears.NewGear(c, GearKinds.Mags, null, Content.PileOf(GearKinds.Mags, null)), 3, last, false));
            var rifle = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            var supplies = Gears.NewGear(c, GearKinds.Supplies, null, 50);
            Assert.True(RunPack.Put(vip.Pack!, rifle, 0, 0, false));
            Assert.True(RunPack.Put(vip.Pack!, supplies, 0, 1, false));
            Board(s, squad.Append(vip));
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            var lost = report.Run!.NoBed!.Lost;
            Assert.Contains(lost, (g) => g.Id == rifle.Id);
            Assert.Equal(30, Assert.Single(lost, (g) => g.Id == supplies.Id).Count);
            Assert.Equal(20, Assert.Single(report.Run.Found, (g) => g.Id == supplies.Id).Count);
            Assert.Equal(20, Assert.Single(report.Run.NoBed.Gave).Count);
            Assert.DoesNotContain(report.Run.Found, (g) => g.Id == rifle.Id);
            Assert.Equal(100, RunsHelpers.Squad(c)[0].Kit!.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Supplies).Sum((p) => p.Gear.Count));
        }

        [Fact(DisplayName = "se une › whether they'll stay, before the fight is applied: the beds of the squad fallen in it count free")]
        public void WhetherTheyStay()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            Join(s);
            Assert.True(Runs.SurvivorStays(c, s));
            var (d, t, _) = AtTheCap();
            Assert.False(Runs.SurvivorStays(d, t));
            // One of the squad fallen in it: their bed is free, as ApplyFight counts it.
            var one = Squad(t)[0];
            one.Alive = false;
            one.Hp = 0;
            Assert.True(Runs.SurvivorStays(d, t));
            Board(t, Squad(t).Skip(1).Append(Survivor(t)));
            Assert.NotNull(EndFight(d, t, Outcomes.Victory, EndReasons.Objective).Run!.Joined);
        }

        /// <summary>Every gear's id in a fight: the units' places and packs, the floor.</summary>
        private static List<string> GearIds(GameState s) => s.Units.SelectMany((u) => (u.Kit != null ? new[] { u.Kit.Primary, u.Kit.Melee, u.Kit.Pistol, u.Kit.Vest }.Concat(u.Kit.Belt) : new Gear?[0])
            .Concat(u.Pack?.Items.Select((p) => p.Gear) ?? new Gear[0])).Concat((s.FloorGear ?? new List<DroppedGear>()).Select((d) => d.Gear)).Where((g) => g != null).Select((g) => g!.Id).ToList();

        [Fact(DisplayName = "se une › the survivor's knife has an id, the campaign's: not one of the fight's finds, nobody else's")]
        public void TheSurvivorsKnifeHasAnId()
        {
            foreach (var (c, s) in new[] { Fight(NodeKinds.Rescue, 3), Loose(5) })
            {
                var knife = Survivor(s).Kit!.Melee!;
                Assert.Matches("^g[0-9]+$", knife.Id);
                Assert.True(int.Parse(knife.Id.Substring(1)) <= s.Run!.FirstGear);
                Assert.Single(GearIds(s), knife.Id);
            }
        }

        [Fact(DisplayName = "se une › fallen, their knife on the floor is taken up by a comrade (Red, 23/11: with no id the room refused it)")]
        public void FallenTheirKnifeIsTakenUp()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var vip = Join(s);
            var knife = vip.Kit!.Melee!;
            Events.ApplyEvent(s, new DiedEvent { Unit = vip.Id });
            var left = Assert.Single(s.FloorGear!, (d) => d.Gear.Id == knife.Id);
            Assert.InRange(left.Gear.Id.Length, 1, 16);
            var taker = Squad(s)[0];
            taker.Pos = Grid.Dirs4.Select((d) => new Pos { X = left.Pos.X + d.X, Y = left.Pos.Y + d.Y, Z = left.Pos.Z })
                .First((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null);
            var take = new ItemCommand { Unit = taker.Id, Action = ItemActions.Take, Gear = knife.Id };
            Assert.Null(RunPack.Refusal(s, take, 0, new[] { 0 }));
            Assert.True(Rules.ExecuteCommand(s, take, 0, new[] { 0 }, new Rng(0)).Ok);
            Assert.Contains(taker.Pack!.Items, (p) => p.Gear.Id == knife.Id);
        }

        [Fact(DisplayName = "se une › a fight saved with gear that has no id (the survivor's knife, one on the floor) gets ids when loaded")]
        public void GearWithNoIdGetsOneWhenLoaded()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var knife = Survivor(s).Kit!.Melee!;
            knife.Id = "";
            var dropped = new Gear { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Knife };
            s.FloorGear = new List<DroppedGear> { new DroppedGear { Gear = dropped, Pos = Survivor(s).Pos, From = Survivor(s).Id } };
            Setup.UpToDate(s);
            Assert.Matches("^g[0-9]+$", knife.Id);
            Assert.Matches("^g[0-9]+$", dropped.Id);
            Assert.Equal(GearIds(s).Count, GearIds(s).Distinct().Count());
        }

        [Fact(DisplayName = "se une › a fight saved before (no pack on the survivor) gets the recruit's pack when loaded")]
        public void AFightOfBeforeGetsThePack()
        {
            var (c, s) = Fight(NodeKinds.Rescue, 3);
            var vip = Survivor(s);
            vip.Pack = null;
            vip.Kit = null;
            Setup.UpToDate(s);
            Assert.Equal((Runs.PackW, Runs.PackRows(c)), (vip.Pack!.W, vip.Pack.H));
            Assert.Equal(FoundWeaponIds.Knife, vip.Kit?.Melee?.Type);
            // One that has it keeps it.
            var pack = vip.Pack;
            Setup.UpToDate(s);
            Assert.Same(pack, vip.Pack);
        }
    }
}
