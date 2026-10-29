using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Rules of its own for the room's tests, until the engine's are in C#: a mission of three soldiers (two of the first
    /// player's, one of the second's) and a zombie, and a few commands that do what the room needs to see.
    /// </summary>
    public sealed class FakeRules : IRules
    {
        public readonly List<SkirmishOptions> Created = new List<SkirmishOptions>();
        public readonly List<List<int>> Synced = new List<List<int>>();
        public readonly List<int> RanOut = new List<int>();
        public readonly List<(string type, int slot)> Executed = new List<(string, int)>();
        public GameState? Last;

        public GameState CreateMission(SkirmishOptions options, Rng rng)
        {
            Created.Add(options);
            Last = new GameState
            {
                MapName = "Prueba",
                Turn = 1,
                ActiveTeam = Teams.Xcom,
                Command = options.Slots[0],
                Units = { Soldier("s1", 0), Soldier("s2", 0), Soldier("s3", 1), new Unit { Id = "z1", Team = Teams.Alien, Alive = true } },
            };
            return Last;
        }

        private static Unit Soldier(string id, int owner) => new Unit { Id = id, Name = id, Team = Teams.Xcom, Owner = owner, Alive = true };

        private static List<GameEvent> Done(GameState s, GameEvent e)
        {
            s.Seq++;
            return new List<GameEvent> { e };
        }

        public CommandResult ExecuteCommand(GameState state, Command cmd, int slot, IReadOnlyList<int> connected, Rng rng)
        {
            Executed.Add((cmd.Type, slot));
            switch (cmd)
            {
                case EndTurnCommand c:
                    state.Ready[slot] = c.Ready;
                    return CommandResult.Success(Done(state, new ReadyEvent { Slot = slot, Ready = c.Ready }));
                case EvacCommand c:
                    state.EvacCall = new EvacCall { By = slot, Unit = c.Unit };
                    return CommandResult.Success(Done(state, new EvacCalledEvent { Slot = slot, Unit = c.Unit }));
                case EvacAnswerCommand _:
                    state.EvacCall = null;
                    state.Outcome = Outcomes.Victory;
                    return CommandResult.Success(Done(state, new MissionEndedEvent { Outcome = Outcomes.Victory, Reason = "evacuated" }));
                default:
                    return CommandResult.Failure("No tienes el mando.");
            }
        }

        public List<GameEvent> SyncTurn(GameState state, IReadOnlyList<int> connected, Rng rng)
        {
            Synced.Add(connected.ToList());
            if (state.Outcome != null || connected.Count == 0 || !connected.All((x) => state.Ready[x])) return new List<GameEvent>();
            state.Turn++;
            state.Ready = new bool[2];
            return Done(state, new TurnBeganEvent { Team = Teams.Xcom, Turn = state.Turn, Command = connected[0] });
        }

        public List<GameEvent> CallAbandon(GameState state, int slot, IReadOnlyList<int> connected)
        {
            if (state.AbandonCall != null) return new List<GameEvent>();
            if (connected.Count > 1)
            {
                state.AbandonCall = new AbandonCall { By = slot };
                return Done(state, new AbandonCalledEvent { Slot = slot });
            }
            state.Outcome = Outcomes.Defeat;
            return Done(state, new MissionEndedEvent { Outcome = Outcomes.Defeat, Reason = "abandoned" });
        }

        public List<GameEvent> AnswerAbandon(GameState state, int slot, bool accept)
        {
            if (state.AbandonCall == null || state.AbandonCall.By == slot) return new List<GameEvent>();
            state.AbandonCall = null;
            if (!accept) return Done(state, new AbandonRefusedEvent { Slot = slot });
            state.Outcome = Outcomes.Defeat;
            return Done(state, new MissionEndedEvent { Outcome = Outcomes.Defeat, Reason = "abandoned" });
        }

        public List<GameEvent> RunOutOfTime(GameState state, int slot, IReadOnlyList<int> connected, Rng rng)
        {
            RanOut.Add(slot);
            state.Ready[slot] = true;
            return Done(state, new ReadyEvent { Slot = slot, Ready = true });
        }
    }

    /// <summary>The host's room (Net/Room.cs): seats, what goes to whom, the grace for a drop, the waits, the turn clock and the save.</summary>
    public class NetRoomTests
    {
        private sealed class Rig : IDisposable
        {
            public readonly FakeClock Clock = new FakeClock();
            public readonly FakeRules Rules = new FakeRules();
            public readonly string Dir = Path.Combine(Path.GetTempPath(), "bastion-sala-" + Guid.NewGuid().ToString("N"));
            public readonly List<string> Log = new List<string>();
            public Room Room;
            private int peers;

            public Rig()
            {
                Room = Make();
            }

            public string SavePath => Path.Combine(Dir, "save.json");

            /// <summary>Another room on the same save, as after the host's app restarts.</summary>
            public Room Make()
            {
                var store = new SaveStore(SavePath, (line) => Log.Add(line));
                return new Room(store, Rules, "1.0.0", new[] { new HostAddress { Address = "192.168.1.20", Kind = "lan" } }, Clock, new Rng(7));
            }

            /// <summary>A player's end of a new connection to the room.</summary>
            public MemoryEnd Open()
            {
                var (player, host) = MemoryEnd.Pair("jugador", $"10.0.0.{++peers}:5000");
                Room.Connect(host);
                Room.Pump();
                return player;
            }

            public void Say(MemoryEnd player, object message)
            {
                player.Send(message is string s ? s : JObject.FromObject(message).ToString());
                Room.Pump();
            }

            public MemoryEnd Join(string name, string? token = null, string version = "1.0.0")
            {
                var player = Open();
                Say(player, new { t = "hello", name, token, version });
                return player;
            }

            /// <summary>Ana and Pepe in a skirmish, with what they heard to get there taken out.</summary>
            public (MemoryEnd ana, MemoryEnd pepe) Mission()
            {
                var ana = Join("Ana");
                var pepe = Join("Pepe");
                Say(ana, new { t = "start" });
                while (ana.Heard.TryDequeue(out _)) { }
                while (pepe.Heard.TryDequeue(out _)) { }
                return (ana, pepe);
            }

            public void Advance(long ms)
            {
                Clock.Advance(ms);
                Room.Pump();
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Dir, true);
                }
                catch (Exception)
                {
                    /* a temporary folder */
                }
            }
        }

        private static JObject Last(MemoryEnd player, string kind) => player.Take(kind).Last();

        private static List<JObject> Players(JObject room) => ((JArray)room["players"]!).Cast<JObject>().ToList();

        [Fact]
        public void TheFirstTwoPlayersGetASeatEach()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var welcome = ana.Take("welcome").Single();
            Assert.Equal(0, (int)welcome["slot"]!);
            Assert.Equal("1.0.0", (string?)welcome["version"]);
            Assert.Equal("192.168.1.20", (string?)welcome["hosts"]![0]!["address"]);
            Assert.False(string.IsNullOrEmpty((string?)welcome["token"]));
            var pepe = rig.Join("Pepe");
            Assert.Equal(1, (int)pepe.Take("welcome").Single()["slot"]!);
            var players = Players(Last(ana, "room"));
            Assert.Equal(new[] { "Ana", "Pepe" }, players.Select((p) => (string)p["name"]!));
            Assert.All(players, (p) => Assert.True((bool)p["connected"]!));
            Assert.Equal(new[] { "#a678ff", "#ffa630" }, players.Select((p) => (string)p["color"]!));
            Assert.Equal(new[] { "Ana", "Pepe" }, rig.Room.Summary().players);
        }

        [Fact]
        public void TheOtherPlayersNameIsRefusedAndTheSocketStays()
        {
            using var rig = new Rig();
            rig.Join("Ana");
            var other = rig.Join("  ana ");
            Assert.Equal("Ese nombre ya lo usa el otro jugador: elige otro.", (string?)other.Take("error").Single()["message"]);
            Assert.False(other.IsClosed);
            Assert.Empty(other.Take("welcome"));
            // Another name, on the same socket: the second seat.
            rig.Say(other, new { t = "hello", name = "Pepe", version = "1.0.0" });
            Assert.Equal(1, (int)other.Take("welcome").Single()["slot"]!);
        }

        [Fact]
        public void AThirdPlayerIsTurnedAway()
        {
            using var rig = new Rig();
            rig.Join("Ana");
            rig.Join("Pepe");
            var luis = rig.Join("Luis");
            Assert.Equal("La partida ya tiene dos jugadores.", (string?)luis.Take("error").Single()["message"]);
            Assert.True(luis.IsClosed);
        }

        [Fact]
        public void AnotherVersionIsTurnedAway()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana", version: "0.9.0");
            Assert.Equal("Quien aloja la partida tiene Bastión 1.0.0 y tú, 0.9.0: necesitáis la misma.", (string?)ana.Take("error").Single()["message"]);
            Assert.True(ana.IsClosed);
            Assert.Empty(rig.Room.Summary().players);
            // No seat was made: the next one takes the first.
            Assert.Equal(0, (int)rig.Join("Pepe").Take("welcome").Single()["slot"]!);
        }

        [Fact]
        public void TheTokenTakesTheSeatBackFromAnOldWindow()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var token = (string)ana.Take("welcome").Single()["token"]!;
            var pepe = rig.Join("Pepe");
            pepe.Take("room");
            var again = rig.Join("Ana", token);
            var welcome = again.Take("welcome").Single();
            Assert.Equal(0, (int)welcome["slot"]!);
            Assert.Equal(token, (string?)welcome["token"]);
            // The old window hears it was replaced and is closed; the partner never sees Ana gone.
            Assert.Single(ana.Take("replaced"));
            Assert.True(ana.IsClosed);
            Assert.All(pepe.Take("room"), (r) => Assert.True((bool)Players(r)[0]["connected"]!));
        }

        [Fact]
        public void AnOfflineSeatIsFoundByItsName()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var token = (string)ana.Take("welcome").Single()["token"]!;
            ana.Close("otro ordenador");
            rig.Room.Pump();
            rig.Join("Pepe");
            // From another computer, without the token: her seat, and her token.
            var welcome = rig.Join("ANA").Take("welcome").Single();
            Assert.Equal(0, (int)welcome["slot"]!);
            Assert.Equal(token, (string?)welcome["token"]);
        }

        [Fact]
        public void BothSeatsTakenAnOfflineOneIsTakenOver()
        {
            using var rig = new Rig();
            rig.Join("Ana");
            var pepe = rig.Join("Pepe");
            var token = (string)pepe.Take("welcome").Single()["token"]!;
            pepe.Close("se va");
            rig.Room.Pump();
            var luis = rig.Join("Luis");
            var welcome = luis.Take("welcome").Single();
            Assert.Equal(1, (int)welcome["slot"]!);
            Assert.NotEqual(token, (string?)welcome["token"]);
            Assert.Equal("Luis", (string?)Players(Last(luis, "room"))[1]["name"]);
        }

        [Fact]
        public void NothingButAHelloOrAReportBeforeASeat()
        {
            using var rig = new Rig();
            var someone = rig.Open();
            rig.Say(someone, new { t = "sync" });
            Assert.Equal("Identifícate primero.", (string?)someone.Take("error").Single()["message"]);
            rig.Say(someone, "{\"t\":\"hello\",\"name\":\"Un nombre demasiado largo\",\"version\":\"1.0.0\"}");
            rig.Say(someone, "no es JSON");
            rig.Say(someone, "{\"t\":\"teleport\"}");
            rig.Say(someone, "{\"t\":\"ping\",\"tile\":{\"x\":1,\"y\":1,\"z\":9},\"kind\":\"point\"}");
            Assert.Equal(4, someone.Take("error").Count((e) => (string?)e["message"] == "Mensaje no válido."));
            rig.Say(someone, new { t = "report", line = "TypeError: algo\nen otra línea" });
            rig.Say(someone, new { t = "report", line = "[rend] 60 fps, 3,1 ms" });
            Assert.Contains("[app 10.0.0.1:5000] TypeError: algo en otra línea", rig.Log);
            Assert.Contains("[rend 10.0.0.1:5000] 60 fps, 3,1 ms", rig.Log);
            Assert.Contains("conecta 10.0.0.1:5000", rig.Log);
        }

        [Fact]
        public void ASkirmishStartsForBoth()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var pepe = rig.Join("Pepe");
            rig.Say(ana, new { t = "start", mission = "rescue", map = "roadside", night = true, veterans = false });
            var options = rig.Rules.Created.Single();
            Assert.Equal(("generated", "wilds", "roadside"), (options.MapKind, options.Biome, options.Site));
            Assert.Equal(new[] { 0, 1 }, options.Slots);
            Assert.True(options.Night);
            Assert.False(options.Veterans);
            Assert.Equal("rescue", options.Kind);
            foreach (var p in new[] { ana, pepe })
            {
                Assert.Equal("mission", (string?)Last(p, "room")["phase"]);
                Assert.Equal("Prueba", (string?)p.Take("snapshot").Single()["state"]!["mapName"]);
            }
            Assert.Equal("mission", rig.Room.Summary().phase);
            var saved = JObject.Parse(File.ReadAllText(rig.SavePath));
            Assert.Equal(6, (int)saved["version"]!);
            Assert.Equal("mission", (string?)saved["phase"]);
            Assert.Equal(4, ((JArray)saved["rng"]!).Count);
            // Started again it is no new mission; over, a new one, and a Saqueo in the wilds is in town.
            rig.Say(ana, new { t = "start" });
            rig.Say(ana, new { t = "newMission", mission = "scavenge", map = "wilds" });
            Assert.Single(rig.Rules.Created);
            rig.Rules.Last!.Outcome = Outcomes.Victory;
            rig.Say(ana, new { t = "newMission", mission = "scavenge", map = "wilds" });
            Assert.Equal("city", rig.Rules.Created.Last().Biome);
            // And back to the menu.
            rig.Say(pepe, new { t = "backToMenu" });
            Assert.Equal("lobby", (string?)Last(ana, "room")["phase"]);
            ana.Take("snapshot");
            rig.Say(ana, new { t = "sync" });
            Assert.Empty(ana.Take("snapshot"));
        }

        [Fact]
        public void ACommandGoesToBothOrBackToWhoeverSentIt()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            foreach (var p in new[] { ana, pepe })
            {
                var events = p.Take("events").Single();
                Assert.Equal(0, (int)events["by"]!);
                Assert.Equal(1, (int)events["seq"]!);
                Assert.Equal("ready", (string?)events["events"]![0]!["t"]);
            }
            rig.Say(pepe, new { t = "cmd", cmd = new { type = "pass" } });
            Assert.Equal("No tienes el mando.", (string?)pepe.Take("rejected").Single()["message"]);
            Assert.Empty(ana.Take("rejected"));
            Assert.Equal(("pass", 1), rig.Rules.Executed.Last());
        }

        [Fact]
        public void AnOrderOutOfBoundsNeverReachesTheRules()
        {
            using var rig = new Rig();
            var (ana, _) = rig.Mission();
            // A storey past the fourth, an ability that doesn't exist, a backpack slot past the eighth, a unit id too long.
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"move\",\"unit\":\"s1\",\"to\":{\"x\":3,\"y\":4,\"z\":4}}}");
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"ability\",\"unit\":\"s1\",\"ability\":\"teleport\"}}");
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"ability\",\"unit\":\"s1\",\"ability\":\"grenade\",\"tile\":{\"x\":3,\"y\":4,\"z\":-1}}}");
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"item\",\"unit\":\"s1\",\"action\":\"drop\",\"index\":8}}");
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"item\",\"unit\":\"s1\",\"action\":\"throw\"}}");
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"evac\",\"unit\":\"s1234567890123456\"}}");
            Assert.Equal(6, ana.Take("error").Count((e) => (string?)e["message"] == "Mensaje no válido."));
            Assert.Empty(rig.Rules.Executed);
            // In bounds, it goes.
            rig.Say(ana, "{\"t\":\"cmd\",\"cmd\":{\"type\":\"move\",\"unit\":\"s1\",\"to\":{\"x\":3,\"y\":4,\"z\":3}}}");
            Assert.Equal(("move", 0), rig.Rules.Executed.Single());
        }

        [Fact]
        public void PresencePlansAndPingsGoWhereTheyShould()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "presence", unit = "s1", tile = new { x = 3, y = 4 } });
            Assert.Empty(ana.Take("presence"));
            var presence = pepe.Take("presence").Single();
            Assert.Equal(0, (int)presence["slot"]!);
            Assert.Equal(4, (int)presence["tile"]!["y"]!);
            // A plan for one's own soldier reaches the partner; for the partner's, nobody.
            rig.Say(ana, new { t = "plan", unit = "s2", tile = new { x = 5, y = 5 } });
            rig.Say(ana, new { t = "plan", unit = "s3", tile = new { x = 5, y = 5 } });
            rig.Say(ana, new { t = "plan", unit = "z1", tile = new { x = 5, y = 5 } });
            Assert.Equal("s2", (string?)pepe.Take("plan").Single()["unit"]);
            rig.Say(pepe, new { t = "ping", tile = new { x = 1, y = 2 }, kind = "enemy" });
            Assert.Equal(1, (int)ana.Take("ping").Single()["slot"]!);
            Assert.Single(pepe.Take("ping"));
        }

        [Fact]
        public void ADroppedPlayerHoldsTheirTurnForTheGrace()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            pepe.Close("wifi");
            rig.Room.Pump();
            Assert.False((bool)Players(Last(ana, "room"))[1]["connected"]!);
            rig.Advance(Room.ReconnectGraceMs);
            Assert.All(rig.Rules.Synced, (c) => Assert.Contains(1, c));
            // Past the grace (and a moment), the turn goes on without them.
            rig.Advance(100);
            Assert.Equal(new[] { 0 }, rig.Rules.Synced.Last());
        }

        [Fact]
        public void APlayerAwayHoldsItFiveMinutes()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(pepe, new { t = "away", away = true });
            Assert.True((bool)Players(Last(ana, "room"))[1]["away"]!);
            pepe.Take("room");
            // Said again, unchanged: only they hear the room, their proof the socket is alive.
            rig.Say(pepe, new { t = "away", away = true });
            Assert.Single(pepe.Take("room"));
            Assert.Empty(ana.Take("room"));
            pepe.Close("dormido");
            rig.Room.Pump();
            rig.Advance(Room.ReconnectGraceMs + 100);
            Assert.All(rig.Rules.Synced, (c) => Assert.Contains(1, c));
            rig.Advance(Room.AwayGraceMs - Room.ReconnectGraceMs);
            Assert.Equal(new[] { 0 }, rig.Rules.Synced.Last());
        }

        [Fact]
        public void TheTurnClockRunsForTheHolderWhileTheirScreenIsStill()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var pepe = rig.Join("Pepe");
            rig.Say(ana, new { t = "start" });
            var clocks = Last(pepe, "turnClock")["clocks"]!;
            // Two soldiers for Ana, one for Pepe.
            Assert.Equal(90_000, (long)clocks[0]!["total"]!);
            Assert.Equal(45_000, (long)clocks[1]!["total"]!);
            Assert.False((bool)clocks[0]!["running"]!);
            rig.Say(ana, new { t = "idle", idle = true });
            Assert.True((bool)Last(pepe, "turnClock")["clocks"]![0]!["running"]!);
            rig.Advance(10_000);
            // An action playing stops it.
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = false } });
            clocks = Last(ana, "turnClock")["clocks"]!;
            Assert.False((bool)clocks[0]!["running"]!);
            Assert.Equal(80_000, (long)clocks[0]!["left"]!);
            rig.Advance(30_000);
            Assert.Empty(ana.Take("turnClock"));
            // Still again, it runs out: her soldiers take cover.
            rig.Say(ana, new { t = "idle", idle = true });
            rig.Advance(80_000);
            Assert.Equal(new[] { 0 }, rig.Rules.RanOut);
            Assert.Equal(0, (long)Last(pepe, "turnClock")["clocks"]![0]!["left"]!);
            Assert.Equal("ready", (string?)Last(pepe, "events")["events"]![0]!["t"]);
        }

        [Fact]
        public void APauseHoldsBothTillTheMenuCloses()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "idle", idle = true });
            Assert.True((bool)Last(pepe, "turnClock")["clocks"]![0]!["running"]!);
            rig.Advance(10_000);
            // Pepe opens the game's menu: both hear it, and Ana's clock stops where it was.
            rig.Say(pepe, new { t = "pause", paused = true });
            foreach (var p in new[] { ana, pepe })
            {
                var players = Players(Last(p, "room"));
                Assert.Null(players[0]["paused"]);
                Assert.True((bool)players[1]["paused"]!);
            }
            var clocks = Last(ana, "turnClock")["clocks"]!;
            Assert.False((bool)clocks[0]!["running"]!);
            Assert.Equal(80_000, (long)clocks[0]!["left"]!);
            rig.Advance(100_000);
            Assert.Empty(rig.Rules.RanOut);
            // No orders from either; a ping, and the answer to a call to evacuate, still go.
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            rig.Say(pepe, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Equal("La partida está en pausa: la orden no se ha hecho.", (string?)ana.Take("rejected").Single()["message"]);
            Assert.Equal("La partida está en pausa: la orden no se ha hecho.", (string?)pepe.Take("rejected").Single()["message"]);
            Assert.Empty(rig.Rules.Executed);
            rig.Say(ana, new { t = "ping", tile = new { x = 1, y = 2 }, kind = "point" });
            Assert.Single(pepe.Take("ping"));
            rig.Say(pepe, new { t = "cmd", cmd = new { type = "evacAnswer", accept = false } });
            Assert.Equal(("evacAnswer", 1), rig.Rules.Executed.Single());
            rig.Rules.Last!.Outcome = null;
            // Said again, nothing new; closed, everything goes on.
            ana.Take("room");
            rig.Say(pepe, new { t = "pause", paused = true });
            Assert.Empty(ana.Take("room"));
            rig.Say(pepe, new { t = "pause", paused = false });
            Assert.Null(Players(Last(ana, "room"))[1]["paused"]);
            rig.Say(ana, new { t = "idle", idle = true });
            Assert.True((bool)Last(pepe, "turnClock")["clocks"]![0]!["running"]!);
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Equal(("endTurn", 0), rig.Rules.Executed.Last());
        }

        [Fact]
        public void BothPausedItWaitsForBothMenus()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "pause", paused = true });
            rig.Say(pepe, new { t = "pause", paused = true });
            rig.Say(ana, new { t = "pause", paused = false });
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Single(ana.Take("rejected"));
            rig.Say(pepe, new { t = "pause", paused = false });
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Empty(ana.Take("rejected"));
            Assert.Equal(("endTurn", 0), rig.Rules.Executed.Single());
        }

        [Fact]
        public void ADropLiftsThePause()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(pepe, new { t = "pause", paused = true });
            pepe.Close("wifi");
            rig.Room.Pump();
            var gone = Players(Last(ana, "room"))[1];
            Assert.False((bool)gone["connected"]!);
            Assert.Null(gone["paused"]);
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Empty(ana.Take("rejected"));
            Assert.Equal(("endTurn", 0), rig.Rules.Executed.Single());
        }

        [Fact]
        public void APauseIsOnlyForTheMissionUnderWay()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var pepe = rig.Join("Pepe");
            // In the lobby the menu pauses nothing.
            pepe.Take("room");
            rig.Say(ana, new { t = "pause", paused = true });
            Assert.Empty(pepe.Take("room"));
            // Left paused for the menu, the next mission starts without it.
            rig.Say(ana, new { t = "start" });
            rig.Say(ana, new { t = "pause", paused = true });
            Assert.True((bool)Players(Last(pepe, "room"))[0]["paused"]!);
            rig.Say(ana, new { t = "backToMenu" });
            Assert.Null(Players(Last(pepe, "room"))[0]["paused"]);
            rig.Say(ana, new { t = "start" });
            rig.Say(pepe, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Empty(pepe.Take("rejected"));
            // Nor over the report: the mission is over.
            rig.Rules.Last!.Outcome = Outcomes.Victory;
            pepe.Take("room");
            rig.Say(ana, new { t = "pause", paused = true });
            Assert.Empty(pepe.Take("room"));
        }

        [Fact]
        public void TimeLeftOverIsSecondsMoreNextTurn()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "idle", idle = true });
            rig.Advance(10_000);
            rig.Say(ana, new { t = "idle", idle = false });
            rig.Rules.Last!.Turn = 2;
            rig.Say(pepe, new { t = "sync" });
            var clocks = Last(ana, "turnClock")["clocks"]!;
            // 80 s left of 90: 16 s more; Pepe, 45 s untouched: 9 s more.
            Assert.Equal(90_000 + 16_000, (long)clocks[0]!["total"]!);
            Assert.Equal(45_000 + 9_000, (long)clocks[1]!["total"]!);
        }

        [Fact]
        public void AnUnansweredEvacuationGoesAheadAfterTwoMinutes()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "cmd", cmd = new { type = "evac", unit = "s1" } });
            Assert.Equal(120_000, (long)Last(pepe, "deadline")["ms"]!);
            rig.Advance(119_999);
            Assert.DoesNotContain(rig.Rules.Executed, (e) => e.type == "evacAnswer");
            rig.Advance(1);
            // Pepe's yes, as if he had said it.
            Assert.Equal(("evacAnswer", 1), rig.Rules.Executed.Last());
            var events = Last(ana, "events");
            Assert.Null(events["by"]);
            Assert.Equal("missionEnded", (string?)events["events"]![0]!["t"]);
            Assert.Null(Last(ana, "deadline")["ms"]);
        }

        [Fact]
        public void APartnerAwayHasFiveMinutesToAnswer()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(pepe, new { t = "away", away = true });
            rig.Say(ana, new { t = "cmd", cmd = new { type = "evac", unit = "s1" } });
            rig.Advance(120_000);
            Assert.Equal(180_000, (long)Last(ana, "deadline")["ms"]!);
            Assert.DoesNotContain(rig.Rules.Executed, (e) => e.type == "evacAnswer");
            // The laptop woke on another network: in again, how long is left.
            pepe.Close("dormido");
            rig.Room.Pump();
            var again = rig.Join("Pepe");
            Assert.Equal(180_000, (long)again.Take("deadline").Single()["ms"]!);
            rig.Advance(180_000);
            Assert.Equal(("evacAnswer", 1), rig.Rules.Executed.Last());
        }

        [Fact]
        public void LeavingIsAskedOfThePartnerWhoMaySayNo()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            rig.Say(ana, new { t = "abandon" });
            Assert.Equal("abandonCalled", (string?)Last(pepe, "events")["events"]![0]!["t"]);
            Assert.Equal(120_000, (long)Last(pepe, "deadline")["ms"]!);
            rig.Say(pepe, new { t = "abandonAnswer", accept = false });
            Assert.Equal("abandonRefused", (string?)Last(ana, "events")["events"]![0]!["t"]);
            Assert.Null(Last(ana, "deadline")["ms"]);
            // Unanswered, it goes ahead.
            rig.Say(ana, new { t = "abandon" });
            rig.Advance(120_000);
            Assert.Equal(Outcomes.Defeat, rig.Rules.Last!.Outcome);
        }

        [Fact]
        public void TheGameComesBackFromItsSave()
        {
            using var rig = new Rig();
            var (ana, _) = rig.Mission();
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            var token = (string)JObject.Parse(File.ReadAllText(rig.SavePath))["seats"]![0]!["token"]!;
            Assert.True(File.Exists(Path.Combine(rig.Dir, "save.anterior.json")));
            // The host's app restarts.
            rig.Room = rig.Make();
            Assert.Equal("mission", rig.Room.Summary().phase);
            Assert.Empty(rig.Room.Summary().players);
            var back = rig.Join("Ana", token);
            Assert.Equal(0, (int)back.Take("welcome").Single()["slot"]!);
            var state = back.Take("snapshot").Single()["state"]!;
            Assert.Equal(1, (int)state["seq"]!);
            Assert.True((bool)state["ready"]![0]!);
        }

        [Fact]
        public void AnUnreadableSaveIsMovedAsideNotWrittenOver()
        {
            using var rig = new Rig();
            Directory.CreateDirectory(rig.Dir);
            File.WriteAllText(rig.SavePath, "{ medio guardado");
            rig.Room = rig.Make();
            Assert.False(File.Exists(rig.SavePath));
            Assert.Equal("{ medio guardado", File.ReadAllText(Path.Combine(rig.Dir, "save.ilegible.json")));
            Assert.Contains(rig.Log, (l) => l.StartsWith("[save] No se pudo leer"));
            // Again: never over the first.
            File.WriteAllText(rig.SavePath, "tampoco");
            rig.Room = rig.Make();
            Assert.Equal("tampoco", File.ReadAllText(Path.Combine(rig.Dir, "save.ilegible-2.json")));
        }

        [Fact]
        public void ClosingSavesAndTellsEveryone()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Mission();
            var seatless = rig.Open();
            File.Delete(rig.SavePath);
            rig.Room.Closing();
            Assert.True(File.Exists(rig.SavePath));
            foreach (var p in new[] { ana, pepe })
            {
                Assert.Single(p.Take("closing"));
                Assert.True(p.IsClosed);
            }
            Assert.True(seatless.IsClosed);
        }

        [Fact]
        public async Task AHostOverTcpWelcomesAPlayersAppAndSaysWhenItCloses()
        {
            using var rig = new Rig();
            var host = new RoomHost(rig.Room, 0, IPAddress.Loopback);
            host.Start();
            var tokens = new MemoryTokens();
            var session = new ClientSession((cancel) => TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, cancel).ContinueWith((t) => (IConnection)t.Result), tokens, "1.0.0");
            var closed = false;
            session.HostClosed += () => closed = true;
            session.Connect("Ana");
            await Wait.Until(() => session.Slot == 0, session.Pump);
            Assert.NotNull(tokens.Token);
            host.Stop();
            await Wait.Until(() => closed, session.Pump);
            session.Stop();
        }
    }
}
