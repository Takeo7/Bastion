using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>The host's port and its connections over TCP (Net/TcpHost.cs, Net/Connection.cs), on 127.0.0.1.</summary>
    public class NetTcpTests
    {
        /// <summary>
        /// `task`, which must end, within `ms`: generous, as it returns once it does. A loaded machine (Unity compiling,
        /// the other tests, a pause of the collector) can hold anything up a second or two, never ten.
        /// </summary>
        private static async Task<T> Within<T>(Task<T> task, int ms = 10_000)
        {
            var done = await Task.WhenAny(task, Task.Delay(ms));
            Assert.True(done == task, $"nothing after {ms} ms");
            return await task;
        }

        /// <summary>A host on a free port of 127.0.0.1 and the server's end of the next connection to it.</summary>
        private static (TcpHost host, Task<TcpConnection> server) Host()
        {
            var host = new TcpHost(0, IPAddress.Loopback);
            var server = new TaskCompletionSource<TcpConnection>();
            host.Connected += (c) => server.TrySetResult(c);
            host.Start();
            return (host, server.Task);
        }

        private static Task<string> Next(IConnection c)
        {
            var got = new TaskCompletionSource<string>();
            c.Message += (m) => got.TrySetResult(m);
            return got.Task;
        }

        private static Task<string> ClosedOf(IConnection c)
        {
            var why = new TaskCompletionSource<string>();
            c.Closed += (r) => why.TrySetResult(r);
            return why.Task;
        }

        [Fact]
        public async Task MessagesGoBothWays()
        {
            var (host, serverTask) = Host();
            using var _ = host;
            var client = await TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, CancellationToken.None);
            var fromServer = Next(client);
            client.Start();
            var server = await Within(serverTask);
            var fromClient = Next(server);
            client.Send("{\"t\":\"hello\",\"name\":\"Ana\"}");
            Assert.Equal("{\"t\":\"hello\",\"name\":\"Ana\"}", await Within(fromClient));
            server.Send("{\"t\":\"welcome\",\"slot\":1}");
            Assert.Equal("{\"t\":\"welcome\",\"slot\":1}", await Within(fromServer));
            client.Close("fin");
        }

        [Fact]
        public async Task AWholeMissionGoesToAPlayer()
        {
            var (host, serverTask) = Host();
            using var _ = host;
            var client = await TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, CancellationToken.None);
            var got = Next(client);
            client.Start();
            var server = await Within(serverTask);
            // Some megabytes of state, far over what a player may send: deflated, it goes in one frame.
            var json = "{\"t\":\"snapshot\",\"tiles\":[" + string.Join(",", Enumerable.Range(0, 400_000).Select((i) => (i % 9).ToString())) + "]}";
            server.Send(json);
            Assert.Equal(json, await Within(got));
            client.Close("fin");
        }

        [Fact]
        public async Task APlayerMayNotSendAWholeMission()
        {
            var (host, serverTask) = Host();
            using var _ = host;
            var client = await TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, CancellationToken.None);
            client.Start();
            var server = await Within(serverTask);
            var closed = ClosedOf(server);
            // Random digits barely deflate: well over the host's 64 KB on the wire.
            var rng = new Random(7);
            client.Send("{\"t\":\"cmd\",\"junk\":\"" + string.Concat(Enumerable.Range(0, 400_000).Select((_) => (char)('0' + rng.Next(10)))) + "\"}");
            Assert.Contains("demasiado grande", await Within(closed));
        }

        [Fact]
        public async Task OneEndClosingClosesTheOther()
        {
            var (host, serverTask) = Host();
            using var _ = host;
            var client = await TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, CancellationToken.None);
            client.Start();
            var server = await Within(serverTask);
            var serverClosed = ClosedOf(server);
            var clientClosed = ClosedOf(client);
            client.Close("me voy");
            Assert.Equal("me voy", await Within(clientClosed));
            Assert.Equal("cerrada", await Within(serverClosed));
            // Sending after the close does nothing, and closing again neither.
            client.Send("{\"t\":\"sync\"}");
            client.Close("otra vez");
        }

        [Fact]
        public async Task APeerThatStopsAnsweringIsTakenForGone()
        {
            // The other end connects and then says nothing at all, not even a pong (a laptop shut, the Wi-Fi gone).
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var silent = new TcpClient();
            await silent.ConnectAsync("127.0.0.1", port);
            var server = new TcpConnection(await listener.AcceptTcpClientAsync(), 1024, 1024, heartbeatMs: 50, deadAfterMs: 300);
            var closed = ClosedOf(server);
            server.Start();
            Assert.Equal("no responde", await Within(closed));
            listener.Stop();
        }

        [Fact]
        public async Task TwoLiveEndsKeepEachOtherAlive()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var raw = new TcpClient();
            await raw.ConnectAsync("127.0.0.1", port);
            // Beating every 50 ms, a second of nothing for dead. Both ends share this process: held up together (the pool
            // once 1.3 s late, with the whole suite twice at once), neither may take the other for gone on waking, before
            // reading what waits (TcpConnection.BeatLoop); the second covers one end held up while the other isn't.
            var server = new TcpConnection(await listener.AcceptTcpClientAsync(), 1024, 1024, heartbeatMs: 50, deadAfterMs: 1000);
            var client = new TcpConnection(raw, 1024, 1024, heartbeatMs: 50, deadAfterMs: 1000);
            var serverClosed = ClosedOf(server);
            var clientClosed = ClosedOf(client);
            server.Start();
            client.Start();
            // How late the thread pool, which the connections' loops run on, was at its worst meanwhile: if they are
            // taken for gone, whether the machine held them up or the heartbeat failed.
            var watch = Stopwatch.StartNew();
            long worst = 0;
            var probe = Task.Run(async () =>
            {
                while (watch.ElapsedMilliseconds < 3000)
                {
                    var before = watch.ElapsedMilliseconds;
                    await Task.Delay(10).ConfigureAwait(false);
                    worst = Math.Max(worst, watch.ElapsedMilliseconds - before - 10);
                }
            });
            // Three times the dead time with nothing to say but the heartbeat: both still open.
            await Task.Delay(3000);
            await probe;
            string Why(Task<string> closed) => closed.IsCompleted ? $"closed «{closed.Result}»; the pool was up to {worst} ms late" : "";
            Assert.False(serverClosed.IsCompleted || clientClosed.IsCompleted, $"server: {Why(serverClosed)} · client: {Why(clientClosed)}");
            client.Close("fin");
            listener.Stop();
        }

        [Fact]
        public void APortInUseSaysSo()
        {
            using var first = new TcpHost(0, IPAddress.Loopback);
            first.Start();
            using var second = new TcpHost(first.Port, IPAddress.Loopback);
            Assert.Throws<SocketException>(() => second.Start());
        }

        [Fact]
        public async Task APlayersSessionGetsItsSeatOverTcp()
        {
            using var host = new TcpHost(0, IPAddress.Loopback);
            var hello = new TaskCompletionSource<(TcpConnection server, string text)>();
            // Listened to as it comes out of Connected: it is started right after, and the hello may come at once.
            host.Connected += (c) => c.Message += (m) => hello.TrySetResult((c, m));
            host.Start();
            var tokens = new MemoryTokens();
            var session = new ClientSession((cancel) => TcpConnection.ConnectAsync("127.0.0.1", host.Port, 5000, cancel).ContinueWith((t) => (IConnection)t.Result), tokens, "1.0.0");
            session.Connect("Ana");
            await Wait.Until(() => hello.Task.IsCompleted, session.Pump);
            var (server, text) = await hello.Task;
            Assert.Contains("\"name\":\"Ana\"", text);
            server.Send("{\"t\":\"welcome\",\"token\":\"ficha-tcp\",\"slot\":0}");
            await Wait.Until(() => session.Slot == 0, session.Pump);
            Assert.Equal("ficha-tcp", tokens.Token);
            session.Stop();
        }

        [Fact]
        public async Task NobodyThereFailsInTime()
        {
            // A port with nobody listening: refused at once, or at most the timeout, never the system's (20 s and more).
            // Held, bound but not listening, while it's tried: a port let go could be taken meanwhile by another test's
            // host, or be the very port the attempt goes out from (a socket connected to itself), and then it connects.
            using var held = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            held.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)held.LocalEndPoint!).Port;
            var started = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<Exception>(() => TcpConnection.ConnectAsync("127.0.0.1", port, 1000, CancellationToken.None));
            Assert.True(started.ElapsedMilliseconds < 5000, $"{started.ElapsedMilliseconds} ms");
        }
    }
}
