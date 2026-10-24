using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>The frames on the wire (Net/Frame.cs): length, flags, deflate, and what is refused.</summary>
    public class NetFrameTests
    {
        private static async Task<Packet?> ReadOne(byte[] wire, int maxBody = 1 << 20)
        {
            return await Frame.ReadAsync(new MemoryStream(wire), maxBody, CancellationToken.None);
        }

        [Fact]
        public async Task SmallMessagesGoAsTheyAre()
        {
            var wire = Frame.Encode("{\"t\":\"ping\"}");
            Assert.Equal(5 + 12, wire.Length);
            var packet = (await ReadOne(wire))!.Value;
            Assert.Equal(0, packet.Flags);
            Assert.Equal("{\"t\":\"ping\"}", Frame.Decode(packet, 1 << 20));
        }

        [Fact]
        public async Task LargeMessagesGoDeflatedAndComeBackTheSame()
        {
            // A mission's state is long and repetitive JSON: it shrinks a lot.
            var json = "{\"t\":\"snapshot\",\"tiles\":[" + string.Join(",", Enumerable.Range(0, 20_000).Select((i) => (i % 7).ToString())) + "]}";
            var wire = Frame.Encode(json);
            Assert.True(wire.Length < json.Length / 4, $"deflated to {wire.Length} of {json.Length}");
            var packet = (await ReadOne(wire))!.Value;
            Assert.Equal(Frame.Deflated, packet.Flags);
            Assert.Equal(json, Frame.Decode(packet, 1 << 20));
        }

        [Fact]
        public async Task HeartbeatFramesCarryNothing()
        {
            var packet = (await ReadOne(Frame.Control(Frame.Ping)))!.Value;
            Assert.Equal(Frame.Ping, packet.Flags);
            Assert.Empty(packet.Body);
        }

        [Fact]
        public async Task TheEndBetweenFramesIsAClose()
        {
            Assert.Null(await ReadOne(Array.Empty<byte>()));
        }

        [Fact]
        public async Task AFrameCutMidwayThrows()
        {
            var wire = Frame.Encode("{\"t\":\"hello\",\"name\":\"Ana\"}");
            await Assert.ThrowsAsync<EndOfStreamException>(() => ReadOne(wire.AsSpan(0, wire.Length - 3).ToArray()));
        }

        [Fact]
        public async Task AFrameOverTheLimitIsRefused()
        {
            var wire = Frame.Encode(new string('x', 900));
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadOne(wire, 800));
        }

        [Fact]
        public async Task ASmallDeflatedFrameMayNotSwellPastTheLimit()
        {
            // Two megabytes of the same letter deflate to a few kilobytes: inflated, over the limit.
            var wire = Frame.Encode(new string('a', 2 * 1024 * 1024));
            Assert.True(wire.Length < 16 * 1024);
            var packet = (await ReadOne(wire))!.Value;
            Assert.Throws<InvalidDataException>(() => Frame.Decode(packet, 256 * 1024));
        }

        [Fact]
        public async Task TextIsUtf8()
        {
            var json = "{\"name\":\"Ñandú Pérez · 🧟\"}";
            var packet = (await ReadOne(Frame.Encode(json)))!.Value;
            Assert.Equal(json, Frame.Decode(packet, 1 << 20));
            Assert.Equal(Encoding.UTF8.GetByteCount(json), packet.Body.Length);
        }
    }
}
