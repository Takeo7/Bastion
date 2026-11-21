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
    /// MissionBot in Evacuación (G23.6; Gameplay, 21/11): the civilians are nobody's, never ordered nor counted in the squad;
    /// those still hidden don't keep La Lata waiting; one found walking to it does, short of two aboard, and the partner's call
    /// is answered no then; with two aboard it goes; the squad goes ahead of one walking, never into their way at La Lata's
    /// door; without civilians, a Saqueo. Never refused, never stuck.
    /// </summary>
    public class NetRunEvacBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunEvacBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly int[] Seats = { 2, 4, 6, 8 };

        /// <summary>Evacuación (or `kind`) at `site` on map seed 100 + `seed`, alone or `two`, `seats` soldiers with rifles, launched as a bench does it.</summary>
        private static GameState Evac(string site, int seats, int seed, bool two = false, string kind = NodeKinds.Evacuation)
        {
            var c = two ? Two(seed) : Solo(seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = System.Array.IndexOf(Seats, seats);
            var rng = new Rng(seed * 101);
            while (c.Soldiers.Count((x) => x.Alive) < seats) Runs.NewSoldier(c, c.Players[c.Soldiers.Count % c.Players.Count], rng);
            foreach (var x in c.Soldiers)
            {
                InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
            }
            c.Squad = Runs.DefaultSquad(c, c.Players);
            var run = Start(c, seed);
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = null;
            n.Seed = 100 + seed;
            n.Site = site;
            n.Difficulty = 2;
            var cmd = new ChooseNodeCommand { Node = n.Id };
            Assert.True((two ? Agree(c, cmd) : Ok(c, cmd)).Launch);
            return new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
        }

        private static List<Unit> Civilians(GameState s) => s.Mission.Civilians!.Select((c) => s.Units.First((u) => u.Id == c.Unit)).ToList();

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        private static void Board(GameState s, Unit u) => Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });

        /// <summary>A civilian found (as CivilianFoundEvent leaves them): walking to La Lata from their place.</summary>
        private static void Find(GameState s, Unit c)
        {
            var spot = s.Mission.Civilians!.First((x) => x.Unit == c.Id);
            spot.Found = true;
            spot.Area = null;
        }

        /// <summary>What a fight played by the bots of `slots` came to.</summary>
        private sealed class Played
        {
            public int Refused, Orders, ToCivilians;
        }

        private static Played Play(GameState s, int[] slots, int seed, int most = 4000)
        {
            var rng = new Rng(seed * 7919 + 13);
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
                    var unit = cmd is MoveCommand m ? m.Unit : cmd is AbilityCommand a ? a.Unit : null;
                    if (unit != null && Grid.GetUnit(s, unit) is Unit who && Rules.IsCivilian(who)) played.ToCivilians++;
                    if (Rules.ExecuteCommand(s, cmd, slot, slots, rng).Ok) any = true;
                    else played.Refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            return played;
        }

        [Fact]
        public void TheHiddenOnesKeepNoneWaitingAndTheyAreNeverOrdered()
        {
            // All of the squad in La Lata, the four civilians still hidden: it goes (counted in the squad, they kept it waiting
            // for ever). Never an order to a civilian.
            var s = Evac(Places.Town, 4, 1);
            Assert.Equal(4, Civilians(s).Count);
            foreach (var u in Squad(s)) Board(s, u);
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is EvacCommand, Json.Write(cmd));
        }

        [Fact]
        public void OneFoundWalkingShortOfTwoAboardIsWaitedFor()
        {
            // The squad in La Lata, one civilian aboard and one found and walking: no call, and the partner's answered no; with
            // the second aboard too, it goes.
            var s = Evac(Places.Town, 4, 1, two: true);
            var civ = Civilians(s);
            Board(s, civ[0]);
            Find(s, civ[1]);
            Assert.NotNull(Rules.CivilianWalk(s, civ[1]));
            // One of Pepe's still out: Ana's call is his to answer (with nobody of his out, La Lata goes at once).
            var out_ = Squad(s).First((u) => u.Owner == 1);
            foreach (var u in Squad(s).Where((u) => u != out_)) Board(s, u);
            var ana = s.Units.First((u) => u.Aboard == true && u.Owner == 0);
            Assert.True(Exec(s, new EvacCommand { Unit = ana.Id }, slot: 0, connected: Both).Ok);
            Assert.NotNull(s.EvacCall);
            var answer = MissionBot.Next(s, 1, Both);
            Assert.True(answer is EvacAnswerCommand { Accept: false }, Json.Write(answer));
            Assert.True(Exec(s, answer!, slot: 1, connected: Both).Ok);
            Assert.Null(s.Outcome);
            // All of the squad in: no call while they walk; with the second aboard too, it goes.
            Board(s, out_);
            Assert.False(MissionBot.Next(s, 0, Both) is EvacCommand);
            Assert.False(MissionBot.Next(s, 1, Both) is EvacCommand);
            Board(s, civ[1]);
            Assert.True(Rules.CiviliansSafe(s));
            var go = MissionBot.Next(s, 0, Both) ?? MissionBot.Next(s, 1, Both);
            Assert.True(go is EvacCommand, Json.Write(go));
        }

        [Fact]
        public void TheSquadGoesAheadOfOneWalkingNeverIntoTheirWayAtLaLata()
        {
            // R17 (the mall, seed 101): eight soldiers waited by La Lata's door on one civilian three tiles off, in their way,
            // ten turns. Played on, the civilian found gets in.
            var s = Evac(Places.Plaza, 8, 1);
            var p = Play(s, Zero, 1);
            output.WriteLine($"{s.Outcome} {s.OutcomeReason} turn {s.Turn}, aboard {Rules.CiviliansAboard(s)}, {p.Orders} orders");
            Assert.Equal(0, p.Refused);
            Assert.Equal(0, p.ToCivilians);
            Assert.True(Rules.CiviliansAboard(s) >= 2);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        [Fact]
        public void WithoutItsCiviliansItIsASaqueo()
        {
            // Too few buildings for them (Setup.PlaceCivilians): a Saqueo of the site, played so. (Seed 3, Gameplay 21/11: with the
            // dead kept off the civilians' buildings at the setup, seed 2's fell at turn 7 having searched only furniture.)
            var s = Evac(Places.Town, 4, 3);
            var ids = s.Mission.Civilians!.Select((c) => c.Unit).ToHashSet();
            s.Units.RemoveAll((u) => ids.Contains(u.Id));
            s.Mission.Civilians = null;
            var p = Play(s, Zero, 2);
            Assert.Equal(0, p.Refused);
            Assert.NotNull(s.Outcome);
            Assert.True((s.RoomsSearched?.Count ?? 0) > 0);
        }

        [Fact]
        public void NeverRefusedNorStuckNorOrderingACivilian()
        {
            // Its four sites, four seats alone and two a pair.
            foreach (var site in RunMap.EvacuationSites)
                foreach (var (seats, two) in new[] { (4, false), (2, true) })
                {
                    var s = Evac(site, seats, 3, two);
                    var p = Play(s, two ? Both : Zero, 3);
                    output.WriteLine($"{site} {seats}{(two ? " a dos" : "")}: {s.Outcome} {s.OutcomeReason} turn {s.Turn}, aboard {(s.Mission.Civilians != null ? Rules.CiviliansAboard(s) : -1)}, {p.Orders} orders");
                    Assert.Equal(0, p.Refused);
                    Assert.Equal(0, p.ToCivilians);
                    Assert.NotNull(s.Outcome);
                }
        }

    }
}
