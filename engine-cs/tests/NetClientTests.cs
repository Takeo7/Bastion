using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>A player's connection to the host (Net/ClientSession.cs): the seat kept, reconnection, sleep and wake, and when it stops.</summary>
    public class NetClientTests
    {
        /// <summary>A session whose connections are in memory: `hosts` gets the host's end of each one it opens.</summary>
        private sealed class Rig
        {
            public readonly FakeClock Clock = new FakeClock();
            public readonly MemoryTokens Tokens = new MemoryTokens();
            public readonly ConcurrentQueue<MemoryEnd> Hosts = new ConcurrentQueue<MemoryEnd>();
            public readonly List<NetStatus> Statuses = new List<NetStatus>();
            public readonly List<string> Events = new List<string>();
            public readonly ClientSession Session;
            /// <summary>While true, connecting fails (nobody there).</summary>
            public volatile bool Down;
            public int Attempts;

            public Rig()
            {
                Session = new ClientSession(Connect, Tokens, "1.0.0", Clock);
                Session.Status += (s) => Statuses.Add(s);
                Session.Rejoining += (on) => Events.Add(on ? "rejoining" : "rejoined");
                Session.HostClosed += () => Events.Add("hostClosed");
                Session.Replaced += () => Events.Add("replaced");
                Session.Refused += (m) => Events.Add($"refused:{m}");
            }

            private Task<IConnection> Connect(CancellationToken cancel)
            {
                Interlocked.Increment(ref Attempts);
                if (Down) throw new TimeoutException("nadie");
                var (client, host) = MemoryEnd.Pair();
                Hosts.Enqueue(host);
                return Task.FromResult<IConnection>(client);
            }

            public MemoryEnd LastHost => Hosts.Last();

            /// <summary>Pumps until a connection more than `count` is open and its hello heard.</summary>
            public async Task<(MemoryEnd host, JObject hello)> NextHello(int count)
            {
                await Wait.Until(() => Hosts.Count > count && !Hosts.Last().Heard.IsEmpty, Session.Pump);
                var host = Hosts.Last();
                return (host, host.Take("hello").Single());
            }

            public void Welcome(MemoryEnd host, string token = "ficha-1", int slot = 1)
            {
                host.Send(new JObject { ["t"] = "welcome", ["token"] = token, ["slot"] = slot }.ToString());
                Session.Pump();
            }
        }

        [Fact]
        public async Task TheSeatIsAskedForByNameAndKeptByToken()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, hello) = await rig.NextHello(0);
            Assert.Equal("Ana", (string?)hello["name"]);
            Assert.Equal("1.0.0", (string?)hello["version"]);
            Assert.Null(hello["token"]);
            rig.Welcome(host);
            Assert.Equal("ficha-1", rig.Tokens.Token);
            Assert.Equal(1, rig.Session.Slot);
            Assert.Equal(NetStatus.Open, rig.Statuses.Last());
        }

        [Fact]
        public async Task ADroppedConnectionComesBackWithItsToken()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            host.Close("wifi");
            rig.Session.Pump();
            Assert.Equal(NetStatus.Closed, rig.Statuses.Last());
            // Not before half a second.
            rig.Clock.Advance(400);
            rig.Session.Pump();
            await Task.Delay(30);
            rig.Session.Pump();
            Assert.Single(rig.Hosts);
            rig.Clock.Advance(100);
            var (_, hello) = await rig.NextHello(1);
            Assert.Equal("ficha-1", (string?)hello["token"]);
        }

        [Fact]
        public async Task NobodyThereIsTriedAgainLessAndLessOften()
        {
            var rig = new Rig { Down = true };
            rig.Session.Connect("Ana");
            var waits = new List<long>();
            for (int tried = 1; tried <= 6; tried++)
            {
                // That attempt failed: closed, the next one waiting for its time.
                await Wait.Until(() => rig.Attempts == tried && rig.Statuses.Last() == NetStatus.Closed, rig.Session.Pump);
                // How long until the next: the clock stepped 100 ms at a time until it is connecting again.
                long waited = 0;
                while (rig.Statuses.Last() != NetStatus.Connecting)
                {
                    rig.Clock.Advance(100);
                    waited += 100;
                    rig.Session.Pump();
                }
                waits.Add(waited);
            }
            Assert.Equal(new long[] { 500, 1000, 2000, 4000, 5000, 5000 }, waits);
        }

        [Fact]
        public async Task ARefusedHelloIsNotRetriedOnItsOwn()
        {
            var rig = new Rig();
            rig.Session.Connect("Arturo");
            var (host, _) = await rig.NextHello(0);
            host.Send("{\"t\":\"error\",\"message\":\"Ese nombre ya lo usa el otro jugador: elige otro.\"}");
            rig.Session.Pump();
            Assert.Contains("refused:Ese nombre ya lo usa el otro jugador: elige otro.", rig.Events);
            // Another name: on the same socket, still open.
            rig.Session.Connect("Ana");
            var again = host.Take("hello").Single();
            Assert.Equal("Ana", (string?)again["name"]);
            Assert.Single(rig.Hosts);
        }

        [Fact]
        public async Task AFullGameClosingTheSocketIsNotRetried()
        {
            var rig = new Rig();
            rig.Session.Connect("Pepe");
            var (host, _) = await rig.NextHello(0);
            host.Send("{\"t\":\"error\",\"message\":\"La partida ya tiene dos jugadores.\"}");
            host.Close("llena");
            rig.Session.Pump();
            rig.Clock.Advance(60_000);
            rig.Session.Pump();
            await Task.Delay(30);
            rig.Session.Pump();
            Assert.Single(rig.Hosts);
        }

        [Fact]
        public async Task TheHostClosingStopsIt()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            host.Send("{\"t\":\"closing\"}");
            host.Close("cierre");
            rig.Session.Pump();
            Assert.Contains("hostClosed", rig.Events);
            rig.Clock.Advance(60_000);
            rig.Session.Pump();
            await Task.Delay(30);
            Assert.Single(rig.Hosts);
        }

        [Fact]
        public async Task ASeatOpenedElsewhereIsGivenUp()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            host.Send("{\"t\":\"replaced\"}");
            host.Close("otra ventana");
            rig.Session.Pump();
            Assert.Contains("replaced", rig.Events);
            rig.Clock.Advance(60_000);
            rig.Session.Pump();
            await Task.Delay(30);
            Assert.Single(rig.Hosts);
        }

        [Fact]
        public async Task BackFromSleepASilentSocketIsReplaced()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            rig.Session.Asleep();
            Assert.True((bool)host.Take("away").Single()["away"]!);
            rig.Clock.Advance(60_000);
            rig.Session.Awake();
            Assert.True((bool?)host.Take("away").Single()["away"] == false);
            // The host does not answer (the socket died asleep): after 2.5 s, a new one, «Volviendo a la partida…».
            rig.Clock.Advance(ClientSession.ProbeMs);
            var (second, hello) = await rig.NextHello(1);
            Assert.Contains("rejoining", rig.Events);
            Assert.Equal("ficha-1", (string?)hello["token"]);
            // The old socket is left for the host to close (a live one would show the partner a drop).
            Assert.False(host.IsClosed);
            rig.Welcome(second);
            Assert.Equal("rejoined", rig.Events.Last());
            // What the old one says now is stale: not heard.
            var heard = new List<ServerMessage>();
            rig.Session.Message += heard.Add;
            host.Send("{\"t\":\"room\",\"players\":[]}");
            rig.Session.Pump();
            Assert.Empty(heard);
        }

        [Fact]
        public async Task BackFromSleepAnAnsweringSocketStays()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            rig.Session.Asleep();
            rig.Session.Awake();
            host.Take("away");
            host.Send("{\"t\":\"room\",\"players\":[]}");
            rig.Session.Pump();
            rig.Clock.Advance(ClientSession.ProbeMs + 1000);
            rig.Session.Pump();
            await Task.Delay(30);
            rig.Session.Pump();
            Assert.Single(rig.Hosts);
            Assert.DoesNotContain("rejoining", rig.Events);
        }

        [Fact]
        public async Task APauseIsSaidAgainAfterADrop()
        {
            var rig = new Rig();
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            rig.Session.Pause(true);
            Assert.True((bool)host.Take("pause").Single()["paused"]!);
            // The host lifts it at the drop; back in with the menu still open, it is paused again.
            host.Close("wifi");
            rig.Session.Pump();
            rig.Clock.Advance(500);
            var (second, _) = await rig.NextHello(1);
            Assert.Empty(second.Take("pause"));
            rig.Welcome(second);
            Assert.True((bool)second.Take("pause").Single()["paused"]!);
            // Closed, it is not said on the next welcome.
            rig.Session.Pause(false);
            Assert.False((bool)second.Take("pause").Single()["paused"]!);
            rig.Welcome(second);
            Assert.Empty(second.Take("pause"));
        }

        [Fact]
        public async Task EveryMessageReachesTheGameInOrder()
        {
            var rig = new Rig();
            var heard = new List<string>();
            rig.Session.Message += (m) => heard.Add(m.T);
            rig.Session.Connect("Ana");
            var (host, _) = await rig.NextHello(0);
            rig.Welcome(host);
            host.Send("{\"t\":\"room\",\"players\":[]}");
            host.Send("{\"t\":\"snapshot\"}");
            host.Send("{\"t\":\"events\",\"seq\":1}");
            rig.Session.Pump();
            Assert.Equal(new[] { "welcome", "room", "snapshot", "events" }, heard);
        }

        [Fact]
        public async Task AMessageIsReadOnTheNetworkThreadNotInPump()
        {
            var readOn = new ConcurrentBag<int>();
            var tokens = new MemoryTokens();
            var hosts = new ConcurrentQueue<MemoryEnd>();
            var session = new ClientSession((cancel) =>
            {
                var (client, host) = MemoryEnd.Pair();
                hosts.Enqueue(host);
                return Task.FromResult<IConnection>(client);
            }, tokens, "1.0.0", new FakeClock(), (json) =>
            {
                readOn.Add(Environment.CurrentManagedThreadId);
                return Json.Read<ServerMessage>(json);
            });
            var heard = new List<ServerMessage>();
            var unread = new List<string>();
            session.Message += heard.Add;
            session.Unreadable += unread.Add;
            session.Connect("Ana");
            await Wait.Until(() => !hosts.IsEmpty, session.Pump);
            var hostEnd = hosts.Single();
            // The host speaks from a thread of its own, as TCP's reading does.
            var network = new Thread(() =>
            {
                hostEnd.Send("{\"t\":\"welcome\",\"token\":\"ficha\",\"slot\":0}");
                hostEnd.Send("{\"t\":\"snapshot\",\"state\":{\"seq\":7,\"turn\":2}}");
                hostEnd.Send("{\"t\":\"teletransporte\"}");
            });
            network.Start();
            network.Join();
            Assert.Equal(new[] { network.ManagedThreadId, network.ManagedThreadId, network.ManagedThreadId }, readOn);
            Assert.NotEqual(network.ManagedThreadId, Environment.CurrentManagedThreadId);
            Assert.Empty(heard);
            session.Pump();
            Assert.Equal(7, Assert.IsType<SnapshotMessage>(heard[1]).State!.Seq);
            Assert.Equal("ficha", tokens.Token);
            // One it can't read is said apart, and the session goes on.
            Assert.Contains("desconocido", unread.Single());
            Assert.Equal(2, heard.Count);
        }
    }
}
