using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;

namespace Bastion.Client.Net
{
    /// <summary>
    /// The network in the game (docs/UNITY.md, Red), one for the app: the host's room when this app hosts, and the
    /// player's connection to a room in every app (the host's own, by 127.0.0.1). The start screen (UI) calls Host or Join
    /// and hears how it goes; the game sends its commands and messages through it and hears the host's. All it says, it
    /// says in Update, on Unity's thread. Its files (the save, red.log, conexion.json) go in the app's data folder, or in
    /// BASTION_DATOS if set (two apps on one computer, for tests). BASTION_MISION, for tests, names a recorded mission (a
    /// save, or a game of engine-cs/golden/) that every game hosted here plays instead of a new one.
    ///
    /// For tests with two apps, without clicking: BASTION_ANFITRION=&lt;name&gt; hosts as it starts, BASTION_UNIRSE=&lt;address&gt;
    /// with BASTION_NOMBRE=&lt;name&gt; joins, and BASTION_EMPEZAR=1 starts the skirmish once both are in (hosting).
    /// BASTION_PUERTO: the first port to host on, instead of RoomHost.FirstPort. BASTION_CAMPANA=1: a campaign from a new
    /// one to its missions, played by MissionBot, and back instead (CampaignRun). BASTION_MEDIR=&lt;case&gt;: a case played
    /// alone while the telemetry measures it (Measure).
    ///
    /// Of its own: the seat's token at each host (back after a drop, the same seat), `away` when the window loses or gets
    /// the focus, and what Net's ServerFeed does: a gap in the events (a `seq` missed) answered with a `sync`, and what
    /// comes while nobody listens kept for the first who does.
    /// </summary>
    public sealed class NetLink : MonoBehaviour
    {
        /// <summary>Connecting gives up after this: «No responde nadie».</summary>
        public const int ConnectTimeoutMs = 4000;

        /// <summary>Frames this far apart: the computer slept or the app was held (the clock of a sleeping Mac stops).</summary>
        private const double SleptSeconds = 10;

        /// <summary>The app's errors sent to the host's red.log, at most this many a minute.</summary>
        private const int ErrorsPerMinute = 20;


        private const string NameProblem = "Escribe tu nombre: de 1 a 20 letras.";

        private static NetLink? instance;

        /// <summary>The app's, made the first time it is asked for if no scene has one; it lives across scenes.</summary>
        public static NetLink Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Red");
                    instance = go.AddComponent<NetLink>();
                }
                return instance;
            }
        }

        /// <summary>The rules the room plays by: the engine's (Setup and Rules), or a recorded mission's (BASTION_MISION). Set before Host.</summary>
        public IRules Rules { get; set; } = new EngineRules();

        /// <summary>The campaign's (Campaign/, campaign.ts in C#): its save brought up to date, its base and its missions. Set before Host.</summary>
        public ICampaignRules CampaignRules { get; set; } = new EngineCampaignRules();

        /// <summary>Where the save, red.log and conexion.json are.</summary>
        public string DataDir { get; private set; } = "";

        /// <summary>This build's: a host and a player of different ones can't play together (the hello says so).</summary>
        public string Version => Application.version;

        private NetLog log = new NetLog("red.log");
        private NetSettings settings = new NetSettings();
        private Room? room;
        private volatile RoomHost? host;
        private IDisposable? announce;
        private IDisposable? listen;
        private ClientSession? session;
        /// <summary>Where the session connects, address:port.</summary>
        private string target = "";
        /// <summary>Asked for a seat, no welcome yet.</summary>
        private bool joining;
        /// <summary>The hello was refused (a name taken, a full game, another version): a close then is no «nobody there».</summary>
        private bool refused;
        private DateTime lastFrame;
        /// <summary>BASTION_EMPEZAR: hosting, the skirmish starts once both are in.</summary>
        private bool startWhenBoth;
        /// <summary>BASTION_CAMPANA: a campaign to its missions and back, by itself.</summary>
        private CampaignRun? campaignRun;
        /// <summary>BASTION_MEDIR: a case played alone while the telemetry measures it.</summary>
        private Measure? measure;
        private readonly ConcurrentQueue<IReadOnlyList<LanGame>> lanHeard = new ConcurrentQueue<IReadOnlyList<LanGame>>();
        private readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        private int errorsThisMinute;
        private DateTime errorMinute;
        private readonly ServerFeed feed = new ServerFeed();

        /// <summary>
        /// A message from the host (Net/Protocol.cs ServerMessage), the welcome among them. What comes while nobody
        /// listens (the scene changing after Joined) waits, and goes to the first who does, in order: the room, the
        /// snapshot and the events since. The start screen, then, doesn't listen to it; it has Joined and Failed.
        /// </summary>
        public event Action<ServerMessage>? Message
        {
            add
            {
                feed.Message += value;
            }
            remove
            {
                feed.Message -= value;
            }
        }

        /// <summary>The seat given (the first welcome of a Join): `slot` is this player's. Back after a drop it says Rejoining instead.</summary>
        public event Action<int>? Joined;

        /// <summary>Connecting, open or closed: the connection's light.</summary>
        public event Action<NetStatus>? Status;

        /// <summary>«Volviendo a la partida…» while on: back from sleep, a new connection to the same seat.</summary>
        public event Action<bool>? Rejoining;

        /// <summary>
        /// Joining didn't go through, and why, said for the player: nobody there, a name taken (another may be tried with
        /// Join), a full game, another version.
        /// </summary>
        public event Action<string>? Failed;

        /// <summary>The host closed the game (its app quit): it is saved; there's nothing to wait for.</summary>
        public event Action? HostClosed;

        /// <summary>This seat was opened in another window: this one gives it up.</summary>
        public event Action? Replaced;

        /// <summary>The games heard on the network changed (StartLooking).</summary>
        public event Action<IReadOnlyList<LanGame>>? LanGamesChanged;

        /// <summary>This app hosts the game.</summary>
        public bool Hosting => host != null;

        /// <summary>This player's slot, -1 before the welcome.</summary>
        public int Slot => session?.Slot ?? -1;

        /// <summary>The name last played with, for the start screen.</summary>
        public string? LastName => settings.Name;

        /// <summary>The address last joined (address:port), for the start screen.</summary>
        public string? LastAddress => settings.Last;

        /// <summary>Who is in the game and which screen it is on: the last `room` heard.</summary>
        public RoomMessage? LastRoom => feed.LastRoom;

        /// <summary>The partner is in (the last `room`): a host leaving is asked first.</summary>
        public bool PartnerConnected => LastRoom != null && LastRoom.Players.Any((p) => p.Slot != Slot && p.Connected);

        /// <summary>The other seat's name (the last `room`): for a player, whoever hosts (Lore.HostClosedLine, when they close it).</summary>
        public string? PartnerName => LastRoom?.Players.FirstOrDefault((p) => p.Slot != Slot)?.Name;

        /// <summary>The games heard on the network, last told by LanGamesChanged.</summary>
        public IReadOnlyList<LanGame> LanGames { get; private set; } = new LanGame[0];

        /// <summary>Hosting, where the partner can join, address:port, the network the default route takes first; none otherwise.</summary>
        public IReadOnlyList<HostAddress> Addresses
        {
            get
            {
                var h = host;
                if (h == null) return new HostAddress[0];
                return HostAddresses.List().Select((a) => new HostAddress { Address = $"{a.Address}:{h.Port}", Kind = a.Kind }).ToList();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            // A game out of sight goes on: the partner plays, and the host's room answers (TypeScript's app ran in the background too).
            Application.runInBackground = true;
            var dir = Environment.GetEnvironmentVariable("BASTION_DATOS");
            DataDir = string.IsNullOrEmpty(dir) ? Application.persistentDataPath : dir!;
            log = new NetLog(Path.Combine(DataDir, "red.log"));
            settings = NetSettings.Load(Path.Combine(DataDir, "conexion.json"));
            var mission = Environment.GetEnvironmentVariable("BASTION_MISION");
            if (!string.IsNullOrEmpty(mission)) Rules = EngineRules.Recorded(mission!);
            Application.logMessageReceivedThreaded += OnLog;
            feed.Resync += (why) =>
            {
                Report(why);
                Send(new SyncMessage());
            };
        }

        private void Start()
        {
            // The tests' drivers first: hosting asks whether one measures (Host).
            campaignRun = CampaignRun.FromEnvironment(log.Write);
            measure = Measure.FromEnvironment(log.Write);
            var hostAs = Environment.GetEnvironmentVariable("BASTION_ANFITRION");
            var joinAt = Environment.GetEnvironmentVariable("BASTION_UNIRSE");
            string? problem = null;
            if (!string.IsNullOrEmpty(hostAs)) problem = Host(hostAs!);
            else if (!string.IsNullOrEmpty(joinAt)) problem = Join(joinAt!, Environment.GetEnvironmentVariable("BASTION_NOMBRE") ?? "Prueba");
            if (problem != null) log.Write($"[prueba] {problem}");
            startWhenBoth = Environment.GetEnvironmentVariable("BASTION_EMPEZAR") == "1";
        }

        // ---------------------------------------------------------- start screen

        /// <summary>
        /// Opens a game on this computer and joins it as `name`: the room (from its save, if there is one), its port, and
        /// its announcement on the network. The problem, said for the player, or null; how joining goes, by the events.
        /// </summary>
        public string? Host(string name)
        {
            name = name.Trim();
            if (name.Length < 1 || name.Length > 20) return NameProblem;
            if (host == null)
            {
                var first = int.TryParse(Environment.GetEnvironmentVariable("BASTION_PUERTO"), out var port) ? port : RoomHost.FirstPort;
                try
                {
                    // Measuring (BASTION_MEDIR) plays each case from nothing, on a save of its own emptied first: with the
                    // players' one, or the last case's, the room would go on with that game instead (26/10: every case
                    // after the first measured the city again).
                    var file = Path.Combine(DataDir, measure != null ? "medida.json" : "save.json");
                    if (measure != null)
                        foreach (var old in new[] { file, file + ".tmp", Path.Combine(DataDir, "medida.anterior.json") })
                            if (File.Exists(old)) File.Delete(old);
                    var store = new SaveStore(file, log.Write, CampaignRules);
                    // BASTION_SEMILLA, only in the tests' campaign (CampaignRun): the room's dice, as CampaignAutoRun's.
                    var seed = CampaignRun.Seed();
                    var made = new Room(store, Rules, Version, HostAddresses.List(), rng: seed != null ? new Rng(seed.Value) : null, say: (line) => Debug.Log(line), campaignRules: CampaignRules);
                    if (seed != null) log.Write($"[prueba] sala con la semilla {seed}");
                    // Measuring alone (BASTION_MEDIR), on 127.0.0.1 only: nobody else to join, and no firewall asking (its
                    // window would take the focus, and the telemetry counts only with the game in front).
                    host = RoomHost.StartFree(made, first, 10, measure != null ? System.Net.IPAddress.Loopback : null);
                    room = made;
                }
                catch (Exception e)
                {
                    // .NET's reason, in English, to red.log; on the screen, one in Spanish.
                    log.Write($"no arranca: {e.GetType().Name}: {e.Message}");
                    if (e is SocketException s && s.SocketErrorCode == SocketError.AddressAlreadyInUse) return $"Los puertos del {first} al {first + 9} están ocupados por otras apps. Cierra alguna y vuelve a intentarlo.";
                    if (e is IOException || e is UnauthorizedAccessException) return "No se pudo abrir la partida: no se puede escribir en la carpeta de datos del juego.";
                    return "No se pudo abrir la partida. El motivo queda en red.log.";
                }
                log.Write($"anfitrión arrancado: tcp {host.Port} · {string.Join(", ", HostAddresses.List().Select((a) => a.Address))} · versión {Version}");
                if (measure == null) announce = LanBeacon.Announce(host.Port, SystemInfo.deviceName, Version, room.Summary);
            }
            return Open("127.0.0.1", host.Port, name, false);
        }

        /// <summary>Joins the game at `address` (as typed, or a LanGame's) as `name`. The problem, said for the player, or null.</summary>
        public string? Join(string address, string name)
        {
            name = name.Trim();
            if (name.Length < 1 || name.Length > 20) return NameProblem;
            if (!GameAddress.TryParse(address, out var h, out var p)) return $"Escribe la dirección que aparece en el menú principal de quien aloja la partida; por ejemplo, 192.168.1.20:{RoomHost.FirstPort}.";
            return Open(h, p, name, true);
        }

        private string? Open(string address, int port, string name, bool remember)
        {
            var key = $"{address}:{port}";
            settings.Name = name;
            if (remember) settings.Last = key;
            settings.Save();
            // The same host and not in yet (a name taken): the same connection, another name.
            if (session != null && joining && target == key)
            {
                refused = false;
                session.Connect(name);
                return null;
            }
            Drop();
            target = key;
            joining = true;
            refused = false;
            feed.Reset();
            var s = new ClientSession((cancel) => Dial(address, port, cancel), settings.TokensFor(key), Version);
            session = s;
            s.Message += (msg) => { if (s == session) Heard(msg); };
            s.Unreadable += (why) => { if (s == session) Report($"[red] mensaje del anfitrión que no se entiende: {why}"); };
            s.Status += (status) => { if (s == session) OnStatus(status); };
            s.Rejoining += (on) => { if (s == session) Rejoining?.Invoke(on); };
            s.HostClosed += () =>
            {
                if (s != session) return;
                log.Write($"[red] {target}: el anfitrión ha cerrado la partida");
                HostClosed?.Invoke();
            };
            s.Replaced += () =>
            {
                if (s != session) return;
                log.Write($"[red] {target}: el puesto se ha abierto en otra ventana");
                Replaced?.Invoke();
            };
            s.Refused += (why) =>
            {
                if (s != session) return;
                refused = true;
                log.Write($"[red] {target}: no deja entrar ({why})");
                Failed?.Invoke(why);
            };
            s.Connect(name);
            return null;
        }

        private static async Task<IConnection> Dial(string address, int port, CancellationToken cancel)
        {
            return await TcpConnection.ConnectAsync(address, port, ConnectTimeoutMs, cancel).ConfigureAwait(false);
        }

        private void OnStatus(NetStatus status)
        {
            Status?.Invoke(status);
            if (status != NetStatus.Closed || !joining) return;
            // Never in: nobody there (or the host closed it on a refusal, already said). Another try is another Join.
            var nobody = !refused;
            var where = target;
            Drop();
            if (!nobody) return;
            log.Write($"[red] {where}: no responde nadie");
            Failed?.Invoke($"No responde nadie en {where}. ¿Tiene abierta la partida («Alojar la partida») y estáis en la misma red o en Tailscale?");
        }

        /// <summary>A message from the host, already read on the network thread (ClientSession): a whole mission never costs a frame.</summary>
        private void Heard(ServerMessage msg)
        {
            if (msg is WelcomeMessage welcome && joining)
            {
                joining = false;
                log.Write($"[red] {target}: dentro, en el puesto {welcome.Slot + 1} (anfitrión {welcome.Version})");
                Joined?.Invoke(welcome.Slot);
            }
            if (startWhenBoth && Hosting && msg is RoomMessage inRoom && inRoom.Phase == RoomPhases.Lobby && inRoom.Players.Count((p) => p.Connected) == 2)
            {
                startWhenBoth = false;
                Send(new StartMessage());
            }
            // BASTION_CAMPANA's player reads it too, before it goes on as it is.
            campaignRun?.Observe(msg);
            measure?.Observe(msg);
            feed.Take(msg);
        }

        /// <summary>Listens for games on the network while the start screen shows; LanGamesChanged says what it hears.</summary>
        public void StartLooking()
        {
            if (listen != null) return;
            try
            {
                listen = LanBeacon.Listen((games) => lanHeard.Enqueue(games), (port) => host?.Port == port);
            }
            catch (Exception e)
            {
                log.Write($"[lan] no se oyen las partidas de la red: {e.Message}");
            }
        }

        public void StopLooking()
        {
            listen?.Dispose();
            listen = null;
        }

        /// <summary>
        /// Back to the start screen: the connection given up and, if this app hosts, the game closed (saved, and the partner
        /// told). The start screen warns a host whose partner is in first.
        /// </summary>
        public void Leave()
        {
            CloseHost();
            Drop();
        }

        private void Drop()
        {
            var old = session;
            session = null;
            joining = false;
            feed.Reset();
            old?.Stop();
        }

        private void CloseHost()
        {
            if (host != null) log.Write("anfitrión cerrado: se avisa a los jugadores");
            announce?.Dispose();
            announce = null;
            host?.Stop();
            host = null;
            room = null;
        }

        // ------------------------------------------------------------- the game

        /// <summary>A message to the host (Net/Protocol.cs ClientMessage); dropped while not connected.</summary>
        public void Send(ClientMessage msg)
        {
            campaignRun?.Sent(msg);
            measure?.Sent(msg);
            session?.Send(Json.Write(msg));
        }

        /// <summary>An order of the engine's for the host to play (in a `cmd`).</summary>
        public void Send(Command cmd)
        {
            Send(new CmdMessage { Cmd = cmd });
        }

        /// <summary>A line for red.log: the host's, through the connection; this app's own while it has none.</summary>
        public void Report(string line)
        {
            if (session != null && !joining) Send(new ReportMessage { Line = line.Length > Room.ReportMax ? line.Substring(0, Room.ReportMax) : line });
            else log.Write($"[app] {line}");
        }

        // ---------------------------------------------------------------- Unity

        private void Update()
        {
            var now = DateTime.UtcNow;
            // Back from sleep: the session makes sure its connection is alive, or opens another to the same seat.
            if (lastFrame != default && (now - lastFrame).TotalSeconds > SleptSeconds) session?.Awake();
            lastFrame = now;
            while (lanHeard.TryDequeue(out var games))
            {
                LanGames = games;
                LanGamesChanged?.Invoke(games);
            }
            if (now - errorMinute > TimeSpan.FromMinutes(1))
            {
                errorMinute = now;
                errorsThisMinute = 0;
            }
            while (errors.TryDequeue(out var line))
                if (errorsThisMinute++ < ErrorsPerMinute) Report(line);
            session?.Pump();
            campaignRun?.Tick(this, Time.realtimeSinceStartup);
            measure?.Tick(this, Time.realtimeSinceStartup);
        }

        /// <summary>The window lost or got the focus: the game out of sight, or back (the `away` message).</summary>
        private void OnApplicationFocus(bool focused)
        {
            if (session == null) return;
            if (focused) session.Awake();
            else session.Asleep();
        }

        private void OnApplicationPause(bool paused)
        {
            OnApplicationFocus(!paused);
        }

        /// <summary>Quitting: the partner hears the host is gone (the game saved), then the connection goes.</summary>
        private void OnApplicationQuit()
        {
            Shut();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            Shut();
            Application.logMessageReceivedThreaded -= OnLog;
            instance = null;
        }

        private void Shut()
        {
            StopLooking();
            CloseHost();
            Drop();
        }

        /// <summary>The app's errors and exceptions (any thread), for red.log: whoever hosts sees what went wrong on the partner's screen.</summary>
        private void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            var at = stack.Split('\n').FirstOrDefault((l) => l.Trim().Length > 0)?.Trim();
            errors.Enqueue(at != null ? $"[error] {condition} · {at}" : $"[error] {condition}");
        }
    }
}
