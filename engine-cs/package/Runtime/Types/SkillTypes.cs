#nullable enable
// skills.ts: the types Unit.deeds, Unit.lines and the barFilled event need (the rest of skills.ts, its tables and
// functions, is not here yet).
using System.Collections.Generic;

namespace Bastion.Engine
{
    // ------------------------------------------------------------------- lines

    public static class SkillIds
    {
        public const string Shooting = "shooting";
        public const string Melee = "melee";
        public const string Throwing = "throwing";
        public const string Medicine = "medicine";
        public const string Searching = "searching";
        public const string Fitness = "fitness";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Shooting, Melee, Throwing, Medicine, Searching, Fitness,
        };
    }

    /// <summary>Experience earned in each line, all of it (the bars follow from it: barsOf).</summary>
    /// <remarks>`Record&lt;SkillId, number&gt;`: every SkillIds key.</remarks>
    public sealed class Skills : Dictionary<string, int>
    {
    }

    /// <summary>What each line's filled bars have added to its stat, in percent (rolled as they fill).</summary>
    /// <remarks>`Record&lt;SkillId, number&gt;`: every SkillIds key.</remarks>
    public sealed class Growth : Dictionary<string, int>
    {
    }

    // ---------------------------------------------------------------- learning

    /// <summary>What a soldier did in a mission that teaches (Unit.deeds).</summary>
    public static class DeedIds
    {
        public const string Shot = "shot";
        public const string Hit = "hit";
        public const string GunKill = "gunKill";
        public const string Swing = "swing";
        public const string Blow = "blow";
        public const string BladeKill = "bladeKill";
        public const string Throw = "throw";
        public const string ThrowHit = "throwHit";
        public const string HealOther = "healOther";
        public const string HealSelf = "healSelf";
        public const string Search = "search";
        public const string Fix = "fix";
        public const string Hunker = "hunker";
        public const string Run = "run";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Shot, Hit, GunKill, Swing, Blow, BladeKill, Throw, ThrowHit, HealOther, HealSelf, Search, Fix, Hunker, Run,
        };
    }

    /// <remarks>`Partial&lt;Record&lt;DeedId, number&gt;&gt;`: DeedIds keys, times done.</remarks>
    public sealed class Deeds : Dictionary<string, int>
    {
    }

    /// <summary>
    /// A soldier's lines during a mission (Unit.lines, docs/FASE10.md): a bar
    /// fills there and then, when the action that fills it is over (Arturo,
    /// 16/10), and its stat goes up at once (Sim.settleBars, the `barFilled`
    /// event). `start`, the experience it set out with; `bonus`, what the Taller de
    /// oficios adds to what it learns; `rolls`, what each bar filled out there
    /// added; `growth`, with them.
    /// </summary>
    public sealed class MissionLines
    {
        public Skills Start { get; set; } = new Skills();
        public double Bonus { get; set; } // 0.2 = 20 % (skills.ts learnedFrom).
        public Growth Growth { get; set; } = new Growth();
        public Dictionary<string, List<int>> Rolls { get; set; } = new Dictionary<string, List<int>>(); // SkillIds → rolls (Partial<Record>)
    }
}
