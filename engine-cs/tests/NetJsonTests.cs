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
    /// The JSON of the network (Net/Json.cs) against TypeScript's: a city mission recorded with the TypeScript engine
    /// (golden/net/mision-ciudad.json), read into the C# types and written again, must say the same. Missing and null
    /// count the same (engine-cs README); anything else that differs is a field the C# types lack or have wrong.
    /// </summary>
    public class NetJsonTests
    {
        private static readonly JObject Recorded = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "net", "mision-ciudad.json")));

        /// <summary>Nulls taken out, recursively: missing and null are the same.</summary>
        private static JToken Clean(JToken token)
        {
            switch (token)
            {
                case JObject o:
                    var copy = new JObject();
                    foreach (var p in o.Properties())
                        if (p.Value.Type != JTokenType.Null) copy[p.Name] = Clean(p.Value);
                    return copy;
                case JArray a:
                    return new JArray(a.Select(Clean));
                default:
                    return token.DeepClone();
            }
        }

        /// <summary>Where `a` and `b` first differ (a JSON path and the two values), or null if they do not.</summary>
        private static string? FirstDifference(JToken a, JToken b, string path = "$")
        {
            if (a.Type != b.Type)
            {
                // A whole number written as 2.0 or 2 is the same number.
                if ((a.Type == JTokenType.Integer || a.Type == JTokenType.Float) && (b.Type == JTokenType.Integer || b.Type == JTokenType.Float) && (double)a == (double)b) return null;
                return $"{path}: TypeScript {Short(a)} · C# {Short(b)}";
            }
            switch (a)
            {
                case JObject oa:
                    var ob = (JObject)b;
                    foreach (var name in oa.Properties().Select((p) => p.Name).Union(ob.Properties().Select((p) => p.Name)))
                    {
                        if (oa[name] == null) return $"{path}.{name}: only in C# ({Short(ob[name]!)})";
                        if (ob[name] == null) return $"{path}.{name}: lost in C# (TypeScript {Short(oa[name]!)})";
                        var d = FirstDifference(oa[name]!, ob[name]!, $"{path}.{name}");
                        if (d != null) return d;
                    }
                    return null;
                case JArray aa:
                    var ab = (JArray)b;
                    if (aa.Count != ab.Count) return $"{path}: {aa.Count} items in TypeScript, {ab.Count} in C#";
                    for (int i = 0; i < aa.Count; i++)
                    {
                        var d = FirstDifference(aa[i], ab[i], $"{path}[{i}]");
                        if (d != null) return d;
                    }
                    return null;
                default:
                    return JToken.DeepEquals(a, b) ? null : $"{path}: TypeScript {Short(a)} · C# {Short(b)}";
            }
        }

        private static string Short(JToken t)
        {
            var s = t.ToString(Formatting.None);
            return s.Length > 80 ? s.Substring(0, 80) + "…" : s;
        }

        /// <summary>Read as `T` and written again: nothing may change but nulls.</summary>
        private static void SameThrough<T>(JToken typescript)
        {
            var value = typescript.ToObject<T>(Json.Serializer)!;
            var written = JToken.Parse(Json.Write(value!));
            var difference = FirstDifference(Clean(typescript), Clean(written));
            Assert.True(difference == null, difference);
        }

        [Fact]
        public void AMissionsStateComesThroughTheSame()
        {
            SameThrough<GameState>(Recorded["state"]!);
        }

        [Fact]
        public void AStateAfterSomeTurnsComesThroughTheSame()
        {
            SameThrough<GameState>(Recorded["after"]!);
        }

        [Fact]
        public void EveryEventComesThroughTheSame()
        {
            var events = (JArray)Recorded["events"]!;
            Assert.NotEmpty(events);
            foreach (var e in events)
            {
                var read = e.ToObject<GameEvent>(Json.Serializer)!;
                Assert.Equal((string)e["t"]!, read.T);
                SameThrough<GameEvent>(e);
            }
        }

        [Fact]
        public void CommandsComeThroughTheSame()
        {
            foreach (var c in ((JArray)Recorded["commands"]!).Select((x) => x["cmd"]!)) SameThrough<Command>(c);
            var move = Json.Read<Command>("{\"type\":\"move\",\"unit\":\"s1\",\"to\":{\"x\":3,\"y\":4}}");
            var m = Assert.IsType<MoveCommand>(move);
            // A place without `z` is on the ground (grid.ts toPos).
            Assert.Equal(0, m.To.Z);
            Assert.Equal(4, m.To.Y);
        }

        [Fact]
        public void EveryEventClassHasItsOwnTag()
        {
            // 76 variants of GameEvent (engine-cs Types/GameEvent.cs), each told by its `t`.
            var converter = new TagConverter<GameEvent>("t", (e) => e.T);
            Assert.Equal(76, converter.Tags.Count);
        }

        [Fact]
        public void AnUnknownTagIsRefused()
        {
            Assert.Throws<JsonSerializationException>(() => Json.Read<GameEvent>("{\"t\":\"teleported\",\"unit\":\"s1\"}"));
            Assert.Throws<JsonSerializationException>(() => Json.Read<Command>("{\"unit\":\"s1\"}"));
            Assert.Throws<JsonSerializationException>(() => Json.Read<ClientMessage>("{\"t\":\"hack\"}"));
        }

        [Fact]
        public void MessagesCarryTheirTagAndLeaveNullsOut()
        {
            var json = Json.Write(new PresenceMessage { Unit = null, Tile = new Pos { X = 2, Y = 5 } });
            Assert.True(JToken.DeepEquals(JObject.Parse("{\"t\":\"presence\",\"tile\":{\"x\":2,\"y\":5,\"z\":0}}"), JObject.Parse(json)), json);
            var back = Assert.IsType<PresenceMessage>(Json.Read<ClientMessage>(json));
            Assert.Null(back.Unit);
            Assert.Equal(5, back.Tile!.Y);
        }

        [Fact]
        public void ACommandTravelsInsideItsMessage()
        {
            var msg = Assert.IsType<CmdMessage>(Json.Read<ClientMessage>("{\"t\":\"cmd\",\"cmd\":{\"type\":\"ability\",\"unit\":\"s2\",\"ability\":\"shoot\",\"target\":\"z7\"}}"));
            var ability = Assert.IsType<AbilityCommand>(msg.Cmd);
            Assert.Equal("shoot", ability.Ability);
            Assert.Equal("z7", ability.Target);
            Assert.Null(ability.Tile);
        }

        [Fact]
        public void TheHostsMessagesComeBack()
        {
            var state = Recorded["state"]!.ToObject<GameState>(Json.Serializer)!;
            var events = ((JArray)Recorded["events"]!).Take(5).Select((e) => e.ToObject<GameEvent>(Json.Serializer)!).ToList();
            var sent = new ServerMessage[]
            {
                new WelcomeMessage { Token = "ficha", Slot = 1, Version = "1.0.0", Hosts = new List<HostAddress> { new HostAddress { Address = "192.168.1.20", Kind = "lan" } } },
                new RoomMessage { Players = new List<PlayerInfo> { new PlayerInfo { Slot = 0, Name = "Arturo", Color = "#a678ff", Connected = true } }, Phase = "mission" },
                new SnapshotMessage { State = state },
                new EventsMessage { Events = events, Seq = 9, By = null },
                new TurnClockMessage { Clocks = new List<TurnClock> { new TurnClock { Left = 1000, Total = 90_000, Running = true }, new TurnClock { Left = 0, Total = 0 } } },
                new DeadlineMessage { Kind = "evac", Ms = null },
                new ClosingMessage(),
                new ReplacedMessage(),
            };
            foreach (var m in sent)
            {
                var json = Json.Write(m);
                var back = Json.Read<ServerMessage>(json);
                Assert.Equal(m.GetType(), back.GetType());
                Assert.Equal(json, Json.Write(back));
            }
            Assert.DoesNotContain("\"by\"", Json.Write(sent[3]));
            Assert.Contains("\"hosts\":[{\"address\":\"192.168.1.20\",\"kind\":\"lan\"}]", Json.Write(sent[0]));
        }
    }
}
