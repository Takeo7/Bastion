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
    /// MissionBot in Saqueadores (G23.5; Producto, 20/11): while the camp sleeps, it goes for the stash by the tiles none of
    /// its watchers would notice it on, stops only where none of those walking in the enemies' turn would see it, takes down
    /// the guards it gets behind, searches a crate only where nobody who hears it and turns to it sees one of the squad, and
    /// shoots at nobody unaware nor where the camp would hear it; with no way in unseen, or the camp awake, it goes at it only
    /// with the guards outnumbered by RaidersEdge, and otherwise goes; without its camp, a Saqueo. Never refused, never stuck.
    /// </summary>
    public class NetRunRaidersBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunRaidersBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly int[] Seats = { 2, 4, 6, 8 };

        /// <summary>
        /// Saqueadores at `site` on map seed 100 + `seed`, alone or `two`, with `seats` soldiers, with rifles (or the knife
        /// alone), at `difficulty`: launched as the bench does it (RunBenchRaiders).
        /// </summary>
        private static GameState Raid(string site, int seats, int seed, bool two = false, bool rifles = true, int difficulty = 2)
        {
            var c = two ? Two(seed) : Solo(seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = System.Array.IndexOf(Seats, seats);
            var rng = new Rng(seed * 101);
            while (c.Soldiers.Count((x) => x.Alive) < seats) Runs.NewSoldier(c, c.Players[c.Soldiers.Count % c.Players.Count], rng);
            if (rifles)
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
            n.Kind = NodeKinds.Raiders;
            n.Modifier = null;
            n.Seed = 100 + seed;
            n.Site = site;
            n.Difficulty = difficulty;
            var cmd = new ChooseNodeCommand { Node = n.Id };
            Assert.True((two ? Agree(c, cmd) : Ok(c, cmd)).Launch);
            return new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
        }

        private static List<Unit> Guards(GameState s) => s.Units.Where((u) => u.Alive && u.Guard != null).ToList();

        private static bool Asleep(GameState s) => Guards(s).All((g) => Paths.OnPatrol(s, g));

        private static int Robbed(GameState s) => s.Mission.Raiders?.Crates.Count((i) => !Looting.IsSearchable(s, Grid.PosOf(s, i))) ?? 0;

        /// <summary>What a fight played by the bots of `slots` came to.</summary>
        private sealed class Played
        {
            public int Refused, Orders, Unseen, Takedowns, ShotsAtUnaware;
        }

        /// <summary>
        /// The bots of `slots` play `s` till it ends (or `most` orders), counting refusals, the crates searched with the squad
        /// hidden and the camp asleep, guards taken down, and attacks at one unaware while the camp sleeps.
        /// </summary>
        private static Played Play(GameState s, int[] slots, int seed, int most = 3000)
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
                    var hidden = s.Concealed && Asleep(s) && Guards(s).Count > 0;
                    var robbed = Robbed(s);
                    if (Asleep(s) && Guards(s).Count > 0 && cmd is AbilityCommand { Target: string t } a && Content.IsAttack(a.Ability) && a.Ability != AbilityIds.Takedown
                        && Grid.GetUnit(s, t) is Unit target && Paths.OnPatrol(s, target)) played.ShotsAtUnaware++;
                    var r = Rules.ExecuteCommand(s, cmd, slot, slots, rng);
                    if (r.Ok) any = true;
                    else played.Refused++;
                    if (hidden && Robbed(s) > robbed) played.Unseen += Robbed(s) - robbed;
                    played.Takedowns += (r.Events ?? new List<GameEvent>()).OfType<ShotEvent>().Count((e) => e.Ability == AbilityIds.Takedown && e.Result == ShotResults.Hit);
                }
                idle = any ? 0 : idle + 1;
            }
            return played;
        }

        [Fact]
        public void UnseenItRobsTheWholeStashTakingDownWhomItGetsBehind()
        {
            // R15 (20/11, the engine with the living unaware like the dead and those by the fire noticing only close): the
            // country's camp of seed 103, two seats; and of 105 (Gameplay, 21/11: with the dead kept off the camp's way, 102
            // robs all three unseen and then falls to the horde's trickle on its way out).
            foreach (var seed in new[] { 3, 5 })
            {
                var s = Raid(Places.Country, 2, seed);
                var p = Play(s, Zero, seed);
                output.WriteLine($"seed {seed}: {s.Outcome} turn {s.Turn}, {p.Unseen} unseen of {Robbed(s)}, {p.Takedowns} taken down");
                Assert.Equal(0, p.Refused);
                Assert.Equal(Outcomes.Victory, s.Outcome);
                Assert.Equal(3, p.Unseen);
                Assert.True(p.Takedowns > 0);
            }
        }

        [Fact]
        public void WithTheCampAsleepItShootsAtNobodyUnaware()
        {
            // Two seats against four guards: never strong enough to go at the camp by force, so no shot at anyone unaware
            // while it sleeps (R15: going so, with no step left, the squad shot the round's guard and woke the camp).
            foreach (var site in RunMap.RaidersSites)
                foreach (var seed in new[] { 1, 5 })
                {
                    var s = Raid(site, 2, seed);
                    var p = Play(s, Zero, seed);
                    Assert.Equal(0, p.Refused);
                    Assert.Equal(0, p.ShotsAtUnaware);
                }
        }

        [Fact]
        public void WithTheCampAsleepNoShotItsGuardsWouldHear()
        {
            // Gameplay, 21/11: found by the horde's trickle on its way out, the squad shot near the camp, and the guards by the
            // fire, up at the noise, saw it. One of the dead awake three tiles off, a guard within a shot's hearing: no shot
            // (the blade, if it is beside); the camp awake, it shoots.
            var s = Raid(Places.Country, 2, 3);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.Alive);
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.Id != u.Id))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            u.Moves = 0;
            bool Free(Pos q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null;
            var guard = Guards(s).First((g) => g.Guard == GuardPosts.Round);
            u.Pos = Enumerable.Range(0, s.Width * s.Height).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })
                .Where((q) => Free(q) && Free(new Pos { X = q.X + 3, Y = q.Y, Z = 0 }) && Grid.Distance(q, guard.Pos) is > 10 and < 14 && Los.CanSeeTile(s, q, new Pos { X = q.X + 3, Y = q.Y, Z = 0 }, 5))
                .First();
            var z = s.Units.First((x) => x.Alive && x.Team != Teams.Xcom && Content.Templates[x.Template].Undead && x.Guard == null);
            z.Pos = new Pos { X = u.Pos.X + 3, Y = u.Pos.Y, Z = 0 };
            s.Pods.First((p) => p.Id == z.PodId).Active = true;
            static bool Fires(Command? c, string at) => c is AbilityCommand { Ability: AbilityIds.Shoot or AbilityIds.Pistol, Target: string t } && t == at;
            Assert.True(Asleep(s));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(Fires(cmd, z.Id), Json.Write(cmd));
            foreach (var p in s.Pods.Where((p) => p.UnitIds.Any((id) => Grid.GetUnit(s, id)?.Guard != null))) p.Active = true;
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(Fires(cmd, z.Id), Json.Write(cmd));
        }

        [Fact]
        public void ACrateWhoseNoiseTurnsAGuardToTheSquadItLeavesBe()
        {
            // Five guards (difficulty 3): one stands by a crate. Whoever hears a crate searched turns to it (Events, NoiseEvent),
            // and the one beside it would see the searcher: not searched while he stands there; with him gone, it is.
            var s = Raid(Places.Country, 4, 2, difficulty: 3);
            var stash = Guards(s).First((g) => g.Guard == GuardPosts.Stash);
            var crate = s.Mission.Raiders!.Crates.Select((i) => Grid.PosOf(s, i)).OrderBy((c) => Grid.Distance(c, stash.Pos)).First();
            Assert.True(Grid.Distance(crate, stash.Pos) < 1.5);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.Alive);
            u.Pos = Grid.Dirs8.Select((d) => new Pos { X = crate.X + d.X, Y = crate.Y + d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null && Looting.ReachesToSearch(s, q, crate) && !Aim.InMeleeReach(s, q, stash.Pos));
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.Id != u.Id))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            u.Moves = 0;
            static bool Searches(Command? c, Pos at) => c is AbilityCommand { Ability: AbilityIds.Search, Tile: Pos t } && Grid.SamePos(t, at);
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(Searches(cmd, crate), Json.Write(cmd));
            s.Units.Remove(stash);
            s.Pods.RemoveAll((p) => p.Id == stash.PodId);
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(Searches(cmd, crate) || Grid.Distance(crate, u.Pos) >= 1.5, Json.Write(cmd));
        }

        [Fact]
        public void BehindAGuardItTakesHimDownButNeverTheCabecilla()
        {
            var s = Raid(Places.Country, 4, 2);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.Alive);
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.Id != u.Id))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            Pos Behind(Unit g) => Grid.Dirs8.Select((d) => new Pos { X = g.Pos.X + d.X, Y = g.Pos.Y + d.Y, Z = 0 })
                .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Grid.UnitAt(s, q) == null && Aim.InMeleeReach(s, q, g.Pos) && !Los.AheadOf(g, q));
            var trooper = Guards(s).First((g) => g.Template == TemplateIds.Lancer || (g.Template == TemplateIds.Trooper && g.Guard == GuardPosts.Fire));
            u.Pos = Behind(trooper);
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Takedown } a && a.Target == trooper.Id, Json.Write(cmd));
            var officer = Guards(s).Single((g) => g.Template == TemplateIds.Officer);
            u.Pos = Behind(officer);
            cmd = MissionBot.Next(s, 0, Zero);
            Assert.False(cmd is AbilityCommand { Ability: AbilityIds.Takedown }, Json.Write(cmd));
            Assert.False(cmd is AbilityCommand { Target: string t } b && t == officer.Id && Content.IsAttack(b.Ability), Json.Write(cmd));
        }

        [Fact]
        public void WithoutItsCampItIsASaqueo()
        {
            // No room for the camp (Setup.PlaceCamp: no camp, no guards): the stage is a Saqueo of the site, and the bot plays
            // it so, searching rooms and going.
            var s = Raid(Places.Roadside, 4, 15);
            var guards = Guards(s).Select((g) => g.Id).ToHashSet();
            s.Units.RemoveAll((u) => guards.Contains(u.Id));
            s.Pods.RemoveAll((p) => p.UnitIds.Any(guards.Contains));
            s.Mission.Raiders = null;
            var p = Play(s, Zero, 15);
            Assert.Equal(0, p.Refused);
            Assert.NotNull(s.Outcome);
            Assert.True((s.RoomsSearched?.Count ?? 0) > 0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NeverRefusedNorStuck(bool rifles)
        {
            // Its three sites, four seats alone and two seats a pair (more in R15's R1: 720 fights, none stuck).
            foreach (var site in RunMap.RaidersSites)
                foreach (var seed in new[] { 3 })
                    foreach (var (seats, two) in new[] { (4, false), (2, true) })
                    {
                        var s = Raid(site, seats, seed, two, rifles);
                        var p = Play(s, two ? Both : Zero, seed);
                        output.WriteLine($"{site} {seed} {seats}{(two ? " a dos" : "")}: {s.Outcome} {s.OutcomeReason} turn {s.Turn}, {Robbed(s)} robbed, {p.Unseen} unseen, {p.Takedowns} taken down, {p.Orders} orders");
                        Assert.Equal(0, p.Refused);
                        Assert.NotNull(s.Outcome);
                    }
        }
    }
}
