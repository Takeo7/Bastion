using System.Collections.Generic;
using Bastion.Engine;

namespace Bastion.Net
{
    // The messages between a player's app and the host (protocol.ts ClientMessage and ServerMessage), as far as the
    // first milestone goes: a mission between two apps (docs/UNITY.md). Each one a class told by its `t`, as in TypeScript.
    // New since TypeScript: `version` in hello and welcome (two apps of different builds must not play together),
    // `report` (the client's telemetry and errors, which went by POST to /__report) and `replaced` (the WebSocket's close
    // code 4000: TCP has none). Left for later: the campaign and the base, and real time.

    /// <summary>The screen the room is on (protocol.ts RoomPhase). The first milestone has no base.</summary>
    public static class RoomPhases
    {
        public const string Lobby = "lobby";
        public const string Base = "base";
        public const string Mission = "mission";
    }

    /// <summary>The joint decisions that wait for the partner's answer (protocol.ts DeadlineKind); a base proposal comes with the base.</summary>
    public static class DeadlineKinds
    {
        public const string Proposal = "proposal";
        public const string Evac = "evac";
        public const string Abandon = "abandon";
    }

    /// <summary>The maps a skirmish is played on (protocol.ts skirmishMapSchema).</summary>
    public static class SkirmishMaps
    {
        public const string Plaza = "plaza";
        public const string City = "city";
        public const string Wilds = "wilds";
        public const string Facility = "facility";
        public const string Torre = "torre";
        public const string Roadside = "roadside";
        public const string Industrial = "industrial";
        public const string Bastion = "bastion";

        public static readonly IReadOnlyList<string> All = new[] { Plaza, City, Wilds, Facility, Torre, Roadside, Industrial, Bastion };
    }

    /// <summary>What a player's app says to the host.</summary>
    public abstract class ClientMessage
    {
        public abstract string T { get; }
    }

    /// <summary>The seat asked for: by token if the app has one, else by name (a free seat, or one of that name nobody plays).</summary>
    public sealed class HelloMessage : ClientMessage
    {
        public override string T => "hello";
        public string Name { get; set; } = "";
        public string? Token { get; set; }
        public string? Version { get; set; }
    }

    /// <summary>A skirmish (the only game of the first milestone).</summary>
    public sealed class StartMessage : ClientMessage
    {
        public override string T => "start";
        public string? Mission { get; set; } // MissionKinds
        public string? Map { get; set; } // SkirmishMaps
        public bool? Veterans { get; set; }
        public bool? Night { get; set; }
    }

    public sealed class NewMissionMessage : ClientMessage
    {
        public override string T => "newMission";
        public string? Mission { get; set; }
        public string? Map { get; set; }
        public bool? Night { get; set; }
    }

    /// <summary>From a skirmish, over or not, back to the menu: it ends there (campaignMessageSchema, which also leaves the base).</summary>
    public sealed class BackToMenuMessage : ClientMessage
    {
        public override string T => "backToMenu";
    }

    /// <summary>Ends the current mission for both players: asked of the partner first, if here (playtest 16/10).</summary>
    public sealed class AbandonMessage : ClientMessage
    {
        public override string T => "abandon";
    }

    public sealed class AbandonAnswerMessage : ClientMessage
    {
        public override string T => "abandonAnswer";
        public bool Accept { get; set; }
    }

    /// <summary>Ask for a full snapshot (the client detected a gap in the event stream).</summary>
    public sealed class SyncMessage : ClientMessage
    {
        public override string T => "sync";
    }

    public sealed class CmdMessage : ClientMessage
    {
        public override string T => "cmd";
        public Command? Cmd { get; set; }
    }

    /// <summary>The unit selected and the tile under the pointer, shown to the partner.</summary>
    public sealed class PresenceMessage : ClientMessage
    {
        public override string T => "presence";
        public string? Unit { get; set; }
        public Pos? Tile { get; set; }
    }

    /// <summary>Marks a tile for the partner ("go here", "this enemy").</summary>
    public sealed class PingMessage : ClientMessage
    {
        public override string T => "ping";
        public Pos Tile { get; set; } = new Pos();
        public string Kind { get; set; } = "point"; // point | enemy
    }

    /// <summary>A move planned without the command, shown to the partner; no tile withdraws it.</summary>
    public sealed class PlanMessage : ClientMessage
    {
        public override string T => "plan";
        public string Unit { get; set; } = "";
        public Pos? Tile { get; set; }
    }

    /// <summary>
    /// The game went out of sight (its window minimized or behind another, the laptop asleep) or came back: while away, a
    /// dropped connection keeps its turn and the command longer. Always answered with `room`, which the client takes as
    /// proof the connection survived the sleep.
    /// </summary>
    public sealed class AwayMessage : ClientMessage
    {
        public override string T => "away";
        public bool Away { get; set; }
    }

    /// <summary>This screen has played every event it was sent (`idle`), or has some to play: the turn clock runs only meanwhile (Arturo, 22/10).</summary>
    public sealed class IdleMessage : ClientMessage
    {
        public override string T => "idle";
        public bool Idle { get; set; }
    }

    /// <summary>A line for the host's red.log: how the screen draws (`[rend …]`), an error, a hang.</summary>
    public sealed class ReportMessage : ClientMessage
    {
        public override string T => "report";
        public string Line { get; set; } = "";
    }

    /// <summary>What the host says to a player's app.</summary>
    public abstract class ServerMessage
    {
        public abstract string T { get; }
    }

    /// <summary>The seat given: its token (kept by the app), its slot, where the partner can connect, and the host's version.</summary>
    public sealed class WelcomeMessage : ServerMessage
    {
        public override string T => "welcome";
        public string Token { get; set; } = "";
        public int Slot { get; set; }
        public List<HostAddress>? Hosts { get; set; }
        public string? Version { get; set; }
    }

    public sealed class PlayerInfo
    {
        public int Slot { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public bool Connected { get; set; }
        /// <summary>Their game out of sight (the `away` message): back soon, their turn waits.</summary>
        public bool? Away { get; set; }
    }

    /// <summary>Who is in the room and which screen it is on (lobby or mission, for now).</summary>
    public sealed class RoomMessage : ServerMessage
    {
        public override string T => "room";
        public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
        public string Phase { get; set; } = RoomPhases.Lobby;
    }

    public sealed class SnapshotMessage : ServerMessage
    {
        public override string T => "snapshot";
        public GameState? State { get; set; }
    }

    /// <summary>What a command (or the host itself) made happen, in order; `seq` the state's after them, `by` whose command.</summary>
    public sealed class EventsMessage : ServerMessage
    {
        public override string T => "events";
        public List<GameEvent> Events { get; set; } = new List<GameEvent>();
        public int Seq { get; set; }
        public int? By { get; set; }
    }

    public sealed class PartnerPresenceMessage : ServerMessage
    {
        public override string T => "presence";
        public int Slot { get; set; }
        public string? Unit { get; set; }
        public Pos? Tile { get; set; }
    }

    public sealed class PartnerPingMessage : ServerMessage
    {
        public override string T => "ping";
        public int Slot { get; set; }
        public Pos Tile { get; set; } = new Pos();
        public string Kind { get; set; } = "point";
    }

    public sealed class PartnerPlanMessage : ServerMessage
    {
        public override string T => "plan";
        public int Slot { get; set; }
        public string Unit { get; set; } = "";
        public Pos? Tile { get; set; }
    }

    /// <summary>A command the rules refused, and why (for the player who sent it).</summary>
    public sealed class RejectedMessage : ServerMessage
    {
        public override string T => "rejected";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// A joint decision waits for the partner (playtest 2026-10-14, #28): a call to evacuate or to leave goes ahead on
    /// its own after `ms` more milliseconds, counted on the client's own clock; no `ms`, it was answered.
    /// </summary>
    public sealed class DeadlineMessage : ServerMessage
    {
        public override string T => "deadline";
        public string Kind { get; set; } = ""; // proposal | evac | abandon
        public long? Ms { get; set; }
    }

    /// <summary>One player's turn clock: milliseconds left of the turn's total, and whether it runs now.</summary>
    public sealed class TurnClock
    {
        public long Left { get; set; }
        public long Total { get; set; }
        public bool Running { get; set; }
    }

    /// <summary>The squad's turn clock (Arturo, 22/10), each player's, by slot; none outside the squad's turn.</summary>
    public sealed class TurnClockMessage : ServerMessage
    {
        public override string T => "turnClock";
        public List<TurnClock>? Clocks { get; set; }
    }

    /// <summary>The host is shutting down (its app quit): the game is saved; don't wait for it to come back.</summary>
    public sealed class ClosingMessage : ServerMessage
    {
        public override string T => "closing";
    }

    /// <summary>This seat was opened in another window: this one gives it up.</summary>
    public sealed class ReplacedMessage : ServerMessage
    {
        public override string T => "replaced";
    }

    /// <summary>Something refused: before the welcome, the hello itself (a name taken, a full game, another version).</summary>
    public sealed class ErrorMessage : ServerMessage
    {
        public override string T => "error";
        public string Message { get; set; } = "";
    }
}
