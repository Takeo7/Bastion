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
    /// MissionBot in Mordido (G23.2; Gameplay, 19/11): it searches the medicine cabinets and shelves till the antiviral turns
    /// up (never told which: Mission.AntiviralAt), picks it up, uses it on the bitten from beside them, and goes with them;
    /// with the count nearly out and the antiviral not in hand, it goes without them. Never refused, and the antiviral never
    /// lost (Looting.AntiviralLost).
    /// </summary>
    public class NetRunBittenBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunBittenBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>Mordido on map seed `seed`, its squad of four (4 seats); with `rifles`, each with a rifle, a pistol, a vest and magazines.</summary>
        private static GameState Bitten(int seed, bool rifles = false) => LaunchAt(NodeKinds.Bitten, seed, before: (c) =>
        {
            if (!rifles) return;
            foreach (var x in c.Soldiers)
            {
                InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                InSlot(c, x, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
                InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
            }
        }, node: (n) =>
        {
            n.Site = Places.Hospital;
            n.Seed = seed;
            n.Modifier = null;
        }).S;

        private static Unit Survivor(GameState s) => s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);

        /// <summary>
        /// `seed`'s Mordido with nobody of the dead left and one soldier to play, the first, with its actions; the rest done
        /// this turn. What the bot does next is that one's.
        /// </summary>
        private static (GameState S, Unit U) Alone(int seed)
        {
            var s = Bitten(seed);
            s.Units.RemoveAll((x) => x.Team == Teams.Alien);
            s.Pods.Clear();
            var squad = s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive).ToList();
            foreach (var x in squad.Skip(1))
            {
                x.Ap = 0;
                x.Moves = 0;
            }
            var u = squad[0];
            u.Ap = 2;
            u.Moves = null;
            return (s, u);
        }

        /// <summary>A free tile beside `p` to stand on that reaches it with the hands.</summary>
        private static Pos Beside(GameState s, Pos p, Unit u) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z })
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && (Grid.UnitAt(s, q) == null || Grid.UnitAt(s, q) == u) && Looting.ReachesToSearch(s, q, p));

        private static void IntoPack(Unit u, Gear g)
        {
            var at = Gears.FirstFit(u.Pack!, g);
            Assert.NotNull(at);
            Gears.Place(u.Pack!, g, at!.X, at.Y, at.Turned);
        }

        [Fact]
        public void BesideTheAntiviralsPieceItSearchesIt()
        {
            var (s, u) = Alone(12);
            var piece = Grid.PosOf(s, s.Mission.AntiviralAt!.Value);
            u.Pos = Beside(s, piece, u);
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Search } search && Grid.SamePos(search.Tile!, piece), Bastion.Net.Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.NotNull(Looting.AntiviralOf(u));
        }

        [Fact]
        public void WithTheAntiviralBesideTheBittenItCuresThem()
        {
            var (s, u) = Alone(12);
            var b = Survivor(s);
            Events.ApplyEvent(s, new SurvivorFoundEvent { Unit = b.Id, By = u.Id });
            var drug = s.Mission.Antiviral!;
            s.Mission.Antiviral = null;
            IntoPack(u, drug);
            u.Pos = Beside(s, b.Pos, u);
            var cmd = MissionBot.Next(s, 0, Zero);
            // Talked to first if still to be (to come along), then cured.
            if (cmd is AbilityCommand { Ability: AbilityIds.Talk })
            {
                Assert.True(Exec(s, cmd).Ok);
                cmd = MissionBot.Next(s, 0, Zero);
            }
            Assert.True(cmd is ItemCommand { Action: ItemActions.Use } use && use.To == b.Id, Bastion.Net.Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.True(s.Mission.Cured);
        }

        [Fact]
        public void ItPicksTheAntiviralUpWhereItLies()
        {
            var (s, u) = Alone(12);
            var drug = s.Mission.Antiviral!;
            s.Mission.Antiviral = null;
            s.FloorGear = new List<DroppedGear> { new DroppedGear { Gear = drug, Pos = Events.CopyPos(u.Pos) } };
            Assert.Equal(AntiviralPlaces.Floor, Looting.AntiviralWhere(s));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is ItemCommand { Action: ItemActions.Take } take && take.Gear == drug.Id, Bastion.Net.Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.NotNull(Looting.AntiviralOf(u));
        }

        /// <summary>What a fight played by both seats' bots came to.</summary>
        private sealed class Played
        {
            public string Antiviral = AntiviralPlaces.Piece;
            public bool Found;
            public bool Turned;
            public int Refused;
            public int Lost;
            public int Turns;
            public string Outcome = "";
            public int Fallen;
        }

        private static Played Play(GameState s, int seed)
        {
            var connected = new[] { 0, 1 };
            var rng = new Rng(seed * 7919 + 13);
            var played = new Played();
            var order = new[] { AntiviralPlaces.Piece, AntiviralPlaces.Floor, AntiviralPlaces.Pack, AntiviralPlaces.Used };
            int orders = 0, idle = 0;
            while (s.Outcome == null && orders < 4000 && idle < 3)
            {
                var any = false;
                foreach (var slot in connected)
                {
                    var cmd = MissionBot.Next(s, slot, connected);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(s, cmd, slot, connected, rng).Ok) any = true;
                    else played.Refused++;
                    if (Looting.AntiviralLost(s)) played.Lost++;
                    var where = Looting.AntiviralWhere(s) ?? AntiviralPlaces.Piece;
                    if (System.Array.IndexOf(order, where) > System.Array.IndexOf(order, played.Antiviral)) played.Antiviral = where;
                    if (s.Mission.BiteTurns != null) played.Found = true;
                }
                idle = any ? 0 : idle + 1;
            }
            played.Turned = Survivor(s).Reanimated;
            played.Turns = s.Turn;
            played.Outcome = s.Outcome ?? "-";
            played.Fallen = s.Units.Count((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Id != s.Mission.ObjectiveUnit && !x.Alive && x.Aboard != true && !x.Evacuated);
            return played;
        }

        /// <summary>
        /// Twenty fights each way, both seats' bots, said one by one: never refused, the antiviral never lost; with rifles,
        /// some won with the bitten cured.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItCuresTheBittenAndGoesWithThem(bool rifles)
        {
            var all = new List<Played>();
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var p = Play(Bitten(seed, rifles), seed);
                all.Add(p);
                output.WriteLine($"seed {seed}: antiviral {p.Antiviral}, found {p.Found}, turned {p.Turned}, {p.Outcome} in turn {p.Turns}, fallen {p.Fallen}, refused {p.Refused}, lost {p.Lost}");
            }
            Assert.All(all, (p) => Assert.Equal(0, p.Refused));
            Assert.All(all, (p) => Assert.Equal(0, p.Lost));
            if (rifles) Assert.Contains(all, (p) => p.Antiviral == AntiviralPlaces.Used && p.Outcome == Outcomes.Victory);
        }
    }
}
