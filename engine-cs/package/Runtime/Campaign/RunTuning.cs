#nullable enable
// Knobs for the benches (Producto, 17/11; .dev-snapshots/gameplay/runs/G3-NUMEROS.md): what a run gives and costs, set by a
// bench to measure a variant against today's. Their defaults are today's: nothing changes unless a bench sets them. Each
// flow of work its own (AsyncLocal): a bench plays four campaigns at once, and what a room hands to another thread keeps
// the setting of the campaign it belongs to. A bench sets them at the start of each campaign it plays and resets them after.
using System.Collections.Generic;
using System.Threading;

namespace Bastion.Engine
{
    public static partial class Runs
    {
        public static class Tuning
        {
            /// <summary>What the base gives when nobody is left (NobodyLeft).</summary>
            public static class Wipes
            {
                /// <summary>Today's (N8, Arturo 22/11): nothing; nobody alive in the game, it is lost.</summary>
                public const string Lose = "lose";
                /// <summary>Before N8: one recruit with a knife for each player with nobody alive (a bench measuring past it).</summary>
                public const string One = "one";
                /// <summary>Recruits with a knife till the squad's seats are full (SeatsOf).</summary>
                public const string Fill = "fill";
                /// <summary>As Fill, each with a pistol too.</summary>
                public const string FillPistol = "fillPistol";
            }

            public sealed class Settings
            {
                public string Wipe { get; set; } = Wipes.Lose;
                /// <summary>When the whole squad falls, the share of the § they carried that comes back to the stash (0 today).</summary>
                public double SalvageSupplies { get; set; }
                /// <summary>When the whole squad falls, whether their firearms come back to the stash (false today).</summary>
                public bool SalvageFirearms { get; set; }
                /// <summary>A multiplier on the § a search finds (RunLoot.SuppliesScale; 1 today).</summary>
                public double SuppliesScale { get; set; } = 1;
                /// <summary>The first level's price of the upgrades given (UpgradeIds → §); the rest, Content.UpgradePrices.</summary>
                public Dictionary<string, int>? UpgradePrices { get; set; }
            }

            private static readonly AsyncLocal<Settings?> current = new AsyncLocal<Settings?>();

            /// <summary>The settings of this flow of work; today's (a new one each time: nobody changes the defaults) when none.</summary>
            public static Settings Current => current.Value ?? new Settings();

            /// <summary>Whether a bench set any: the defaults' fast way out.</summary>
            public static bool IsSet => current.Value != null;

            public static void Set(Settings? settings) => current.Value = settings;

            public static void Reset() => current.Value = null;
        }
    }
}
