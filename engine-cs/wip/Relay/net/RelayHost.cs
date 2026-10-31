using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bastion.Net
{
    /// <summary>Where the host's link to the relay stands, for its menu.</summary>
    public enum RelayState
    {
        /// <summary>Asking for its first code.</summary>
        Connecting,
        /// <summary>The code works: the partner joins with it.</summary>
        Ready,
        /// <summary>The relay doesn't answer, or the link dropped: trying again. A code it had stays its own.</summary>
        Unreachable,
        /// <summary>The relay speaks a newer protocol: this app has to be updated. It doesn't try again.</summary>
        Outdated,
    }

    /// <summary>
    /// The host's side of «Unirse con código» (docs/UNIRSE-CON-CODIGO.md): a link out to the relay that holds the game's code, and
    /// for each joiner the relay announces, a pipe out to it, handed over as the TCP host's connections are (Connected,
    /// started right after). The link beats every PingMs and comes back on its own, with the same code (its signature
    /// says it is the host's, even to a relay restarted since); a code that ran out is replaced by a new one (Changed).
    /// Dispose says BYE: the code ends, and the pipes already open go on till their ends close them.
    /// </summary>
    public sealed class RelayHost : IDisposable
    {
        public const int PingMs = 20_000;

        /// <summary>Nothing heard on the link for this long (its pongs included): the relay is gone.</summary>
        public const int DeadAfterMs = 60_000;

        private const int MaxBackoffMs = 30_000;

        /// <summary>The BYE has this long to go out on Dispose.</summary>
        private const int ByeMs = 500;

        private readonly RelayAddress relay;
        private readonly Action<string> log;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly object gate = new object();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly SemaphoreSlim writing = new SemaphoreSlim(1, 1);
        private string? code;
        private string? issued;
        private string? signature;
        private RelayState state = RelayState.Connecting;
        private TcpClient? link;
        private Stream? linkStream;
        private int disposed;

        /// <summary>A joiner's pipe, open: listen to its events before anything else (it is started right after).</summary>
        public event Action<TcpConnection>? Connected;

        /// <summary>Code or State changed (on a network thread).</summary>
        public event Action? Changed;

        /// <summary>The game's code, as the relay knows it (RelayWire.Show for the screen); null till the first is given.</summary>
        public string? Code
        {
            get
            {
                lock (gate) return code;
            }
        }

        public RelayState State
        {
            get
            {
                lock (gate) return state;
            }
        }

        public RelayHost(RelayAddress relay, Action<string>? log = null)
        {
            this.relay = relay;
            this.log = log ?? ((_) => { });
        }

        public void Start()
        {
            _ = Task.Run(Run);
        }

        private async Task Run()
        {
            var retry = 0;
            while (!cancel.IsCancellationRequested)
            {
                string why;
                try
                {
                    string? had;
                    string first;
                    lock (gate)
                    {
                        had = code;
                        first = code == null ? RelayWire.Hello(RelayWire.Host) : RelayWire.Hello(RelayWire.Host, code, issued!, signature!);
                    }
                    var (client, stream, answer) = await RelayDial.GreetAsync(relay, first, cancel.Token).ConfigureAwait(false);
                    var words = answer.Split(' ');
                    if (words.Length == 4 && words[0] == RelayWire.Ok && RelayWire.Normalize(words[1]) == words[1])
                    {
                        retry = 0;
                        Set(words[1], words[2], words[3], RelayState.Ready);
                        log(had == words[1] ? $"[rele] {relay}: de vuelta con el código {RelayWire.Show(words[1])}" : $"[rele] {relay}: código {RelayWire.Show(words[1])}");
                        await Serve(client, stream).ConfigureAwait(false);
                        why = "se cortó el enlace";
                    }
                    else
                    {
                        client.Dispose();
                        var refusal = RelayWire.Refusal(answer);
                        if (refusal == RelayRefusal.Version)
                        {
                            log($"[rele] {relay}: el servidor intermedio pide una versión más nueva de la app");
                            Set(null, null, null, RelayState.Outdated);
                            return;
                        }
                        if (refusal == RelayRefusal.Unknown && had != null)
                        {
                            // Its code ran out (or isn't the relay's): a new one, at once.
                            log($"[rele] {relay}: el código {RelayWire.Show(had)} ya no vale; se pide otro");
                            Set(null, null, null, RelayState.Connecting);
                            continue;
                        }
                        why = $"no deja ({refusal})";
                    }
                }
                catch (Exception e)
                {
                    why = e.Message;
                }
                if (cancel.IsCancellationRequested) return;
                lock (gate) state = RelayState.Unreachable;
                Changed?.Invoke();
                var delay = Math.Min(MaxBackoffMs, 1000 << Math.Min(retry, 5));
                retry++;
                log($"[rele] {relay}: {why}; otra vez en {delay / 1000} s");
                try
                {
                    await Task.Delay(delay, cancel.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void Set(string? newCode, string? newIssued, string? newSignature, RelayState newState)
        {
            lock (gate)
            {
                code = newCode;
                issued = newIssued;
                signature = newSignature;
                state = newState;
            }
            Changed?.Invoke();
        }

        /// <summary>The link, till it drops: the relay's PEERs answered with pipes, a PING every PingMs, and a silent relay given up.</summary>
        private async Task Serve(TcpClient client, Stream stream)
        {
            lock (gate)
            {
                link = client;
                linkStream = stream;
            }
            long heardAt = clock.ElapsedMilliseconds;
            using var done = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            var beat = Task.Run(async () =>
            {
                try
                {
                    while (!done.IsCancellationRequested)
                    {
                        await Task.Delay(PingMs, done.Token).ConfigureAwait(false);
                        if (clock.ElapsedMilliseconds - Volatile.Read(ref heardAt) > DeadAfterMs)
                        {
                            client.Close();
                            return;
                        }
                        await Write(stream, RelayWire.Ping, done.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    /* the link is gone: the read below finds out */
                    client.Close();
                }
            });
            try
            {
                while (true)
                {
                    var line = await RelayWire.ReadLineAsync(stream, done.Token).ConfigureAwait(false);
                    if (line == null) return;
                    Volatile.Write(ref heardAt, clock.ElapsedMilliseconds);
                    var words = line.Split(' ');
                    if (words.Length == 3 && words[0] == RelayWire.Peer) _ = Task.Run(() => OpenPipe(words[1], words[2]));
                }
            }
            finally
            {
                done.Cancel();
                client.Close();
                lock (gate)
                {
                    if (link == client)
                    {
                        link = null;
                        linkStream = null;
                    }
                }
            }
        }

        private async Task Write(Stream stream, string line, CancellationToken token)
        {
            await writing.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await RelayWire.WriteLineAsync(stream, line, token).ConfigureAwait(false);
            }
            finally
            {
                writing.Release();
            }
        }

        /// <summary>A pipe for the joiner the relay announced with `ticket`, from `who` (their address, for red.log).</summary>
        private async Task OpenPipe(string ticket, string who)
        {
            try
            {
                var (client, stream, answer) = await RelayDial.GreetAsync(relay, RelayWire.Hello(RelayWire.Pipe, ticket), cancel.Token).ConfigureAwait(false);
                if (answer != RelayWire.Ok)
                {
                    client.Dispose();
                    log($"[rele] {relay}: el empalme para {who} no se abre ({RelayWire.Refusal(answer)})");
                    return;
                }
                var connection = new TcpConnection(client, stream, $"{who} por código", TcpConnection.HostMaxBody, TcpConnection.HostMaxText) { ByCode = true };
                Connected?.Invoke(connection);
                connection.Start();
            }
            catch (Exception e)
            {
                if (!cancel.IsCancellationRequested) log($"[rele] {relay}: el empalme para {who} no se abre: {e.Message}");
            }
        }

        /// <summary>The link closed, BYE said first: the code ends now, not when the relay finds the link silent.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            TcpClient? client;
            Stream? stream;
            lock (gate)
            {
                client = link;
                stream = linkStream;
            }
            if (stream != null)
            {
                try
                {
                    using var bye = new CancellationTokenSource(ByeMs);
                    Write(stream, RelayWire.Bye, bye.Token).Wait(ByeMs);
                }
                catch (Exception)
                {
                    /* gone already */
                }
            }
            cancel.Cancel();
            client?.Close();
        }
    }
}
