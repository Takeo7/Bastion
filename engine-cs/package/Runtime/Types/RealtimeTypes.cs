#nullable enable
// types.ts: the real-time mode's state.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>
    /// The real-time mode (docs/TIEMPO-REAL.md, step 1): the mission's clock and what each one is doing. The host's clock
    /// (realtime.ts realtimeStep) moves it on a tick at a time; a unit's order stands until it is done or changed.
    /// </summary>
    public sealed class Realtime
    {
        /// <summary>Mission time, in ms.</summary>
        public int Clock { get; set; }
        public bool Paused { get; set; }
        /// <summary>How fast the clock runs (1, or RT_SLOW while someone chooses what an ability is aimed at: realtime.ts).</summary>
        public double? Speed { get; set; }
        /// <summary>By unit id (made as each one first does something).</summary>
        public Dictionary<string, RtUnit> Units { get; set; } = new Dictionary<string, RtUnit>();
    }

    /// <summary>
    /// A standing order: stay; go to a tile; attack someone with an attack (the gun's, kept up until they are down; a
    /// special one, once); keep watch (shoot, or strike, whatever comes in reach); or use an ability once, on oneself, an
    /// ally or a tile (getting there first if it must).
    /// </summary>
    public abstract class RtOrder
    {
        /// <summary>The order's tag (TypeScript `kind`).</summary>
        public abstract string Kind { get; }
    }

    public sealed class IdleRtOrder : RtOrder
    {
        public override string Kind => "idle";
    }

    public sealed class MoveRtOrder : RtOrder
    {
        public override string Kind => "move";
        public Pos To { get; set; } = new Pos();
    }

    public sealed class AttackRtOrder : RtOrder
    {
        public override string Kind => "attack";
        public string Target { get; set; } = "";
        public string? Ability { get; set; } // AbilityIds
    }

    public sealed class WatchRtOrder : RtOrder
    {
        public override string Kind => "watch";
        public bool? Melee { get; set; }
    }

    public sealed class ActRtOrder : RtOrder
    {
        public override string Kind => "act";
        public string Ability { get; set; } = ""; // AbilityIds
        public string? Target { get; set; }
        public Pos? Tile { get; set; }
    }

    /// <summary>
    /// What a unit is doing: nothing, walking a step (its `moved` came when it set off), aiming (the shot comes when it
    /// ends), reloading, winding up a blow (the bite comes when it ends), or waiting (someone in the way, no line of fire,
    /// getting its breath back after a blow).
    /// </summary>
    public static class RtDoings
    {
        public const string Idle = "idle";
        public const string Walk = "walk";
        public const string Aim = "aim";
        public const string Reload = "reload";
        public const string Strike = "strike";
        public const string Wait = "wait";
        public const string Act = "act";

        public static readonly IReadOnlyList<string> All = new[] { Idle, Walk, Aim, Reload, Strike, Wait, Act };
    }

    public sealed class RtUnit
    {
        public RtOrder Order { get; set; } = new IdleRtOrder();
        public string Doing { get; set; } = ""; // RtDoings
        /// <summary>The clock (ms) at which what it is doing ends.</summary>
        public int Until { get; set; }
        /// <summary>Whom it is aiming at or striking.</summary>
        public string? Target { get; set; }
        /// <summary>With what: the attack it aims or strikes with, the ability it is getting ready (`act`).</summary>
        public string? Ability { get; set; } // AbilityIds
        /// <summary>The host's own bookkeeping, never sent: the steps it means to take, how often it has waited for one, and when its own round ends (realtime.ts RT_ROUND).</summary>
        public List<Pos>? Path { get; set; }
        public int? Waited { get; set; }
        public int? Round { get; set; }
    }
}
