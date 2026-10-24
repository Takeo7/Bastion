using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// A single co-op session for two seats (room.ts Room), as far as the first milestone goes: a skirmish between two
    /// apps (docs/UNITY.md). The room owns the authoritative state; clients only send intents, and what they make happen
    /// streams to both as events.
    ///
    /// It runs on one thread, its own: what comes from the connections (on theirs) waits in a queue, and Pump plays it,
    /// in order, with the timers that are due by its clock (RoomHost does that; the tests, by hand with a fake clock).
    /// </summary>
    public sealed class Room
    {
        /// <summary>A dropped player keeps blocking the end of turn this long, so a reload or Wi-Fi blip never costs them their turn.</summary>
        public const long ReconnectGraceMs = 30_000;

        /// <summary>The same for a player whose game was out of sight when it dropped (a laptop put to sleep): back, the turn and the command are still theirs.</summary>
        public const long AwayGraceMs = 5 * 60_000;

        /// <summary>
        /// How long a joint decision waits for the partner's answer before it goes ahead (playtest 2026-10-14, #28: in
        /// case they left). Leaving the mission, two minutes (Arturo, 22/10: Ana was taken home unasked). A base
        /// proposal (30 s) comes with the base.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, long> AnswerWaitMs = new Dictionary<string, long> { [DeadlineKinds.Evac] = 120_000, [DeadlineKinds.Abandon] = 120_000 };

        /// <summary>The squad's turn clock (Arturo, 22/10): each player gets this long for each soldier they command.</summary>
        public const long TurnMsPerSoldier = 45_000;

        /// <summary>Each this much a player has left at the end of the squad's turn is a second more in the next (Arturo, 22/10).</summary>
        public const long SavedMsPerSecond = 5_000;

        /// <summary>content.ts PLAYER_COLORS, by slot.</summary>
        public static readonly IReadOnlyList<string> PlayerColors = new[] { "#a678ff", "#ffa630" };

        /// <summary>A report's line is cut here, as /__report's body was (index.ts).</summary>
        public const int ReportMax = 2000;

        private sealed class Seat
        {
            public int Slot;
            public string Name = "";
            public string Token = "";
            public IConnection? Socket;
            /// <summary>When the socket dropped; null while connected.</summary>
            public long? DroppedAt;
            /// <summary>Their game is out of sight (the `away` message): a drop then waits AwayGraceMs.</summary>
            public bool Away;
        }

        /// <summary>A connection and the seat it took with its hello, if any.</summary>
        private sealed class Link
        {
            public readonly IConnection Socket;
            public Seat? Seat;

            public Link(IConnection socket)
            {
                Socket = socket;
            }
        }

        /// <summary>Something to do at `At` (room.ts setTimeout), in the order they were asked for when due together.</summary>
        private sealed class Later
        {
            public long At;
            public long Order;
            public Action Run = () => { };
        }

        /// <summary>A joint decision waiting for an answer: what it waits on, when it ends, and since when.</summary>
        private sealed class Waiting
        {
            public string Key = "";
            public long At;
            public long Since;
            public Later? Timer;
        }

        /// <summary>
        /// For the squad turn `Turn`, each player's milliseconds `Left` of their `Total`; whose runs now (the holder of the
        /// command, while their screen shows nothing going on) and since when. Not saved: a restart gives the turn its clock afresh.
        /// </summary>
        private sealed class TurnClockState
        {
            public int Turn;
            public long[] Left = new long[2];
            public long[] Total = new long[2];
            public int? Running;
            public long Since;
            public Later? Timer;
        }

        private readonly SaveStore store;
        private readonly IRules rules;
        private readonly string version;
        private readonly List<HostAddress> hosts;
        private readonly IClock clock;
        private readonly Action<string> say;

        private readonly ConcurrentQueue<Action> inbox = new ConcurrentQueue<Action>();
        private readonly AutoResetEvent posted = new AutoResetEvent(false);
        private readonly List<Later> timers = new List<Later>();
        private long laterOrder;
        private readonly List<Link> links = new List<Link>();

        private readonly Seat?[] seats = new Seat?[2];
        /// <summary>The waits on joint decisions now running (AnswerWaitMs), by kind.</summary>
        private readonly Dictionary<string, Waiting> waits = new Dictionary<string, Waiting>();
        private string phase = RoomPhases.Lobby;
        private GameState? state;
        private Rng rng;
        private string missionKind = MissionKinds.Elimination;
        private string skirmishMap = SkirmishMaps.City;
        private bool skirmishVeterans = true;
        private bool skirmishNight;
        /// <summary>The last write of the save failed: the players were told once, and are told again only after one goes through.</summary>
        private bool saveFailed;
        private TurnClockState? turnClock;
        /// <summary>Whose screen has played every event it was sent (the idle message): the turn clock waits while an action shows.</summary>
        private readonly bool[] idle = new bool[2];
        /// <summary>The last turnClock message sent, so an unchanged one isn't sent again.</summary>
        private string clockSent = "";

        private readonly object summaryGate = new object();
        private IReadOnlyList<string> summaryPlayers = new string[0];
        private string summaryPhase = RoomPhases.Lobby;

        /// <summary>
        /// The room, from its save if there is one. `version`: this build's, which every player's must match. `hosts`:
        /// where the partner can connect, for the welcome. `say`: the host's console (room.ts console.log); red.log is the
        /// store's Log.
        /// </summary>
        public Room(SaveStore store, IRules rules, string version, IReadOnlyList<HostAddress>? hosts = null, IClock? clock = null, Rng? rng = null, Action<string>? say = null)
        {
            this.store = store;
            this.rules = rules;
            this.version = version;
            this.hosts = hosts?.ToList() ?? new List<HostAddress>();
            this.clock = clock ?? new SystemClock();
            this.say = say ?? ((_) => { });
            this.rng = rng ?? new Rng(unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            var saved = store.Load();
            if (saved == null) return;
            for (int i = 0; i < 2; i++)
            {
                var s = i < saved.Seats.Count ? saved.Seats[i] : null;
                seats[i] = s == null ? null : new Seat { Slot = s.Slot, Name = s.Name, Token = s.Token };
            }
            // Only a skirmish's mission goes on: a campaign's, and the base, come later.
            if (saved.Mode == "skirmish" && saved.Phase == RoomPhases.Mission && saved.State != null)
            {
                phase = RoomPhases.Mission;
                state = saved.State;
            }
            if (saved.Rng.Length == 4) this.rng = new Rng(saved.Rng);
            missionKind = saved.MissionKind;
            skirmishMap = saved.SkirmishMap;
            skirmishVeterans = saved.SkirmishVeterans;
            skirmishNight = saved.SkirmishNight ?? false;
            Summarize();
            this.say($"[room] Partida restaurada ({phase}).");
        }

        // ------------------------------------------------------------- the thread

        /// <summary>Set when something waits in the queue: the room's thread wakes to Pump.</summary>
        public WaitHandle Posted => posted;

        /// <summary>Something for the room's thread to do, from any thread.</summary>
        public void Post(Action job)
        {
            inbox.Enqueue(job);
            posted.Set();
        }

        /// <summary>Plays what waits in the queue and the timers due, in order, until nothing is left. On the room's thread.</summary>
        public void Pump()
        {
            while (true)
            {
                if (inbox.TryDequeue(out var job))
                {
                    Run(job);
                    continue;
                }
                var due = NextTimer();
                if (due == null || due.At > clock.Now) break;
                timers.Remove(due);
                Run(due.Run);
            }
            Summarize();
        }

        /// <summary>How long until the next timer is due, `max` at most (RoomHost sleeps that long unless something comes).</summary>
        public int WaitMs(int max)
        {
            var next = NextTimer();
            return next == null ? max : (int)Math.Max(0, Math.Min(max, next.At - clock.Now));
        }

        private Later? NextTimer()
        {
            Later? next = null;
            foreach (var t in timers)
                if (next == null || t.At < next.At || (t.At == next.At && t.Order < next.Order)) next = t;
            return next;
        }

        private Later After(long ms, Action run)
        {
            var t = new Later { At = clock.Now + ms, Order = laterOrder++, Run = run };
            timers.Add(t);
            return t;
        }

        private void Cancel(Later? t)
        {
            if (t != null) timers.Remove(t);
        }

        private void Run(Action job)
        {
            try
            {
                job();
            }
            catch (Exception e)
            {
                // A rule that throws, a disk that fails: the game goes on, and red.log says what.
                var line = $"[room] Fallo: {e.GetType().Name}: {e.Message} · {e.StackTrace?.Replace(Environment.NewLine, " | ")}";
                say(line);
                store.Log(line);
            }
        }

        // ---------------------------------------------------------- connections

        /// <summary>A player's app connected (from any thread): before it says anything.</summary>
        public void Connect(IConnection socket)
        {
            var link = new Link(socket);
            Post(() =>
            {
                links.Add(link);
                store.Log($"conecta {socket.Peer}");
            });
            socket.Message += (text) => Post(() => Heard(link, text));
            socket.Closed += (why) => Post(() => Gone(link, why));
        }

        private void Heard(Link link, string text)
        {
            ClientMessage msg;
            try
            {
                msg = Json.Read<ClientMessage>(text);
            }
            catch (Exception)
            {
                Send(link.Socket, new ErrorMessage { Message = "Mensaje no válido." });
                return;
            }
            if (!Valid(msg))
            {
                Send(link.Socket, new ErrorMessage { Message = "Mensaje no válido." });
                return;
            }
            if (msg is ReportMessage report)
            {
                Report(link.Socket, report.Line);
                return;
            }
            if (msg is HelloMessage hello)
            {
                link.Seat = Join(link.Socket, hello);
                return;
            }
            var seat = link.Seat;
            if (seat == null || seat.Socket != link.Socket)
            {
                Send(link.Socket, new ErrorMessage { Message = "Identifícate primero." });
                return;
            }
            Handle(seat, msg);
            CheckTurnClock();
        }

        private void Gone(Link link, string why)
        {
            links.Remove(link);
            store.Log($"desconecta {link.Socket.Peer} ({why})");
            var seat = link.Seat;
            if (seat == null || seat.Socket != link.Socket) return;
            seat.Socket = null;
            seat.DroppedAt = clock.Now;
            say($"[room] {seat.Name} se ha desconectado.");
            BroadcastRoom();
            idle[seat.Slot] = false;
            CheckTurnClock();
            After((seat.Away ? AwayGraceMs : ReconnectGraceMs) + 100, ResyncTurn);
        }

        /// <summary>protocol.ts clientMessageSchema and commandSchema, past what the types already say: the hello's name trimmed, ids, places and names of things in bounds.</summary>
        private static bool Valid(ClientMessage msg)
        {
            switch (msg)
            {
                case HelloMessage m:
                    m.Name = (m.Name ?? "").Trim();
                    return m.Name.Length >= 1 && m.Name.Length <= 20 && (m.Token == null || m.Token.Length <= 64);
                case StartMessage m:
                    return (m.Mission == null || MissionKinds.All.Contains(m.Mission)) && (m.Map == null || SkirmishMaps.All.Contains(m.Map));
                case NewMissionMessage m:
                    return (m.Mission == null || MissionKinds.All.Contains(m.Mission)) && (m.Map == null || SkirmishMaps.All.Contains(m.Map));
                case CmdMessage m:
                    return m.Cmd != null && ValidCommand(m.Cmd);
                case PresenceMessage m:
                    return (m.Unit == null || UnitId(m.Unit)) && (m.Tile == null || Spot(m.Tile));
                case PingMessage m:
                    return m.Tile != null && Spot(m.Tile) && (m.Kind == "point" || m.Kind == "enemy");
                case PlanMessage m:
                    return UnitId(m.Unit) && (m.Tile == null || Spot(m.Tile));
                case ReportMessage m:
                    return m.Line != null;
                default:
                    return true;
            }
        }

        private static bool ValidCommand(Command cmd)
        {
            switch (cmd)
            {
                case MoveCommand c:
                    return UnitId(c.Unit) && Spot(c.To);
                case AbilityCommand c:
                    return UnitId(c.Unit) && AbilityIds.All.Contains(c.Ability) && (c.Target == null || UnitId(c.Target)) && (c.Tile == null || Spot(c.Tile));
                case EvacCommand c:
                    return UnitId(c.Unit);
                case ItemCommand c:
                    return UnitId(c.Unit) && ItemActions.All.Contains(c.Action) && (c.Index == null || (c.Index >= 0 && c.Index <= 7)) && (c.Id == null || UnitId(c.Id)) && (c.To == null || UnitId(c.To));
                case RtOrderCommand c:
                    return UnitId(c.Unit) && (c.To == null || Spot(c.To)) && (c.Target == null || UnitId(c.Target)) && (c.Ability == null || AbilityIds.All.Contains(c.Ability)) && (c.Tile == null || Spot(c.Tile));
                default:
                    return true;
            }
        }

        private static bool UnitId(string? id) => id != null && id.Length >= 1 && id.Length <= 16;

        private static bool Spot(Pos p) => p.Z >= 0 && p.Z <= 3;

        /// <summary>A line from an app for red.log: how its screen draws comes tagged `[rend name]`; the address stands in for a missing one.</summary>
        private void Report(IConnection socket, string line)
        {
            var text = (line.Length > ReportMax ? line.Substring(0, ReportMax) : line).Replace('\r', ' ').Replace('\n', ' ');
            store.Log(Regex.IsMatch(text, @"^\[rend[ \]]") ? Regex.Replace(text, @"^\[rend\]", $"[rend {socket.Peer}]") : $"[app {socket.Peer}] {text}");
        }

        private Seat? Join(IConnection socket, HelloMessage hello)
        {
            var name = hello.Name;
            var token = hello.Token;
            // Two builds play by the same rules or not at all: the seat isn't asked for.
            if (hello.Version != version)
            {
                Send(socket, new ErrorMessage { Message = $"Quien aloja la partida tiene Bastión {version} y tú, {hello.Version ?? "otra versión"}: necesitáis la misma." });
                socket.Close("otra versión");
                return null;
            }
            // By the seat's token; from an app without it (another computer, a reinstall), by the player's name, so each
            // finds their own seat and soldiers. Only a seat nobody plays from: one in use with that name is someone else's (below).
            var seat = seats.FirstOrDefault((s) => s != null && token != null && s.Token == token) ?? seats.FirstOrDefault((s) => s != null && s.Socket == null && SameName(s.Name, name));
            // Two commanders with one name can't be told apart (Arturo, 18/10 #51): the other seat's name is theirs.
            // Asked before any seat is made, so a refusal leaves none behind.
            if (seats.Any((s) => s != null && s != seat && SameName(s.Name, name)))
            {
                // The socket stays open and seatless (closing it, the app would come back with the same name, again and again).
                Send(socket, new ErrorMessage { Message = "Ese nombre ya lo usa el otro jugador: elige otro." });
                return null;
            }
            if (seat == null)
            {
                var free = Array.IndexOf(seats, null);
                if (free >= 0)
                {
                    seat = new Seat { Slot = free, Name = name, Token = Guid.NewGuid().ToString() };
                    seats[free] = seat;
                }
                else
                {
                    // Both seats taken: allow taking over a seat whose player is offline.
                    seat = seats.FirstOrDefault((s) => s != null && s.Socket == null);
                    if (seat == null)
                    {
                        Send(socket, new ErrorMessage { Message = "La partida ya tiene dos jugadores." });
                        socket.Close("llena");
                        return null;
                    }
                    seat.Token = Guid.NewGuid().ToString();
                }
            }

            if (seat.Socket != null && seat.Socket != socket)
            {
                Send(seat.Socket, new ReplacedMessage());
                seat.Socket.Close("Sesión abierta en otra ventana");
            }
            seat.Socket = socket;
            seat.DroppedAt = null;
            seat.Away = false;
            seat.Name = name;
            say($"[room] {name} ocupa el puesto {seat.Slot + 1}.");

            Send(socket, new WelcomeMessage { Token = seat.Token, Slot = seat.Slot, Hosts = hosts, Version = version });
            BroadcastRoom();
            if (phase == RoomPhases.Mission && state != null) Send(socket, new SnapshotMessage { State = state });
            Persist();
            ResyncTurn();
            // A call saved before the host restarted had no clock: it gets one when someone is back.
            CheckWaits();
            // Back in: how long the open decisions have left, and the turn clock.
            foreach (var kind in new[] { DeadlineKinds.Evac, DeadlineKinds.Abandon })
                if (waits.TryGetValue(kind, out var wait)) Send(socket, new DeadlineMessage { Kind = kind, Ms = Math.Max(0, wait.At - clock.Now) });
            idle[seat.Slot] = false;
            Send(socket, TurnClockNow());
            return seat;
        }

        /// <summary>room.ts sameName: trimmed, and big or small letters the same.</summary>
        private static bool SameName(string a, string b) => string.Equals(a.Trim().ToLowerInvariant(), b.Trim().ToLowerInvariant(), StringComparison.Ordinal);

        // ------------------------------------------------------------- messages

        private void Handle(Seat seat, ClientMessage msg)
        {
            switch (msg)
            {
                // ---- lobby and skirmish
                case StartMessage m:
                    if (phase == RoomPhases.Lobby)
                    {
                        if (m.Veterans != null) skirmishVeterans = m.Veterans.Value;
                        if (m.Night != null) skirmishNight = m.Night.Value;
                        StartSkirmish(m.Mission, m.Map);
                    }
                    break;
                case NewMissionMessage m:
                    if (phase != RoomPhases.Mission || state?.Outcome == null) return;
                    if (m.Night != null) skirmishNight = m.Night.Value;
                    StartSkirmish(m.Mission, m.Map);
                    break;
                case BackToMenuMessage _:
                    // From a skirmish, over or not: it ends there.
                    if (phase != RoomPhases.Mission) return;
                    state = null;
                    phase = RoomPhases.Lobby;
                    BroadcastRoom();
                    Persist();
                    CheckWaits();
                    break;
                case IdleMessage m:
                    idle[seat.Slot] = m.Idle;
                    break;
                case AwayMessage m:
                {
                    // The partner hears of a change; whoever asked always gets an answer, its proof the socket is alive (ClientSession).
                    var changed = seat.Away != m.Away;
                    seat.Away = m.Away;
                    if (changed) BroadcastRoom();
                    else Send(seat.Socket!, RoomNow());
                    break;
                }

                // ---- tactical
                case AbandonMessage _:
                {
                    // Asked of the partner first, if here (playtest 16/10); alone, it ends now.
                    if (phase != RoomPhases.Mission || state == null) return;
                    var events = rules.CallAbandon(state, seat.Slot, ActiveSlots());
                    if (events.Count == 0) return;
                    say($"[room] {seat.Name} {(state.Outcome != null ? "abandona la misión" : "pide abandonar la misión")}.");
                    Broadcast(new EventsMessage { Events = events, Seq = state.Seq, By = seat.Slot });
                    AfterTactical();
                    CheckWaits();
                    break;
                }
                case AbandonAnswerMessage m:
                {
                    if (phase != RoomPhases.Mission || state == null) return;
                    var events = rules.AnswerAbandon(state, seat.Slot, m.Accept);
                    if (events.Count == 0) return;
                    say($"[room] {seat.Name} {(m.Accept ? "acepta abandonar la misión" : "no quiere abandonar la misión")}.");
                    Broadcast(new EventsMessage { Events = events, Seq = state.Seq, By = seat.Slot });
                    AfterTactical();
                    CheckWaits();
                    break;
                }
                case PingMessage m:
                    Broadcast(new PartnerPingMessage { Slot = seat.Slot, Tile = m.Tile, Kind = m.Kind });
                    break;
                case SyncMessage _:
                    if (phase == RoomPhases.Mission && state != null) Send(seat.Socket!, new SnapshotMessage { State = state });
                    break;
                case CmdMessage m:
                {
                    if (phase != RoomPhases.Mission || state == null) return;
                    var result = rules.ExecuteCommand(state, m.Cmd!, seat.Slot, ActiveSlots(), rng);
                    if (!result.Ok)
                    {
                        Send(seat.Socket!, new RejectedMessage { Message = result.Error ?? "" });
                        return;
                    }
                    Broadcast(new EventsMessage { Events = result.Events ?? new List<GameEvent>(), Seq = state.Seq, By = seat.Slot });
                    AfterTactical();
                    CheckWaits();
                    break;
                }
                case PresenceMessage m:
                    ToPartner(seat, new PartnerPresenceMessage { Slot = seat.Slot, Unit = m.Unit, Tile = m.Tile });
                    break;
                case PlanMessage m:
                {
                    // Only with one's own soldiers, and only in a mission.
                    var unit = phase == RoomPhases.Mission ? state?.Units.Find((u) => u.Id == m.Unit) : null;
                    if (unit == null || unit.Team != Teams.Xcom || (unit.Owner != null && unit.Owner != seat.Slot)) return;
                    ToPartner(seat, new PartnerPlanMessage { Slot = seat.Slot, Unit = m.Unit, Tile = m.Tile });
                    break;
                }
            }
        }

        private void StartSkirmish(string? kind, string? map)
        {
            var slots = ConnectedSlots();
            if (slots.Count == 0) return;
            kind ??= missionKind;
            map ??= skirmishMap;
            // A Saqueo is always in town: houses to search (the lobby only offers the city for it).
            if (kind == MissionKinds.Scavenge && map != SkirmishMaps.Industrial) map = SkirmishMaps.City;
            missionKind = kind;
            skirmishMap = map;
            // rng.int(2 ** 31): past an int, so as rng.ts does it.
            var seed = (int)Math.Floor(rng.Next() * 2147483648.0);
            var options = new SkirmishOptions { Seed = seed, Slots = slots, Kind = kind, Veterans = skirmishVeterans, Night = skirmishNight };
            if (map == SkirmishMaps.Plaza || map == SkirmishMaps.Torre || map == SkirmishMaps.Bastion)
            {
                options.MapKind = "handmade";
                options.Id = map;
            }
            else if (map == SkirmishMaps.Roadside)
            {
                options.Biome = "wilds";
                options.Site = "roadside";
            }
            else if (map == SkirmishMaps.Industrial)
            {
                options.Biome = "city";
                options.Site = "industrial";
            }
            else options.Biome = map;
            state = rules.CreateMission(options, rng);
            phase = RoomPhases.Mission;
            say($"[room] Escaramuza ({kind}{(skirmishNight ? ", de noche" : "")}, {state.MapName}) con {slots.Count} jugador(es).");
            BroadcastRoom();
            Broadcast(new SnapshotMessage { State = state });
            Persist();
        }

        /// <summary>After any tactical change. A campaign's mission over is folded into the campaign here, when the campaign comes.</summary>
        private void AfterTactical()
        {
            Persist();
        }

        /// <summary>A disconnect may be the last thing blocking the end of the XCOM turn.</summary>
        private void ResyncTurn()
        {
            if (phase != RoomPhases.Mission || state == null || ConnectedSlots().Count == 0) return;
            var events = rules.SyncTurn(state, ActiveSlots(), rng);
            if (events.Count > 0)
            {
                Broadcast(new EventsMessage { Events = events, Seq = state.Seq, By = null });
                AfterTactical();
            }
            CheckWaits();
        }

        /// <summary>
        /// Starts, keeps or drops the waits on joint decisions (playtest 2026-10-14, #28): a call to evacuate, or to
        /// leave. A new one starts its AnswerWaitMs and both clients hear how long it has; when it is answered or gone,
        /// they hear it is over.
        /// </summary>
        private void CheckWaits()
        {
            var on = phase == RoomPhases.Mission && state != null && state.Outcome == null;
            var call = on ? state!.EvacCall : null;
            Watch(DeadlineKinds.Evac, call != null ? $"{state!.Turn}:{call.By}:{call.Unit}" : null);
            var leave = on ? state!.AbandonCall : null;
            Watch(DeadlineKinds.Abandon, leave != null ? $"by:{leave.By}" : null);
            CheckTurnClock();
        }

        /// <summary>
        /// The squad's turn clock (Arturo, 22/10). Each player has TurnMsPerSoldier for each soldier they command, and a
        /// second more for each SavedMsPerSecond they had left at the end of the last turn. Only the holder of the command
        /// spends theirs, and only while their screen shows nothing going on (an action playing stops it), until they are
        /// done. At none left, their soldiers take cover and their turn is over (runOutOfTime). Called after anything that
        /// may change it; both screens hear whenever it starts, stops or changes.
        /// </summary>
        private void CheckTurnClock()
        {
            var s = state;
            var c = turnClock;
            var now = clock.Now;
            if (c != null && c.Running != null)
            {
                var r = c.Running.Value;
                c.Left[r] = Math.Max(0, c.Left[r] - (now - c.Since));
                c.Running = null;
            }
            if (c?.Timer != null)
            {
                Cancel(c.Timer);
                c.Timer = null;
            }
            var on = phase == RoomPhases.Mission && s != null && s.Outcome == null && s.Realtime == null && s.ActiveTeam == Teams.Xcom;
            if (!on)
            {
                // Out of the squad's turn the clock waits; a new mission, or none, drops it.
                if (s == null || s.Outcome != null || phase != RoomPhases.Mission) turnClock = null;
                SendTurnClock();
                return;
            }
            var active = ActiveSlots();
            if (c == null || c.Turn != s!.Turn)
            {
                // A new squad turn: each player's time, for the soldiers they command, and what they saved last turn.
                var total = new long[2];
                for (int slot = 0; slot < 2; slot++)
                {
                    var soldiers = Grid.Combatants(s!, Teams.Xcom).Count((u) => u.Owner == slot || u.Owner == null || !active.Contains(u.Owner.Value));
                    var saved = c != null ? c.Left[slot] / SavedMsPerSecond * 1000 : 0;
                    total[slot] = soldiers > 0 ? soldiers * TurnMsPerSoldier + saved : 0;
                }
                turnClock = new TurnClockState { Turn = s!.Turn, Left = (long[])total.Clone(), Total = total, Since = now };
            }
            var tc = turnClock!;
            var holder = s!.Command != null && active.Contains(s.Command.Value) ? s.Command : null;
            if (holder != null)
            {
                var h = holder.Value;
                if (!s.Ready[h] && idle[h] && seats[h]?.Socket != null && tc.Left[h] > 0)
                {
                    tc.Running = h;
                    tc.Since = now;
                    var turn = s.Turn;
                    tc.Timer = After(tc.Left[h], () => ClockOut(h, turn));
                }
            }
            SendTurnClock();
        }

        /// <summary>`slot`'s turn clock has run out (CheckTurnClock): their soldiers take cover and they are done for the turn.</summary>
        private void ClockOut(int slot, int turn)
        {
            var s = state;
            var c = turnClock;
            if (s == null || c == null || c.Turn != turn || c.Running != slot || s.Turn != turn) return;
            c.Left[slot] = 0;
            c.Running = null;
            c.Timer = null;
            var events = rules.RunOutOfTime(s, slot, ActiveSlots(), rng);
            say($"[room] Se acaba el tiempo de {seats[slot]?.Name ?? $"el puesto {slot + 1}"}: sus soldados se cubren.");
            if (events.Count > 0)
            {
                Broadcast(new EventsMessage { Events = events, Seq = s.Seq, By = null });
                AfterTactical();
            }
            CheckWaits();
        }

        private TurnClockMessage TurnClockNow()
        {
            var c = turnClock;
            var s = state;
            if (c == null || s == null || s.ActiveTeam != Teams.Xcom || s.Outcome != null) return new TurnClockMessage { Clocks = null };
            var now = clock.Now;
            TurnClock One(int slot) => new TurnClock { Left = c.Running == slot ? Math.Max(0, c.Left[slot] - (now - c.Since)) : c.Left[slot], Total = c.Total[slot], Running = c.Running == slot };
            return new TurnClockMessage { Clocks = new List<TurnClock> { One(0), One(1) } };
        }

        /// <summary>Both screens hear the turn clock when it starts, stops or changes (to a tenth of a second).</summary>
        private void SendTurnClock()
        {
            var msg = TurnClockNow();
            var key = msg.Clocks == null ? "null" : string.Join(";", msg.Clocks.Select((k) => $"{Math.Round(k.Left / 100.0, MidpointRounding.AwayFromZero)},{k.Total},{k.Running}"));
            if (key == clockSent) return;
            clockSent = key;
            Broadcast(msg);
        }

        private void Watch(string kind, string? key)
        {
            waits.TryGetValue(kind, out var wait);
            if (wait?.Key == key) return;
            if (wait != null)
            {
                Cancel(wait.Timer);
                waits.Remove(kind);
                if (key == null) Broadcast(new DeadlineMessage { Kind = kind, Ms = null });
            }
            if (key == null) return;
            var ms = AnswerWaitMs[kind];
            var now = clock.Now;
            var w = new Waiting { Key = key, At = now + ms, Since = now };
            w.Timer = After(ms, () => AnswerForPartner(kind, key));
            waits[kind] = w;
            Broadcast(new DeadlineMessage { Kind = kind, Ms = ms });
        }

        /// <summary>Who is to answer a question of `kind`: the one who didn't ask.</summary>
        private int? Answerer(string kind)
        {
            var by = kind == DeadlineKinds.Evac ? state?.EvacCall?.By : state?.AbandonCall?.By;
            return by == null ? (int?)null : by == 0 ? 1 : 0;
        }

        /// <summary>The wait ran out: the decision goes ahead, as if the partner had said yes.</summary>
        private void AnswerForPartner(string kind, string key)
        {
            if (!waits.TryGetValue(kind, out var wait) || wait.Key != key) return;
            var now = clock.Now;
            // Whoever is to answer has the window minimized or the laptop asleep: the question waits for them, up to AwayGraceMs.
            var answerer = Answerer(kind);
            var away = answerer != null && seats[answerer.Value]?.Away == true;
            if (away && now - wait.Since < AwayGraceMs)
            {
                var left = Math.Max(1000, wait.Since + AwayGraceMs - now);
                wait.At = now + left;
                wait.Timer = After(left, () => AnswerForPartner(kind, key));
                Broadcast(new DeadlineMessage { Kind = kind, Ms = left });
                return;
            }
            waits.Remove(kind);
            if (kind == DeadlineKinds.Evac && state?.EvacCall != null && phase == RoomPhases.Mission)
            {
                var partner = state.EvacCall.By == 0 ? 1 : 0;
                var result = rules.ExecuteCommand(state, new EvacAnswerCommand { Accept = true }, partner, ActiveSlots(), rng);
                say($"[room] Evacuación sin respuesta: sale el autobús{(result.Ok ? "" : $" ({result.Error})")}.");
                if (result.Ok)
                {
                    Broadcast(new EventsMessage { Events = result.Events ?? new List<GameEvent>(), Seq = state.Seq, By = null });
                    AfterTactical();
                }
            }
            if (kind == DeadlineKinds.Abandon && state?.AbandonCall != null && phase == RoomPhases.Mission)
            {
                var partner = state.AbandonCall.By == 0 ? 1 : 0;
                var events = rules.AnswerAbandon(state, partner, true);
                say("[room] Abandono sin respuesta: se abandona la misión.");
                if (events.Count > 0)
                {
                    Broadcast(new EventsMessage { Events = events, Seq = state.Seq, By = null });
                    AfterTactical();
                }
            }
            Broadcast(new DeadlineMessage { Kind = kind, Ms = null });
            CheckWaits();
        }

        // -------------------------------------------------------------- helpers

        private List<int> ConnectedSlots()
        {
            return seats.Where((s) => s?.Socket != null).Select((s) => s!.Slot).ToList();
        }

        /// <summary>Seats that still count for the end of turn: connected, or dropped less than the grace period ago.</summary>
        private List<int> ActiveSlots()
        {
            var now = clock.Now;
            return seats
                .Where((s) => s != null && (s.Socket != null || (s.DroppedAt != null && now - s.DroppedAt.Value < (s.Away ? AwayGraceMs : ReconnectGraceMs))))
                .Select((s) => s!.Slot)
                .ToList();
        }

        /// <summary>
        /// The host is shutting down (its app quits): the game is saved, then everyone hears it, so the partner stops
        /// waiting; then every connection closes (what was sent still goes out). On the room's thread: RoomHost.Stop posts it.
        /// </summary>
        public void Closing()
        {
            Persist();
            Broadcast(new ClosingMessage());
            foreach (var link in links.ToList()) link.Socket.Close("cierre");
        }

        /// <summary>Who is playing and where, for the app's announcement on the local network (LanBeacon), from any thread.</summary>
        public (IReadOnlyList<string> players, string phase) Summary()
        {
            lock (summaryGate) return (summaryPlayers, summaryPhase);
        }

        private void Summarize()
        {
            var players = Players().Where((p) => p.Connected).Select((p) => p.Name).ToList();
            lock (summaryGate)
            {
                summaryPlayers = players;
                summaryPhase = phase;
            }
        }

        private List<PlayerInfo> Players()
        {
            return seats
                .Where((s) => s != null)
                .Select((s) => new PlayerInfo { Slot = s!.Slot, Name = s.Name, Color = PlayerColors[s.Slot], Connected = s.Socket != null, Away = s.Away ? true : (bool?)null })
                .ToList();
        }

        private void BroadcastRoom()
        {
            Broadcast(RoomNow());
        }

        private RoomMessage RoomNow()
        {
            return new RoomMessage { Players = Players(), Phase = phase };
        }

        private void Broadcast(ServerMessage msg)
        {
            // What happened has to play on both screens before the turn clock goes on (CheckTurnClock).
            if (msg is EventsMessage)
            {
                idle[0] = false;
                idle[1] = false;
            }
            var data = Json.Write(msg);
            foreach (var s in seats) s?.Socket?.Send(data);
        }

        private void ToPartner(Seat seat, ServerMessage msg)
        {
            foreach (var other in seats)
                if (other != null && other != seat && other.Socket != null) Send(other.Socket, msg);
        }

        private static void Send(IConnection socket, ServerMessage msg)
        {
            socket.Send(Json.Write(msg));
        }

        private void Persist()
        {
            try
            {
                store.Save(new SaveFile
                {
                    Seats = seats.Select((s) => s == null ? null : new SavedSeat { Slot = s.Slot, Name = s.Name, Token = s.Token }).ToList(),
                    Phase = phase,
                    Mode = "skirmish",
                    State = state,
                    ResultApplied = false,
                    Rng = rng.State,
                    MissionKind = missionKind,
                    SkirmishMap = skirmishMap,
                    SkirmishVeterans = skirmishVeterans,
                    SkirmishNight = skirmishNight,
                });
                saveFailed = false;
            }
            catch (Exception e)
            {
                // A disk that's full or an antivirus holding the file (Windows) must not stop the game: the next change tries again.
                var line = $"[save] No se pudo guardar la partida: {e.Message}";
                say(line);
                store.Log(line);
                if (!saveFailed) Broadcast(new ErrorMessage { Message = "No se pudo guardar la partida. Se vuelve a intentar con el siguiente cambio." });
                saveFailed = true;
            }
        }
    }
}
