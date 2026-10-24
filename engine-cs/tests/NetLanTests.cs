using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Games on the local network (Net/LanBeacon.cs) and the host's addresses (Net/HostAddresses.cs). On a free UDP port,
    /// not 41390: a test must not show a game in the list of an app that is open.
    /// </summary>
    public class NetLanTests
    {
        private static int FreeUdpPort()
        {
            using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        }

        [Fact]
        public async Task AnAnnouncedGameIsHeard()
        {
            int udp = FreeUdpPort();
            var heard = new TaskCompletionSource<IReadOnlyList<LanGame>>();
            using var listen = LanBeacon.Listen((games) => { if (games.Count > 0) heard.TrySetResult(games); }, null, udp);
            using var announce = LanBeacon.Announce(3999, "mac-de-arturo", "1.0.0", () => (new[] { "Arturo", "Ana" }, "mission"), udp);
            var done = await Task.WhenAny(heard.Task, Task.Delay(5000));
            Assert.True(done == heard.Task, "nothing heard in 5 s");
            var game = (await heard.Task).Single();
            Assert.Equal("Arturo", game.Name);
            Assert.EndsWith(":3999", game.Address);
            Assert.Equal(new[] { "Arturo", "Ana" }, game.Players);
            Assert.Equal("mission", game.Phase);
            Assert.Equal("1.0.0", game.Version);
        }

        [Fact]
        public async Task TheAppsOwnGameIsLeftOut()
        {
            int udp = FreeUdpPort();
            var lists = new List<IReadOnlyList<LanGame>>();
            using var listen = LanBeacon.Listen((games) => { lock (lists) lists.Add(games); }, (port) => port == 3999, udp);
            using var announce = LanBeacon.Announce(3999, "mac-de-arturo", "1.0.0", () => (Array.Empty<string>(), "lobby"), udp);
            await Task.Delay(1500);
            lock (lists) Assert.All(lists, (games) => Assert.Empty(games));
        }

        [Fact]
        public void WithoutPlayersTheComputerNamesTheGame()
        {
            var game = LanBeacon.Read(Encoding.UTF8.GetBytes("{\"game\":\"bastion-tcp\",\"port\":3000,\"name\":\"mac-de-arturo\",\"players\":[],\"phase\":\"lobby\"}"), IPAddress.Parse("192.168.1.20"));
            Assert.NotNull(game);
            Assert.Equal("mac-de-arturo", game!.Name);
            Assert.Equal("192.168.1.20:3000", game.Address);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("{\"game\":\"other\",\"port\":3000}")]
        // The TypeScript app's (lan.ts): over WebSocket, it can't be joined from here.
        [InlineData("{\"game\":\"bastion\",\"port\":3000,\"name\":\"mac-de-arturo\",\"players\":[],\"phase\":\"lobby\"}")]
        [InlineData("{\"game\":\"bastion-tcp\",\"port\":\"3000\"}")]
        [InlineData("{\"game\":\"bastion-tcp\",\"port\":70000}")]
        [InlineData("{\"game\":\"bastion-tcp\",\"port\":0}")]
        public void AnythingElseOnThePortIsIgnored(string text)
        {
            Assert.Null(LanBeacon.Read(Encoding.UTF8.GetBytes(text), IPAddress.Loopback));
        }

        [Fact]
        public void NamesAndListsAreCut()
        {
            var game = LanBeacon.Read(Encoding.UTF8.GetBytes("{\"game\":\"bastion-tcp\",\"port\":3000,\"name\":\"" + new string('x', 80) + "\",\"players\":[\"a\",\"b\",\"c\"],\"phase\":\"" + new string('p', 40) + "\"}"), IPAddress.Loopback);
            Assert.Equal(32, game!.Name.Length);
            Assert.Equal(2, game.Players.Count);
            Assert.Equal(16, game.Phase.Length);
        }

        [Fact]
        public void TailscaleIsItsOwnKind()
        {
            Assert.True(HostAddresses.IsTailscale(IPAddress.Parse("100.116.6.72")));
            Assert.False(HostAddresses.IsTailscale(IPAddress.Parse("100.20.0.1")));
            Assert.False(HostAddresses.IsTailscale(IPAddress.Parse("192.168.1.128")));
        }

        [Fact]
        public void TheDefaultRoutesAddressGoesFirst()
        {
            var list = HostAddresses.List();
            var main = HostAddresses.Outbound();
            Assert.All(list, (h) => Assert.False(h.Address.StartsWith("127.")));
            if (main != null && list.Any((h) => h.Address == main)) Assert.Equal(main, list[0].Address);
        }
    }
}
