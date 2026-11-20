using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// MissionBot in Separados (G23.4; Producto, 20/11): each group goes for La Lata from the start, searching only what it
    /// stands in or beside on the way; nobody asks La Lata to go, nor says yes to the partner's call, while the group with
    /// nobody going has one that can walk to it, past the dead too; with a group all fallen it goes as a Saqueo's; La Lata's
    /// last seat is the group still to come's. Never refused.
    /// </summary>
    public class NetRunSplitBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunSplitBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Separados at `site` on map seed `seed`, alone or `two`, the squad upgrade at `level` and as many soldiers as its
        /// seats, launched as the room does it; with none of the dead if `clear`.
        /// </summary>
        private static GameState Split(int seed = 7, string site = Places.Country, bool two = false, int level = 1, bool clear = true)
        {
            var c = two ? Two(seed) : Solo(seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = level;
            var rng = new Rng(seed);
            while (c.Soldiers.Count((x) => x.Alive) < Runs.SeatsOf(c)) Runs.NewSoldier(c, c.Players[c.Soldiers.Count % c.Players.Count], rng);
            c.Squad = Runs.DefaultSquad(c, c.Players);
            var run = Start(c);
            var n = RunMap.Choices(run)[0];
            n.Kind = NodeKinds.Split;
            n.Modifier = null;
            n.Site = site;
            n.Seed = seed;
            var cmd = new ChooseNodeCommand { Node = n.Id };
            Assert.True((two ? Agree(c, cmd) : Ok(c, cmd)).Launch);
            var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed + 100))!.Value.state;
            Assert.NotNull(s.Mission.Split);
            if (clear)
            {
                s.Units.RemoveAll((x) => x.Team == Teams.Alien);
                s.Pods.Clear();
            }
            return s;
        }

        private static List<Unit> Group(GameState s, int g) => s.Mission.Split![g].Select((id) => Grid.GetUnit(s, id)!).ToList();

        private static void Board(GameState s, Unit u) => Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });

        /// <summary>A free tile beside one of La Lata's, out of it.</summary>
        private static Pos BesideLaLata(GameState s) => s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 })
            .SelectMany((p) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = 0 }))
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null && !Abilities.InEvacZone(s, q));

        /// <summary>The bot's own reason to wait, as the test reads it: a group with nobody going, and one of it that can walk to La Lata past the dead.</summary>
        private static bool Waits(GameState s)
        {
            if (Rules.SplitShort(s) is not List<Unit> behind) return false;
            var bus = Rules.OpenTiles(s).Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            return behind.Any((o) => Paths.ComputeReach(s, o, double.MaxValue, null, (x) => x.Team != o.Team) is Reach r && bus.Any((n) => r.Cost.TryGetValue(n, out _)));
        }

        /// <summary>What a fight played by the bots of `slots` came to.</summary>
        private sealed class Played
        {
            public int Refused, Orders, GoneWaiting;
        }

        /// <summary>The bots of `slots` play `s` till it ends (or `most` orders), counting refusals and any going while a group could still come.</summary>
        private static Played Play(GameState s, int[] slots, int seed = 11, int most = 4000)
        {
            var rng = new Rng(seed);
            var played = new Played();
            var idle = 0;
            while (s.Outcome == null && played.Orders < most && idle < 3)
            {
                var any = false;
                foreach (var slot in slots)
                {
                    var cmd = MissionBot.Next(s, slot, slots);
                    if (cmd == null) continue;
                    played.Orders++;
                    if ((cmd is EvacCommand || cmd is EvacAnswerCommand { Accept: true }) && Waits(s)) played.GoneWaiting++;
                    if (Rules.ExecuteCommand(s, cmd, slot, slots, rng).Ok) any = true;
                    else played.Refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            return played;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WithNoneOfTheDeadBothGroupsGetInAndItGoesWithTheReward(bool two)
        {
            var s = Split(two: two);
            var p = Play(s, two ? Both : Zero);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.True(Rules.SplitSafe(s));
            Assert.Equal(0, p.Refused);
            Assert.Equal(0, p.GoneWaiting);
        }

        [Fact]
        public void TheFirstGroupInWaitsForTheOther()
        {
            var s = Split();
            foreach (var u in Group(s, 0)) Board(s, u);
            Assert.NotNull(Rules.SplitShort(s));
            Assert.False(MissionBot.Next(s, 0, Zero) is EvacCommand);
            var p = Play(s, Zero);
            Assert.Equal(0, p.GoneWaiting);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.All(Group(s, 1), (u) => Assert.True(u.Evacuated));
        }

        [Fact]
        public void ThePartnersCallWhileTheOtherGroupCanComeItSaysNoTo()
        {
            var s = Split(two: true);
            var ana = Group(s, 0);
            Assert.All(ana, (u) => Assert.Equal(0, u.Owner));
            foreach (var u in ana) Board(s, u);
            Assert.True(Exec(s, new EvacCommand { Unit = ana[0].Id }, slot: 0, connected: Both).Ok);
            Assert.NotNull(s.EvacCall);
            var cmd = MissionBot.Next(s, 1, Both);
            Assert.True(cmd is EvacAnswerCommand { Accept: false }, Json.Write(cmd));
            Assert.True(Exec(s, cmd!, slot: 1, connected: Both).Ok);
            Assert.Null(s.EvacCall);
            Assert.Null(s.Outcome);
        }

        [Fact]
        public void WithAGroupAllFallenItGoesAsASaqueo()
        {
            var s = Split();
            foreach (var u in Group(s, 0)) Board(s, u);
            var gone = s.Mission.Split![1].ToHashSet();
            s.Units.RemoveAll((u) => gone.Contains(u.Id));
            Assert.True(Rules.SplitLost(s));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is EvacCommand, Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.NotNull(s.Outcome);
        }

        [Fact]
        public void LaLatasLastSeatIsTheGroupStillToComes()
        {
            // Three seats, two of the first group in and the third beside La Lata, one of the second far off: the seat is
            // the second's; the third of the first stays out (without the rule it got in first and La Lata went, no reward).
            var s = Split(level: 2);
            var (a, b) = (Group(s, 0), Group(s, 1));
            Assert.Equal(3, a.Count);
            Assert.Equal(3, b.Count);
            s.Mission.Seats = 3;
            Board(s, a[0]);
            Board(s, a[1]);
            s.Units.RemoveAll((u) => u.Id == b[1].Id || u.Id == b[2].Id);
            a[2].Pos = BesideLaLata(s);
            Assert.Single(Rules.OpenTiles(s));
            var p = Play(s, Zero);
            Assert.Equal(0, p.Refused);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.True(b[0].Evacuated);
            Assert.False(a[2].Evacuated);
        }

        [Fact]
        public void HiddenItKeepsNoWatch()
        {
            // R13, 20/11 (city, 2 seats, seed 1): a lone soldier's watch at the end of its first turn shot one of the dead
            // going by, and gave both groups away.
            var s = Split(clear: false);
            var u = Group(s, 0)[0];
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.Id != u.Id))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            u.Ap = 1;
            u.Moves = 0;
            var e = s.Units.First((x) => x.Team == Teams.Alien && x.Alive);
            e.Pos = Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + 5 * d.X, Y = u.Pos.Y + 5 * d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null);
            Assert.True(s.Concealed);
            static bool Watch(Command? c) => c is AbilityCommand { Ability: AbilityIds.Overwatch or AbilityIds.OverwatchMelee };
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(Watch(cmd), Json.Write(cmd));
            // Seen, it does (with a gun, or the blade's).
            s.Concealed = false;
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(Watch(cmd), Json.Write(cmd));
        }

        /// <summary>
        /// Two fights at each of Separados' six sites, two players and their squad of four, with rifles at difficulty 2 or
        /// the knife at 1, the dead as the map has them, played by both seats' bots: never refused, never stuck, never gone
        /// while a group could still come; and some won.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NeverRefusedNorStuckNorGoneWithoutThemWhileTheyCanCome(bool rifles)
        {
            var won = 0;
            foreach (var site in RunMap.SplitSites)
                foreach (var seed in new[] { 1, 2 })
                {
                    var c = Two(seed);
                    if (rifles)
                        foreach (var x in c.Soldiers)
                        {
                            InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                            InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                            InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                            InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
                        }
                    var run = Start(c, seed);
                    var n = RunMap.Choices(run)[0];
                    n.Kind = NodeKinds.Split;
                    n.Modifier = null;
                    n.Seed = 100 + seed;
                    n.Site = site;
                    n.Difficulty = rifles ? 2 : 1;
                    Agree(c, new ChooseNodeCommand { Node = n.Id });
                    var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
                    var p = Play(s, Both, seed * 7919 + 13);
                    output.WriteLine($"{site} {seed}: {s.Outcome ?? "-"} ({s.OutcomeReason}) in turn {s.Turn}, split {s.Mission.Split != null}, safe {Rules.SplitSafe(s)}, lost {Rules.SplitLost(s)}, orders {p.Orders}, refused {p.Refused}, gone waiting {p.GoneWaiting}");
                    Assert.Equal(0, p.Refused);
                    Assert.NotNull(s.Outcome);
                    Assert.Equal(0, p.GoneWaiting);
                    if (s.Outcome == Outcomes.Victory) won++;
                }
            Assert.True(won > 0);
        }
    }
}
