using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>
    /// What an app and the relay say before the relay only copies bytes (docs/UNIRSE-CON-CODIGO.md): one
    /// line each way, in ASCII. The host's link asks for a code, or takes its own back after a drop (HOST); a joiner gives
    /// the code (JOIN); the relay tells the host's link (PEER), and the host opens a pipe for that joiner with the ticket
    /// it was told (PIPE). After the «OK» of a JOIN and of its PIPE the two are one: what goes through is the game's own
    /// (Frame), which the relay doesn't read. The relay compiles this file as its own too (BASTION_RELAY).
    /// </summary>
#if BASTION_RELAY
    internal
#else
    public
#endif
    static class RelayWire
    {
        /// <summary>Every first line starts with it and the protocol's version: a relay that moves on tells an old app so (RelayRefusal.Version).</summary>
        public const string Magic = "BASTION-RELE";
        public const int Version = 1;

        /// <summary>No line is longer, a code's signature included.</summary>
        public const int MaxLine = 200;

        /// <summary>The code's symbols: none of 0, O, 1, I, L and U, which are misread.</summary>
        public const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

        public const int CodeLength = 8;

        public const string Host = "HOST";
        public const string Join = "JOIN";
        public const string Pipe = "PIPE";
        public const string Peer = "PEER";
        public const string Ping = "PING";
        public const string Pong = "PONG";
        public const string Bye = "BYE";
        public const string Ok = "OK";
        public const string Err = "ERR";

        /// <summary>An app's first line: the magic, the version, what it comes for and its words.</summary>
        public static string Hello(string verb, params string[] words)
        {
            return string.Join(" ", new[] { Magic, Version.ToString(), verb }.Concat(words));
        }

        /// <summary>A code as typed or pasted, as the relay knows it (8 symbols, capitals, no dash); null if it isn't one.</summary>
        public static string? Normalize(string? text)
        {
            var bare = new string((text ?? "").Where((c) => c != '-' && !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());
            return bare.Length == CodeLength && bare.All((c) => Alphabet.IndexOf(c) >= 0) ? bare : null;
        }

        /// <summary>As shown and typed: two groups of four («KXRT-4MWP»).</summary>
        public static string Show(string code)
        {
            return code.Length == CodeLength ? code.Substring(0, 4) + "-" + code.Substring(4) : code;
        }

        public static async Task WriteLineAsync(Stream stream, string line, CancellationToken cancel)
        {
            var bytes = Encoding.ASCII.GetBytes(line + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length, cancel).ConfigureAwait(false);
            await stream.FlushAsync(cancel).ConfigureAwait(false);
        }

        /// <summary>
        /// The next line, read a byte at a time so as never to take what follows it (the game's, after an «OK»); null if the
        /// stream ended before it. One longer than MaxLine, or not text, throws: whoever sent it is not to be trusted.
        /// </summary>
        public static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancel)
        {
            var line = new StringBuilder();
            var one = new byte[1];
            while (true)
            {
                var read = await stream.ReadAsync(one, 0, 1, cancel).ConfigureAwait(false);
                if (read == 0)
                {
                    if (line.Length == 0) return null;
                    throw new EndOfStreamException("La línea se cortó a medias.");
                }
                var b = one[0];
                if (b == '\n') return line.ToString().TrimEnd('\r');
                if ((b < 0x20 && b != '\r') || b > 0x7e) throw new InvalidDataException("No es una línea de texto.");
                if (line.Length >= MaxLine) throw new InvalidDataException("Línea demasiado larga.");
                line.Append((char)b);
            }
        }

        /// <summary>
        /// A certificate's key as RelayAddress keeps it: the SHA-256 of its public key, in hex (the relay prints its own
        /// with --crear-certificado). The key, not the certificate: one made again with the same key still passes.
        /// </summary>
        public static string KeyOf(X509Certificate cert)
        {
            using var sha = SHA256.Create();
            using var full = new X509Certificate2(cert);
            return string.Concat(sha.ComputeHash(full.GetPublicKey()).Select((b) => b.ToString("x2")));
        }

        /// <summary>The refusal in an «ERR …» answer, or Bad for anything else that isn't the «OK» expected.</summary>
        public static string Refusal(string? answer)
        {
            var words = (answer ?? "").Split(' ');
            return words.Length == 2 && words[0] == Err ? words[1] : RelayRefusal.Bad;
        }
    }

    /// <summary>
    /// Why the relay says no («ERR …»): a word the app turns into Lore's text, so a text never needs the relay changed.
    /// </summary>
#if BASTION_RELAY
    internal
#else
    public
#endif
    static class RelayRefusal
    {
        /// <summary>The relay speaks another version of this protocol: the app has to be updated.</summary>
        public const string Version = "version";
        /// <summary>No game has that code (it was never given, or its host closed it, or it ran out).</summary>
        public const string Unknown = "unknown";
        /// <summary>The code's host has lost its link to the relay for now: it comes back on its own.</summary>
        public const string Away = "away";
        /// <summary>Too many tries, codes or bytes from here: later.</summary>
        public const string Limit = "limit";
        /// <summary>The relay, or the game, has no room for one more.</summary>
        public const string Full = "full";
        /// <summary>The host didn't open the pipe in time.</summary>
        public const string Timeout = "timeout";
        /// <summary>A line the relay doesn't understand, or a signature that isn't its own.</summary>
        public const string Bad = "bad";
    }
}
