#nullable enable
// types.ts: the buildings the client finishes and dresses (medical centres, houses, supermarkets).
// Three doc comments in types.ts sit away from what they describe (other declarations came in between); here each is
// on its own type: MedicalSite's, HomeRoom's and HomeSite's.
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static class MedicalUses
    {
        public const string Reception = "reception";
        public const string Consultation = "consultation";
        public const string Treatment = "treatment";
        public const string Ward = "ward";
        public const string Equipment = "equipment";
        public const string Nurses = "nurses";
        public const string Waiting = "waiting";
        public const string Supplies = "supplies";
        public const string Hygiene = "hygiene";
        public const string Stairs = "stairs";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Reception, Consultation, Treatment, Ward, Equipment, Nurses, Waiting, Supplies, Hygiene, Stairs,
        };
    }

    public sealed class MedicalRoom
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Z { get; set; }
        public string Use { get; set; } = ""; // MedicalUses
    }

    public sealed class MedicalDetail
    {
        public string Kind { get; set; } = ""; // MedicalDetailKinds
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Facing { get; set; } // A DIRS4 index (medicalDressing.ts: rng.int(4)).
        public double Height { get; set; }
        public int? Variant { get; set; }
    }

    /// <summary>MedicalDetail.kind.</summary>
    public static class MedicalDetailKinds
    {
        public const string CorpseCivilian = "corpseCivilian";
        public const string CorpseStaff = "corpseStaff";
        public const string CoveredCorpse = "coveredCorpse";
        public const string CoveredBed = "coveredBed";
        public const string BareBed = "bareBed";
        public const string PrivacyScreen = "privacyScreen";
        public const string MedicalSupplies = "medicalSupplies";
        public const string Luggage = "luggage";
        public const string Crutches = "crutches";
        public const string Books = "books";
        public const string WallCross = "wallCross";
        public const string CautionSign = "cautionSign";
        public const string SafeSign = "safeSign";
        public const string BloodPool = "bloodPool";
        public const string BloodDrops = "bloodDrops";
        public const string WallBlood = "wallBlood";
        public const string FloorGrime = "floorGrime";
        public const string ScatteredPapers = "scatteredPapers";
        public const string MedicalWaste = "medicalWaste";
        public const string OverturnedChair = "overturnedChair";
        public const string FallenIV = "fallenIV";
        public const string SpilledSupplies = "spilledSupplies";
        public const string RumpledLinen = "rumpledLinen";

        public static readonly IReadOnlyList<string> All = new[]
        {
            CorpseCivilian, CorpseStaff, CoveredCorpse, CoveredBed, BareBed, PrivacyScreen, MedicalSupplies, Luggage,
            Crutches, Books, WallCross, CautionSign, SafeSign, BloodPool, BloodDrops, WallBlood, FloorGrime,
            ScatteredPapers, MedicalWaste, OverturnedChair, FallenIV, SpilledSupplies, RumpledLinen,
        };
    }

    /// <summary>What a house's room is for; a lavatory and a pantry two by three at least (docs/MAPAS.md §4.3, decided by Arturo 19/10).</summary>
    public static class HomeUses
    {
        public const string Living = "living";
        public const string Kitchen = "kitchen";
        public const string Bedroom = "bedroom";
        public const string Bath = "bath";
        public const string Study = "study";
        public const string Hall = "hall";
        public const string Toilet = "toilet";
        public const string Pantry = "pantry";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Living, Kitchen, Bedroom, Bath, Study, Hall, Toilet, Pantry,
        };
    }

    /// <summary>What a room of a house is (playtest 2026-10-18, #15).</summary>
    public sealed class HomeRoom
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Z { get; set; }
        public string Use { get; set; } = ""; // HomeUses
    }

    /// <summary>A supermarket (playtest 20/10, #18): its bounds, walls included, and the side its glass front and its sign look onto (DIRS4).</summary>
    public sealed class MarketSite
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Front { get; set; }
        /// <summary>
        /// How much is left on its shelves (playtest 21/10, #19): 'full', the shelves still stocked, if a little picked
        /// over, and searching it always turns up food; 'picked', stripped bare, maybe something, maybe nothing (loot.ts
        /// rollRoom). Missing on maps made before: 'picked'.
        /// </summary>
        public string? Stock { get; set; } // MarketStocks
    }

    /// <summary>MarketSite.stock: `'full' | 'picked'`.</summary>
    public static class MarketStocks
    {
        public const string Full = "full";
        public const string Picked = "picked";

        public static readonly IReadOnlyList<string> All = new[] { Full, Picked };
    }

    /// <summary>
    /// A house and its rooms by use, as the client finishes and dresses them: how
    /// worn it is (0 kept, about 0.5 lived in rough, near 1 wrecked) and, seeded,
    /// what lies about in it (the medical centres' details: bodies, blood, mess).
    /// </summary>
    public sealed class HomeSite
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public double Wear { get; set; }
        public List<HomeRoom> Rooms { get; set; } = new List<HomeRoom>();
        /// <summary>Seeded scenery, stored with the mission. Never affects movement, cover or AI.</summary>
        public List<MedicalDetail>? Dressing { get; set; }
    }

    /// <summary>A medical building's bounds, including walls; wear is cosmetic, from 0 to 1.</summary>
    public sealed class MedicalSite
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public double Wear { get; set; }
        public string? Kind { get; set; } // MedicalSiteKinds
        public List<MedicalRoom>? Rooms { get; set; }
        /// <summary>Seeded scenery, stored with the mission. Never affects movement, cover or AI.</summary>
        public List<MedicalDetail>? Dressing { get; set; }
    }

    /// <summary>MedicalSite.kind: `'clinic' | 'ward' | 'hospital'`.</summary>
    public static class MedicalSiteKinds
    {
        public const string Clinic = "clinic";
        public const string Ward = "ward";
        public const string Hospital = "hospital";

        public static readonly IReadOnlyList<string> All = new[] { Clinic, Ward, Hospital };
    }
}
