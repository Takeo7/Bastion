#nullable enable
// base.ts: the types GameState.base needs (the rest of base.ts, its types, tables and functions, is in Base.cs).
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static class PieceIds
    {
        // The buildings: the facilities of before, with a place on the map (§11.3).
        public const string Caravan = "caravan";
        public const string Radio = "radio";
        public const string Quarters = "quarters";
        public const string Infirmary = "infirmary";
        public const string Workshop = "workshop";
        public const string Garden = "garden";
        public const string Quarantine = "quarantine";
        public const string Memorial = "memorial";
        public const string Range = "range";
        public const string Training = "training";
        public const string Echoes = "echoes";
        public const string Garage = "garage";
        public const string Simulation = "simulation";
        // Defences built by hand (§11.4).
        public const string Palisade = "palisade";
        public const string Wall = "wall";
        public const string Gate = "gate";
        public const string Sandbags = "sandbags";
        public const string Logs = "logs";
        public const string Tower = "tower";
        public const string Floodlight = "floodlight";
        public const string Wire = "wire";
        // And those that defend on their own, and are spent (§11.4).
        public const string Mine = "mine";
        public const string Spikes = "spikes";
        public const string Snare = "snare";
        public const string Turret = "turret";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Caravan, Radio, Quarters, Infirmary, Workshop, Garden, Quarantine, Memorial, Range, Training, Echoes,
            Garage, Simulation, Palisade, Wall, Gate, Sandbags, Logs, Tower, Floodlight, Wire, Mine, Spikes, Snare,
            Turret,
        };
    }

    /// <summary>Something standing on the base: where, which way round, and how it is.</summary>
    public sealed class BasePiece
    {
        public string Id { get; set; } = "";
        public string Piece { get; set; } = ""; // PieceIds
        /// <summary>Its top-left tile once turned.</summary>
        public int X { get; set; }
        public int Y { get; set; }
        /// <summary>Quarter turns clockwise, 0–3.</summary>
        public int Rot { get; set; }
        /// <summary>
        /// Its size unturned, when not its kind's: one built before the base grew (docs/MAPAS.md phase 6), as it stood, until
        /// it is improved or built again.
        /// </summary>
        public int[]? Size { get; set; } // [number, number]: across (x) and deep (y).
        public int Hp { get; set; }
        /// <summary>A building's level, 1–3.</summary>
        public int? Level { get; set; }
        /// <summary>A trap's or a turret's uses left; at 0, set again (or reloaded) with piezas.</summary>
        public int? Charges { get; set; }
    }
}
