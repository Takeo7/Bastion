using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>
    /// One end of a connection between the host's room and a player's app: JSON messages each way. Over TCP in the game
    /// (TcpConnection); in memory in the room's tests. Its events come on a network thread: whoever listens moves them
    /// to their own (the room's queue, the client's Update).
    /// </summary>
    public interface IConnection
    {
        /// <summary>Who is at the other end, for the log: an address and port.</summary>
        string Peer { get; }

        /// <summary>Sends a message; never blocks on the network. Nothing happens once closed.</summary>
        void Send(string json);

        /// <summary>Closes it, once; `Closed` says why. What was sent before still goes out (a `replaced`, a `closing`).</summary>
        void Close(string reason);

        /// <summary>A message arrived.</summary>
        event Action<string>? Message;

        /// <summary>It closed, from either end or because the other stopped answering; once, with why.</summary>
        event Action<string>? Closed;
    }

    /// <summary>
    /// A connection over TCP (Frame on the wire). It reads and writes on its own tasks, so a slow or stuck peer never holds
    /// up whoever sends; it beats every HeartbeatMs, and one that hears nothing for DeadAfterMs takes the other for gone
    /// (a laptop shut, the Wi-Fi lost), as the WebSocket's ping did: DeadAfterMs of its own time awake, not the time it
    /// was held up itself (BeatLoop).
    /// </summary>
    public sealed class TcpConnection : IConnection
    {
        /// <summary>A ping this often.</summary>
        public const int HeartbeatMs = 15_000;

        /// <summary>Nothing heard for this long, from either a message or a pong: the other end is gone.</summary>
        public const int DeadAfterMs = 30_000;

        /// <summary>Waiting to be written beyond this, the peer is not reading: it is closed rather than letting it pile up.</summary>
        private const long MaxPending = 32L * 1024 * 1024;

        /// <summary>Closed from this end, what was queued has this long at most to go out.</summary>
        public const int DrainMs = 1000;

        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly int maxBody;
        private readonly int maxText;
        private readonly int heartbeatMs;
        private readonly int deadAfterMs;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim waiting = new SemaphoreSlim(0);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly TaskCompletionSource<bool> drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private long heardAt;
        private long pending;
        private int closed;

        public string Peer { get; }
        public event Action<string>? Message;
        public event Action<string>? Closed;

        /// <summary>Done once the socket is shut: at once if the other end went; closed from this one, once what was queued went out.</summary>
        public Task Ended => ended.Task;

        /// <summary>
        /// Over an open `client`. `maxBody`: the largest frame accepted on the wire; `maxText`, the largest message once
        /// inflated. The host takes little from players (64 KB, as the WebSocket did); a player takes a whole mission.
        /// </summary>
        public TcpConnection(TcpClient client, int maxBody, int maxText, int heartbeatMs = HeartbeatMs, int deadAfterMs = DeadAfterMs)
        {
            this.client = client;
            this.maxBody = maxBody;
            this.maxText = maxText;
            this.heartbeatMs = heartbeatMs;
            this.deadAfterMs = deadAfterMs;
            client.NoDelay = true;
            stream = client.GetStream();
            Peer = client.Client.RemoteEndPoint?.ToString() ?? "?";
        }

        /// <summary>
        /// Connects to `host`:`port`, giving up after `timeoutMs`. For a player's app: messages up to a whole mission.
        /// </summary>
        public static async Task<TcpConnection> ConnectAsync(string host, int port, int timeoutMs, CancellationToken cancel)
        {
            var client = new TcpClient();
            try
            {
                var connect = client.ConnectAsync(host, port);
                var done = await Task.WhenAny(connect, Task.Delay(timeoutMs, cancel)).ConfigureAwait(false);
                if (done != connect) throw new TimeoutException($"No responde nadie en {host}:{port}.");
                await connect.ConfigureAwait(false);
                return new TcpConnection(client, ClientMaxBody, ClientMaxText);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>What a player's app accepts from the host: a whole mission and more.</summary>
        public const int ClientMaxBody = 16 * 1024 * 1024;
        public const int ClientMaxText = 64 * 1024 * 1024;

        /// <summary>What the host accepts from a player: orders are small (the WebSocket's limit was 64 KB).</summary>
        public const int HostMaxBody = 64 * 1024;
        public const int HostMaxText = 256 * 1024;

        /// <summary>Starts reading, writing and beating. Listen to its events first.</summary>
        public void Start()
        {
            heardAt = clock.ElapsedMilliseconds;
            _ = Task.Run(ReadLoop);
            _ = Task.Run(WriteLoop);
            _ = Task.Run(BeatLoop);
        }

        public void Send(string json)
        {
            Enqueue(Frame.Encode(json));
        }

        private void Enqueue(byte[] frame)
        {
            if (Volatile.Read(ref closed) != 0) return;
            if (Interlocked.Add(ref pending, frame.Length) > MaxPending)
            {
                Close("no lee lo que se le manda");
                return;
            }
            outgoing.Enqueue(frame);
            waiting.Release();
        }

        public void Close(string reason)
        {
            Shut(reason, true);
        }

        /// <summary>Closed, once: nothing more is sent or heard. `drain`: what was queued still goes out first, for DrainMs at most.</summary>
        private void Shut(string reason, bool drain)
        {
            if (Interlocked.Exchange(ref closed, 1) != 0) return;
            if (drain && !outgoing.IsEmpty)
            {
                waiting.Release();
                _ = Task.Run(async () =>
                {
                    await Task.WhenAny(drained.Task, Task.Delay(DrainMs)).ConfigureAwait(false);
                    Kill();
                });
            }
            else Kill();
            Closed?.Invoke(reason);
        }

        private void Kill()
        {
            cancel.Cancel();
            try
            {
                client.Close();
            }
            catch (Exception)
            {
                /* already gone */
            }
            ended.TrySetResult(true);
        }

        private async Task ReadLoop()
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var packet = await Frame.ReadAsync(stream, maxBody, cancel.Token).ConfigureAwait(false);
                    if (packet == null)
                    {
                        Shut("cerrada", false);
                        return;
                    }
                    Volatile.Write(ref heardAt, clock.ElapsedMilliseconds);
                    var p = packet.Value;
                    if ((p.Flags & Frame.Ping) != 0) Enqueue(Frame.Control(Frame.Pong));
                    else if ((p.Flags & Frame.Pong) == 0 && Volatile.Read(ref closed) == 0) Message?.Invoke(Frame.Decode(p, maxText));
                }
            }
            catch (Exception e)
            {
                // The network's own failures, a frame not to be trusted, or a listener that threw: the connection goes.
                Shut(e.Message, false);
            }
        }

        private async Task WriteLoop()
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    await waiting.WaitAsync(cancel.Token).ConfigureAwait(false);
                    if (outgoing.TryDequeue(out var frame))
                    {
                        await stream.WriteAsync(frame, 0, frame.Length, cancel.Token).ConfigureAwait(false);
                        Interlocked.Add(ref pending, -frame.Length);
                    }
                    // Closed from this end: once the last of it is out, the socket can go.
                    if (Volatile.Read(ref closed) != 0 && outgoing.IsEmpty)
                    {
                        drained.TrySetResult(true);
                        return;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException || e is OperationCanceledException)
            {
                drained.TrySetResult(true);
                Shut(e.Message, false);
            }
        }

        private async Task BeatLoop()
        {
            var beat = Math.Min(heartbeatMs, deadAfterMs / 2);
            // How long this end was held up itself since it last heard the other, which isn't the other's silence.
            long held = 0;
            long heardSeen = Volatile.Read(ref heardAt);
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var slept = clock.ElapsedMilliseconds;
                    await Task.Delay(beat, cancel.Token).ConfigureAwait(false);
                    var now = clock.ElapsedMilliseconds;
                    var heard = Volatile.Read(ref heardAt);
                    if (heard != heardSeen)
                    {
                        heardSeen = heard;
                        held = 0;
                    }
                    // Woken more than a beat late, this end was held up itself (a loaded machine, a pause of the
                    // collector): its reading as much as this, so what the other sent may be waiting unread, and its
                    // pings went late. A beat adds two beats' time to the silence at most, so a peer gone quiet is still
                    // found, only later. (A test with both ends in one process once lost them for 1.3 s.)
                    held += Math.Max(0, now - slept - 2 * beat);
                    if (now - heard - held > deadAfterMs && !Unread())
                    {
                        Shut("no responde", false);
                        return;
                    }
                    Enqueue(Frame.Control(Frame.Ping));
                }
            }
            catch (OperationCanceledException)
            {
                /* closed */
            }
        }

        /// <summary>Bytes from the other end waiting to be read (the reading was held up while they came): it is there.</summary>
        private bool Unread()
        {
            try
            {
                return client.Available > 0;
            }
            catch (Exception e) when (e is ObjectDisposedException || e is SocketException)
            {
                return false;
            }
        }
    }
}
