using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The game's port opened on the router (Net/RouterPort.cs), against routers of its own on 127.0.0.1: one with UPnP
    /// (its description and its SOAP, found by an SSDP that answers the search) and one with NAT-PMP. Never the real
    /// router: a test must not open a port on the house's. Alone, after the rest (TimeBudgets): NAT-PMP's answer is waited
    /// for 1.75 s in all, and with every test at once it sometimes came later, so UPnP opened it (NatPmpComesBeforeUpnp,
    /// 6/11; alone it always passed).
    /// </summary>
    [Collection(TimeBudgets.Name)]
    public class NetRouterTests
    {
        private const int Game = 41391;

        [Fact]
        public async Task UpnpOpensThePortAndSaysWhere()
        {
            using var igd = new FakeIgd();
            using var ssdp = new FakeSsdp(igd.Location);
            var lines = new List<string>();
            var router = RouterPort.Open(Game, (l) => { lock (lines) lines.Add(l); }, ssdp.Places());
            await Settled(router);
            lock (lines) Assert.True(router.State == RouterState.Open, $"{router.State}: {string.Join(" | ", lines)}");
            Assert.Equal($"83.45.12.7:{Game}", router.Public);
            var (client, inside, lease) = igd.Mapped[Game];
            Assert.Equal("127.0.0.1", client);
            Assert.Equal(Game, inside);
            Assert.Equal(RouterPort.LeaseSeconds, lease);
            router.Dispose();
            Assert.Empty(igd.Mapped);
            lock (lines) Assert.Contains(lines, (l) => l.Contains("puerto abierto por UPnP"));
        }

        [Fact]
        public async Task ARouterThatIsNotOnTheInternetIsShared()
        {
            using var igd = new FakeIgd { External = "100.72.9.1" };
            using var ssdp = new FakeSsdp(igd.Location);
            using var router = RouterPort.Open(Game, null, ssdp.Places());
            await Settled(router);
            Assert.Equal(RouterState.Shared, router.State);
            Assert.Null(router.Public);
            Assert.Empty(igd.Mapped);
        }

        [Fact]
        public async Task AnotherMachinesPortIsLeftAndTheNextOneTaken()
        {
            using var igd = new FakeIgd();
            igd.Taken.Add(Game);
            using var ssdp = new FakeSsdp(igd.Location);
            using var router = RouterPort.Open(Game, null, ssdp.Places());
            await Settled(router);
            Assert.Equal($"83.45.12.7:{Game + 1}", router.Public);
            Assert.Equal(Game, igd.Mapped[Game + 1].Inside);
        }

        [Fact]
        public async Task ThisMachinesOwnLeftoverIsTakenAsItIs()
        {
            using var igd = new FakeIgd();
            igd.Mapped[Game] = ("127.0.0.1", Game, 3600);
            igd.Conflict = true;
            using var ssdp = new FakeSsdp(igd.Location);
            using var router = RouterPort.Open(Game, null, ssdp.Places());
            await Settled(router);
            Assert.Equal($"83.45.12.7:{Game}", router.Public);
        }

        [Fact]
        public async Task ARouterThatOnlyKeepsThemForGoodGetsOneForGood()
        {
            using var igd = new FakeIgd { OnlyPermanent = true };
            using var ssdp = new FakeSsdp(igd.Location);
            var router = RouterPort.Open(Game, null, ssdp.Places());
            await Settled(router);
            Assert.Equal(RouterState.Open, router.State);
            Assert.Equal(0, igd.Mapped[Game].Lease);
            router.Dispose();
            Assert.Empty(igd.Mapped);
        }

        [Fact]
        public async Task ARouterThatWontIsRefused()
        {
            using var igd = new FakeIgd { Refuse = 606 };
            using var ssdp = new FakeSsdp(igd.Location);
            using var router = RouterPort.Open(Game, null, ssdp.Places());
            await Settled(router);
            Assert.Equal(RouterState.Refused, router.State);
            Assert.Null(router.Public);
        }

        [Fact]
        public async Task APppRouterWithItsOwnBaseAndChunksIsUnderstood()
        {
            using var igd = new FakeIgd { Ppp = true, Chunked = true };
            using var ssdp = new FakeSsdp(igd.Location);
            using var router = RouterPort.Open(Game, null, ssdp.Places());
            await Settled(router);
            Assert.Equal(RouterState.Open, router.State);
            Assert.Contains(igd.Calls, (c) => c == "/base/ctl/PPPConn AddPortMapping");
        }

        [Fact]
        public async Task NoRouterIsSaidSoon()
        {
            var places = new RouterPlaces { Pmp = { new IPEndPoint(IPAddress.Loopback, FreeUdpPort()) }, Ssdp = new IPEndPoint(IPAddress.Loopback, FreeUdpPort()), Locals = { IPAddress.Loopback }, SearchMs = 800 };
            var clock = Stopwatch.StartNew();
            using var router = RouterPort.Open(Game, null, places);
            await Settled(router);
            Assert.Equal(RouterState.NoRouter, router.State);
            Assert.True(clock.ElapsedMilliseconds < 4000, $"{clock.ElapsedMilliseconds} ms");
        }

        [Fact]
        public async Task NatPmpOpensThePortAndTakesItBack()
        {
            using var pmp = new FakePmp { Outside = Game + 3 };
            var router = RouterPort.Open(Game, null, new RouterPlaces { Pmp = { pmp.EndPoint }, SearchMs = 300 });
            await Settled(router);
            Assert.Equal(RouterState.Open, router.State);
            Assert.Equal($"83.45.12.7:{Game + 3}", router.Public);
            Assert.True(pmp.Mapped.ContainsKey(Game));
            router.Dispose();
            Assert.False(pmp.Mapped.ContainsKey(Game));
        }

        [Fact]
        public async Task NatPmpComesBeforeUpnp()
        {
            using var pmp = new FakePmp();
            using var igd = new FakeIgd();
            using var ssdp = new FakeSsdp(igd.Location);
            var places = ssdp.Places();
            places.Pmp.Add(pmp.EndPoint);
            using var router = RouterPort.Open(Game, null, places);
            await Settled(router);
            Assert.Equal(RouterState.Open, router.State);
            Assert.True(pmp.Mapped.ContainsKey(Game));
            Assert.Empty(igd.Mapped);
        }

        [Fact]
        public void AddressesOffTheInternetAreTold()
        {
            foreach (var a in new[] { "10.0.0.1", "172.16.4.1", "172.31.255.1", "192.168.1.1", "100.64.0.1", "100.127.3.4", "169.254.1.1", "0.0.0.0" })
                Assert.True(RouterPort.Shared(IPAddress.Parse(a)), a);
            foreach (var a in new[] { "83.45.12.7", "172.32.0.1", "100.128.0.1", "100.63.255.1", "8.8.8.8" })
                Assert.False(RouterPort.Shared(IPAddress.Parse(a)), a);
            // A connection's peer, as the room hears it.
            foreach (var p in new[] { "83.45.12.7:51000", "[::ffff:83.45.12.7]:51000" })
                Assert.True(HostAddresses.FromTheInternet(p), p);
            foreach (var p in new[] { "192.168.1.30:51000", "100.101.12.7:41391", "127.0.0.1:4000", "10.0.0.2:5000", "?" })
                Assert.False(HostAddresses.FromTheInternet(p), p);
        }

        private static async Task Settled(RouterPort router)
        {
            var clock = Stopwatch.StartNew();
            while (router.State == RouterState.Trying && clock.ElapsedMilliseconds < 10000) await Task.Delay(20);
            Assert.NotEqual(RouterState.Trying, router.State);
        }

        private static int FreeUdpPort()
        {
            using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        }

        /// <summary>An Internet Gateway Device: its description, and AddPortMapping and the rest by SOAP.</summary>
        private sealed class FakeIgd : IDisposable
        {
            public string External = "83.45.12.7";

            /// <summary>Ports outside another machine has.</summary>
            public readonly HashSet<int> Taken = new HashSet<int>();

            /// <summary>A port already mapped is a conflict, even this machine's (some routers).</summary>
            public bool Conflict;

            public bool OnlyPermanent;
            public int? Refuse;
            public bool Ppp;
            public bool Chunked;
            public readonly ConcurrentDictionary<int, (string Client, int Inside, int Lease)> Mapped = new ConcurrentDictionary<int, (string, int, int)>();
            public readonly ConcurrentQueue<string> Calls = new ConcurrentQueue<string>();
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource stop = new CancellationTokenSource();

            public FakeIgd()
            {
                listener.Start();
                _ = Task.Run(Serve);
            }

            public Uri Location => new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/rootDesc.xml");

            private async Task Serve()
            {
                while (!stop.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync();
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    _ = Task.Run(() => Answer(client));
                }
            }

            private async Task Answer(TcpClient client)
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var got = new MemoryStream();
                    var buffer = new byte[4096];
                    string text;
                    while (true)
                    {
                        var n = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (n == 0) return;
                        got.Write(buffer, 0, n);
                        text = Encoding.UTF8.GetString(got.ToArray());
                        var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (split < 0) continue;
                        var length = Regex.Match(text, @"Content-Length: (\d+)", RegexOptions.IgnoreCase);
                        if (!length.Success || text.Length - split - 4 >= int.Parse(length.Groups[1].Value)) break;
                    }
                    var path = text.Split(' ')[1];
                    var (status, body) = text.StartsWith("GET") ? (200, Description()) : Soap(path, text);
                    var bytes = Encoding.UTF8.GetBytes(body);
                    var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Internal Server Error")}\r\nContent-Type: text/xml\r\n" + (Chunked ? "Transfer-Encoding: chunked\r\n\r\n" : $"Content-Length: {bytes.Length}\r\n\r\n");
                    var out_ = new MemoryStream();
                    out_.Write(Encoding.ASCII.GetBytes(head));
                    if (Chunked)
                    {
                        // In pieces of 100 bytes, as a router's small buffer would.
                        for (var at = 0; at < bytes.Length; at += 100)
                        {
                            var size = Math.Min(100, bytes.Length - at);
                            out_.Write(Encoding.ASCII.GetBytes($"{size:x}\r\n"));
                            out_.Write(bytes, at, size);
                            out_.Write(Encoding.ASCII.GetBytes("\r\n"));
                        }
                        out_.Write(Encoding.ASCII.GetBytes("0\r\n\r\n"));
                    }
                    else out_.Write(bytes);
                    var all = out_.ToArray();
                    await stream.WriteAsync(all, 0, all.Length);
                }
            }

            private string Description()
            {
                var service = Ppp ? "WANPPPConnection" : "WANIPConnection";
                var control = Ppp ? "ctl/PPPConn" : "/ctl/IPConn";
                var urlBase = Ppp ? $"<URLBase>http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/base/</URLBase>" : "";
                return $@"<?xml version=""1.0""?>
<root xmlns=""urn:schemas-upnp-org:device-1-0""><specVersion><major>1</major><minor>0</minor></specVersion>{urlBase}
<device><deviceType>urn:schemas-upnp-org:device:InternetGatewayDevice:1</deviceType>
<serviceList><service><serviceType>urn:schemas-upnp-org:service:Layer3Forwarding:1</serviceType><controlURL>/ctl/L3F</controlURL></service></serviceList>
<deviceList><device><deviceType>urn:schemas-upnp-org:device:WANDevice:1</deviceType>
<deviceList><device><deviceType>urn:schemas-upnp-org:device:WANConnectionDevice:1</deviceType>
<serviceList><service><serviceType>urn:schemas-upnp-org:service:{service}:1</serviceType><serviceId>urn:upnp-org:serviceId:Conn1</serviceId><controlURL>{control}</controlURL><eventSubURL>/evt</eventSubURL><SCPDURL>/scpd.xml</SCPDURL></service></serviceList>
</device></deviceList></device></deviceList></device></root>";
            }

            private (int, string) Soap(string path, string request)
            {
                var action = Regex.Match(request, @"SOAPAction: ""[^#]+#(\w+)""").Groups[1].Value;
                Calls.Enqueue($"{path} {action}");
                string Arg(string name) => Regex.Match(request, $"<{name}>([^<]*)</{name}>").Groups[1].Value;
                int outside = int.TryParse(Arg("NewExternalPort"), out var p) ? p : 0;
                switch (action)
                {
                    case "GetExternalIPAddress":
                        return Ok(action, $"<NewExternalIPAddress>{External}</NewExternalIPAddress>");
                    case "AddPortMapping":
                        if (Refuse != null) return Fault(Refuse.Value, "Action not authorized");
                        var lease = int.Parse(Arg("NewLeaseDuration"));
                        if (OnlyPermanent && lease != 0) return Fault(725, "OnlyPermanentLeasesSupported");
                        if (Taken.Contains(outside) || (Conflict && Mapped.ContainsKey(outside))) return Fault(718, "ConflictInMappingEntry");
                        Mapped[outside] = (Arg("NewInternalClient"), int.Parse(Arg("NewInternalPort")), lease);
                        return Ok(action, "");
                    case "GetSpecificPortMappingEntry":
                        if (Taken.Contains(outside)) return Ok(action, $"<NewInternalPort>{Game}</NewInternalPort><NewInternalClient>192.168.1.50</NewInternalClient>");
                        if (Mapped.TryGetValue(outside, out var m)) return Ok(action, $"<NewInternalPort>{m.Inside}</NewInternalPort><NewInternalClient>{m.Client}</NewInternalClient>");
                        return Fault(714, "NoSuchEntryInArray");
                    case "DeletePortMapping":
                        return Mapped.TryRemove(outside, out _) ? Ok(action, "") : Fault(714, "NoSuchEntryInArray");
                }
                return Fault(401, "Invalid Action");
            }

            private (int, string) Ok(string action, string inner)
            {
                var service = Ppp ? "WANPPPConnection" : "WANIPConnection";
                return (200, $@"<?xml version=""1.0""?><s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><u:{action}Response xmlns:u=""urn:schemas-upnp-org:service:{service}:1"">{inner}</u:{action}Response></s:Body></s:Envelope>");
            }

            private static (int, string) Fault(int code, string description)
            {
                return (500, $@"<?xml version=""1.0""?><s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><s:Fault><faultcode>s:Client</faultcode><faultstring>UPnPError</faultstring><detail><UPnPError xmlns=""urn:schemas-upnp-org:control-1-0""><errorCode>{code}</errorCode><errorDescription>{description}</errorDescription></UPnPError></detail></s:Fault></s:Body></s:Envelope>");
            }

            public void Dispose()
            {
                stop.Cancel();
                listener.Stop();
            }
        }

        /// <summary>SSDP's answer to the search, from a router on 127.0.0.1: where its description is.</summary>
        private sealed class FakeSsdp : IDisposable
        {
            private readonly UdpClient udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

            public FakeSsdp(Uri location)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        UdpReceiveResult got;
                        try
                        {
                            got = await udp.ReceiveAsync();
                        }
                        catch (Exception)
                        {
                            return;
                        }
                        var text = Encoding.ASCII.GetString(got.Buffer);
                        var st = Regex.Match(text, @"\nST: (\S+)").Groups[1].Value;
                        if (!text.StartsWith("M-SEARCH") || !st.Contains("InternetGatewayDevice")) continue;
                        var answer = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nST: {st}\r\nUSN: uuid:prueba::{st}\r\nLocation: {location}\r\nSERVER: prueba UPnP/1.1\r\n\r\n");
                        await udp.SendAsync(answer, answer.Length, got.RemoteEndPoint);
                    }
                });
            }

            public RouterPlaces Places()
            {
                return new RouterPlaces { Ssdp = (IPEndPoint)udp.Client.LocalEndPoint!, Locals = { IPAddress.Loopback }, SearchMs = 1500 };
            }

            public void Dispose()
            {
                udp.Dispose();
            }
        }

        /// <summary>A router with NAT-PMP on 127.0.0.1: its address, and TCP ports mapped and taken back.</summary>
        private sealed class FakePmp : IDisposable
        {
            public int? Outside;
            public readonly ConcurrentDictionary<int, int> Mapped = new ConcurrentDictionary<int, int>();
            private readonly UdpClient udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

            public FakePmp()
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        UdpReceiveResult got;
                        try
                        {
                            got = await udp.ReceiveAsync();
                        }
                        catch (Exception)
                        {
                            return;
                        }
                        var r = got.Buffer;
                        byte[] answer;
                        if (r.Length == 2 && r[1] == 0) answer = new byte[] { 0, 128, 0, 0, 0, 0, 0, 9, 83, 45, 12, 7 };
                        else if (r.Length == 12 && r[1] == 2)
                        {
                            var inside = (r[4] << 8) | r[5];
                            var lifetime = (r[8] << 24) | (r[9] << 16) | (r[10] << 8) | r[11];
                            var outside = lifetime == 0 ? 0 : Outside ?? ((r[6] << 8) | r[7]);
                            if (lifetime == 0) Mapped.TryRemove(inside, out _);
                            else Mapped[inside] = outside;
                            answer = new byte[] { 0, 130, 0, 0, 0, 0, 0, 9, r[4], r[5], (byte)(outside >> 8), (byte)outside, r[8], r[9], r[10], r[11] };
                        }
                        else continue;
                        await udp.SendAsync(answer, answer.Length, got.RemoteEndPoint);
                    }
                });
            }

            public IPEndPoint EndPoint => (IPEndPoint)udp.Client.LocalEndPoint!;

            public void Dispose()
            {
                udp.Dispose();
            }
        }
    }
}
