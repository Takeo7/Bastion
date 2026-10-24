#nullable enable
// types.ts: the whole state of a mission, as the host keeps it and every client replays it.
using System.Collections.Generic;

namespace Bastion.Engine
{
    public sealed class GameState
    {
        /// <summary>Number of events applied so far. Clients use it to detect gaps.</summary>
        public int Seq { get; set; }
        /// <summary>Played in real time instead of by turns (docs/TIEMPO-REAL.md); absent, by turns.</summary>
        public Realtime? Realtime { get; set; }
        public string MapId { get; set; } = "";
        /// <summary>Display name of the area (generated maps get a name per biome).</summary>
        public string MapName { get; set; } = "";
        public string Biome { get; set; } = ""; // Biomes
        /// <summary>The scenario a generated map is, if not the biome's usual one (playtest 2026-10-14, #25).</summary>
        public string? Site { get; set; } // Sites
        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>Row-major: index = y * width + x.</summary>
        public List<string> Tiles { get; set; } = new List<string>(); // TileKinds
        /// <summary>Elevation of each tile in storeys (same indexing as `tiles`).</summary>
        public List<int> Elev { get; set; } = new List<int>();
        /// <summary>Floor of each tile (procedural maps; missing on hand-made maps and older saves).</summary>
        public List<int>? Ground { get; set; } // GroundKind: 0–4 (Map.cs)
        /// <summary>Clinical finishes stay attached to the building after furniture is destroyed.</summary>
        public List<MedicalSite>? Medical { get; set; }
        /// <summary>The houses, their rooms by use and how worn (playtest 2026-10-18, #15).</summary>
        public List<HomeSite>? Homes { get; set; }
        /// <summary>The supermarkets (playtest 2026-10-20, #18): one on a town's map at most.</summary>
        public List<MarketSite>? Markets { get; set; }
        /// <summary>
        /// What the cover of each tile is meant to look like (procedural maps; see
        /// decor.ts). Cosmetic and never updated: a tile whose kind has changed since
        /// (blown up) is drawn from its kind.
        /// </summary>
        public List<int>? Decor { get; set; } // Decor: kind * 4 + facing (Map.cs)
        /// <summary>
        /// Storeys of the building each tile belongs to (its walls and its inside),
        /// 0 outside buildings (procedural maps; missing on hand-made maps and older
        /// saves, which see as if every building were one storey). In a building of
        /// two or more, walls and windows stop sight up to its roof (los.ts).
        /// </summary>
        public List<int>? Storeys { get; set; }
        /// <summary>
        /// The storeys above the ground, inside buildings (docs/FASE6-PLAN.md):
        /// `levels[k - 1]` is storey k. Missing on maps without them (older saves,
        /// hand-made maps but «la torre»), which see and move as they always did.
        /// </summary>
        public List<Level>? Levels { get; set; }
        /// <summary>Lit tiles at night: the map's street lights, Bengalas and fires (missing in older saves).</summary>
        public List<Light>? Lights { get; set; }
        /// <summary>What closes the map round what is played (docs/MAPAS.md §5.10); missing on older saves and maps without an outline.</summary>
        public List<FramePiece>? Frame { get; set; }
        public List<Unit> Units { get; set; } = new List<Unit>();
        public List<Pod> Pods { get; set; } = new List<Pod>();
        public MissionState Mission { get; set; } = new MissionState();
        public int Turn { get; set; }
        public string ActiveTeam { get; set; } = ""; // Teams
        public bool Concealed { get; set; }
        /// <summary>
        /// Misses in a row by each player's soldiers (bad luck, XCOM 2's aim assist):
        /// the next shot gets +10 per miss, up to +20. Missing in older saves.
        /// </summary>
        public int[]? MissStreak { get; set; } // [number, number]: by Slot.
        /// <summary>
        /// The last attack or explosive and whose it was (Fase 9, skills.ts): who
        /// gets the kill or the blast that `died` and `damaged` count for. `landed`:
        /// the explosive already hit someone.
        /// </summary>
        public Blow? Blow { get; set; }
        public bool[] Ready { get; set; } = new bool[2]; // [boolean, boolean]: by Slot.
        /// <summary>
        /// Who has the command (el mando) in the squad's turn: only that player gives
        /// orders, and passes it to the partner when they choose. Missing in missions
        /// saved before it existed: the first player to act takes it.
        /// </summary>
        public int? Command { get; set; } // Slot
        /// <summary>The soldier in play (playtest 2026-10-15, #9): the holder finishes it before another; once it's done, the partner plays one of theirs.</summary>
        public string? ActiveUnit { get; set; }
        /// <summary>A player asked to evacuate from the bus and waits for the partner's yes (docs/PRUEBA-2026-10-14.md P1).</summary>
        public EvacCall? EvacCall { get; set; }
        /// <summary>A player asked to abandon the mission and waits for the partner's yes (playtest 16/10).</summary>
        public AbandonCall? AbandonCall { get; set; }
        public string? Outcome { get; set; } // Outcomes
        public string? OutcomeReason { get; set; } // EndReasons
        // Set when the mission belongs to a campaign (the client offers "back to base"). `story` is its
        // chapter in lore.ts STORY; `kicker` titles the arrival briefing and `item` names what a recovery
        // carries out (story chapters and side quests).
        /// <summary>`briefing`: the operation's briefing, for the mission's intro card (playtest 13/10: what the mission is for).</summary>
        public CampaignMission? CampaignMission { get; set; }
        public List<SmokeCloud> Smoke { get; set; } = new List<SmokeCloud>();
        /// <summary>La Lata's vehicle (Arturo, 18/10 #45): 0 todoterreno, 1 furgoneta, 2 camión, 3 autobús; absent, the bus. What comes to take the squad out.</summary>
        public int? Vehicle { get; set; }
        /// <summary>Under snow (playtest 16/10): still winter in the campaign. Missing, only the opening chapter is.</summary>
        public bool? Snow { get; set; }
        /// <summary>Tiles already searched, by node (grid.ts nodeOf; on the ground, y * width + x); missing before searching existed.</summary>
        public List<int>? Searched { get; set; }
        /// <summary>Rooms the squad has searched, by id (loot.ts roomAt; docs/FASE8.md §B).</summary>
        public List<int>? RoomsSearched { get; set; }
        /// <summary>
        /// Things left on the floor (docs/FASE8.md §C): dropped, found with a full backpack, or what a fallen soldier
        /// carried (Arturo, 18/10 #49: «la mochila de ‹nombre›», Lore), `from` that soldier (its unit id).
        /// </summary>
        public List<DroppedLoot>? Dropped { get; set; }
        /// <summary>How many things have been left on the floor so far: the next one's id.</summary>
        public int? DroppedCount { get; set; }
        /// <summary>
        /// In a siege (docs/FASE12.md §4), what each door and barricade still holds,
        /// by node (grid.ts nodeOf); 0 once it gave way, to blows or a blast: the
        /// breaches stay known through a reload.
        /// </summary>
        public Dictionary<int, int>? Breakables { get; set; }
        /// <summary>The campaign's base under siege (docs/FASE12.md §11): its pieces as they stood when it began, and its stumps (base.ts).</summary>
        public SiegeBase? Base { get; set; }
        /// <summary>The base's traps set (docs/FASE12.md §11.4), with their uses left; spent, 0.</summary>
        public List<Trap>? Traps { get; set; }
        /// <summary>Tiles with barbed wire, by node: costlier to cross.</summary>
        public List<int>? Slow { get; set; }
    }

    /// <summary>GameState.blow: `{ unit: string; kind: 'gun' | 'blade' | 'blast'; landed?: boolean }`.</summary>
    public sealed class Blow
    {
        public string Unit { get; set; } = "";
        public string Kind { get; set; } = ""; // BlowKinds
        public bool? Landed { get; set; }
    }

    /// <summary>Blow.kind: `'gun' | 'blade' | 'blast'`.</summary>
    public static class BlowKinds
    {
        public const string Gun = "gun";
        public const string Blade = "blade";
        public const string Blast = "blast";

        public static readonly IReadOnlyList<string> All = new[] { Gun, Blade, Blast };
    }

    /// <summary>GameState.evacCall: `{ by: Slot; unit: string }`.</summary>
    public sealed class EvacCall
    {
        public int By { get; set; } // Slot
        public string Unit { get; set; } = "";
    }

    /// <summary>GameState.abandonCall: `{ by: Slot }`.</summary>
    public sealed class AbandonCall
    {
        public int By { get; set; } // Slot
    }

    /// <summary>GameState.campaignMission: `{ id; name; story?; kicker?; item?; briefing? }`.</summary>
    public sealed class CampaignMission
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int? Story { get; set; }
        public string? Kicker { get; set; }
        public string? Item { get; set; }
        public string? Briefing { get; set; }
    }

    /// <summary>An element of GameState.dropped: `{ id: string; pos: Pos; loot: Loot; from?: string }`.</summary>
    public sealed class DroppedLoot
    {
        public string Id { get; set; } = "";
        public Pos Pos { get; set; } = new Pos();
        public Loot Loot { get; set; } = new Loot();
        public string? From { get; set; }
    }

    /// <summary>GameState.base: `{ pieces: BasePiece[]; felled?: number[]; level?: number }`.</summary>
    public sealed class SiegeBase
    {
        public List<BasePiece> Pieces { get; set; } = new List<BasePiece>();
        public List<int>? Felled { get; set; }
        public int? Level { get; set; }
    }
}
