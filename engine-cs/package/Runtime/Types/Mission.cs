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

    /// <summary>
    /// A run's day sky (Arturo, 19/11, cuadro 83: «Varios, según la etapa»; MissionState.Sky): look only, no rule changes
    /// with it (.dev-snapshots/gameplay/g23/CIELO.md).
    /// </summary>
    public static class Skies
    {
        public const string Clear = "clear";
        public const string Overcast = "overcast";
        public const string Dusk = "dusk";

        public static readonly IReadOnlyList<string> All = new[] { Clear, Overcast, Dusk };
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
        /// <summary>
        /// A run's fight (Red, R3 17/11): what there is to search that nobody gets to on foot, by node (a car a town's outline
        /// walled in with its lot): never a spot to search (Looting.SearchSpot). Missing: nothing.
        /// </summary>
        public List<int>? OutOfReach { get; set; }
        /// <summary>
        /// G19 (Arturo, 18/11, cuadros 65–67): the pieces searched one by one as a box (SearchPieces; Looting.PieceAt) that hold
        /// something, by node of their tile (a piece over several, its anchor's); Setup's. One of those kinds not in it is
        /// empty from the start: drawn empty, never a spot to search. Its room isn't searched whole (LootRoom.Pieces).
        /// Missing (a fight saved by the 0.13.2): none of it, every room searched whole as before.
        /// </summary>
        public List<int>? Pieces { get; set; }
        /// <summary>
        /// G21 (Arturo, 18/11, cuadros 70, 72 y 74: «uno de cada tres»; «oxidado = no se registra»): the whole cars rifled
        /// before the squad came, by their nose's ground tile index (as GameState.Wrecks' Anchor): no boot to open
        /// (Looting.BootOf), drawn rusted as a wreck is. Setup's (PlaceLootedCars). Missing (a fight saved before): none.
        /// </summary>
        public List<int>? Rifled { get; set; }
        /// <summary>G23.1 La llave: this fight's (MissionOptions.KeyArmory), for its objective; missing, none.</summary>
        public bool? KeyArmory { get; set; }
        /// <summary>
        /// A run's fight's day sky (Skies; RunMap.SkyOf its node's): look only. Null: today's (outside the runs, at night, a
        /// fight saved before it).
        /// </summary>
        public string? Sky { get; set; }
        /// <summary>
        /// G23.2 Mordido (Arturo, 19/11, cuadros 73 y 87; .dev-snapshots/gameplay/g23/MORDIDO.md): a run's Rescate whose
        /// survivor (MissionState.ObjectiveUnit) is bitten: from the moment they are found, BiteTurns of the squad's turns till
        /// they turn; cured with the antiviral (Cured), they don't. Missing, none of it.
        /// </summary>
        public bool? Bitten { get; set; }
        /// <summary>
        /// G23.3 El herido (Arturo, cuadros 71, 73 y 92; .dev-snapshots/gameplay/g23/HERIDO.md): a run's Rescate whose survivor
        /// (MissionState.ObjectiveUnit, Unit.Lame) can't walk or act: carried (AbilityIds.Carry), set down, passed on; La Lata
        /// doesn't go while they live out of it; won with them aboard. Missing, none of it.
        /// </summary>
        public bool? Wounded { get; set; }
        /// <summary>
        /// G23.4 Separados (Arturo, cuadros 71 and 94; .dev-snapshots/gameplay/g23/SEPARADOS.md): a run's Saqueo whose squad
        /// starts in two groups on either side of La Lata (Setup.Split): the two, by unit id. Won with one of each
        /// aboard as La Lata goes (Rules.SplitSafe). Missing, none of it (one soldier, or no room on the map).
        /// </summary>
        public List<List<string>>? Split { get; set; }
        /// <summary>Mordido: the squad's turns left till the bitten turns, from their finding (Setup.BiteTurns); null before.</summary>
        public int? BiteTurns { get; set; }
        /// <summary>Mordido: the antiviral used on the bitten (GearUsedEvent): no count any more, and La Lata takes them.</summary>
        public bool? Cured { get; set; }
        /// <summary>Mordido: the antiviral (a gear of the fight's) while still in its piece, at node AntiviralAt; null once taken.</summary>
        public Gear? Antiviral { get; set; }
        /// <summary>Mordido: the medicine cabinet or shelf the antiviral is in (its node; MissionState.Pieces).</summary>
        public int? AntiviralAt { get; set; }
        /// <summary>
        /// G23.1: the doors locked, by node (the armoury's): nobody walks through one (Paths), and only one with the key
        /// in its pack opens it (OpenDoor; UnlockedEvent). Missing: none.
        /// </summary>
        public List<int>? Locked { get; set; }
        /// <summary>G23.1: the unit the key is on (one of the dead in uniform), its body's to search for it.</summary>
        public string? KeyHolder { get; set; }
        /// <summary>
        /// G23.1: the key while it is still on KeyHolder (alive, or its body not searched yet); null once it is taken
        /// (SearchedEvent), on the floor (its body risen: FloorGear) or used.
        /// </summary>
        public Gear? Key { get; set; }
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
        /// <summary>The runs' «horda despierta» (H3): every group alert from the start, and a wave by an edge in the first enemy turn.</summary>
        public bool? Awake { get; set; }
        /// <summary>A run's Asedio (H5): La Lata comes on this turn; till then it takes nobody, and the waves come every two turns.</summary>
        public int? HoldUntil { get; set; }
        /// <summary>A run's Travesía (H5): the edge the horde comes in by, the squad's start's (Sim.ChooseDropZone's sides).</summary>
        public int? HordeSide { get; set; }
        /// <summary>A run's Sigilo (H5): the item, unseen. Seen (concealment broken), a big wave comes at once by the edges.</summary>
        public bool? Stealth { get; set; }
        /// <summary>A run's Guarida (H5): the reward's box on this tile, its guard round it; it must be opened to win.</summary>
        public Pos? Lair { get; set; }
        /// <summary>A boss's fight (H6): its unit; it must be dead to win.</summary>
        public string? Boss { get; set; }
        /// <summary>
        /// A map that is one building (H7, the hospital only inside: Sites.Hospital): the outside out of play but the strip at
        /// its main entrance, and the squad finds it room by room, not whole (Buildings: GameState.Discovered keeps nodes, z ×
        /// area + tile).
        /// </summary>
        public bool? Inside { get; set; }
        /// <summary>
        /// A run's La Lata (N2, N4): how many it takes when it leaves (Runs.SeatsOf); who is in its zone past them is lost
        /// (Rules.NoSeat). Null: everyone in the zone, as before the runs.
        /// </summary>
        public int? Seats { get; set; }
        /// <summary>
        /// A run's Rescate (N2): where the survivor is to be looked for, a circle round somewhere near them (they are in it,
        /// not at its middle). Null elsewhere.
        /// </summary>
        public SearchArea? SearchArea { get; set; }
        /// <summary>
        /// A run's survivor in this fight (N2), the unit's id: a Rescate's (also MissionState.ObjectiveUnit), or one loose on
        /// any map (RunMap.LooseSurvivor). Captive and nobody's till talked to (AbilityIds.Talk). Null: none.
        /// </summary>
        public string? Survivor { get; set; }
        /// <summary>A run's Rescate with Cuervos (RunNode.Guarded): the survivor their prisoner.</summary>
        public bool? Guarded { get; set; }
        /// <summary>A run's Rescate (N2): someone of the squad has seen the survivor (SurvivorFoundEvent); till then they aren't shown.</summary>
        public bool? SurvivorFound { get; set; }
    }

    /// <summary>MissionState.searchArea (N2): a circle on the ground floor.</summary>
    public sealed class SearchArea
    {
        public Vec2 Center { get; set; } = new Vec2();
        public double Radius { get; set; }
    }

    /// <summary>MissionState.evacBus: `{ tiles: Vec2[]; heading: number }`.</summary>
    public sealed class EvacBus
    {
        public List<Vec2> Tiles { get; set; } = new List<Vec2>();
        public int Heading { get; set; }
    }
}
