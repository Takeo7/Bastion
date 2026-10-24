#nullable enable
// mapgen.ts: what a mission needs from a map (MissionLayout) and what the generator is asked for (GenOptions).
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>Everything a mission needs from a map: terrain plus where things go.</summary>
    public sealed class MissionLayout
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Biome { get; set; } = ""; // Biomes
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Tiles { get; set; } = new List<string>(); // TileKinds
        public List<int> Elev { get; set; } = new List<int>();
        /// <summary>What the floor of each tile is (procedural maps only).</summary>
        public List<int>? Ground { get; set; } // GroundKind: 0–4 (Map.cs)
        public List<MedicalSite>? Medical { get; set; }
        /// <summary>The houses' rooms by use (playtest 2026-10-18, #15).</summary>
        public List<HomeSite>? Homes { get; set; }
        /// <summary>The supermarkets (playtest 2026-10-20, #18).</summary>
        public List<MarketSite>? Markets { get; set; }
        /// <summary>What each piece of cover is meant to be (procedural maps only; cosmetic).</summary>
        public List<int>? Decor { get; set; }
        /// <summary>Storeys of the building each tile belongs to, 0 outside (procedural maps only; GameState.storeys).</summary>
        public List<int>? Storeys { get; set; }
        /// <summary>The storeys above the ground (GameState.levels; docs/FASE6-PLAN.md).</summary>
        public List<Level>? Levels { get; set; }
        /// <summary>A few places together on an upper floor, by a window and far from the squad, for a group of the living posted up there (decision 7).</summary>
        public List<Pos>? Perch { get; set; }
        /// <summary>Where the map's street lights and floodlights stand (procedural maps only; GameState.lights); `radius`, if not a street light's.</summary>
        public List<LayoutLight>? Lights { get; set; }
        public List<Vec2> Spawns { get; set; } = new List<Vec2>();
        /// <summary>Spawn tiles and patrol route of each pod, in the order the pod plan lists them (on the ground unless they say `z`).</summary>
        public List<PodSpot> Pods { get; set; } = new List<PodSpot>();
        /// <summary>Recovery data; also the default spot for other objectives on hand-made maps.</summary>
        public Vec2? Item { get; set; }
        public Vec2? Terminal { get; set; }
        /// <summary>Where the relay (sabotage) or the VIP (rescue) stands.</summary>
        public Vec2? ObjectiveSpot { get; set; }
        public List<Vec2> EvacZone { get; set; } = new List<Vec2>();
        /// <summary>The bus parked beside the evac zone: its tiles (`high`, decor 'bus') and the way it drives off (DIRS4 index).</summary>
        public EvacBus? EvacBus { get; set; }
        /// <summary>What closes a town round what is played (docs/MAPAS.md §5.10).</summary>
        public List<FramePiece>? Frame { get; set; }
        /// <summary>A town's block courtyards (docs/MAPAS.md §5.4: rows round them on two sides or more, two ways in at least).</summary>
        public List<PlanRect>? Courtyards { get; set; }
        /// <summary>Steps on foot from the squad to the objective and on to the evacuation (generated maps; setup.ts missionTurns).</summary>
        public int? Route { get; set; }
        /// <summary>The scenario, when not the biome's usual one (GenOptions.site).</summary>
        public string? Site { get; set; } // Sites
        /// <summary>A base to defend (docs/FASE12.md), and its barricades (low cover the dead break down).</summary>
        public bool? Siege { get; set; }
        public List<Vec2>? Barricades { get; set; }
        /// <summary>What each piece of the base holds, by tile (the new base, base.ts baseLayout); missing, a hand-made base's doors and barricades.</summary>
        public Dictionary<int, int>? Breakables { get; set; }
    }

    /// <summary>An element of MissionLayout.lights: `Vec2 & { radius?: number }`.</summary>
    public sealed class LayoutLight : Vec2
    {
        public double? Radius { get; set; }
    }

    /// <summary>An element of MissionLayout.pods: `{ tiles: (Vec2 & { z?: number })[]; patrol: (Vec2 & { z?: number })[] }`.</summary>
    public sealed class PodSpot
    {
        public List<Pos> Tiles { get; set; } = new List<Pos>();
        public List<Pos> Patrol { get; set; } = new List<Pos>();
    }

    public sealed class GenOptions
    {
        public string Biome { get; set; } = ""; // Biomes
        public string Kind { get; set; } = ""; // MissionKinds
        /// <summary>Size of each pod, in the order the pod plan lists them.</summary>
        public List<int> PodSizes { get; set; } = new List<int>();
        public int Seed { get; set; }
        /// <summary>A recovery whose story puts the item out in the open (playtest 2026-10-14, #27): on a square or a car park, not in a room.</summary>
        public bool? ItemOutdoors { get; set; }
        /// <summary>The opening's medicines belong inside Bielsa's furnished clinic.</summary>
        public bool? Clinic { get; set; }
        /// <summary>A hospital objective, including the rescue of Dr Nwosu.</summary>
        public bool? Hospital { get; set; }
        /// <summary>Explicit medical locations; ordinary houses keep their existing identity.</summary>
        public string? Medical { get; set; } // MedicalSiteKinds
        /// <summary>A scenario other than the biome's usual one (playtest 2026-10-14, #25): 'roadside' in the wilds, 'industrial' in a city.</summary>
        public string? Site { get; set; } // Sites
        /// <summary>A sabotage's alarm (playtest 2026-10-15, #14): a patrol car or an ambulance takes a second tile, its nose.</summary>
        public string? Alarm { get; set; } // Alarms
    }
}
