using System;
using System.IO;
using System.Net;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>What a player's app keeps (Net/NetSettings.cs), the addresses it is given, and the host's port.</summary>
    public class NetSettingsTests
    {
        [Theory]
        [InlineData("192.168.1.20", "192.168.1.20", RoomHost.FirstPort)]
        [InlineData("  192.168.1.20:41392 ", "192.168.1.20", 41392)]
        [InlineData("http://100.116.6.72:41391/", "100.116.6.72", 41391)]
        [InlineData("mac-de-arturo.local", "mac-de-arturo.local", RoomHost.FirstPort)]
        public void AnAddressIsReadAsTypedOrPasted(string text, string host, int port)
        {
            Assert.True(GameAddress.TryParse(text, out var h, out var p));
            Assert.Equal((host, port), (h, p));
        }

        [Theory]
        [InlineData("")]
        [InlineData("192.168.1.20:")]
        [InlineData("192.168.1.20:99999")]
        [InlineData("dos palabras")]
        public void ANonAddressIsNot(string text)
        {
            Assert.False(GameAddress.TryParse(text, out _, out _));
        }

        [Fact]
        public void TheNameTheAddressAndATokenPerHostAreKept()
        {
            var dir = Path.Combine(Path.GetTempPath(), "bastion-ajustes-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "conexion.json");
            try
            {
                var settings = NetSettings.Load(path);
                Assert.Null(settings.Name);
                settings.Name = "Ana";
                settings.Last = "192.168.1.20:41391";
                settings.Save();
                settings.TokensFor("192.168.1.20:41391").Save("ficha-de-arturo");
                settings.TokensFor("127.0.0.1:41391").Save("ficha-propia");
                var again = NetSettings.Load(path);
                Assert.Equal("Ana", again.Name);
                Assert.Equal("192.168.1.20:41391", again.Last);
                Assert.Equal("ficha-de-arturo", again.TokensFor("192.168.1.20:41391").Load());
                Assert.Equal("ficha-propia", again.TokensFor("127.0.0.1:41391").Load());
                Assert.Null(again.TokensFor("10.0.0.9:41391").Load());
                // An unreadable file is a fresh start.
                File.WriteAllText(path, "{ a medias");
                Assert.Null(NetSettings.Load(path).Name);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void AHostTakesTheNextPortIfOneIsTaken()
        {
            var dir = Path.Combine(Path.GetTempPath(), "bastion-puerto-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var first = new TcpHost(0, IPAddress.Loopback);
                first.Start();
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json")), new FakeRules(), "1.0.0");
                using var host = RoomHost.StartFree(room, first.Port, 10, IPAddress.Loopback);
                Assert.NotEqual(first.Port, host.Port);
                Assert.InRange(host.Port, first.Port + 1, first.Port + 9);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
