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

    /// <summary>
    /// GameState.weather (Arturo, 1/11: Arte's five, in different missions): rain out of winter, snow in it; missing, clear.
    /// A storm and a blizzard cut sight, a downpour and a storm the reach of noise (Content.Weather).
    /// </summary>
    public static class Weathers
    {
        public const string Drizzle = "drizzle";
        public const string Downpour = "downpour";
        public const string Storm = "storm";
        public const string Snowfall = "snowfall";
        public const string Blizzard = "blizzard";

        public static readonly IReadOnlyList<string> All = new[] { Drizzle, Downpour, Storm, Snowfall, Blizzard };
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

    /// <summary>
    /// A car left wrecked in the street (Arturo, 1/11; Mapgen.LayWrecks): the car itself is the CarFront of GameState.decor
    /// at `anchor`, a car like any other to the rules; this says how it came there, for the client to draw it crashed.
    /// </summary>
    public sealed class Wreck
    {
        /// <summary>The car's anchor, its nose, by tile index (y * width + x, on the ground).</summary>
        public int Anchor { get; set; }
        public string Kind { get; set; } = ""; // WreckKinds
        /// <summary>Degrees it lies askew of its facing, either way (−35 to 35), the map's mirroring already counted.</summary>
        public int Tilt { get; set; }
    }

    public static class WreckKinds
    {
        /// <summary>One of two or three run into each other by a junction.</summary>
        public const string Pileup = "pileup";
        /// <summary>Left in the middle of the road.</summary>
        public const string Abandoned = "abandoned";
        /// <summary>Driven into a building's front: its nose where the wall was (that tile is the car's now, low cover).</summary>
        public const string Wall = "wall";
    }

    /// <summary>
    /// Something burning, all the mission long (Arturo, 1/11; Mapgen.LayFires): it lights round it at night (its light in
    /// GameState.lights has the same id) and whoever steps on its ground burns (Rules.Burn).
    /// </summary>
    public sealed class Fire
    {
        public string Id { get; set; } = "";
        /// <summary>The tile of what burns (a car's nose, a bin, drums, or the pile itself), by tile index.</summary>
        public int Source { get; set; }
        /// <summary>The ground on fire, tiles one can step on, by tile index (on the ground).</summary>
        public List<int> Tiles { get; set; } = new List<int>();
        /// <summary>The squad's turns it still burns, a molotov's (Arturo, 1/11); absent, the map's own, for ever.</summary>
        public int? Turns { get; set; }
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
        /// <summary>The campaign counts its pistols (CampaignState.Pistols; Arturo, 1/11): searches turn them up too.</summary>
        public bool? Pistols { get; set; }
        /// <summary>
        /// The police cars (Arturo, 2/11: any mission may have some), two ground tile indices each, its nose and its boot;
        /// the boot holds a gun more often (Looting.PoliceBoot). In the 0.11.5 and the 0.11.6, the first mission's one.
        /// </summary>
        public List<int>? PoliceCar { get; set; }
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
