using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>
    /// Where the players' apps connect (docs/UNITY.md, Red): a TCP port on every address of the host's machine, so a
    /// partner reaches it on the local network or by Tailscale, and the host's own app by 127.0.0.1, as one more. Each
    /// connection comes out of `Connected` already started.
    /// </summary>
    public sealed class TcpHost : IDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();

        /// <summary>A connection opened: listen to its events before anything else (it is started right after).</summary>
        public event Action<TcpConnection>? Connected;

        /// <summary>A connection that failed as it came, or an accept that failed: what, for red.log. The port goes on listening.</summary>
        public event Action<string>? Failed;

        /// <summary>The port it listens on (the one asked for, or the one the system gave for 0).</summary>
        public int Port { get; private set; }

        /// <summary>On `port` (0: any free one), on every address unless `address` says one.</summary>
        public TcpHost(int port, IPAddress? address = null)
        {
            listener = new TcpListener(address ?? IPAddress.Any, port);
        }

        /// <summary>Starts listening; throws if the port is taken.</summary>
        public void Start()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptLoop);
        }

        private async Task AcceptLoop()
        {
            while (!cancel.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Stopped (Dispose, or a listener no longer listening): no more connections. Anything else (one reset
                    // before it was taken, the system out of sockets for a moment) is that one's: noted, and the next is
                    // taken after a breath.
                    if (cancel.IsCancellationRequested || e is ObjectDisposedException || e is InvalidOperationException) return;
                    Note($"no se pudo aceptar una conexión: {e.GetType().Name}: {e.Message}");
                    try
                    {
                        await Task.Delay(100, cancel.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    continue;
                }
                Take(client);
            }
        }

        /// <summary>
        /// One connection, out of `Connected` and started. If it fails as it comes (gone before its address could be read,
        /// a listener that threw), only that one goes: closed, noted, and the port takes the next (once a connection cut
        /// as it arrived left the host taking nobody else).
        /// </summary>
        private void Take(TcpClient client)
        {
            TcpConnection? connection = null;
            try
            {
                connection = new TcpConnection(client, TcpConnection.HostMaxBody, TcpConnection.HostMaxText);
                Connected?.Invoke(connection);
                connection.Start();
            }
            catch (Exception e)
            {
                Note($"conexión perdida al llegar ({connection?.Peer ?? "?"}): {e.GetType().Name}: {e.Message}");
                try
                {
                    // Handed out already, its Closed tells whoever took it; if not, the socket alone.
                    if (connection != null) connection.Close("perdida al llegar");
                    else client.Dispose();
                }
                catch (Exception)
                {
                    /* already gone */
                }
            }
        }

        private void Note(string line)
        {
            try
            {
                Failed?.Invoke(line);
            }
            catch (Exception)
            {
                /* the log is a help: it never stops the port */
            }
        }

        /// <summary>Stops listening. The connections already open stay; whoever holds them closes them.</summary>
        public void Dispose()
        {
            cancel.Cancel();
            listener.Stop();
        }
    }
}
