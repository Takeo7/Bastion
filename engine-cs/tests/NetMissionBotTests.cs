using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The player that plays by itself in the tests between two apps (Net/MissionBot.cs): the opening operation of a new
    /// campaign of two (golden/campaign/states.json#duo-new), launched as the room does and played by it for both seats with
    /// the engine's rules, is done: the insulin carried into La Lata, and the bus gone.
    /// </summary>
    public class NetMissionBotTests
    {
        private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "golden");

        /// <summary>The opening operation of a new campaign of two, as the room launches it.</summary>
        private static GameState Launch(int seed)
        {
            var states = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Golden, "campaign", "states.json")))["states"]!;
            var c = states.Single((s) => (string)s["name"]! == "duo-new")["campaign"]!.ToObject<CampaignState>(Json.Serializer)!;
            return new EngineCampaignRules().LaunchMission(c, new Rng(seed))!.Value.state;
        }

        /// <summary>The mission over, both seats played by MissionBot: its state, the orders given and those refused.</summary>
        private static (GameState state, int orders, int refused) Play(int seed)
        {
            var state = Launch(seed);
            var rng = new Rng(seed * 7919 + 13);
            var connected = new List<int> { 0, 1 };
            int orders = 0, refused = 0, idle = 0;
            while (state.Outcome == null && orders < 2000 && idle < 3)
            {
                var any = false;
                foreach (var slot in connected)
                {
                    var cmd = MissionBot.Next(state, slot, connected);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(state, cmd, slot, connected, rng).Ok) any = true;
                    else refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            return (state, orders, refused);
        }

        [Fact]
        public void TheOpeningOperationIsDone()
        {
            // 95 of the first 100 seeds when it was written, these eight all; with the dead at 80 % of a soldier's pace and
            // walking both actions (28/10), 85 of 100, and these eight all but the 6th and the 8th. With a recruit's aim at 55
            // and the common dead without their −10 of defence (Arturo, 31/10), 12 of the first 24 (17 before), and of these
            // eight the 7th and the 8th.
            var played = Enumerable.Range(1, 8).Select(Play).ToList();
            Assert.All(played, (p) => Assert.Equal(0, p.refused));
            var won = played.Count((p) => p.state.Outcome == Outcomes.Victory);
            Assert.True(won >= 2, $"{won} of 8: {string.Join(", ", played.Select((p) => $"{p.state.Outcome}/{p.state.OutcomeReason}"))}");
            Assert.All(played.Where((p) => p.state.Outcome == Outcomes.Victory), (p) => Assert.True(p.state.Mission.Item!.Evacuated || p.state.Units.Any((u) => u.Evacuated && u.Id == p.state.Mission.Item.Carrier)));
        }

        [Fact]
        public void ItWaitsForTheCommandAndAnswersTheBus()
        {
            var state = Launch(3);
            var both = new List<int> { 0, 1 };
            // With the command held by one, the other has nothing to say.
            state.Command = 0;
            Assert.Null(MissionBot.Next(state, 1, both));
            Assert.NotNull(MissionBot.Next(state, 0, both));
            // The partner calls La Lata: yes, whoever holds the command; one's own call waits.
            state.EvacCall = new EvacCall { By = 0, Unit = state.Units.First((u) => u.Team == Teams.Xcom).Id };
            Assert.True(Assert.IsType<EvacAnswerCommand>(MissionBot.Next(state, 1, both)).Accept);
            Assert.Null(MissionBot.Next(state, 0, both));
            // Over, nothing.
            state.EvacCall = null;
            state.Outcome = Outcomes.Victory;
            Assert.Null(MissionBot.Next(state, 0, both));
        }

        /// <summary>
        /// A city mission of `seed` (the opening operation's clinic has no boarded door) set for one soldier and one of the
        /// dead with a boarded door between (Mapgen BoardDoors), the way round longer than through it by more than a move: the
        /// first such door. The soldier has the crowbar and no gun (nothing to shoot through the planks with), and no police
        /// car to go for; the others are still.
        /// </summary>
        private static (GameState state, Unit soldier, Pos door)? Walled(int seed)
        {
            var state = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
            var u = state.Units.First((x) => x.Team == Teams.Xcom);
            var e = state.Units.First((x) => x.Team == Teams.Alien);
            for (var i = 0; i < state.Tiles.Count; i++)
            {
                var door = new Pos { X = i % state.Width, Y = i / state.Width, Z = 0 };
                if (Abilities.Boards(state, door) != DecorKinds.DoorBoards) continue;
                foreach (var d in Grid.Dirs4)
                {
                    var here = new Pos { X = door.X - d.X, Y = door.Y - d.Y, Z = 0 };
                    var there = new Pos { X = door.X + d.X, Y = door.Y + d.Y, Z = 0 };
                    if (!Grid.InBounds(state, here.X, here.Y) || !Grid.InBounds(state, there.X, there.Y)) continue;
                    if (Grid.KindAt(state, here) != TileKinds.Floor || Grid.KindAt(state, there) != TileKinds.Floor) continue;
                    if (state.Units.Any((x) => x.Alive && x != u && x != e && (Grid.SamePos(x.Pos, here) || Grid.SamePos(x.Pos, there)))) continue;
                    var round = Paths.ComputeReach(state, u, double.MaxValue, there).Cost.TryGetValue(Grid.NodeOf(state, here), out var w) ? w : int.MaxValue;
                    if (round <= 2 * Paths.CostOrtho + Paths.CostPerAp(u) + 2 * Paths.CostOrtho) continue;
                    u.Pos = here;
                    e.Pos = there;
                    foreach (var x in state.Units)
                    {
                        if (x.Team == Teams.Xcom && x != u)
                        {
                            // Still: no actions and no movement either (Arturo, 5/11 #14: it is apart from them).
                            x.Ap = 0;
                            x.Moves = 0;
                        }
                        if (x.Team == Teams.Alien && x != e) x.Alive = false;
                    }
                    u.Ap = 2;
                    u.Arms = new Arms { Melee = FoundWeaponIds.Crowbar, Pistol = false };
                    u.Backpack = new List<Loot>();
                    state.Mission.PoliceCar = null;
                    state.Dropped = null;
                    state.Command = null;
                    return (state, u, door);
                }
            }
            return null;
        }

        [Fact]
        public void ABoardedDoorInTheWayIsPriedOrGoneRound()
        {
            // Arturo, 2/11: the crowbar takes the planks off a door. With it, the bot does when the way round is longer;
            // with the knife alone, it starts round.
            var found = Enumerable.Range(1, 20).Select(Walled).FirstOrDefault((w) => w != null);
            Assert.NotNull(found);
            var (state, u, door) = found!.Value;
            var connected = new List<int> { 0 };
            var slot = 0;
            var pry = Assert.IsType<AbilityCommand>(MissionBot.Next(state, slot, connected));
            Assert.Equal(AbilityIds.Pry, pry.Ability);
            Assert.True(Grid.SamePos(door, pry.Tile!));
            var after = Json.Read<GameState>(Json.Write(state));
            Assert.True(Rules.ExecuteCommand(after, pry, slot, connected, new Rng(5)).Ok);
            Assert.Equal(TileKinds.Door, Grid.KindAt(after, door));

            u.Arms = new Arms { Pistol = false };
            var goal = state.Units.First((x) => x.Team == Teams.Alien && x.Alive).Pos;
            var back = Paths.ComputeReach(state, u, double.MaxValue, goal);
            int Walk(Pos p) => back.Cost.TryGetValue(Grid.NodeOf(state, p), out var w) ? w : int.MaxValue;
            var move = Assert.IsType<MoveCommand>(MissionBot.Next(state, slot, connected));
            Assert.True(Walk(move.To) < Walk(u.Pos), $"a step round, nearer by the walk: {Walk(move.To)} from {Walk(u.Pos)}");
        }

        [Fact]
        public void OnTheBaseTheRecruitGoesInAndTheWoundedAreWaitedFor()
        {
            // With two soldiers each (Arturo, 2/11) one may come back from a mission with nobody, and get a recruit for
            // nothing (Campaign.NobodyLeft): the bot puts that one in the squad; with nobody fit at all, it waits a day, or
            // in a siege recruits one.
            var states = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Golden, "campaign", "states.json")))["states"]!;
            var c = states.Single((s) => (string)s["name"]! == "duo-new")["campaign"]!.ToObject<CampaignState>(Json.Serializer)!;
            var rules = new EngineCampaignRules();
            var both = new List<int> { 0, 1 };
            var rng = new Rng(9);
            var heard = new HeardGame();
            CampaignCommand? Order(int slot)
            {
                heard.Observe(new CampaignMessage { State = Json.Read<CampaignState>(Json.Write(c)) });
                return heard.BaseOrder(slot, slot == 0);
            }
            Assert.NotNull(c.ActiveMission);
            var pepe = c.Soldiers.Where((s) => s.Owner == 1).ToList();
            foreach (var s in pepe.Skip(1)) s.Alive = false;
            c.Squad = c.Squad.Where((id) => !pepe.Any((s) => s.Id == id)).ToList();
            var into = Assert.IsType<SquadCommand>(Order(1));
            Assert.Equal(pepe[0].Id, into.Soldier);
            Assert.True(into.On);
            Assert.True(rules.ExecuteCampaignCommand(c, into, 1, both, rng).Ok);
            Assert.IsType<LaunchCommand>(Order(1));
            Assert.IsType<LaunchCommand>(Order(0));

            // Nobody fit: a day's wait, and both waiting it, it goes by.
            foreach (var s in c.Soldiers.Where((s) => s.Alive)) s.WoundedDays = 2;
            c.Squad = new List<string>();
            var day = c.Day;
            foreach (var slot in both) Assert.True(rules.ExecuteCampaignCommand(c, Assert.IsType<AdvanceCommand>(Order(slot)), slot, both, rng).Ok);
            Assert.Equal(day + 1, c.Day);

            // The convoy wants someone fit to send: the host takes the other answer.
            c.Event = CampaignEventIds.Convoy;
            foreach (var s in c.Soldiers.Where((s) => s.Alive)) s.WoundedDays = 2;
            var answer = Assert.IsType<AnswerEventCommand>(Order(0));
            Assert.Equal(1, answer.Option);
            Assert.True(rules.ExecuteCampaignCommand(c, answer, 0, both, rng).Ok);
            var yes = Assert.IsType<VoteCommand>(Order(1));
            Assert.True(rules.ExecuteCampaignCommand(c, yes, 1, both, rng).Ok);
            Assert.Null(c.Event);

            // A siege with nobody fit: no waiting then, so the host calls a recruit straight into the squad.
            var siege = new MissionOffer { Id = "asedio", Name = "Asedio", Kind = MissionKinds.Elimination, Map = MapSource.Handmade("bastion"), Siege = true };
            c.Offers.Add(siege);
            c.ActiveMission = siege.Id;
            c.Squad = new List<string>();
            foreach (var s in c.Soldiers.Where((s) => s.Alive)) s.WoundedDays = 2;
            Assert.Null(Order(1));
            var recruit = Assert.IsType<RecruitCommand>(Order(0));
            Assert.True(recruit.Squad);
            Assert.True(rules.ExecuteCampaignCommand(c, recruit, 0, both, rng).Ok);
            Assert.True(rules.ExecuteCampaignCommand(c, Assert.IsType<VoteCommand>(Order(1)), 1, both, rng).Ok);
            Assert.Single(c.Squad);
            Assert.IsType<LaunchCommand>(Order(0));
        }

        private static RoomMessage Room(bool pepe, bool pepeAway = false) => new RoomMessage
        {
            Phase = RoomPhases.Mission,
            Players = new List<PlayerInfo>
            {
                new PlayerInfo { Slot = 0, Name = "Ana", Connected = true },
                new PlayerInfo { Slot = 1, Name = "Pepe", Connected = pepe, Away = pepeAway ? true : (bool?)null },
            },
        };

        [Fact]
        public void ADroppedPartnerCountsAsHereTillTheHostsGrace()
        {
            var clock = new FakeClock();
            var heard = new HeardGame(clock);
            heard.Observe(Room(true));
            Assert.Equal(new[] { 0, 1 }, heard.Active());
            // Dropped: here for the host's 30 s, as Room.ActiveSlots has it, then gone.
            heard.Observe(Room(false));
            clock.Advance(Bastion.Net.Room.ReconnectGraceMs - 1);
            Assert.Equal(new[] { 0, 1 }, heard.Active());
            heard.Observe(Room(false));
            clock.Advance(1);
            Assert.Equal(new[] { 0 }, heard.Active());
            // Back, and dropped again with the game out of sight: five minutes.
            heard.Observe(Room(true));
            Assert.Equal(new[] { 0, 1 }, heard.Active());
            heard.Observe(Room(false, true));
            clock.Advance(Bastion.Net.Room.AwayGraceMs - 1);
            Assert.Equal(new[] { 0, 1 }, heard.Active());
            clock.Advance(1);
            Assert.Equal(new[] { 0 }, heard.Active());
        }

        [Fact]
        public void TheDroppedPartnersSoldiersWaitForTheGrace()
        {
            // Pepe drops: what Ana's bot asks for in the grace is all hers, and the host takes it all (with the connected
            // alone it tried Pepe's soldiers every 0.6 s, «Es de la escuadra del otro jugador.»: the apps, 29/10).
            var clock = new FakeClock();
            var heard = new HeardGame(clock);
            heard.Observe(Room(true));
            heard.Observe(Room(false));
            var state = Launch(4);
            state.Command = null;
            var rng = new Rng(17);
            var orders = new List<Command>();
            for (var i = 0; i < 40 && state.Outcome == null; i++)
            {
                var cmd = MissionBot.Next(state, 0, heard.Active());
                if (cmd == null || cmd is EndTurnCommand) break;
                orders.Add(cmd);
                var id = cmd switch { MoveCommand m => m.Unit, AbilityCommand a => a.Unit, EvacCommand e => e.Unit, ItemCommand it => it.Unit, _ => null };
                var unit = state.Units.FirstOrDefault((u) => u.Id == id);
                Assert.True(unit == null || unit.Owner != 1, $"an order for Pepe's {unit?.Id}");
                Assert.True(Rules.ExecuteCommand(state, cmd, 0, new List<int> { 0, 1 }, rng).Ok, $"refused: {Json.Write(cmd)}");
            }
            Assert.NotEmpty(orders);
            // Past the grace his soldiers are hers too, as for the host.
            clock.Advance(Bastion.Net.Room.ReconnectGraceMs);
            Assert.Equal(new[] { 0 }, heard.Active());
            Assert.Contains(state.Units, (u) => u.Team == Teams.Xcom && u.Owner == 1 && Content.CommandsFor(0, u.Owner, heard.Active()));
        }
    }
}
