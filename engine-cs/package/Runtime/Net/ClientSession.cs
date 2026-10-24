using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Bastion.Net
{
    /// <summary>Where the connection to the host stands, for the screens.</summary>
    public enum NetStatus
    {
        Connecting,
        Open,
        Closed,
    }

    /// <summary>Where a player's seat token is kept between sessions (a file in the app's data folder; in memory in the tests).</summary>
    public interface ITokenStore
    {
        string? Load();
        void Save(string token);
    }

    /// <summary>
    /// A player's connection to the host (client net.ts): the seat asked for by name and token, and kept. It reconnects on
    /// its own, half a second at first and up to five seconds apart. While the game is out of sight (Asleep) the host
    /// hears it, so the turn and the command wait for it (room.ts); back (Awake), the socket is tried at once and replaced
    /// if it does not answer, «Volviendo a la partida…» meanwhile. A host that closed, a seat opened elsewhere or a hello
    /// refused stop it: coming back on its own would only be refused again.
    ///
    /// Nothing happens on the network threads but reading what arrives (a whole mission takes Mono some 15–30 ms: never on
    /// the frame) and queueing it: Pump, called often on the caller's thread (Unity's Update), runs the timers and raises
    /// every event there.
    /// </summary>
    public sealed class ClientSession
    {
        /// <summary>Back to the window, how long the old socket has to show it is alive before a new one is opened.</summary>
        public const int ProbeMs = 2500;

        private readonly Func<CancellationToken, Task<IConnection>> connect;
        private readonly ITokenStore tokens;
        private readonly string version;
        private readonly IClock clock;
        private readonly Func<string, ServerMessage> read;
        private readonly ConcurrentQueue<Action> inbox = new ConcurrentQueue<Action>();

        private IConnection? socket;
        private bool socketOpen;
        private bool connecting;
        private bool welcomed;
        private string name = "";
        private int retry;
        private long retryAt = -1;
        private bool stopped = true;
        private long hiddenAt;
        private long heardAt;
        private long probeAt = -1;
        private long probeAsked;
        private bool rejoining;
        private CancellationTokenSource? attempt;

        /// <summary>Every message from the host, already read, the session's own handling done (welcome, closing…).</summary>
        public event Action<ServerMessage>? Message;

        /// <summary>A message from the host that could not be read (a newer host's, or broken), and why: for red.log.</summary>
        public event Action<string>? Unreadable;

        public event Action<NetStatus>? Status;

        /// <summary>Back from out of sight, a new socket is on its way (true) or in (false): «Volviendo a la partida…».</summary>
        public event Action<bool>? Rejoining;

        /// <summary>The host closed the game (its app quit, or its server stopped): nobody to reconnect to.</summary>
        public event Action? HostClosed;

        /// <summary>This seat was opened in another window: this one gives it up, and does not fight back.</summary>
        public event Action? Replaced;

        /// <summary>The hello was refused (a name the other player has, a full game, another version), with why.</summary>
        public event Action<string>? Refused;

        /// <summary>The seat given on the last welcome (0 or 1), or -1 before.</summary>
        public int Slot { get; private set; } = -1;

        /// <summary>
        /// `connect`: opens a connection to the host (TCP in the game). `version`: the app's, which the host checks: a
        /// partner on another build is told so plainly instead of playing a game the two see differently. `read`: how a
        /// message is read, on the network thread (Json.Read; the tests watch which thread).
        /// </summary>
        public ClientSession(Func<CancellationToken, Task<IConnection>> connect, ITokenStore tokens, string version, IClock? clock = null, Func<string, ServerMessage>? read = null)
        {
            this.connect = connect;
            this.tokens = tokens;
            this.version = version;
            this.clock = clock ?? new SystemClock();
            this.read = read ?? Json.Read<ServerMessage>;
        }

        /// <summary>Asks for a seat as `name`: on the socket already open if there is one (a name refused, another tried), else on a new one.</summary>
        public void Connect(string name)
        {
            this.name = name;
            stopped = false;
            if (socket != null && socketOpen && !welcomed) Hello();
            else Open();
        }

        /// <summary>Sends a message, if connected; dropped otherwise (as the WebSocket's send did).</summary>
        public void Send(string json)
        {
            if (socket != null && socketOpen) socket.Send(json);
        }

        /// <summary>The game went out of sight (the window minimized or behind another, the laptop asleep).</summary>
        public void Asleep()
        {
            hiddenAt = clock.Now;
            Send(new JObject { ["t"] = "away", ["away"] = true }.ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>Back in sight: the host hears it, and must answer (with `room`) soon, or the socket died asleep.</summary>
        public void Awake()
        {
            long slept = hiddenAt > 0 ? clock.Now - hiddenAt : 0;
            hiddenAt = 0;
            if (name.Length == 0 || stopped || connecting) return;
            if (socket == null || !socketOpen)
            {
                ReconnectNow(slept > 0);
                return;
            }
            probeAsked = clock.Now;
            probeAt = probeAsked + ProbeMs;
            Send(new JObject { ["t"] = "away", ["away"] = false }.ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>Gives up the connection for good (back to the start screen, quitting).</summary>
        public void Stop()
        {
            stopped = true;
            retryAt = -1;
            probeAt = -1;
            attempt?.Cancel();
            var last = socket;
            Detach();
            last?.Close("salida");
        }

        /// <summary>Runs what came in and what is due; call it often (every frame).</summary>
        public void Pump()
        {
            while (inbox.TryDequeue(out var job)) job();
            long now = clock.Now;
            if (retryAt >= 0 && now >= retryAt)
            {
                retryAt = -1;
                Open();
            }
            if (probeAt >= 0 && now >= probeAt)
            {
                probeAt = -1;
                if (heardAt < probeAsked && !stopped) ReconnectNow(true);
            }
        }

        private void ReconnectNow(bool rejoin)
        {
            retryAt = -1;
            retry = 0;
            if (rejoin) SetRejoining(true);
            Open();
        }

        private void SetRejoining(bool on)
        {
            if (rejoining == on) return;
            rejoining = on;
            Rejoining?.Invoke(on);
        }

        private void Open()
        {
            // The old socket, if it ever speaks again, is not listened to: its news are stale.
            Detach();
            attempt?.Cancel();
            var token = new CancellationTokenSource();
            attempt = token;
            connecting = true;
            welcomed = false;
            Status?.Invoke(NetStatus.Connecting);
            _ = Task.Run(async () =>
            {
                IConnection? c = null;
                Exception? failed = null;
                try
                {
                    c = await connect(token.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    failed = e;
                }
                inbox.Enqueue(() => Opened(token, c, failed));
            });
        }

        private void Opened(CancellationTokenSource token, IConnection? c, Exception? failed)
        {
            if (token != attempt || token.IsCancellationRequested)
            {
                c?.Close("sustituida");
                return;
            }
            connecting = false;
            if (c == null)
            {
                Dropped();
                return;
            }
            socket = c;
            c.Message += (json) =>
            {
                // Read here, on the network thread: Pump only hands it on.
                ServerMessage? msg = null;
                string? unread = null;
                try
                {
                    msg = read(json);
                }
                catch (Exception e)
                {
                    unread = e.Message;
                }
                inbox.Enqueue(() => Received(c, msg, unread));
            };
            c.Closed += (_) => inbox.Enqueue(() => Lost(c));
            if (c is TcpConnection tcp) tcp.Start();
            socketOpen = true;
            retry = 0;
            Status?.Invoke(NetStatus.Open);
            Hello();
        }

        private void Hello()
        {
            var hello = new JObject { ["t"] = "hello", ["name"] = name, ["version"] = version };
            var token = tokens.Load();
            if (token != null) hello["token"] = token;
            socket?.Send(hello.ToString(Newtonsoft.Json.Formatting.None));
        }

        private void Received(IConnection from, ServerMessage? msg, string? unread)
        {
            if (from != socket) return;
            heardAt = clock.Now;
            if (msg == null)
            {
                Unreadable?.Invoke(unread ?? "");
                return;
            }
            switch (msg)
            {
                case WelcomeMessage welcome:
                    tokens.Save(welcome.Token);
                    Slot = welcome.Slot;
                    welcomed = true;
                    SetRejoining(false);
                    break;
                case ClosingMessage _:
                    // The host closed the game (its shutdown): there is nobody to reconnect to.
                    stopped = true;
                    SetRejoining(false);
                    HostClosed?.Invoke();
                    break;
                case ReplacedMessage _:
                    // This seat was opened in another window: do not fight over it.
                    stopped = true;
                    SetRejoining(false);
                    Replaced?.Invoke();
                    break;
                case ErrorMessage error:
                    // The hello refused (room.ts join): coming back on its own would only be refused again.
                    if (!welcomed)
                    {
                        stopped = true;
                        SetRejoining(false);
                        Refused?.Invoke(error.Message);
                    }
                    break;
            }
            Message?.Invoke(msg);
        }

        private void Lost(IConnection c)
        {
            if (c != socket) return;
            Detach();
            Dropped();
        }

        /// <summary>No connection now: again after the backoff, unless stopped.</summary>
        private void Dropped()
        {
            Status?.Invoke(NetStatus.Closed);
            if (stopped)
            {
                SetRejoining(false);
                return;
            }
            long delay = Math.Min(5000, 500L << Math.Min(retry, 4));
            retry++;
            retryAt = clock.Now + delay;
        }

        /// <summary>
        /// The current socket is no longer listened to. Not closed: a live one reaching the host after the new hello would
        /// show the partner a drop; the host closes it when the new one says hello, and a dead one ends by itself.
        /// </summary>
        private void Detach()
        {
            socket = null;
            socketOpen = false;
        }
    }
}
