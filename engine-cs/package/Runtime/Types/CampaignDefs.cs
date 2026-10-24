#nullable enable
// campaign.ts: the definitions of its tables (FacilityDef, RegionDef, EventDef) and what some of its functions give or
// take (MissionInfo, NextStep, CampaignContext). The state is in Campaign.cs; the tables in Campaign/.
using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    public sealed class FacilityDef
    {
        public string Id { get; set; } = ""; // FacilityIds
        public string Name { get; set; } = "";
        /// <summary>What it does at level 1.</summary>
        public string Description { get; set; } = "";
        /// <summary>What levels 2 and 3 bring.</summary>
        public IReadOnlyList<string> Upgrades { get; set; } = new string[0]; // [string, string]
        /// <summary>Price in credits.</summary>
        public int Cost { get; set; }
        public int Days { get; set; }
        /// <summary>Bastion level that unlocks it.</summary>
        public int Level { get; set; }
        /// <summary>Only one can be built.</summary>
        public bool Unique { get; set; }
    }

    public sealed class RegionDef
    {
        public string Id { get; set; } = ""; // RegionIds
        public string Name { get; set; } = "";
        /// <summary>Where it lies on the valley's map (docs/FASE5.md): x west to east, z north (the sierra) to south (the plain), in map units.</summary>
        public double X { get; set; }
        public double Z { get; set; }
        /// <summary>The zones a road joins it to (ROADS).</summary>
        public IReadOnlyList<string> Roads { get; set; } = new string[0]; // RegionIds
        /// <summary>Credits it adds to every monthly payment once contacted.</summary>
        public int Income { get; set; }
    }

    /// <summary>One of an event's two answers: what it says, whether it can be taken now, and what it does.</summary>
    public sealed class EventOption
    {
        public string Label { get; set; } = "";
        public string Effect { get; set; } = "";
        /// <summary>Missing: always.</summary>
        public Func<CampaignState, bool>? Available { get; set; }
        public Action<CampaignState, Rng> Apply { get; set; } = (c, rng) => { };
    }

    public sealed class EventDef
    {
        public string Id { get; set; } = ""; // CampaignEventIds
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        /// <summary>Story events only come when the story calls them, never at random.</summary>
        public bool? Story { get; set; } // true
        /// <summary>Heard on the radio: only with a Sala de radio (Ana, 23/10 #27).</summary>
        public bool? Radio { get; set; } // true
        public IReadOnlyList<EventOption> Options { get; set; } = new EventOption[0]; // [EventOption, EventOption]
    }

    /// <summary>What the battlefield of an operation names: its kicker, the object to recover, the VIP and an enemy leader.</summary>
    public sealed class MissionInfo
    {
        public string? Kicker { get; set; }
        /// <summary>A sabotage's alarm (#14).</summary>
        public string? Alarm { get; set; } // Alarms
        public string? Item { get; set; }
        /// <summary>Its story puts the item out in the open (playtest 2026-10-14, #27).</summary>
        public bool? ItemOutdoors { get; set; }
        public string? VipName { get; set; }
        public MissionLeader? Leader { get; set; }
        /// <summary>Still winter (playtest 16/10): the mission under snow.</summary>
        public bool? Snow { get; set; }
    }

    /// <summary>NextStep.tone: `'urgent' | 'info'`.</summary>
    public static class NextStepTones
    {
        public const string Urgent = "urgent";
        public const string Info = "info";

        public static readonly IReadOnlyList<string> All = new[] { Urgent, Info };
    }

    /// <summary>Something a player has pending in the base, for the "Ahora" block and the nav counters.</summary>
    public sealed class NextStep
    {
        public string View { get; set; } = ""; // BaseViews
        public string Label { get; set; } = "";
        public int? Count { get; set; }
        public string Tone { get; set; } = NextStepTones.Info; // NextStepTones
        /// <summary>`piece`: a piece of the base (docs/FASE12.md §11).</summary>
        public NextStepGo? Go { get; set; }
    }

    /// <summary>NextStep.go: `{ offer?; soldier?; piece?; region?; rumor? }`, what the step opens.</summary>
    public sealed class NextStepGo
    {
        public string? Offer { get; set; }
        public string? Soldier { get; set; }
        public string? Piece { get; set; }
        public string? Region { get; set; } // RegionIds
        public string? Rumor { get; set; }
    }

    // What some functions give, as TypeScript's inline objects.

    /// <summary>upgradeCost's `{ cost: number; days: number; level: number }`: price, days and Bastion level of a facility's next level.</summary>
    public sealed class FacilityUpgrade
    {
        public int Cost { get; set; }
        public int Days { get; set; }
        public int Level { get; set; }
    }

    /// <summary>garageCut's `{ wounds: number }`: the days (missions) the Chapistería takes off each wound.</summary>
    public sealed class GarageBonus
    {
        public int Wounds { get; set; }
    }

    /// <summary>
    /// raiseCost's `{ cost: number; level: number } | string`: what raising a building a level costs and the Bastion's level
    /// it needs, or, in `Error`, why it doesn't rise (then `Cost` and `Level` are 0).
    /// </summary>
    public sealed class PieceRaise
    {
        public int Cost { get; set; }
        public int Level { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>siegeHeard's `{ in: number; count?: number; sides?: number[] }`: when, always; how many and by where, with the Sala de radio.</summary>
    public sealed class SiegeNews
    {
        public int In { get; set; }
        public int? Count { get; set; }
        public List<int>? Sides { get; set; }
    }

    /// <summary>squadPlace's `{ error: string | null; evict?: string }`: why not, or null; `evict`, who leaves to make room.</summary>
    public sealed class SquadPlaceResult
    {
        public string? Error { get; set; }
        public string? Evict { get; set; }
    }

    /// <summary>nextLevel's `{ level: number; cost: number; missions: number; flown: number }`.</summary>
    public sealed class LevelStep
    {
        public int Level { get; set; }
        public int Cost { get; set; }
        public int Missions { get; set; }
        public int Flown { get; set; }
    }

    /// <summary>Who gives a command (executeCampaignCommand): their seat, the seats connected, and the room's Rng.</summary>
    public sealed class CampaignContext
    {
        public int Slot { get; set; } // Slot
        public IReadOnlyList<int> Connected { get; set; } = new int[0]; // Slot
        public Rng Rng { get; set; } = new Rng(0);
    }
}
