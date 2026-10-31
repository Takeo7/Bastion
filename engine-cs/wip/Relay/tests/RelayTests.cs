using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Bastion.Relay;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>«Unirse con código» (docs/UNIRSE-CON-CODIGO.md): the relay (server/) and the apps' side of it (net/Relay*.cs), on 127.0.0.1.</summary>
    public class RelayTests
    {
        private static RelayOptions Options(Action<RelayOptions>? change = null)
        {
            var o = new RelayOptions { Address = IPAddress.Loopback, Port = 0, RestartGraceMs = 0 };
            change?.Invoke(o);
            return o;
        }

        private static RelayServer Start(RelayOptions o)
        {
            var relay = new RelayServer(o, (_) => { });
            relay.Start();
            return relay;
        }

        private static RelayAddress At(RelayServer relay)
        {
            return new RelayAddress("127.0.0.1", relay.Port);
        }

        /// <summary>A host's link, started, once it has a code.</summary>
        private static async Task<RelayHost> Link(RelayAddress at, Action<TcpConnection>? connected = null)
        {
            var link = new RelayHost(at);
            if (connected != null) link.Connected += connected;
            link.Start();
            await Wait.Until(() => link.State == RelayState.Ready);
            return link;
        }

        /// <summary>The host's end of each pipe answers what it hears, «eco:» first.</summary>
        private static void Echo(TcpConnection c)
        {
            c.Message += (m) => c.Send("eco:" + m);
        }

        private static async Task<string> Refusal(Func<Task> attempt)
        {
            var e = await Assert.ThrowsAsync<RelayRefusedException>(attempt);
            return e.Reason;
        }

        [Fact]
        public void CodesAreReadAsTypedOrPasted()
        {
            Assert.Equal("KXRT4MWP", RelayWire.Normalize("KXRT-4MWP"));
            Assert.Equal("KXRT4MWP", RelayWire.Normalize(" kxrt 4mwp\n"));
            Assert.Null(RelayWire.Normalize("KXRT-4MW"));
            // 0, O, 1, I, L and U are never given: a code with one was misread.
            Assert.Null(RelayWire.Normalize("KXRT-4MWO"));
            Assert.Null(RelayWire.Normalize("192.168.1.20"));
            Assert.Equal("KXRT-4MWP", RelayWire.Show("KXRT4MWP"));
        }

        [Fact]
        public async Task AJoinerReachesTheHostThroughTheRelay()
        {
            using var relay = Start(Options());
            var peer = new TaskCompletionSource<string>();
            using var link = await Link(At(relay), (c) =>
            {
                peer.TrySetResult(c.Peer);
                Echo(c);
            });
            Assert.Equal(RelayWire.CodeLength, link.Code!.Length);
            var joiner = await RelayDial.JoinAsync(At(relay), link.Code, CancellationToken.None);
            var heard = new TaskCompletionSource<string>();
            joiner.Message += (m) => heard.TrySetResult(m);
            joiner.Start();
            // Large enough to go deflated, as a mission does.
            var big = "{\"t\":\"x\",\"pad\":\"" + new string('a', 5000) + "\"}";
            joiner.Send(big);
            Assert.Equal("eco:" + big, await heard.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Contains("127.0.0.1 por código", await peer.Task);
            joiner.Close("fin");
        }

        [Fact]
        public async Task ARoomWelcomesAPlayerByCodeAndKeepsTheirSeatAcrossADrop()
        {
            using var relay = Start(Options());
            var dir = Path.Combine(Path.GetTempPath(), "bastion-rele-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json"), null), new EngineRules(), "1.0.0", null, new SystemClock(), new Rng(7));
                var host = new RoomHost(room, 0, IPAddress.Loopback);
                host.Start();
                var link = new RelayHost(At(relay));
                host.Relay(link);
                await Wait.Until(() => link.State == RelayState.Ready);
                var code = link.Code!;
                // The host's own app, by 127.0.0.1, takes the first seat; the partner comes by the code.
                var own = new ClientSession((cancel) => TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, cancel).ContinueWith((t) => (IConnection)t.Result), new MemoryTokens(), "1.0.0");
                own.Connect("Ana");
                await Wait.Until(() => own.Slot == 0, own.Pump);
                var tokens = new MemoryTokens();
                var partner = new ClientSession(async (cancel) => await RelayDial.JoinAsync(At(relay), code, cancel), tokens, "1.0.0");
                var closed = false;
                partner.HostClosed += () => closed = true;
                partner.Connect("Pepe");
                await Wait.Until(() => partner.Slot == 1, () =>
                {
                    own.Pump();
                    partner.Pump();
                });
                var token = tokens.Token;
                Assert.NotNull(token);
                // The partner's Wi-Fi drops: back by the same code, to the same seat, with the same token.
                var statuses = 0;
                var welcomes = 0;
                partner.Status += (s) => { if (s == NetStatus.Closed) statuses++; };
                partner.Message += (m) => { if (m is WelcomeMessage) welcomes++; };
                relay.CutPipes(code);
                await Wait.Until(() => statuses > 0, partner.Pump);
                await Wait.Until(() => welcomes == 1, partner.Pump, 15_000);
                Assert.Equal(1, partner.Slot);
                Assert.Equal(token, tokens.Token);
                // The host closes: the partner hears it through the relay, and the code is gone.
                host.Stop();
                await Wait.Until(() => closed, partner.Pump);
                await Wait.Until(() => relay.Codes.Count == 0);
                await Assert.ThrowsAsync<RelayRefusedException>(() => RelayDial.JoinAsync(At(relay), code, CancellationToken.None));
                partner.Stop();
                own.Stop();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task ByACodeAnOfflineSeatIsOnlyItsOwnPlayers()
        {
            using var relay = Start(Options());
            var dir = Path.Combine(Path.GetTempPath(), "bastion-rele-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json"), null), new EngineRules(), "1.0.0", null, new SystemClock(), new Rng(7));
                var host = new RoomHost(room, 0, IPAddress.Loopback);
                host.Start();
                var link = new RelayHost(At(relay));
                host.Relay(link);
                await Wait.Until(() => link.State == RelayState.Ready);
                var code = link.Code!;
                var own = new ClientSession((cancel) => TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, cancel).ContinueWith((t) => (IConnection)t.Result), new MemoryTokens(), "1.0.0");
                own.Connect("Ana");
                await Wait.Until(() => own.Slot == 0, own.Pump);
                ClientSession ByCode()
                {
                    return new ClientSession(async (cancel) => await RelayDial.JoinAsync(At(relay), code, cancel), new MemoryTokens(), "1.0.0");
                }
                var pepe = ByCode();
                pepe.Connect("Pepe");
                await Wait.Until(() => pepe.Slot == 1, () =>
                {
                    own.Pump();
                    pepe.Pump();
                });
                pepe.Stop();
                // Someone else with the code, while Pepe is away: not his seat.
                var stranger = ByCode();
                string? refused = null;
                stranger.Refused += (why) => refused = why;
                stranger.Connect("Otro");
                await Wait.Until(() => refused != null, stranger.Pump);
                Assert.Equal("La partida ya tiene dos jugadores.", refused);
                stranger.Stop();
                // Pepe from another computer (no token): his seat, by his name.
                var back = ByCode();
                back.Connect("Pepe");
                await Wait.Until(() => back.Slot == 1, back.Pump);
                back.Stop();
                own.Stop();
                host.Stop();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task TheHostsLinkComesBackWithItsCode()
        {
            using var relay = Start(Options());
            using var link = await Link(At(relay), Echo);
            var code = link.Code!;
            relay.CutLink(code);
            await Wait.Until(() => link.State == RelayState.Unreachable);
            // Meanwhile a joiner hears the host is away, not that the code is wrong.
            Assert.Equal(RelayRefusal.Away, await Refusal(() => RelayDial.JoinAsync(At(relay), code, CancellationToken.None)));
            await Wait.Until(() => link.State == RelayState.Ready);
            Assert.Equal(code, link.Code);
            var joiner = await RelayDial.JoinAsync(At(relay), code, CancellationToken.None);
            joiner.Close("fin");
        }

        [Fact]
        public async Task ARestartedRelayKnowsItsHostsByTheirSignatures()
        {
            var key = RandomNumberGenerator.GetBytes(32);
            var first = Start(Options((o) => o.Key = key));
            var port = first.Port;
            using var link = await Link(At(first), Echo);
            var code = link.Code!;
            first.Dispose();
            await Wait.Until(() => link.State == RelayState.Unreachable);
            // The same port and key, just restarted: an unknown code may be one whose host isn't back yet.
            using var second = Start(Options((o) =>
            {
                o.Key = key;
                o.Port = port;
                o.RestartGraceMs = 60_000;
            }));
            var at = new RelayAddress("127.0.0.1", port);
            if (link.State != RelayState.Ready) Assert.Equal(RelayRefusal.Away, await Refusal(() => RelayDial.JoinAsync(at, code, CancellationToken.None)));
            await Wait.Until(() => link.State == RelayState.Ready, null, 15_000);
            Assert.Equal(code, link.Code);
            var joiner = await RelayDial.JoinAsync(at, code, CancellationToken.None);
            joiner.Close("fin");
        }

        [Fact]
        public async Task ACodeThatRanOutIsReplacedByANewOne()
        {
            long now = 1_800_000_000_000;
            using var relay = Start(Options((o) => o.Now = () => Interlocked.Read(ref now)));
            using var link = await Link(At(relay));
            var code = link.Code!;
            Interlocked.Add(ref now, 25 * 3600_000L);
            relay.Sweep();
            await Wait.Until(() => link.State == RelayState.Ready && link.Code != null && link.Code != code, null, 15_000);
            Assert.Equal(RelayRefusal.Unknown, await Refusal(() => RelayDial.JoinAsync(At(relay), code, CancellationToken.None)));
        }

        [Fact]
        public async Task WrongCodesShutAnAddressOut()
        {
            using var relay = Start(Options((o) => o.FailsPerIp = 3));
            using var link = await Link(At(relay), Echo);
            for (var i = 0; i < 3; i++) Assert.Equal(RelayRefusal.Unknown, await Refusal(() => RelayDial.JoinAsync(At(relay), "AAAAAAAA", CancellationToken.None)));
            // Shut out, even with the right one.
            Assert.Equal(RelayRefusal.Limit, await Refusal(() => RelayDial.JoinAsync(At(relay), link.Code!, CancellationToken.None)));
        }

        [Fact]
        public async Task OneAddressHoldsSoManyCodes()
        {
            using var relay = Start(Options((o) => o.CodesPerIp = 1));
            using var first = await Link(At(relay));
            using var second = new RelayHost(At(relay));
            second.Start();
            await Wait.Until(() => second.State == RelayState.Unreachable);
            Assert.Null(second.Code);
            Assert.Single(relay.Codes);
        }

        [Fact]
        public async Task AGameHoldsSoManyPipes()
        {
            using var relay = Start(Options((o) => o.PipesPerCode = 1));
            using var link = await Link(At(relay), Echo);
            var one = await RelayDial.JoinAsync(At(relay), link.Code!, CancellationToken.None);
            Assert.Equal(RelayRefusal.Full, await Refusal(() => RelayDial.JoinAsync(At(relay), link.Code!, CancellationToken.None)));
            one.Close("fin");
        }

        [Fact]
        public async Task ASilentOrRudeConnectionIsClosed()
        {
            using var relay = Start(Options((o) => o.GreetMs = 300));
            // Silent: closed after GreetMs.
            using (var silent = new TcpClient())
            {
                await silent.ConnectAsync(IPAddress.Loopback, relay.Port);
                var read = silent.GetStream().ReadAsync(new byte[1], 0, 1);
                Assert.True(await Task.WhenAny(read, Task.Delay(5000)) == read);
                Assert.Equal(0, await read.ContinueWith((t) => t.IsFaulted ? 0 : t.Result));
            }
            // A line that never ends: closed, no answer.
            using (var rude = new TcpClient())
            {
                await rude.ConnectAsync(IPAddress.Loopback, relay.Port);
                var s = rude.GetStream();
                await s.WriteAsync(Encoding.ASCII.GetBytes(new string('A', 400)));
                var read = s.ReadAsync(new byte[1], 0, 1);
                Assert.True(await Task.WhenAny(read, Task.Delay(5000)) == read);
                Assert.Equal(0, await read.ContinueWith((t) => t.IsFaulted ? 0 : t.Result));
            }
        }

        [Fact]
        public async Task AnAppOfAnotherProtocolIsToldSo()
        {
            using var relay = Start(Options());
            using var old = new TcpClient();
            await old.ConnectAsync(IPAddress.Loopback, relay.Port);
            var s = old.GetStream();
            await RelayWire.WriteLineAsync(s, "BASTION-RELE 0 JOIN KXRT4MWP", CancellationToken.None);
            Assert.Equal("ERR version", await RelayWire.ReadLineAsync(s, CancellationToken.None));
        }

        /// <summary>Our own certificate (exported and read back: macOS's TLS won't serve one made in memory).</summary>
        private static X509Certificate2 Certificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Bastion rele", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pkcs12), null);
        }

        [Fact]
        public async Task OverTlsOnlyTheRelaysOwnKeyPasses()
        {
            using var cert = Certificate();
            using var relay = Start(Options((o) => o.Certificate = cert));
            var at = new RelayAddress("127.0.0.1", relay.Port, RelayWire.KeyOf(cert));
            using var link = await Link(at, Echo);
            var joiner = await RelayDial.JoinAsync(at, link.Code!, CancellationToken.None);
            var heard = new TaskCompletionSource<string>();
            joiner.Message += (m) => heard.TrySetResult(m);
            joiner.Start();
            joiner.Send("{\"t\":\"x\"}");
            Assert.Equal("eco:{\"t\":\"x\"}", await heard.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            joiner.Close("fin");
            // Another key (someone in between with a certificate of their own): no.
            using var other = Certificate();
            var wrong = new RelayAddress("127.0.0.1", relay.Port, RelayWire.KeyOf(other));
            await Assert.ThrowsAnyAsync<Exception>(() => RelayDial.JoinAsync(wrong, link.Code!, CancellationToken.None));
        }
    }
}
