#nullable enable
// The runs' maps that are one building (H9, .dev-snapshots/gameplay/runs/H9-INTERIORES.md; new in C#): the contract with
// Arte, on the model of the medical buildings (MedicalSite, MedicalUses, MedicalDetailKinds). The engine lays the plan, the
// rooms with their use, the furniture that blocks or covers (tiles and decor), and the marks of scenery; Arte puts a model on
// each mark. The hospital stays medical (MedicalSite).
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>InteriorSite.kind: the map it is (Sites).</summary>
    public static class InteriorKinds
    {
        public const string Offices = "offices";
        public const string Shed = "shed";
        public const string Police = "police";
        public const string Plaza = "plaza";
        public const string Camping = "camping";

        public static readonly IReadOnlyList<string> All = new[] { Offices, Shed, Police, Plaza, Camping };
    }

    /// <summary>An office block's rooms (H9 §1). A corridor upstairs has none; the stairs' rect lies in the room they stand in.</summary>
    public static class OfficeUses
    {
        public const string Reception = "reception";
        public const string OpenPlan = "openPlan";
        public const string Office = "office";
        public const string Meeting = "meeting";
        public const string Kitchen = "kitchen";
        public const string Hygiene = "hygiene";
        public const string Storage = "storage";
        public const string Stairs = "stairs";

        public static readonly IReadOnlyList<string> All = new[] { Reception, OpenPlan, Office, Meeting, Kitchen, Hygiene, Storage, Stairs };
    }

    /// <summary>An office block's marks (H9 §1).</summary>
    public static class OfficeDetailKinds
    {
        public const string Desk = "desk";
        public const string DeskChair = "deskChair";
        public const string Computer = "computer";
        public const string FilingCabinet = "filingCabinet";
        public const string Whiteboard = "whiteboard";
        public const string MeetingTable = "meetingTable";
        public const string WaterCooler = "waterCooler";
        public const string Copier = "copier";
        public const string Plant = "plant";
        public const string ScatteredPapers = "scatteredPapers";
        public const string CorpseWorker = "corpseWorker";
        public const string BloodPool = "bloodPool";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Desk, DeskChair, Computer, FilingCabinet, Whiteboard, MeetingTable, WaterCooler, Copier, Plant, ScatteredPapers, CorpseWorker, BloodPool,
        };
    }

    /// <summary>A factory shed's rooms (H9 §2): the floor (the whole shed, one room found at once), the foreman's office, the loading bay along the docks (inside the floor), the lockers.</summary>
    public static class ShedUses
    {
        public const string Floor = "floor";
        public const string Office = "office";
        public const string LoadingBay = "loadingBay";
        public const string Lockers = "lockers";

        public static readonly IReadOnlyList<string> All = new[] { Floor, Office, LoadingBay, Lockers };
    }

    /// <summary>
    /// A factory shed's marks (H9 §2): shelving stands tall and blocks (a `high` tile under it); crates and drums along its
    /// walls and in its loading bay, a container (2 × 4) where it fits (Mapgen.ShedOnly).
    /// </summary>
    public static class ShedDetailKinds
    {
        public const string Shelving = "shelving";
        public const string Pallet = "pallet";
        public const string Crate = "crate";
        public const string Forklift = "forklift";
        public const string Workbench = "workbench";
        public const string Drum = "drum";
        public const string Container = "container";
        public const string LockerRow = "lockerRow";

        /// <summary>(No chainHoist: Arte, 17/11, there is no hoist in the packs.)</summary>
        public static readonly IReadOnlyList<string> All = new[] { Shelving, Pallet, Crate, Forklift, Workbench, Drum, Container, LockerRow };
    }

    /// <summary>
    /// A police station's rooms (H9 §3). `cells`: each cell (2 × 3, its front of bars onto the corridor, solid walls
    /// between: Arturo, 17/11); `hygiene` and `kitchen` furnished as a house's, with its marks (Arte, 17/11).
    /// </summary>
    public static class PoliceUses
    {
        public const string Reception = "reception";
        public const string Cells = "cells";
        public const string Office = "office";
        public const string Armory = "armory";
        public const string Evidence = "evidence";
        public const string Interrogation = "interrogation";
        public const string Lockers = "lockers";
        public const string Hygiene = "hygiene";
        public const string Kitchen = "kitchen";
        public const string Stairs = "stairs";

        public static readonly IReadOnlyList<string> All = new[] { Reception, Cells, Office, Armory, Evidence, Interrogation, Lockers, Hygiene, Kitchen, Stairs };
    }

    /// <summary>
    /// A police station's marks (H9 §3, with Arte's of 17/11: corkboard and waterCooler). `counter` is a wall with its
    /// window between the lobby and the office (Arte): its mark on the window's first tile, facing the lobby.
    /// </summary>
    public static class PoliceDetailKinds
    {
        public const string CellBed = "cellBed";
        public const string CellToilet = "cellToilet";
        public const string Counter = "counter";
        public const string Bench = "bench";
        public const string Desk = "desk";
        public const string DeskChair = "deskChair";
        public const string Computer = "computer";
        public const string FilingCabinet = "filingCabinet";
        public const string Whiteboard = "whiteboard";
        public const string WeaponLocker = "weaponLocker";
        public const string WeaponRack = "weaponRack";
        public const string GunCase = "gunCase";
        public const string AmmoCase = "ammoCase";
        public const string EvidenceShelf = "evidenceShelf";
        public const string EvidenceBag = "evidenceBag";
        public const string InterrogationTable = "interrogationTable";
        /// <summary>On the floor at each side of the interrogation table (one tile), along its facing, facing it (Arte, 17/11).</summary>
        public const string InterrogationChair = "interrogationChair";
        public const string Polygraph = "polygraph";
        public const string LockerRow = "lockerRow";
        public const string VendingMachine = "vendingMachine";
        public const string CorpseOfficer = "corpseOfficer";
        public const string BloodPool = "bloodPool";
        public const string Corkboard = "corkboard";
        public const string WaterCooler = "waterCooler";

        public static readonly IReadOnlyList<string> All = new[]
        {
            CellBed, CellToilet, Counter, Bench, Desk, DeskChair, Computer, FilingCabinet, Whiteboard, WeaponLocker, WeaponRack, GunCase, AmmoCase,
            EvidenceShelf, EvidenceBag, InterrogationTable, Polygraph, LockerRow, VendingMachine, CorpseOfficer, BloodPool, Corkboard, WaterCooler,
            InterrogationChair,
        };
    }

    /// <summary>
    /// A shopping plaza's shops (H9 §4), one a room each. The cinema, its lobby only (Arte, 17/11: no seats in any pack);
    /// `empty`, empty or looted.
    /// </summary>
    public static class ShopUses
    {
        public const string Jewellery = "jewellery";
        public const string Clothes = "clothes";
        public const string Electronics = "electronics";
        public const string Pharmacy = "pharmacy";
        public const string Sports = "sports";
        public const string Cinema = "cinema";
        public const string Cafe = "cafe";
        public const string Toys = "toys";
        public const string Empty = "empty";

        public static readonly IReadOnlyList<string> All = new[] { Jewellery, Clothes, Electronics, Pharmacy, Sports, Cinema, Cafe, Toys, Empty };
    }

    /// <summary>
    /// A shopping plaza's marks (H9 §4, with Arte's of 17/11: billboard; the fountain is the street's own decor, so no mark
    /// of its own). Out of doors too: an awning and a sign over each shop's front, the planters, broken glass.
    /// </summary>
    public static class PlazaDetailKinds
    {
        public const string DisplayCase = "displayCase";
        public const string RingDisplay = "ringDisplay";
        public const string WatchDisplay = "watchDisplay";
        public const string ClothesRack = "clothesRack";
        public const string Mannequin = "mannequin";
        public const string Shelf = "shelf";
        public const string Counter = "counter";
        public const string Till = "till";
        public const string Popcorn = "popcorn";
        public const string Projector = "projector";
        public const string MoviePoster = "moviePoster";
        public const string Sofa = "sofa";
        public const string Awning = "awning";
        public const string ShopSign = "shopSign";
        public const string Planter = "planter";
        public const string BrokenGlass = "brokenGlass";
        public const string Billboard = "billboard";
        /// <summary>The toy shop's and the cinema lobby's own pieces (Producto, 17/11), each on its tile of cover: a shelf of toys against a wall (high), a bin or stand of toys (low), a bench (low), the ticket booth against a wall (high).</summary>
        public const string ToyShelf = "toyShelf";
        public const string ToyBin = "toyBin";
        public const string LobbyBench = "lobbyBench";
        public const string TicketBooth = "ticketBooth";

        public static readonly IReadOnlyList<string> All = new[]
        {
            DisplayCase, RingDisplay, WatchDisplay, ClothesRack, Mannequin, Shelf, Counter, Till, Popcorn, Projector, MoviePoster, Sofa, Awning, ShopSign,
            Planter, BrokenGlass, Billboard, ToyShelf, ToyBin, LobbyBench, TicketBooth,
        };
    }

    /// <summary>
    /// A camping's parts (H9 §5): each pitch, each caravan's room (a building: its door on its side), the military or
    /// quarantine area, the portable toilets, the fire pit.
    /// </summary>
    public static class CampUses
    {
        public const string Pitch = "pitch";
        public const string Trailer = "trailer";
        public const string Military = "military";
        public const string Toilets = "toilets";
        public const string FirePit = "firePit";
        /// <summary>G23.5 Saqueadores: the camp of the living, its fence in (MissionState.Raiders).</summary>
        public const string Raiders = "raiders";

        public static readonly IReadOnlyList<string> All = new[] { Pitch, Trailer, Military, Toilets, FirePit, Raiders };
    }

    /// <summary>
    /// A camping's marks (H9 §5, Arte's sizes of 17/11): tents (dome 2 × 2, long 2 × 3, military 4 × 4 or 6 × 6, the
    /// quarantine one 4 × 5), each a block of `low` tiles that covers (a military one `high`), its mark on its first tile;
    /// `trailer` on a caravan's first tile out of doors (its room a building).
    /// </summary>
    public static class CampDetailKinds
    {
        public const string TentDome = "tentDome";
        public const string TentLong = "tentLong";
        public const string TentMilitary = "tentMilitary";
        public const string Trailer = "trailer";
        public const string FirePit = "firePit";
        public const string CampChair = "campChair";
        public const string SleepingBag = "sleepingBag";
        public const string ToiletPortable = "toiletPortable";
        public const string Clothesline = "clothesline";
        public const string Cooler = "cooler";
        public const string CorpseCamper = "corpseCamper";
        /// <summary>Inside a caravan (Synty's, Arte's template of 17/11): on low tiles, a piece each; the bed and a booth table may be two tiles (InteriorDetail.Length).</summary>
        public const string TrailerBed = "trailerBed";
        public const string TrailerTable = "trailerTable";
        public const string TrailerCounter = "trailerCounter";
        public const string TrailerDrawers = "trailerDrawers";
        /// <summary>
        /// G23.5 Saqueadores' fence (Arte, 20/11): a `low` tile of it, no decor; facing the outer side. A corner's, its first
        /// outer side, the other one after it (Facing + 1: north-east 0, south-east 1, south-west 2, north-west 3).
        /// </summary>
        public const string Barricade = "barricade";
        public const string BarricadeCorner = "barricadeCorner";
        /// <summary>G23.5: a crate of the raiders' stash, on a `low` tile, facing the camp's middle; searched one by one (SearchPieces.StashCrate).</summary>
        public const string StashCrate = "stashCrate";

        public static readonly IReadOnlyList<string> All = new[] { TentDome, TentLong, TentMilitary, Trailer, FirePit, CampChair, SleepingBag, ToiletPortable, Clothesline, Cooler, CorpseCamper, TrailerBed, TrailerTable, TrailerCounter, TrailerDrawers, Barricade, BarricadeCorner, StashCrate };
    }

    /// <summary>A room of an InteriorSite: its rect (walls not included), its storey and its use (OfficeUses, ShedUses, PoliceUses, ShopUses, CampUses).</summary>
    public sealed class InteriorRoom
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Z { get; set; }
        public string Use { get; set; } = "";
    }

    /// <summary>
    /// A mark of an InteriorSite (OfficeDetailKinds, ShedDetailKinds): where Arte puts a model. On a tile of furniture (a
    /// `low` or `high` tile, what blocks or covers is the tile's) or on the floor (scenery: it never blocks).
    /// </summary>
    public sealed class InteriorDetail
    {
        public string Kind { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        /// <summary>A DIRS4 index.</summary>
        public int Facing { get; set; }
        public int? Variant { get; set; }
        /// <summary>
        /// How many tiles it covers from its own (X, Y), along the line across its facing (facing north or south, along x;
        /// east or west, along y): an office's meeting table, 2 (Arte, 17/11: Office's are 4 m long). Null: one.
        /// </summary>
        public int? Length { get; set; }
    }

    /// <summary>
    /// A map's building or place of H9: a map that is one building (MissionState.Inside), or a shopping plaza (its bounds
    /// the square and its shops round it, its rooms the shops): its bounds (walls included), its rooms and its marks.
    /// </summary>
    public sealed class InteriorSite
    {
        public string Kind { get; set; } = ""; // InteriorKinds
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public List<InteriorRoom> Rooms { get; set; } = new List<InteriorRoom>();
        public List<InteriorDetail> Dressing { get; set; } = new List<InteriorDetail>();

        /// <summary>The use of the room `p` stands in on any site of `s` (the smallest that holds it); null in none.</summary>
        public static string? UseAt(GameState s, Pos p)
        {
            InteriorRoom? best = null;
            foreach (var site in s.Interiors ?? new List<InteriorSite>())
            {
                foreach (var r in site.Rooms)
                {
                    if (r.Z != p.Z || p.X < r.X || p.Y < r.Y || p.X >= r.X + r.W || p.Y >= r.Y + r.H) continue;
                    if (best == null || r.W * r.H < best.W * best.H) best = r;
                }
            }
            return best?.Use;
        }

        /// <summary>A copy of it, nothing shared (Setup, Events.CloneState).</summary>
        public InteriorSite Copy()
        {
            var rooms = new List<InteriorRoom>();
            foreach (var r in Rooms) rooms.Add(new InteriorRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = r.Z, Use = r.Use });
            var dressing = new List<InteriorDetail>();
            foreach (var d in Dressing) dressing.Add(new InteriorDetail { Kind = d.Kind, X = d.X, Y = d.Y, Z = d.Z, Facing = d.Facing, Variant = d.Variant, Length = d.Length });
            return new InteriorSite { Kind = Kind, X = X, Y = Y, W = W, H = H, Rooms = rooms, Dressing = dressing };
        }
    }
}
