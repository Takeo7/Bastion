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
    /// MissionBot in El herido (G23.3; Gameplay, 19/11): it walks to the wounded, takes them up from beside, carries them to
    /// La Lata with the rest along, sets them down on a tile of it (they get in), passes them on to one beside with walk
    /// left when the carrier has none, and never asks La Lata to go while they live out of it. Never refused.
    /// </summary>
    public class NetRunWoundedBotTests
    {
        private readonly ITestOutputHelper output;

        public NetRunWoundedBotTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly string[] Sites = { Places.Country, Places.Hospital, Places.Police, Places.Estate };

        /// <summary>El herido at `site` on map seed `seed`, one player's squad of four (LaunchAt), nobody of the dead left; the first soldier to play, with its actions, the rest done this turn.</summary>
        private static (GameState S, Unit U, Unit W) Alone(int seed, string site = Places.Country)
        {
            var s = LaunchAt(NodeKinds.Wounded, seed, node: (n) =>
            {
                n.Site = site;
                n.Seed = seed;
                n.Modifier = null;
            }).S;
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
            return (s, u, s.Units.Single((x) => x.Id == s.Mission.ObjectiveUnit));
        }

        /// <summary>A free tile beside `p`, on its level, reached from it as a box is searched, out of La Lata.</summary>
        private static Pos Beside(GameState s, Pos p, Unit? but = null) => Grid.Dirs8.Select((d) => new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z })
            .First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.IsWalkable(Grid.KindAt(s, q)) && Flights.CanStopAt(s, q) && (Grid.UnitAt(s, q) == null || Grid.UnitAt(s, q) == but)
                && Looting.ReachesToSearch(s, p, q) && !Abilities.InEvacZone(s, q));

        [Fact]
        public void BesideTheWoundedItTakesThemUp()
        {
            var (s, u, w) = Alone(3);
            u.Pos = Beside(s, w.Pos, u);
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Carry }, Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.Equal(w.Id, u.Carrying);
        }

        [Fact]
        public void CarryingThemBesideLaLataItSetsThemDownInIt()
        {
            var (s, u, w) = Alone(3);
            u.Pos = Beside(s, w.Pos, u);
            Assert.True(Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Carry }).Ok);
            var zone = s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null);
            u.Pos = Beside(s, zone, u);
            u.Ap = 2;
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.SetDown } down && Abilities.InEvacZone(s, down.Tile!), Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.True(w.Evacuated);
        }

        [Fact]
        public void WithNoStepLeftItPassesThemToOneBesideThatHasWalkLeft()
        {
            var (s, u, w) = Alone(3);
            u.Pos = Beside(s, w.Pos, u);
            Assert.True(Exec(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Carry }).Ok);
            u.Pos = Beside(s, u.Pos, u);
            u.Hauled = Paths.CarryMoves;
            u.Ap = 1;
            var mate = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive && x.Id != u.Id);
            mate.Pos = Beside(s, u.Pos);
            mate.Moves = null;
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Pass } pass && pass.Target == mate.Id, Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.Equal(w.Id, mate.Carrying);
        }

        [Fact]
        public void WithAllInLaLataAndThemOutOneGetsDownForThem()
        {
            // R1, 19/11 (fábrica, cuchillos, semilla 16): one got in before they were found, the rest fell, and La Lata, which
            // won't go without them and isn't a wipe with one waiting in it, held it 2900 turns.
            var (s, u, w) = Alone(3);
            foreach (var x in s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive).ToList())
            {
                Events.ApplyEvent(s, new BoardedEvent { Unit = x.Id });
                x.Ap = 2;
            }
            Assert.True(Rules.WoundedOut(s));
            var cmd = MissionBot.Next(s, 0, Zero);
            Assert.True(cmd is AbilityCommand { Ability: AbilityIds.Alight }, Json.Write(cmd));
            Assert.True(Exec(s, cmd!).Ok);
            Assert.Contains(s.Units, (x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Alive && x.Aboard != true);
        }

        /// <summary>What a fight played by both seats' bots came to.</summary>
        private sealed class Played
        {
            public bool Carried;
            public bool Safe;
            public int Passes;
            public int Refused;
            public int Turns;
            public string Outcome = "";
            public string Reason = "";
            public int Fallen;
        }

        /// <summary>
        /// El herido at `site` on map seed 100 + `seed`, two players and their squad of four (Two), with rifles or the knife,
        /// at difficulty 1, played by both seats' bots as RunBench's R1 does.
        /// </summary>
        private static Played Play(string site, bool rifles, int seed)
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
            n.Kind = NodeKinds.Wounded;
            n.Modifier = null;
            n.Seed = 100 + seed;
            n.Site = site;
            n.Difficulty = 1;
            Agree(c, new ChooseNodeCommand { Node = n.Id });
            var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
            var rng = new Rng(seed * 7919 + 13);
            var played = new Played();
            int orders = 0, idle = 0;
            while (s.Outcome == null && orders < 4000 && idle < 3)
            {
                var any = false;
                foreach (var slot in Both)
                {
                    var cmd = MissionBot.Next(s, slot, Both);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(s, cmd, slot, Both, rng).Ok) any = true;
                    else played.Refused++;
                    if (cmd is AbilityCommand { Ability: AbilityIds.Pass }) played.Passes++;
                    if (Rules.WoundedOf(s)?.CarriedBy != null) played.Carried = true;
                }
                idle = any ? 0 : idle + 1;
            }
            played.Safe = Rules.WoundedSafe(s);
            played.Turns = s.Turn;
            played.Outcome = s.Outcome ?? "-";
            played.Reason = s.OutcomeReason ?? "";
            played.Fallen = s.Units.Count((x) => x.Team == Teams.Xcom && x.CampaignId != null && !x.Alive && x.Aboard != true && !x.Evacuated);
            return played;
        }

        /// <summary>
        /// Twenty fights each way (five seeds at each of four sites), both seats' bots, said one by one: never refused, never
        /// stuck (an end always), never won without them aboard; and some won with them aboard.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheyAreCarriedToLaLata(bool rifles)
        {
            var all = new List<Played>();
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var site = Sites[(seed - 1) / 5];
                var p = Play(site, rifles, seed);
                all.Add(p);
                output.WriteLine($"{site} {seed}: carried {p.Carried}, safe {p.Safe}, passes {p.Passes}, {p.Outcome} ({p.Reason}) in turn {p.Turns}, fallen {p.Fallen}, refused {p.Refused}");
            }
            Assert.All(all, (p) => Assert.Equal(0, p.Refused));
            Assert.All(all, (p) => Assert.NotEqual("-", p.Outcome));
            Assert.All(all.Where((p) => p.Outcome == Outcomes.Victory), (p) => Assert.True(p.Safe));
            Assert.Contains(all, (p) => p.Safe && p.Outcome == Outcomes.Victory);
        }
    }
}
