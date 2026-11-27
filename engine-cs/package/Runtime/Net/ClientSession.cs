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

    /// <summary>Why a try to connect never reached the host (ClientSession.Unreached), for what the player is told to do.</summary>
    public enum Unreach
    {
        /// <summary>Nobody listening there: the game isn't open, or not on that port. At once.</summary>
        Refused,

        /// <summary>No way to that address: on a Mac, an iPhone or an iPad, the local network not allowed to the app. At once.</summary>
        NoRoute,

        /// <summary>No answer in time: something drops it on the way (a firewall, a VPN), or the address isn't the host's.</summary>
        NoAnswer,
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
        private bool paused;
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

        /// <summary>
        /// A try to connect that never reached the host, and why: the kind, for what the player is told to do (Arturo, cuadro
        /// 172), and the socket's error, for red.log. Until 0.14.11 the player saw «No responde nadie» for all of them
        /// (Arturo and Ana on one Wi-Fi, the game heard and the connection failing).
        /// </summary>
        public event Action<Unreach, string>? Unreached;

        /// <summary>The seat given on the last welcome (0 or 1), or -1 before.</summary>
        public int Slot { get; private set; } = -1;

        /// <summary>The room's key, to join from the internet (Room.Key), sent with each hello; null on the local network or Tailscale.</summary>
        public string? Key { get; set; }

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

        /// <summary>
        /// The game's menu opened or closed in a mission (the `pause` message): the host waits for both while it is open.
        /// Kept, and said again on every welcome: a drop lifts the pause on the host, and the menu may still be open.
        /// </summary>
        public void Pause(bool on)
        {
            paused = on;
            SendPause();
        }

        private void SendPause()
        {
            Send(new JObject { ["t"] = "pause", ["paused"] = paused }.ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>Gives up the connection for good (back to the start screen, quitting).</summary>
        public void Stop()
        {
            stopped = true;
            paused = false;
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

        /// <summary>The socket's own error under `failed`, if it is one (a task's or a wrapper's inner exception), or `failed`.</summary>
        private static Exception? SocketErrorOf(Exception? failed)
        {
            for (var e = failed; e != null; e = e.InnerException)
                if (e is System.Net.Sockets.SocketException) return e;
            return failed;
        }

        /// <summary>
        /// What a failed connect means for the player. No route: what macOS and iOS answer at once when the app may not use the
        /// local network (Privacy › Local Network), and a machine with no way to that network at all. Anything else that isn't a
        /// refusal, a timeout among them: no answer.
        /// </summary>
        public static Unreach Why(Exception? failed)
        {
            if (!(failed is System.Net.Sockets.SocketException e)) return Unreach.NoAnswer;
            // On iOS (IL2CPP) the code is the system's own errno, not .NET's: «61: mono-io-layer-error (61)» for a refusal in
            // the simulator (0.14.12), where the Mac's Mono says ConnectionRefused. Both are read: Darwin's numbers are
            // ECONNREFUSED 61, EHOSTUNREACH 65, ENETUNREACH 51 and ENETDOWN 50, none of them one of .NET's.
            switch ((int)e.SocketErrorCode)
            {
                case (int)System.Net.Sockets.SocketError.ConnectionRefused:
                case 61:
                    return Unreach.Refused;
                case (int)System.Net.Sockets.SocketError.HostUnreachable:
                case (int)System.Net.Sockets.SocketError.NetworkUnreachable:
                case (int)System.Net.Sockets.SocketError.NetworkDown:
                case 65:
                case 51:
                case 50:
                    return Unreach.NoRoute;
                default:
                    return Unreach.NoAnswer;
            }
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
                var socketError = SocketErrorOf(failed);
                Unreached?.Invoke(Why(socketError), socketError is System.Net.Sockets.SocketException e ? $"{e.SocketErrorCode}: {e.Message}" : failed?.Message ?? "sin conexión");
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
            if (!string.IsNullOrEmpty(Key)) hello["key"] = Key;
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
                    // Back in with the menu still open: paused again.
                    if (paused) SendPause();
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
