using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The room with the engine's rules, end to end: each recorded game of golden/step2 played through the room by two
    /// players in memory, and beside it straight through Rules from the same state and the same random numbers. What
    /// each player hears must be what Rules says: the same events, from the right player, with the state's seq, and no
    /// gap (ServerFeed); a refused order, to whoever gave it, with its reason; and at the end, the same state.
    /// </summary>
    public class NetEngineRoomTests
    {
        /// <summary>
        /// Every kind of order (evacuating and its answer, the backpack, ending the turn, the command passed, a siege,
        /// storeys) and the longest city game. The other city games say nothing more of the room, and each takes about two
        /// seconds: the room saves the whole mission after every order (persistence.ts), as the game does.
        /// </summary>
        public static IEnumerable<object[]> Games()
        {
            return new[] { "arena-evac", "arena-hack", "arena-rescue", "arena-siege", "arena-skills", "arena-storeys", "bastion-siege", "torre", "city-3-elimination" }
                .Select((g) => new object[] { g });
        }

        /// <summary>A player's end, with what it heard read as the game would (ServerFeed).</summary>
        private sealed class Player
        {
            public readonly MemoryEnd End;
            public readonly ServerFeed Feed = new ServerFeed();
            public readonly List<string> Resyncs = new List<string>();

            public Player(MemoryEnd end)
            {
                End = end;
                Feed.Resync += Resyncs.Add;
            }

            /// <summary>What it heard since the last time.</summary>
            public List<ServerMessage> Heard()
            {
                var got = new List<ServerMessage>();
                Feed.Message += got.Add;
                while (End.Heard.TryDequeue(out var text)) Feed.Take(Json.Read<ServerMessage>(text));
                Feed.Message -= got.Add;
                return got;
            }
        }

        [Fact]
        public void ARecordedMissionIsEveryNewMission()
        {
            var rules = EngineRules.Recorded(Path.Combine(AppContext.BaseDirectory, "golden", "step2", "torre.json"));
            var first = rules.CreateMission(new SkirmishOptions(), new Rng(1));
            first.Units[0].Hp = 0;
            var second = rules.CreateMission(new SkirmishOptions(), new Rng(2));
            Assert.Equal("torre", second.MapId);
            Assert.NotEqual(0, second.Units[0].Hp);
        }

        [Fact]
        public void ASkirmishAsksSetupForItsMap()
        {
            var roadside = EngineRules.MissionOptionsOf(new SkirmishOptions { Biome = "wilds", Seed = 77, Site = "roadside", Slots = { 0, 1 }, Kind = "rescue", Veterans = true, Night = true }, new Rng(1));
            Assert.Equal((MapSourceKinds.Generated, "wilds", 77, "roadside"), (roadside.Map.Kind, roadside.Map.Biome, roadside.Map.Seed, roadside.Map.Site));
            Assert.Equal(("rescue", true, true, (int?)null), (roadside.Kind, roadside.Veterans, roadside.Night, roadside.Difficulty));
            Assert.Equal(new[] { 0, 1 }, roadside.Slots);
            var torre = EngineRules.MissionOptionsOf(new SkirmishOptions { MapKind = "handmade", Id = "torre", Slots = { 0 } }, new Rng(1));
            Assert.Equal((MapSourceKinds.Handmade, "torre"), (torre.Map.Kind, torre.Map.Id));
        }

        [Fact]
        public void TheRoomMakesNewMissionsWithSetup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "bastion-setup-" + Guid.NewGuid().ToString("N"));
            try
            {
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json")), new EngineRules(), "1.0.0", null, new FakeClock(), new Rng(5));
                var players = new List<MemoryEnd>();
                foreach (var who in new[] { "Ana", "Pepe" })
                {
                    var (player, host) = MemoryEnd.Pair();
                    room.Connect(host);
                    player.Send(JObject.FromObject(new { t = "hello", name = who, version = "1.0.0" }).ToString(Formatting.None));
                    room.Pump();
                    players.Add(player);
                }
                foreach (var map in new[] { "city", "torre" })
                {
                    if (map == "torre")
                    {
                        players[0].Send("{\"t\":\"backToMenu\"}");
                        room.Pump();
                    }
                    players[0].Send($"{{\"t\":\"start\",\"mission\":\"elimination\",\"map\":\"{map}\"}}");
                    room.Pump();
                    var snapshot = Json.Read<ServerMessage>(players[1].Take("snapshot").Single().ToString(Formatting.None));
                    var state = Assert.IsType<SnapshotMessage>(snapshot).State!;
                    Assert.Equal(map == "torre" ? "torre" : "city", map == "torre" ? state.MapId : state.Biome);
                    Assert.Equal(Teams.Xcom, state.ActiveTeam);
                    // Both commanders have soldiers of their own.
                    var owners = state.Units.Where((u) => u.Team == Teams.Xcom).Select((u) => u.Owner).Distinct().OrderBy((o) => o).ToList();
                    Assert.Equal(new int?[] { 0, 1 }, owners);
                }
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Theory]
        [MemberData(nameof(Games))]
        public void ARecordedGamePlaysTheSameThroughTheRoom(string name)
        {
            var file = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "step2", name + ".json")));
            var initial = file["state"]!.ToString(Formatting.None);
            GameState? mirror = null;
            Rng? straight = null;
            // The recorded mission instead of setup.ts; Rules beside the room starts from it and from the room's random numbers.
            var rules = new EngineRules((options, rng) =>
            {
                mirror = Json.Read<GameState>(initial);
                straight = new Rng(rng.State);
                return Json.Read<GameState>(initial);
            });
            var dir = Path.Combine(Path.GetTempPath(), "bastion-motor-" + Guid.NewGuid().ToString("N"));
            try
            {
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json")), rules, "1.0.0", null, new FakeClock(), new Rng((int)file["rngSeed"]!));
                var players = new List<Player>();
                foreach (var who in new[] { "Ana", "Pepe" })
                {
                    var (player, host) = MemoryEnd.Pair();
                    room.Connect(host);
                    player.Send(JObject.FromObject(new { t = "hello", name = who, version = "1.0.0" }).ToString(Formatting.None));
                    room.Pump();
                    players.Add(new Player(player));
                }
                players[0].End.Send("{\"t\":\"start\",\"mission\":\"elimination\",\"map\":\"city\"}");
                room.Pump();
                Assert.NotNull(mirror);
                foreach (var p in players)
                {
                    var snapshot = p.Heard().OfType<SnapshotMessage>().Single();
                    Assert.Equal(Json.Write(mirror!), Json.Write(snapshot.State!));
                }

                int commands = 0, refused = 0;
                foreach (var op in ((JArray)file["ops"]!).Where((o) => (string?)o["op"] == "command"))
                {
                    var slot = (int)op["slot"]!;
                    var cmd = op["cmd"]!;
                    players[slot].End.Send(new JObject { ["t"] = "cmd", ["cmd"] = cmd }.ToString(Formatting.None));
                    room.Pump();
                    var expected = Rules.ExecuteCommand(mirror!, cmd.ToObject<Command>(Json.Serializer)!, slot, new[] { 0, 1 }, straight!);
                    var heard = players.Select((p) => p.Heard()).ToList();
                    var where = $"{name}, order {commands} of slot {slot}: {cmd.ToString(Formatting.None)}";
                    if (!expected.Ok)
                    {
                        refused++;
                        Assert.True(expected.Error == heard[slot].OfType<RejectedMessage>().SingleOrDefault()?.Message, where);
                        Assert.All(heard, (h) => Assert.Empty(h.OfType<EventsMessage>()));
                    }
                    else
                    {
                        var events = Json.Write(expected.Events ?? new List<GameEvent>());
                        foreach (var h in heard)
                        {
                            var batch = h.OfType<EventsMessage>().SingleOrDefault();
                            Assert.True(batch != null, $"{where}: no events");
                            Assert.True(events == Json.Write(batch!.Events), $"{where}: other events");
                            Assert.Equal(slot, batch.By);
                            Assert.Equal(mirror!.Seq, batch.Seq);
                        }
                    }
                    commands++;
                }
                Assert.All(players, (p) => Assert.Empty(p.Resyncs));
                // At the end, the whole state asked for: the same as Rules'.
                players[1].End.Send("{\"t\":\"sync\"}");
                room.Pump();
                Assert.Equal(Json.Write(mirror!), Json.Write(players[1].Heard().OfType<SnapshotMessage>().Single().State!));
                Assert.True(commands > 0);
                Assert.True(refused < commands);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
