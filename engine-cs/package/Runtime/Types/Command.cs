#nullable enable
// protocol.ts: the Command union (commandSchema, by `type`), one class per variant; and rules.ts CommandResult.
// What commandSchema checks, for whoever reads them off the wire:
// - `unit`, `target`, `to` (a unit) and `id`: 1–16 characters;
// - `ability`: an AbilityIds value (abilityIdSchema: the keys of content.ts ABILITIES);
// - a tile (`to`, `tile`): whole x and y, and z 0–3 or missing. A place (types.ts Pos): without `z`, on the ground
//   (docs/FASE6-PLAN.md); here a Pos, whose Z is 0 when missing (Geometry.cs);
// - ItemCommand.index: 0–7.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>What a player asks of the host in a mission (protocol.ts commandSchema).</summary>
    public abstract class Command
    {
        /// <summary>The command's tag (TypeScript `type`).</summary>
        public abstract string Type { get; }
    }

    public sealed class MoveCommand : Command
    {
        public override string Type => "move";
        public string Unit { get; set; } = "";
        public Pos To { get; set; } = new Pos();
    }

    public sealed class AbilityCommand : Command
    {
        public override string Type => "ability";
        public string Unit { get; set; } = "";
        public string Ability { get; set; } = ""; // AbilityIds
        /// <summary>Unit target (attacks, support abilities).</summary>
        public string? Target { get; set; }
        /// <summary>Tile target (explosives).</summary>
        public Pos? Tile { get; set; }
    }

    public sealed class EndTurnCommand : Command
    {
        public override string Type => "endTurn";
        public bool Ready { get; set; }
    }

    /// <summary>From a soldier in the bus, ask to evacuate; the partner answers (docs/PRUEBA-2026-10-14.md P1).</summary>
    public sealed class EvacCommand : Command
    {
        public override string Type => "evac";
        public string Unit { get; set; } = "";
    }

    public sealed class EvacAnswerCommand : Command
    {
        public override string Type => "evacAnswer";
        public bool Accept { get; set; }
    }

    /// <summary>
    /// The backpack (docs/FASE8.md §C): drop a thing (`index`) or take one from the floor (`id`), free; give one
    /// to a partner next to you (`to`), use a medicine on yourself or on them, or equip a found weapon, one action.
    /// </summary>
    public sealed class ItemCommand : Command
    {
        public override string Type => "item";
        public string Unit { get; set; } = "";
        public string Action { get; set; } = ""; // ItemActions
        public int? Index { get; set; }
        public string? Id { get; set; }
        public string? To { get; set; }
    }

    /// <summary>ItemCommand.action: `'drop' | 'take' | 'give' | 'use' | 'equip'`.</summary>
    public static class ItemActions
    {
        public const string Drop = "drop";
        public const string Take = "take";
        public const string Give = "give";
        public const string Use = "use";
        public const string Equip = "equip";

        public static readonly IReadOnlyList<string> All = new[] { Drop, Take, Give, Use, Equip };
    }

    /// <summary>Hand the command (el mando) to the partner.</summary>
    public sealed class PassCommand : Command
    {
        public override string Type => "pass";
    }

    /// <summary>Real time (docs/TIEMPO-REAL.md): a soldier's standing order: go to `to`, attack `target` until down, or (neither) stand.</summary>
    public sealed class RtOrderCommand : Command
    {
        public override string Type => "rtOrder";
        public string Unit { get; set; } = "";
        public Pos? To { get; set; }
        public string? Target { get; set; }
        public string? Ability { get; set; } // AbilityIds
        public Pos? Tile { get; set; }
    }

    /// <summary>Real time: stop the clock or let it go on, for everyone.</summary>
    public sealed class RtPauseCommand : Command
    {
        public override string Type => "rtPause";
        public bool Paused { get; set; }
    }

    /// <summary>Real time: the clock very slow while an ability is being aimed (Arturo, 20/10), or back to its pace.</summary>
    public sealed class RtSlowCommand : Command
    {
        public override string Type => "rtSlow";
        public bool Slow { get; set; }
    }

    /// <summary>
    /// What a command came to (rules.ts): `{ ok: true; events: GameEvent[] } | { ok: false; error: string }`.
    /// One class for both: `Events` when `Ok`, `Error` when not.
    /// </summary>
    public sealed class CommandResult
    {
        public bool Ok { get; set; }
        /// <summary>When `Ok`: the events the command produced.</summary>
        public List<GameEvent>? Events { get; set; }
        /// <summary>When not `Ok`: why it was refused.</summary>
        public string? Error { get; set; }

        public static CommandResult Success(List<GameEvent> events) => new CommandResult { Ok = true, Events = events };

        public static CommandResult Failure(string error) => new CommandResult { Ok = false, Error = error };
    }
}
