using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>
    /// How messages travel on a socket (docs/UNITY.md, Red): each frame is the length of what follows the header
    /// (4 bytes, big-endian), a byte of flags, and the bytes. A message is JSON in UTF-8, deflated when it is large (a
    /// mission's state goes from ~105 KB to ~7 KB, as permessage-deflate did on the WebSocket); the heartbeat's ping and
    /// pong carry nothing.
    /// </summary>
    public static class Frame
    {
        /// <summary>The body is deflated JSON.</summary>
        public const byte Deflated = 1;
        /// <summary>The heartbeat asks: answer with a pong.</summary>
        public const byte Ping = 2;
        /// <summary>The heartbeat's answer.</summary>
        public const byte Pong = 4;

        /// <summary>Messages from this size up go deflated; the small ones of each order are not worth it.</summary>
        public const int DeflateFrom = 1024;

        private const int HeaderSize = 5;

        /// <summary>A message's frame: deflated when it is large.</summary>
        public static byte[] Encode(string json)
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            if (raw.Length < DeflateFrom) return Wrap(0, raw, raw.Length);
            using var packed = new MemoryStream();
            // Fastest: nearly as small as the default, and cheaper (as the WebSocket's level 1 was).
            using (var deflate = new DeflateStream(packed, CompressionLevel.Fastest, true)) deflate.Write(raw, 0, raw.Length);
            return Wrap(Deflated, packed.GetBuffer(), (int)packed.Length);
        }

        /// <summary>A heartbeat frame (Ping or Pong): no body.</summary>
        public static byte[] Control(byte flag)
        {
            return Wrap(flag, Array.Empty<byte>(), 0);
        }

        private static byte[] Wrap(byte flags, byte[] body, int length)
        {
            var frame = new byte[HeaderSize + length];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            frame[4] = flags;
            Buffer.BlockCopy(body, 0, frame, HeaderSize, length);
            return frame;
        }

        /// <summary>
        /// The next frame from `stream`, or null when the other end closed it cleanly between frames. A frame longer than
        /// `maxBody`, or one cut off midway, throws: the connection is not to be trusted after it.
        /// </summary>
        public static async Task<Packet?> ReadAsync(Stream stream, int maxBody, CancellationToken cancel)
        {
            var header = new byte[HeaderSize];
            if (!await FillAsync(stream, header, HeaderSize, true, cancel)) return null;
            int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            if (length < 0 || length > maxBody) throw new InvalidDataException($"Mensaje demasiado grande ({length} bytes).");
            var body = new byte[length];
            await FillAsync(stream, body, length, false, cancel);
            return new Packet(header[4], body);
        }

        /// <summary>
        /// A message's JSON from its frame's body, inflated if need be; never more than `maxText` bytes once inflated
        /// (a small deflated frame must not swell into a huge one).
        /// </summary>
        public static string Decode(Packet packet, int maxText)
        {
            if ((packet.Flags & Deflated) == 0) return Encoding.UTF8.GetString(packet.Body);
            using var source = new MemoryStream(packet.Body);
            using var inflate = new DeflateStream(source, CompressionMode.Decompress);
            using var text = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = inflate.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (text.Length + read > maxText) throw new InvalidDataException("Mensaje demasiado grande al descomprimirlo.");
                text.Write(chunk, 0, read);
            }
            return Encoding.UTF8.GetString(text.GetBuffer(), 0, (int)text.Length);
        }

        /// <summary>
        /// Reads exactly `count` bytes into `buffer`. False if the stream ended before the first byte and that is
        /// allowed (`endOk`, between frames); an end anywhere else throws.
        /// </summary>
        private static async Task<bool> FillAsync(Stream stream, byte[] buffer, int count, bool endOk, CancellationToken cancel)
        {
            int done = 0;
            while (done < count)
            {
                int read = await stream.ReadAsync(buffer, done, count - done, cancel).ConfigureAwait(false);
                if (read == 0)
                {
                    if (done == 0 && endOk) return false;
                    throw new EndOfStreamException("La conexión se cortó a mitad de un mensaje.");
                }
                done += read;
            }
            return true;
        }
    }

    /// <summary>One frame as read: its flags and its body (deflated or not).</summary>
    public readonly struct Packet
    {
        public readonly byte Flags;
        public readonly byte[] Body;

        public Packet(byte flags, byte[] body)
        {
            Flags = flags;
            Body = body;
        }
    }
}
