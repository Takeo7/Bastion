using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>
    /// Where the relay is (docs/UNIRSE-CON-CODIGO.md): its address and port, and the keys its certificate
    /// may have. With keys, TLS checked against them only (our own certificate, no domain and nothing to renew); none, no
    /// TLS: the local tests' relay on 127.0.0.1.
    /// </summary>
    public sealed class RelayAddress
    {
        public string Host { get; }
        public int Port { get; }
        public IReadOnlyList<string> Keys { get; }

        public RelayAddress(string host, int port, params string[] keys)
        {
            Host = host;
            Port = port;
            Keys = keys;
        }

        /// <summary>«host:port», as BASTION_RELE gives it to the test apps: no TLS.</summary>
        public static RelayAddress? Parse(string? text)
        {
            if (!GameAddress.TryParse(text, out var host, out var port) || !(text ?? "").Contains(":")) return null;
            return new RelayAddress(host, port);
        }

        public override string ToString()
        {
            return $"{Host}:{Port}";
        }
    }

    /// <summary>The relay said no, and why (RelayRefusal).</summary>
    public sealed class RelayRefusedException : Exception
    {
        public string Reason { get; }

        public RelayRefusedException(string reason)
            : base($"El servidor intermedio no deja ({reason}).")
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// The connections that go through the relay: a joiner's, by the code (JoinAsync), and the host's own (RelayHost), each
    /// a socket out to it greeted with its first line. Nothing listens on this computer: nothing for a firewall to ask.
    /// </summary>
    public static class RelayDial
    {
        /// <summary>Connecting to the relay gives up after this.</summary>
        public const int ConnectMs = 4000;

        /// <summary>
        /// Its answer, after this: a joiner's waits for the host to open the pipe (the relay gives it 10 s), and TLS's
        /// handshake goes in it too.
        /// </summary>
        public const int AnswerMs = 15_000;

        /// <summary>
        /// Into the game with code `code` (RelayWire.Normalize's): the connection to its host, as the TCP one, a player's
        /// size. The relay's no throws RelayRefusedException, with why.
        /// </summary>
        public static async Task<TcpConnection> JoinAsync(RelayAddress relay, string code, CancellationToken cancel)
        {
            var (client, stream, answer) = await GreetAsync(relay, RelayWire.Hello(RelayWire.Join, code), cancel).ConfigureAwait(false);
            if (answer == RelayWire.Ok) return new TcpConnection(client, stream, $"{relay} · código {RelayWire.Show(code)}", TcpConnection.ClientMaxBody, TcpConnection.ClientMaxText);
            client.Dispose();
            throw new RelayRefusedException(RelayWire.Refusal(answer));
        }

        /// <summary>
        /// A socket to the relay, TLS if its keys are known, greeted with `first`; the relay's answer read. The socket, the
        /// stream to go on with (the TLS one, if so) and the answer; the socket closed if anything fails.
        /// </summary>
        internal static async Task<(TcpClient client, Stream stream, string answer)> GreetAsync(RelayAddress relay, string first, CancellationToken cancel)
        {
            var client = new TcpClient();
            using var late = new CancellationTokenSource();
            try
            {
                var connect = client.ConnectAsync(relay.Host, relay.Port);
                var done = await Task.WhenAny(connect, Task.Delay(ConnectMs, cancel)).ConfigureAwait(false);
                if (done != connect) throw new TimeoutException($"No responde el servidor intermedio ({relay}).");
                await connect.ConfigureAwait(false);
                client.NoDelay = true;
                // Whatever is still unanswered then (TLS, the answer) ends with the socket closed: Mono's reads don't
                // always heed a cancellation once started, a closed socket they do.
                late.CancelAfter(AnswerMs);
                using var closing = late.Token.Register(() => client.Close());
                Stream stream = client.GetStream();
                if (relay.Keys.Count > 0)
                {
                    var tls = new SslStream(stream, false, (_, cert, __, ___) => cert != null && relay.Keys.Contains(RelayWire.KeyOf(cert)));
                    await tls.AuthenticateAsClientAsync(relay.Host).ConfigureAwait(false);
                    stream = tls;
                }
                await RelayWire.WriteLineAsync(stream, first, cancel).ConfigureAwait(false);
                var answer = await RelayWire.ReadLineAsync(stream, cancel).ConfigureAwait(false);
                if (late.IsCancellationRequested) throw new TimeoutException($"El servidor intermedio no contesta ({relay}).");
                return (client, stream, answer ?? "");
            }
            catch (Exception e)
            {
                client.Dispose();
                if (late.IsCancellationRequested && !cancel.IsCancellationRequested && !(e is TimeoutException)) throw new TimeoutException($"El servidor intermedio no contesta ({relay}).", e);
                throw;
            }
        }
    }
}
