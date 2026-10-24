using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Bastion.Engine;
using Newtonsoft.Json.Linq;

namespace Bastion.Net
{
    /// <summary>
    /// A single co-op session for two seats (room.ts Room): skirmishes and, with ICampaignRules, the campaign and its
    /// base. The room owns the authoritative state: the campaign and the tactical mission in progress. Clients only send
    /// intents; tactical results stream as events, the campaign is sent whole.
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
        /// case they left; 16/10: 90 s was too long). Leaving the mission, two minutes (Arturo, 22/10: Ana was taken home
        /// unasked).
        /// </summary>
        private static readonly IReadOnlyDictionary<string, long> AnswerWaitMs = new Dictionary<string, long> { [DeadlineKinds.Proposal] = 30_000, [DeadlineKinds.Evac] = 120_000, [DeadlineKinds.Abandon] = 120_000 };

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
            /// <summary>The base screen this player is on (BaseViews), so a reconnecting partner sees it.</summary>
            public string? View;
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
        /// <summary>The campaign's rules; without them, skirmishes only.</summary>
        private readonly ICampaignRules? campaignRules;
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
        private string mode = "skirmish"; // skirmish | campaign
        private GameState? state;
        private CampaignState? campaign;
        /// <summary>Whether the finished campaign mission has already been folded into the campaign.</summary>
        private bool resultApplied;
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
        /// store's Log. `campaignRules`: the campaign's; without them, skirmishes only.
        /// </summary>
        public Room(SaveStore store, IRules rules, string version, IReadOnlyList<HostAddress>? hosts = null, IClock? clock = null, Rng? rng = null, Action<string>? say = null, ICampaignRules? campaignRules = null)
        {
            this.store = store;
            this.rules = rules;
            this.campaignRules = campaignRules;
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
            // Without the campaign's rules, only a skirmish's mission goes on; the campaign stays in the save as it was.
            campaign = saved.Campaign;
            if (campaignRules != null)
            {
                phase = saved.Phase;
                mode = saved.Mode;
                state = saved.State;
                resultApplied = saved.ResultApplied;
            }
            else if (saved.Mode == "skirmish" && saved.Phase == RoomPhases.Mission && saved.State != null)
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
            this.say($"[room] Partida restaurada ({phase}{(campaign != null ? $", campaña día {campaign.Day}" : "")}).");
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
                msg = Json.ReadFromPlayer<ClientMessage>(text);
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
                case CampaignCmdMessage m:
                    return m.Cmd != null && ValidCampaignCommand(m.Cmd);
                case BaseViewMessage m:
                    return BaseViews.All.Contains(m.View);
                case BaseCursorMessage m:
                    return (m.Piece == null || Base.Pieces.ContainsKey(m.Piece)) && BaseTile(m.X, m.Y) && Turn(m.Rot);
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

        /// <summary>
        /// protocol.ts campaignCommandSchema, past what the types already say (Types/CampaignCommand.cs lists it): ids,
        /// tiles of the base and turns in bounds, and the names of things among the engine's.
        /// </summary>
        private static bool ValidCampaignCommand(CampaignCommand cmd)
        {
            switch (cmd)
            {
                case PlaceCommand c:
                    return Has(Base.Pieces, c.Piece) && BaseTile(c.X, c.Y) && Turn(c.Rot);
                case FellCommand c:
                    return BaseTiles(c.Tiles, 400);
                case MendCommand c:
                    return Id(c.Piece);
                case RaiseCommand c:
                    return Id(c.Piece);
                case PlaceLineCommand c:
                    return Has(Base.Pieces, c.Piece) && BaseTiles(c.Cells, 60) && Turn(c.Rot);
                case PostCommand c:
                    return Id(c.Soldier) && (c.At == null || BaseTile(c.At.X, c.At.Y));
                case RearmCommand c:
                    return Id(c.Piece);
                case DismantleCommand c:
                    return Id(c.Piece);
                case ChooseMissionCommand c:
                    return Id(c.Id);
                case ContactCommand c:
                    return Has(Campaign.Regions, c.Region);
                case AnswerEventCommand c:
                    return c.Option == 0 || c.Option == 1;
                case InvestigateCommand c:
                    return Id(c.Id);
                case SquadCommand c:
                    return Id(c.Soldier);
                case UnlockCommand c:
                    return Id(c.Soldier) && Has(Board.BoardCells, c.Cell);
                case RepairCommand c:
                    return Id(c.Weapon);
                case ModCommand c:
                    return Id(c.Weapon) && Has(Content.WeaponMods, c.Mod);
                case BuyCommand c:
                    return Has(Content.Items, c.Item);
                case EquipCommand c:
                    return Id(c.Soldier) && ItemSlots.All.Contains(c.Slot) && (c.Item == null || Has(Content.Items, c.Item));
                case CustomizeCommand c:
                    return Id(c.Soldier) && c.Name.Length <= Appearances.NameMax * 2 && c.Nickname.Length <= Appearances.NicknameMax * 2 && ValidAppearance(c.Appearance);
                case TreatCommand c:
                    return Id(c.Soldier);
                case MedicateCommand c:
                    return Id(c.Soldier);
                case CraftCommand c:
                    return Has(Progression.Crafts, c.Project);
                case AssignWeaponCommand c:
                    return Id(c.Weapon) && (c.Soldier == null || Id(c.Soldier));
                default:
                    // levelUp, cancelMission, recruit, advance, launch, vote, withdraw: nothing past their types.
                    return true;
            }
        }

        /// <summary>protocol.ts appearanceSchema: its colours `#rrggbb`, its head and build the engine's; the pieces may be missing.</summary>
        private static bool ValidAppearance(Appearance? a)
        {
            return a != null && Colour(a.Primary) && Colour(a.Secondary) && Colour(a.Visor) && Has(Appearances.Heads, a.Head) && Has(Appearances.Builds, a.Build)
                && (a.Sex == null || Sexes.All.Contains(a.Sex)) && (a.Helmet == null || Colour(a.Helmet)) && (a.Torso == null || Colour(a.Torso))
                && (a.Arms == null || Colour(a.Arms)) && (a.Legs == null || Colour(a.Legs)) && (a.Character == null || Character(a.Character));
        }

        /// <summary>/^#[0-9a-f]{6}$/i.</summary>
        private static bool Colour(string? s)
        {
            return s != null && s.Length == 7 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);
        }

        /// <summary>/^[a-z][a-zA-Z0-9]{0,23}$/: which survivor model a soldier is.</summary>
        private static bool Character(string s)
        {
            return s.Length >= 1 && s.Length <= 24 && s[0] >= 'a' && s[0] <= 'z' && s.All((ch) => (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'));
        }

        /// <summary>A key of one of the engine's tables (z.enum(keys(…))).</summary>
        private static bool Has<T>(IReadOnlyDictionary<string, T> table, string? key) => key != null && table.ContainsKey(key);

        /// <summary>A tile of the base's map (docs/FASE12.md §11): 0–255 each way.</summary>
        private static bool BaseTile(int x, int y) => x >= 0 && x <= 255 && y >= 0 && y <= 255;

        private static bool BaseTiles(List<Vec2>? tiles, int max) => tiles != null && tiles.Count >= 1 && tiles.Count <= max && tiles.All((t) => t != null && BaseTile(t.X, t.Y));

        private static bool Turn(int rot) => rot >= 0 && rot <= 3;

        /// <summary>protocol.ts `id`: 1–16 characters.</summary>
        private static bool Id(string? id) => id != null && id.Length >= 1 && id.Length <= 16;

        private static bool UnitId(string? id) => Id(id);

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
            // A partner joining a campaign started without them gets soldiers of their own.
            if (phase == RoomPhases.Base) BringPlayersIn();
            BroadcastRoom();
            if (campaign != null && phase != RoomPhases.Lobby) Send(socket, new CampaignMessage { State = campaign });
            foreach (var other in seats)
                if (other != null && other != seat && other.View != null) Send(socket, new PartnerBaseViewMessage { Slot = other.Slot, View = other.View });
            if (phase == RoomPhases.Mission && state != null) Send(socket, new SnapshotMessage { State = state });
            Persist();
            ResyncTurn();
            // A proposal saved before the host restarted had no clock: it gets one when someone is back.
            CheckWaits();
            // Back in: how long the open decisions have left, and the turn clock.
            foreach (var kind in new[] { DeadlineKinds.Proposal, DeadlineKinds.Evac, DeadlineKinds.Abandon })
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
                        // Not over a campaign mission left half played: it would be lost, and played again.
                        if (PausedMission() != null)
                        {
                            Send(seat.Socket!, new ErrorMessage { Message = "Antes, acabad la misión de la campaña que está a medias: «Continuar campaña»." });
                            return;
                        }
                        if (m.Veterans != null) skirmishVeterans = m.Veterans.Value;
                        if (m.Night != null) skirmishNight = m.Night.Value;
                        StartSkirmish(m.Mission, m.Map);
                    }
                    break;
                case NewMissionMessage m:
                    if (phase != RoomPhases.Mission || mode != "skirmish" || state?.Outcome == null) return;
                    if (m.Night != null) skirmishNight = m.Night.Value;
                    StartSkirmish(m.Mission, m.Map);
                    break;
                case BackToMenuMessage _:
                case LeaveCampaignMessage _:
                    // From the base; from a skirmish, over or not (it ends there); from a campaign mission under way, which
                    // waits as it is for «Continuar campaña» (playtest 19/10, #7). A campaign mission over goes back to the base first.
                    if (phase != RoomPhases.Base && phase != RoomPhases.Mission) return;
                    if (phase == RoomPhases.Mission && mode == "skirmish") state = null;
                    else if (phase == RoomPhases.Mission && (state == null || state.Outcome != null)) return;
                    phase = RoomPhases.Lobby;
                    BroadcastRoom();
                    Persist();
                    CheckWaits();
                    break;

                // ---- campaign
                case NewCampaignMessage _:
                {
                    if (phase != RoomPhases.Lobby || campaignRules == null) return;
                    // The campaign it replaces is kept as it was, next to the save: the client asks first, but a click is easy.
                    if (campaign != null)
                    {
                        try
                        {
                            var kept = store.KeepCopy("antes-de-campana-nueva");
                            if (kept != null) store.Log($"[save] La campaña anterior queda en {kept}.");
                        }
                        catch (IOException e)
                        {
                            store.Log($"[save] {e.Message}");
                            Send(seat.Socket!, new ErrorMessage { Message = "No se pudo guardar aparte la campaña de ahora: no se empieza otra." });
                            return;
                        }
                    }
                    // A mission of the old one left half played goes with it (in the copy).
                    if (PausedMission() != null) state = null;
                    campaign = campaignRules.CreateCampaign(ConnectedSlots(), rng);
                    // It opens with the first operation chosen and its squad in the hangar (playtest 2026-10-13).
                    campaignRules.OpeningMission(campaign);
                    say($"[room] Nueva campaña con {campaign.Players.Count} jugador(es).");
                    EnterBase();
                    break;
                }
                case ContinueCampaignMessage _:
                    if (phase != RoomPhases.Lobby || campaign == null || campaignRules == null) return;
                    // Back to a mission left half played, as it was; else to the base.
                    if (PausedMission() != null) ResumeMission();
                    else EnterBase();
                    break;
                case CampaignCmdMessage m:
                {
                    var cmd = m.Cmd!;
                    // Orders that don't go through are written to red.log, to find out why when a player says nothing
                    // happened (Arturo, 18/10: a skill that would not unlock, with no message).
                    if (phase != RoomPhases.Base || campaign == null || campaignRules == null)
                    {
                        store.Log($"[campaña] {seat.Name}: «{cmd.Type}» ignorada (fase {phase}{(campaign != null ? "" : ", sin campaña")})");
                        return;
                    }
                    var result = campaignRules.ExecuteCampaignCommand(campaign, cmd, seat.Slot, ConnectedSlots(), rng);
                    if (!result.Ok)
                    {
                        store.Log($"[campaña] {seat.Name} (puesto {seat.Slot + 1}): «{cmd.Type}» rechazada: {result.Error}{CommandDetail(cmd)}");
                        Send(seat.Socket!, new RejectedMessage { Message = result.Error ?? "" });
                        return;
                    }
                    if (cmd.Type == "unlock") store.Log($"[campaña] {seat.Name}: {Field(cmd, "cell")} para {Field(cmd, "soldier")}{CommandDetail(cmd)}");
                    if (result.Launch) LaunchCampaignMission();
                    else Broadcast(new CampaignMessage { State = campaign });
                    Persist();
                    CheckWaits();
                    break;
                }
                case ReturnToBaseMessage _:
                    if (phase != RoomPhases.Mission || mode != "campaign" || state?.Outcome == null) return;
                    state = null;
                    EnterBase();
                    break;
                case BaseViewMessage m:
                    seat.View = m.View;
                    ToPartner(seat, new PartnerBaseViewMessage { Slot = seat.Slot, View = m.View });
                    break;
                case BaseCursorMessage m:
                    // What the partner has in hand on the base, passed on as it comes (docs/FASE12.md §11.7).
                    if (phase == RoomPhases.Base) ToPartner(seat, new PartnerBaseCursorMessage { Slot = seat.Slot, Piece = m.Piece, X = m.X, Y = m.Y, Rot = m.Rot });
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
            mode = "skirmish";
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

        /// <summary>Every connected player takes part in the campaign (one who arrives late gets soldiers).</summary>
        private void BringPlayersIn()
        {
            var c = campaign;
            if (c == null || campaignRules == null) return;
            var changed = false;
            foreach (var slot in ConnectedSlots())
                if (campaignRules.AddPlayer(c, slot, rng)) changed = true;
            if (!changed) return;
            // A squad being picked is redone with both commanders' soldiers.
            if (c.ActiveMission != null)
            {
                c.Squad = campaignRules.DefaultSquad(c, ConnectedSlots());
                campaignRules.SyncPads(c);
                c.LaunchReady = new bool[2];
            }
            Broadcast(new CampaignMessage { State = c });
            Persist();
        }

        private void EnterBase()
        {
            BringPlayersIn();
            phase = RoomPhases.Base;
            mode = "campaign";
            BroadcastRoom();
            if (campaign != null) Broadcast(new CampaignMessage { State = campaign });
            Persist();
        }

        /// <summary>A campaign mission left for the menu half played (phase lobby), if there is one.</summary>
        private GameState? PausedMission()
        {
            return phase == RoomPhases.Lobby && mode == "campaign" && state != null && state.Outcome == null ? state : null;
        }

        /// <summary>Back to the campaign mission left half played, as it was.</summary>
        private void ResumeMission()
        {
            phase = RoomPhases.Mission;
            say($"[room] Vuelta a la misión de campaña{(state?.CampaignMission != null ? $": {state.CampaignMission.Name}" : "")}.");
            BroadcastRoom();
            if (campaign != null) Broadcast(new CampaignMessage { State = campaign });
            Broadcast(new SnapshotMessage { State = state });
            Persist();
        }

        private void LaunchCampaignMission()
        {
            var c = campaign!;
            var made = campaignRules!.LaunchMission(c, rng);
            if (made == null) return;
            state = made.Value.state;
            resultApplied = false;
            phase = RoomPhases.Mission;
            say($"[room] Misión de campaña: {made.Value.name}.");
            BroadcastRoom();
            Broadcast(new CampaignMessage { State = c });
            Broadcast(new SnapshotMessage { State = state });
        }

        /// <summary>After any tactical change: fold a finished campaign mission into the campaign once.</summary>
        private void AfterTactical()
        {
            if (mode == "campaign" && campaign != null && campaignRules != null && state?.Outcome != null && !resultApplied)
            {
                campaignRules.ApplyMissionResult(campaign, state, rng);
                resultApplied = true;
                Broadcast(new CampaignMessage { State = campaign });
            }
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
            var proposal = phase == RoomPhases.Base ? campaign?.Proposal : null;
            Watch(DeadlineKinds.Proposal, proposal != null ? Json.Write(proposal) : null);
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
            var by = kind == DeadlineKinds.Proposal ? campaign?.Proposal?.By : kind == DeadlineKinds.Evac ? state?.EvacCall?.By : state?.AbandonCall?.By;
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
            // Nobody here: it waits for whoever comes back (Join starts the clock again).
            if (kind == DeadlineKinds.Proposal && ConnectedSlots().Count == 0) return;
            if (kind == DeadlineKinds.Proposal && campaign?.Proposal != null && campaignRules != null && phase == RoomPhases.Base)
            {
                var partner = campaign.Proposal.By == 0 ? 1 : 0;
                // Who is really here: a mission chosen this way fills La Lata as without the partner, not with their soldiers.
                var result = campaignRules.ExecuteCampaignCommand(campaign, new VoteCommand { Accept = true }, partner, ConnectedSlots(), rng);
                say($"[room] Propuesta sin respuesta: sigue adelante{(result.Ok ? "" : $" ({result.Error})")}.");
                if (result.Ok && result.Launch) LaunchCampaignMission();
                else Broadcast(new CampaignMessage { State = campaign });
                Persist();
            }
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

        /// <summary>For red.log: the soldier an order is about, as the campaign has it (whose, alive, points, board).</summary>
        private string CommandDetail(CampaignCommand cmd)
        {
            var id = Field(cmd, "soldier");
            var s = id != null ? campaign?.Soldiers.Find((x) => x.Id == id) : null;
            if (s == null) return id != null ? $" · {id} no está en la campaña" : "";
            return $" · {s.Name} ({s.Id}), de {s.Owner + 1}, {(s.Alive ? "vivo" : "caído")}, {Bars.PointsLeft(s.Skills, s.Board)} puntos, tablero [{string.Join(" ", s.Board)}] · conectados {string.Join(",", ConnectedSlots().Select((x) => x + 1))}";
        }

        /// <summary>A field of an order, by its name in the JSON (room.ts reads `'soldier' in cmd`), or null.</summary>
        private static string? Field(CampaignCommand cmd, string name)
        {
            var value = JObject.FromObject(cmd, Json.Serializer)[name];
            return value?.Type == JTokenType.String ? (string?)value : null;
        }

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
            var paused = PausedMission();
            var saved = campaign != null && campaign.Outcome == null && campaignRules != null
                ? new SavedCampaign { Day = campaign.Day, Missions = campaign.MissionsFlown ?? 0, Doom = campaign.Doom, Level = Progression.BastionLevel(campaign.Level), InMission = paused != null ? paused.CampaignMission?.Name ?? paused.MapName : null }
                : null;
            // A campaign from before the 0.7 kept aside (docs/FASE9.md §7): its file's name, for the lobby to say so.
            return new RoomMessage { Players = Players(), Phase = phase, SavedCampaign = saved, SetAside = store.SetAside != null ? Path.GetFileName(store.SetAside) : null };
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
                    Mode = mode,
                    State = state,
                    Campaign = campaign,
                    ResultApplied = resultApplied,
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
