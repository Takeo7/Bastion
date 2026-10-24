#nullable enable
// types.ts: what a mission holds besides the map and the units.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>A trap of the base on a tile (docs/FASE12.md §11.4): it goes off when one of the dead steps on it. `piece`: its piece on the base.</summary>
    public sealed class Trap
    {
        public string Piece { get; set; } = "";
        public string Kind { get; set; } = ""; // TrapKinds
        public int Node { get; set; }
        public int Charges { get; set; }
    }

    /// <summary>Trap.kind: `'mine' | 'spikes' | 'snare'`.</summary>
    public static class TrapKinds
    {
        public const string Mine = "mine";
        public const string Spikes = "spikes";
        public const string Snare = "snare";

        public static readonly IReadOnlyList<string> All = new[] { Mine, Spikes, Snare };
    }

    /// <summary>
    /// Light at night (docs/ALTURA-Y-NOCHE.md): whoever stands on a tile it lights
    /// is seen from as far as by day. The map's street lights never go out; a
    /// Bengala's light and fire last `turns` of the squad's turns.
    /// </summary>
    public sealed class Light
    {
        public string Id { get; set; } = "";
        public Pos Tile { get; set; } = new Pos();
        public double Radius { get; set; }
        /// <summary>Squad turns it still lasts; absent for the map's own lights.</summary>
        public int? Turns { get; set; }
        /// <summary>The tiles it lights, by node (grid.ts nodeOf; on the ground, y * width + x): within `radius` and in its line of sight (los.ts lightTiles).</summary>
        public List<int>? Tiles { get; set; }
    }

    /// <summary>A smoke cloud: units inside are harder to hit.</summary>
    public sealed class SmokeCloud
    {
        public Pos Pos { get; set; } = new Pos();
        public double Radius { get; set; }
        /// <summary>XCOM turns it still lasts.</summary>
        public int Turns { get; set; }
    }

    public sealed class Pod
    {
        public string Id { get; set; } = "";
        public bool Active { get; set; }
        public List<string> UnitIds { get; set; } = new List<string>();
        /// <summary>Waypoints an inactive pod walks between during the alien turn (empty = it stays put).</summary>
        public List<Pos> Patrol { get; set; } = new List<Pos>();
        public int PatrolIndex { get; set; }
    }

    /// <summary>
    /// Enemies arriving mid-mission. Since the playtest of 2026-10-13, only the
    /// horde (`horde`): zombies that come in by a map edge, where they land, with
    /// no flare. Missions saved before may still hold a drop announced by a flare
    /// one turn before.
    /// </summary>
    public sealed class Reinforcement
    {
        public string Id { get; set; } = "";
        /// <summary>Alien turn at the end of which they land.</summary>
        public int Turn { get; set; }
        /// <summary>A drop's landing tile, chosen when its flare goes up; always null for the horde (no exact spot is ever announced).</summary>
        public Pos? Pos { get; set; }
        public List<string> Units { get; set; } = new List<string>(); // TemplateIds
        public bool Landed { get; set; }
        /// <summary>Saqueo: zombies coming in by the edges, drawn by the noise; they land on the prowl, not alert.</summary>
        public bool? Horde { get; set; }
        /// <summary>The few that come in every turn (playtest 2026-10-15, #11), apart from the waves.</summary>
        public bool? Trickle { get; set; }
        /// <summary>A siege's wave (docs/FASE12.md §4): the map edge it comes in by, a DIRS4 index (0 north, 1 east, 2 south, 3 west).</summary>
        public int? Side { get; set; }
    }

    /// <summary>
    /// A siege of the base (docs/FASE12.md): the dead come in waves
    /// (MissionState.reinforcements, each by its `side`, any of the four) and go
    /// for the soldiers, breaking down the doors and barricades in their way
    /// (GameState.breakables). The base falls when nobody is left to defend it;
    /// it holds once every wave is in and down (Ana, 23/10: not after a set turn;
    /// `hold` is from missions saved before, and no longer ends it).
    /// </summary>
    public sealed class Siege
    {
        public int Hold { get; set; }
    }

    public sealed class MissionItem
    {
        /// <summary>Where it lies, or null while someone carries it.</summary>
        public Pos? Pos { get; set; }
        public string? Carrier { get; set; }
        public bool Evacuated { get; set; }
    }

    public sealed class MissionState
    {
        public string Kind { get; set; } = ""; // MissionKinds
        /// <summary>XCOM turns left to secure the objective; null when there is no timer.</summary>
        public int? TurnsLeft { get; set; }
        public MissionItem? Item { get; set; }
        public List<Vec2> EvacZone { get; set; } = new List<Vec2>();
        /// <summary>The bus parked beside the evac zone (docs/PRUEBA-2026-10-14.md P1): its tiles, and the way it drives off (DIRS4 index).</summary>
        public EvacBus? EvacBus { get; set; }
        /// <summary>Hack missions: the terminal (a `console` tile).</summary>
        public Pos? Terminal { get; set; }
        /// <summary>Sabotage: the relay unit; rescue: the VIP unit.</summary>
        public string? ObjectiveUnit { get; set; }
        /// <summary>Relay destroyed, terminal hacked, data or VIP extracted.</summary>
        public bool ObjectiveDone { get; set; }
        /// <summary>The bus has left (docs/PRUEBA-2026-10-14.md P1): whoever was in it got out, the rest were lost.</summary>
        public bool? EvacDone { get; set; }
        /// <summary>Hack bonus from the campaign (research), added to every hack attempt.</summary>
        public int HackBonus { get; set; }
        /// <summary>A sabotage's alarm (playtest 2026-10-15, #14); missing in older saves and outside the campaign: a siren.</summary>
        public string? Alarm { get; set; } // Alarms
        /// <summary>Medicines also stop a bite's contagion, for everyone (docs/FASE8.md §C: the Enfermería from level 2).</summary>
        public bool? MedicineCures { get; set; }
        /// <summary>The campaign's Bastion has a Cuarentena (playtest 2026-10-15, #13); false, the bitten can't board La Lata. Missing (a skirmish, older saves): no rule.</summary>
        public bool? Quarantine { get; set; }
        public List<Reinforcement> Reinforcements { get; set; } = new List<Reinforcement>();
        /// <summary>1–4, for the loot tables (missing in missions saved before searching: 2).</summary>
        public int? Difficulty { get; set; }
        /// <summary>Noise the squad has made, in horde points (content.ts hordePoints): it brings the Saqueo's horde sooner.</summary>
        public int? Noise { get; set; }
        /// <summary>Saqueo: who comes in each wave of the horde; absent on missions without one.</summary>
        public List<string>? Horde { get; set; } // TemplateIds
        /// <summary>A night mission (docs/ALTURA-Y-NOCHE.md): the living see less, light matters. Missing in older saves: day.</summary>
        public bool? Night { get; set; }
        /// <summary>A siege of the base (docs/FASE12.md); an elimination mission otherwise.</summary>
        public Siege? Siege { get; set; }
    }

    /// <summary>MissionState.evacBus: `{ tiles: Vec2[]; heading: number }`.</summary>
    public sealed class EvacBus
    {
        public List<Vec2> Tiles { get; set; } = new List<Vec2>();
        public int Heading { get; set; }
    }
}
