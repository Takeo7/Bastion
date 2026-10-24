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
                catch (Exception)
                {
                    // Stopped (Dispose), or the listener failed: no more connections.
                    return;
                }
                var connection = new TcpConnection(client, TcpConnection.HostMaxBody, TcpConnection.HostMaxText);
                Connected?.Invoke(connection);
                connection.Start();
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
