using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Bastion.Net
{
    /// <summary>A game heard on the network, and where to join it.</summary>
    public sealed class LanGame
    {
        public string Name { get; set; } = "";

        /// <summary>Where to join it: address:port.</summary>
        public string Address { get; set; } = "";

        public List<string> Players { get; set; } = new List<string>();

        /// <summary>lobby, base or mission.</summary>
        public string Phase { get; set; } = "";

        /// <summary>The host's build: only one of the same version can be joined (the hello says so).</summary>
        public string Version { get; set; } = "";
    }

    /// <summary>
    /// Finding a game on the local network without typing addresses (server lan.ts). The host's app says every two
    /// seconds, by UDP broadcast, that there is a game here and on which port (Announce); the start screen listens and
    /// lists what it hears (Listen). Tailscale carries no broadcasts: there the address is still typed. The same port and
    /// message as the TypeScript app's, but for its `game` (the two can't play together: neither lists the other's) and
    /// the version.
    /// </summary>
    public static class LanBeacon
    {
        /// <summary>The UDP port the announcements go to.</summary>
        public const int Port = 41390;

        /// <summary>What an announcement of this app says it is (lan.ts says «bastion»: the TypeScript app's, over WebSocket).</summary>
        public const string Game = "bastion-tcp";

        /// <summary>A game not heard for this long has gone.</summary>
        private const int GoneMs = 7000;

        /// <summary>
        /// Announces the game on `port` every two seconds, of build `version`; `about` says who is in it and in which phase.
        /// Dispose stops it. `toPort`: where else to send it (the tests' own port); by default, every local network's
        /// broadcast and this machine.
        /// </summary>
        public static IDisposable Announce(int port, string computer, string version, Func<(IReadOnlyList<string> players, string phase)> about, int toPort = Port)
        {
            var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            var cancel = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, cancel.Token).ConfigureAwait(false);
                    while (!cancel.IsCancellationRequested)
                    {
                        var (players, phase) = about();
                        var beacon = new JObject
                        {
                            ["game"] = Game,
                            ["port"] = port,
                            ["name"] = players.Count > 0 ? players[0] : computer,
                            ["players"] = new JArray(players),
                            ["phase"] = phase,
                            ["version"] = version,
                        };
                        var data = Encoding.UTF8.GetBytes(beacon.ToString(Newtonsoft.Json.Formatting.None));
                        // This machine too: a second copy of the app on it hears the game (tests, two players on one computer).
                        foreach (var to in Broadcasts().Append(IPAddress.Loopback))
                        {
                            try
                            {
                                await udp.SendAsync(data, data.Length, new IPEndPoint(to, toPort)).ConfigureAwait(false);
                            }
                            catch (SocketException)
                            {
                                /* the announcement is a help: it never stops the game */
                            }
                        }
                        await Task.Delay(2000, cancel.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    /* stopped */
                }
            });
            return new Stop(() =>
            {
                cancel.Cancel();
                udp.Dispose();
            });
        }

        /// <summary>
        /// Listens for announcements: `changed` gets the games heard in the last few seconds whenever that list changes
        /// (on a network thread). `skip(port)` leaves out a game of this machine (the app's own). Dispose stops it.
        /// </summary>
        public static IDisposable Listen(Action<IReadOnlyList<LanGame>> changed, Func<int, bool>? skip = null, int port = Port)
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            // Several copies of the app on one computer all hear the announcements.
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            var cancel = new CancellationTokenSource();
            var games = new Dictionary<string, (LanGame game, DateTime seen)>();
            var own = OwnAddresses();
            string last = "";
            var gate = new object();
            void Emit()
            {
                List<LanGame> list;
                lock (gate)
                {
                    foreach (var key in games.Where((g) => (DateTime.UtcNow - g.Value.seen).TotalMilliseconds > GoneMs).Select((g) => g.Key).ToList()) games.Remove(key);
                    list = games.Values.Select((g) => g.game).OrderBy((g) => g.Name, StringComparer.Ordinal).ToList();
                }
                var now = string.Join("|", list.Select((g) => $"{g.Name}/{g.Address}/{string.Join(",", g.Players)}/{g.Phase}/{g.Version}"));
                if (now == last) return;
                last = now;
                changed(list);
            }
            _ = Task.Run(async () =>
            {
                while (!cancel.IsCancellationRequested)
                {
                    UdpReceiveResult got;
                    try
                    {
                        got = await udp.ReceiveAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    var game = Read(got.Buffer, got.RemoteEndPoint.Address);
                    if (game == null) continue;
                    string from = got.RemoteEndPoint.Address.ToString();
                    // A game of this machine arrives twice (broadcast and loopback): one entry, with its network address if there is one.
                    bool local = own.Contains(from);
                    int gamePort = int.Parse(game.Address.Substring(game.Address.LastIndexOf(':') + 1));
                    if (local && skip != null && skip(gamePort)) continue;
                    string key = local ? $"local:{gamePort}" : game.Address;
                    lock (gate)
                    {
                        if (from == "127.0.0.1" && games.TryGetValue(key, out var before)) game.Address = before.game.Address;
                        games[key] = (game, DateTime.UtcNow);
                    }
                    Emit();
                }
            });
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cancel.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancel.Token).ConfigureAwait(false);
                        Emit();
                    }
                }
                catch (OperationCanceledException)
                {
                    /* stopped */
                }
            });
            return new Stop(() =>
            {
                cancel.Cancel();
                udp.Dispose();
            });
        }

        /// <summary>An announcement, or null if it is not one of ours (anything else arriving on the port is ignored).</summary>
        public static LanGame? Read(byte[] data, IPAddress from)
        {
            JObject b;
            try
            {
                b = JObject.Parse(Encoding.UTF8.GetString(data));
            }
            catch (Exception)
            {
                return null;
            }
            if ((string?)b["game"] != Game || b["port"]?.Type != JTokenType.Integer) return null;
            long port = (long)b["port"]!;
            if (port <= 0 || port > 65535) return null;
            static string Text(JToken? v, int max) => v?.Type == JTokenType.String ? Cut((string)v!, max) : "";
            var players = b["players"] is JArray list ? list.Take(2).Select((p) => Text(p, 32)).ToList() : new List<string>();
            string name = Text(b["name"], 32);
            return new LanGame { Name = name.Length > 0 ? name : from.ToString(), Address = $"{from}:{port}", Players = players, Phase = Text(b["phase"], 16), Version = Text(b["version"], 16) };
        }

        private static string Cut(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>This machine's IPv4 addresses.</summary>
        private static HashSet<string> OwnAddresses()
        {
            var own = new HashSet<string> { "127.0.0.1" };
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                    foreach (var a in nic.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork) own.Add(a.Address.ToString());
            }
            catch (NetworkInformationException)
            {
                /* only loopback, then */
            }
            return own;
        }

        /// <summary>Broadcast addresses of this machine's networks (not loopback, nor point-to-point links such as Tailscale's).</summary>
        private static IEnumerable<IPAddress> Broadcasts()
        {
            var found = new HashSet<string>();
            foreach (var nic in HostAddresses.Interfaces())
            {
                foreach (var a in nic.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork || a.IPv4Mask == null) continue;
                    var ip = a.Address.GetAddressBytes();
                    var mask = a.IPv4Mask.GetAddressBytes();
                    if (mask.All((m) => m == 255)) continue;
                    var broadcast = new byte[4];
                    for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    var address = new IPAddress(broadcast);
                    if (found.Add(address.ToString())) yield return address;
                }
            }
        }

        private sealed class Stop : IDisposable
        {
            private Action? stop;

            public Stop(Action stop)
            {
                this.stop = stop;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref stop, null)?.Invoke();
            }
        }
    }
}
